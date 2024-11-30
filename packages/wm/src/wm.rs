use anyhow::Context;
use tokio::sync::mpsc::{self};
use uuid::Uuid;

use crate::{
  app_command::InvokeCommand,
  common::{
    commands::platform_sync,
    events::{
      handle_display_settings_changed, handle_mouse_move,
      handle_window_destroyed, handle_window_focused,
      handle_window_hidden, handle_window_location_changed,
      handle_window_minimize_ended, handle_window_minimized,
      handle_window_moved_or_resized_end,
      handle_window_moved_or_resized_start, handle_window_shown,
      handle_window_title_changed,
    },
    platform::PlatformEvent,
  },
  user_config::UserConfig,
  wm_event::WmEvent,
  wm_state::WmState,
};

pub struct WindowManager {
  pub event_rx: mpsc::UnboundedReceiver<WmEvent>,
  pub exit_rx: mpsc::UnboundedReceiver<()>,
  pub state: WmState,
}

impl WindowManager {
  pub fn new(config: &mut UserConfig) -> anyhow::Result<Self> {
    let (event_tx, event_rx) = mpsc::unbounded_channel();
    let (exit_tx, exit_rx) = mpsc::unbounded_channel();

    let mut state = WmState::new(event_tx, exit_tx);
    state.populate(config)?;

    Ok(Self {
      event_rx,
      exit_rx,
      state,
    })
  }

  pub fn process_event(
    &mut self,
    event: PlatformEvent,
    config: &mut UserConfig,
  ) -> anyhow::Result<()> {
    let state = &mut self.state;

    match event {
      PlatformEvent::DisplaySettingsChanged => {
        handle_display_settings_changed(state, config)
      }
      PlatformEvent::KeybindingTriggered(kb_config) => {
        self.process_commands(kb_config.commands, None, config)?;

        // Return early since we don't want to redraw twice.
        return Ok(());
      }
      PlatformEvent::MouseMove(event) => {
        handle_mouse_move(event, state, config)
      }
      PlatformEvent::WindowDestroyed(window) => {
        handle_window_destroyed(window, state)
      }
      PlatformEvent::WindowFocused(window) => {
        handle_window_focused(window, state, config)
      }
      PlatformEvent::WindowHidden(window) => {
        handle_window_hidden(window, state)
      }
      PlatformEvent::WindowLocationChanged(window) => {
        handle_window_location_changed(window, state, config)
      }
      PlatformEvent::WindowMinimized(window) => {
        handle_window_minimized(window, state, config)
      }
      PlatformEvent::WindowMinimizeEnded(window) => {
        handle_window_minimize_ended(window, state, config)
      }
      PlatformEvent::WindowMovedOrResizedEnd(window) => {
        handle_window_moved_or_resized_end(window, state, config)
      }
      PlatformEvent::WindowMovedOrResizedStart(window) => {
        handle_window_moved_or_resized_start(window, state)
      }
      PlatformEvent::WindowShown(window) => {
        handle_window_shown(window, state, config)
      }
      PlatformEvent::WindowTitleChanged(window) => {
        handle_window_title_changed(window, state, config)
      }
    }?;

    platform_sync(state, config)
  }

  pub fn process_commands(
    &mut self,
    commands: Vec<InvokeCommand>,
    subject_container_id: Option<Uuid>,
    config: &mut UserConfig,
  ) -> anyhow::Result<Uuid> {
    let state = &mut self.state;

    // Get the container to run WM commands with.
    let subject_container = match subject_container_id {
      Some(id) => state.container_by_id(id).with_context(|| {
        format!("No container found with the given ID '{}'.", id)
      })?,
      None => state
        .focused_container()
        .context("No subject container for command.")?,
    };

    let new_subject_container_id = InvokeCommand::run_multiple(
      commands,
      subject_container,
      state,
      config,
    )?;

    platform_sync(state, config)?;

    Ok(new_subject_container_id)
  }
}
