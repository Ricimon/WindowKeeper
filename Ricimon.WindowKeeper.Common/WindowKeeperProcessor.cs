using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using Win32Interop.WinHandles;
using Ricimon.WindowKeeper.Common.Diagnostics;
using Ricimon.WindowKeeper.Common.WinApiBridge;
using Ricimon.WindowKeeper.Common.Models;

namespace Ricimon.WindowKeeper.Common
{
    public partial class WindowKeeperProcessor : IDisposable
    {
        private readonly IDictionary<WinEventHook.SWEH_Events, WinEventHook.WinEventDelegate> _eventDelegates;

        static GCHandle GCSafetyHandle;

        private readonly IDictionary<IntPtr, TrackedWindow> _trackedWindows = new Dictionary<IntPtr, TrackedWindow>();

        public WindowKeeperProcessor()
        {
            // These events actually fire ridiculously often, so maybe there's a way reduce the number of calls?
            // All of these events fire when a mouse-movement or cursor blink happens over a window.
            // As a result a filter is applied to these event fires to discard a large majority of them.
            _eventDelegates = new Dictionary<WinEventHook.SWEH_Events, WinEventHook.WinEventDelegate>
            {
                // DESTROY/HIDE are taken care of by ShellEvents
                { WinEventHook.SWEH_Events.EVENT_OBJECT_LOCATIONCHANGE, new WinEventHook.WinEventDelegate(WinEventCallback_OBJECT_LOCATIONCHANGE) },
                //{ WinEventHook.SWEH_Events.EVENT_SYSTEM_FOREGROUND, new WinEventHook.WinEventDelegate(WinEventCallback_SYSTEM_FOREGROUND) },
            };
            foreach(var del in _eventDelegates.Values)
            {
                GCSafetyHandle = GCHandle.Alloc(del);
            }
        }

        public WindowKeeperProcessor Start()
        {
            Log.Info("WindowKeeper Processor Started");

            // Get all currently open windows and subscribe to their window change events
            var allWindows = TopLevelWindowUtils.FindWindows(wh => wh.IsVisible() && !string.IsNullOrEmpty(wh.GetWindowText()));
            foreach(var window in allWindows)
            {
                TrackWindow(window.RawPtr);
            }

            // Subscribe to any window created/destroyed events (ex. new window opens), and track the opened windows
            var sysProcHook = new SystemProcessHook();
            sysProcHook.OnWindowEvent += (IntPtr hWnd, SystemProcessHook.ShellEvent ev) =>
            {
                // No empty string checking here, as visible windows may open with an empty string name
                string windowName = WinEventHook.GetWindowText(hWnd);
                Log.Info($"{ev.ToString()} - {hWnd}: {windowName}");

                switch(ev)
                {
                    case SystemProcessHook.ShellEvent.HSHELL_WINDOWCREATED:
                        TrackWindow(hWnd);
                        break;
                    case SystemProcessHook.ShellEvent.HSHELL_WINDOWDESTROYED:
                        if (_trackedWindows.TryGetValue(hWnd, out var trackedWindow))
                        {
                            trackedWindow.hWinEventHooks.Select(h => WinEventHook.WinEventUnhook(h));
                            _trackedWindows.Remove(hWnd);
                        }
                        break;
                }
            };

            return this;
        }

        private void TrackWindow(IntPtr hWnd)
        {
            var trackedWindow = new TrackedWindow
            {
                Info = GetWindowInfo(hWnd)
            };

            WinEventHook.GetWindowPlacement(hWnd, out var placement);

            var rect = trackedWindow.Info.Rect;

            Log.Info($"{hWnd}: {WinEventHook.GetWindowText(hWnd)}\n\tstatus:{placement.windowStatus.ToString()}, WINDOWRECT top{rect.top}, left{rect.left}, right{rect.right}, bottom{rect.bottom}");

            // Subscribe to window updates
            uint threadId = NativeMethods.GetWindowThreadProcessId(hWnd, out uint processId);

            foreach (var ev in _eventDelegates)
            {
                trackedWindow.hWinEventHooks.Add(
                    WinEventHook.WinEventHookOne(ev.Key, ev.Value, processId, threadId));
            }

            _trackedWindows[hWnd] = trackedWindow;
        }

        protected void WinEventCallback_OBJECT_LOCATIONCHANGE(IntPtr hWinEventHook, WinEventHook.SWEH_Events eventType, IntPtr hWnd, WinEventHook.SWEH_ObjectId idObject, long idChild, uint dwEventThread, uint dwmsEventTime)
        {
            int windowTextLength = NativeMethods.GetWindowTextLength(hWnd);
            if (windowTextLength > 0)
            {
                if (WinEventHook.GetWindowRect(hWnd, out var rect)) // new rect is valid
                {
                    var windowInfo = GetWindowInfo(hWnd, rect);
                    if (_trackedWindows.TryGetValue(hWnd, out var trackedWindow) && trackedWindow.Info != windowInfo)   // new window info isn't the same as stored window info
                    {
                        trackedWindow.Info = windowInfo;

                        string windowName = WinEventHook.GetWindowText(hWnd, windowTextLength);
                        Log.Info($"{hWnd}: {windowName} status changed, new status: {windowInfo.WindowStatus.ToString()}, top{rect.top}, left{rect.left}, right{rect.right}, bottom{rect.bottom}");
                    }
                }
            }
        }

        protected void WinEventCallback_SYSTEM_FOREGROUND(IntPtr hWinEventHook, WinEventHook.SWEH_Events eventType, IntPtr hWnd, WinEventHook.SWEH_ObjectId idObject, long idChild, uint dwEventThread, uint dwmsEventTime)
        {
            int windowTextLength = NativeMethods.GetWindowTextLength(hWnd);
            if (windowTextLength > 0)
            {
                string windowName = WinEventHook.GetWindowText(hWnd, windowTextLength);
                Log.Info($"{hWnd}: {windowName} SYSTEM_FOREGROUND fired.");
            }
        }

        private WindowInfo GetWindowInfo(IntPtr hWnd, RECT? alreadyCalculatedRect = null)
        {
            WinEventHook.GetWindowPlacement(hWnd, out var placement);

            RECT rect;
            if (alreadyCalculatedRect.HasValue)
            {
                rect = alreadyCalculatedRect.Value;
            }
            else
            {
                WinEventHook.GetWindowRect(hWnd, out rect);
            }

            return new WindowInfo
            {
                WindowStatus = placement.windowStatus,
                Rect = rect
            };
        }

        public void Dispose()
        {
            foreach (var hook in _trackedWindows.Values.SelectMany(w => w.hWinEventHooks))
                WinEventHook.WinEventUnhook(hook);
            GCSafetyHandle.Free();
            Log.Info("Disposing WindowKeeper Processor");
        }
    }
}
