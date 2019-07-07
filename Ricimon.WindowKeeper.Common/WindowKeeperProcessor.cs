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
        private IList<IntPtr> _hWinEventHooks = new List<IntPtr>();

        private readonly IDictionary<WinEventHook.SWEH_Events, WinEventHook.WinEventDelegate> _eventDelegates;

        static GCHandle GCSafetyHandle;

        private readonly IDictionary<IntPtr, WindowInfo> _windowInfos = new Dictionary<IntPtr, WindowInfo>();

        public WindowKeeperProcessor()
        {
            // These events actually fire ridiculously often, so maybe there's a way reduce the number of calls?
            // All of these events fire when a mouse-movement or cursor blink happens over a window.
            // As a result a filter is applied to these event fires to discard a large majority of them.
            _eventDelegates = new Dictionary<WinEventHook.SWEH_Events, WinEventHook.WinEventDelegate>
            {
                // DESTROY/HIDE are taken care of by ShellEvents
                //{ WinEventHook.SWEH_Events.EVENT_OBJECT_DESTROY,        new WinEventHook.WinEventDelegate(WinEventCallback_OBJECT_DESTROY) },
                //{ WinEventHook.SWEH_Events.EVENT_OBJECT_HIDE,           new WinEventHook.WinEventDelegate(WinEventCallback_OBJECT_HIDE) },
                { WinEventHook.SWEH_Events.EVENT_OBJECT_LOCATIONCHANGE, new WinEventHook.WinEventDelegate(WinEventCallback_OBJECT_LOCATIONCHANGE) }
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
                var windowInfo = GetWindowInfo(window.RawPtr);
                WinEventHook.GetWindowPlacement(window.RawPtr, out var placement);

                var placementRect = placement.rcNormalPosition;
                var rect = windowInfo.Rect;

                _windowInfos[window.RawPtr] = windowInfo;

                Log.Info($"{window.RawPtr}: {window.GetWindowText()}\n\tstatus:{placement.windowStatus.ToString()}, WINDOWRECT top{rect.top}, left{rect.left}, right{rect.right}, bottom{rect.bottom}"
                    // + $"\n\tWINDOWPLACEMENT top{placementRect.top}, left{placementRect.left}, right{placementRect.right}, bottom{placementRect.bottom}"
                    //+ $"\n\tRawPtr: {window.RawPtr.ToInt32()}"
                    );

                // Subscribe to window updates
                uint threadId = NativeMethods.GetWindowThreadProcessId(window.RawPtr, out uint processId);

                foreach(var ev in _eventDelegates)
                {
                    _hWinEventHooks.Add(
                        WinEventHook.WinEventHookOne(ev.Key, ev.Value, processId, threadId));
                }
            }

            // Subscribe to any window created/destroyed events (ex. new window opens)
            var sysProcHook = new SystemProcessHook();
            sysProcHook.OnWindowEvent += (IntPtr hWnd, SystemProcessHook.ShellEvent ev) =>
            {
                int windowTextLength = NativeMethods.GetWindowTextLength(hWnd);
                if (windowTextLength > 0)
                {
                    string windowName = WinEventHook.GetWindowText(hWnd, windowTextLength);
                    Log.Info($"{ev.ToString()} - {hWnd}: {windowName}");
                }
            };

            return this;
        }

        protected void WinEventCallback_OBJECT_DESTROY(IntPtr hWinEventHook, WinEventHook.SWEH_Events eventType, IntPtr hWnd, WinEventHook.SWEH_ObjectId idObject, long idChild, uint dwEventThread, uint dwmsEventTime)
        {
            int windowTextLength = NativeMethods.GetWindowTextLength(hWnd);
            if (windowTextLength > 0)
            {
                string windowName = WinEventHook.GetWindowText(hWnd, windowTextLength);
                Log.Info($"{hWnd}: {windowName} raised OBJECT_DESTROY");
            }
        }

        protected void WinEventCallback_OBJECT_HIDE(IntPtr hWinEventHook, WinEventHook.SWEH_Events eventType, IntPtr hWnd, WinEventHook.SWEH_ObjectId idObject, long idChild, uint dwEventThread, uint dwmsEventTime)
        {
            int windowTextLength = NativeMethods.GetWindowTextLength(hWnd);
            if (windowTextLength > 0)
            {
                string windowName = WinEventHook.GetWindowText(hWnd, windowTextLength);
                Log.Info($"{hWnd}: {windowName} raised OBJECT_HIDE");
            }
        }
        protected void WinEventCallback_OBJECT_LOCATIONCHANGE(IntPtr hWinEventHook, WinEventHook.SWEH_Events eventType, IntPtr hWnd, WinEventHook.SWEH_ObjectId idObject, long idChild, uint dwEventThread, uint dwmsEventTime)
        {
            int windowTextLength = NativeMethods.GetWindowTextLength(hWnd);
            if (windowTextLength > 0)
            {
                if (WinEventHook.GetWindowRect(hWnd, out var rect)) // new rect is valid
                {
                    var windowInfo = GetWindowInfo(hWnd);
                    if (_windowInfos.TryGetValue(hWnd, out var storedWindowInfo) && storedWindowInfo != windowInfo)   // new window info isn't the same as stored window info
                    {
                        _windowInfos[hWnd] = windowInfo;

                        string windowName = WinEventHook.GetWindowText(hWnd, windowTextLength);
                        Log.Info($"{hWnd}: {windowName} status changed, new status: {windowInfo.WindowStatus.ToString()}, top{rect.top}, left{rect.left}, right{rect.right}, bottom{rect.bottom}");
                    }
                }
            }
        }

        private WindowInfo GetWindowInfo(IntPtr hWnd)
        {
            WinEventHook.GetWindowPlacement(hWnd, out var placement);
            WinEventHook.GetWindowRect(hWnd, out var rect);
            return new WindowInfo
            {
                WindowStatus = placement.windowStatus,
                Rect = rect
            };
        }

        public void Dispose()
        {
            GCSafetyHandle.Free();
            foreach(var hook in _hWinEventHooks)
                WinEventHook.WinEventUnhook(hook);
            Log.Info("Disposing WindowKeeper Processor");
        }
    }
}
