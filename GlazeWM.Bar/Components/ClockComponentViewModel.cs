﻿using System;
using System.Globalization;
using System.Reactive.Linq;
using System.Text;
using GlazeWM.Domain.UserConfigs;

namespace GlazeWM.Bar.Components
{
  public class ClockComponentViewModel : ComponentViewModel
  {
    private ClockComponentConfig _config => _componentConfig as ClockComponentConfig;
    private string _timeFormatting => formatCalendarWeek(_config.TimeFormatting);

    /// <summary>
    /// Format the current time with the user's formatting config.
    /// </summary>
    public string FormattedTime => DateTime.Now.ToString(_timeFormatting, CultureInfo.InvariantCulture);


    private String formatCalendarWeek(string timeFormat)
    {

      if (!timeFormat.Contains('w')) return timeFormat;

      var now = DateTime.Now;
      var i = 0;
      var result = new StringBuilder();

      while (i < timeFormat.Length)
      {
        var nextChar = i + 1 < timeFormat.Length ? timeFormat[i + 1].ToString() : "";
        switch (timeFormat[i])
        {
          case '\\':
            {
              result.Append(timeFormat[i]);
              result.Append(nextChar);
              i = i + 2;
              break;
            }
          case 'w':
            {
              if (nextChar.Equals("w"))
              {
                i = i + 2;
                result.Append(ISOWeek.GetWeekOfYear(now).ToString("00"));
              }
              else
              {
                i++;
                result.Append(ISOWeek.GetWeekOfYear(now).ToString());
              }
              break;
            }
          default:
            {
              result.Append(timeFormat[i]);
              i++;
              break;
            }
        }
      }
      return result.ToString();
    }

    public ClockComponentViewModel(
      BarViewModel parentViewModel,
      ClockComponentConfig config) : base(parentViewModel, config)
    {
      // Update the displayed time every second.
      var updateInterval = TimeSpan.FromSeconds(1);

      Observable.Interval(updateInterval)
        .Subscribe(_ => OnPropertyChanged(nameof(FormattedTime)));
    }
  }
}
