#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;

namespace OneStarMaker.Runtime.BuildContent.Distribution
{
    /// <summary>
    /// 取得元: 検証する前の bytes を持つ場所（手元の publish 済みの directory、LAN、cloud）。到着点（installed cache）でも、
    /// 要求時解決の供給元（<c>IContentProvider</c>）でもない。
    /// <para>
    /// 取得元が答えるのは 2 つだけ: 照会（head か指定の revision を持つか）と、見つけた 1 件の bytes を開くこと。
    /// 期待 digest の照合・互換の判定・install・cache・当たる順と止まる規則・取消と例外の受け止め・記録は共通の側
    /// （<see cref="ContentAcquisition"/>）が持ち、取得元に実装させない。共通の側は取得元を信用しない: 照会の答えも bytes も、
    /// 検証を通るまで使わず、取得元の型や <see cref="Id"/> で分岐しない。
    /// </para>
    /// <para>
    /// 資格情報を受ける引数は持たない（実装は host が資格情報ごと組み立てて渡す）。<see cref="OpenArtifacts"/> と、それが返した取得口の
    /// <see cref="IDisposable.Dispose"/> は同期で待たない契約で、共通の側はこの 2 つを打ち切れない。
    /// </para>
    /// </summary>
    public interface IContentOrigin
    {
        /// <summary>結果と試行の記録に出す名前。共通の側はこの値で分岐しない。</summary>
        string Id { get; }

        /// <summary>
        /// 照会。「持っていない」は例外ではなく <see cref="ContentOriginAnswerKind.NotFound"/> で答える。
        /// 取消に応じなくても、例外を投げても、共通の側が受け止める。
        /// </summary>
        Task<ContentOriginAnswer> FindAsync(ContentOriginQuery query, CancellationToken cancellationToken);

        /// <summary>照会が返した 1 件の bytes の取得口。呼び手が破棄する。</summary>
        IContentArtifactSource OpenArtifacts(ContentOriginEntry entry);
    }

    /// <summary>照会: contentSet と target と、任意の revision。revision が null なら取得元が勧める 1 件（head）を問う。</summary>
    public sealed class ContentOriginQuery
    {
        public ContentOriginQuery(string contentSet, string target, string? revision = null)
        {
            ContentSet = RequireSegment(contentSet, nameof(contentSet));
            Target = RequireSegment(target, nameof(target));
            Revision = revision == null ? null : RequireSegment(revision, nameof(revision));
        }

        public string ContentSet { get; }
        public string Target { get; }
        public string? Revision { get; }

        internal static string RequireSegment(string value, string name)
        {
            if (!ContentManifestValidation.Segment(value)) throw new ArgumentException("Value is not a valid content identity segment.", name);
            return value;
        }
    }

    /// <summary>
    /// 照会が見つけた 1 件。申告の digest は取得元の head に書いてあった値で、無いこともある（revision の決まった所在だけで答えた時）。
    /// 所在は取得元だけが解釈する不透明な文字列で、共通の側は読まずに <see cref="IContentOrigin.OpenArtifacts"/> へ返す。
    /// </summary>
    public sealed class ContentOriginEntry
    {
        public ContentOriginEntry(string contentSet, string target, string revision, string? declaredManifestSha256, string location)
        {
            // 取得元の FindAsync の中で投げた例外は、共通の側が「答えない」として受け止める（調停を止めない）。
            // 照会と違う contentSet・target・revision の entry は、形が正しくても共通の側が照会と照合して飛ばす。
            ContentSet = ContentOriginQuery.RequireSegment(contentSet, nameof(contentSet));
            Target = ContentOriginQuery.RequireSegment(target, nameof(target));
            Revision = ContentOriginQuery.RequireSegment(revision, nameof(revision));
            if (declaredManifestSha256 != null && !ContentManifestValidation.Hash(declaredManifestSha256))
                throw new ArgumentException("Declared digest must be 64 lowercase hex characters.", nameof(declaredManifestSha256));
            DeclaredManifestSha256 = declaredManifestSha256;
            Location = location ?? throw new ArgumentNullException(nameof(location));
        }

        public string ContentSet { get; }
        public string Target { get; }
        public string Revision { get; }
        public string? DeclaredManifestSha256 { get; }
        public string Location { get; }
    }

    public enum ContentOriginAnswerKind { Found, NotFound, Unavailable }

    /// <summary>照会の答え。<see cref="Entry"/> は <see cref="ContentOriginAnswerKind.Found"/> の時だけ在る。</summary>
    public sealed class ContentOriginAnswer
    {
        private ContentOriginAnswer(ContentOriginAnswerKind kind, ContentOriginEntry? entry, string reason)
        {
            Kind = kind;
            Entry = entry;
            Reason = reason ?? string.Empty;
        }

        public static ContentOriginAnswer Found(ContentOriginEntry entry) =>
            new(ContentOriginAnswerKind.Found, entry ?? throw new ArgumentNullException(nameof(entry)), string.Empty);

        public static ContentOriginAnswer NotFound(string reason) => new(ContentOriginAnswerKind.NotFound, null, reason);

        /// <summary>答えられない（読めない、未対応の head、壊れた head など）。共通の側は次の取得元へ進む。</summary>
        public static ContentOriginAnswer Unavailable(string reason) => new(ContentOriginAnswerKind.Unavailable, null, reason);

        public ContentOriginAnswerKind Kind { get; }
        public ContentOriginEntry? Entry { get; }
        public string Reason { get; }
    }

    /// <summary>期待 digest の出所。pin は bytes と別の経路で得た組、申告は取得元の head に書いてあった digest。</summary>
    public enum ContentDigestAuthority { Pinned, Declared }

    /// <summary>
    /// host が取得元ごとに決める信頼。Framework の既定は <see cref="PinnedOnly"/>。申告を認めた取得元は、そこに書ける人による
    /// 差し替えを検出しない（記録には出るが止めない）。
    /// </summary>
    public enum ContentOriginTrust { PinnedOnly, AcceptDeclared }
}
