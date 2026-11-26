#nullable enable

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace OneStarMaker.Runtime.BuildContent.Distribution
{
    internal enum ContentOriginOpenState { Opened, Missing, Unreadable }

    /// <summary>取得元の root からの相対の所在を開いた結果。今の取得口は「無い」と「読めない」を区別しないので、開く関数が自分で区別する。</summary>
    internal readonly struct ContentOriginOpen
    {
        private ContentOriginOpen(ContentOriginOpenState state, Stream? stream, string reason)
        {
            State = state;
            Stream = stream;
            Reason = reason;
        }

        internal static ContentOriginOpen Opened(Stream stream) => new(ContentOriginOpenState.Opened, stream, string.Empty);
        internal static ContentOriginOpen Missing(string reason) => new(ContentOriginOpenState.Missing, null, reason);
        internal static ContentOriginOpen Unreadable(string reason) => new(ContentOriginOpenState.Unreadable, null, reason);

        internal ContentOriginOpenState State { get; }
        internal Stream? Stream { get; }
        internal string Reason { get; }
    }

    /// <summary>
    /// head の file と revision の所在を読んで答える取得元。呼ばれるたびに読み、状態を持たない。
    /// 配置の規約: head は <see cref="ContentOriginHead.RelativePath"/>、revision を指定した照会で head と違う revision は root の直下の
    /// revision の名前の directory。directory の列挙は使わない（cloud の object storage でも同じ形で答えられるように）。
    /// </summary>
    internal sealed class IndexedContentOrigin : IContentOrigin
    {
        private readonly Func<string, CancellationToken, Task<ContentOriginOpen>> _open;
        private readonly Func<string, IContentArtifactSource> _artifacts;

        internal IndexedContentOrigin(string id, Func<string, CancellationToken, Task<ContentOriginOpen>> open,
            Func<string, IContentArtifactSource> artifacts)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Origin id is required.", nameof(id));
            Id = id;
            _open = open ?? throw new ArgumentNullException(nameof(open));
            _artifacts = artifacts ?? throw new ArgumentNullException(nameof(artifacts));
        }

        public string Id { get; }

        public async Task<ContentOriginAnswer> FindAsync(ContentOriginQuery query, CancellationToken cancellationToken)
        {
            if (query == null) throw new ArgumentNullException(nameof(query));
            var head = await ReadHead(query, cancellationToken);
            if (query.Revision == null)
            {
                return head.State switch
                {
                    ContentOriginHeadState.Present => ContentOriginAnswer.Found(ToEntry(head.Head!, true)),
                    ContentOriginHeadState.None => ContentOriginAnswer.NotFound(head.Reason),
                    _ => ContentOriginAnswer.Unavailable(head.Reason),
                };
            }
            if (head.State == ContentOriginHeadState.Present && string.Equals(head.Head!.revision, query.Revision, StringComparison.Ordinal))
                return ContentOriginAnswer.Found(ToEntry(head.Head, true));

            // head と違う revision（または head を読めない時）は、root の直下の revision の名前の directory を所在にする。
            // 申告の digest はここには無い（pin で照合する経路だけが使う）。
            var opened = await _open(query.Revision + "/transport.json", cancellationToken);
            switch (opened.State)
            {
                case ContentOriginOpenState.Opened:
                    opened.Stream!.Dispose();
                    return ContentOriginAnswer.Found(new ContentOriginEntry(query.ContentSet, query.Target, query.Revision, null, query.Revision));
                case ContentOriginOpenState.Missing:
                    return ContentOriginAnswer.NotFound("Revision " + query.Revision + " is not in this origin: " + opened.Reason);
                default:
                    return ContentOriginAnswer.Unavailable("Revision " + query.Revision + " cannot be read: " + opened.Reason);
            }
        }

        public IContentArtifactSource OpenArtifacts(ContentOriginEntry entry)
        {
            if (entry == null) throw new ArgumentNullException(nameof(entry));
            return _artifacts(entry.Location);
        }

        private async Task<ContentOriginHeadRead> ReadHead(ContentOriginQuery query, CancellationToken cancellationToken)
        {
            var opened = await _open(ContentOriginHead.RelativePath(query.ContentSet, query.Target), cancellationToken);
            if (opened.State == ContentOriginOpenState.Missing)
                return new ContentOriginHeadRead(ContentOriginHeadState.None, null, "No head: " + opened.Reason);
            if (opened.State == ContentOriginOpenState.Unreadable)
                return new ContentOriginHeadRead(ContentOriginHeadState.Invalid, null, "Head cannot be read: " + opened.Reason);
            byte[] bytes;
            using (var stream = opened.Stream!)
            using (var memory = new MemoryStream())
            {
                var buffer = new byte[8192];
                while (true)
                {
                    var read = await stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken);
                    if (read == 0) break;
                    // 上限の 1 byte 先まで読めば「超えた」と分かる。それ以上は読まない。
                    if (memory.Length + read > ContentOriginHead.MaxBytes)
                        return new ContentOriginHeadRead(ContentOriginHeadState.Invalid, null, "Head exceeds " + ContentOriginHead.MaxBytes + " bytes.");
                    memory.Write(buffer, 0, read);
                }
                bytes = memory.ToArray();
            }
            return ContentOriginHead.Read(bytes, query.ContentSet, query.Target);
        }

        private static ContentOriginEntry ToEntry(ContentOriginHeadFile head, bool declared) =>
            new(head.contentSet, head.target, head.revision, declared ? head.manifestSha256 : null, head.path);
    }

    /// <summary>host が組み立てる取得元の入口。</summary>
    public static class ContentOrigins
    {
        /// <summary>
        /// 手元の directory の取得元。root の下に head（<c>.origin/</c>）と、publish が公開した 1 revision の directory を持つ。
        /// file の不在は <see cref="File.Exists"/> で自分で確かめて「無い」と答え、root が無い時も「無い」と答える（設定の場所が
        /// まだ作られていなくても拒まない）。
        /// </summary>
        public static IContentOrigin Directory(string id, string root)
        {
            if (string.IsNullOrWhiteSpace(root) || !Path.IsPathRooted(root))
                throw new ArgumentException("Origin root must be an absolute path.", nameof(root));
            var full = Path.GetFullPath(root);
            return new IndexedContentOrigin(id, (relative, token) => Task.FromResult(OpenFile(full, relative, token)),
                location => new LocalContentArtifactSource(ContentDeliveryFiles.Resolve(full, location)));
        }

        private static ContentOriginOpen OpenFile(string root, string relative, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            string path;
            try { path = ContentDeliveryFiles.Resolve(root, relative); }
            catch (ContentDeliveryException ex) { return ContentOriginOpen.Unreadable(ex.Message); }
            if (!File.Exists(path)) return ContentOriginOpen.Missing(path);
            try { return ContentOriginOpen.Opened(new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read)); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return ContentOriginOpen.Unreadable(ex.Message); }
        }
    }
}
