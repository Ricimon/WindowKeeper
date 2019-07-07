using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Ricimon.WindowKeeper.WpfShell;

namespace Ricimon.WindowKeeper.SystrayShell
{
    // adapted from https://archive.codeplex.com/?p=persistentwindows

    public partial class SystrayForm : Form
    {
        public MainWindow MainView { get; set; }

        public SystrayForm()
        {
            InitializeComponent();

            MainView = new MainWindow();
            //MainView.Hide();
            MainView.Show();
        }

        private void DiagnosticsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            //bool shouldShow = false;
            //if (MainView == null || MainView.IsClosed)
            //{
            //    MainView = new MainWindow();
            //    shouldShow = true;
            //}

            //if (shouldShow)
            //{
            //    MainView.Show();
            //}

            MainView.Show();
            MainView.Activate();
        }

        private void NotifyIconMain_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            MainView.Show();
            MainView.Activate();
        }

        private void ShutdownToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this.notifyIconMain.Visible = false;
            this.notifyIconMain = null;
            Application.Exit();
        }

        private void SystrayForm_Load(object sender, EventArgs e)
        {

        }

        private void ContextMenuStrip1_Opening(object sender, CancelEventArgs e)
        {

        }
    }
}
