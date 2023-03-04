using System;
using GlazeWM.Domain.Common.Enums;
using GlazeWM.Domain.Containers;
using GlazeWM.Domain.Monitors;
using GlazeWM.Domain.UserConfigs;
using GlazeWM.Infrastructure;
using GlazeWM.Infrastructure.Utils;

namespace GlazeWM.Domain.Workspaces
{
  public sealed class Workspace : SplitContainer
  {
    public override string Id { get; init; }
    public string Name { get; set; }

    private readonly UserConfigService _userConfigService =
        ServiceLocator.GetRequiredService<UserConfigService>();

    private readonly WorkspaceService _workspaceService =
        ServiceLocator.GetRequiredService<WorkspaceService>();

    public string DisplayName =>
      _userConfigService.GetWorkspaceConfigByName(Name).DisplayName ?? Name;

    public bool KeepAlive =>
      _userConfigService.GetWorkspaceConfigByName(Name).KeepAlive;

    /// <summary>
    /// Get height of bar after it's been automatically adjusted by DPI scaling.
    /// </summary>
    private int _logicalBarHeight
    {
      get
      {
        var barHeight = UnitsHelper.TrimUnits(_userConfigService.GetBarConfigForMonitor(Parent as Monitor).Height);
        return Convert.ToInt32(barHeight * (Parent as Monitor).ScaleFactor);
      }
    }

    private int _yOffset
    {
      get
      {
        var barPosition = _userConfigService.GetBarConfigForMonitor(Parent as Monitor).Position;
        return barPosition == BarPosition.Top ? _logicalBarHeight : 0;
      }
    }

    private int _outerGap => _userConfigService.GapsConfig.OuterGap;

    private BarConfig barForMonitor => _userConfigService.GetBarConfigForMonitor(Parent as Monitor);
    private int floatBarOffsetY => UnitsHelper.TrimUnits(barForMonitor.OffsetY);

    public override int Height
    {
      get
      {
        if (!_userConfigService.GetBarConfigForMonitor(Parent as Monitor).Enabled)
        {
          return Parent.Height - (_outerGap * 2);
        }

        return Parent.Height - (_outerGap * 2) - floatBarOffsetY - _logicalBarHeight;
      }
    }

    public override int Width => Parent.Width - (_outerGap * 2);
    public override int X => Parent.X + _outerGap;
    public override int Y
    {
      get
      {
        if (!_userConfigService.GetBarConfigForMonitor(Parent as Monitor).Enabled)
        {
          return Parent.Y + _outerGap;
        }

        return Parent.Y + _outerGap + _yOffset + floatBarOffsetY;
      }
    }

    /// <summary>
    /// Whether the workspace itself or a descendant container has focus.
    /// </summary>
    public bool HasFocus => _workspaceService.GetFocusedWorkspace() == this;

    /// <summary>
    /// Whether the workspace is currently displayed by the parent monitor.
    /// </summary>
    public bool IsDisplayed => (Parent as Monitor)?.DisplayedWorkspace == this;

    public Workspace(string name, Layout layout)
    {
      Layout = layout;
      Id = $"WORKSPACE/{name}";
      Name = name;
    }
  }
}
