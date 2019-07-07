using Prism.Mvvm;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ricimon.WindowKeeper.WpfShell
{
    // adapted from https://archive.codeplex.com/?p=persistentwindows

    public class DiagnosticsViewModel : BindableBase
    {
        public BindingList<string> EventLog
        {
            get => allProcesses;
            set => SetProperty(ref allProcesses, value);
        }

        private BindingList<string> allProcesses;

        public DiagnosticsViewModel()
        {
            EventLog = new BindingList<string>();
        }
    }
}
