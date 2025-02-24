using bleus.BLE;
using bleus.BleViewModel;
using Reactive.Bindings;
using Reactive.Bindings.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Data;

namespace bleus
{
    internal class MainWindowViewModel : Utility.BindableBase, IDisposable
    {
        // GUI
        public ReactiveCommand OnScan {  get; set; }
        public ReactivePropertySlim<bool> IsScanning { get; set; }
        // BLE
        public ReactiveCollection<BleViewModel.Device> Devices { get; }
        ListCollectionView deviceCV;
        public ReactivePropertySlim<BleViewModel.Device> DevicesSelectItem { get; set; }

        // ScanFilter情報
        public ReactivePropertySlim<int> FilterRssi { get; set; }

        //
        System.Windows.Threading.DispatcherTimer cycleTimer;
        int waitScanning;


        public MainWindowViewModel()
        {
            //
            Devices = new ReactiveCollection<BleViewModel.Device>();
            Devices.AddTo(Disposables);
            DevicesSelectItem = new ReactivePropertySlim<BleViewModel.Device>(null);
            DevicesSelectItem.Subscribe(x =>
            {
                if (!(x is null))
                {

                }
            })
            .AddTo(Disposables);
            //
            FilterRssi = new ReactivePropertySlim<int>(-50);
            //Devices.Add(new BLE.Device(1));
            var cv = CollectionViewSource.GetDefaultView(Devices);
            cv.Filter = x =>
            {
                if (x is BleViewModel.Device dev)
                {
                    bool check = true;
                    if (!dev.IsActive.Value)
                    {
                        check = false;
                    }
                    if (dev.RawSignalStrengthInDBm.Value < FilterRssi.Value)
                    {
                        check = false;
                    }
                    return check;
                }
                return false;
            };
            if (cv is ListCollectionView lcv)
            {
                lcv.IsLiveFiltering = true;
                deviceCV = lcv;
            }
            FilterRssi.Subscribe(x => {
                deviceCV.Refresh();
            })
            .AddTo(Disposables);

            //
            OnScan = new ReactiveCommand();
            OnScan.Subscribe(x =>
            {
                if (!IsScanning.Value)
                {
                    // Connect中のデバイスも消してしまうので一旦無効化
                    // そもそも1:Nで制御するものでもないので、ペアリングと同時にスキャンは止めるべき？
                    // 取得済みDevicesをクリアしてからスキャン開始
                    //BLE.Central.ResetDevices();
                    //Devices.Clear();
                    //
                    waitScanning = 0;
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
            // polling周期作成
            // 処理内容ごとに周期を変える想定
            waitScanning++;
            if (waitScanning >= 5)
            {
                waitScanning = 0;

                // Deviceスキャン中の処理
                // PeripheralはペアリングするとAdvertisingを止める点に注意
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

                // 既存Device情報更新チェック
                // Scan中はdevice情報が更新される
                // Connect中は通信データが更新される
                // List表示情報に変更があるかチェックする
                bool updateList = false;
                foreach (var device in Devices)
                {
                    if (device.Update())
                    {
                        updateList = true;
                    }
                }
                if (updateList)
                {
                    deviceCV.Refresh();
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
