using bleus.BLE;
using Reactive.Bindings;
using Reactive.Bindings.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;

namespace bleus.BleViewModel
{
    internal class Device : Utility.BindableBase, IDisposable
    {
        //
        BLE.Device device;
        // Device情報前回取得時間
        DateTimeOffset timestamp;
        // BLE
        GattDeviceService service;
        bool isDisconnected = false;

        public ReactivePropertySlim<bool> IsActive { get; set; }
        public ReactivePropertySlim<BLE.PairingStatus> PairingStatus { get; set; }
        public ReactivePropertySlim<string> PairingStatusDisp { get; set; }
        public AsyncReactiveCommand OnConnect { get; set; }
        public ReactivePropertySlim<string> OnConnectDisp { get; set; }

        // BLE Device情報
        public ReactivePropertySlim<string> DeviceId { get; set; }
        public ReactivePropertySlim<string> BluetoothDeviceId { get; set; }
        public ReactivePropertySlim<string> LocalName { get; set; }
        public ReactivePropertySlim<short> RawSignalStrengthInDBm { get; set; }

        // BLE Service情報
        public ReactivePropertySlim<bool> HasM5PaperS3Service { get; set; }
        public M5PaperS3Service M5PaperS3Service { get; set; }
        public ReactivePropertySlim<bool> HasSerialService { get; set; }
        public BleViewModel.SerialService SerialService { get; set; }

        //
        public ReactivePropertySlim<string> ErrMsg { get; set; }

        public Device(BLE.Device dev)
        {
            //
            ErrMsg = new ReactivePropertySlim<string>();
            //
            device = dev;
            timestamp = device.Timestamp;
            device.device.ConnectionStatusChanged += ConnectionStatusChanged;
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
            HasM5PaperS3Service = new ReactivePropertySlim<bool>(false);
            HasM5PaperS3Service.AddTo(Disposables);
            M5PaperS3Service = null;
            HasSerialService = new ReactivePropertySlim<bool>(false);
            HasSerialService.AddTo(Disposables);
            SerialService = null;
            //
            OnConnectDisp = new ReactivePropertySlim<string>("Connect");
            OnConnectDisp.AddTo(Disposables);
            OnConnect = new AsyncReactiveCommand();
            OnConnect.Subscribe(async x =>
            {
                try
                {
                    if (PairingStatus.Value == BLE.PairingStatus.Disconnected)
                    {
                        PairingStatus.Value = BLE.PairingStatus.Connected;
                        OnConnectDisp.Value = "Disconnect";
                        await StartConnect();
                    }
                    else
                    {
                        PairingStatus.Value = BLE.PairingStatus.Disconnected;
                        OnConnectDisp.Value = "Connect";
                        StartDisconnect();
                    }
                }
                catch (Exception ex)
                {
                    StartDisconnect();
                    ErrMsg.Value = ex.Message;
                    PairingStatus.Value = BLE.PairingStatus.Disconnected;
                    OnConnectDisp.Value = "Connect";
                }
            })
            .AddTo(Disposables);
        }


        public bool Update(bool force = false)
        {
            bool updateList = false;

            // 接続状況チェック
            // 切断後に勝手に再接続して再切断はありえないので
            // フラグ参照に排他制御は不要
            if (isDisconnected)
            {
                isDisconnected = false;
                // Deviceを削除する必要がある？

                //
                StartDisconnect();

                return true;
            }

            // 未接続かつ一定時間Advertising受信無しは非アクティブにする
            if (PairingStatus.Value == BLE.PairingStatus.Disconnected && device.CheckTimeout())
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
                // Serviceチェック
                if (!(SerialService is null))
                {
                    SerialService.Update();
                }
            }
            return updateList;
        }

        private void ConnectionStatusChanged(BluetoothLEDevice sender, object e)
        {
            if (sender.ConnectionStatus == BluetoothConnectionStatus.Disconnected)
            {
                isDisconnected = true;
            }
        }

        private async Task StartConnect()
        {
            // Serviceを取得して接続開始
            // SerialService設定
            service = await BLE.Central.GetGattService(device, BleViewModel.SerialService.ServiceGuid);
            if (!(service is null))
            {
                // Service作成
                this.SerialService = new SerialService();
                // Service初期化
                if (await this.SerialService.Setup(service))
                {
                    HasSerialService.Value = true;
                }
                else
                {
                    StartDisconnect();
                }
            }
        }
        private void StartDisconnect()
        {
            if (!(service is null))
            {
                HasSerialService.Value = false;
                //
                if (this.SerialService is IDisposable obj)
                {
                    obj.Dispose();
                }
                this.SerialService = null;
                service.Dispose();
                service = null;
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
                device.device.ConnectionStatusChanged -= ConnectionStatusChanged;
                StartDisconnect();

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
