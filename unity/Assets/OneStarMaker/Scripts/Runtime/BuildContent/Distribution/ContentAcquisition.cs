#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace OneStarMaker.Runtime.BuildContent.Distribution
{
    /// <summary>test だけが使う、取消の順を作る通知。</summary>
    internal sealed class ContentAcquisitionHooks
    {
        /// <summary>guard が installer へ EOF（0 byte の読み）を返す直前に、その相対 path で呼ばれる。通知の後に guard は token を見ない。</summary>
        internal Action<string>? BeforeEndOfFileReturned { get; set; }
    }

    /// <summary>
    /// 取得の調停: 取得元の列と pin から対象の組を 1 回だけ決め、到着点を見て、無ければ取得元を順に当たり、installer で install する
    /// （HANDOFF §2.4 の段と止まる規則。振り分けは <see cref="ContentAcquisitionRules"/>）。
    /// <para>
    /// 到着点（installed cache）へ書くのは installer だけで、調停は cache に書かない。調停が先に読んだ manifest は互換と identity の
    /// 判定と guard の照合にだけ使い、install の根拠にしない（installer は自分で読み直し、期待 digest で照合する）。
    /// 対象の組を決めた後に別の revision へは進まない。取得元の型や id で分岐しない。
    /// </para>
    /// </summary>
    public static class ContentAcquisition
    {
        public static Task<ContentAcquisitionResult> AcquireAsync(ContentAcquisitionRequest request,
            IReadOnlyList<ContentAcquisitionOrigin> origins, ContentCacheStore store, CancellationToken cancellationToken)
            => AcquireAsync(request, origins, store, cancellationToken, null);

        internal static async Task<ContentAcquisitionResult> AcquireAsync(ContentAcquisitionRequest request,
            IReadOnlyList<ContentAcquisitionOrigin> origins, ContentCacheStore store, CancellationToken cancellationToken,
            ContentAcquisitionHooks? hooks)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (origins == null || origins.Any(x => x == null)) throw new ArgumentNullException(nameof(origins));
            if (store == null) throw new ArgumentNullException(nameof(store));
            var run = new Run(request, origins, store, hooks);
            try { return await run.ExecuteAsync(cancellationToken); }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // installer は取消を受けて staging を片付け、lock を離してから投げている。ここでは待つものが無い。
                return run.Finish(ContentAcquisitionState.Canceled, "Canceled by the caller.");
            }
        }

        private sealed class Run
        {
            private readonly ContentAcquisitionRequest _request;
            private readonly IReadOnlyList<ContentAcquisitionOrigin> _origins;
            private readonly ContentCacheStore _store;
            private readonly ContentAcquisitionHooks? _hooks;
            private readonly List<ContentAcquisitionAttempt> _attempts = new();
            private readonly List<string> _diagnostics = new();
            private ContentCacheSnapshot? _before;
            private bool _installerCalled;
            private string? _revision;
            private string? _digest;
            private string? _targetOriginId;
            private ContentDigestAuthority? _authority;

            internal Run(ContentAcquisitionRequest request, IReadOnlyList<ContentAcquisitionOrigin> origins, ContentCacheStore store,
                ContentAcquisitionHooks? hooks)
            {
                _request = request;
                _origins = origins;
                _store = store;
                _hooks = hooks;
            }

            internal async Task<ContentAcquisitionResult> ExecuteAsync(CancellationToken token)
            {
                // ─── 段 1: 対象を決める ───
                var targetIndex = -1;
                ContentOriginEntry? targetEntry = null;
                if (_request.Pin != null)
                {
                    _revision = _request.Pin.Revision;
                    _digest = _request.Pin.ManifestSha256;
                    _authority = ContentDigestAuthority.Pinned;
                }
                else
                {
                    for (var i = 0; i < _origins.Count && targetEntry == null; i++)
                    {
                        token.ThrowIfCancellationRequested();
                        var origin = _origins[i];
                        if (origin.Trust == ContentOriginTrust.PinnedOnly)
                        {
                            Attempt(ContentAcquisitionStage.Target, origin, "PinRequired", "This origin is trusted only with a pin.");
                            continue;
                        }
                        var query = await ContentOriginGuard.FindAsync(origin.Origin, new ContentOriginQuery(_request.ContentSet, _request.Target), token);
                        var outcome = query.Outcome == ContentQueryOutcome.Found && query.Entry!.DeclaredManifestSha256 == null
                            ? ContentQueryOutcome.MissingDigest : query.Outcome;
                        if (ContentAcquisitionRules.ForQuery(outcome).Action != ContentAcquisitionAction.Proceed)
                        {
                            Attempt(ContentAcquisitionStage.Target, origin, outcome.ToString(),
                                outcome == ContentQueryOutcome.MissingDigest ? "The head declares no digest." : query.Reason);
                            continue;
                        }
                        targetIndex = i;
                        targetEntry = query.Entry;
                        _revision = targetEntry!.Revision;
                        _digest = targetEntry.DeclaredManifestSha256;
                        _targetOriginId = origin.Origin.Id;
                        _authority = ContentDigestAuthority.Declared;
                        Attempt(ContentAcquisitionStage.Target, origin, "Target", "Head names " + _revision + " (" + _digest + ").");
                    }
                    if (targetEntry == null)
                        return Finish(ContentAcquisitionState.NotFound, "No origin named a target for " + _request.ContentSet + "/" + _request.Target + ".");
                }

                // ─── 段 2: 到着点を見る ───
                token.ThrowIfCancellationRequested();
                var arrival = ObserveArrival(recordBefore: true);
                var decision = ContentAcquisitionRules.ForArrival(arrival.Observation);
                if (decision.Action == ContentAcquisitionAction.End) return Finish(decision.State, arrival.Reason, arrival.Install);

                // ─── 段 3: 取得元から取る ───
                var installRequest = new ContentInstallRequest(_digest!, _request.ContentSet, _revision!, _request.Target,
                    _request.UnityVersion, _request.RootSchemaVersion, _request.DiskBudgetBytes);
                var anyIntegrity = false;
                for (var i = 0; i < _origins.Count; i++)
                {
                    token.ThrowIfCancellationRequested();
                    var origin = _origins[i];
                    var entry = targetEntry;
                    // 対象を決めた取得元は照会し直さない（同じ答えの entry を使う）。
                    if (i != targetIndex)
                    {
                        var query = await ContentOriginGuard.FindAsync(origin.Origin,
                            new ContentOriginQuery(_request.ContentSet, _request.Target, _revision), token);
                        if (ContentAcquisitionRules.ForQuery(query.Outcome).Action != ContentAcquisitionAction.Proceed)
                        {
                            Attempt(ContentAcquisitionStage.Fetch, origin, query.Outcome.ToString(), query.Reason);
                            continue;
                        }
                        entry = query.Entry;
                    }

                    using var guard = new ContentOriginGuard(origin.Origin, entry!, _digest!, _hooks?.BeforeEndOfFileReturned);
                    var pre = await guard.PreReadManifestAsync(token);
                    var preOutcome = pre.Outcome;
                    var reason = pre.Reason;
                    if (preOutcome == ContentPreReadOutcome.Matched)
                    {
                        preOutcome = ValidateManifest(pre.Bytes!, installRequest, out reason, out var files);
                        if (files != null) guard.SetExpectedFiles(files);
                    }
                    decision = ContentAcquisitionRules.ForPreRead(preOutcome);
                    if (decision.Action != ContentAcquisitionAction.Proceed)
                    {
                        Attempt(ContentAcquisitionStage.Fetch, origin, preOutcome.ToString(), reason);
                        if (decision.Action == ContentAcquisitionAction.End) return Finish(decision.State, reason);
                        anyIntegrity |= decision.Kind == ContentOriginFailureKind.Integrity;
                        continue;
                    }

                    _installerCalled = true;
                    ContentInstallResult installed;
                    try { installed = await new ContentInstaller(_store).InstallAsync(installRequest, guard, token); }
                    catch (OperationCanceledException ex) when (token.IsCancellationRequested)
                    {
                        CollectStagingDiagnostics(ex);
                        Attempt(ContentAcquisitionStage.Fetch, origin, "Canceled", "Canceled while installing.", guard.ManifestRead);
                        throw;
                    }
                    catch (Exception ex)
                    {
                        CollectStagingDiagnostics(ex);
                        var code = (ex as ContentDeliveryException)?.Code;
                        var present = Directory.Exists(_store.RevisionRoot(_request.ContentSet, _revision!));
                        decision = ContentAcquisitionRules.ForInstallFailure(code, guard.SourceFailureObserved, guard.DeliveredMismatch, present);
                        var failure = (code?.ToString() ?? ex.GetType().Name) + ": " + ex.Message
                                      + (guard.DeliveredMismatch ? " (delivered bytes: " + guard.DeliveredMismatchReason + ")"
                                          : guard.SourceFailureObserved ? " (origin: " + guard.SourceFailureReason + ")" : "");
                        if (decision.Action == ContentAcquisitionAction.ReapplyArrival)
                        {
                            Attempt(ContentAcquisitionStage.Fetch, origin, "ArrivedMeanwhile", failure, guard.ManifestRead);
                            var again = ObserveArrival(recordBefore: false);
                            var final = ContentAcquisitionRules.ForArrival(again.Observation);
                            // 読み直しの間に消えた時は、帰属を確かめられないので到着点の側の失敗にする。
                            return final.Action == ContentAcquisitionAction.End
                                ? Finish(final.State, again.Reason, again.Install)
                                : Finish(ContentAcquisitionState.ArrivalFailure, "The target appeared and disappeared during the install: " + failure);
                        }
                        var kind = decision.Kind == ContentOriginFailureKind.Integrity ? "Integrity"
                            : decision.Kind == ContentOriginFailureKind.Unreachable ? "Unreachable" : "ArrivalFailure";
                        Attempt(ContentAcquisitionStage.Fetch, origin, kind, failure, guard.ManifestRead);
                        if (decision.Action == ContentAcquisitionAction.End) return Finish(decision.State, "The cache side failed: " + failure);
                        anyIntegrity |= decision.Kind == ContentOriginFailureKind.Integrity;
                        continue;
                    }
                    // rename が終われば取消は効かない。成功の後に token を見て取消へ変えない。
                    decision = ContentAcquisitionRules.ForInstallSuccess(installed.AlreadyInstalled);
                    Attempt(ContentAcquisitionStage.Fetch, origin, installed.AlreadyInstalled ? "AlreadyInstalled" : "Installed",
                        installed.RevisionRoot, guard.ManifestRead);
                    return Finish(decision.State, installed.AlreadyInstalled
                        ? "Another operation installed the target meanwhile; it was re-verified." : "Installed from " + origin.Origin.Id + ".",
                        installed, installed.AlreadyInstalled ? null : origin.Origin.Id);
                }
                decision = ContentAcquisitionRules.ForExhausted(anyIntegrity);
                return Finish(decision.State, anyIntegrity
                    ? "No origin delivered bytes matching the target digest."
                    : "No origin could deliver the target " + _revision + ".");
            }

            private readonly struct Arrival
            {
                internal Arrival(ContentArrivalObservation observation, string reason, ContentInstallResult? install)
                { Observation = observation; Reason = reason; Install = install; }
                internal ContentArrivalObservation Observation { get; }
                internal string Reason { get; }
                internal ContentInstallResult? Install { get; }
            }

            private Arrival ObserveArrival(bool recordBefore)
            {
                ContentCacheSnapshot snapshot;
                try { snapshot = _store.Inspect(); }
                catch (ContentDeliveryException ex)
                {
                    return new Arrival(ContentArrivalObservation.CacheUnreadable, "The content cache cannot be read: " + ex.Message, null);
                }
                if (recordBefore) _before = snapshot;
                var root = _store.RevisionRoot(_request.ContentSet, _revision!);
                // installer の再利用の経路と同じく path で見る（Windows では大小文字だけが違う directory も在ると読む）。
                if (!Directory.Exists(root)) return new Arrival(ContentArrivalObservation.Absent, string.Empty, null);
                var entry = snapshot.Entries.FirstOrDefault(x => SamePath(x.RevisionRoot, root));
                if (entry == null)
                    return new Arrival(ContentArrivalObservation.CorruptOrUnverified,
                        "The cache side failed: the installed " + _revision + " has an unreadable or corrupt receipt.", null);
                if (!string.Equals(entry.ManifestSha256, _digest, StringComparison.Ordinal))
                    return new Arrival(ContentArrivalObservation.DifferentDigest,
                        "The installed " + _revision + " has digest " + entry.ManifestSha256 + ", not the target " + _digest + ".", null);
                try
                {
                    var install = _store.ValidateInstalled(entry.RevisionRoot, _digest!, _request.ContentSet, _revision!, _request.Target);
                    return new Arrival(ContentArrivalObservation.SameDigestVerified, "The target is already installed and was re-verified.", install);
                }
                catch (ContentDeliveryException ex)
                {
                    return new Arrival(ContentArrivalObservation.CorruptOrUnverified,
                        "The cache side failed: the installed " + _revision + " failed verification: " + ex.Message, null);
                }
            }

            // installer の検査と同じ規則（ContentManifestValidation.Validate）を、digest を照合した bytes に対して先に走らせる。
            // digest が対象と同じなので、ここで落ちる manifest はどの取得元から来ても同じで、次の取得元へは進まない。
            private static ContentPreReadOutcome ValidateManifest(byte[] bytes, ContentInstallRequest request, out string reason,
                out IReadOnlyList<ContentTransportFile>? files)
            {
                files = null;
                try
                {
                    var json = new UTF8Encoding(false, true).GetString(bytes);
                    ContentManifestValidation.ValidateRequiredFields(json);
                    var dto = JsonUtility.FromJson<ContentTransportManifest>(json) ?? throw new FormatException();
                    var manifest = ContentManifestValidation.Validate(dto, request.ManifestSha256, request);
                    files = manifest.Files;
                    reason = string.Empty;
                    return ContentPreReadOutcome.Matched;
                }
                catch (ContentDeliveryException ex)
                {
                    reason = ex.Code + ": " + ex.Message;
                    return ex.Code switch
                    {
                        ContentDeliveryFailureCode.IdentityMismatch => ContentPreReadOutcome.IdentityMismatch,
                        ContentDeliveryFailureCode.TargetMismatch => ContentPreReadOutcome.TargetMismatch,
                        ContentDeliveryFailureCode.CompatibilityMismatch => ContentPreReadOutcome.Incompatible,
                        ContentDeliveryFailureCode.BudgetUnsatisfied => ContentPreReadOutcome.OverBudget,
                        _ => ContentPreReadOutcome.ManifestInvalid,
                    };
                }
                catch (Exception ex) when (ex is ArgumentException or FormatException)
                {
                    reason = "InvalidManifest: " + ex.Message;
                    return ContentPreReadOutcome.ManifestInvalid;
                }
            }

            private void Attempt(ContentAcquisitionStage stage, ContentAcquisitionOrigin origin, string outcome, string reason, bool readManifest = false)
                => _attempts.Add(new ContentAcquisitionAttempt(stage, origin.Origin.Id, outcome, reason, readManifest));

            private void CollectStagingDiagnostics(Exception ex)
            {
                if (ex.Data.Contains("ContentDeliveryStagingPath"))
                    _diagnostics.Add("Staging could not be removed: " + ex.Data["ContentDeliveryStagingPath"]
                                     + " (" + (ex.Data["ContentDeliveryCleanupFailure"] as Exception)?.Message + ")");
            }

            internal ContentAcquisitionResult Finish(ContentAcquisitionState state, string reason, ContentInstallResult? install = null,
                string? bytesOriginId = null)
            {
                var removed = new List<string>();
                if (_installerCalled && _before != null)
                {
                    try
                    {
                        var after = _store.Inspect();
                        removed.AddRange(_before.Entries
                            .Where(x => !after.Entries.Any(y => SamePath(x.RevisionRoot, y.RevisionRoot)))
                            .Select(x => x.ContentSet + "/" + x.Revision));
                    }
                    catch (ContentDeliveryException ex) { _diagnostics.Add("Removed installs could not be observed: " + ex.Message); }
                }
                return new ContentAcquisitionResult(state, reason, install, _revision, _digest, _targetOriginId, bytesOriginId, _authority,
                    _attempts.ToArray(), removed, _diagnostics.ToArray());
            }

            private static bool SamePath(string left, string right) =>
                string.Equals(Path.GetFullPath(left).TrimEnd(Path.DirectorySeparatorChar),
                    Path.GetFullPath(right).TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase);
        }
    }
}
