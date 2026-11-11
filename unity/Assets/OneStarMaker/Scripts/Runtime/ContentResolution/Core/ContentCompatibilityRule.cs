#nullable enable

using System;
using System.Collections.Generic;

namespace OneStarMaker.Runtime.ContentResolution
{
    /// <summary>
    /// <see cref="IContentCompatibilityRule"/> の製品実装。Play 前に固定した互換の基準（<see cref="ContentCompatibilityEnvelope"/>）と
    /// 要求の expected identity だけで、候補を採ってよいかを判定する。ホストの宣言は知らず、ホスト固有の比較は
    /// ホストが登録した述語として、汎用の判定を通った directory の候補にだけ追加の条件（AND）で加わる。
    /// </summary>
    /// <remarks>
    /// 判定順（理由はこの順で最初に当たった 1 つ）:
    /// 1. 由来と受け入れ根拠の組が ContentDirectory + VerifiedDigest か EditorSnapshot + OpenSceneSnapshot であること。
    ///    resolver も同じ表を先に見るが、規則を単独で呼ぶ利用者に表の外の組を採らせないため、ここでも見る。
    ///    表は resolver の private な表の複製で、由来を足すときは両方を直す（offline テストが一致を確かめる）。
    /// 2. 要求に expected identity があれば、由来に関係なく全欄一致すること（resolver の pin と同じ答え）。
    /// 3. EditorSnapshot はここで採る。受け入れ理由が digest ではないので identity・engine・schema を要求せず、
    ///    利用者が開いている Scene を最優先にする方針をホストの述語で崩さないため、述語も当てない。
    /// 4. ContentDirectory は identity の完全さ、content set、revision（基準が固定した時だけ）、target、engine、schema を比べる。
    ///    文字列は ordinal の完全一致で、正規化・大文字小文字の同一視をしない。事実が欠けた・形が壊れた候補は採らない。
    /// 5. ホストの述語を登録順に評価し、最初に採らないと答えた述語で止める（後の述語は呼ばない）。
    /// 6. 全て通れば採る。
    /// 規則の不採用は false を返すだけで例外にしない。例外にすると resolver が fault として探索を止め、下位の互換な候補へ進めないためである。
    /// 一方、ホストの述語が投げた例外は捕まえず包み直さずにそのまま伝える。握りつぶすと壊れた述語で全候補が静かに落ち、
    /// 包み直すと取消（取消済みの token の OperationCanceledException）が fault に化けるためである。
    /// 状態を持たず、述語が決定的なら同じ入力に同じ結果を返す。
    /// </remarks>
    public sealed class ContentCompatibilityRule : IContentCompatibilityRule
    {
        private const string ReasonPrefix = "compat:";
        private const int MaxSchemaDigits = 9;

        private readonly ContentCompatibilityEnvelope _envelope;
        private readonly IContentCompatibilityRule[] _hostPredicates;

        /// <param name="envelope">互換の基準。null は拒む。</param>
        /// <param name="hostPredicates">
        /// 任意。ホスト固有の比較の述語を評価する順に並べたもの。null の要素は拒む。複製して保持するので、渡した一覧を後から変えても影響しない。
        /// </param>
        public ContentCompatibilityRule(ContentCompatibilityEnvelope envelope, IEnumerable<IContentCompatibilityRule>? hostPredicates = null)
        {
            _envelope = envelope ?? throw new ArgumentNullException(nameof(envelope));

            var copy = new List<IContentCompatibilityRule>();
            if (hostPredicates != null)
            {
                foreach (var predicate in hostPredicates)
                {
                    if (predicate == null)
                    {
                        throw new ArgumentException("Host predicates must not contain null.", nameof(hostPredicates));
                    }

                    copy.Add(predicate);
                }
            }

            _hostPredicates = copy.ToArray();
        }

        /// <inheritdoc />
        /// <remarks><see cref="Evaluate"/> が <see cref="ContentCompatibilityMismatch.Compatible"/> を返したときだけ true。</remarks>
        public bool Accepts(ContentRequest request, ContentCandidate candidate, out string reason)
        {
            return Evaluate(request, candidate, out reason) == ContentCompatibilityMismatch.Compatible;
        }

        /// <summary>
        /// 候補の互換性を判定し、種別と理由（<c>compat:&lt;種別&gt;:</c> で始まる）を返す。
        /// </summary>
        /// <remarks>
        /// 互換性だけの部分判定である。要求の logical key・kind・表現と候補の一致、probe した供給元と候補の一致は見ない
        /// （resolver が規則の前に見る）。resolver を通さずにこの結果だけで候補を確定する呼び手は、それらの一致を別に保証する。
        /// 修復中の pin（identity 付きの Rejected から resolver が作るもの）も resolver だけが知り、ここでは比べない。
        /// ホストの述語の例外はそのまま伝わる。
        /// </remarks>
        public ContentCompatibilityMismatch Evaluate(ContentRequest request, ContentCandidate candidate, out string reason)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (candidate == null)
            {
                throw new ArgumentNullException(nameof(candidate));
            }

            var generic = EvaluateGeneric(request, candidate, out reason);
            if (generic != ContentCompatibilityMismatch.Compatible || candidate.SourceKind != ContentSourceKind.ContentDirectory)
            {
                return generic;
            }

            return EvaluateHostPredicates(request, candidate, out reason);
        }

        /// <summary>汎用の判定（判定順の 1〜4）。ホストの述語には触れない。</summary>
        private ContentCompatibilityMismatch EvaluateGeneric(ContentRequest request, ContentCandidate candidate, out string reason)
        {
            var isDirectory = candidate.SourceKind == ContentSourceKind.ContentDirectory
                              && candidate.AcceptanceBasis == ContentAcceptanceBasis.VerifiedDigest;
            var isSnapshot = candidate.SourceKind == ContentSourceKind.EditorSnapshot
                             && candidate.AcceptanceBasis == ContentAcceptanceBasis.OpenSceneSnapshot;

            // 既定の分岐は「採らない」。由来や根拠の値が将来足されても、ここで判定できない組は素通りさせない。
            if (!isDirectory && !isSnapshot)
            {
                return Reject(ContentCompatibilityMismatch.UnsupportedSource, $"source {candidate.SourceKind} with basis {candidate.AcceptanceBasis} is not judged by this rule.", out reason);
            }

            if (request.ExpectedIdentity.HasValue && !candidate.Identity.Equals(request.ExpectedIdentity.Value))
            {
                return Reject(ContentCompatibilityMismatch.ExpectedIdentityMismatch, $"identity {candidate.Identity} does not equal the expected {request.ExpectedIdentity.Value}.", out reason);
            }

            if (isSnapshot)
            {
                // この理由に digest の検証を主張する語を入れない（snapshot の受け入れ理由は digest ではない）。
                reason = ReasonPrefix + nameof(ContentCompatibilityMismatch.Compatible) + ":" + nameof(ContentSourceKind.EditorSnapshot);
                return ContentCompatibilityMismatch.Compatible;
            }

            var identity = candidate.Identity;
            if (!identity.IsComplete)
            {
                return Reject(ContentCompatibilityMismatch.IncompleteIdentity, $"identity {identity} has an empty field.", out reason);
            }

            if (!string.Equals(identity.ContentSet, _envelope.ContentSet, StringComparison.Ordinal))
            {
                return Reject(ContentCompatibilityMismatch.ContentSetMismatch, $"content set '{identity.ContentSet}' is not '{_envelope.ContentSet}'.", out reason);
            }

            if (_envelope.Revision != null && !string.Equals(identity.Revision, _envelope.Revision, StringComparison.Ordinal))
            {
                return Reject(ContentCompatibilityMismatch.RevisionMismatch, $"revision '{identity.Revision}' is not the fixed '{_envelope.Revision}'.", out reason);
            }

            if (!string.Equals(identity.Target, _envelope.Target, StringComparison.Ordinal))
            {
                return Reject(ContentCompatibilityMismatch.TargetMismatch, $"target '{identity.Target}' is not '{_envelope.Target}'.", out reason);
            }

            // 欠けた事実を「比べられないので一致」と扱わない。CopyFacts は null の値を空文字にするので、null もここで落ちる。
            if (!candidate.Facts.TryGetValue(ContentCompatibilityFactKeys.EngineVersion, out var engine) || string.IsNullOrWhiteSpace(engine))
            {
                return Reject(ContentCompatibilityMismatch.EngineVersionMissing, $"fact '{ContentCompatibilityFactKeys.EngineVersion}' is missing or blank.", out reason);
            }

            if (!string.Equals(engine, _envelope.EngineVersion, StringComparison.Ordinal))
            {
                return Reject(ContentCompatibilityMismatch.EngineVersionMismatch, $"engine version '{engine}' is not '{_envelope.EngineVersion}'.", out reason);
            }

            if (!candidate.Facts.TryGetValue(ContentCompatibilityFactKeys.RootSchemaVersion, out var schemaText) || string.IsNullOrWhiteSpace(schemaText))
            {
                return Reject(ContentCompatibilityMismatch.SchemaVersionMissing, $"fact '{ContentCompatibilityFactKeys.RootSchemaVersion}' is missing or blank.", out reason);
            }

            if (!TryParseSchemaVersion(schemaText, out var schema))
            {
                return Reject(ContentCompatibilityMismatch.SchemaVersionMalformed, $"root schema version '{schemaText}' is not a canonical decimal.", out reason);
            }

            if (schema != _envelope.RootSchemaVersion)
            {
                return Reject(ContentCompatibilityMismatch.SchemaVersionMismatch, $"root schema version {schema} is not {_envelope.RootSchemaVersion}.", out reason);
            }

            reason = string.Empty;
            return ContentCompatibilityMismatch.Compatible;
        }

        /// <summary>述語の合成（判定順の 5〜6）。汎用の判定を通った directory の候補だけがここに来る。</summary>
        private ContentCompatibilityMismatch EvaluateHostPredicates(ContentRequest request, ContentCandidate candidate, out string reason)
        {
            for (var i = 0; i < _hostPredicates.Length; i++)
            {
                // 例外は捕まえない（クラスの remarks を参照）。
                if (!_hostPredicates[i].Accepts(request, candidate, out var predicateReason))
                {
                    return Reject(ContentCompatibilityMismatch.HostPredicateRejected, $"host predicate {i} rejected: {predicateReason}", out reason);
                }
            }

            // 述語の理由は連結しない。採用理由は由来だけを示し、述語の文言で受け入れ理由を偽らせない。
            reason = ReasonPrefix + nameof(ContentCompatibilityMismatch.Compatible) + ":" + nameof(ContentSourceKind.ContentDirectory);
            return ContentCompatibilityMismatch.Compatible;
        }

        private static ContentCompatibilityMismatch Reject(ContentCompatibilityMismatch mismatch, string detail, out string reason)
        {
            reason = ReasonPrefix + mismatch + ":" + detail;
            return mismatch;
        }

        /// <summary>
        /// root schema の version の正規表記（ASCII の 1〜9 で始まり ASCII の数字だけ、9 桁以内）だけを受け付ける。
        /// int.TryParse は culture・符号・空白・先頭の 0・全角数字を許しうるので使わない。
        /// 9 桁以内なので int に収まり、互換の基準の範囲（1〜999,999,999）と一致する。
        /// </summary>
        private static bool TryParseSchemaVersion(string text, out int value)
        {
            value = 0;
            if (text.Length == 0 || text.Length > MaxSchemaDigits || text[0] < '1' || text[0] > '9')
            {
                return false;
            }

            var result = 0;
            foreach (var c in text)
            {
                if (c < '0' || c > '9')
                {
                    return false;
                }

                result = result * 10 + (c - '0');
            }

            value = result;
            return true;
        }
    }
}
