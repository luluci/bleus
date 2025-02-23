using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
using System.Text;
using System.Threading.Tasks;
using Windows.Devices.Bluetooth.GenericAttributeProfile;

namespace bleus.BleViewModel
{
    internal class SerialService : Utility.BindableBase, IDisposable
    {

        // BLE対応Service
        public readonly Guid ServiceGuid = new Guid("6E400001-B5A3-F393-E0A9-E50E24DCCA9E");
        // Peripheral側がRx
        public readonly Guid CharacteristicGuidSerialRx = new Guid("6E400002-B5A3-F393-E0A9-E50E24DCCA9E");
        // Peripheral側がTx(Notify)
        public readonly Guid CharacteristicGuidSerialTx = new Guid("6E400003-B5A3-F393-E0A9-E50E24DCCA9E");

        GattDeviceService service;
        GattCharacteristic characteristicRx;
        GattCharacteristic characteristicTx;

        public SerialService()
        {
        }

        public async Task<bool> Setup(GattDeviceService service_)
        {
            service = service_;

            // Rx
            characteristicRx = await BLE.Central.GetCharacteristic(service, CharacteristicGuidSerialRx, GattCharacteristicProperties.Write);
            if (characteristicRx is null)
            {
                return false;
            }
            // Tx
            characteristicTx = await BLE.Central.GetCharacteristic(service, CharacteristicGuidSerialTx, GattCharacteristicProperties.Notify);
            if (characteristicTx is null)
            {
                characteristicRx = null;
                return false;
            }


            return true;
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
