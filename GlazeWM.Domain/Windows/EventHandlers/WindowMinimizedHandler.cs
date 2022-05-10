using System.Linq;
using GlazeWM.Domain.Containers;
using GlazeWM.Domain.Containers.Commands;
using GlazeWM.Domain.Windows.Commands;
using GlazeWM.Domain.Workspaces;
using GlazeWM.Domain.Workspaces.Commands;
using GlazeWM.Infrastructure.Bussing;
using GlazeWM.Infrastructure.WindowsApi.Events;

namespace GlazeWM.Domain.Windows.EventHandlers
{
  internal class WindowMinimizedHandler : IEventHandler<WindowMinimizedEvent>
  {
    private readonly Bus _bus;
    private readonly WindowService _windowService;
    private readonly ContainerService _containerService;

    public WindowMinimizedHandler(
      Bus bus,
      WindowService windowService,
      ContainerService containerService
    )
    {
      _bus = bus;
      _windowService = windowService;
      _containerService = containerService;
    }

    public void Handle(WindowMinimizedEvent @event)
    {
      var window = _windowService.GetWindows()
        .FirstOrDefault(window => window.Hwnd == @event.WindowHandle);

      if (window is null or MinimizedWindow)
        return;

      var workspace = WorkspaceService.GetWorkspaceFromChildContainer(window);

      // Move tiling windows to be direct children of workspace (in case they aren't already).
      if (window is TilingWindow)
        _bus.Invoke(new MoveContainerWithinTreeCommand(window, workspace, true));

      var previousState = WindowService.GetWindowType(window);
      var minimizedWindow = new MinimizedWindow(
        window.Hwnd,
        window.FloatingPlacement,
        window.BorderDelta,
        previousState
      );

      _bus.Invoke(new ReplaceContainerCommand(minimizedWindow, window.Parent, window.Index));

      var focusTarget = workspace.LastFocusedDescendantExcluding(minimizedWindow) ?? workspace;

      if (focusTarget is Window)
        _bus.Invoke(new FocusWindowCommand(focusTarget as Window));
      else if (focusTarget is Workspace)
        _bus.Invoke(new FocusWorkspaceCommand((focusTarget as Workspace).Name));

      _containerService.ContainersToRedraw.Add(workspace);
      _bus.Invoke(new RedrawContainersCommand());
    }
  }
}
