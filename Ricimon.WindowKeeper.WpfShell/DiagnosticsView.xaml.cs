using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using NLog;
using Ricimon.WindowKeeper.WpfShell.Util;
using Ricimon.WindowKeeper.Common.Diagnostics;

namespace Ricimon.WindowKeeper.WpfShell
{
    // adapted from https://archive.codeplex.com/?p=persistentwindows

    /// <summary>
    /// Interaction logic for DiagnosticsView.xaml
    /// </summary>
    public partial class DiagnosticsView : UserControl
    {
        private DiagnosticsViewModel viewModel;

        public DiagnosticsView()
        {
            InitializeComponent();
            viewModel = new DiagnosticsViewModel();
            this.DataContext = viewModel;
            Log.LogEvent += (level, message) =>
            {
                if (level != LogLevel.Trace)
                {
                    this.Dispatcher.BeginInvoke(new Action(delegate
                    {
                        viewModel.EventLog.Add($"{DateTime.Now.ToString("HH:mm:ss")}: {level}, {message}");
                        if (viewModel.EventLog.Count > 500)
                        {
                            viewModel.EventLog.RemoveAt(0);
                        }
                        eventLogList.AutoScrollToCurrentItem(eventLogList.Items.Count);
                    }));
                }
            };

            Log.Info("Diagnostics View Initialized");
        }
    }
}
