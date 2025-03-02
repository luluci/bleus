using Reactive.Bindings;
using Reactive.Bindings.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Text;
using System.Threading.Tasks;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.Advertisement;

namespace bleus.BLE
{
    internal class Device
    {
        //
        public DateTimeOffset Timestamp { get; set; }
        //
        ulong bluetoothAddress;
        public BluetoothLEDevice device;
        public string LocalName { get; set; }
        public short RawSignalStrengthInDBm { get; set; }

        public Device(BluetoothLEAdvertisementReceivedEventArgs args)
        {
            //
            Timestamp = args.Timestamp;
            //
            bluetoothAddress = args.BluetoothAddress;
            //
            LocalName = "<NoDeviceName>";
            RawSignalStrengthInDBm = args.RawSignalStrengthInDBm;
        }


        public void Update(DateTime now, BluetoothLEAdvertisementReceivedEventArgs args, BluetoothLEDevice dev)
        {
            device = dev;
            // 
            if (Timestamp < args.Timestamp)
            {
                Timestamp = now;
                RawSignalStrengthInDBm = args.RawSignalStrengthInDBm;
                if (args.Advertisement.LocalName.Length > 0)
                {
                    LocalName = args.Advertisement.LocalName;
                }
            }
        }

        public void Disconnect()
        {
            // 切断処理
            // deviceを削除することで切断される？
            if (!(device is null))
            {
                device.Dispose();
                device = null;
            }
        }
        public async Task<bool> Connect()
        {
            bool result = false;
            // 接続処理
            // disconnectで切断した後の再接続処理
            // AdvertisingからDeviceを取得したときはインスタンス取得済み
            try
            {
                if (device is null)
                {
                    // タイムアウトは必要？
                    device = await BluetoothLEDevice.FromBluetoothAddressAsync(bluetoothAddress);
                }
                result = true;
            }
            catch (Exception e)
            {
                // Console.WriteLine(e.Message);
                // result = false;
            }

            return result;
        }

        public bool CheckTimeout()
        {
            // 10秒取得できていないデバイスはタイムアウトと判定
            var diff = DateTimeOffset.Now - Timestamp;
            return diff.Seconds > 10;
        }

        public string DeviceId { get { return device.DeviceId; } }
        public string BluetoothDeviceId { get { return device.BluetoothDeviceId.Id; } }

        public bool IsLowEnergy
        {
            get
            {
                return device.BluetoothDeviceId.IsLowEnergyDevice;
            }
        }
        public bool IsClassic
        {
            get
            {
                return device.BluetoothDeviceId.IsClassicDevice;
            }
        }
    }
}
