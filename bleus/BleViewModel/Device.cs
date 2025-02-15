using Reactive.Bindings;
using Reactive.Bindings.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
using System.Text;
using System.Threading.Tasks;
using Windows.Devices.Bluetooth;

namespace bleus.BleViewModel
{
    internal class Device : Utility.BindableBase, IDisposable
    {
        //
        BLE.Device device;
        // Device情報前回取得時間
        DateTime timestamp;
        //
        public ReactivePropertySlim<bool> IsActive { get; set; }
        public ReactivePropertySlim<string> DeviceId { get; set; }
        public ReactivePropertySlim<string> BluetoothDeviceId { get; set; }

        public Device(BLE.Device dev)
        {
            //
            device = dev;
            timestamp = device.Timestamp;
            //
            IsActive = new ReactivePropertySlim<bool>(true);
            IsActive.AddTo(Disposables);
            DeviceId = new ReactivePropertySlim<string>("<None>");
            DeviceId.AddTo(Disposables);
            BluetoothDeviceId = new ReactivePropertySlim<string>("<None>");
            BluetoothDeviceId.AddTo(Disposables);
        }


        public void Update(bool force = false)
        {
            if (device.CheckTimeout())
            {
                // Device情報の無効判定時間が経過していたら
                IsActive.Value = false;
            }
            else
            {
                // 一応有効化する
                IsActive.Value = true;
                // BLE.Deviceが更新されていたら反映する
                if (force || timestamp != device.Timestamp)
                {
                    timestamp = device.Timestamp;
                    DeviceId.Value = device.DeviceId;
                    BluetoothDeviceId.Value = device.BluetoothDeviceId;
                }
            }

        }


        #region IDisposable Support
        private CompositeDisposable Disposables { get; } = new CompositeDisposable();
        private bool disposedValue = false; // 重複する呼び出しを検出するには

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // TODO: マネージド状態を破棄します (マネージド オブジェクト)。
                    this.Disposables.Dispose();
                }

                // TODO: アンマネージド リソース (アンマネージド オブジェクト) を解放し、下のファイナライザーをオーバーライドします。
                // TODO: 大きなフィールドを null に設定します。

                disposedValue = true;
            }
        }

        // TODO: 上の Dispose(bool disposing) にアンマネージド リソースを解放するコードが含まれる場合にのみ、ファイナライザーをオーバーライドします。
        //~MainWindowViewModel()
        //{
        //    // このコードを変更しないでください。クリーンアップ コードを上の Dispose(bool disposing) に記述します。
        //    Dispose(false);
        //}

        // このコードは、破棄可能なパターンを正しく実装できるように追加されました。
        void IDisposable.Dispose()
        {
            // このコードを変更しないでください。クリーンアップ コードを上の Dispose(bool disposing) に記述します。
            Dispose(true);
            // TODO: 上のファイナライザーがオーバーライドされる場合は、次の行のコメントを解除してください。
            // GC.SuppressFinalize(this);
        }

        #endregion
    }
}
