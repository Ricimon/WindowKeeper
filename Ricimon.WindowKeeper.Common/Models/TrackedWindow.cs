using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ricimon.WindowKeeper.Common.Models
{
    public class TrackedWindow
    {
        public WindowInfo Info;
        public IList<IntPtr> hWinEventHooks = new List<IntPtr>();
    }
}
