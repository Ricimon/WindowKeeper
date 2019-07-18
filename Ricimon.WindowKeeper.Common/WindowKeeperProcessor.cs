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
using System.Threading.Tasks;
using Ricimon.WindowKeeper.Common.Util;

namespace Ricimon.WindowKeeper.Common
{
    public partial class WindowKeeperProcessor : IDisposable
    {
        // Tunable parameters
        /// <summary>
        /// Some windows (like IntelliJ) will move themselves back to their no-DisplayPort monitor position even after they've been
        /// moved to their original position. So, we continue retaining these window positions for a set amount time. If they move 
        /// a significant amount (as in not dragged by user), then we push them back to their original position.
        /// </summary>
        private const float CONTINUOUS_WINDOW_RETAIN_TIME_AFTER_SETTING_RESTORE = 10f;
        /// <summary>
        /// Minimum distance from old window rect center to new window rect center for a LOCATIONCHANGED event to be regarded as
        /// an automatic window movement that should be forced back to the window's original position.
        /// </summary>
        private const int MIN_CENTER_DISTANCE_FOR_WINDOW_RETAIN = 100;

        private readonly IDictionary<WinEventHook.SWEH_Events, WinEventHook.WinEventDelegate> _eventDelegates;
        static GCHandle GCSafetyHandle;

        // Saved states
        private enum State
        {
            SavingWindowPositions,
            WaitingForSavedMonitorSettingRestore,
            RetainingWindowPositionsAfterMonitorSettingRestore
        }
        private State state
        {
            get => _state;
            set
            {
                if (value != _state)
                {
                    Log.Info($"State change: {value.ToString()}");
                }
                _state = value;
            }
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
                        if (state != State.WaitingForSavedMonitorSettingRestore)
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
                    string log = $"Monitor count: {monitors.Count}";
                    for (int i = 0; i < monitors.Count; i++)
                    {
                        var rect = monitors[i].Rect;
                        log += $"\n\t{i}: top{rect.top}, left{rect.left}, right{rect.right}, bottom{rect.bottom}";
                    }
                    Log.Info(log);

                    switch (state)
                    {
                        case State.SavingWindowPositions:
                        case State.RetainingWindowPositionsAfterMonitorSettingRestore:
                            if (_savedMonitorSetting.HasSameMonitors(monitors))
                            {
                                RestoreWindowPositions();
                            }
                            else
                            {
                                state = State.WaitingForSavedMonitorSettingRestore;
                            }
                            break;
                        case State.WaitingForSavedMonitorSettingRestore:
                            if (_savedMonitorSetting.HasSameMonitors(monitors))
                            {
                                Log.Info("Saved monitor setting restored, restoring window positions");
                                RestoreWindowPositions();
                            }
                            break;
                    }
                }
            }
            else
            {
                Log.Error("An error occurred while enumerating monitors.");
            }
        }

        private void RestoreWindowPositions()
        {
            foreach (var window in _trackedWindows)
            {
                RestoreWindowPosition(window.Key);
            }

            state = State.RetainingWindowPositionsAfterMonitorSettingRestore;
            CancellableMethodCollection.CallCancellableMethod("retainWindows", async (cancelToken) =>
            {
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(CONTINUOUS_WINDOW_RETAIN_TIME_AFTER_SETTING_RESTORE), cancelToken);
                }
                catch (TaskCanceledException) { return; }

                state = State.SavingWindowPositions;
            });
        }

        private void RestoreWindowPosition(IntPtr hWnd)
        {
            if (_trackedWindows.TryGetValue(hWnd, out var window))
            {
                WindowInfo savedWindow = window.Info;
                if (WinEventHook.GetWindowRect(hWnd, out RECT rect) && savedWindow.Rect != rect)
                {
                    var windowInfo = GetWindowInfo(hWnd, rect);
                    var savedRect = savedWindow.Rect;
                    Log.Info($"Restoring position of {hWnd}: {WinEventHook.GetWindowText(hWnd)} to" +
                        $"\n\tstatus: {savedWindow.WindowStatus.ToString()}, top{savedRect.top}, left{savedRect.left}, right{savedRect.right}, bottom{savedRect.bottom}");

                    WinEventHook.MoveWindow(hWnd, savedWindow.Rect);

                    // Some previously maximized windows may not remain maximized when moved back, so manually maximize them.
                    if (savedWindow.WindowStatus == WINDOWPLACEMENT.WindowStatus.Maximized &&
                        windowInfo.WindowStatus != savedWindow.WindowStatus)
                    {
                        NativeMethods.ShowWindow(hWnd, SW_Ints.SW_SHOWMAXIMIZED);
                    }
                }
            }
        }

        #region Callbacks
        private void WinEventCallback_OBJECT_LOCATIONCHANGE(IntPtr hWinEventHook, WinEventHook.SWEH_Events eventType, IntPtr hWnd, WinEventHook.SWEH_ObjectId idObject, long idChild, uint dwEventThread, uint dwmsEventTime)
        {
            if (state == State.WaitingForSavedMonitorSettingRestore)
            {
                return;
            }

            int windowTextLength = NativeMethods.GetWindowTextLength(hWnd);
            if (windowTextLength > 0)
                if (WinEventHook.GetWindowRect(hWnd, out var rect)) // new rect is valid
                {
                    var windowInfo = GetWindowInfo(hWnd, rect);
                    if (_trackedWindows.TryGetValue(hWnd, out var trackedWindow) && trackedWindow.Info != windowInfo)   // new window info isn't the same as stored window info
                    {
                        switch (state)
                        {
                            case State.RetainingWindowPositionsAfterMonitorSettingRestore:
                                // Don't really want to import a math package, or do data type transforms
                                var oldCenter = trackedWindow.Info.Rect.center;
                                var newCenter = windowInfo.Rect.center;
                                var delX = newCenter.x - oldCenter.x; var delY = newCenter.y - oldCenter.y;
                                var centerMoveDistanceSq = delX * delX + delY * delY;

                                if (centerMoveDistanceSq >= MIN_CENTER_DISTANCE_FOR_WINDOW_RETAIN * MIN_CENTER_DISTANCE_FOR_WINDOW_RETAIN)
                                {
                                    RestoreWindowPosition(hWnd);
                                }
                                else
                                {
                                    goto case State.SavingWindowPositions;
                                }
                                break;
                            case State.SavingWindowPositions:
                                trackedWindow.Info = windowInfo;

                                string windowName = WinEventHook.GetWindowText(hWnd, windowTextLength);
                                Log.Info($"Status change on window {hWnd}: {windowName} | " +
                                    $"status: {windowInfo.WindowStatus.ToString()}, top{rect.top}, left{rect.left}, right{rect.right}, bottom{rect.bottom}");
                                break;
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
                    state = State.WaitingForSavedMonitorSettingRestore;
                    Log.Info($"Session locked");
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
