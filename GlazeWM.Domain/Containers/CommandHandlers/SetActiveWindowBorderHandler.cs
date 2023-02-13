using System;
using System.Diagnostics;
using GlazeWM.Domain.Containers.Commands;
using GlazeWM.Domain.UserConfigs;
using GlazeWM.Domain.Windows;
using GlazeWM.Infrastructure.Bussing;
using static GlazeWM.Infrastructure.WindowsApi.WindowsApiService;

namespace GlazeWM.Domain.Containers.CommandHandlers
{
  internal sealed class SetActiveWindowBorderHandler : ICommandHandler<SetActiveWindowBorderCommand>
  {
    private readonly UserConfigService _userConfigService;
    private static Window _lastFocused;

    private uint rgbToUint(string rgb)
    {
      var c = rgb.ToCharArray();
      var bgr = string.Concat(c[5], c[6], c[3], c[4], c[1], c[2]);
      return Convert.ToUInt32(bgr, 16);
    }

    public SetActiveWindowBorderHandler(UserConfigService userConfigService)
    {
      _userConfigService = userConfigService;
    }

    public CommandResponse Handle(SetActiveWindowBorderCommand command)
    {
      uint BorderColorAttribute = 34;
      if (_lastFocused is not null)
      {
        uint defaultColor = 0xFFFFFFFF;
        // Clear old window border
        _ = DwmSetWindowAttribute(_lastFocused.Handle, BorderColorAttribute, ref defaultColor, 4);
      }

      var newWindowFocused = command.TargetWindow;
      if (newWindowFocused is null)
        return CommandResponse.Ok;

      _lastFocused = command.TargetWindow;
      // Set new window border
      var configColor = rgbToUint(_userConfigService.GeneralConfig.FocusBorderColor);
      _ = DwmSetWindowAttribute(_lastFocused.Handle, BorderColorAttribute, ref configColor, 4);
      return CommandResponse.Ok;
    }
  }
}
