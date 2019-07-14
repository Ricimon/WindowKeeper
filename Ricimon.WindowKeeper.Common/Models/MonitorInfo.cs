using Ricimon.WindowKeeper.Common.WinApiBridge;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ricimon.WindowKeeper.Common.Models
{
    public class MonitorSetting
    {
        public readonly IList<MonitorInfo> Monitors;

        public MonitorSetting(IList<MonitorInfo> monitors)
        {
            Monitors = monitors;
        }

        public bool HasSameMonitors(IList<MonitorInfo> monitors)
        {
            if (Monitors.Count != monitors.Count)
            {
                return false;
            }
            for (int i = 0; i < Monitors.Count; i++)
            {
                if (Monitors[i] != monitors[i])
                {
                    return false;
                }
            }
            return true;
        }
    }

    public struct MonitorInfo
    {
        public RECT Rect;

        public static bool operator ==(MonitorInfo mi1, MonitorInfo mi2)
        {
            return mi1.Rect == mi2.Rect;
        }

        public static bool operator !=(MonitorInfo mi1, MonitorInfo mi2)
        {
            return !(mi1 == mi2);
        }


        public override bool Equals(object obj)
        {
            return obj is MonitorInfo info &&
                   Rect == info.Rect;
        }

        public override int GetHashCode()
        {
            return -1519027649 + EqualityComparer<RECT>.Default.GetHashCode(Rect);
        }
    }
}
