using Reactive.Bindings;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.Advertisement;
using Windows.Devices.Bluetooth.GenericAttributeProfile;

namespace bleus.BLE
{
    enum PairingStatus
    {
        Disconnected,   // 未接続
        Connected,  // 接続
        Bonded, // 
    }

    // BLE Client実装
    internal static class Central
    {
        // APIデータ
        static BluetoothLEAdvertisementWatcher advertisementWatcher;
        // 制御データ
        public static List<Device> Devices;
        public static Dictionary<ulong, Device> DevicesMap;
        public static readonly object lockDevices = new object();

        static Central()
        {
            //
            advertisementWatcher = new BluetoothLEAdvertisementWatcher();
            //
            Devices = new List<Device>();
            DevicesMap = new Dictionary<ulong, Device>();
        }

        public static bool Setup()
        {
            // 
            advertisementWatcher.Received += AdvertisementWatcher_Received;
            advertisementWatcher.ScanningMode = BluetoothLEScanningMode.Active;

            return true;
        }

        public static void StartScan()
        {
            advertisementWatcher.Start();
        }
        public static void StopScan()
        {
            advertisementWatcher.Stop();
        }

        public static void ResetDevices()
        {
            // Devices情報をクリア
            DevicesMap.Clear();
            Devices.Clear();
        }

        static async void AdvertisementWatcher_Received(BluetoothLEAdvertisementWatcher sender, BluetoothLEAdvertisementReceivedEventArgs args)
        {
            // lock内でawaitできないので外で実行、排他制御的には特に問題ない
            BluetoothLEDevice dev = await BluetoothLEDevice.FromBluetoothAddressAsync(args.BluetoothAddress);
            if (!(dev is null))
            {
                // GUIスレッド側の処理が終わるまで待機する
                lock (lockDevices)
                {
                    // デバイス情報が取得済みか判定, 未取得ならリストに登録
                    var key = args.BluetoothAddress;
                    if (!DevicesMap.ContainsKey(key))
                    {
                        var temp = new Device(args);
                        DevicesMap.Add(key, temp);
                        Devices.Add(temp);
                    }
                    else
                    {
                        // 取得済みのデバイスも再通知される
                    }
                    // デバイス情報取得, 失敗することはありえないが一応チェック
                    if (DevicesMap.TryGetValue(key, out var device))
                    {
                        var timestamp = DateTime.Now;
                        device.Update(timestamp, args, dev);
                    }
                }
            }
            else
            {
                // nullになることがある
                // 何故？
            }



            //GattDeviceService service = dev.GetGattService(new Guid("00002220-0000-1000-8000-00805f9b34fb"));
            //var characteristicsRx = service.GetCharacteristics(new Guid("00002221-0000-1000-8000-00805f9b34fb"));
            //CHARACTERISTIC_UUID_RX = characteristicsRx.First();
            //if (CHARACTERISTIC_UUID_RX.CharacteristicProperties.HasFlag(GattCharacteristicProperties.Notify))
            //{
            //    CHARACTERISTIC_UUID_RX.ValueChanged += characteristicBleDevice;
            //    await CHARACTERISTIC_UUID_RX.WriteClientCharacteristicConfigurationDescriptorAsync(GattClientCharacteristicConfigurationDescriptorValue.Notify);
            //    isBleNotify = true;
            //}
            //var characteristicsTx = service.GetCharacteristics(new Guid("00002222-0000-1000-8000-00805f9b34fb"));
            //CHARACTERISTIC_UUID_TX = characteristicsTx.First();
            //if (CHARACTERISTIC_UUID_TX.CharacteristicProperties.HasFlag(GattCharacteristicProperties.Write))
            //{
            //    isBleWrite = true;
            //}
            //var bleServiceUUIDs = args.Advertisement.ServiceUuids;
            //foreach (var uuidone in bleServiceUUIDs)
            //{
            //}
        }
    }
}
