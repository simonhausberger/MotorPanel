using Com_ELF.Models;
using Com_ELF.Services;
using Syncfusion.UI.Xaml.Charts;
using Syncfusion.UI.Xaml.Gauges;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace MotorPanel
{
    public partial class MotorPanelBlock : Window
    {
        private readonly JLinkCommanderSettings _jlinkSettings;
        private readonly string _elfPath;
        private readonly DispatcherTimer _pollTimer = new();

        private Dictionary<string, ElfSymbol> _symbols = new();
        private readonly Dictionary<string, ReadField> _readFields = new();
        private readonly HashSet<string> _missingSymbolsLogged = new();

        private uint _readBlockBaseAddress;
        private int _readBlockSize;
        private bool _readLayoutReady;

        private readonly ObservableCollection<ChartPoint> dutycycleData = new();

        private Stopwatch? stopwatch;
        private double baseTime = 0.0;
        private const int MaxPoints = 300;

        private uint? _lastSystemState;
        private uint? _lastFocType;
        private uint? _lastFocControllerType;

        // ===== Symbol names from your µC =====
        private const string SymWeRef = "We_ref";
        private const string SymWe = "We";
        private const string SymDuty = "Duty";
        private const string SymPcbTemp = "PCB_Temp";
        private const string SymBldcTemp = "BLDC_Temp";
        private const string SymIBUS = "I_Bus";
        private const string SymVBUS = "V_Bus";
        private const string SymEnable = "Enable";
        private const string SymStart = "Start";
        private const string SymDirectionCw = "Direction_CW";
        private const string SymFoc = "FOC";

        private const string SymSystemState = "system_state";

        public MotorPanelBlock() : this(new JLinkCommanderSettings(), string.Empty)
        {
        }

        public MotorPanelBlock(JLinkCommanderSettings settings, string elfPath)
        {
            InitializeComponent();

            _jlinkSettings = settings;
            _elfPath = elfPath;

            _pollTimer.Interval = TimeSpan.FromMilliseconds(10);
            _pollTimer.Tick += PollTimer_Tick;
        }

        private sealed class ReadField
        {
            public required string Name { get; init; }
            public required WatchDataType DataType { get; init; }
            public required uint Address { get; init; }
            public required int Offset { get; init; }
            public required int ByteCount { get; init; }
        }

        public class ChartPoint
        {
            public double X { get; set; }
            public double Y { get; set; }
        }

        private bool TryParseUserDouble(string text, out double value)
        {
            value = 0.0;
            if (string.IsNullOrWhiteSpace(text))
                return false;

            if (double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out value))
                return true;

            if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
                return true;

            string alt = text.Replace(',', '.');
            if (double.TryParse(alt, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
                return true;

            alt = text.Replace('.', ',');
            if (double.TryParse(alt, NumberStyles.Float, CultureInfo.CurrentCulture, out value))
                return true;

            return false;
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            LogEvent("motor-panel opened");
            await StartupSelfTest();

            _pollTimer.Start();

            stopwatch = Stopwatch.StartNew();

            DutycycleSeries.ItemsSource = dutycycleData;
            DutycycleSeries.XBindingPath = "X";
            DutycycleSeries.YBindingPath = "Y";

            SwitchEnableMotorOn();
            SwitchBlockFocOFF();

            try
            {
                ReloadSymbolsAndBuildReadLayout();
            }
            catch (Exception ex)
            {
                MessageBox.Show("ELF load failed: " + ex.Message,
                                "ELF error",
                                MessageBoxButton.OK,
                                MessageBoxImage.Error);
                LogEvent("ELF load failed: " + ex.Message);
            }
        }

        private void ReloadSymbolsAndBuildReadLayout()
        {
            if (string.IsNullOrWhiteSpace(_elfPath) || !File.Exists(_elfPath))
                throw new FileNotFoundException("ELF file not found.", _elfPath);

            _symbols = ElfSymbolReader.ReadObjectSymbols(_elfPath)
                                      .GroupBy(s => s.Name)
                                      .Select(g => g.First())
                                      .ToDictionary(s => s.Name, s => s);

            LogEvent($"Loaded {_symbols.Count} ELF symbols.");

            BuildReadLayout();
        }

        private void BuildReadLayout()
        {
            _readFields.Clear();
            _missingSymbolsLogged.Clear();

            var definitions = new Dictionary<string, WatchDataType>
            {
                [SymWeRef] = WatchDataType.Float,
                [SymWe] = WatchDataType.Float,
                [SymDuty] = WatchDataType.Float,
                [SymPcbTemp] = WatchDataType.Float,
                [SymBldcTemp] = WatchDataType.Float,
                [SymIBUS] = WatchDataType.Float,
                [SymVBUS] = WatchDataType.Float,

                [SymDirectionCw] = WatchDataType.UInt8,
                [SymStart] = WatchDataType.UInt8,

                [SymSystemState] = WatchDataType.UInt32
            };

            var present = new List<(string Name, ElfSymbol Symbol, WatchDataType Type, int ByteCount)>();

            foreach (var def in definitions)
            {
                if (_symbols.TryGetValue(def.Key, out var symbol))
                {
                    int byteCount = ValueCodec.GetByteCount(def.Value);
                    present.Add((def.Key, symbol, def.Value, byteCount));
                }
                else
                {
                    LogMissingSymbolOnce(def.Key);
                }
            }

            if (present.Count == 0)
                throw new InvalidOperationException("None of the expected GUI symbols were found in the ELF.");

            _readBlockBaseAddress = present.Min(p => p.Symbol.Address);
            uint maxEnd = present.Max(p => p.Symbol.Address + (uint)p.ByteCount);
            _readBlockSize = checked((int)(maxEnd - _readBlockBaseAddress));

            foreach (var item in present)
            {
                _readFields[item.Name] = new ReadField
                {
                    Name = item.Name,
                    DataType = item.Type,
                    Address = item.Symbol.Address,
                    Offset = checked((int)(item.Symbol.Address - _readBlockBaseAddress)),
                    ByteCount = item.ByteCount
                };
            }

            _readLayoutReady = true;

            LogEvent($"Prepared J-Link read block: 0x{_readBlockBaseAddress:X8} .. 0x{maxEnd - 1:X8} ({_readBlockSize} bytes)");
        }

        private void LogMissingSymbolOnce(string name)
        {
            if (_missingSymbolsLogged.Add(name))
                LogEvent($"ELF symbol not found: {name}");
        }

        private bool TryGetField(string symbolName, out ReadField field)
        {
            if (_readFields.TryGetValue(symbolName, out field!))
                return true;

            LogMissingSymbolOnce(symbolName);
            return false;
        }

        private bool TryReadFloatFromBlock(byte[] block, string symbolName, out float value)
        {
            value = 0.0f;

            if (!TryGetField(symbolName, out var field))
                return false;

            if (field.DataType != WatchDataType.Float)
                return false;

            if (field.Offset + 4 > block.Length)
                return false;

            value = BitConverter.ToSingle(block, field.Offset);
            return true;
        }

        private bool TryReadUInt8FromBlock(byte[] block, string symbolName, out byte value)
        {
            value = 0;

            if (!TryGetField(symbolName, out var field))
                return false;

            if (field.DataType != WatchDataType.UInt8)
                return false;

            if (field.Offset + 1 > block.Length)
                return false;

            value = block[field.Offset];
            return true;
        }

        private bool TryReadUInt32FromBlock(byte[] block, string symbolName, out uint value)
        {
            value = 0;

            if (!TryGetField(symbolName, out var field))
                return false;

            if (field.DataType != WatchDataType.UInt32)
                return false;

            if (field.Offset + 4 > block.Length)
                return false;

            value = BitConverter.ToUInt32(block, field.Offset);
            return true;
        }

        private async Task WriteFloatAsync(string symbolName, float value)
        {
            if (!_symbols.TryGetValue(symbolName, out var symbol))
                throw new InvalidOperationException($"ELF symbol not found: {symbolName}");

            var client = new JLinkCommanderClient(_jlinkSettings);
            byte[] raw = ValueCodec.ParseValue(value.ToString(CultureInfo.InvariantCulture), WatchDataType.Float);
            await client.WriteScalarAsync(symbol.Address, raw);
        }

        private async Task WriteUInt8Async(string symbolName, byte value)
        {
            if (!_symbols.TryGetValue(symbolName, out var symbol))
                throw new InvalidOperationException($"ELF symbol not found: {symbolName}");

            var client = new JLinkCommanderClient(_jlinkSettings);
            byte[] raw = ValueCodec.ParseValue(value.ToString(), WatchDataType.UInt8);
            await client.WriteScalarAsync(symbol.Address, raw);
        }

        private async Task WriteUInt32Async(string symbolName, uint value)
        {
            if (!_symbols.TryGetValue(symbolName, out var symbol))
                throw new InvalidOperationException($"ELF symbol not found: {symbolName}");

            var client = new JLinkCommanderClient(_jlinkSettings);
            byte[] raw = ValueCodec.ParseValue(value.ToString(), WatchDataType.UInt32);
            await client.WriteScalarAsync(symbol.Address, raw);
        }

        private async Task StartupSelfTest()
        {
            LedError.Fill = Brushes.Red;
            LedWarning.Fill = Brushes.Orange;
            LedEnabled.Fill = Brushes.Green;
            LedConnected.Fill = Brushes.Green;

            Ellipse[] leds = { LedError, LedWarning, LedEnabled, LedConnected };
            for (int i = 0; i < 3; i++)
            {
                foreach (var led in leds) led.Visibility = Visibility.Visible;
                await Task.Delay(200);
                foreach (var led in leds) led.Visibility = Visibility.Hidden;
                await Task.Delay(200);
            }

            LedError.Visibility = Visibility.Visible;
            LedWarning.Visibility = Visibility.Visible;
            LedEnabled.Visibility = Visibility.Visible;
            LedConnected.Visibility = Visibility.Visible;

            LedError.Fill = Brushes.WhiteSmoke;
            LedWarning.Fill = Brushes.WhiteSmoke;
            LedEnabled.Fill = Brushes.WhiteSmoke;
            LedConnected.Fill = Brushes.WhiteSmoke;

            AnimatePointer(NeedleSpeed, 2000);
            AnimatePointer(PointerSpeed, 2000);
            AnimatePointer(NeedleIBus, 40);
            AnimatePointer(NeedleVBus, 50);

            await Task.Delay(500);

            await Task.WhenAll(
                AnimateLinearPointer(PointerPCBTemp, 80),
                AnimateLinearPointer(BarPCBTemp, 80),
                AnimateLinearPointer(PointerWindingTemp, 80),
                AnimateLinearPointer(BarWindingTemp, 80)
            );

            await Task.Delay(500);

            AnimatePointer(NeedleSpeed, 0);
            AnimatePointer(PointerSpeed, 0);
            AnimatePointer(NeedleIBus, 0);
            AnimatePointer(NeedleVBus, 0);

            await Task.WhenAll(
                AnimateLinearPointer(PointerPCBTemp, 0),
                AnimateLinearPointer(BarPCBTemp, 0),
                AnimateLinearPointer(PointerWindingTemp, 0),
                AnimateLinearPointer(BarWindingTemp, 0)
            );

            LogEvent("startup self-test done");
        }

        private async void MenuItemSelfTest_Click(object sender, RoutedEventArgs e)
        {
            LogEvent("self-test started");
            await StartupSelfTest();
            LogEvent("self-test finished");
        }

        private void AnimatePointer(CircularPointer pointer, double toValue)
        {
            DoubleAnimation anim = new()
            {
                From = pointer.Value,
                To = toValue,
                Duration = TimeSpan.FromMilliseconds(400)
            };
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

        private void LogEvent(string message)
        {
            string timestamp = DateTime.Now.ToString("HH:mm:ss");
            string logEntry = $"[{timestamp}] {message}";

            EventLogListBox.Items.Insert(0, logEntry);
            EventLogListBox.ScrollIntoView(EventLogListBox.Items[0]);
        }

        private void LogSystemstateChange(uint state)
        {
            string stateName = GetSystemStateName(state);
            string timestamp = DateTime.Now.ToString("HH:mm:ss");
            string logEntry = $"[{timestamp}] {stateName} ({state})";

            EventLogListBox.Items.Insert(0, logEntry);
            EventLogListBox.ScrollIntoView(EventLogListBox.Items[0]);
        }

        private void TrimCollectionsIfNeeded()
        {
            while (dutycycleData.Count > MaxPoints)
            {
                double oldestX = double.MaxValue;
                if (dutycycleData.Count > 0) oldestX = Math.Min(oldestX, dutycycleData[0].X);

                if (oldestX == double.MaxValue)
                    break;

                if (dutycycleData.Count > 0 && dutycycleData[0].X == oldestX) dutycycleData.RemoveAt(0);

                foreach (var p in dutycycleData) p.X -= oldestX;

                baseTime += oldestX;
            }
        }

        private async void PollTimer_Tick(object? sender, EventArgs e)
        {
            if (!_readLayoutReady)
                return;

            _pollTimer.Stop();

            try
            {
                var client = new JLinkCommanderClient(_jlinkSettings);
                byte[] block = await client.ReadMemoryAsync(_readBlockBaseAddress, _readBlockSize);

                double t = stopwatch?.Elapsed.TotalSeconds ?? 0.0;
                t -= baseTime;

                // We -> actual speed
                if (TryReadFloatFromBlock(block, SymWe, out float we))
                {
                    NeedleSpeed.BeginAnimation(CircularPointer.ValueProperty, null);
                    NeedleSpeed.Value = we;
                }

                // We_ref -> target speed
                if (TryReadFloatFromBlock(block, SymWeRef, out float weRef))
                {
                    PointerSpeed.BeginAnimation(CircularPointer.ValueProperty, null);
                    PointerSpeed.Value = weRef;
                }


                // PCB temperature
                if (TryReadFloatFromBlock(block, SymPcbTemp, out float pcbTemp))
                {
                    PointerPCBTemp.Value = pcbTemp;
                    BarPCBTemp.Value = pcbTemp;
                }

                // BLDC / winding temperature
                if (TryReadFloatFromBlock(block, SymBldcTemp, out float bldcTemp))
                {
                    PointerWindingTemp.Value = bldcTemp;
                    BarWindingTemp.Value = bldcTemp;
                }

                // I_Bus
                if (TryReadFloatFromBlock(block, SymIBUS, out float ibus))
                {
                    NeedleIBus.BeginAnimation(CircularPointer.ValueProperty, null);
                    NeedleIBus.Value = ibus;
                }

                // V_Bus
                if (TryReadFloatFromBlock(block, SymVBUS, out float vbus))
                {
                    NeedleVBus.BeginAnimation(CircularPointer.ValueProperty, null);
                    NeedleVBus.Value = vbus;
                }

                if (TryReadUInt8FromBlock(block, SymDirectionCw, out byte directionCw))
                {
                    SwitchDirection.IsChecked = directionCw != 0;
                }


                if (TryReadUInt8FromBlock(block, SymStart, out byte start))
                {
                    SwitchStartStop.IsChecked = start != 0;
                }

                // Enum values
                if (TryReadUInt32FromBlock(block, SymSystemState, out uint systemState))
                {
                    ApplySystemState(systemState);
                }


                TrimCollectionsIfNeeded();

                DutycycleChart.InvalidateVisual();
            }
            catch (Exception ex)
            {
                LogEvent("poll error: " + ex.Message);
            }
            finally
            {
                _pollTimer.Start();
            }
        }

        private void ApplySystemState(uint state)
        {
            string stateName = GetSystemStateName(state);

            TxtSystemState.Text = $"{stateName} ({state})";

            // Always show the current system_state in the small system-state box (same behavior as MotorPanelFOC)
            try
            {
                string timestamp = DateTime.Now.ToString("HH:mm:ss");
                string currentEntry = $"[{timestamp}] {stateName} ({state})";
                SystemSateLogListBox.Items.Clear();
                SystemSateLogListBox.Items.Add(currentEntry);
                SystemSateLogListBox.ScrollIntoView(SystemSateLogListBox.Items[0]);
            }
            catch
            {
                // ignore UI update errors
            }

            if (_lastSystemState != state)
            {
                _lastSystemState = state;
                LogEvent($"system_state = {stateName} ({state})");
                LogSystemstateChange(state);
            }

            bool isFault = state == 14;
            bool isTransition = state is 1 or 2 or 4 or 5 or 7 or 8 or 9 or 11 or 12 or 13;

            LedError.Fill = isFault ? Brushes.Red : Brushes.WhiteSmoke;
            LedWarning.Fill = (!isFault && isTransition) ? Brushes.Orange : Brushes.WhiteSmoke;

            if (isFault)
                TxtSystemState.Foreground = Brushes.Red;
            else if (isTransition)
                TxtSystemState.Foreground = Brushes.DarkOrange;
            else
                TxtSystemState.Foreground = Brushes.DarkGreen;
        }


        private static string GetSystemStateName(uint state) => state switch
        {
            0 => "SYSTEM_OFF",
            1 => "SYSTEM_INIT",
            2 => "SYSTEM_ENABLE",
            3 => "SYSTEM_READY",
            4 => "SYSTEM_STARTUP_FOC_PREALIGN",
            5 => "SYSTEM_STARTUP_FOC_OPENLOOP",
            6 => "SYSTEM_RUN_FOC",
            7 => "SYSTEM_SHUTDOWN_FOC_CMD",
            8 => "SYSTEM_SHUTDOWN_FOC",
            9 => "SYSTEM_STARTUP_BLOCK",
            10 => "SYSTEM_RUN_BLOCK",
            11 => "SYSTEM_SHUTDOWN_BLOCK_CMD",
            12 => "SYSTEM_SHUTDOWN_BLOCK",
            13 => "SYSTEM_DISABLE",
            14 => "SYSTEM_FAULT",
            _ => "UNKNOWN"
        };

        private async void BtnApplyTarget_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (TryParseUserDouble(txtbTargetSpeed.Text, out double speed))
                {
                    await WriteFloatAsync(SymWeRef, (float)speed);
                    LogEvent($"wrote {SymWeRef} = {speed}");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Write error: " + ex.Message,
                                "J-Link write",
                                MessageBoxButton.OK,
                                MessageBoxImage.Error);
                LogEvent("write error: " + ex.Message);
            }
        }

        private async void SwitchEnableMotorOn()
        {
            try
            {
                await WriteUInt8Async(SymEnable, (byte)1);
                LogEvent("System enabled");
            }
            catch (Exception ex)
            {
                LogEvent("write error: " + ex.Message);
            }
        }

        private async void SwitchEnableMotorOFF()
        {
            try
            {
                await WriteUInt8Async(SymEnable, (byte)0);
                LogEvent("System disabled");
            }
            catch (Exception ex)
            {
                LogEvent("write error: " + ex.Message);
            }
        }

        private async void SwitchStartStop_Click(object sender, RoutedEventArgs e)
        {
            bool on = SwitchStartStop.IsChecked == true;
            try
            {
                await WriteUInt8Async(SymStart, on ? (byte)1 : (byte)0);
                LogEvent(on ? "Start = 1" : "Start = 0");
            }
            catch (Exception ex)
            {
                LogEvent("write error: " + ex.Message);
            }
        }

        private async void SwitchDirection_Click(object sender, RoutedEventArgs e)
        {
            bool cw = SwitchDirection.IsChecked == true;
            try
            {
                await WriteUInt8Async(SymDirectionCw, cw ? (byte)1 : (byte)0);
                LogEvent(cw ? "Direction_CW = 1" : "Direction_CW = 0");
            }
            catch (Exception ex)
            {
                LogEvent("write error: " + ex.Message);
            }
        }

        private async void SwitchBlockFocOFF()
        {
            try
            {
                await WriteUInt8Async(SymFoc, (byte)0);
                LogEvent("FOC Off");
            }
            catch (Exception ex)
            {
                LogEvent("write error: " + ex.Message);
            }
        }

        private void txtbTargetSpeed_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtbTargetSpeed.Text))
            {
                AnimatePointer(PointerSpeed, 0);
                return;
            }

            if (TryParseUserDouble(txtbTargetSpeed.Text, out double speed))
            {
                PointerSpeed.BeginAnimation(CircularPointer.ValueProperty, null);
                PointerSpeed.Value = speed;
            }
        }

        private void info_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("ELF + J-Link mode active.", "info", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void help_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Controls now read and write directly through ELF symbol addresses via J-Link.", "help", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BackToSetupWindow_Click(object sender, RoutedEventArgs e)
        {
            SwitchEnableMotorOFF();

            var SetupWindow = new SetupWindow();
            SetupWindow.Show();
            this.Close();
        }
    }
}
