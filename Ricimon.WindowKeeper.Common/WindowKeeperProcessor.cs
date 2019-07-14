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
using Microsoft.Win32;

namespace Ricimon.WindowKeeper.Common
{
    public partial class WindowKeeperProcessor : IDisposable
    {
        private readonly IDictionary<WinEventHook.SWEH_Events, WinEventHook.WinEventDelegate> _eventDelegates;
        static GCHandle GCSafetyHandle;

        // Saved states
        private enum State
        {
            SavingWindowPositions,
            WaitingForSavedMonitorSettingRestore
        }
        private State _state = State.SavingWindowPositions;
        private readonly IDictionary<IntPtr, TrackedWindow> _trackedWindows = new Dictionary<IntPtr, TrackedWindow>();
        private MonitorSetting _savedMonitorSetting;

        public WindowKeeperProcessor()
        {
            // These events actually fire ridiculously often, so maybe there's a way reduce the number of calls?
            // All of these events fire when a mouse-movement or cursor blink happens over a window.
            // As a result a filter is applied to these event fires to discard a large majority of them.
            _eventDelegates = new Dictionary<WinEventHook.SWEH_Events, WinEventHook.WinEventDelegate>
            {
                // DESTROY/HIDE are taken care of by ShellEvents
                // ShellEvents also capture windows created from windows, how nice!
                { WinEventHook.SWEH_Events.EVENT_OBJECT_LOCATIONCHANGE, new WinEventHook.WinEventDelegate(WinEventCallback_OBJECT_LOCATIONCHANGE) },
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

                switch(ev)
                {
                    case SystemProcessHook.ShellEvent.HSHELL_WINDOWCREATED:
                        if (_state == State.SavingWindowPositions)
                        {
                            string windowName = WinEventHook.GetWindowText(hWnd);
                            Log.Info($"{ev.ToString()} - {hWnd}: {windowName}");

                            TrackWindow(hWnd);
                        }
                        break;
                    case SystemProcessHook.ShellEvent.HSHELL_WINDOWDESTROYED:
                        if (_trackedWindows.TryGetValue(hWnd, out var trackedWindow))
                        {
                            string windowName = WinEventHook.GetWindowText(hWnd);
                            Log.Info($"{ev.ToString()} - {hWnd}: {windowName}");

                            trackedWindow.UnhookWinEvents();
                            _trackedWindows.Remove(hWnd);
                        }
                        break;
                }
            };

            // Do an initial monitor setting process to save current monitor settings
            // Window restoration logic lives here!
            ProcessCurrentMonitorSetting();

            // Detect monitor changes
            SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChangedCallback;

            // Detect system lock/unlock. This is necessary because display setting changes do not fire when the system is locked,
            // and DisplayPort monitors will disconnect when screen shutoff happens in the lock screen.
            SystemEvents.SessionSwitch += OnSessionSwitchCallback;

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

            Log.Info($"Tracking window {hWnd}: {WinEventHook.GetWindowText(hWnd)}\n" +
                $"\tstatus:{placement.windowStatus.ToString()}, top{rect.top}, left{rect.left}, right{rect.right}, bottom{rect.bottom}");

            // Subscribe to window updates
            uint threadId = NativeMethods.GetWindowThreadProcessId(hWnd, out uint processId);

            foreach (var ev in _eventDelegates)
            {
                trackedWindow.hWinEventHooks.Add(
                    WinEventHook.WinEventHookOne(ev.Key, ev.Value, processId, threadId));
            }

            _trackedWindows[hWnd] = trackedWindow;
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

        /// <summary>
        /// Compares current monitor setting with saved monitor setting, and according to current program state,
        /// potentially transition the state and restore window positions.
        /// </summary>
        private void ProcessCurrentMonitorSetting()
        {
            List<MonitorInfo> monitors = new List<MonitorInfo>();
            NativeMethods.MonitorEnumProc callback = (IntPtr hDesktop, IntPtr hdc, ref RECT prect, int d) =>
            {
                monitors.Add(new MonitorInfo
                {
                    Rect = prect
                });
                return true;
            };
            if (NativeMethods.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, callback, 0))
            {
                if (_savedMonitorSetting == null)
                {
                    _savedMonitorSetting = new MonitorSetting(monitors);    // first call, save settings
                }
                else
                {
                    switch (_state)
                    {
                        case State.SavingWindowPositions:
                            if (_savedMonitorSetting.HasSameMonitors(monitors))
                            {
                                return; // redudant call, discard
                            }
                            else
                            {
                                _state = State.WaitingForSavedMonitorSettingRestore;
                                Log.Info($"State change: {_state.ToString()}");
                            }
                            break;
                        case State.WaitingForSavedMonitorSettingRestore:
                            if (_savedMonitorSetting.HasSameMonitors(monitors))
                            {
                                Log.Info("Saved monitor setting restored, restoring window positions");

                                foreach (var window in _trackedWindows)
                                {
                                    RECT savedRect = window.Value.Info.Rect;
                                    if (WinEventHook.GetWindowRect(window.Key, out RECT rect) && savedRect != rect)
                                    {
                                        Log.Info($"Restoring position of {window.Key}: {WinEventHook.GetWindowText(window.Key)} to\n" +
                                            $"top{savedRect.top}, left{savedRect.left}, right{savedRect.right}, bottom{savedRect.bottom}");
                                        WinEventHook.MoveWindow(window.Key, savedRect);
                                    }
                                }

                                _state = State.SavingWindowPositions;
                                Log.Info($"State change: {_state.ToString()}");
                            }
                            break;
                    }
                }

                string log = $"Monitor count: {monitors.Count}";
                for (int i = 0; i < monitors.Count; i++)
                {
                    var rect = monitors[i].Rect;
                    log += $"\n\t{i}: top{rect.top}, left{rect.left}, right{rect.right}, bottom{rect.bottom}";
                }
                Log.Info(log);
            }
            else
            {
                Log.Error("An error occurred while enumerating monitors.");
            }
        }

        #region Callbacks
        private void WinEventCallback_OBJECT_LOCATIONCHANGE(IntPtr hWinEventHook, WinEventHook.SWEH_Events eventType, IntPtr hWnd, WinEventHook.SWEH_ObjectId idObject, long idChild, uint dwEventThread, uint dwmsEventTime)
        {
            if (_state != State.SavingWindowPositions)
            {
                return;
            }

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
                        Log.Info($"Status change on window {hWnd}: {windowName} | " +
                            $"status: {windowInfo.WindowStatus.ToString()}, top{rect.top}, left{rect.left}, right{rect.right}, bottom{rect.bottom}");
                    }
                }
            }
        }

        private void OnDisplaySettingsChangedCallback(object sender, EventArgs e)
        {
            ProcessCurrentMonitorSetting();
        }

        private void OnSessionSwitchCallback(object sender, SessionSwitchEventArgs e)
        {
            switch(e.Reason)
            {
                case SessionSwitchReason.SessionLock:
                    _state = State.WaitingForSavedMonitorSettingRestore;
                    Log.Info($"Session locked, state change: {_state.ToString()}");
                    break;
                case SessionSwitchReason.SessionUnlock:
                    Log.Info($"Session unlocked, checking current monitor setting");
                    // Act as if monitor setting changed, but with the state set by session locking
                    ProcessCurrentMonitorSetting();
                    break;
            }
        }
        #endregion

        public void Dispose()
        {
            foreach(var window in _trackedWindows.Values)
            {
                window.UnhookWinEvents();
            }
            GCSafetyHandle.Free();

            SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChangedCallback;
            SystemEvents.SessionSwitch -= OnSessionSwitchCallback;

            Log.Info("Disposing WindowKeeper Processor, shutting down\n\n");
        }
    }
}
