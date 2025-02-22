using bleus.BLE;
using Reactive.Bindings;
using Reactive.Bindings.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
using System.Text;
using System.Threading.Tasks;
using Windows.Devices.Bluetooth;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;

namespace bleus.BleViewModel
{
    internal class Device : Utility.BindableBase, IDisposable
    {
        //
        BLE.Device device;
        // Device情報前回取得時間
        DateTimeOffset timestamp;
        //
        public ReactivePropertySlim<bool> IsActive { get; set; }
        public ReactivePropertySlim<BLE.PairingStatus> PairingStatus { get; set; }
        public ReactivePropertySlim<string> PairingStatusDisp { get; set; }
        public ReactiveCommand OnConnect { get; set; }

        // Charactaristic
        public ReactivePropertySlim<bool> HasSerialTx { get; set; }
        public ReactivePropertySlim<bool> HasSerialRx { get; set; }

        // BLE情報
        public ReactivePropertySlim<string> DeviceId { get; set; }
        public ReactivePropertySlim<string> BluetoothDeviceId { get; set; }
        public ReactivePropertySlim<string> LocalName { get; set; }
        public ReactivePropertySlim<short> RawSignalStrengthInDBm { get; set; }


        public Device(BLE.Device dev)
        {
            //
            device = dev;
            timestamp = device.Timestamp;
            //
            IsActive = new ReactivePropertySlim<bool>(true);
            IsActive.AddTo(Disposables);
            PairingStatusDisp = new ReactivePropertySlim<string>("<Disconnect>");
            PairingStatusDisp.Subscribe(x =>
            {

            })
            .AddTo(Disposables);
            PairingStatus = new ReactivePropertySlim<BLE.PairingStatus>(BLE.PairingStatus.Disconnected);
            PairingStatus.Subscribe(x =>
            {
                switch (x)
                {
                    case BLE.PairingStatus.Connected:
                        PairingStatusDisp.Value = "<Connected>";
                        break;
                    case BLE.PairingStatus.Bonded:
                        PairingStatusDisp.Value = "<Bonded>";
                        break;
                    case BLE.PairingStatus.Disconnected:
                    default:
                        PairingStatusDisp.Value = "<Disconnect>";
                        break;
                }
            })
            .AddTo(Disposables);
            //
            DeviceId = new ReactivePropertySlim<string>("<None>");
            DeviceId.AddTo(Disposables);
            BluetoothDeviceId = new ReactivePropertySlim<string>("<None>");
            BluetoothDeviceId.AddTo(Disposables);
            LocalName = new ReactivePropertySlim<string>("<None>");
            LocalName.AddTo(Disposables);
            RawSignalStrengthInDBm = new ReactivePropertySlim<short>(0);
            RawSignalStrengthInDBm.AddTo(Disposables);

            //
            HasSerialTx = new ReactivePropertySlim<bool>(true);
            HasSerialTx.AddTo(Disposables);
            HasSerialRx = new ReactivePropertySlim<bool>(false);
            HasSerialRx.AddTo(Disposables);
            OnConnect = new ReactiveCommand();
            OnConnect.Subscribe(x =>
            {
                if (PairingStatus.Value == BLE.PairingStatus.Disconnected)
                {
                    PairingStatus.Value = BLE.PairingStatus.Connected;
                    HasSerialTx.Value = false;
                    HasSerialRx.Value = true;
                }
                else
                {
                    PairingStatus.Value = BLE.PairingStatus.Disconnected;
                    HasSerialTx.Value = true;
                    HasSerialRx.Value = false;
                }
            })
            .AddTo(Disposables);
        }


        public bool Update(bool force = false)
        {
            bool updateList = false;

            if (device.CheckTimeout())
            {
                // Device情報の無効判定時間が経過していたら
                if (IsActive.Value)
                {
                    updateList = true;
                }
                IsActive.Value = false;
            }
            else
            {
                // 一応有効化する
                if (!IsActive.Value)
                {
                    updateList = true;
                }
                IsActive.Value = true;
                // BLE.Deviceが更新されていたら反映する
                if (force || timestamp < device.Timestamp)
                {
                    //
                    if (RawSignalStrengthInDBm.Value != device.RawSignalStrengthInDBm)
                    {
                        updateList = true;
                    }
                    //
                    timestamp = device.Timestamp;
                    DeviceId.Value = device.DeviceId;
                    BluetoothDeviceId.Value = device.BluetoothDeviceId;
                    LocalName.Value = device.LocalName;
                    RawSignalStrengthInDBm.Value = device.RawSignalStrengthInDBm;
                }
            }

            return updateList;
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
