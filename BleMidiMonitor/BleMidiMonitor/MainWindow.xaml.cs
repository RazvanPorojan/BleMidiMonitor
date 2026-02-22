using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.ObjectModel;

namespace BleMidiMonitor
{
    public sealed partial class MainWindow : Window
    {
        private BleMidiManager _bleMidiManager;
        private ObservableCollection<BleMidiDevice> _devices;
        private ObservableCollection<string> _midiLog;
        private BleMidiDevice _connectedDevice;
        private int _messageCount = 0;
        private const int MaxLogMessages = 1000;
        private FretState _fretState;
        private FretboardWindow _fretboardWindow;

        public MainWindow()
        {
            InitializeComponent();

            _devices = new ObservableCollection<BleMidiDevice>();
            DeviceListView.ItemsSource = _devices;

            _midiLog = new ObservableCollection<string>();
            MidiLogListView.ItemsSource = _midiLog;

            _fretState = new FretState();

            _bleMidiManager = new BleMidiManager();
            _bleMidiManager.DeviceDiscovered += OnDeviceDiscovered;
            _bleMidiManager.ErrorOccurred += OnErrorOccurred;
            _bleMidiManager.ConnectionStatusChanged += OnConnectionStatusChanged;
            _bleMidiManager.MidiDataReceived += OnMidiDataReceived;
        }

        private void ScanButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Clear previous results
                _devices.Clear();
                StatusText.Text = "Scanning for BLE MIDI devices...";
                ScanButton.IsEnabled = false;
                StopScanButton.IsEnabled = true;

                // Start scanning with 30-second timeout
                _bleMidiManager.StartScanning(30);

                // Re-enable button after scan timeout
                System.Threading.Tasks.Task.Delay(30000).ContinueWith(_ =>
                {
                    DispatcherQueue.TryEnqueue(() =>
                    {
                        ScanButton.IsEnabled = true;
                        StopScanButton.IsEnabled = false;

                        if (_devices.Count == 0)
                        {
                            StatusText.Text = "No BLE MIDI devices found. Make sure Bluetooth is enabled and your device is powered on.";
                        }
                        else
                        {
                            StatusText.Text = $"Found {_devices.Count} BLE MIDI device(s). Click a device to connect.";
                        }
                    });
                });
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Error starting scan: {ex.Message}";
                ScanButton.IsEnabled = true;
                StopScanButton.IsEnabled = false;
            }
        }

        private void StopScanButton_Click(object sender, RoutedEventArgs e)
        {
            _bleMidiManager.StopScanning();
            ScanButton.IsEnabled = true;
            StopScanButton.IsEnabled = false;

            if (_devices.Count == 0)
            {
                StatusText.Text = "Scan stopped. No devices found.";
            }
            else
            {
                StatusText.Text = $"Scan stopped. Found {_devices.Count} device(s). Click a device to connect.";
            }
        }

        private void OnDeviceDiscovered(object sender, BleMidiDevice device)
        {
            // Update UI on the UI thread
            DispatcherQueue.TryEnqueue(() =>
            {
                _devices.Add(device);
                StatusText.Text = $"Scanning... Found {_devices.Count} device(s). (Click Stop to finish early)";
            });
        }

        private void OnErrorOccurred(object sender, string error)
        {
            // Update UI on the UI thread
            DispatcherQueue.TryEnqueue(() =>
            {
                StatusText.Text = $"Error: {error}";
            });
        }

        private async void DeviceListView_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is BleMidiDevice device)
            {
                // Don't allow connection while scanning
                if (_bleMidiManager.IsScanning)
                {
                    StatusText.Text = "Please wait for scan to complete before connecting.";
                    return;
                }

                // If already connected to this device, ignore
                if (device.IsConnected)
                {
                    return;
                }

                // Disconnect from any existing device
                if (_connectedDevice != null)
                {
                    await _bleMidiManager.DisconnectAsync();
                }

                StatusText.Text = $"Connecting to {device.Name}...";
                ScanButton.IsEnabled = false;
                DisconnectButton.IsEnabled = false;

                bool success = await _bleMidiManager.ConnectToDeviceAsync(device);

                if (success)
                {
                    _connectedDevice = device;
                    StatusText.Text = $"Connected to {device.Name}";
                    DisconnectButton.IsEnabled = true;
                }
                else
                {
                    StatusText.Text = $"Failed to connect to {device.Name}";
                    ScanButton.IsEnabled = true;
                }
            }
        }

        private async void DisconnectButton_Click(object sender, RoutedEventArgs e)
        {
            if (_connectedDevice != null)
            {
                StatusText.Text = "Disconnecting...";
                DisconnectButton.IsEnabled = false;

                await _bleMidiManager.DisconnectAsync();

                _connectedDevice.IsConnected = false;
                _connectedDevice = null;

                StatusText.Text = "Disconnected";
                ScanButton.IsEnabled = true;
            }
        }

        private void OnConnectionStatusChanged(object sender, bool isConnected)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                if (!isConnected && _connectedDevice != null)
                {
                    _connectedDevice.IsConnected = false;
                    _connectedDevice = null;
                    StatusText.Text = "Device disconnected";
                    DisconnectButton.IsEnabled = false;
                    ScanButton.IsEnabled = true;
                }
            });
        }

        private void OnMidiDataReceived(object sender, byte[] data)
        {
            try
            {
                var messages = MidiMessageParser.ParseBleMidiPacket(data);

                foreach (var message in messages)
                {
                    string logEntry = $"[{message.Timestamp:HH:mm:ss.fff}] {message.FormattedMessage}";

                    // Update log immediately with High priority
                    DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.High, () =>
                    {
                        _midiLog.Add(logEntry);

                        // Remove old messages to maintain max count
                        if (_midiLog.Count > MaxLogMessages)
                        {
                            _midiLog.RemoveAt(0);
                        }
                    });

                    _messageCount++;
                    ProcessFretEvent(message);
                }

                // Update counter less frequently (every 10 messages)
                if (_messageCount % 10 == 0)
                {
                    DispatcherQueue.TryEnqueue(() =>
                    {
                        MessageCountText.Text = $"Messages: {_messageCount}";
                    });
                }
            }
            catch (Exception ex)
            {
                DispatcherQueue.TryEnqueue(() =>
                {
                    StatusText.Text = $"Error parsing MIDI data: {ex.Message}";
                });
            }
        }

        private void ProcessFretEvent(MidiMessage message)
        {
            // AeroBand Fret mapping:
            // Channel = string number (1-6, where 6 is low E)
            // Controller 49 = "note on" - fret touched
            // Controller 50 = "note off" - fret released
            // Value = fret number (1-22)

            if (message.MessageType == "Control Change")
            {
                int controller = message.Data1;
                int fretNumber = message.Data2;
                int stringNumber = message.Channel;

                if (controller == 49) // Fret touched
                {
                    _fretState.UpdateFret(stringNumber, fretNumber, true);
                }
                else if (controller == 50) // Fret released
                {
                    _fretState.UpdateFret(stringNumber, fretNumber, false);
                }
            }
        }

        private void ClearLogButton_Click(object sender, RoutedEventArgs e)
        {
            _midiLog.Clear();
            _messageCount = 0;
            MessageCountText.Text = "Messages: 0";
        }

        private void ShowFretboardButton_Click(object sender, RoutedEventArgs e)
        {
            if (_fretboardWindow == null)
            {
                _fretboardWindow = new FretboardWindow(_fretState);
                _fretboardWindow.Closed += (s, args) =>
                {
                    _fretboardWindow?.Cleanup();
                    _fretboardWindow = null;
                };
                _fretboardWindow.Activate();
            }
            else
            {
                _fretboardWindow.Activate();
            }
        }
    }
}
