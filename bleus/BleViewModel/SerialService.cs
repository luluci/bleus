using Reactive.Bindings;
using Reactive.Bindings.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Markup;
using Windows.Devices.Bluetooth.GenericAttributeProfile;

namespace bleus.BleViewModel
{
    internal class SerialService : Utility.BindableBase, IDisposable
    {

        // BLE対応Service
        public static readonly Guid ServiceGuid = new Guid("6E400001-B5A3-F393-E0A9-E50E24DCCA9E");
        // Peripheral側がRx
        public static readonly Guid CharacteristicGuidSerialRx = new Guid("6E400002-B5A3-F393-E0A9-E50E24DCCA9E");
        // Peripheral側がTx(Notify)
        public static readonly Guid CharacteristicGuidSerialTx = new Guid("6E400003-B5A3-F393-E0A9-E50E24DCCA9E");

        static readonly Encoding encoding = Encoding.GetEncoding("UTF-8");

        GattDeviceService service;
        GattCharacteristic characteristicRx;
        GattCharacteristic characteristicTx;

        //
        public ReactivePropertySlim<string> SendData { get; set; }
        public AsyncReactiveCommand OnSend { get; set; }
        public ReactivePropertySlim<string> RecvData { get; set; }

        // スレッド間通信用データ
        public readonly object lockRx = new object();
        byte[] RxData;
        bool HasRxData = false;

        public SerialService()
        {
            SendData = new ReactivePropertySlim<string>("test");
            SendData.AddTo(Disposables);
            OnSend = new AsyncReactiveCommand();
            OnSend.Subscribe(async x => {
                if (!(characteristicRx is null))
                {
                    var encoding = Encoding.GetEncoding("UTF-8");
                    var data = encoding.GetBytes(SendData.Value);
                    await characteristicRx.WriteValueAsync(data.AsBuffer());
                }
            })
            .AddTo(Disposables);
            RecvData = new ReactivePropertySlim<string>("");
            RecvData.AddTo(Disposables);
        }

        public async Task<bool> Setup(GattDeviceService service_)
        {
            service = service_;

            // PeripheralがRxを待ち受けている
            characteristicRx = await BLE.Central.GetCharacteristic(service, CharacteristicGuidSerialRx, GattCharacteristicProperties.Write);
            if (characteristicRx is null)
            {
                return false;
            }
            // PeripheralがTxする
            // Peripheral側で送信タイミングをコントロールするNotify
            characteristicTx = await BLE.Central.GetCharacteristic(service, CharacteristicGuidSerialTx, GattCharacteristicProperties.Notify);
            if (characteristicTx is null)
            {
                characteristicRx = null;
                return false;
            }
            characteristicTx.ValueChanged += CharacteristicTx_ValueChanged;
            await characteristicTx.WriteClientCharacteristicConfigurationDescriptorAsync(GattClientCharacteristicConfigurationDescriptorValue.Notify);


            return true;
        }

        void CharacteristicTx_ValueChanged(GattCharacteristic sender, GattValueChangedEventArgs eventArgs)
        {
            //Notifyによる受信時の処理
            byte[] data = new byte[eventArgs.CharacteristicValue.Length];
            Windows.Storage.Streams.DataReader.FromBuffer(eventArgs.CharacteristicValue).ReadBytes(data);
            // インターフェースにセット
            lock (lockRx)
            {
                RxData = data;
                HasRxData = true;
            }
        }

        public void Update()
        {
            // 受信データ取り出し
            byte[] data = null;
            lock (lockRx)
            {
                if (HasRxData)
                {
                    data = RxData;
                    HasRxData = false;
                }
            }
            //
            if (!(data is null))
            {
                RecvData.Value = encoding.GetString(data);
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

                    if (!(service is null))
                    {
                        if (!(characteristicTx is null))
                        {
                            characteristicTx.ValueChanged -= CharacteristicTx_ValueChanged;
                        }
                        characteristicTx = null;
                        characteristicRx = null;

                        service.Dispose();
                        service = null;
                    }

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
