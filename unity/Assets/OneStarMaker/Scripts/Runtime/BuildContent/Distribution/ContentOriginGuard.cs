#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace OneStarMaker.Runtime.BuildContent.Distribution
{
    /// <summary>
    /// 取得元の照会と、返した取得口・stream を包む。共通の側が取得元を信用しないための受け止めを 1 か所に持つ:
    /// <list type="bullet">
    /// <item>取消に応じない照会・open・read を、呼び手の token の取消で待たずに打ち切る。打ち切った task が後で終われば、
    /// 遅れて返った stream は破棄し、遅れた例外は観測する（観測されない task の例外を残さない）。</item>
    /// <item>取得元の側の例外を、installer へは <c>ContentDeliveryException(TransportFailure)</c> として返す（installer は他の型を
    /// 到着点の側の <c>IoFailure</c> に包むため）。取得元が自分で投げた <see cref="OperationCanceledException"/> も取得元の失敗にする。
    /// 呼び手の取消だけを <see cref="OperationCanceledException"/> で返す。</item>
    /// <item>取得口と stream の後片付け（<c>Dispose</c>）の例外は投げ直さず、<c>cleanupFailed</c> へ診断として渡す。後片付けは
    /// 成功・取消・元の失敗の結果を上書きしない。打ち切った task の後で走る遅れた後片付けの例外も観測する。</item>
    /// <item>失敗の証拠を記録する: (i) 取得元の open・read の失敗を観測した、(ii) installer へ渡した bytes そのものが対象と食い違った
    /// （<c>transport.json</c> の SHA-256 か上限、content の file の大きさか SHA-256）。hash の違いと「短い」は EOF まで渡した時だけ確定し、
    /// installer が途中で読むのを止めた時の部分の hash は数えない。「長い」は上限を超えて渡した時点で確定する。</item>
    /// </list>
    /// 読みは自分の buffer で受けてから installer の buffer へ写すので、打ち切った後に終わった読みは installer の buffer を書かない。
    /// </summary>
    internal sealed class ContentOriginGuard : IContentArtifactSource
    {
        internal const string ManifestPath = "transport.json";
        private readonly IContentArtifactSource? _inner;
        private readonly Exception? _openFailure;
        private readonly string _originId;
        private readonly string _targetDigest;
        private readonly Action<string>? _beforeEndOfFileReturned;
        private readonly Action<string>? _cleanupFailed;
        private bool _disposed;
        private Dictionary<string, ContentTransportFile> _expected = new(StringComparer.Ordinal);

        /// <param name="cleanupFailed">後片付けの例外の診断を受ける。打ち切った task の後片付けからは別の thread で呼ばれうる。</param>
        internal ContentOriginGuard(IContentOrigin origin, ContentOriginEntry entry, string targetDigest, Action<string>? beforeEndOfFileReturned,
            Action<string>? cleanupFailed = null)
        {
            _originId = origin.Id;
            _targetDigest = targetDigest;
            _beforeEndOfFileReturned = beforeEndOfFileReturned;
            _cleanupFailed = cleanupFailed;
            // OpenArtifacts は同期の契約で、打ち切れない。投げた例外は取得元の失敗 (i) として、以後の open で返す。
            try { _inner = origin.OpenArtifacts(entry) ?? throw new InvalidOperationException("The origin returned no artifact source."); }
            catch (Exception ex)
            {
                _openFailure = ex;
                SourceFailureObserved = true;
                SourceFailureReason = ex.Message;
            }
        }

        internal bool SourceFailureObserved { get; private set; }
        internal string SourceFailureReason { get; private set; } = string.Empty;
        internal bool DeliveredMismatch { get; private set; }
        internal string DeliveredMismatchReason { get; private set; } = string.Empty;
        /// <summary>この取得元から <c>transport.json</c> を installer へ渡したか（試行の記録用）。</summary>
        internal bool ManifestRead { get; private set; }

        // ─── 照会 ───

        internal readonly struct QueryResult
        {
            internal QueryResult(ContentQueryOutcome outcome, ContentOriginEntry? entry, string reason)
            { Outcome = outcome; Entry = entry; Reason = reason; }
            internal ContentQueryOutcome Outcome { get; }
            internal ContentOriginEntry? Entry { get; }
            internal string Reason { get; }
        }

        internal static async Task<QueryResult> FindAsync(IContentOrigin origin, ContentOriginQuery query, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            ContentOriginAnswer? answer;
            try
            {
                var task = origin.FindAsync(query, token) ?? throw new InvalidOperationException("The origin returned no task.");
                answer = await AwaitOrAbandon(task, token, null);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
            catch (Exception ex) { return new QueryResult(ContentQueryOutcome.Threw, null, "Query failed: " + ex.Message); }
            if (answer == null) return new QueryResult(ContentQueryOutcome.Threw, null, "Query returned no answer.");
            if (answer.Kind == ContentOriginAnswerKind.NotFound) return new QueryResult(ContentQueryOutcome.NotFound, null, answer.Reason);
            if (answer.Kind != ContentOriginAnswerKind.Found || answer.Entry == null)
                return new QueryResult(ContentQueryOutcome.Unavailable, null, answer.Reason);
            var entry = answer.Entry;
            if (!string.Equals(entry.ContentSet, query.ContentSet, StringComparison.Ordinal)
                || !string.Equals(entry.Target, query.Target, StringComparison.Ordinal)
                || (query.Revision != null && !string.Equals(entry.Revision, query.Revision, StringComparison.Ordinal)))
                return new QueryResult(ContentQueryOutcome.MismatchedEntry, null,
                    "Answered " + entry.ContentSet + "/" + entry.Target + "/" + entry.Revision + " for a different query.");
            return new QueryResult(ContentQueryOutcome.Found, entry, string.Empty);
        }

        // ─── 調停の先の読み ───

        /// <summary>
        /// installer より先に <c>transport.json</c> を読む（上限の 1 byte 先まで）。証拠の記録はここでは残さない
        /// （証拠は installer へ渡した bytes について記録する。installer は自分で読み直すので、その間の差し替えも見る）。
        /// </summary>
        internal async Task<(ContentPreReadOutcome Outcome, byte[]? Bytes, string Reason)> PreReadManifestAsync(CancellationToken token)
        {
            try
            {
                using var stream = await OpenReadAsync(ManifestPath, token);
                var guarded = (GuardStream)stream;
                guarded.Silent = true;
                using var memory = new MemoryStream();
                var buffer = new byte[81920];
                while (true)
                {
                    var read = await stream.ReadAsync(buffer, 0, buffer.Length, token);
                    if (read == 0) break;
                    if (memory.Length + read > ContentManifestValidation.MaxManifestBytes)
                        return (ContentPreReadOutcome.TooLarge, null, "transport.json exceeds " + ContentManifestValidation.MaxManifestBytes + " bytes.");
                    memory.Write(buffer, 0, read);
                }
                var bytes = memory.ToArray();
                var digest = ContentDeliveryFiles.Sha256(bytes);
                return digest == _targetDigest
                    ? (ContentPreReadOutcome.Matched, bytes, string.Empty)
                    : (ContentPreReadOutcome.DigestMismatch, null, "transport.json digest " + digest + " differs from the target " + _targetDigest + ".");
            }
            catch (ContentDeliveryException ex) when (ex.Code == ContentDeliveryFailureCode.TransportFailure)
            {
                return (ContentPreReadOutcome.Unreachable, null, ex.Message);
            }
            finally
            {
                // 先の読みは installer へ渡した bytes ではない。open・read の失敗の観測 (i) も installer の段で取り直す。
                ManifestRead = false;
                if (_openFailure == null) { SourceFailureObserved = false; SourceFailureReason = string.Empty; }
            }
        }

        /// <summary>先の読みで検証した manifest の content の file の大きさと SHA-256。installer へ渡す bytes をこれと照合する。</summary>
        internal void SetExpectedFiles(IReadOnlyList<ContentTransportFile> files)
        {
            var expected = new Dictionary<string, ContentTransportFile>(StringComparer.Ordinal);
            foreach (var file in files) expected["content/" + file.path] = file;
            _expected = expected;
        }

        // ─── installer へ渡す取得口 ───

        public async Task<Stream> OpenReadAsync(string relativePath, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (_inner == null) throw SourceFailure(_openFailure!, cancellationToken);
            Stream? stream;
            try
            {
                var task = _inner.OpenReadAsync(relativePath, cancellationToken) ?? throw new InvalidOperationException("The origin returned no task.");
                stream = await AwaitOrAbandon(task, cancellationToken, late => DisposeQuietly(late, "a late stream of " + relativePath));
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
            {
                throw SourceFailure(ex, cancellationToken);
            }
            if (stream == null) throw SourceFailure(new InvalidOperationException("The origin returned no stream."), cancellationToken);
            if (relativePath == ManifestPath) ManifestRead = true;
            _expected.TryGetValue(relativePath, out var expected);
            return new GuardStream(this, stream, relativePath, expected);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            // 取得口の Dispose は同期の契約で、待たない。違反は host の不備としてここでは打ち切らない。
            // 投げた例外は診断にし、成功・取消・元の失敗を上書きしない。
            DisposeQuietly(_inner, "the artifact source");
        }

        private void DisposeQuietly(IDisposable? disposable, string what)
        {
            try { disposable?.Dispose(); }
            catch (Exception ex)
            {
                try { _cleanupFailed?.Invoke("Origin '" + _originId + "': disposing " + what + " failed: " + ex.Message); }
                catch (Exception) { /* 診断の受け手の失敗で後片付けの結果を変えない */ }
            }
        }

        private Exception SourceFailure(Exception ex, CancellationToken token)
        {
            if (token.IsCancellationRequested) return new OperationCanceledException("Content acquisition was canceled.", ex, token);
            SourceFailureObserved = true;
            if (SourceFailureReason.Length == 0) SourceFailureReason = ex.Message;
            return ex is ContentDeliveryException { Code: ContentDeliveryFailureCode.TransportFailure }
                ? ex
                : new ContentDeliveryException(ContentDeliveryFailureCode.TransportFailure, "Origin '" + _originId + "' failed: " + ex.Message, ex);
        }

        private void MarkMismatch(string reason)
        {
            if (DeliveredMismatch) return;
            DeliveredMismatch = true;
            DeliveredMismatchReason = reason;
        }

        /// <summary>
        /// 取消に応じない task を、token の取消で待たずに打ち切る。打ち切った task の遅れた結果は <paramref name="disposeLate"/> に渡し、
        /// 遅れた例外は観測して捨てる。
        /// </summary>
        private static async Task<T> AwaitOrAbandon<T>(Task<T> task, CancellationToken token, Action<T>? disposeLate)
        {
            if (!task.IsCompleted)
            {
                var canceled = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                using (token.Register(() => canceled.TrySetResult(true)))
                {
                    if (await Task.WhenAny(task, canceled.Task) != task)
                    {
                        _ = task.ContinueWith(late =>
                        {
                            if (late.IsFaulted) _ = late.Exception;
                            else if (late.Status == TaskStatus.RanToCompletion)
                            {
                                // 遅れた後片付けの例外も観測する（disposeLate は自分で診断にするが、ここで観測されない例外を残さない）。
                                try { disposeLate?.Invoke(late.Result); }
                                catch (Exception) { /* 観測して捨てる */ }
                            }
                        }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
                        throw new OperationCanceledException("Content acquisition was canceled.", token);
                    }
                }
            }
            return await task;
        }

        private sealed class GuardStream : Stream
        {
            private readonly ContentOriginGuard _owner;
            private readonly Stream _inner;
            private readonly string _path;
            private readonly ContentTransportFile? _expected;
            private readonly IncrementalHash _hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            private byte[] _buffer = Array.Empty<byte>();
            private Task<int>? _pending;
            private long _delivered;
            private bool _disposed;

            internal GuardStream(ContentOriginGuard owner, Stream inner, string path, ContentTransportFile? expected)
            {
                _owner = owner;
                _inner = inner;
                _path = path;
                _expected = expected;
            }

            /// <summary>調停の先の読み。証拠と test 用の通知を出さない。</summary>
            internal bool Silent { get; set; }

            public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            {
                if (_disposed) throw new ObjectDisposedException(nameof(GuardStream));
                cancellationToken.ThrowIfCancellationRequested();
                // 打ち切った読みが後で書くかもしれない buffer は使い回さない。
                if (_buffer.Length < count || (_pending != null && !_pending.IsCompleted)) _buffer = new byte[Math.Max(count, 1)];
                int read;
                try
                {
                    _pending = _inner.ReadAsync(_buffer, 0, count, cancellationToken) ?? throw new InvalidOperationException("The origin returned no task.");
                    read = await AwaitOrAbandon(_pending, cancellationToken, null);
                }
                catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
                {
                    throw _owner.SourceFailure(ex, cancellationToken);
                }
                if (read < 0 || read > count) throw _owner.SourceFailure(new IOException("The origin returned an invalid read count."), cancellationToken);
                if (read > 0)
                {
                    Array.Copy(_buffer, 0, buffer, offset, read);
                    _hash.AppendData(_buffer, 0, read);
                    _delivered += read;
                    if (!Silent) CheckLength();
                    return read;
                }
                if (!Silent)
                {
                    CheckEndOfFile();
                    // 通知の後は token を見ない（installer も EOF の後に token を見ないので、この点が installer の取消の最後の点になる）。
                    _owner._beforeEndOfFileReturned?.Invoke(_path);
                }
                return 0;
            }

            private void CheckLength()
            {
                if (_path == ManifestPath && _delivered > ContentManifestValidation.MaxManifestBytes)
                    _owner.MarkMismatch("transport.json exceeded its byte limit.");
                else if (_expected != null && _delivered > _expected.size)
                    _owner.MarkMismatch(_path + " is longer than the manifest size " + _expected.size + ".");
            }

            private void CheckEndOfFile()
            {
                var digest = Hex(_hash.GetHashAndReset());
                if (_path == ManifestPath)
                {
                    if (digest != _owner._targetDigest) _owner.MarkMismatch("transport.json digest " + digest + " differs from the target.");
                }
                else if (_expected != null)
                {
                    if (_delivered != _expected.size) _owner.MarkMismatch(_path + " size " + _delivered + " differs from the manifest size " + _expected.size + ".");
                    else if (digest != _expected.sha256) _owner.MarkMismatch(_path + " digest differs from the manifest.");
                }
            }

            private static string Hex(byte[] bytes)
            {
                var chars = new char[bytes.Length * 2];
                for (var i = 0; i < bytes.Length; i++)
                {
                    chars[i * 2] = "0123456789abcdef"[bytes[i] >> 4];
                    chars[i * 2 + 1] = "0123456789abcdef"[bytes[i] & 15];
                }
                return new string(chars);
            }

            protected override void Dispose(bool disposing)
            {
                if (disposing && !_disposed)
                {
                    _disposed = true;
                    _hash.Dispose();
                    // 打ち切った読みがまだ走っていれば、終わってから閉じる（読みの最中の stream を閉じない）。
                    var pending = _pending;
                    if (pending != null && !pending.IsCompleted)
                        _ = pending.ContinueWith(_ => _owner.DisposeQuietly(_inner, "the stream of " + _path + " after an abandoned read"),
                            CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
                    else _owner.DisposeQuietly(_inner, "the stream of " + _path);
                }
                base.Dispose(disposing);
            }

            public override bool CanRead => !_disposed;
            public override bool CanSeek => false;
            public override bool CanWrite => false;
            public override long Length => throw new NotSupportedException();
            public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
            public override void Flush() { }
            public override int Read(byte[] buffer, int offset, int count) =>
                ReadAsync(buffer, offset, count, CancellationToken.None).GetAwaiter().GetResult();
            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
            public override void SetLength(long value) => throw new NotSupportedException();
            public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        }
    }
}
