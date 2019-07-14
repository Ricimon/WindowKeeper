using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Ricimon.WindowKeeper.Common.WinApiBridge;

namespace Ricimon.WindowKeeper.Common.Models
{
    public class TrackedWindow
    {
        public WindowInfo Info;
        public IList<IntPtr> hWinEventHooks = new List<IntPtr>();

        public void UnhookWinEvents()
        {
            foreach(var hook in hWinEventHooks)
            {
                WinEventHook.WinEventUnhook(hook);
            }
            hWinEventHooks.Clear();
        }

        ~TrackedWindow()
        {
            UnhookWinEvents();
        }
    }

    public struct WindowInfo
    {
        public WINDOWPLACEMENT.WindowStatus WindowStatus;
        public RECT Rect;

        public static bool operator ==(WindowInfo wi1, WindowInfo wi2)
        {
            return wi1.WindowStatus == wi2.WindowStatus &&
                    wi1.Rect == wi2.Rect;
        }

        public static bool operator !=(WindowInfo wi1, WindowInfo wi2)
        {
            return !(wi1 == wi2);
        }

        public override bool Equals(object obj)
        {
            return obj is WindowInfo info &&
                   WindowStatus == info.WindowStatus &&
                   Rect == info.Rect;
        }

        public override int GetHashCode()
        {
            var hashCode = -862463514;
            hashCode = hashCode * -1521134295 + WindowStatus.GetHashCode();
            hashCode = hashCode * -1521134295 + EqualityComparer<RECT>.Default.GetHashCode(Rect);
            return hashCode;
        }
    }
}
