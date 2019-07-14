using Ricimon.WindowKeeper.Common.Diagnostics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Ricimon.WindowKeeper.Common.WinApiBridge
{
    // adapted from https://stackoverflow.com/questions/21912686/most-efficient-way-for-getting-notified-on-window-open

    public class SystemProcessHook : Form
    {
        public enum ShellEvent : int
        {
            HSHELL_WINDOWCREATED = 1,
            HSHELL_WINDOWDESTROYED = 2,
            HSHELL_ACTIVATESHELLWINDOW = 3,
            HSHELL_WINDOWACTIVATED = 4,
            HSHELL_GETMINRECT = 5,
            HSHELL_REDRAW = 6,
            HSHELL_TASKMAN = 7,
            HSHELL_LANGUAGE = 8,
            HSHELL_ACCESSIBILITYSTATE = 11,
            HSHELL_APPCOMMAND = 12
        }

        public Action<IntPtr, ShellEvent> OnWindowEvent;

        private readonly int _msgNotify;

        public SystemProcessHook()
        {
            // Hook on to the shell
            _msgNotify = NativeMethods.RegisterWindowMessage("SHELLHOOK");
            NativeMethods.RegisterShellHookWindow(this.Handle);
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == _msgNotify)
            {
                // Receive shell messages
                var shellEvent = (ShellEvent)m.WParam.ToInt32();
                switch(shellEvent)
                {
                    // only accept these events
                    case ShellEvent.HSHELL_WINDOWCREATED:
                    case ShellEvent.HSHELL_WINDOWDESTROYED:
                        OnWindowEvent?.Invoke(m.LParam, shellEvent);
                        break;
                }
            }

            base.WndProc(ref m);
        }

        protected override void Dispose(bool disposing)
        {
            try
            {
                NativeMethods.DeregisterShellHookWindow(this.Handle);
            }
            catch { }
            base.Dispose(disposing);
        }
    }
}
