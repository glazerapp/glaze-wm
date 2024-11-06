use anyhow::Context;
use tracing::info;

use crate::{
  containers::{
    commands::{move_container_within_tree, set_focused_descendant},
    traits::{CommonGetters, PositionGetters},
    WindowContainer,
  },
  user_config::UserConfig,
  windows::{traits::WindowGetters, WindowState},
  wm_state::WmState,
  workspaces::{commands::activate_workspace, WorkspaceTarget},
};

pub fn move_all_window_to_workspace(
  window: WindowContainer,
  target: WorkspaceTarget,
  state: &mut WmState,
  config: &UserConfig,
) -> anyhow::Result<()> {
  let current_workspace = window.workspace().context("No workspace.")?;
  let _ = current_workspace
    .children()
    .into_iter()
    .try_for_each(|child| {
      let child_container = child.as_window_container()?;
      move_window_to_workspace(
        child_container,
        target.clone(),
        state,
        config,
      )
    });
  Ok(())
}

pub fn move_window_to_workspace(
  window: WindowContainer,
  target: WorkspaceTarget,
  state: &mut WmState,
  config: &UserConfig,
) -> anyhow::Result<()> {
  let current_workspace = window.workspace().context("No workspace.")?;
  let current_monitor =
    current_workspace.monitor().context("No monitor.")?;

  let (target_workspace_name, target_workspace) =
    state.workspace_by_target(&current_workspace, target, config)?;

  // Retrieve or activate the target workspace by its name.
  let target_workspace = match target_workspace {
    Some(_) => anyhow::Ok(target_workspace),
    _ => match target_workspace_name {
      Some(name) => {
        activate_workspace(Some(&name), None, state, config)?;

        Ok(state.workspace_by_name(&name))
      }
      _ => Ok(None),
    },
  }?;

  if let Some(target_workspace) = target_workspace {
    if target_workspace.id() == current_workspace.id() {
      return Ok(());
    }

    info!(
      "Moving window to workspace: '{}'.",
      target_workspace.config().name
    );

    let target_monitor =
      target_workspace.monitor().context("No monitor.")?;

    // Since target workspace could be on a different monitor, adjustments
    // might need to be made because of DPI.
    if current_monitor
      .has_dpi_difference(&target_monitor.clone().into())?
    {
      window.set_has_pending_dpi_adjustment(true);
    }

    // Update floating placement if the window has to cross monitors.
    if target_monitor.id() != current_monitor.id() {
      window.set_floating_placement(
        window
          .floating_placement()
          .translate_to_center(&target_workspace.to_rect()?),
      );
    }

    if let WindowContainer::NonTilingWindow(window) = &window {
      window.set_insertion_target(None);
    }

    // Focus target is `None` if the window is not focused.
    let focus_target = state.focus_target_after_removal(&window);

    let focus_reset_target = match target_workspace.is_displayed() {
      true => None,
      false => target_monitor.descendant_focus_order().next(),
    };

    let insertion_sibling = target_workspace
      .descendant_focus_order()
      .filter_map(|descendant| descendant.as_window_container().ok())
      .find(|descendant| descendant.state() == WindowState::Tiling);

    // Insert the window into the target workspace.
    match (window.is_tiling_window(), insertion_sibling.is_some()) {
      (true, true) => {
        if let Some(insertion_sibling) = insertion_sibling {
          move_container_within_tree(
            window.clone().into(),
            insertion_sibling.clone().parent().context("No parent.")?,
            insertion_sibling.index() + 1,
            state,
          )?;
        }
      }
      _ => {
        move_container_within_tree(
          window.clone().into(),
          target_workspace.clone().into(),
          target_workspace.child_count(),
          state,
        )?;
      }
    }

    // When moving a focused window within the tree to another workspace,
    // the target workspace will get displayed. If moving the window e.g.
    // from monitor 1 -> 2, and the target workspace is hidden on that
    // monitor, we want to reset focus to the workspace that was displayed
    // on that monitor.
    if let Some(focus_reset_target) = focus_reset_target {
      set_focused_descendant(focus_reset_target, None);
      state.pending_sync.focus_change = true;
    }

    // Retain focus within the workspace from where the window was moved.
    if let Some(focus_target) = focus_target {
      set_focused_descendant(focus_target, None);
      state.pending_sync.focus_change = true;
    }

    let containers_to_redraw = match window {
      WindowContainer::NonTilingWindow(_) => vec![window.into()],
      WindowContainer::TilingWindow(_) => current_workspace
        .tiling_children()
        .chain(target_workspace.tiling_children())
        .map(Into::into)
        .collect(),
    };

    state
      .pending_sync
      .containers_to_redraw
      .extend(containers_to_redraw);
  }

  Ok(())
}
