using Syncfusion.UI.Xaml.Charts;
using Syncfusion.UI.Xaml.Gauges;
using System.IO.Ports;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace MotorPanel
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        SerialPort Serial1;
        const int baudRate = 115200;

        double TargedSpeed;
        double TargedIq;
        bool DisableOrEnable;
        bool StopOrstart;
        bool LeftOrRight;
        bool BlockOrFOC;
        bool SensOrSL;
        bool SpeedOrTorque;         // bei bool-variablen: true = vordere und false = hintere option, z. B. true = speed-regelung, false = torque-regelung

        List<ChartPoint> idData = new();
        List<ChartPoint> iqData = new();
        List<ChartPoint> vdData = new();
        List<ChartPoint> vqData = new();
        double time = 0;

        public MainWindow()
        {
            InitializeComponent();

            // alle verfügbaren COM-Ports abrufen und in combobox füllen
            string[] ports = SerialPort.GetPortNames();
            ComboBoxComPorts.ItemsSource = ports;
        }
        public class ChartPoint
        {
            public double X { get; set; }
            public double Y { get; set; }
        }
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            BtnConnect.IsEnabled = true;
            BtnDisconnect.IsEnabled = false;
            txtbTargetIq.IsEnabled = false;     // weil in speed-Regelung gestartet wird

            LogEvent("motor-panel opened");
            StartupSelfTest();

            // Listen mit chart verbinden, damit die Datenpunkte angezeigt werden können (z. B. Id, Iq, vd, vq)
            IdSeries.ItemsSource = idData;
            IdSeries.XBindingPath = "X";
            IdSeries.YBindingPath = "Y";

            IqSeries.ItemsSource = iqData;
            IqSeries.XBindingPath = "X";
            IqSeries.YBindingPath = "Y";

            vdSeries.ItemsSource = vdData;
            vdSeries.XBindingPath = "X";
            vdSeries.YBindingPath = "Y";

            vqSeries.ItemsSource = vqData;
            vqSeries.XBindingPath = "X";
            vqSeries.YBindingPath = "Y";
        }
        // -------------- startup animation: LEDs blinken, Zeiger auf max fahren, LinearPointer animieren, alles zurücksetzen ---------------
        private async Task StartupSelfTest()
        {
            // 1LEDs blinken
            LedError.Fill = Brushes.Red;
            LedWarning.Fill = Brushes.Orange;
            LedEnabled.Fill = Brushes.Green;
            LedConnected.Fill = Brushes.Green;

            Ellipse[] leds = { LedError, LedWarning, LedEnabled, LedConnected };
            for (int i = 0; i < 3; i++) // 3 Blinkzyklen
            {
                foreach (var led in leds) led.Visibility = Visibility.Visible;
                await Task.Delay(200);
                foreach (var led in leds) led.Visibility = Visibility.Hidden;
                await Task.Delay(200);
            }
            // alle wieder sichtbar (oder nur Enabled/Connected?)
            LedError.Visibility = Visibility.Visible;
            LedWarning.Visibility = Visibility.Visible;
            LedEnabled.Visibility = Visibility.Visible;
            LedConnected.Visibility = Visibility.Visible;

            LedError.Fill = Brushes.WhiteSmoke;
            LedWarning.Fill = Brushes.WhiteSmoke;
            LedEnabled.Fill = Brushes.WhiteSmoke;
            LedConnected.Fill = Brushes.WhiteSmoke;

            // CircularPointer auf Max fahren
            AnimatePointer(NeedleSpeed, 4000);
            AnimatePointer(PointerSpeed, 4000);
            AnimatePointer(NeedleTorque, 5);
            AnimatePointer(PointerTorque, 5);
            AnimatePointer(NeedleIBus, 25);
            AnimatePointer(PointerIBus, 25);
            AnimatePointer(NeedleVBus, 50);
            AnimatePointer(PointerVBus, 50);

            await Task.Delay(500); // kurz stehen lassen

            // LinearPointer animieren
            await Task.WhenAll(
                AnimateLinearPointer(PointerPCBTemp, 80),
                AnimateLinearPointer(BarPCBTemp, 80),
                AnimateLinearPointer(PointerWindingTemp, 80),
                AnimateLinearPointer(BarWindingTemp, 80)
            );

            await Task.Delay(500);

            // Alles wieder auf 0
            AnimatePointer(NeedleSpeed, 0);
            AnimatePointer(PointerSpeed, 0);
            AnimatePointer(NeedleTorque, 0);
            AnimatePointer(PointerTorque, 0);
            AnimatePointer(NeedleIBus, 0);
            AnimatePointer(PointerIBus, 0);
            AnimatePointer(NeedleVBus, 0);
            AnimatePointer(PointerVBus, 0);

            await Task.WhenAll(
                AnimateLinearPointer(PointerPCBTemp, 0),
                AnimateLinearPointer(BarPCBTemp, 0),
                AnimateLinearPointer(PointerWindingTemp, 0),
                AnimateLinearPointer(BarWindingTemp, 0)
            );

            // Test-Log
            LogEvent("startup self-test done");
        }
        private void MenuItemSelfTest_Click(object sender, RoutedEventArgs e)
        {
            LogEvent("self-test started");
            StartupSelfTest();
            LogEvent("self-test finished");
        }
        private void AnimatePointer(CircularPointer pointer, double toValue)
        {
            DoubleAnimation anim = new DoubleAnimation();
            anim.From = pointer.Value;
            anim.To = toValue;
            anim.Duration = TimeSpan.FromMilliseconds(400);
            pointer.BeginAnimation(CircularPointer.ValueProperty, anim);
        }

        private async Task AnimateLinearPointer(LinearPointer pointer, double target, int steps = 20, int delayMs = 20)
        {
            double start = pointer.Value;
            double delta = (target - start) / steps;

            for (int i = 0; i < steps; i++)
            {
                pointer.Value += delta;
                await Task.Delay(delayMs);
            }
        }

        // -------------------------------------------------------- manage uart connection ---------------------------------------------------
        private void BtnConnect_Click(object sender, RoutedEventArgs e)
        {
            if (ComboBoxComPorts.SelectedItem == null)
            {
                MessageBox.Show("Please select a COM-port!");
            }
            else
            {
                TryOpenSerialPort(ComboBoxComPorts.SelectedItem.ToString());
            }
        }
        private void BtnDisconnect_Click(object sender, RoutedEventArgs e)
        {
            if (Serial1 != null && Serial1.IsOpen)
            {
                Serial1.Close();
                Serial1.Dispose();
                Serial1 = null;
                LedConnected.Fill = Brushes.WhiteSmoke;
                LogEvent("serial port closed");
                BtnConnect.IsEnabled = true;
                BtnDisconnect.IsEnabled = false;
            }
        }
        private void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            string[] ports = SerialPort.GetPortNames();
            ComboBoxComPorts.ItemsSource = ports;
        }
        void TryOpenSerialPort(string portName)
        {
            try
            {
                Serial1 = new SerialPort(portName, baudRate, Parity.None, 8, StopBits.One);
                Serial1.Open(); // Port erfolgreich geöffnet
                LogEvent("serial port opened");
                LedConnected.Fill = Brushes.Green;
                BtnConnect.IsEnabled = false;
                BtnDisconnect.IsEnabled = true;
            }
            catch (UnauthorizedAccessException)
            {
                LedConnected.Fill = Brushes.WhiteSmoke;
                MessageBox.Show($"Port {portName} is already in use!", "connection error", MessageBoxButton.OK, MessageBoxImage.Error);
                LogEvent($"port {portName} is already in use");
            }
            catch (Exception ex)
            {
                LedConnected.Fill = Brushes.WhiteSmoke;
                MessageBox.Show($"Error opening {portName}: {ex.Message}","connection error", MessageBoxButton.OK, MessageBoxImage.Error);
                LogEvent($"error opening {portName}");
            }
        }
        // ------------------------------------------------------- log event function --------------------------------------------------------
        private void LogEvent(string message)
        {
            string timestamp = DateTime.Now.ToString("HH:mm:ss");
            string logEntry = $"[{timestamp}] {message}";

            EventLogListBox.Items.Insert(0, logEntry);
            EventLogListBox.ScrollIntoView(EventLogListBox.Items[0]);
        }
        // ----------------------------------------------------- receiving - event handler ---------------------------------------------------
        private void Serial1_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            int bytesToRead = Serial1.BytesToRead;
            if (bytesToRead % 2 != 0)
                return; // unvollständiges Paket ignorieren, zum bsp. wenn erst 1/2 angekommen ist

            byte[] buffer = new byte[bytesToRead];
            Serial1.Read(buffer, 0, buffer.Length);

            // GUI-Update im UI-Thread
            Dispatcher.Invoke(() =>
            {
                for (int i = 0; i < buffer.Length; i += 2)
                {
                    // High- und Low-Byte zusammenfügen
                    ushort value = (ushort)((buffer[i] << 8) | buffer[i + 1]);

                    // Beispiel: je nach Index dem richtigen Sensor zuweisen
                    switch (i / 2)
                    {
                        case 0:
                            NeedleSpeed.Value = value;
                            PointerSpeed.Value = value;
                            break;
                        case 1:
                            NeedleTorque.Value = value; 
                            PointerTorque.Value = value;
                            break;
                        case 2:
                            PointerIBus.Value = value;
                            NeedleIBus.Value = value;
                            break;
                        case 3:
                            PointerVBus.Value = value;
                            NeedleVBus.Value = value;
                            break;
                        case 4:
                            PointerPCBTemp.Value = value;
                            BarPCBTemp.Value = value;
                            break;
                        case 5:
                            PointerWindingTemp.Value = value;
                            BarWindingTemp.Value = value;
                            break;
                        case 6:
                            double id = value;
                            idData.Add(new ChartPoint { X = time, Y = id });
                            if (idData.Count > 300)
                                idData.RemoveAt(0);
                            break;
                        case 7:
                            double iq = value;
                            iqData.Add(new ChartPoint { X = time, Y = iq });
                            if (iqData.Count > 300)
                                iqData.RemoveAt(0);
                            break;
                        case 8:
                            double vd = value;
                            vdData.Add(new ChartPoint { X = time, Y = vd });
                            if (vdData.Count > 300)
                                vdData.RemoveAt(0);
                            break;
                        case 9:
                            double vq = value;
                            vqData.Add(new ChartPoint { X = time, Y = vq });
                            if (vqData.Count > 300)
                                vqData.RemoveAt(0);
                            break;
                        case 10:
                            bool error = value == 1;
                            LedError.Fill = error ? Brushes.Red : Brushes.WhiteSmoke;
                            break;
                        case 11:
                            bool warning = value == 1;
                            LedWarning.Fill = warning ? Brushes.Orange : Brushes.WhiteSmoke;
                            break;
                    }
                }
                CurrentChart.InvalidateVisual();
                VoltageChart.InvalidateVisual();
                time += 0.02;
            });
        }
        // ----------------------------------------------------- sending packets -------------------------------------------------------------
        // Reihenfolge: TargetSpeed, TargetIq, ControlMode, EnableMotor, StartStop, Direction, FOC/Block, Sensored/Sensorless
        private void SendControlPacket()
        {
            if (Serial1 == null || !Serial1.IsOpen)
                return;

            ushort targetSpeed = 0;
            ushort targetIq = 0;

            ushort.TryParse(txtbTargetSpeed.Text, out targetSpeed);
            ushort.TryParse(txtbTargetIq.Text, out targetIq);

            // bool → ushort
            ushort controlMode = (ushort)(SpeedOrTorque ? 1 : 0);
            ushort enableMotor = (ushort)(SwitchEnableMotor.IsChecked == true ? 1 : 0);
            ushort startStop = (ushort)(SwitchStartStop.IsChecked == true ? 1 : 0);
            ushort direction = (ushort)(SwitchDirection.IsChecked == true ? 1 : 0);
            ushort focBlock = (ushort)(SwitchBlockFoc.IsChecked == true ? 1 : 0);
            ushort sensorMode = (ushort)(SwitchSensor.IsChecked == true ? 1 : 0);

            ushort[] values =
            {
                targetSpeed,
                targetIq,
                controlMode,
                enableMotor,
                startStop,
                direction,
                focBlock,
                sensorMode
            };

            byte[] packet = new byte[values.Length * 2 + 2]; // +2 für Checksum
            int index = 0;

            ushort checksum = 0;

            foreach (ushort val in values)
            {
                byte high = (byte)(val >> 8);
                byte low = (byte)(val & 0xFF);

                packet[index++] = high;
                packet[index++] = low;

                checksum += val;
            }

            // einfache 16-bit Checksum
            packet[index++] = (byte)(checksum >> 8);
            packet[index++] = (byte)(checksum & 0xFF);

            try
            {
                Serial1.Write(packet, 0, packet.Length);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error sending packet to µC: {ex.Message}", "serial error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // -------------------------------------------------------- buttons ------------------------------------------------------------------
        private void BtnApplyTarget_Click(object sender, RoutedEventArgs e)
        {
            TargedIq = double.TryParse(txtbTargetIq.Text, out double iq) ? iq : 0;
            TargedSpeed = double.TryParse(txtbTargetSpeed.Text, out double speed) ? speed : 0;

            SendControlPacket();
        }

        private void SwitchControlMode_Click(object sender, RoutedEventArgs e)
        {
            if(SwitchControlMode.IsChecked == true) // Iq-Regelung
            {
                LogEvent("switched to torque control (just in FOC available)");
                SwitchBlockFoc.IsChecked = true;   // hier keine Blockkommutierung möglich
                txtbTargetSpeed.IsEnabled = false;
                txtbTargetIq.IsEnabled = true;

                SpeedOrTorque = false;
            }
            else     // speed-Regelung
            {
                LogEvent("switched to speed control (FOC keeps activated)");
                SwitchBlockFoc.IsEnabled = true;
                txtbTargetSpeed.IsEnabled = true;
                txtbTargetIq.IsEnabled = false;

                SpeedOrTorque = true;
            }
            SendControlPacket();
        }

        private void SwitchEnableMotor_Click(object sender, RoutedEventArgs e)
        {
            if(SwitchEnableMotor.IsChecked == true)
            {
                LogEvent("motor enabled");
                DisableOrEnable = false;
                LedEnabled.Fill = Brushes.Green;
            }
            else
            {
                LogEvent("motor disabled");
                DisableOrEnable = true;
                LedEnabled.Fill = Brushes.WhiteSmoke;
            }
            SendControlPacket();
        }

        private void SwitchStartStop_Click(object sender, RoutedEventArgs e)
        {
            if (SwitchStartStop.IsChecked == true)            
            {
                LogEvent("motor started");
                StopOrstart = false;
            }
            else
            {
                LogEvent("motor stopped");
                StopOrstart = true;
            }
            SendControlPacket();
        }

        private void SwitchDirection_Click(object sender, RoutedEventArgs e)
        {
            if (SwitchDirection.IsChecked == true)
            {
                LogEvent("direction: right");
                LeftOrRight = false;
            }
            else
            {
                LogEvent("direction: left");
                LeftOrRight = true;
            }
            SendControlPacket();
        }

        private void SwitchBlockFoc_Click(object sender, RoutedEventArgs e)
        {
            if (SwitchBlockFoc.IsChecked == true)
            {
                LogEvent("FOC activated");
                BlockOrFOC = false;
            }
            else
            {
                LogEvent("block-commutation activated");
                BlockOrFOC = true;
            }
            SendControlPacket();
        }

        private void SwitchSensor_Click(object sender, RoutedEventArgs e)
        {
            if(SwitchSensor.IsChecked == true) 
            {
                LogEvent("sensoless mode activated");
                SensOrSL = false;
            }
            else
            {
                LogEvent("sensored mode activated");
                SensOrSL = true;
            }
            SendControlPacket();
        }

        private void txtbTargetSpeed_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtbTargetSpeed.Text))
            {
                AnimatePointer(PointerSpeed, 0);
                return;
            }

            if (double.TryParse(txtbTargetSpeed.Text, out double speed))
            {
                if (speed >= 0 && speed <= 4000)
                {
                    AnimatePointer(PointerSpeed, speed);
                }
                else
                {
                    MessageBox.Show("The value must be between 0 and 4000.", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            else
            {
                MessageBox.Show("Please enter a valid number.", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void txtbTargetIq_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtbTargetIq.Text))
            {
                AnimatePointer(PointerTorque, 0);
                return;
            }

            if (double.TryParse(txtbTargetIq.Text, out double iq))
            {
                if (iq >= 0 && iq <= 5)
                {
                    AnimatePointer(PointerTorque, iq);
                }
                else
                {
                    MessageBox.Show("The value must be between 0 and 5.", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            else
            {
                MessageBox.Show("Please enter a valid number.", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    
    }
}