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
using Windows.Devices.Enumeration;

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
        public static bool IsScanStopped;   //Stop() or エラーで停止した

        static Central()
        {
            //
            advertisementWatcher = new BluetoothLEAdvertisementWatcher();
            //
            Devices = new List<Device>();
            DevicesMap = new Dictionary<ulong, Device>();
            //
            IsScanStopped = false;
        }

        public static bool Setup()
        {
            // Microsoft参考
            // https://learn.microsoft.com/ja-jp/samples/microsoft/windows-universal-samples/bluetoothle/

            // (a) Bond/Pairingしていない状態からスタートするケース
            //   https://github.com/microsoft/Windows-universal-samples/blob/main/Samples/BluetoothAdvertisement/cs/Scenario1_Watcher.xaml.cs
            // 1. BluetoothLEAdvertisementWatcherによる監視
            //  未接続のPeripheralはAdvertising動作をしていて、GAP(Generic Access Profile)を発信している。
            //  Advertisingで使用するGAPは AdvertisingData と ScanResponseData の2種類になる。ペイロード最大長が31バイト
            //  Peripheralは定期的にAdvertisingDataを発信していて、CentralからのScanResponseRequestを受信するとScanResponseDataを発信する。
            //   -> BluetoothLEAdvertisementWatcherで受信したGAP信号がコールバックされているはず。
            //      上記2種類のパケットがまとめてハンドラ引数のBluetoothLEAdvertisementにGAPの情報が格納されている。
            //      ScanResponseDataが未受信だと、対応するデータ部分が空になっていると思われる。
            //      ScanResponseDataにデバイス名が含まれているが、しばらく空文字列になってて、ちょっと待つと取得できたりする。すぐ取得できることもある。
            //   -> BluetoothLEManufacturerDataでAdvertisingのフィルタリングができるっぽい
            //  * BroadcastingだけするPeripheralならGAPだけ使えばいい
            // 2. BluetoothLEDevice取得
            //   BluetoothLEDevice dev = await BluetoothLEDevice.FromBluetoothAddressAsync(args.BluetoothAddress);
            //   BluetoothLEDeviceオブジェクト作成だけでは接続することもしないこともある？
            //   接続条件：GattSession.MaintainConnection を true に設定するか、BluetoothLEDeviceでキャッシュされていないサービスを呼び出すか、デバイスに対して読み取り/書き込み操作を実行。
            advertisementWatcher.Received += AdvertisementWatcher_Received;
            advertisementWatcher.Stopped += AdvertisementWatcher_Stopped;
            advertisementWatcher.ScanningMode = BluetoothLEScanningMode.Active;

            // (b) Windows側でBond/Pairing済みのデバイスを使用するケース
            //   https://learn.microsoft.com/ja-jp/windows/uwp/devices-sensors/gatt-client
            // 1. DeviceWatcherでWindowsが認識しているデバイスを取得する？
            //   DeviceWatcher deviceWatcher = DeviceInformation.CreateWatcher による監視をする？
            // 2. DeviceIDからBLEDeviceを取得する
            //   BluetoothLEDevice bluetoothLeDevice = await BluetoothLEDevice.FromIdAsync(deviceInfo.Id);

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

        static void AdvertisementWatcher_Stopped(BluetoothLEAdvertisementWatcher sender, BluetoothLEAdvertisementWatcherStoppedEventArgs args)
        {
            IsScanStopped = true;
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
                // すでにペアリング済みデバイスはnullになるっぽい？
            }


        }

        public static void Pairing()
        {
            //DevicePairingResult result = await deviceInfo.Pairing.PairAsync();
            //if (result.Status == DevicePairingResultStatus.Paired || result.Status == DevicePairingResultStatus.AlreadyPaired)
            //{
            //    // success
            //}
            //else
            //{
            //    // fail
            //}
        }
        //private async void Pair(DeviceInformation deviceInfo)
        //{
        //    deviceInfo.Pairing.Custom.PairingRequested += PairingRequestedHandler;
        //    DevicePairingResult result =
        //            await deviceInfo.Pairing.Custom.PairAsync(DevicePairingKinds.ConfirmOnly,
        //                                                      DevicePairingProtectionLevel.Encryption);
        //    deviceInfo.Pairing.Custom.PairingRequested -= PairingRequestedHandler;
        //}
        //private static void PairingRequestedHandler(DeviceInformationCustomPairing sender,
        //                                    DevicePairingRequestedEventArgs eventArgs)
        //{
        //    switch (eventArgs.PairingKind)
        //    {
        //        case DevicePairingKinds.ConfirmOnly:
        //            eventArgs.Accept();
        //            break;
        //        default:
        //            break;
        //    }
        //}


        public static async Task<GattDeviceService> GetGattService(ulong deviceId, Guid guid)
        {
            if (DevicesMap.TryGetValue(deviceId, out var device))
            {
                return await GetGattService(device, guid);
            }

            return null;
        }
        public static async Task<GattDeviceService> GetGattService(Device device, Guid guid)
        {
            // DeviceからServiceを取得する
            // Disconnectedのとき、ここでキャッシュされてないServiceを取得する可能性あり
            // BLEテストツールでServiceをすべて取得するなら
            // device.device.GetGattServicesAsync();
            // 特定のServiceを実装するツールならGuidを指定する
            var servresult = await device.device.GetGattServicesForUuidAsync(guid);
            if (servresult.Status == GattCommunicationStatus.Success)
            {
                return servresult.Services[0];
            }

            // 
            return null;
        }

        public static async Task<GattCharacteristic> GetCharacteristic(GattDeviceService service, Guid guid, GattCharacteristicProperties prop)
        {
            var result = await service.GetCharacteristicsForUuidAsync(guid);
            if (result.Status == GattCommunicationStatus.Success)
            {
                var characteristics = result.Characteristics[0];
                //GattCharacteristicProperties.Write
                if (characteristics.CharacteristicProperties.HasFlag(prop))
                {
                    return characteristics;
                }
            }
            return null;

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
