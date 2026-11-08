#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace OneStarMaker.Runtime.ContentResolution
{
    /// <summary>
    /// 要求・policy・probe の回答から候補を 1 つ採用する、状態を持たない orchestration。
    /// I/O・時間・乱数・外部状態を持たず、同じ要求・同じ回答・同じ規則なら同じ結果を返す。
    /// 候補を採用するだけで、bytes の取得・install・Build はしない。
    /// </summary>
    /// <remarks>
    /// 判定の順序（各 probe）:
    /// 1. 要求の不変: 候補の provider id / 由来が probe した provider と一致し、logical key・kind・表現が要求と一致し、
    ///    pin があれば identity が pin と全欄一致すること。
    /// 2. 受け入れ根拠: 由来ごとの固定表（ContentDirectory → VerifiedDigest、EditorSnapshot → OpenSceneSnapshot、
    ///    Addressables → 定めない＝採用しない）に合うこと。identity 付きの Rejected の後は VerifiedDigest だけ。
    /// 3. 互換の規則。
    /// 探索を止めるのは、採用・identity の無い Rejected・pin の食い違い・fault・取消の 5 つだけで、
    /// それ以外の不採用は記録して次の供給元へ進む。修復が供給元の並び順に依存しないよう、
    /// pin と違う候補も止めずに次へ進む（別 revision を黙って採ることは pin の比較が防ぐ）。
    /// 継続は呼び手の context で行う（ConfigureAwait(false) を使わない。main thread を前提とする adapter のため）。
    /// </remarks>
    public sealed class ContentResolver
    {
        public async ValueTask<ContentResolutionResult> ResolveAsync(
            ContentRequest request,
            ResolutionPolicy policy,
            CancellationToken cancellationToken)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (policy == null)
            {
                throw new ArgumentNullException(nameof(policy));
            }

            var state = new SearchState(request.ExpectedIdentity);
            foreach (var provider in policy.Providers)
            {
                // 前の probe が戻った時点で取消済みなら、次の供給元を呼ばない。
                cancellationToken.ThrowIfCancellationRequested();

                var providerId = provider.Id;
                var providerSource = provider.SourceKind;
                ContentAvailability answer;
                try
                {
                    answer = await provider.FindAsync(request, cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    // token が取消されていない OperationCanceledException も、ここで他の例外と同じ fault になる。
                    state.Add(providerId, providerSource, ProbeOutcome.Fault, null, "Provider threw: " + exception.GetType().Name + ": " + exception.Message);
                    return state.Fail();
                }

                // token を無視して取消後に完了した回答は採用しない。
                cancellationToken.ThrowIfCancellationRequested();

                switch (answer.Status)
                {
                    case ContentAvailabilityStatus.NotFound:
                        state.Add(providerId, providerSource, ProbeOutcome.NotFound, null, answer.Reason);
                        continue;

                    case ContentAvailabilityStatus.Unavailable:
                        state.Add(providerId, providerSource, ProbeOutcome.Unavailable, null, answer.Reason);
                        continue;

                    case ContentAvailabilityStatus.Rejected:
                        state.Add(providerId, providerSource, ProbeOutcome.Rejected, null, answer.Reason);
                        if (!answer.ExpectedIdentity.HasValue)
                        {
                            // 修復の基準が無い（権限拒否等）。下位の供給元へ黙って切り替えない。
                            state.StopForIntegrity();
                            return state.Fail();
                        }

                        if (!state.TryPinFromRejected(answer.ExpectedIdentity.Value))
                        {
                            return state.Fail();
                        }

                        continue;

                    case ContentAvailabilityStatus.Available when answer.Candidate != null:
                        break;

                    default:
                        // constructor を通らない default の回答（または未知の状態）。壊れた回答は fail-closed で止める。
                        state.Add(providerId, providerSource, ProbeOutcome.Fault, null, "Provider returned an invalid availability.");
                        return state.Fail();
                }

                var candidate = answer.Candidate!;
                var mismatch = CheckRequestInvariants(request, providerId, providerSource, candidate, state.Pin, out var mismatchReason);
                if (mismatch == ProbeOutcome.None)
                {
                    mismatch = CheckAcceptanceBasis(candidate, state.RepairOnly, out mismatchReason);
                }

                if (mismatch != ProbeOutcome.None)
                {
                    state.Add(providerId, providerSource, mismatch, candidate, mismatchReason);
                    continue;
                }

                bool accepted;
                string ruleReason;
                try
                {
                    accepted = policy.Rule.Accepts(request, candidate, out ruleReason);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    state.Add(providerId, providerSource, ProbeOutcome.Fault, candidate, "Rule threw: " + exception.GetType().Name + ": " + exception.Message);
                    return state.Fail();
                }

                if (!accepted)
                {
                    state.Add(providerId, providerSource, ProbeOutcome.RuleRejected, candidate, ruleReason);
                    continue;
                }

                state.Add(providerId, providerSource, ProbeOutcome.Adopted, candidate, ruleReason);
                return new ContentResolutionResult(candidate, ContentResolutionFailureCode.None, state.Probes, state.Pin);
            }

            return state.Fail();
        }

        private static ProbeOutcome CheckRequestInvariants(
            ContentRequest request,
            string providerId,
            ContentSourceKind providerSource,
            ContentCandidate candidate,
            ContentIdentity? pin,
            out string reason)
        {
            if (!string.Equals(candidate.ProviderId, providerId, StringComparison.Ordinal) || candidate.SourceKind != providerSource)
            {
                reason = $"Candidate claims provider '{candidate.ProviderId}' ({candidate.SourceKind}) but was returned by '{providerId}' ({providerSource}).";
                return ProbeOutcome.ProviderMismatch;
            }

            if (!string.Equals(candidate.LogicalKey, request.LogicalKey, StringComparison.Ordinal))
            {
                reason = $"Candidate logical key '{candidate.LogicalKey}' does not match the request.";
                return ProbeOutcome.KeyMismatch;
            }

            if (candidate.Kind != request.Kind)
            {
                reason = $"Candidate kind {candidate.Kind} does not match the requested {request.Kind}.";
                return ProbeOutcome.KindMismatch;
            }

            // 空の表現の候補は、要求が表現を指定していなくても採らない（content 入口が既存 index の fallback で表現を変えるため）。
            if (candidate.Representation.Length == 0
                || (request.Representation.Length != 0
                    && !string.Equals(candidate.Representation, request.Representation, StringComparison.Ordinal)))
            {
                reason = $"Candidate representation '{candidate.Representation}' does not match the requested '{request.Representation}'.";
                return ProbeOutcome.RepresentationMismatch;
            }

            if (pin.HasValue && !candidate.Identity.MatchesPin(pin.Value))
            {
                reason = $"Candidate identity {candidate.Identity} does not match the pin {pin.Value}.";
                return ProbeOutcome.PinMismatch;
            }

            reason = string.Empty;
            return ProbeOutcome.None;
        }

        private static ProbeOutcome CheckAcceptanceBasis(ContentCandidate candidate, bool repairOnly, out string reason)
        {
            var required = RequiredBasis(candidate.SourceKind);
            if (required == ContentAcceptanceBasis.None || candidate.AcceptanceBasis != required)
            {
                reason = $"Acceptance basis {candidate.AcceptanceBasis} is not accepted for source {candidate.SourceKind}.";
                return ProbeOutcome.NoAcceptanceBasis;
            }

            // identity 付きの Rejected の後は、同じ expected digest の正規コピーでの修復だけを認める。
            if (repairOnly && candidate.AcceptanceBasis != ContentAcceptanceBasis.VerifiedDigest)
            {
                reason = "Only a verified-digest copy may repair a rejected identity.";
                return ProbeOutcome.NoAcceptanceBasis;
            }

            reason = string.Empty;
            return ProbeOutcome.None;
        }

        /// <summary>由来ごとに要る受け入れ根拠の固定表。None は「この由来の候補は採用しない」。</summary>
        private static ContentAcceptanceBasis RequiredBasis(ContentSourceKind sourceKind)
        {
            switch (sourceKind)
            {
                case ContentSourceKind.ContentDirectory:
                    return ContentAcceptanceBasis.VerifiedDigest;
                case ContentSourceKind.EditorSnapshot:
                    return ContentAcceptanceBasis.OpenSceneSnapshot;
                default:
                    return ContentAcceptanceBasis.None;
            }
        }

        /// <summary>1 回の ResolveAsync の中だけで使う探索の状態。</summary>
        private sealed class SearchState
        {
            private readonly List<ProviderProbe> _probes = new List<ProviderProbe>();
            private bool _integrityStop;

            internal SearchState(ContentIdentity? initialPin)
            {
                Pin = initialPin;
            }

            internal ContentIdentity? Pin { get; private set; }

            /// <summary>identity 付きの Rejected を受けた後か（修復だけを認める状態）。</summary>
            internal bool RepairOnly { get; private set; }

            internal IReadOnlyList<ProviderProbe> Probes => _probes;

            internal void Add(string providerId, ContentSourceKind sourceKind, ProbeOutcome outcome, ContentCandidate? candidate, string reason)
            {
                _probes.Add(new ProviderProbe(providerId, sourceKind, outcome, candidate, reason));
            }

            internal void StopForIntegrity()
            {
                _integrityStop = true;
            }

            /// <summary>
            /// Rejected が与えた identity を pin にする。既存の pin と食い違えば探索を止める（pin は元の値のまま）。
            /// </summary>
            internal bool TryPinFromRejected(ContentIdentity expected)
            {
                if (Pin.HasValue && !Pin.Value.Equals(expected))
                {
                    _integrityStop = true;
                    return false;
                }

                Pin = expected;
                RepairOnly = true;
                return true;
            }

            /// <summary>誰も採用しなかったときの終わり方を、実績から優先順位で 1 つに決める。</summary>
            internal ContentResolutionResult Fail()
            {
                return new ContentResolutionResult(null, DecideFailureCode(), _probes, Pin);
            }

            private ContentResolutionFailureCode DecideFailureCode()
            {
                var pinMismatch = false;
                var providerFault = false;
                var incompatible = false;
                foreach (var probe in _probes)
                {
                    switch (probe.Outcome)
                    {
                        case ProbeOutcome.PinMismatch:
                            pinMismatch = true;
                            break;
                        case ProbeOutcome.KeyMismatch:
                        case ProbeOutcome.KindMismatch:
                        case ProbeOutcome.ProviderMismatch:
                        case ProbeOutcome.Fault:
                            providerFault = true;
                            break;
                        case ProbeOutcome.RuleRejected:
                        case ProbeOutcome.NoAcceptanceBasis:
                        case ProbeOutcome.RepresentationMismatch:
                            incompatible = true;
                            break;
                    }
                }

                // (1) 未充足の pin（pin 不一致、または identity 付き Rejected の後に未採用）・基準の無い Rejected・pin の食い違い。
                if (_integrityStop || pinMismatch || RepairOnly)
                {
                    return ContentResolutionFailureCode.IntegrityRejected;
                }

                if (providerFault)
                {
                    return ContentResolutionFailureCode.ProviderFault;
                }

                return incompatible ? ContentResolutionFailureCode.NoCompatibleCandidate : ContentResolutionFailureCode.NoCandidate;
            }
        }
    }
}
