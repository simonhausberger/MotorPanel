using Com_ELF.Services;
using Microsoft.Win32;
using System.IO;
using System.Windows;

namespace MotorPanel
{
    public partial class SetupWindow : Window
    {
        public SetupWindow()
        {
            InitializeComponent();

            TbElfPath.Text = @"C:\Users\Matthias\mtw4\Motorcontrol_Infineon_CC1_HTL\Motorsteuerung_V3_1_3\build\last_config\mtb-example-ce240786-empty-app.elf";
            TbJLinkPath.Text = @"C:\Program Files\SEGGER\JLink_V880\JLink.exe";
            TbDevice.Text = "PSC3XXF_TM";
            TbSpeed.Text = "1000";
        }

        private void BrowseElf_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Filter = "ELF files (*.elf)|*.elf|All files (*.*)|*.*"
            };

            if (dlg.ShowDialog() == true)
                TbElfPath.Text = dlg.FileName;
        }

        private void BrowseJLink_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Filter = "Executable (*.exe)|*.exe|All files (*.*)|*.*"
            };

            if (dlg.ShowDialog() == true)
                TbJLinkPath.Text = dlg.FileName;
        }

        private void OpenMotorPanelBLOCK_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var settings = BuildSettings();

                if (!File.Exists(TbElfPath.Text))
                    throw new FileNotFoundException("ELF file not found.");

                var panel = new MotorPanelBlock(settings, TbElfPath.Text);
                panel.Show();
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Setup error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OpenMotorPanelFOC_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var settings = BuildSettings();

                if (!File.Exists(TbElfPath.Text))
                    throw new FileNotFoundException("ELF file not found.");

                var panel = new MotorPanelFOC(settings, TbElfPath.Text);
                panel.Show();
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Setup error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private JLinkCommanderSettings BuildSettings()
        {
            if (!File.Exists(TbJLinkPath.Text))
                throw new FileNotFoundException("JLink.exe not found.");

            if (!int.TryParse(TbSpeed.Text, out int speed) || speed <= 0)
                throw new InvalidOperationException("Speed must be a positive integer.");

            return new JLinkCommanderSettings
            {
                JLinkExePath = TbJLinkPath.Text,
                DeviceName = TbDevice.Text,
                InterfaceName = "SWD",
                SpeedKHz = speed,
                HaltBeforeAccess = true,
                ResumeAfterAccess = false
            };
        }
    }
}