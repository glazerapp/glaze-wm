﻿using System;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using GlazeWM.Infrastructure.Bussing;
using GlazeWM.Infrastructure.Common.Commands;

namespace GlazeWM.Infrastructure.WindowsApi
{
  public class SystemTrayService
  {
    private readonly Bus _bus;
    private NotifyIcon _notifyIcon { get; set; }

    public SystemTrayService(Bus bus)
    {
      _bus = bus;
    }

    public void AddToSystemTray()
    {
      var contextMenuStrip = new ContextMenuStrip();
      contextMenuStrip.Items.Add("Exit", null, ExitApplication);

      var assembly = Assembly.GetEntryAssembly();
      const string iconResourceName = "GlazeWM.Bootstrapper.icon.ico";

      // Get the embedded icon resource from the entry assembly.
      using var stream = assembly.GetManifestResourceStream(iconResourceName);

      _notifyIcon = new NotifyIcon
      {
        Icon = new Icon(stream),
        ContextMenuStrip = contextMenuStrip,
        Text = "GlazeWM",
        Visible = true
      };
    }

    private void ExitApplication(object sender, EventArgs e)
    {
      _bus.Invoke(new ExitApplicationCommand(false));
    }

    public void RemoveFromSystemTray()
    {
      _notifyIcon?.Dispose();
    }
  }
}
