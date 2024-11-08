use anyhow::Context;
use tracing::info;

use crate::{
  common::{platform::NativeWindow, Rect},
  containers::{
    commands::{flatten_split_container, move_container_within_tree},
    traits::{CommonGetters, PositionGetters},
    WindowContainer,
  },
  try_warn,
  user_config::{FloatingStateConfig, FullscreenStateConfig, UserConfig},
  windows::{
    commands::update_window_state, traits::WindowGetters, ActiveDrag,
    ActiveDragOperation, TilingWindow, WindowState,
  },
  wm_state::WmState,
};

pub fn handle_window_location_changed(
  native_window: NativeWindow,
  state: &mut WmState,
  config: &UserConfig,
) -> anyhow::Result<()> {
  let found_window = state.window_from_native(&native_window);

  // Update the window's state to be fullscreen or toggled from fullscreen.
  if let Some(window) = found_window {
    let old_frame_position = window.native().frame_position()?;
    let frame_position =
      try_warn!(window.native().refresh_frame_position());

    let is_minimized = try_warn!(window.native().refresh_is_minimized());

    let old_is_maximized = window.native().is_maximized()?;
    let is_maximized = try_warn!(window.native().refresh_is_maximized());

    let nearest_monitor = state
      .nearest_monitor(&window.native())
      .context("Failed to get workspace of nearest monitor.")?;

    // TODO: Include this as part of the `match` statement below.
    if let Some(tiling_window) = window.as_tiling_window() {
      update_drag_state(
        tiling_window.clone(),
        &frame_position,
        &old_frame_position,
        state,
        config,
      )?;
    }

    let monitor_rect = if config.has_outer_gaps() {
      nearest_monitor.native().working_rect()?.clone()
    } else {
      nearest_monitor.to_rect()?
    };

    let is_fullscreen = window.native().is_fullscreen(&monitor_rect)?;

    match window.state() {
      WindowState::Fullscreen(fullscreen_state) => {
        // A fullscreen window that gets minimized can hit this arm, so
        // ignore such events and let it be handled by the handler for
        // `PlatformEvent::WindowMinimized` instead.
        if !(is_fullscreen || is_maximized) && !is_minimized {
          info!("Window restored");

          let target_state = window
            .prev_state()
            .unwrap_or(WindowState::default_from_config(config));

          update_window_state(
            window.clone(),
            target_state,
            state,
            config,
          )?;
        } else if is_maximized != old_is_maximized {
          info!("Updating window's fullscreen state.");

          update_window_state(
            window.clone(),
            WindowState::Fullscreen(FullscreenStateConfig {
              maximized: is_maximized,
              ..fullscreen_state
            }),
            state,
            config,
          )?;
        }
      }
      _ => {
        // Update the window to be fullscreen if there's been a change in
        // maximized state or if the window is now fullscreen.
        if (is_maximized && old_is_maximized != is_maximized)
          || is_fullscreen
        {
          info!("Window fullscreened");

          update_window_state(
            window,
            WindowState::Fullscreen(FullscreenStateConfig {
              maximized: is_maximized,
              ..config.value.window_behavior.state_defaults.fullscreen
            }),
            state,
            config,
          )?;

        // A floating window that gets minimized can hit this arm, so
        // ignore such events and let it be handled by the handler for
        // `PlatformEvent::WindowMinimized` instead.
        } else if !is_minimized
          && matches!(window.state(), WindowState::Floating(_))
        {
          // Update state with the new location of the floating window.
          info!("Updating floating window position.");
          window.set_floating_placement(frame_position);

          let monitor = window.monitor().context("No monitor.")?;

          // Update the window's workspace if it goes out of bounds of its
          // current workspace.
          if monitor.id() != nearest_monitor.id() {
            let updated_workspace = nearest_monitor
              .displayed_workspace()
              .context("Failed to get workspace of nearest monitor.")?;

            info!(
              "Floating window moved to new workspace: '{}'.",
              updated_workspace.config().name
            );

            if let WindowContainer::NonTilingWindow(window) = &window {
              window.set_insertion_target(None);
            }

            move_container_within_tree(
              window.into(),
              updated_workspace.clone().into(),
              updated_workspace.child_count(),
              state,
            )?;
          }
        }
      }
    }
  }

  Ok(())
}

/// Updates the window operation based on changes in frame position.
///
/// This function determines whether a window is being moved or resized and
/// updates its operation state accordingly. If the window is being moved,
/// it's set to floating mode.
fn update_drag_state(
  window: TilingWindow,
  frame_position: &Rect,
  old_frame_position: &Rect,
  state: &mut WmState,
  config: &UserConfig,
) -> anyhow::Result<()> {
  if let Some(active_drag) = window.active_drag() {
    let should_ignore = active_drag.operation.is_some()
      || frame_position == old_frame_position;

    if should_ignore {
      return Ok(());
    }

    let is_move = frame_position.height() == old_frame_position.height()
      && frame_position.width() == old_frame_position.width();

    let operation = match is_move {
      true => ActiveDragOperation::Moving,
      false => ActiveDragOperation::Resizing,
    };

    window.set_active_drag(Some(ActiveDrag {
      operation: Some(operation),
      ..active_drag
    }));

    // Transition window to be floating while it's being dragged.
    if is_move {
      let parent = window.parent().context("No parent")?;

      let window = update_window_state(
        window.clone().into(),
        WindowState::Floating(FloatingStateConfig {
          centered: false,
          ..config.value.window_behavior.state_defaults.floating
        }),
        state,
        config,
      )?;

      // Windows are added for redraw on state changes, so here we need to
      // remove the window from the pending redraw.
      state
        .pending_sync
        .containers_to_redraw
        .retain(|container| container.id() != window.id());

      // Flatten the parent split container if it only contains the window.
      if let Some(split_parent) = parent.as_split() {
        if split_parent.child_count() == 1 {
          flatten_split_container(split_parent.clone())?;

          // Hacky fix to redraw siblings after flattening. The parent is
          // queued for redraw from the state change, which gets detached
          // on flatten.
          state
            .pending_sync
            .containers_to_redraw
            .extend(window.tiling_siblings().map(Into::into));
        }
      }
    }
  }

  Ok(())
}
