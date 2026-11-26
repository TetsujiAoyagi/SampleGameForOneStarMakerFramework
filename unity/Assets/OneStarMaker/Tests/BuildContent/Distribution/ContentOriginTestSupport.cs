#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using OneStarMaker.Runtime.BuildContent.Distribution;
using UnityEngine;

namespace OneStarMaker.Tests.BuildContent.Distribution
{
    /// <summary>調停の表を同じように走らせる 3 種類の取得元（HANDOFF §2.3）。</summary>
    public enum OriginKind
    {
        /// <summary>製品の directory の取得元。</summary>
        Directory,
        /// <summary>directory でも HTTP でもない memory の取得元（file system を使わない、別の実装）。</summary>
        Memory,
        /// <summary>directory の取得元を別の型と別の id で包んだ物（型や id での分岐を検出する）。</summary>
        Wrapped,
    }

    /// <summary>temp の取得元の root と cache root。取得元の中身は directory に書き、memory の取得元はその写しを持つ。</summary>
    internal sealed class OriginFixture : IDisposable
    {
        internal const string Set = "set";
        internal const string Target = "StandaloneWindows64-Player";
        internal const string UnityVersion = "6000.6.9f1";

        internal OriginFixture()
        {
            Root = Path.Combine(Path.GetTempPath(), "osm-org", Guid.NewGuid().ToString("N").Substring(0, 10));
            CacheRoot = Path.Combine(Root, "cache");
            System.IO.Directory.CreateDirectory(Root);
        }

        internal string Root { get; }
        internal string CacheRoot { get; }
        internal string OriginRoot(int index) => Path.Combine(Root, "origin" + index);

        public void Dispose()
        {
            if (System.IO.Directory.Exists(Root)) System.IO.Directory.Delete(Root, true);
        }

        internal ContentCacheStore Store() => new(CacheRoot);

        internal ContentAcquisitionRequest Request(ContentPin? pin = null, long budget = 1024 * 1024) =>
            new(Set, Target, UnityVersion, 2, budget, pin);

        /// <summary>revision の directory（<c>transport.json</c> と <c>content/payload.bin</c>）を root の直下に置き、manifest の digest を返す。</summary>
        internal static string Publish(string originRoot, string revision, string payload = "payload", string unityVersion = UnityVersion)
        {
            var directory = Path.Combine(originRoot, revision);
            System.IO.Directory.CreateDirectory(Path.Combine(directory, "content"));
            var bytes = Encoding.UTF8.GetBytes(payload);
            File.WriteAllBytes(Path.Combine(directory, "content", "payload.bin"), bytes);
            var manifest = ManifestBytes(revision, bytes, unityVersion);
            File.WriteAllBytes(Path.Combine(directory, "transport.json"), manifest);
            return ContentDeliveryFiles.Sha256(manifest);
        }

        internal static byte[] ManifestBytes(string revision, byte[] payload, string unityVersion = UnityVersion) =>
            Encoding.UTF8.GetBytes(JsonUtility.ToJson(new ContentTransportManifest
            {
                version = 1, product = "OneStarMaker", contentSet = Set, revision = revision, target = Target,
                unityVersion = unityVersion, rootSchemaVersion = 2, playerConfigSchemaVersion = 1,
                files = new[] { new ContentTransportFile { path = "payload.bin", size = payload.Length, sha256 = ContentDeliveryFiles.Sha256(payload) } },
            }));

        /// <summary>root の head を、その revision の directory の <c>transport.json</c> に向ける（製品の形の規則で作る）。</summary>
        internal static void WriteHead(string originRoot, string revision)
        {
            var draft = ContentOriginHead.Create(File.ReadAllBytes(Path.Combine(originRoot, revision, "transport.json")), revision);
            var path = Path.Combine(originRoot, draft.RelativePath.Replace('/', Path.DirectorySeparatorChar));
            System.IO.Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllBytes(path, draft.Bytes);
        }

        /// <summary>head の申告の digest だけを書き換える（到着点の同じ revision と違う digest を申告する取得元）。</summary>
        internal static void DeclareDigest(string originRoot, string digest)
        {
            var path = Path.Combine(originRoot, ContentOriginHead.RelativePath(Set, Target).Replace('/', Path.DirectorySeparatorChar));
            var head = JsonUtility.FromJson<ContentOriginHeadFile>(File.ReadAllText(path));
            head.manifestSha256 = digest;
            File.WriteAllText(path, JsonUtility.ToJson(head, true));
        }

        internal static IContentOrigin Create(OriginKind kind, string id, string originRoot) => kind switch
        {
            OriginKind.Directory => ContentOrigins.Directory(id, originRoot),
            OriginKind.Memory => MemoryOrigin.FromDirectory(id, originRoot),
            _ => new WrappedOrigin("wrapped-" + id, ContentOrigins.Directory(id, originRoot)),
        };

        internal static ContentAcquisitionOrigin Accept(IContentOrigin origin) => new(origin, ContentOriginTrust.AcceptDeclared);

        /// <summary>cache の <c>installed/&lt;set&gt;/&lt;revision&gt;</c> の一覧。</summary>
        internal string[] Installed()
        {
            var installed = Path.Combine(CacheRoot, "installed");
            if (!System.IO.Directory.Exists(installed)) return Array.Empty<string>();
            return System.IO.Directory.GetDirectories(installed).SelectMany(System.IO.Directory.GetDirectories)
                .Select(x => Path.GetFileName(Path.GetDirectoryName(x)) + "/" + Path.GetFileName(x)).OrderBy(x => x, StringComparer.Ordinal).ToArray();
        }

        internal string[] Staging()
        {
            var staging = Path.Combine(CacheRoot, "staging");
            return System.IO.Directory.Exists(staging) ? System.IO.Directory.GetDirectories(staging).OrderBy(x => x, StringComparer.Ordinal).ToArray() : Array.Empty<string>();
        }
    }

    /// <summary>
    /// memory の取得元。file system を使わず、相対 path → bytes の表から、directory の取得元と同じ配置の規約で答える
    /// （head の読みは製品の形の規則 <see cref="ContentOriginHead.Read"/>）。振る舞いの悪い取得元は override で作る。
    /// </summary>
    internal sealed class MemoryOrigin : IContentOrigin
    {
        private readonly Dictionary<string, byte[]> _files;

        internal MemoryOrigin(string id, Dictionary<string, byte[]> files)
        {
            Id = id;
            _files = files;
        }

        internal static MemoryOrigin FromDirectory(string id, string root)
        {
            var files = new Dictionary<string, byte[]>(StringComparer.Ordinal);
            if (System.IO.Directory.Exists(root))
                foreach (var path in System.IO.Directory.GetFiles(root, "*", SearchOption.AllDirectories))
                    files[Path.GetRelativePath(root, path).Replace('\\', '/')] = File.ReadAllBytes(path);
            return new MemoryOrigin(id, files);
        }

        public string Id { get; }
        internal Dictionary<string, byte[]> Files() => new(_files, StringComparer.Ordinal);
        internal int FindCalls { get; private set; }
        internal int OpenArtifactsCalls { get; private set; }
        internal int OpenReadCalls { get; set; }
        internal Func<ContentOriginQuery, CancellationToken, Task<ContentOriginAnswer>>? FindOverride { get; set; }
        /// <summary>(location 付きの相対 path, token) → stream。null を返せば既定の動き。</summary>
        internal Func<string, CancellationToken, Task<Stream>?>? OpenOverride { get; set; }
        internal Action? OnOpenArtifacts { get; set; }

        public Task<ContentOriginAnswer> FindAsync(ContentOriginQuery query, CancellationToken cancellationToken)
        {
            FindCalls++;
            if (FindOverride != null) return FindOverride(query, cancellationToken);
            var headPath = ContentOriginHead.RelativePath(query.ContentSet, query.Target);
            ContentOriginHeadRead? head = _files.TryGetValue(headPath, out var bytes) ? ContentOriginHead.Read(bytes, query.ContentSet, query.Target) : null;
            if (query.Revision == null)
            {
                if (head == null || head.State == ContentOriginHeadState.None) return Task.FromResult(ContentOriginAnswer.NotFound("no head"));
                if (head.State != ContentOriginHeadState.Present) return Task.FromResult(ContentOriginAnswer.Unavailable(head.Reason));
                return Task.FromResult(ContentOriginAnswer.Found(new ContentOriginEntry(head.Head!.contentSet, head.Head.target, head.Head.revision,
                    head.Head.manifestSha256, head.Head.path)));
            }
            if (head?.Head != null && head.Head.revision == query.Revision)
                return Task.FromResult(ContentOriginAnswer.Found(new ContentOriginEntry(query.ContentSet, query.Target, query.Revision,
                    head.Head.manifestSha256, head.Head.path)));
            return Task.FromResult(_files.ContainsKey(query.Revision + "/transport.json")
                ? ContentOriginAnswer.Found(new ContentOriginEntry(query.ContentSet, query.Target, query.Revision, null, query.Revision))
                : ContentOriginAnswer.NotFound("no revision"));
        }

        public IContentArtifactSource OpenArtifacts(ContentOriginEntry entry)
        {
            OpenArtifactsCalls++;
            OnOpenArtifacts?.Invoke();
            return new Artifacts(this, entry.Location);
        }

        private sealed class Artifacts : IContentArtifactSource
        {
            private readonly MemoryOrigin _owner;
            private readonly string _location;
            internal Artifacts(MemoryOrigin owner, string location) { _owner = owner; _location = location; }

            public Task<Stream> OpenReadAsync(string relativePath, CancellationToken cancellationToken)
            {
                _owner.OpenReadCalls++;
                var path = _location + "/" + relativePath;
                var overridden = _owner.OpenOverride?.Invoke(path, cancellationToken);
                if (overridden != null) return overridden;
                if (!_owner._files.TryGetValue(path, out var bytes))
                    throw new ContentDeliveryException(ContentDeliveryFailureCode.TransportFailure, "Missing in memory: " + path);
                return Task.FromResult<Stream>(new MemoryStream(bytes, false));
            }

            public void Dispose() { }
        }
    }

    /// <summary>別の型と id で包んだ取得元。開いた回数を数える（到着点に在る時に bytes の取得口を開かないことの確認）。</summary>
    internal sealed class WrappedOrigin : IContentOrigin
    {
        private readonly IContentOrigin _inner;
        internal WrappedOrigin(string id, IContentOrigin inner) { Id = id; _inner = inner; }

        public string Id { get; }
        internal int OpenArtifactsCalls { get; private set; }
        internal int OpenReadCalls { get; private set; }

        public Task<ContentOriginAnswer> FindAsync(ContentOriginQuery query, CancellationToken cancellationToken) => _inner.FindAsync(query, cancellationToken);

        public IContentArtifactSource OpenArtifacts(ContentOriginEntry entry)
        {
            OpenArtifactsCalls++;
            return new Counting(this, _inner.OpenArtifacts(entry));
        }

        private sealed class Counting : IContentArtifactSource
        {
            private readonly WrappedOrigin _owner;
            private readonly IContentArtifactSource _inner;
            internal Counting(WrappedOrigin owner, IContentArtifactSource inner) { _owner = owner; _inner = inner; }

            public Task<Stream> OpenReadAsync(string relativePath, CancellationToken cancellationToken)
            {
                _owner.OpenReadCalls++;
                return _inner.OpenReadAsync(relativePath, cancellationToken);
            }

            public void Dispose() => _inner.Dispose();
        }
    }

    /// <summary>bytes を返す stream。指定の読み（0 始まりの回数）で、signal が来るまで待つ・例外を投げる。待ちは token を見ない（取消に応じない取得元）。</summary>
    internal sealed class ControlledStream : Stream
    {
        private readonly MemoryStream _inner;
        private int _reads;
        internal ControlledStream(byte[] bytes) => _inner = new MemoryStream(bytes, false);

        /// <summary>この回の読みの前に待つ（null なら待たない）。EOF の読みも 1 回に数える。</summary>
        internal Func<int, Task?>? BeforeRead { get; set; }
        internal int? ThrowAtRead { get; set; }
        internal bool Disposed { get; private set; }

        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            var index = _reads++;
            var gate = BeforeRead?.Invoke(index);
            if (gate != null) await gate;
            if (ThrowAtRead == index) throw new IOException("fixture read failure at " + index);
            return _inner.Read(buffer, offset, Math.Min(count, 7));
        }

        public override int Read(byte[] buffer, int offset, int count) => ReadAsync(buffer, offset, count, CancellationToken.None).GetAwaiter().GetResult();
        protected override void Dispose(bool disposing) { Disposed = true; base.Dispose(disposing); }
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
