#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net.WebSockets;
using System.Threading;
using Cysharp.Threading.Tasks;
using OneStarMaker.Foundation.DebugSocket;
using OneStarMaker.Runtime.DebugSocketServices.Protocol;

namespace OneStarMaker.Runtime.DebugSocketServices
{
    /// <summary>
    /// 単一クライアントの送受信を担当する内部セッション。
    ///
    /// <para>
    /// WebSocket は同時 Send に弱いので、
    /// logger / telemetry / command result の送信はすべて 1 本の queue に集約する。
    /// </para>
    /// </summary>
    internal sealed class DebugSocketClientSession : IDebugSocketInboundSession
    {
        private const int MaxInboundMessageBytes = 1024 * 1024;
        private static readonly TimeSpan WebSocketCloseTimeout = TimeSpan.FromSeconds(2);

        private readonly object _queueGate = new();
        private readonly IDebugSocketClientSessionHost _host;
        private readonly WebSocket _socket;
        private readonly int _maxQueueLength;
        private readonly CancellationTokenSource _cts;
        private readonly Queue<DebugSocketOutgoingFrame> _outgoingMessages = new();
        private readonly SemaphoreSlim _queueSignal = new(0);
        private readonly UniTaskCompletionSource _completionSource = new();

        private UniTask? _sendLoopTask;
        private UniTask? _receiveLoopTask;
        private int _closeStarted;
        private bool _cleanedUp;

        public DebugSocketClientSession(
            IDebugSocketClientSessionHost host,
            WebSocket socket,
            int maxQueueLength,
            CancellationToken serviceCancellationToken)
        {
            _host = host;
            _socket = socket;
            _maxQueueLength = maxQueueLength;
            _cts = CancellationTokenSource.CreateLinkedTokenSource(serviceCancellationToken);
        }

        public string SessionId { get; } = Guid.NewGuid().ToString("N");
        public UniTask Completion => _completionSource.Task;
        public int PendingQueueLength
        {
            get
            {
                lock (_queueGate)
                {
                    return _outgoingMessages.Count;
                }
            }
        }

        /// <summary>
        /// capability hello 後に確定した、このセッション向けの negotiated capability。
        /// scene event 側から差分送信可否を判定するため、session に保持する。
        /// </summary>
        public bool HasCompletedCapabilityHello { get; set; }

        public DebugStudioCapability NegotiatedCapabilities { get; set; } = DebugStudioCapability.None;

        /// <summary>
        /// send / receive の両ループを起動する。
        /// セッション生成直後に一度だけ呼ばれる想定。
        /// </summary>
        public void Start()
        {
            // ActivateSessionAsync では current へ公開してから Start するため、
            // 極端な再接続/停止 race では「Start 前に close 済み」になり得る。
            // その場合に ObjectDisposedException を投げるより、
            // 既に閉じられたセッションとして静かに起動を諦める。
            if (Volatile.Read(ref _closeStarted) != 0 || _cleanedUp || _cts.IsCancellationRequested)
            {
                CloseAsync("start-skipped", CancellationToken.None).Forget();
                return;
            }

            try
            {
                _sendLoopTask = SendLoopAsync(_cts.Token);
                _receiveLoopTask = ReceiveLoopAsync(_cts.Token);
            }
            catch (ObjectDisposedException)
            {
            }
        }

        /// <summary>
        /// 送信キューへメッセージを積む。
        /// v1 方針どおり bounded queue + oldest drop をここで実装する。
        /// </summary>
        public void Enqueue(byte[] framedMessage)
        {
            Enqueue(DebugSocketOutgoingFrame.CreateOwned(framedMessage));
        }

        public void Enqueue(in DebugSocketOutgoingFrame framedMessage)
        {
            var shouldSignal = false;
            var droppedCount = 0;
            lock (_queueGate)
            {
                if (framedMessage.IsEmpty || _cts.IsCancellationRequested || Volatile.Read(ref _closeStarted) != 0 || _cleanedUp)
                {
                    framedMessage.Release();
                    return;
                }

                while (_outgoingMessages.Count >= _maxQueueLength)
                {
                    // 最新の観測を優先したいので、古いものから捨てる。
                    var droppedFrame = _outgoingMessages.Dequeue();
                    droppedFrame.Release();
                    droppedCount++;
                }

                _outgoingMessages.Enqueue(framedMessage);
                shouldSignal = true;
            }

            if (droppedCount > 0)
            {
                _host.RecordQueueOverflowDrops(droppedCount);
            }

            if (!shouldSignal)
            {
                return;
            }

            try
            {
                _queueSignal.Release();
            }
            catch (ObjectDisposedException)
            {
            }
        }

        /// <summary>
        /// 外側から明示停止するときの close。
        /// 停止処理では send / receive の両 loop 終了も待ち合わせる。
        /// </summary>
        public UniTask CloseAsync(string reason, CancellationToken cancellationToken)
        {
            return CloseCoreAsync(reason, cancellationToken, awaitSendLoop: true, awaitReceiveLoop: true);
        }

        /// <summary>
        /// receive loop 自身から close を要求するときの入口。
        ///
        /// <para>
        /// receive task が自分自身の終了を await すると相互待機になるため、
        /// この経路では receive loop の完了待ちを行わない。
        /// </para>
        /// </summary>
        public UniTask CloseFromReceiveLoopAsync(string reason)
        {
            return CloseCoreAsync(reason, CancellationToken.None, awaitSendLoop: true, awaitReceiveLoop: false);
        }

        /// <summary>
        /// queue から順に取り出し、WebSocket へ 1 メッセージずつ送る。
        /// ここ以外から SendAsync しないことで同時送信競合を防ぐ。
        /// </summary>
        private async UniTask SendLoopAsync(CancellationToken cancellationToken)
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    await _queueSignal.WaitAsync(cancellationToken).AsUniTask();

                    while (TryDequeue(out var framedMessage))
                    {
                        if (_socket.State != WebSocketState.Open)
                        {
                            framedMessage.Release();
                            return;
                        }

                        try
                        {
                            await _socket.SendAsync(
                                    framedMessage.AsSegment(),
                                    WebSocketMessageType.Binary,
                                    true,
                                    cancellationToken)
                                .AsUniTask();
                        }
                        finally
                        {
                            // send 完了後、または例外で中断した時点で ownership を閉じる。
                            framedMessage.Release();
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (ObjectDisposedException)
            {
            }
            catch (WebSocketException)
            {
                // 送信失敗はセッション継続不能として扱い、自分自身を閉じる。
                await CloseCoreAsync("send-loop-ended", CancellationToken.None, awaitSendLoop: false, awaitReceiveLoop: true);
            }
        }

        /// <summary>
        /// WebSocket から binary message を受け取り、protocol として service へ渡す。
        /// text frame は v1 では不採用なので protocol error とする。
        /// </summary>
        private async UniTask ReceiveLoopAsync(CancellationToken cancellationToken)
        {
            var receiveBuffer = new byte[8192];
            using var memoryStream = new MemoryStream(8192);

            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    memoryStream.SetLength(0);

                    WebSocketReceiveResult receiveResult;
                    do
                    {
                        receiveResult = await _socket.ReceiveAsync(
                                new ArraySegment<byte>(receiveBuffer),
                                cancellationToken)
                            .AsUniTask();

                        if (receiveResult.MessageType == WebSocketMessageType.Close)
                        {
                            return;
                        }

                        if (receiveResult.Count > 0)
                        {
                            memoryStream.Write(receiveBuffer, 0, receiveResult.Count);
                            if (memoryStream.Length > MaxInboundMessageBytes)
                            {
                                Enqueue(_host.CreateServiceStatus(
                                    "protocol-error",
                                    $"Inbound message exceeded {MaxInboundMessageBytes.ToString(CultureInfo.InvariantCulture)} bytes."));
                                await CloseFromReceiveLoopAsync("message-too-large");
                                return;
                            }
                        }
                    }
                    while (!receiveResult.EndOfMessage);

                    if (receiveResult.MessageType != WebSocketMessageType.Binary)
                    {
                        Enqueue(_host.CreateServiceStatus("protocol-error", "Only binary WebSocket messages are supported."));
                        continue;
                    }

                    // ToArray() は毎メッセージ新しい配列を作るため、
                    // 高頻度 log/telemetry では GC 圧が無視できなくなる。
                    // TryGetBuffer() で内部バッファを直接参照し、今回有効な長さだけを slice して渡す。
                    if (!memoryStream.TryGetBuffer(out var segment))
                    {
                        Enqueue(_host.CreateServiceStatus("protocol-error", "Failed to access the receive buffer."));
                        continue;
                    }

                    await _host.HandleInboundMessageAsync(
                        this,
                        new ReadOnlyMemory<byte>(segment.Array!, segment.Offset, (int)memoryStream.Length),
                        cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (ObjectDisposedException)
            {
            }
            catch (WebSocketException)
            {
            }
            finally
            {
                // receive loop 自身から閉じるので、自分自身の await はしない。
                await CloseCoreAsync("receive-loop-ended", CancellationToken.None, awaitSendLoop: true, awaitReceiveLoop: false);
            }
        }

        /// <summary>
        /// 共通 close 実装。
        /// 呼び出し元が send/receive loop 自身かどうかで待ち合わせ対象を切り替える。
        /// </summary>
        private async UniTask CloseCoreAsync(
            string reason,
            CancellationToken cancellationToken,
            bool awaitSendLoop,
            bool awaitReceiveLoop)
        {
            // close の主導権は必ず 1 経路だけが取る。
            // send loop / receive loop / 外部 CloseAsync が同時に来ても、
            // 後続は sibling loop を待たずに completion だけ待つ。
            // そうしないと send 側が receive を待ち、receive 側が send を待つ相互待機が起こり得る。
            if (Interlocked.Exchange(ref _closeStarted, 1) != 0)
            {
                await Completion.SuppressCancellationThrow();
                return;
            }

            _cts.Cancel();

            if (_socket.State is WebSocketState.Open or WebSocketState.CloseReceived)
            {
                try
                {
                    using var closeCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                    closeCts.CancelAfter(WebSocketCloseTimeout);
                    await _socket.CloseAsync(
                            WebSocketCloseStatus.NormalClosure,
                            reason,
                            closeCts.Token)
                        .AsUniTask();
                }
                catch (OperationCanceledException)
                {
                    _socket.Abort();
                }
                catch
                {
                }
            }

            if (awaitSendLoop && _sendLoopTask.HasValue)
            {
                await _sendLoopTask.Value.SuppressCancellationThrow();
            }

            if (awaitReceiveLoop && _receiveLoopTask.HasValue)
            {
                await _receiveLoopTask.Value.SuppressCancellationThrow();
            }

            CleanupManagedResources();
        }

        /// <summary>
        /// セッションの後始末を一度だけ実行する。
        /// 複数経路から close されても二重 dispose しないよう guard を入れている。
        /// </summary>
        private void CleanupManagedResources()
        {
            if (_cleanedUp)
            {
                return;
            }

            _cleanedUp = true;
            ReleasePendingOutgoingMessages();
            _queueSignal.Dispose();
            _cts.Dispose();
            _socket.Dispose();
            _host.OnSessionClosed(this);
            _completionSource.TrySetResult();
        }

        private bool TryDequeue(out DebugSocketOutgoingFrame framedMessage)
        {
            lock (_queueGate)
            {
                if (_outgoingMessages.Count > 0)
                {
                    framedMessage = _outgoingMessages.Dequeue();
                    return true;
                }
            }

            framedMessage = default;
            return false;
        }

        private void ReleasePendingOutgoingMessages()
        {
            lock (_queueGate)
            {
                while (_outgoingMessages.Count > 0)
                {
                    var pendingFrame = _outgoingMessages.Dequeue();
                    pendingFrame.Release();
                }
            }
        }
    }
}
