use anyhow::Context;
use tracing::warn;

use crate::{
  common::{platform::Platform, DisplayState},
  containers::{
    traits::{CommonGetters, PositionGetters},
    Container, WindowContainer,
  },
  monitors::Monitor,
  user_config::{CursorJumpTrigger, UserConfig},
  windows::traits::WindowGetters,
  wm_event::WmEvent,
  wm_state::WmState,
};

pub fn platform_sync(
  state: &mut WmState,
  config: &UserConfig,
) -> anyhow::Result<()> {
  if state.pending_sync.containers_to_redraw.len() > 0 {
    redraw_containers(state)?;
    state.pending_sync.containers_to_redraw.clear();
  }

  let recent_focused_container = state.recent_focused_container.clone();
  let focused_container =
    state.focused_container().context("No focused container.")?;

  if state.pending_sync.cursor_jump {
    jump_cursor(focused_container.clone(), state, config)?;
    state.pending_sync.cursor_jump = false;
  }

  if state.pending_sync.focus_change {
    sync_focus(focused_container.clone(), state)?;
    state.pending_sync.focus_change = false;
  }

  if let Ok(window) = focused_container.as_window_container() {
    apply_window_effects(window, true, config);
  }

  // Get windows that should have the unfocused border applied to them.
  // For the sake of performance, we only update the border of the
  // previously focused window. If the `reset_window_effects` flag is
  // passed, the unfocused border is applied to all unfocused windows.
  let unfocused_windows = match state.pending_sync.reset_window_effects {
    true => state
      .root_container
      .descendants_of_type()
      .collect::<Vec<WindowContainer>>(),
    false => recent_focused_container
      .and_then(|container| container.as_window_container().ok())
      .into_iter()
      .collect(),
  }
  .into_iter()
  .filter(|window| window.id() != focused_container.id());

  for window in unfocused_windows {
    apply_window_effects(window, false, config);
  }

  state.pending_sync.reset_window_effects = false;

  Ok(())
}

fn sync_focus(
  focused_container: Container,
  state: &mut WmState,
) -> anyhow::Result<()> {
  let native_window = match focused_container.as_window_container() {
    Ok(window) => window.native().clone(),
    _ => Platform::desktop_window(),
  };

  // Set focus to the given window handle. If the container is a normal
  // window, then this will trigger a `PlatformEvent::WindowFocused` event.
  if Platform::foreground_window() != native_window {
    if let Err(err) = native_window.set_foreground() {
      warn!("Failed to set foreground window: {}", err);
    }
  }

  // TODO: Change z-index of workspace windows that match the focused
  // container's state. Make sure not to decrease z-index for floating
  // windows that are always on top.

  state.emit_event(WmEvent::FocusChanged {
    focused_container: focused_container.to_dto()?,
  });

  state.recent_focused_container = Some(focused_container);

  Ok(())
}

fn redraw_containers(state: &mut WmState) -> anyhow::Result<()> {
  for window in &state.windows_to_redraw() {
    let workspace =
      window.workspace().context("Window has no workspace.")?;

    // Transition display state depending on whether window will be
    // shown or hidden.
    window.set_display_state(
      match (window.display_state(), workspace.is_displayed()) {
        (DisplayState::Hidden | DisplayState::Hiding, true) => {
          DisplayState::Showing
        }
        (DisplayState::Shown | DisplayState::Showing, false) => {
          DisplayState::Hiding
        }
        _ => window.display_state(),
      },
    );

    let rect =
      window.to_rect()?.apply_delta(&window.total_border_delta()?);

    let is_visible = match window.display_state() {
      DisplayState::Showing | DisplayState::Shown => true,
      _ => false,
    };

    if let Err(err) = window.native().set_position(
      &window.state(),
      &rect,
      is_visible,
      window.has_pending_dpi_adjustment(),
    ) {
      warn!("Failed to set window position: {}", err);
    }
  }

  Ok(())
}

fn jump_cursor(
  focused_container: Container,
  state: &WmState,
  config: &UserConfig,
) -> anyhow::Result<()> {
  let cursor_jump = &config.value.general.cursor_jump;

  let jump_target = match cursor_jump.trigger {
    CursorJumpTrigger::WindowFocus => Some(focused_container),
    CursorJumpTrigger::MonitorFocus => {
      let target_monitor =
        focused_container.monitor().context("No monitor.")?;

      let cursor_monitor: Option<Monitor> = state
        .containers_at_position(&Platform::mouse_position()?)
        .into_iter()
        .next();

      cursor_monitor
        .filter(|monitor| monitor.id() != target_monitor.id())
        .map(Into::into)
    }
  };

  if let Some(jump_target) = jump_target {
    let center = jump_target.to_rect()?.center_point();

    if let Err(err) = Platform::set_cursor_pos(center.x, center.y) {
      warn!("Failed to set cursor position: {}", err);
    }
  }

  Ok(())
}

fn apply_window_effects(
  window: WindowContainer,
  is_focused: bool,
  config: &UserConfig,
) {
  // TODO: Be able to add transparency to windows.

  let window_effects = &config.value.window_effects;

  // Skip if both focused + non-focused window effects are disabled.
  if !window_effects.focused_window.border.enabled
    && !window_effects.other_windows.border.enabled
  {
    return;
  };

  let border_config = match is_focused {
    true => &config.value.window_effects.focused_window.border,
    false => &config.value.window_effects.other_windows.border,
  };

  let border_color = match border_config.enabled {
    true => Some(&border_config.color),
    false => None,
  };

  _ = window.native().set_border_color(border_color);
}
