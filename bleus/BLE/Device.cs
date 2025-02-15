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

namespace bleus.BLE
{
    internal class Device
    {
        //
        public DateTime Timestamp { get; set; }
        //
        ulong bluetoothAddress;
        BluetoothLEDevice device;

        public Device(ulong btaddr)
        {
            //
            Timestamp = DateTime.Now;
            //
            bluetoothAddress = btaddr;
        }


        public void Update(DateTime now, BluetoothLEDevice dev)
        {
            //
            Timestamp = now;
            device = dev;
            // Device情報更新
        }

        public bool CheckTimeout()
        {
            // 10秒取得できていないデバイスはタイムアウトと判定
            var diff = DateTime.Now - Timestamp;
            return diff.Seconds > 10;
        }

        public string DeviceId { get { return device.DeviceId; } }
        public string BluetoothDeviceId { get { return device.BluetoothDeviceId.Id; } }
    }
}
