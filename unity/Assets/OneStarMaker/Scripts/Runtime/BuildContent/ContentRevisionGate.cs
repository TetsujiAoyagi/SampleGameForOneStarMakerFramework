#nullable enable

using System;
using System.Collections.Generic;
using System.IO;

namespace OneStarMaker.Runtime.BuildContent
{
    /// <summary>process 内 ledger と user-local OS lease で revision の登録、利用、削除を調停する。</summary>
    public static class ContentRevisionGate
    {
        private static readonly object Sync = new();
        private static Registration? _registration;
        private static readonly List<Deletion> Deletions = new();
        private static readonly List<Registration.Layer> Reads = new();

        internal static IDisposable AcquireRead(string buildIdentity, string target, string absolutePath)
        {
            if (!TryNormalize(buildIdentity, target, absolutePath, out var path))
                throw new ContentDirectoryException(ContentDirectoryFailureCode.InvalidConfiguration,
                    "Content read lease input is invalid.", buildIdentity, target);
            lock (Sync)
            {
                if (Deletions.Exists(value => SameRevisionOrPath(value.Identity, value.Target,
                        value.Path, buildIdentity, target, path)))
                    throw new ContentDirectoryException(ContentDirectoryFailureCode.DeletionInProgress,
                        "A content deletion is in progress.", buildIdentity, target);
                var process = AcquireProcessRead(buildIdentity, target, path, "read");
                // 読みの lease は登録とは別の台帳。参照の同一で外すので、同じ revision の読みが重なっても数が合う。
                var value = new Registration.Layer(buildIdentity, target, path);
                Reads.Add(value);
                return new CompositeLease(process, () =>
                {
                    lock (Sync) Reads.Remove(value);
                });
            }
        }

        public static bool TryAcquireDelete(string buildIdentity, string target, string absolutePath,
            out ContentDeletionLease? lease, out ContentDirectoryFailureCode rejection)
        {
            lease = null;
            if (!TryNormalize(buildIdentity, target, absolutePath, out var path))
            { rejection = ContentDirectoryFailureCode.InvalidConfiguration; return false; }
            lock (Sync)
            {
                if (Deletions.Exists(value => SameRevisionOrPath(value.Identity, value.Target, value.Path,
                    buildIdentity, target, path)))
                { rejection = ContentDirectoryFailureCode.DeletionInProgress; return false; }
                if (_registration != null && _registration.Overlaps(buildIdentity, target, path))
                {
                    rejection = _registration.RejectionFor(buildIdentity, target, path);
                    return false;
                }
                if (Reads.Exists(value => SameRevisionOrPath(value.Identity, value.Target, value.Path,
                        buildIdentity, target, path)))
                { rejection = ContentDirectoryFailureCode.RevisionBusy; return false; }
                RevisionProcessLease? process;
                try
                {
                    if (!RevisionProcessLease.TryAcquireDelete(buildIdentity, target, path, out process))
                    { rejection = ContentDirectoryFailureCode.RevisionBusy; return false; }
                }
                catch (ContentDirectoryException exception)
                {
                    rejection = exception.Code;
                    return false;
                }
                var deletion = new Deletion(buildIdentity, target, path);
                Deletions.Add(deletion);
                lease = new ContentDeletionLease(() => { process!.Dispose(); ReleaseDeletion(deletion); });
                rejection = default;
                return true;
            }
        }

        internal static IDisposable Reserve(string buildIdentity, string target, string absolutePath)
            => Reserve(new[] { new ContentRevisionLayer(buildIdentity, target, absolutePath) });

        /// <summary>
        /// 層（base と任意の上書き）を 1 つの登録として予約する。どの層の (identity, path) への削除の許可も、予約を返すまで拒まれる。
        /// 層ごとに OS の lease を取って合成し、途中で取れなければ取れた分を返して予約 0 件で終える。
        /// 登録の基数は今までどおり process に 1 件で、層を持つ登録の後の別の登録も拒む。
        /// </summary>
        /// <exception cref="ContentDirectoryException">
        /// 入力が不正、または層どうしで path か revision が同じ（<see cref="ContentDirectoryFailureCode.InvalidConfiguration"/>。予約の前に拒む）。
        /// 削除中、既に登録がある、OS の lease が取れない時は単層と同じ値。
        /// </exception>
        internal static IDisposable Reserve(IReadOnlyList<ContentRevisionLayer> layers)
        {
            if (layers == null || layers.Count == 0) throw new ArgumentException("At least one layer is required.", nameof(layers));
            var normalized = new Registration.Layer[layers.Count];
            for (var i = 0; i < layers.Count; i++)
            {
                var layer = layers[i];
                if (!TryNormalize(layer.BuildIdentity, layer.Target, layer.AbsolutePath, out var path))
                    throw new ContentDirectoryException(ContentDirectoryFailureCode.InvalidConfiguration, "Content directory registration input is invalid.", layer.BuildIdentity, layer.Target);
                normalized[i] = new Registration.Layer(layer.BuildIdentity, layer.Target, path);
            }
            // 同じ path か同じ revision を 2 層に持つ登録は、層ごとの照合と解除の順の前提（層は別の directory）を崩す。
            for (var i = 0; i < normalized.Length; i++)
                for (var j = i + 1; j < normalized.Length; j++)
                    if (SameRevisionOrPath(normalized[i].Identity, normalized[i].Target, normalized[i].Path,
                            normalized[j].Identity, normalized[j].Target, normalized[j].Path))
                        throw new ContentDirectoryException(ContentDirectoryFailureCode.InvalidConfiguration,
                            "Layers of one content directory registration must not share a path or a revision.", normalized[j].Identity, normalized[j].Target);
            lock (Sync)
            {
                foreach (var layer in normalized)
                    if (Deletions.Exists(value => SameRevisionOrPath(value.Identity, value.Target, value.Path,
                        layer.Identity, layer.Target, layer.Path)))
                        throw new ContentDirectoryException(ContentDirectoryFailureCode.DeletionInProgress,
                            "A content directory deletion is in progress.", layer.Identity, layer.Target);
                if (_registration != null)
                {
                    var code = ContentDirectoryFailureCode.RevisionBusy;
                    foreach (var layer in normalized)
                        if (_registration.RejectionFor(layer.Identity, layer.Target, layer.Path) == ContentDirectoryFailureCode.PathInUse)
                            code = ContentDirectoryFailureCode.PathInUse;
                    throw new ContentDirectoryException(code, "A content directory is already registered in this process.", normalized[0].Identity, normalized[0].Target);
                }
                var registration = new Registration(normalized);
                var processes = new List<IDisposable>(normalized.Length);
                try
                {
                    foreach (var layer in normalized)
                        processes.Add(AcquireProcessRead(layer.Identity, layer.Target, layer.Path, "registration"));
                }
                catch
                {
                    for (var i = processes.Count - 1; i >= 0; i--) processes[i].Dispose();
                    throw;
                }
                _registration = registration;
                return new CompositeLease(new LeaseSet(processes), new RegistrationLease(registration).Dispose);
            }
        }

        private static RevisionProcessLease AcquireProcessRead(string buildIdentity, string target,
            string path, string operation)
        {
            try
            {
                return RevisionProcessLease.AcquireRead(buildIdentity, target, path);
            }
            catch (IOException exception) when (RevisionProcessLease.IsSharingViolation(exception))
            {
                // process 内 ledger に見えない別 process の排他削除も busy として扱う。
                throw new ContentDirectoryException(ContentDirectoryFailureCode.DeletionInProgress,
                    $"A content deletion blocks the revision {operation} lock.", buildIdentity, target,
                    innerException: exception);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                throw new ContentDirectoryException(ContentDirectoryFailureCode.RevisionLockUnavailable,
                    $"Revision {operation} lock is unavailable.", buildIdentity, target,
                    innerException: exception);
            }
        }

        internal static bool TryNormalize(string identity, string target, string path, out string normalized)
        {
            normalized = string.Empty;
            if (string.IsNullOrWhiteSpace(identity) || string.IsNullOrWhiteSpace(target) || string.IsNullOrWhiteSpace(path)) return false;
            try
            {
                if (!Path.IsPathRooted(path)) return false;
                var full = Path.GetFullPath(path);
                var root = Path.GetPathRoot(full)!;
                // 同じ物理 directory の末尾 separator・Windows 大小文字 alias を一つの
                // revision path に畳む。root 自体の separator は落とさない。
                full = full.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                if (full.Length < root.Length) full = root;
                normalized = Path.DirectorySeparatorChar == '\\' ? full.ToUpperInvariant() : full;
                return true;
            }
            catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
            { return false; }
        }

        private static void ReleaseDeletion(Deletion deletion)
        {
            lock (Sync)
            {
                Deletions.Remove(deletion);
            }
        }

        // 単一の active session 制約は、無関係な revision の物理削除まで止める理由にならない。
        // identity または実 path が同じ場合だけ利用と削除を排他し、別 identity で同じ path を削る抜け道も塞ぐ。
        private static bool SameRevisionOrPath(string identity, string target, string path,
            string otherIdentity, string otherTarget, string otherPath)
            => (identity == otherIdentity && target == otherTarget) || path == otherPath;

        // 1 件の登録。層（base と任意の上書き）の (identity, target, path) を全て持ち、利用・削除の調停はどれかの層に当たれば当たったとする。
        private sealed class Registration
        {
            private readonly Layer[] _layers;
            internal Registration(Layer[] layers) => _layers = layers;

            internal bool Overlaps(string identity, string target, string path)
            {
                foreach (var layer in _layers)
                    if (SameRevisionOrPath(layer.Identity, layer.Target, layer.Path, identity, target, path)) return true;
                return false;
            }

            // 拒否の値の優先: どれかの層と path が同じで revision が違えば PathInUse（別 revision で同じ path を削る抜け道）、それ以外は RevisionBusy。
            internal ContentDirectoryFailureCode RejectionFor(string identity, string target, string path)
            {
                foreach (var layer in _layers)
                    if (layer.Path == path && (layer.Identity != identity || layer.Target != target))
                        return ContentDirectoryFailureCode.PathInUse;
                return ContentDirectoryFailureCode.RevisionBusy;
            }

            internal sealed class Layer
            {
                internal Layer(string identity, string target, string path)
                { Identity = identity; Target = target; Path = path; }
                internal string Identity { get; }
                internal string Target { get; }
                internal string Path { get; }
            }
        }

        internal sealed class Deletion
        {
            internal Deletion(string identity, string target, string path)
            { Identity = identity; Target = target; Path = path; }
            internal string Identity { get; }
            internal string Target { get; }
            internal string Path { get; }
        }

        private sealed class RegistrationLease : IDisposable
        {
            private Registration? _value;
            internal RegistrationLease(Registration value) => _value = value;
            public void Dispose()
            {
                lock (Sync)
                {
                    if (_value != null && ReferenceEquals(_registration, _value)) _registration = null;
                    _value = null;
                }
            }
        }

        // 層ごとの OS の lease を逆順に返す。1 つの Dispose の例外で残りを返し損ねない。
        private sealed class LeaseSet : IDisposable
        {
            private List<IDisposable>? _leases;
            internal LeaseSet(List<IDisposable> leases) => _leases = leases;
            public void Dispose()
            {
                var leases = _leases;
                _leases = null;
                if (leases == null) return;
                Exception? failure = null;
                for (var i = leases.Count - 1; i >= 0; i--)
                {
                    try { leases[i].Dispose(); }
                    catch (Exception ex) { failure ??= ex; }
                }
                if (failure != null) throw failure;
            }
        }

        private sealed class CompositeLease : IDisposable
        {
            private IDisposable? _inner;
            private Action? _release;
            internal CompositeLease(IDisposable inner, Action release)
            { _inner = inner; _release = release; }
            public void Dispose()
            {
                var release = _release;
                _release = null;
                try { release?.Invoke(); }
                finally { _inner?.Dispose(); _inner = null; }
            }
        }
    }

    /// <summary>登録の予約の 1 層（base か上書き）の入力。</summary>
    internal readonly struct ContentRevisionLayer
    {
        internal ContentRevisionLayer(string buildIdentity, string target, string absolutePath)
        { BuildIdentity = buildIdentity; Target = target; AbsolutePath = absolutePath; }
        internal string BuildIdentity { get; }
        internal string Target { get; }
        internal string AbsolutePath { get; }
    }

    /// <summary>物理削除そのものは行わず、DIST が finally まで保持する process 内 delete lease。</summary>
    public sealed class ContentDeletionLease : IDisposable
    {
        private Action? _release;
        internal ContentDeletionLease(Action release) => _release = release;
        public void Dispose() { var release = _release; _release = null; release?.Invoke(); }
    }
}
