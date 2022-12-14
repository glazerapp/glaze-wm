﻿using System;
using System.Collections.Generic;
using System.Windows.Threading;
using GlazeWM.Bar.Common;
using GlazeWM.Bar.Components;
using GlazeWM.Domain.Monitors;
using GlazeWM.Domain.UserConfigs;
using GlazeWM.Infrastructure;

namespace GlazeWM.Bar
{
  public class BarViewModel : ViewModelBase
  {
    public Dispatcher Dispatcher { get; set; }
    public Monitor Monitor { get; set; }

    private readonly UserConfigService _userConfigService =
      ServiceLocator.GetRequiredService<UserConfigService>();
    private BarConfig _barConfig => _userConfigService.BarConfig;

    public BarPosition Position => _barConfig.Position;
    public string Background => XamlHelper.FormatColor(_barConfig.Background);
    public string Foreground => XamlHelper.FormatColor(_barConfig.Foreground);
    public string FontFamily => _barConfig.FontFamily;
    public string FontWeight => _barConfig.FontWeight;
    public string FontSize => _barConfig.FontSize;
    public string BorderColor => XamlHelper.FormatColor(_barConfig.BorderColor);
    public string BorderWidth => XamlHelper.FormatRectShorthand(_barConfig.BorderWidth);
    public string Padding => XamlHelper.FormatRectShorthand(_barConfig.Padding);
    public double Opacity => _barConfig.Opacity;

    public List<ComponentViewModel> ComponentsLeft =>
      CreateComponentViewModels(_barConfig.ComponentsLeft);

    public List<ComponentViewModel> ComponentsCenter =>
      CreateComponentViewModels(_barConfig.ComponentsCenter);

    public List<ComponentViewModel> ComponentsRight =>
      CreateComponentViewModels(_barConfig.ComponentsRight);

    private List<ComponentViewModel> CreateComponentViewModels(
      List<BarComponentConfig> componentConfigs)
    {
      return componentConfigs.ConvertAll<ComponentViewModel>(config => config switch
      {
        WorkspacesComponentConfig wcc => new WorkspacesComponentViewModel(this, wcc),
        ClockComponentConfig ccc => new ClockComponentViewModel(this, ccc),
        TextComponentConfig tcc => new TextComponentViewModel(this, tcc),
        _ => throw new ArgumentOutOfRangeException(nameof(config)),
      });
    }
  }
}
