use anyhow::Context;

use crate::{
  common::{platform::Platform, DisplayState},
  containers::{
    traits::{CommonGetters, PositionGetters},
    Container, WindowContainer,
  },
  user_config::UserConfig,
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
    true => state.windows(),
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

pub fn sync_focus(
  focused_container: Container,
  state: &mut WmState,
) -> anyhow::Result<()> {
  // Set focus to the given window handle. If the container is a normal
  // window, then this will trigger a `PlatformEvent::WindowFocused` event.
  match focused_container.as_window_container() {
    Ok(window) => {
      if Platform::foreground_window() != *window.native() {
        let _ = window.native().set_foreground();
      }
    }
    _ => {
      let desktop_window = Platform::desktop_window();

      if Platform::foreground_window() != desktop_window {
        let _ = desktop_window.set_foreground();
      }
    }
  };

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

    let rect = window.to_rect()?.apply_delta(&window.border_delta());

    let _ = window.native().set_position(
      &window.state(),
      &window.display_state(),
      &rect,
      window.has_pending_dpi_adjustment(),
    );
  }

  Ok(())
}

fn apply_window_effects(
  window: WindowContainer,
  is_focused: bool,
  config: &UserConfig,
) {
  // TODO: Be able to add transparency to windows.

  let enable_borders = match is_focused {
    true => config.value.window_effects.focused_window.border.enabled,
    false => config.value.window_effects.other_windows.border.enabled,
  };

  if enable_borders {
    let border_config = match is_focused {
      true => &config.value.window_effects.focused_window.border,
      false => &config.value.window_effects.other_windows.border,
    }
    .clone();

    _ = window.native().set_border_color(Some(&border_config.color));
  }
}