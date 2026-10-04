#nullable enable

using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace OneStarMaker.Tests.Editor.Build
{
    // StopAsync が返るまで listener、実行中 response、worker の寿命をまとめて所有する。
    internal sealed class LoopbackArtifactServer
    {
        private readonly HttpListener _listener = new();
        private readonly Task _worker;
        private readonly CancellationTokenRegistration _operationCancellation;
        private readonly string _root;
        private readonly string? _stallPath;
        private readonly TaskCompletionSource<bool> _responseStarted =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<bool> _responseClosed =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<bool> _releaseStall =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private HttpListenerResponse? _activeResponse;
        private int _requestCount;
        private int _stopping;

        internal Uri BaseUri { get; }
        internal int RequestCount => Volatile.Read(ref _requestCount);
        internal Task ResponseStarted => _responseStarted.Task;
        internal Task ResponseClosed => _responseClosed.Task;
        internal Task Completion => _worker;
        internal bool HasActiveResponse => Volatile.Read(ref _activeResponse) != null;

        internal LoopbackArtifactServer(string root, CancellationToken operationCancellation,
            string? stallPath = null)
        {
            _root = Path.GetFullPath(root);
            _stallPath = stallPath;
            var reservation = new TcpListener(IPAddress.Loopback, 0);
            reservation.Start();
            var port = ((IPEndPoint)reservation.LocalEndpoint).Port;
            reservation.Stop();
            BaseUri = new Uri("http://127.0.0.1:" + port + "/");
            _listener.Prefixes.Add(BaseUri.AbsoluteUri);
            _listener.Start();
            _operationCancellation = operationCancellation.Register(RequestStop);
            _worker = Task.Run(ServeAsync);
        }

        private async Task ServeAsync()
        {
            try
            {
                while (Volatile.Read(ref _stopping) == 0)
                {
                    var context = await _listener.GetContextAsync();
                    Volatile.Write(ref _activeResponse, context.Response);
                    try
                    {
                        var relative = Uri.UnescapeDataString(context.Request.Url!.AbsolutePath.TrimStart('/'));
                        var file = Path.GetFullPath(Path.Combine(_root,
                            relative.Replace('/', Path.DirectorySeparatorChar)));
                        if (!file.StartsWith(_root + Path.DirectorySeparatorChar,
                                StringComparison.OrdinalIgnoreCase) || !File.Exists(file))
                        {
                            context.Response.StatusCode = 404;
                            continue;
                        }
                        Interlocked.Increment(ref _requestCount);
                        context.Response.ContentLength64 = new FileInfo(file).Length;
                        using var stream = File.OpenRead(file);
                        if (relative == _stallPath)
                        {
                            // header と本文の先頭1 byte を送ってから止め、実 HTTP 本文の
                            // read 中断を再現する。接続前の取消を誤って成功としない。
                            var firstByte = new byte[1];
                            var firstLength = await stream.ReadAsync(firstByte, 0, 1);
                            if (firstLength != 0)
                                await context.Response.OutputStream.WriteAsync(firstByte, 0, firstLength);
                            await context.Response.OutputStream.FlushAsync();
                            _responseStarted.TrySetResult(true);
                            await _releaseStall.Task;
                        }
                        await stream.CopyToAsync(context.Response.OutputStream);
                    }
                    finally
                    {
                        try { context.Response.Close(); }
                        catch (Exception ex) when (Volatile.Read(ref _stopping) != 0 &&
                            (ex is HttpListenerException or ObjectDisposedException or IOException)) { }
                        Volatile.Write(ref _activeResponse, null);
                        _responseClosed.TrySetResult(true);
                    }
                }
            }
            catch (Exception ex) when (Volatile.Read(ref _stopping) != 0 &&
                (ex is HttpListenerException or ObjectDisposedException or IOException or TaskCanceledException)) { }
        }

        private void RequestStop()
        {
            if (Interlocked.Exchange(ref _stopping, 1) != 0) return;
            _releaseStall.TrySetCanceled();
            _listener.Stop();
            _listener.Close();
            try { Volatile.Read(ref _activeResponse)?.Close(); }
            catch (Exception ex) when (ex is HttpListenerException or ObjectDisposedException or IOException) { }
        }

        internal async Task StopAsync()
        {
            RequestStop();
            try { await _worker; }
            finally { _operationCancellation.Dispose(); }
        }
    }
}
