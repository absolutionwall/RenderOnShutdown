using System;
using System.Windows;
using System.Windows.Forms;
using Microsoft.Win32;

namespace RenderOnShutdown
{
    public partial class SettingsWindow : Window
    {
        private Config _config;
        public SettingsWindow(Config config)
        {
            InitializeComponent();
            _config = config;
            TxtMonitorFolder.Text = _config.MonitoredFolder;
            TxtFfmpeg.Text = _config.FfmpegPath;
            TxtCommand.Text = _config.Command;
            TxtOutputFolder.Text = _config.OutputFolder;
        }

        private void BtnBrowseMonitor_Click(object sender, RoutedEventArgs e)
        {
            using var dlg = new FolderBrowserDialog();
            if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                TxtMonitorFolder.Text = dlg.SelectedPath;
            }
        }

        private void BtnBrowseFfmpeg_Click(object sender, RoutedEventArgs e)
        {
            using var ofd = new System.Windows.Forms.OpenFileDialog();
            ofd.Filter = "Executable|*.exe|All files|*.*";
            if (ofd.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                TxtFfmpeg.Text = ofd.FileName;
            }
        }

        private void BtnBrowseOutput_Click(object sender, RoutedEventArgs e)
        {
            using var dlg = new FolderBrowserDialog();
            if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                TxtOutputFolder.Text = dlg.SelectedPath;
            }
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            _config.MonitoredFolder = TxtMonitorFolder.Text;
            _config.FfmpegPath = TxtFfmpeg.Text;
            _config.Command = TxtCommand.Text;
            _config.OutputFolder = TxtOutputFolder.Text;
            _config.Save();
            DialogResult = true;
            Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

    }
}
