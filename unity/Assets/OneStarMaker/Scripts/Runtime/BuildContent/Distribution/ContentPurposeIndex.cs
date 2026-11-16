#nullable enable

using System;
using System.Collections.Generic;
using System.Text;

namespace OneStarMaker.Runtime.BuildContent.Distribution
{
    /// <summary>
    /// 選択の候補の 1 つ: 目的 build の <c>transport.json</c> の bytes と、供給元が別に得た期待 digest。
    /// content の file・path・Unity の Content Directory を持たないので、選択はそれらを開けない。
    /// </summary>
    public sealed class ContentPurposeCandidate
    {
        public ContentPurposeCandidate(byte[] manifestBytes, string expectedManifestSha256)
        {
            if (manifestBytes == null) throw new ArgumentNullException(nameof(manifestBytes));
            // 呼び手が後から配列を書き換えても、検証した bytes と読む bytes をずらさない。
            ManifestBytes = (byte[])manifestBytes.Clone();
            ExpectedManifestSha256 = expectedManifestSha256 ?? throw new ArgumentNullException(nameof(expectedManifestSha256));
        }

        internal byte[] ManifestBytes { get; }
        public string ExpectedManifestSha256 { get; }
    }

    /// <summary>要求: logical key・kind・任意の表現。表現が null か空なら「指定なし」で、Full の entry だけに当たる。</summary>
    public sealed class ContentPurposeRequest
    {
        public ContentPurposeRequest(BuildContentKind kind, string logicalKey, string? representation = null)
        {
            if (!Enum.IsDefined(typeof(BuildContentKind), kind)) throw new ArgumentOutOfRangeException(nameof(kind));
            if (string.IsNullOrWhiteSpace(logicalKey)) throw new ArgumentException("Logical key is required.", nameof(logicalKey));
            Kind = kind;
            LogicalKey = logicalKey;
            Representation = representation;
        }

        public BuildContentKind Kind { get; }
        public string LogicalKey { get; }
        public string? Representation { get; }
    }

    public enum ContentPurposeIndexState
    {
        /// <summary>版 1 の索引を読めた。<see cref="ContentPurposeIndex.Contains"/> で照合できる。</summary>
        Indexed,
        /// <summary>索引の欄が無いか、版 0（索引を記録しない build の publish）。「含む」とも「含まない」とも答えない。</summary>
        NoIndex,
        /// <summary>型の規則を保つ知らない版（2 以上）。「含む」とも「含まない」とも答えない。</summary>
        UnsupportedIndex,
        /// <summary>検証に落ちた（<see cref="ContentPurposeIndexFailure"/>）。この bytes を選択に使わない。</summary>
        Invalid,
    }

    public enum ContentPurposeIndexFailure
    {
        None,
        /// <summary>manifest が install と同じ上限（4 MiB）を超える。</summary>
        TooLarge,
        /// <summary>bytes の sha256 が期待 digest と違う。</summary>
        DigestMismatch,
        /// <summary>manifest が UTF-8 の JSON の object として読めない。</summary>
        InvalidManifest,
        /// <summary>索引の型の失敗（どの版でも）、負の版、版 1 の値の失敗（欄の欠け、未知の kind、重複、空白だけの値）。</summary>
        InvalidIndex,
    }

    /// <summary>
    /// publish が <c>transport.json</c> に書いた目的 build の索引の読み口と、順のある候補からの選択。
    /// 選択は manifest の bytes だけで答え、content を登録・走査しない。照合の規則（表現の Full fallback を含む）は、
    /// 登録した directory の解決と同じ 1 つの規則を使うので、「含む」と答えた build はその directory で解決できる。
    /// </summary>
    public sealed class ContentPurposeIndex
    {
        private readonly ContentEntryTable<bool>? _table;

        private ContentPurposeIndex(ContentPurposeIndexState state, ContentPurposeIndexFailure failure, string detail,
            int? version, IReadOnlyList<ContentTransportIndexEntry> entries, ContentEntryTable<bool>? table)
        {
            State = state;
            Failure = failure;
            Detail = detail;
            Version = version;
            Entries = entries;
            _table = table;
        }

        public ContentPurposeIndexState State { get; }
        public ContentPurposeIndexFailure Failure { get; }
        /// <summary>診断用の説明。分岐には <see cref="State"/> と <see cref="Failure"/> を使う。</summary>
        public string Detail { get; }
        /// <summary>型の規則を通った索引の版。欄が無い・検証に落ちた時は null。</summary>
        public int? Version { get; }
        /// <summary>版 1 の索引の entry（manifest の生の値の写し）。それ以外の状態では空。</summary>
        public IReadOnlyList<ContentTransportIndexEntry> Entries { get; }

        public static ContentPurposeIndex Read(ContentPurposeCandidate candidate)
        {
            if (candidate == null) throw new ArgumentNullException(nameof(candidate));
            return Read(candidate.ManifestBytes, candidate.ExpectedManifestSha256);
        }

        // 判定の順を固定する: (a) 上限と digest、(b) 欄の有無、(c) 型の規則（版に関係なく）、(d) 版と、版 1 の値。
        // 順を入れ替えると、型の壊れた版 0 を「索引なし」と読む、などの別の答えになる。
        public static ContentPurposeIndex Read(byte[] manifestBytes, string expectedManifestSha256)
        {
            if (manifestBytes == null) throw new ArgumentNullException(nameof(manifestBytes));
            if (expectedManifestSha256 == null) throw new ArgumentNullException(nameof(expectedManifestSha256));
            if (manifestBytes.Length > ContentManifestValidation.MaxManifestBytes)
                return Invalid(ContentPurposeIndexFailure.TooLarge, "Manifest exceeds its byte limit.", null);
            if (!string.Equals(ContentDeliveryFiles.Sha256(manifestBytes), expectedManifestSha256, StringComparison.Ordinal))
                return Invalid(ContentPurposeIndexFailure.DigestMismatch, "Manifest digest does not match the expected digest.", null);

            string json;
            try { json = new UTF8Encoding(false, true).GetString(manifestBytes); }
            catch (DecoderFallbackException) { return Invalid(ContentPurposeIndexFailure.InvalidManifest, "Manifest is not UTF-8.", null); }

            ContentManifestValidation.RawIndex? raw;
            try { raw = ContentManifestValidation.ReadIndex(json); }
            catch (ContentDeliveryException ex)
            {
                // 文書全体が JSON の object でないのか、索引の型の失敗なのかを分ける。どちらも検証の失敗で、別の解釈に落とさない。
                return IsJsonObject(json)
                    ? Invalid(ContentPurposeIndexFailure.InvalidIndex, ex.Message, null)
                    : Invalid(ContentPurposeIndexFailure.InvalidManifest, ex.Message, null);
            }
            if (raw == null)
                return new ContentPurposeIndex(ContentPurposeIndexState.NoIndex, ContentPurposeIndexFailure.None,
                    "Manifest has no index.", null, Array.Empty<ContentTransportIndexEntry>(), null);
            if (raw.Version == 0)
                // 版 0 の entries の値は見ない（型の規則は上で通っている）。
                return new ContentPurposeIndex(ContentPurposeIndexState.NoIndex, ContentPurposeIndexFailure.None,
                    "Manifest index is version 0 (not recorded).", 0, Array.Empty<ContentTransportIndexEntry>(), null);
            if (raw.Version < 0)
                return Invalid(ContentPurposeIndexFailure.InvalidIndex, "Manifest index version is negative.", raw.Version);
            if (raw.Version > 1)
                return new ContentPurposeIndex(ContentPurposeIndexState.UnsupportedIndex, ContentPurposeIndexFailure.None,
                    "Manifest index version " + raw.Version + " is not supported.", raw.Version,
                    Array.Empty<ContentTransportIndexEntry>(), null);

            var table = new ContentEntryTable<bool>();
            var entries = new List<ContentTransportIndexEntry>(raw.Entries.Count);
            foreach (var entry in raw.Entries)
            {
                if (entry.LogicalKey == null || entry.Kind == null || entry.Representation == null)
                    return Invalid(ContentPurposeIndexFailure.InvalidIndex, "Manifest index entry has a missing field.", 1);
                // 登録した directory の root と同じ値の規則: logical key は空白だけでなく、表現は空（tag の無い root）か空白だけでない。
                if (string.IsNullOrWhiteSpace(entry.LogicalKey)
                    || (entry.Representation.Length != 0 && string.IsNullOrWhiteSpace(entry.Representation)))
                    return Invalid(ContentPurposeIndexFailure.InvalidIndex, "Manifest index entry has a blank value.", 1);
                // 数値の名前（"0"）や大小の違う名前を Enum.Parse に通すと別の kind に読めるので、名前の完全一致だけを受ける。
                BuildContentKind kind;
                if (string.Equals(entry.Kind, nameof(BuildContentKind.Scene), StringComparison.Ordinal)) kind = BuildContentKind.Scene;
                else if (string.Equals(entry.Kind, nameof(BuildContentKind.Object), StringComparison.Ordinal)) kind = BuildContentKind.Object;
                else return Invalid(ContentPurposeIndexFailure.InvalidIndex, "Manifest index entry kind is unknown: " + entry.Kind, 1);
                // directory は同じ (logical key, 実効表現, kind) の entry が 2 つあると登録を拒む。索引だけが「含む」と答えないよう同じく拒む。
                if (!table.TryAdd(entry.LogicalKey, entry.Representation, kind, true))
                    return Invalid(ContentPurposeIndexFailure.InvalidIndex, "Manifest index contains duplicate entries.", 1);
                entries.Add(new ContentTransportIndexEntry
                    { logicalKey = entry.LogicalKey, kind = entry.Kind, representation = entry.Representation });
            }
            return new ContentPurposeIndex(ContentPurposeIndexState.Indexed, ContentPurposeIndexFailure.None,
                "Manifest index is readable.", 1, entries.AsReadOnly(), table);
        }

        /// <summary>この索引の build が要求を含むか。<see cref="ContentPurposeIndexState.Indexed"/> の時だけ答える。</summary>
        public bool Contains(ContentPurposeRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (_table == null)
                throw new InvalidOperationException("Only an indexed manifest can answer whether it contains a request (state: " + State + ").");
            return _table.TryResolve(request.LogicalKey, request.Representation, request.Kind, out _);
        }

        /// <summary>
        /// 呼び手が渡した順で候補を 1 つずつ評価し、最初に要求を含む候補を選ぶ。順を並べ替えず、revision や contentSet の大小を見ない。
        /// 索引なし・未対応の版の候補は理由を残して飛ばす。検証に落ちた候補（上限超過、digest の不一致、形の失敗）では選択を止め、
        /// 後ろに含む候補があっても成功にしない（整合性の失敗を黙って次の build で埋めない）。止まった後ろの候補は評価しない。
        /// </summary>
        public static ContentPurposeSelection Select(ContentPurposeRequest request, IReadOnlyList<ContentPurposeCandidate> candidates)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (candidates == null) throw new ArgumentNullException(nameof(candidates));
            for (var i = 0; i < candidates.Count; i++)
                if (candidates[i] == null) throw new ArgumentException("Candidate must not be null.", nameof(candidates));

            var results = new ContentPurposeCandidateResult[candidates.Count];
            var status = ContentPurposeSelectionStatus.NotFound;
            var selected = -1;
            for (var i = 0; i < candidates.Count; i++)
            {
                if (status != ContentPurposeSelectionStatus.NotFound)
                {
                    results[i] = new ContentPurposeCandidateResult(ContentPurposeVerdict.NotEvaluated, ContentPurposeIndexFailure.None,
                        "Not evaluated because the selection stopped at an earlier candidate.");
                    continue;
                }
                var index = Read(candidates[i]);
                switch (index.State)
                {
                    case ContentPurposeIndexState.Invalid:
                        results[i] = new ContentPurposeCandidateResult(ContentPurposeVerdict.ValidationFailed, index.Failure, index.Detail);
                        status = ContentPurposeSelectionStatus.ValidationFailed;
                        break;
                    case ContentPurposeIndexState.NoIndex:
                        results[i] = new ContentPurposeCandidateResult(ContentPurposeVerdict.NoIndex, ContentPurposeIndexFailure.None, index.Detail);
                        break;
                    case ContentPurposeIndexState.UnsupportedIndex:
                        results[i] = new ContentPurposeCandidateResult(ContentPurposeVerdict.UnsupportedIndex, ContentPurposeIndexFailure.None, index.Detail);
                        break;
                    default:
                        if (index.Contains(request))
                        {
                            results[i] = new ContentPurposeCandidateResult(ContentPurposeVerdict.Contains, ContentPurposeIndexFailure.None,
                                "The index contains the request.");
                            status = ContentPurposeSelectionStatus.Selected;
                            selected = i;
                        }
                        else
                            results[i] = new ContentPurposeCandidateResult(ContentPurposeVerdict.DoesNotContain, ContentPurposeIndexFailure.None,
                                "The index does not contain the request.");
                        break;
                }
            }
            return new ContentPurposeSelection(status, selected, Array.AsReadOnly(results));
        }

        private static ContentPurposeIndex Invalid(ContentPurposeIndexFailure failure, string detail, int? version)
            => new(ContentPurposeIndexState.Invalid, failure, detail, version, Array.Empty<ContentTransportIndexEntry>(), null);

        private static bool IsJsonObject(string json)
        {
            try { ContentManifestValidation.RequireObjectFields(json); return true; }
            catch (ContentDeliveryException) { return false; }
        }
    }

    public enum ContentPurposeVerdict
    {
        Contains,
        DoesNotContain,
        NoIndex,
        UnsupportedIndex,
        ValidationFailed,
        /// <summary>前の候補で選択が止まったので評価していない（「含む」「含まない」のどちらでもない）。</summary>
        NotEvaluated,
    }

    public enum ContentPurposeSelectionStatus
    {
        Selected,
        /// <summary>どの候補も要求を含まない（索引なし・未対応の候補しか無い場合を含む）。成功ではない。</summary>
        NotFound,
        /// <summary>検証に落ちた候補で止まった。成功ではない。</summary>
        ValidationFailed,
    }

    public sealed class ContentPurposeCandidateResult
    {
        internal ContentPurposeCandidateResult(ContentPurposeVerdict verdict, ContentPurposeIndexFailure failure, string detail)
        { Verdict = verdict; Failure = failure; Detail = detail; }

        public ContentPurposeVerdict Verdict { get; }
        public ContentPurposeIndexFailure Failure { get; }
        public string Detail { get; }
    }

    public sealed class ContentPurposeSelection
    {
        internal ContentPurposeSelection(ContentPurposeSelectionStatus status, int selectedIndex,
            IReadOnlyList<ContentPurposeCandidateResult> candidates)
        { Status = status; SelectedIndex = selectedIndex; Candidates = candidates; }

        public ContentPurposeSelectionStatus Status { get; }
        /// <summary>選んだ候補の、渡された列での位置。選ばなかった時は -1。</summary>
        public int SelectedIndex { get; }
        /// <summary>渡された列と同じ順の、候補ごとの理由。</summary>
        public IReadOnlyList<ContentPurposeCandidateResult> Candidates { get; }
        public bool IsSuccess => Status == ContentPurposeSelectionStatus.Selected;
    }
}
