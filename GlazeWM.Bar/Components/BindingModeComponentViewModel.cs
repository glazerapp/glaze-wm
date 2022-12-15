using System;
using System.Reactive.Linq;
using System.Windows.Threading;
using GlazeWM.Domain.Containers;
using GlazeWM.Domain.Monitors.Events;
using GlazeWM.Domain.UserConfigs;
using GlazeWM.Infrastructure;
using GlazeWM.Infrastructure.Bussing;

namespace GlazeWM.Bar.Components
{
  public class BindingModeComponentViewModel : ComponentViewModel
  {
    private Dispatcher _dispatcher => _parentViewModel.Dispatcher;
    private readonly Bus _bus = ServiceLocator.GetRequiredService<Bus>();
    private readonly ContainerService _containerService =
     ServiceLocator.GetRequiredService<ContainerService>();

    /// <summary>
    /// Name of the currently active binding mode (if one is active).
    /// </summary>
    public string ActiveBindingMode => _containerService.ActiveBindingMode;

    public BindingModeComponentViewModel(
      BarViewModel parentViewModel,
      BindingModeComponentConfig config) : base(parentViewModel, config)
    {
      _bus.Events.OfType<BindingModeChangedEvent>().Subscribe(_ =>
        _dispatcher.Invoke(() => OnPropertyChanged(nameof(ActiveBindingMode)))
      );
    }
  }
}
