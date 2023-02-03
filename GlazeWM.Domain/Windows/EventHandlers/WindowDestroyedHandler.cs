using System.Linq;
using GlazeWM.Domain.Common.Utils;
using GlazeWM.Domain.Monitors.Commands;
using GlazeWM.Domain.Windows.Commands;
using GlazeWM.Infrastructure.Bussing;
using GlazeWM.Infrastructure.Common.Events;
using Microsoft.Extensions.Logging;

namespace GlazeWM.Domain.Windows.EventHandlers
{
  internal sealed class WindowDestroyedHandler : IEventHandler<WindowDestroyedEvent>
  {
    private readonly Bus _bus;
    private readonly WindowService _windowService;
    private readonly ILogger<WindowDestroyedHandler> _logger;

    public WindowDestroyedHandler(
      Bus bus,
      WindowService windowService,
      ILogger<WindowDestroyedHandler> logger)
    {
      _bus = bus;
      _windowService = windowService;
      _logger = logger;
    }

    public void Handle(WindowDestroyedEvent @event)
    {
      var windowHandle = @event.WindowHandle;

      if (_windowService.AppBarHandles.Contains(windowHandle))
      {
        _windowService.AppBarHandles.Remove(windowHandle);
        _bus.Invoke(new RefreshMonitorStateCommand());
        return;
      }

      var window = _windowService.GetWindows()
        .FirstOrDefault(window => window.Handle == windowHandle);

      if (window == null)
        return;

      _logger.LogWindowEvent("Window closed", window);

      // If window is in tree, detach the removed window from its parent.
      _bus.Invoke(new UnmanageWindowCommand(window));
    }
  }
}
