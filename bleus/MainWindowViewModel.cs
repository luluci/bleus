using bleus.BLE;
using Reactive.Bindings;
using Reactive.Bindings.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace bleus
{
    internal class MainWindowViewModel : Utility.BindableBase, IDisposable
    {
        // GUI
        public ReactiveCommand OnScan {  get; set; }
        public ReactivePropertySlim<bool> IsScanning { get; set; }
        // BLE
        public ReactiveCollection<BleViewModel.Device> Devices { get; }

        //
        System.Windows.Threading.DispatcherTimer cycleTimer;


        public MainWindowViewModel()
        {
            //
            Devices = new ReactiveCollection<BleViewModel.Device>();
            //Devices.Add(new BLE.Device(1));

            //
            OnScan = new ReactiveCommand();
            OnScan.Subscribe(x =>
            {
                if (!IsScanning.Value)
                {
                    BLE.Central.StartScan();
                    IsScanning.Value = true;
                }
                else
                {
                    BLE.Central.StopScan();
                    IsScanning.Value = false;
                }
            })
            .AddTo(Disposables);
            IsScanning = new ReactivePropertySlim<bool>(false);
            IsScanning.AddTo(Disposables);


            cycleTimer = new System.Windows.Threading.DispatcherTimer();
            cycleTimer.Interval = TimeSpan.FromMilliseconds(100);
            cycleTimer.Tick += CycleTimerHandler;
            cycleTimer.Start();
        }

        public void OnLoad()
        {
            BLE.Central.Setup();
        }

        private void CycleTimerHandler(object sender, EventArgs e)
        {
            // Deviceスキャン中の処理
            if (IsScanning.Value)
            {
                bool lockTaken = false;
                try
                {
                    // GUIスレッドではロックでブロックせず次回更新タイミングに任せる
                    Monitor.TryEnter(BLE.Central.lockDevices, 0, ref lockTaken);
                    if (lockTaken)
                    {
                        // 新規Deviceチェック
                        if (BLE.Central.Devices.Count > Devices.Count)
                        {
                            for (int i = Devices.Count; i < BLE.Central.Devices.Count; i++)
                            {
                                var dev = new BleViewModel.Device(BLE.Central.Devices[i]);
                                dev.Update(true);
                                Devices.Add(dev);
                            }
                        }
                        // 既存Device情報チェック
                        foreach (var device in Devices)
                        {
                            device.Update();
                        }
                    }
                }
                finally
                {
                    if (lockTaken)
                    {
                        Monitor.Exit(BLE.Central.lockDevices);
                    }
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
