using Reactive.Bindings;
using Reactive.Bindings.Extensions;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Reactive.Disposables;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using System.Reactive.Linq;

namespace bleus.BleViewModel
{
    // 通信プロトコル
    enum DataTransCommand : byte
    {
        DataDeclare, // 転送データ宣言
        DataContent, // 転送データ内容
    }
    enum DataTransDataType : byte
    {
        data_setting, // 設定データ
        data_text,    // テキストデータ
        file_PNG,     // ファイル:PNG
    }

    [TypeConverter(typeof(Utility.EnumDisplayTypeConverter))]
    internal enum DataTransTextType
    {
        [Display(Name = "サイネージ上段1")]
        DynamicSignageU1,
        [Display(Name = "サイネージ下段1")]
        DynamicSignageD1,
        [Display(Name = "サイネージ下段2")]
        DynamicSignageD2,
    }

    // data_trans_packet_declare, data_trans_packet_contentの管理
    internal class DataTransPacketSend
    {
        public static readonly byte HeaderSize = 8;
        public static readonly byte DeclareBodySize = 8;
        public static readonly byte ContentBodySize = 120;
        //
        public static readonly int DeclarePacketSize = HeaderSize + DeclareBodySize;
        public static readonly int ContentPacketSize = HeaderSize + ContentBodySize;
        public byte[] DeclareData { get; private set; } = new byte[DeclarePacketSize];
        public byte[] ContentData { get; private set; } = new byte[ContentPacketSize];

        // 送信データストリーム
        System.IO.Stream stream;

        System.IO.FileStream fs;
        UInt32 datasize;
        public UInt32 NextPage;    // 次回送信ページ番号, 0開始としている
        UInt32 restSize;    // 残り送信サイズ
        public UInt32 MaxPage;     // 最大ページ数=転送データ全体分割数
        UInt32 sendSize;      //送信ボディサイズ

        public DataTransPacketSend()
        {
        }

        public bool StartSendText(string text, int subType)
        {
            try
            {
                // textをUTF-8のバイト列に変換
                var textBytes = System.Text.Encoding.UTF8.GetBytes(text);
                stream = new System.IO.MemoryStream(textBytes);
                //
                datasize = (UInt32)textBytes.Length;
                restSize = datasize;
                NextPage = 0;
                MaxPage = (UInt32)Math.Ceiling((double)datasize / ContentBodySize);
                sendSize = ContentBodySize;

                //
                InitDeclareData(DataTransDataType.data_text, subType);
                InitContentData(DataTransDataType.data_text, subType);

                return true;
            }
            catch (Exception e)
            {
                return false;
            }
        }

        public bool StartSendFile(string filepath, DataTransDataType dataType, int subType)
        {
            try
            {
                // ファイルをバイナリで開く
                fs = new System.IO.FileStream(filepath, System.IO.FileMode.Open, System.IO.FileAccess.Read);
                stream = fs;
                // ファイル情報取得
                datasize = (UInt32)fs.Length;
                restSize = datasize;
                NextPage = 0;
                MaxPage = (UInt32)Math.Ceiling((double)datasize / ContentBodySize);
                sendSize = ContentBodySize;

                //
                InitDeclareData(DataTransDataType.file_PNG, 0);
                InitContentData(DataTransDataType.file_PNG, 0);

                return true;
            }
            catch (Exception e)
            {
                //Debug.WriteLine(e.Message);
                return false;
            }
        }
        public void CloseFile()
        {
            fs.Close();
        }

        void InitDeclareData(DataTransDataType dataType, int subType)
        {
            // Header
            // コマンド
            DeclareData[0] = (byte)DataTransCommand.DataDeclare;
            // ボディサイズ
            DeclareData[1] = DeclareBodySize;
            // 転送データ内容
            DeclareData[2] = (byte)dataType;
            // 送信ページ番号:0開始
            DeclareData[3] = 0;
            // 転送データ補助情報
            DeclareData[4] = (byte)subType;
            // reserve
            DeclareData[5] = 0;
            DeclareData[6] = 0;
            DeclareData[7] = 0;

            // Body
            BitConverter.GetBytes(datasize).CopyTo(DeclareData, 8);
            BitConverter.GetBytes(MaxPage).CopyTo(DeclareData, 12);
        }
        void InitContentData(DataTransDataType dataType, int subType)
        {
            // Header
            // コマンド
            ContentData[0] = (byte)DataTransCommand.DataContent;
            // ボディサイズ
            ContentData[1] = ContentBodySize;
            // 転送データ内容
            ContentData[2] = (byte)dataType;
            // 送信ページ番号:0開始
            ContentData[3] = 0;
            // 転送データ補助情報
            DeclareData[4] = (byte)subType;
            // reserve
            ContentData[4] = 0;
            ContentData[5] = 0;
            ContentData[6] = 0;
            ContentData[7] = 0;
            // Body
            // ファイルデータは随時格納
            //fs.Read(ContentData, 8, ContentBodySize);
        }

        public bool UpdateContentData(UInt32 currPage)
        {
            // currPageがresponseで受信した「Peripheral側の受信済みページ数」
            // Declareの応答が0開始になる。Contentはページ1から開始する。

            // 再送対応、する？
            // すでに送信データ作成済みか判定
            if (currPage+1 == NextPage)
            {
                return true;
            }
            // 正常シーケンスではcurrPage==nextPageになる
            // 不一致のときはシーケンス異常
            if (currPage != NextPage)
            {
                return false;
            }
            // 最大ページ数を超えて通信完了しないのは異常
            if (currPage >= MaxPage)
            {
                return false;
            }

            // ContentData更新
            NextPage = currPage + 1;
            // ボディサイズ
            if (restSize < ContentBodySize)
            {
                sendSize = restSize;
            }
            else
            {
                sendSize = ContentBodySize;
            }
            ContentData[1] = (byte)sendSize;
            // 送信ページ番号:0開始
            ContentData[3] = (byte)NextPage;
            // Body
            var readSize = stream.Read(ContentData, 8, (int)sendSize);
            restSize -= (UInt32)readSize;

            return true;
        }
    }


    // data_trans_resp_type : response内容定義
    enum DataTransRespType : byte
    {
        // 正常系
        RecvStatus,   // 受信ステータス
        RecvComplete, // 受信完了

        // 異常系
        RejectDeclare, // 転送データ宣言拒否
        RejectContent, // 転送データ内容拒否
    }

    // data_trans_packet_responseを実装する
    // Peripheralから通知されるresponseを解析する
    internal class DataTransPacketResponse
    {
        public static readonly int PacketSize = 8;
        public byte[] Data { get; private set; } = new byte[PacketSize];

        public DataTransPacketResponse()
        {

        }

        public DataTransRespType RespType
        {
            get { return (DataTransRespType)Data[0]; }
        }
        public byte RejectReason
        {
            get { return Data[4]; }
        }
        // Peripheral側の受信済みページ数
        // Declare受信後に0になり、最初のデータを受信したら1になる
        public UInt32 Page
        {
            get { return BitConverter.ToUInt32(Data, 4); }
        }
    }

    internal class DataTrans : Utility.BindableBase, IDisposable
    {

        // BLE対応Service
        public static readonly Guid ServiceGuid = new Guid("37ea2bc7-86d9-4c8d-9e38-3d5ff19995f4");
        // Peripheral側がRx
        public static readonly Guid CharacteristicGuidSerialRx = new Guid("537b7e5c-599d-4465-a708-a314ee9b3b37");
        // Peripheral側がTx(Notify)
        public static readonly Guid CharacteristicGuidSerialTx = new Guid("6fcfe024-2586-4e1f-931a-5e9e3a8a12c7");

        static readonly Encoding encoding = Encoding.GetEncoding("UTF-8");

        GattDeviceService service;
        GattCharacteristic characteristicRx;
        GattCharacteristic characteristicTx;

        // Peripheralへの送信データ
        DataTransPacketSend packetSend = new DataTransPacketSend();
        // PeripheralからのNotifyデータ
        DataTransPacketResponse packetResp = new DataTransPacketResponse();
        bool hasResponse = false;
        object lockResponse = new object();

        // 
        public static readonly int SendDelay = 10;  // msec
        // ステータス表示
        public static readonly int StatusDelayMax = (500) / SendDelay; // 500ms
        // タイムアウト判定
        // データ転送シーケンス全体タイムアウト
        public ReactivePropertySlim<int> TimeoutSeqMax { get; set; }
        public static readonly int TimeoutSeqMaxValue = (30 * 1000) / SendDelay; // 30sec
        int timeoutSeqCount = 0;
        // 受信タイムアウト
        public static readonly int TimeoutRecvMax = 500 / SendDelay; // 500ms
        int timeoutRecvCount = 0;

        // VM
        public ReactivePropertySlim<string> SendStatus { get; set; }
        public ReactivePropertySlim<bool> IsSending { get; set; }
        public ReadOnlyReactivePropertySlim<bool> IsSendOk { get; set; }
        // Text
        public ReactivePropertySlim<string> SendText { get; set; }
        public ReactivePropertySlim<DataTransTextType> SendTextType { get; set; }
        public ReactivePropertySlim<bool> IsOkSendText { get; set; }
        public AsyncReactiveCommand OnSendText { get; set; }
        // File
        public ReactivePropertySlim<string> SendFile { get; set; }
        public ReactivePropertySlim<bool> IsOkSendFile { get; set; }
        public ReactiveCommand<System.Windows.DragEventArgs> PreviewDragOver { get; set; }
        public ReactiveCommand<System.Windows.DragEventArgs> PreviewDragLeave { get; set; }
        public ReactiveCommand<System.Windows.DragEventArgs> PreviewDropFile { get; set; }
        public AsyncReactiveCommand OnSendFile { get; set; }


        //
        public CancellationTokenSource TransDataCancellationTokenSource = null;

        public DataTrans()
        {
            // Timeout
            TimeoutSeqMax = new ReactivePropertySlim<int>(TimeoutSeqMaxValue);
            // VM
            SendStatus = new ReactivePropertySlim<string>(string.Empty);
            SendStatus.AddTo(Disposables);
            // Text
            SendTextType = new ReactivePropertySlim<DataTransTextType>(DataTransTextType.DynamicSignageU1);
            SendTextType.AddTo(Disposables);
            IsOkSendText = new ReactivePropertySlim<bool>(false);
            IsOkSendText.AddTo(Disposables);
            SendText = new ReactivePropertySlim<string>(string.Empty);
            SendText
                .Subscribe(x =>
                {
                    if (x.Length > 0)
                    {
                        IsOkSendText.Value = true;
                    }
                    else
                    {
                        IsOkSendText.Value = false;
                    }
                })
                .AddTo(Disposables);
            OnSendText = new AsyncReactiveCommand();
            OnSendText
                 .Subscribe(DoSendText)
                 .AddTo(Disposables);
            // File
            SendFile = new ReactivePropertySlim<string>(string.Empty);
            SendFile.AddTo(Disposables);
            IsOkSendFile = new ReactivePropertySlim<bool>(false);
            IsOkSendFile.AddTo(Disposables);
            PreviewDragOver = new ReactiveCommand<System.Windows.DragEventArgs>();
            PreviewDragOver
                .Subscribe(DoPreviewDragOver)
                .AddTo(Disposables);
            PreviewDragLeave = new ReactiveCommand<System.Windows.DragEventArgs>();
            PreviewDragLeave
                .Subscribe(DoPreviewDragLeave)
                .AddTo(Disposables);
            PreviewDropFile = new ReactiveCommand<System.Windows.DragEventArgs>();
            PreviewDropFile
                .Subscribe(DoDropFile)
                .AddTo(Disposables);
            OnSendFile = new AsyncReactiveCommand();
            OnSendFile
                .Subscribe(DoSendFile)
                .AddTo(Disposables);
            //
            IsSending = new ReactivePropertySlim<bool>(false);
            IsSending.AddTo(Disposables);
            IsSendOk = Observable.Merge(
                    IsOkSendText,
                    IsOkSendFile,
                    IsSending.Select(x => !x)
                )
                .ToReadOnlyReactivePropertySlim();
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
            // Notifyによる受信時の処理
            if (eventArgs.CharacteristicValue.Length == DataTransPacketResponse.PacketSize)
            {
                lock (lockResponse)
                {
                    Windows.Storage.Streams.DataReader.FromBuffer(eventArgs.CharacteristicValue).ReadBytes(packetResp.Data);
                    hasResponse = true;
                }
            }
            //// インターフェースにセット
            //lock (lockRx)
            //{
            //    RxData = data;
            //    HasRxData = true;
            //}
        }


        public void StopSendFile()
        {
            if (TransDataCancellationTokenSource != null)
            {
                TransDataCancellationTokenSource.Cancel();
            }
        }

        private async Task DoSendFile()
        {
            try
            {
                IsSending.Value = true;

                if (IsOkSendFile.Value)
                {
                    TransDataCancellationTokenSource = new CancellationTokenSource();

                    // ファイルをバイナリで開いて送信準備
                    if (!packetSend.StartSendFile(SendFile.Value, DataTransDataType.file_PNG, 0))
                    {
                        SendStatus.Value = "Error: Failed to file open";
                        return;
                    }

                    //await Task.Run(DoSendFileImpl, SendFileCancellationTokenSource.Token);
                    await DoTransData(DataTransDataType.file_PNG, 0);
                }
            }
            catch (Exception e)
            {
                //Debug.WriteLine(e.Message);
            }
            finally
            {
                packetSend.CloseFile();
                IsSending.Value = false;
            }
        }

        private async Task DoSendText()
        {
            try
            {
                IsSending.Value = true;

                if (IsOkSendText.Value)
                {
                    TransDataCancellationTokenSource = new CancellationTokenSource();

                    // ファイルをバイナリで開いて送信準備
                    if (!packetSend.StartSendText(SendText.Value, (int)SendTextType.Value))
                    {
                        SendStatus.Value = "Error: Failed to text";
                        return;
                    }

                    await DoTransData(DataTransDataType.data_text, (int)SendTextType.Value);
                }
            }
            catch (Exception e)
            {
                //Debug.WriteLine(e.Message);
            }
            finally
            {
                IsSending.Value = false;
            }
        }

        enum SendState
        {
            SendDeclare,
            SendContent,
        }
        SendState sendState;
        enum RecvAnalyzeResult
        {
            WaitRecv,       //Notify待機中
            NextSeq,        //次データ送信
            SendComplete,   //送信完了
            Cancel,         //送信キャンセル
        }

        private async Task DoTransData(DataTransDataType dataType, int subType)
        {
            // 送受信状態
            sendState = SendState.SendDeclare;
            bool stateTxRx = true;
            bool isComplete = false;

            try
            {
                // ステータス表示ウェイト
                // データ送信中に毎回表示更新するとオーバーヘッドが大きそうなので(未確認)
                int statusDelayCount = 0;
                // シーケンスタイムアウト判定
                timeoutSeqCount = 0;

                while (!isComplete)
                {
                    // キャンセル判定
                    if (TransDataCancellationTokenSource.Token.IsCancellationRequested)
                    {
                        SendStatus.Value = "Cancel";
                        break;
                    }
                    // シーケンスタイムアウト判定
                    timeoutSeqCount++;
                    if (timeoutSeqCount > TimeoutSeqMax.Value)
                    {
                        SendStatus.Value = "Error: TimeoutSeq";
                        break;
                    }
                    //
                    statusDelayCount++;
                    if (statusDelayCount > StatusDelayMax)
                    {
                        statusDelayCount = 0;
                    }

                    if (stateTxRx)
                    {
                        // 送信処理
                        await DoTransDataSend(statusDelayCount);
                        stateTxRx = false;
                        timeoutRecvCount = 0;
                    }
                    else
                    {
                        // 受信待機
                        var result = DoTransDataRecv();
                        switch (result)
                        {
                            case RecvAnalyzeResult.WaitRecv:
                                // タイムアウト判定
                                timeoutRecvCount++;
                                if (timeoutRecvCount > TimeoutRecvMax)
                                {
                                    SendStatus.Value = "Error: Timeout";
                                    isComplete = true;
                                }
                                break;
                            case RecvAnalyzeResult.NextSeq:
                                stateTxRx = true;
                                break;
                            case RecvAnalyzeResult.SendComplete:
                                SendStatus.Value = "Complete: Data Send";
                                isComplete = true;
                                break;
                            case RecvAnalyzeResult.Cancel:
                                SendStatus.Value = "Cancel: Data Send";
                                isComplete = true;
                                break;
                        }
                    }


                    // 送信処理
                    await Task.Delay(SendDelay);
                }
            }
            finally
            {
            }
        }
        private async Task DoTransDataSend(int statusDelayCount)
        {
            switch (sendState)
            {
                case SendState.SendDeclare:
                    // Declare送信
                    await characteristicRx.WriteValueAsync(packetSend.DeclareData.AsBuffer());
                    sendState = SendState.SendContent;
                    SendStatus.Value = "Start: Data Send";
                    break;
                case SendState.SendContent:
                    // Content送信
                    // 送信バッファは応答解析時に更新する
                    await characteristicRx.WriteValueAsync(packetSend.ContentData.AsBuffer());
                    //
                    if (statusDelayCount == 0)
                    {
                        SendStatus.Value = $"Data Sending: {packetSend.NextPage}/{packetSend.MaxPage}";
                    }
                    break;
            }
        }
        private RecvAnalyzeResult DoTransDataRecv()
        {
            RecvAnalyzeResult result = RecvAnalyzeResult.WaitRecv;

            if (hasResponse)
            {
                // 半二重通信なのでlockは不要だが一応実施
                lock (lockResponse)
                {
                    //
                    hasResponse = false;
                    // 応答内容解析
                    switch (packetResp.RespType)
                    {
                        case DataTransRespType.RecvStatus:
                            // 受信待機中応答
                            // 応答内容に応じて次の送信データを更新
                            if (packetSend.UpdateContentData(packetResp.Page))
                            {
                                // 更新OKならシーケンス移行
                                result = RecvAnalyzeResult.NextSeq;
                            }
                            else
                            {
                                // 更新NGなら異常終了
                                result = RecvAnalyzeResult.Cancel;
                            }
                            break;
                        case DataTransRespType.RecvComplete:
                            result = RecvAnalyzeResult.SendComplete;
                            break;
                        case DataTransRespType.RejectDeclare:
                            // データ宣言NG
                            SendStatus.Value = $"Recv: RejectDeclare, detail={packetResp.RejectReason}";
                            result = RecvAnalyzeResult.Cancel;
                            break;
                        case DataTransRespType.RejectContent:
                            // データ内容NG
                            SendStatus.Value = $"Recv: RejectContent, detail={packetResp.RejectReason}";
                            result = RecvAnalyzeResult.Cancel;
                            break;
                        default:
                            SendStatus.Value = $"Recv: Invalid RespType={packetResp.RespType}";
                            result = RecvAnalyzeResult.Cancel;
                            break;
                    }
                }
            }

            return result;
        }

        private void DoPreviewDragOver(System.Windows.DragEventArgs e)
        {
            // ファイルのD&Dのみ受付
            if (e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop))
            {
                e.Handled = true;
                e.Effects = System.Windows.DragDropEffects.Copy;
            }
        }
        private void DoPreviewDragLeave(System.Windows.DragEventArgs e)
        {
            e.Effects = System.Windows.DragDropEffects.None;
            e.Handled = false;
        }
        private void DoDropFile(System.Windows.DragEventArgs e)
        {
            // ファイルのD&Dのみ受付
            if (e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop))
            {
                var dropFiles = e.Data.GetData(System.Windows.DataFormats.FileDrop);
                if (dropFiles is string[] files)
                {
                    if (files.Length > 0)
                    {
                        var tgt = files[0];
                        if (System.IO.File.Exists(tgt))
                        {
                            SendFile.Value = tgt;
                            IsOkSendFile.Value = true;
                        }
                        else
                        {
                            SendFile.Value = "<ファイル以外がD&Dされました？>";
                            IsOkSendFile.Value = false;
                        }
                    }
                }

                e.Handled = true;
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
