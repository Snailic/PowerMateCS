using System;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Devices.Enumeration;
using System.Collections.Generic;
using Windows.Storage.Streams;
using Windows.Devices.Bluetooth.Advertisement;

namespace PowerMateCS
{
    class Powermate
    {
        public string processName { get; set; }
        public int processId { get; set; }
        public GattCharacteristic readCharacteristic { get; set; }

        public bool isSubscribing = false;

        public Powermate(string name)
        {
            this.processName = name;
            this.processId = -1;
        }

    }

    class Program
    {
        private static BluetoothLEAdvertisementWatcher deviceWatcher;
        private static List<BluetoothLEDevice> bleDevices = new List<BluetoothLEDevice>();
        
        private readonly static string uuidRead = "9cf53570-ddd9-47f3-ba63-09acefc60415";
        private readonly static Guid guidRead = new Guid(uuidRead);

        private readonly static string uuidLed = "847d189e-86ee-4bd2-966f-800832b1259d";
        private readonly static Guid guidLed = new Guid(uuidLed);

        private readonly static Dictionary<string, Powermate> blePowermates = new Dictionary<string, Powermate> {
            { "00:12:92:08:2b:a1", new Powermate("vivaldi") },
            { "00:12:92:08:2d:f9", new Powermate("vivaldi") },
            { "00:12:92:08:2b:c8", new Powermate("vivaldi") }
        };

        static void Main(string[] args)
        {

            StartBleDeviceWatcher();

            Console.ReadKey();

            StopBleDeviceWatcher();
        }

        private static void StartBleDeviceWatcher()
        {
            deviceWatcher = new BluetoothLEAdvertisementWatcher
            {
                ScanningMode = BluetoothLEScanningMode.Active
            };

            deviceWatcher.Received += DeviceWatcher_Received;

            deviceWatcher.Start();
            Console.WriteLine("Listening for BLE Devices...");
        }

        private static void StopBleDeviceWatcher()
        {
            if (deviceWatcher != null)
            {
                // Unregister the event handlers.
                deviceWatcher.Received -= DeviceWatcher_Received;

                // Stop the watcher.
                deviceWatcher.Stop();
                deviceWatcher = null;
            }
        }

        private static async void DeviceWatcher_Received(BluetoothLEAdvertisementWatcher sender, BluetoothLEAdvertisementReceivedEventArgs bleArgs)
        {
            // Protect against race condition if the task runs after the app stopped the deviceWatcher.
            if (sender == deviceWatcher)
            {
                string _ = bleArgs.BluetoothAddress.ToString("X");
                
                if (!_.StartsWith("1292") || _.Length >  10)
                {
                    return;
                }

                BluetoothLEDevice bleDevice = await BluetoothLEDevice.FromBluetoothAddressAsync(bleArgs.BluetoothAddress);

                if (bleDevice != null)
                {
                    DeviceInformation bleDeviceInfo = bleDevice.DeviceInformation;
                    if (true)
                    {
                        if (bleDeviceInfo.Name.Contains("PowerMate"))
                        {
                            string bleReceiver = bleDeviceInfo.Id.Substring(23, 17);
                            string bleSender = bleDeviceInfo.Id.Substring(41);
                            Console.WriteLine(String.Format("Discovered {0} - {1} | Paired: {2} | {3}", bleReceiver, bleSender, bleDeviceInfo.Pairing.IsPaired, bleDeviceInfo.Name));

                            if (!blePowermates.ContainsKey(bleSender))
                            {
                                Console.WriteLine("└Unknown PowerMate");
                                return;
                            }

                            if (!blePowermates[bleSender].isSubscribing)
                            {
                                blePowermates[bleSender].isSubscribing = true;
                                GattDeviceServicesResult result = await bleDevice.GetGattServicesAsync(BluetoothCacheMode.Uncached);          

                                if (result.Status == GattCommunicationStatus.Success)
                                {
                                    var services = result.Services;
                                    Console.WriteLine(String.Format("Found {0} services", services.Count));
                                    foreach (GattDeviceService service in services)
                                    {
                                        Console.WriteLine(String.Format("└{0}", service.Uuid));
                                    }

                                    GetCharacteristics(services[3]);

                                }
                                else
                                {
                                    blePowermates[bleSender].isSubscribing = false;
                                    Console.WriteLine("Device unreachable");
                                }
                            }
                        }
                    }
                }
            }
        }

        private static async void GetCharacteristics(GattDeviceService service)
        {
            IReadOnlyList<GattCharacteristic> characteristics = null;
            string device = service.Device.DeviceInformation.Id.Substring(41);
            try
            {
                var accessStatus = await service.RequestAccessAsync();
                if(accessStatus == DeviceAccessStatus.Allowed)
                {
                    GattCharacteristicsResult result = await service.GetCharacteristicsAsync(BluetoothCacheMode.Uncached);

                    if(result.Status == GattCommunicationStatus.Success)
                    {
                        characteristics = result.Characteristics;
                        Console.WriteLine("Found Characteristics");
                    }
                    else
                    {
                        blePowermates[device].isSubscribing = false;
                        Console.WriteLine("Error accessing service.");
                        characteristics = new List<GattCharacteristic>();
                    }
                }
                else
                {
                    blePowermates[service.DeviceId].isSubscribing = false;
                    Console.WriteLine("Error accessing service.");
                }
            } catch (Exception ex)
            {
                blePowermates[device].isSubscribing = false;
                Console.WriteLine("Error: Restricted service. Can't read characteristics: " + ex.ToString());
            }

            if(characteristics != null)
            {
                foreach(GattCharacteristic characteristic in characteristics)
                {
                    Console.WriteLine("└Characteristic uuid: " + characteristic.Uuid.ToString());
                    if (uuid_equal(uuidRead, characteristic.Uuid))
                    {
                        SubscribeToValueChange(characteristic);
                        Console.WriteLine(" └Subscribing to Read Characteristic");
                    }
                }
            }
        }

        public static bool uuid_equal(string left, Guid right)
        {
            return (right.ToString().CompareTo(left) == 0);
        }

        private static async void SubscribeToValueChange(GattCharacteristic characteristic)
        {
            string device = characteristic.Service.Device.DeviceInformation.Id.Substring(41);

            GattClientCharacteristicConfigurationDescriptorValue gcccdValue = GattClientCharacteristicConfigurationDescriptorValue.None;
            if((characteristic.CharacteristicProperties & GattCharacteristicProperties.Indicate) != GattCharacteristicProperties.None)
            {
                gcccdValue = GattClientCharacteristicConfigurationDescriptorValue.Indicate;
            }else if ((characteristic.CharacteristicProperties & GattCharacteristicProperties.Notify) != GattCharacteristicProperties.None)
            {
                gcccdValue = GattClientCharacteristicConfigurationDescriptorValue.Notify;
            }
            else
            {
                blePowermates[device].isSubscribing = false;
                Console.WriteLine("Couldn't set Characteristic Configuration Descriptor");
            }

            try
            {
                GattCommunicationStatus status = await characteristic.WriteClientCharacteristicConfigurationDescriptorAsync(gcccdValue);

                if(status == GattCommunicationStatus.Success)
                {
                    blePowermates[device].readCharacteristic = characteristic;
                    blePowermates[device].isSubscribing = false;

                    AddValueChangedHandler(blePowermates[device].readCharacteristic);
                    Console.WriteLine("Successfully subscribed for value changes");
                }
            }catch(Exception ex)
            {
                blePowermates[device].isSubscribing = false;
                Console.WriteLine("Error registering for value changes: Status = ", ex.Message);
            }
        }

        private static void AddValueChangedHandler(GattCharacteristic characteristic)
        {
            characteristic.ValueChanged += Characteristic_ValueChanged;
        }

        private static void Characteristic_ValueChanged(GattCharacteristic sender, GattValueChangedEventArgs args)
        {
            IBuffer valueBuffer = args.CharacteristicValue;
            byte value = DataReader.FromBuffer(valueBuffer).ReadByte();
            ActOnPowerMate(sender, value);
        }

        enum  POWERMATE_ACTIONS : byte
        {
            PRESS = 0x65,
            LONG_RELEASE = 0x66, // This seems to be less reliably sent
            LEFT = 0x67,
            RIGHT = 0x68,
            PRESSED_LEFT = 0x69,
            PRESSED_RIGHT = 0x70,
            HOLD_1 = 0x71,
            HOLD_2 = 0x72,
            HOLD_3 = 0x74,
            HOLD_4 = 0x75,
            HOLD_5 = 0x76,
            HOLD_6 = 0x77
        };

        public static int GetProcessIDByName(string name)
        {
            foreach (var process in AppVolumeController.GetAudioProcesses())
            {
                Console.WriteLine(String.Format("id: {0} | name: {1} | title: {2}", process.Id, process.ProcessName, process.MainWindowTitle));
                if (process.ProcessName == name)
                {
                    return process.Id;
                }
            }
            return -1;
        }
        public static void StepVolume(float stepper, string sender)
        {
            float? volume = AppVolumeController.GetApplicationVolume(blePowermates[sender].processId) ?? -1f;

            if (!volume.HasValue || volume == -1f || blePowermates[sender].processId == 0)
            {
                blePowermates[sender].processId = GetProcessIDByName(blePowermates[sender].processName);
                volume = AppVolumeController.GetApplicationVolume(blePowermates[sender].processId);
            }

            AppVolumeController.SetApplicationVolume(blePowermates[sender].processId, (float)volume.Value + stepper);
        }

        public static void ActOnPowerMate(GattCharacteristic sender, byte value)
        {
            string device = sender.Service.Device.DeviceInformation.Id.Substring(41);
            Console.WriteLine("Sender: " + device + " | Action: 0x" + value.ToString("X2"));
            
            switch ((POWERMATE_ACTIONS)value)
            {
                case (POWERMATE_ACTIONS.PRESS):
                    bool? muted = AppVolumeController.GetApplicationMute(blePowermates[device].processId);
                    if(muted == null) {
                        blePowermates[device].processId = GetProcessIDByName(blePowermates[device].processName);
                        AppVolumeController.SetApplicationMute(blePowermates[device].processId, (bool)!AppVolumeController.GetApplicationMute(blePowermates[device].processId));
                    }
                    else
                    {
                        AppVolumeController.SetApplicationMute(blePowermates[device].processId, (bool)!muted);
                    }
                    break;
                case (POWERMATE_ACTIONS.LONG_RELEASE):
                    break;
                case (POWERMATE_ACTIONS.LEFT):
                    StepVolume(-2f, device);
                    break;
                case (POWERMATE_ACTIONS.RIGHT):
                    StepVolume(2f, device);
                    break;
                case (POWERMATE_ACTIONS.PRESSED_LEFT):
                    break;
                case (POWERMATE_ACTIONS.PRESSED_RIGHT):
                    break;
                case (POWERMATE_ACTIONS.HOLD_1):
                    break;
                case (POWERMATE_ACTIONS.HOLD_2):
                    break;
                case (POWERMATE_ACTIONS.HOLD_3):
                    break;
                case (POWERMATE_ACTIONS.HOLD_4):
                    break;
                case (POWERMATE_ACTIONS.HOLD_5):
                    break;
                case (POWERMATE_ACTIONS.HOLD_6):
                    break;
                default:
                    Console.WriteLine("└Unknown PowerMate Action " + value);
                    break;
            }
        }
    }
}
