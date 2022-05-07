﻿using GlazeWM.Infrastructure.Bussing;
using GlazeWM.Infrastructure.WindowsApi.Enums;
using GlazeWM.Infrastructure.WindowsApi.Events;
using System;
using System.Threading;
using System.Windows.Forms;
using static GlazeWM.Infrastructure.WindowsApi.WindowsApiService;

namespace GlazeWM.Infrastructure.WindowsApi
{
  public class WindowEventService
  {
    private readonly Bus _bus;
    private const int CHILDID_SELF = 0;

    public WindowEventService(Bus bus)
    {
      _bus = bus;
    }

    public void Start()
    {
      var thread = new Thread(() => CreateWindowEventHook());
      thread.Name = "GlazeWMWindowHooks";
      thread.Start();
    }

    private void CreateWindowEventHook()
    {
      SetWinEventHook(EventConstant.EVENT_OBJECT_LOCATIONCHANGE, EventConstant.EVENT_OBJECT_LOCATIONCHANGE, IntPtr.Zero, WindowEventHookProc, 0, 0, 0);
      SetWinEventHook(EventConstant.EVENT_OBJECT_DESTROY, EventConstant.EVENT_OBJECT_HIDE, IntPtr.Zero, WindowEventHookProc, 0, 0, 0);
      SetWinEventHook(EventConstant.EVENT_SYSTEM_MINIMIZESTART, EventConstant.EVENT_SYSTEM_MINIMIZEEND, IntPtr.Zero, WindowEventHookProc, 0, 0, 0);
      SetWinEventHook(EventConstant.EVENT_SYSTEM_MOVESIZEEND, EventConstant.EVENT_SYSTEM_MOVESIZEEND, IntPtr.Zero, WindowEventHookProc, 0, 0, 0);
      SetWinEventHook(EventConstant.EVENT_SYSTEM_FOREGROUND, EventConstant.EVENT_SYSTEM_FOREGROUND, IntPtr.Zero, WindowEventHookProc, 0, 0, 0);

      // `SetWinEventHook` requires a message loop within the thread that is executing the code.
      Application.Run();
    }

    private void WindowEventHookProc(IntPtr hWinEventHook, EventConstant eventType, IntPtr hwnd, ObjectIdentifier idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
    {
      // Whether the event is actually associated with a window object (instead of a UI control).
      var isWindowEvent = idChild == CHILDID_SELF && idObject == ObjectIdentifier.OBJID_WINDOW
        && hwnd != IntPtr.Zero;

      if (!isWindowEvent)
        return;

      switch (eventType)
      {
        case EventConstant.EVENT_OBJECT_LOCATIONCHANGE:
          _bus.RaiseEvent(new WindowLocationChangedEvent(hwnd));
          break;
        case EventConstant.EVENT_SYSTEM_FOREGROUND:
          _bus.RaiseEvent(new WindowFocusedEvent(hwnd));
          break;
        case EventConstant.EVENT_SYSTEM_MINIMIZESTART:
          _bus.RaiseEvent(new WindowMinimizedEvent(hwnd));
          break;
        case EventConstant.EVENT_SYSTEM_MINIMIZEEND:
          _bus.RaiseEvent(new WindowMinimizeEndedEvent(hwnd));
          break;
        case EventConstant.EVENT_SYSTEM_MOVESIZEEND:
          _bus.RaiseEvent(new WindowMovedOrResizedEvent(hwnd));
          break;
        case EventConstant.EVENT_OBJECT_DESTROY:
          _bus.RaiseEvent(new WindowDestroyedEvent(hwnd));
          break;
        case EventConstant.EVENT_OBJECT_SHOW:
          _bus.RaiseEvent(new WindowShownEvent(hwnd));
          break;
        case EventConstant.EVENT_OBJECT_HIDE:
          _bus.RaiseEvent(new WindowHiddenEvent(hwnd));
          break;
      }
    }
  }
}
