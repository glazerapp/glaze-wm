using System.Linq;
using GlazeWM.Domain.Common.Enums;
using GlazeWM.Domain.Common.Utils;
using GlazeWM.Domain.Containers;
using GlazeWM.Domain.Containers.Commands;
using GlazeWM.Domain.Containers.Events;
using GlazeWM.Domain.Monitors;
using GlazeWM.Domain.Windows.Commands;
using GlazeWM.Domain.Workspaces;
using GlazeWM.Infrastructure.Bussing;
using GlazeWM.Infrastructure.WindowsApi;
using GlazeWM.Infrastructure.WindowsApi.Events;
using Microsoft.Extensions.Logging;

namespace GlazeWM.Domain.Windows.EventHandlers
{
  internal class WindowMovedOrResizedHandler : IEventHandler<WindowMovedOrResizedEvent>
  {
    private readonly Bus _bus;
    private readonly WindowService _windowService;
    private readonly MonitorService _monitorService;
    private readonly ContainerService _containerService;
    private readonly ILogger<WindowMovedOrResizedHandler> _logger;

    public WindowMovedOrResizedHandler(
      Bus bus,
      WindowService windowService,
      MonitorService monitorService,
      ILogger<WindowMovedOrResizedHandler> logger,
      ContainerService containerService
    )
    {
      _bus = bus;
      _windowService = windowService;
      _monitorService = monitorService;
      _logger = logger;
      _containerService = containerService;
    }

    public void Handle(WindowMovedOrResizedEvent @event)
    {
      var window = _windowService.GetWindows()
        .FirstOrDefault(window => window.Hwnd == @event.WindowHandle);

      if (window is null)
        return;

      _logger.LogWindowEvent("Window moved/resized", window);

      if (window is TilingWindow)
      {
        UpdateTilingWindow(window as TilingWindow);
        return;
      }

      if (window is FloatingWindow)
        UpdateFloatingWindow(window as FloatingWindow);
    }

    private void UpdateTilingWindow(TilingWindow window)
    {
      // Snap window to its original position even if it's not being resized.
      var hasNoResizableSiblings = window.Parent is Workspace
        && !window.SiblingsOfType(typeof(IResizable)).Any();

      if (hasNoResizableSiblings)
      {
        _containerService.ContainersToRedraw.Add(window);
        _bus.Invoke(new RedrawContainersCommand());
        return;
      }

      // Remove invisible borders from current placement to be able to compare window width/height.
      var currentPlacement = WindowService.GetPlacementOfHandle(window.Hwnd).NormalPosition;
      var adjustedPlacement = new WindowRect
      {
        Left = currentPlacement.Left + window.BorderDelta.DeltaLeft,
        Right = currentPlacement.Right - window.BorderDelta.DeltaRight,
        Top = currentPlacement.Top + window.BorderDelta.DeltaTop,
        Bottom = currentPlacement.Bottom - window.BorderDelta.DeltaBottom,
      };

      var deltaWidth = adjustedPlacement.Width - window.Width;
      var deltaHeight = adjustedPlacement.Height - window.Height;

      // TODO: Avoid unnecessary resize call if either delta is 0.
      _bus.Invoke(new ResizeWindowCommand(window, ResizeDimension.WIDTH, $"{deltaWidth}px"));
      _bus.Invoke(new ResizeWindowCommand(window, ResizeDimension.HEIGHT, $"{deltaHeight}px"));
    }

    private void UpdateFloatingWindow(FloatingWindow window)
    {
      // Update state with new location of the floating window.
      window.FloatingPlacement = WindowService.GetPlacementOfHandle(window.Hwnd).NormalPosition;

      // Change floating window's parent workspace if out of its bounds.
      UpdateParentWorkspace(window);
    }

    private void UpdateParentWorkspace(Window window)
    {
      var currentWorkspace = WorkspaceService.GetWorkspaceFromChildContainer(window);

      // Get workspace that encompasses most of the window.
      var targetMonitor = _monitorService.GetMonitorFromHandleLocation(window.Hwnd);
      var targetWorkspace = targetMonitor.DisplayedWorkspace;

      // Ignore if window is still within the bounds of its current workspace.
      if (currentWorkspace == targetWorkspace)
        return;

      // Change the window's parent workspace.
      _bus.Invoke(new MoveContainerWithinTreeCommand(window, targetWorkspace, false));
      _bus.RaiseEvent(new FocusChangedEvent(window));
    }
  }
}
