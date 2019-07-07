using Ricimon.WindowKeeper.Common.WinApiBridge;
using System.Collections.Generic;

namespace Ricimon.WindowKeeper.Common.Models
{
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
