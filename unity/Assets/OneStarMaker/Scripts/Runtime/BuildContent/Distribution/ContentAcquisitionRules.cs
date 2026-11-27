#nullable enable

namespace OneStarMaker.Runtime.BuildContent.Distribution
{
    internal enum ContentAcquisitionAction
    {
        /// <summary>同じ取得元で次の段へ進む。</summary>
        Proceed,
        /// <summary>理由を残して次の取得元へ進む（同じ対象の組だけを探す）。</summary>
        NextOrigin,
        /// <summary><see cref="ContentAcquisitionDecision.State"/> で終わる。</summary>
        End,
        /// <summary>到着点に対象が現れた。段 2 の規則で終わり方を決め直す（取得元の不整合に数えない）。</summary>
        ReapplyArrival,
    }

    /// <summary>次の取得元へ進む時の理由の種類。取得元が尽きた時の終わり方（整合性の失敗か、見つからないか）を決める。</summary>
    internal enum ContentOriginFailureKind { None, Unanswered, Unreachable, Integrity }

    internal readonly struct ContentAcquisitionDecision
    {
        internal ContentAcquisitionDecision(ContentAcquisitionAction action, ContentAcquisitionState state, ContentOriginFailureKind kind)
        {
            Action = action;
            State = state;
            Kind = kind;
        }

        internal ContentAcquisitionAction Action { get; }
        /// <summary><see cref="ContentAcquisitionAction.End"/> の時だけ意味を持つ。</summary>
        internal ContentAcquisitionState State { get; }
        internal ContentOriginFailureKind Kind { get; }

        public override string ToString() => Action + "/" + State + "/" + Kind;
    }

    /// <summary>段 2 で到着点を見た結果。</summary>
    internal enum ContentArrivalObservation
    {
        /// <summary>cache を読めない（<c>Inspect</c> が Busy などで失敗）。</summary>
        CacheUnreadable,
        /// <summary>対象の revision の directory が無い（大小文字だけが違う directory も「在る」と読む）。</summary>
        Absent,
        /// <summary>在り、receipt の digest が対象と同じで、再検証が通った。</summary>
        SameDigestVerified,
        /// <summary>在り、receipt の digest が対象と違う。</summary>
        DifferentDigest,
        /// <summary>在るが receipt を読めない・壊れている、または digest は同じだが再検証が通らない。</summary>
        CorruptOrUnverified,
    }

    /// <summary>段 1・段 3 の照会の結果（guard が受け止めた後）。</summary>
    internal enum ContentQueryOutcome { Found, NotFound, Unavailable, Threw, MismatchedEntry, MissingDigest }

    /// <summary>段 3 で調停が installer より先に <c>transport.json</c> を読んだ結果。</summary>
    internal enum ContentPreReadOutcome
    {
        Matched,
        /// <summary>取得元の open・read が失敗した（証拠 (i)）。</summary>
        Unreachable,
        /// <summary>bytes の SHA-256 が対象の digest と違う（証拠 (ii)）。</summary>
        DigestMismatch,
        /// <summary>bytes が manifest の上限を超えた（証拠 (ii)）。</summary>
        TooLarge,
        ManifestInvalid, IdentityMismatch, TargetMismatch, Incompatible,
        /// <summary>対象の大きさが budget を超える（到着点の側）。</summary>
        OverBudget,
    }

    /// <summary>
    /// 取得の段と止まる規則の表（HANDOFF §2.4）。失敗の振り分けは installer の失敗 code では決めず、どの段で起きたかと、
    /// 失敗の証拠がどちらの側から来たかで決める: installer の同じ code は取得元の側からも到着点の側からも出るため
    /// （例: 壊れた <c>known-good.json</c> の読みも <c>InvalidManifest</c>、staging の異常も <c>IntegrityMismatch</c>）。
    /// 取得元の側と読む証拠は、guard が観測した取得元の open・read の失敗 (i) と、guard が渡した bytes そのものの食い違い (ii) だけ。
    /// </summary>
    internal static class ContentAcquisitionRules
    {
        private static ContentAcquisitionDecision End(ContentAcquisitionState state) => new(ContentAcquisitionAction.End, state, ContentOriginFailureKind.None);
        private static ContentAcquisitionDecision Next(ContentOriginFailureKind kind) => new(ContentAcquisitionAction.NextOrigin, default, kind);
        private static readonly ContentAcquisitionDecision ProceedDecision = new(ContentAcquisitionAction.Proceed, default, ContentOriginFailureKind.None);

        internal static ContentAcquisitionDecision ForArrival(ContentArrivalObservation observation) => observation switch
        {
            ContentArrivalObservation.Absent => ProceedDecision,
            ContentArrivalObservation.SameDigestVerified => End(ContentAcquisitionState.AlreadyInstalled),
            ContentArrivalObservation.DifferentDigest => End(ContentAcquisitionState.ArrivalDigestConflict),
            // 壊れた install を直す操作はこのスライスの外。cache の側の失敗として止める。
            _ => End(ContentAcquisitionState.ArrivalFailure),
        };

        /// <summary>段 1（対象を決める）と段 3 の照会。Found 以外は全て次の取得元へ進む（段 1 で対象が決まらなければ呼び手が NotFound で終える）。</summary>
        internal static ContentAcquisitionDecision ForQuery(ContentQueryOutcome outcome) =>
            outcome == ContentQueryOutcome.Found ? ProceedDecision : Next(ContentOriginFailureKind.Unanswered);

        internal static ContentAcquisitionDecision ForPreRead(ContentPreReadOutcome outcome) => outcome switch
        {
            ContentPreReadOutcome.Matched => ProceedDecision,
            ContentPreReadOutcome.Unreachable => Next(ContentOriginFailureKind.Unreachable),
            ContentPreReadOutcome.DigestMismatch or ContentPreReadOutcome.TooLarge => Next(ContentOriginFailureKind.Integrity),
            // digest が対象と同じ bytes は、どの取得元から来ても同じ。別の revision へも、次の取得元へも進まない。
            ContentPreReadOutcome.OverBudget => End(ContentAcquisitionState.ArrivalFailure),
            _ => End(ContentAcquisitionState.TargetUnusable),
        };

        /// <summary>
        /// installer が失敗を返した後。<paramref name="code"/> は記録のためだけに受け、振り分けには使わない（null は取得元の側の未知の例外）。
        /// 読み直しで対象が到着点に在れば、段 2 の後に別の操作が同じ revision を入れたので到着点の側として扱う（code に依らない）。
        /// </summary>
        internal static ContentAcquisitionDecision ForInstallFailure(ContentDeliveryFailureCode? code, bool sourceFailureObserved,
            bool deliveredBytesMismatch, bool targetNowPresent)
        {
            if (targetNowPresent) return new ContentAcquisitionDecision(ContentAcquisitionAction.ReapplyArrival, default, ContentOriginFailureKind.None);
            if (deliveredBytesMismatch) return Next(ContentOriginFailureKind.Integrity);
            if (sourceFailureObserved) return Next(ContentOriginFailureKind.Unreachable);
            return End(ContentAcquisitionState.ArrivalFailure);
        }

        /// <summary>installer の成功。再利用（段 2 の後に別の操作が同じ revision を入れた）は、自分が公開していないので install 済み。</summary>
        internal static ContentAcquisitionDecision ForInstallSuccess(bool reused) =>
            End(reused ? ContentAcquisitionState.AlreadyInstalled : ContentAcquisitionState.Acquired);

        internal static ContentAcquisitionDecision ForExhausted(bool anyIntegrityFailure) =>
            End(anyIntegrityFailure ? ContentAcquisitionState.IntegrityFailure : ContentAcquisitionState.NotFound);
    }
}
