using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.Advertisement;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Storage.Streams;

namespace BleMidiMonitor
{
    public class BleMidiManager
    {
        // BLE MIDI Service UUID
        private static readonly Guid BleMidiServiceGuid = new Guid("03B80E5A-EDE8-4B33-A751-6CE34EC4C700");

        // BLE MIDI Characteristic UUID
        private static readonly Guid BleMidiCharacteristicGuid = new Guid("7772E5DB-3868-4112-A1A9-F2669D106BF3");

        private BluetoothLEAdvertisementWatcher _watcher;
        private Dictionary<ulong, BleMidiDevice> _discoveredDevices = new Dictionary<ulong, BleMidiDevice>();
        private BluetoothLEDevice _connectedDevice;
        private GattCharacteristic _midiCharacteristic;

        public event EventHandler<BleMidiDevice> DeviceDiscovered;
        public event EventHandler<string> ErrorOccurred;
        public event EventHandler<byte[]> MidiDataReceived;
        public event EventHandler<bool> ConnectionStatusChanged;

        public bool IsScanning => _watcher?.Status == BluetoothLEAdvertisementWatcherStatus.Started;

        public void StartScanning(int timeoutSeconds = 30)
        {
            try
            {
                // Clear previous discoveries
                _discoveredDevices.Clear();

                // Create watcher if needed
                if (_watcher == null)
                {
                    _watcher = new BluetoothLEAdvertisementWatcher();
                    _watcher.ScanningMode = BluetoothLEScanningMode.Active;

                    // Filter for BLE MIDI service UUID
                    _watcher.AdvertisementFilter.Advertisement.ServiceUuids.Add(BleMidiServiceGuid);

                    _watcher.Received += OnAdvertisementReceived;
                    _watcher.Stopped += OnWatcherStopped;
                }

                // Start watching
                _watcher.Start();

                // Auto-stop after timeout
                Task.Delay(timeoutSeconds * 1000).ContinueWith(_ =>
                {
                    if (_watcher?.Status == BluetoothLEAdvertisementWatcherStatus.Started)
                    {
                        _watcher.Stop();
                    }
                });
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(this, $"Error starting scan: {ex.Message}");
            }
        }

        public void StopScanning()
        {
            try
            {
                if (_watcher?.Status == BluetoothLEAdvertisementWatcherStatus.Started)
                {
                    _watcher.Stop();
                }
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(this, $"Error stopping scan: {ex.Message}");
            }
        }

        private async void OnAdvertisementReceived(BluetoothLEAdvertisementWatcher sender, BluetoothLEAdvertisementReceivedEventArgs args)
        {
            try
            {
                // Check if we've already discovered this device
                if (_discoveredDevices.ContainsKey(args.BluetoothAddress))
                {
                    return;
                }

                // Get device name from advertisement
                string deviceName = args.Advertisement.LocalName;

                // If name is empty, try to get it from the device
                if (string.IsNullOrEmpty(deviceName))
                {
                    try
                    {
                        var device = await BluetoothLEDevice.FromBluetoothAddressAsync(args.BluetoothAddress);
                        if (device != null)
                        {
                            deviceName = device.Name;
                            device.Dispose();
                        }
                    }
                    catch
                    {
                        // Ignore errors getting device name
                    }
                }

                // Use address as fallback name
                if (string.IsNullOrEmpty(deviceName))
                {
                    deviceName = $"BLE MIDI Device {args.BluetoothAddress:X12}";
                }

                // Create device model
                var bleMidiDevice = new BleMidiDevice
                {
                    Name = deviceName,
                    BluetoothAddress = args.BluetoothAddress,
                    DeviceId = $"BluetoothLE#{args.BluetoothAddress:X12}",
                    IsConnected = false
                };

                _discoveredDevices[args.BluetoothAddress] = bleMidiDevice;

                // Notify listeners
                DeviceDiscovered?.Invoke(this, bleMidiDevice);
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(this, $"Error processing advertisement: {ex.Message}");
            }
        }

        private void OnWatcherStopped(BluetoothLEAdvertisementWatcher sender, BluetoothLEAdvertisementWatcherStoppedEventArgs args)
        {
            // Scanning stopped
        }

        public async Task<bool> ConnectToDeviceAsync(BleMidiDevice device, int timeoutSeconds = 10)
        {
            try
            {
                // Disconnect any existing device
                await DisconnectAsync();

                // Create cancellation token for timeout
                using (var cts = new System.Threading.CancellationTokenSource(timeoutSeconds * 1000))
                {
                    // Connect to the device
                    _connectedDevice = await BluetoothLEDevice.FromBluetoothAddressAsync(device.BluetoothAddress);

                    if (_connectedDevice == null)
                    {
                        ErrorOccurred?.Invoke(this, "Failed to connect to device. Make sure the device is powered on and in range.");
                        return false;
                    }

                    // Get MIDI service
                    var servicesResult = await _connectedDevice.GetGattServicesForUuidAsync(BleMidiServiceGuid);

                    if (servicesResult.Status != GattCommunicationStatus.Success || servicesResult.Services.Count == 0)
                    {
                        string errorMsg = servicesResult.Status == GattCommunicationStatus.Unreachable
                            ? "Device unreachable. Make sure it's powered on and in range."
                            : "MIDI service not found on device. Make sure this is a BLE MIDI device.";
                        ErrorOccurred?.Invoke(this, errorMsg);
                        await DisconnectAsync();
                        return false;
                    }

                    var midiService = servicesResult.Services[0];

                    // Get MIDI characteristic
                    var characteristicsResult = await midiService.GetCharacteristicsForUuidAsync(BleMidiCharacteristicGuid);

                    if (characteristicsResult.Status != GattCommunicationStatus.Success || characteristicsResult.Characteristics.Count == 0)
                    {
                        ErrorOccurred?.Invoke(this, "MIDI characteristic not found. This device may not support BLE MIDI.");
                        await DisconnectAsync();
                        return false;
                    }

                    _midiCharacteristic = characteristicsResult.Characteristics[0];

                    // Subscribe to notifications
                    var notifyResult = await _midiCharacteristic.WriteClientCharacteristicConfigurationDescriptorAsync(
                        GattClientCharacteristicConfigurationDescriptorValue.Notify);

                    if (notifyResult != GattCommunicationStatus.Success)
                    {
                        ErrorOccurred?.Invoke(this, "Failed to enable MIDI notifications. Try reconnecting.");
                        await DisconnectAsync();
                        return false;
                    }

                    // Register for value changes
                    _midiCharacteristic.ValueChanged += OnMidiCharacteristicValueChanged;

                    // Listen for connection status changes
                    _connectedDevice.ConnectionStatusChanged += OnConnectionStatusChanged;

                    device.IsConnected = true;
                    ConnectionStatusChanged?.Invoke(this, true);

                    return true;
                }
            }
            catch (System.Threading.Tasks.TaskCanceledException)
            {
                ErrorOccurred?.Invoke(this, $"Connection timeout after {timeoutSeconds} seconds. Make sure the device is in range.");
                await DisconnectAsync();
                return false;
            }
            catch (Exception ex)
            {
                string errorMsg = ex.Message.Contains("Bluetooth")
                    ? "Bluetooth connection error. Make sure Bluetooth is enabled on your PC."
                    : $"Connection error: {ex.Message}";
                ErrorOccurred?.Invoke(this, errorMsg);
                await DisconnectAsync();
                return false;
            }
        }

        public async Task DisconnectAsync()
        {
            try
            {
                if (_midiCharacteristic != null)
                {
                    _midiCharacteristic.ValueChanged -= OnMidiCharacteristicValueChanged;
                    _midiCharacteristic = null;
                }

                if (_connectedDevice != null)
                {
                    _connectedDevice.ConnectionStatusChanged -= OnConnectionStatusChanged;
                    _connectedDevice.Dispose();
                    _connectedDevice = null;
                }

                ConnectionStatusChanged?.Invoke(this, false);
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(this, $"Disconnect error: {ex.Message}");
            }
        }

        private void OnMidiCharacteristicValueChanged(GattCharacteristic sender, GattValueChangedEventArgs args)
        {
            try
            {
                // Read the MIDI data
                byte[] data;
                using (var reader = DataReader.FromBuffer(args.CharacteristicValue))
                {
                    data = new byte[reader.UnconsumedBufferLength];
                    reader.ReadBytes(data);
                }

                // Notify listeners
                MidiDataReceived?.Invoke(this, data);
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(this, $"Error reading MIDI data: {ex.Message}");
            }
        }

        private void OnConnectionStatusChanged(BluetoothLEDevice sender, object args)
        {
            if (sender.ConnectionStatus == BluetoothConnectionStatus.Disconnected)
            {
                ConnectionStatusChanged?.Invoke(this, false);
            }
        }

        public void Dispose()
        {
            StopScanning();
            DisconnectAsync().Wait();
        }
    }
}
