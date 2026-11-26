#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OneStarMaker.Runtime.BuildContent.Distribution;
using UnityEditor;
using UnityEngine;

namespace SampleGame.DependOnAll.Editor.Build
{
    /// <summary>取得の操作の 1 回の結果（窓と eval が読む）。<see cref="ContentAcquireLauncher.LastResultJson"/> はこれの JSON。</summary>
    [Serializable]
    internal sealed class ContentAcquireOutcome
    {
        public int operation;
        /// <summary><see cref="ContentAcquisitionState"/> の名前か、始まらなかった時の <c>Rejected</c>、予期しない例外の <c>Failed</c>。</summary>
        public string state = "";
        public string reason = "";
        public string cacheRoot = "";
        public string contentSet = "";
        public string target = "";
        public string revision = "";
        public string manifestSha256 = "";
        public string targetOrigin = "";
        public string bytesOrigin = "";
        /// <summary><c>Pinned</c> か <c>Declared</c>（対象が決まらなければ空）。</summary>
        public string digestAuthority = "";
        public Attempt[] attempts = Array.Empty<Attempt>();
        public string[] removedInstalls = Array.Empty<string>();
        public string[] diagnostics = Array.Empty<string>();
        public bool selectionSaved;
        /// <summary>今の cache と選んだ base の記録で、Play（経路 Built）が使う revision。無ければ空。</summary>
        public string playRevision = "";
        public string playReason = "";
        public string[] warnings = Array.Empty<string>();
        public string startedUtc = "";
        public string finishedUtc = "";

        [Serializable]
        internal sealed class Attempt
        {
            public string stage = "";
            public string origin = "";
            public string outcome = "";
            public string reason = "";
            public bool readManifest;
        }
    }

    /// <summary>取得の操作の外の世界。単体 test が差し替える 5 つの口。</summary>
    internal sealed class ContentAcquirePorts
    {
        internal ContentAcquirePorts(Func<string?> editorBusyReason, Func<ContentAcquireSettings> loadSettings, Func<DateTime> utcNow,
            Action<ContentBaseSelectionEntry> saveSelection,
            Func<ContentAcquisitionRequest, IReadOnlyList<ContentAcquisitionOrigin>, ContentCacheStore, CancellationToken, Task<ContentAcquisitionResult>> acquire)
        {
            EditorBusyReason = editorBusyReason;
            LoadSettings = loadSettings;
            UtcNow = utcNow;
            SaveSelection = saveSelection;
            Acquire = acquire;
        }

        /// <summary>Play の間・Play への移行の間・compile の間なら理由。取得はこの時に始まらない（cache に触れない）。</summary>
        internal Func<string?> EditorBusyReason { get; }
        internal Func<ContentAcquireSettings> LoadSettings { get; }
        internal Func<DateTime> UtcNow { get; }
        internal Action<ContentBaseSelectionEntry> SaveSelection { get; }
        internal Func<ContentAcquisitionRequest, IReadOnlyList<ContentAcquisitionOrigin>, ContentCacheStore, CancellationToken, Task<ContentAcquisitionResult>> Acquire { get; }
    }

    /// <summary>取得の開始のたびに読む設定（Delivery 窓の cache root・budget、入口の RunPurpose、host の取得元の列）。</summary>
    internal sealed class ContentAcquireSettings
    {
        internal ContentAcquireSettings(string cacheRoot, long diskBudgetBytes, string runPurpose, IReadOnlyList<ContentAcquisitionOrigin> origins,
            string selectionPath, IReadOnlyList<string> notes)
        {
            CacheRoot = cacheRoot;
            DiskBudgetBytes = diskBudgetBytes;
            RunPurpose = runPurpose;
            Origins = origins;
            SelectionPath = selectionPath;
            Notes = notes;
        }

        internal string CacheRoot { get; }
        internal long DiskBudgetBytes { get; }
        internal string RunPurpose { get; }
        internal IReadOnlyList<ContentAcquisitionOrigin> Origins { get; }
        internal string SelectionPath { get; }
        /// <summary>設定を既定に戻した理由など、結果の Warning に載せる注意。</summary>
        internal IReadOnlyList<string> Notes { get; }
    }

    /// <summary>
    /// 取得の操作: 入口で選んである RunPurpose の宣言について、host の取得元の列から head の 1 件を取得・検証・install し、選んだ base を
    /// 記録し、Play（経路 Built）が使う revision を確かめて結果の文を作る。利用者が始める 1 回の取得という 1 つの変更理由と寿命に属する。
    /// <para>
    /// 状態: 走っている操作 1 つと直近の結果を static に持つ（Editor の process の間。domain reload で失われ、選んだ base の記録は残る）。
    /// Delivery 窓の Prepare と同じく main thread の上で非同期に走る。結果は窓の文と Console の 1 行で示し、dialog を出さない。
    /// eval からは <see cref="Start()"/> の操作の番号と、<see cref="LastResultJson"/> の <c>operation</c> で今回の操作を結び付けて読む。
    /// </para>
    /// </summary>
    public static class ContentAcquireLauncher
    {
        private const string LogPrefix = "[ContentAcquire] ";
        private const int RootSchemaVersion = 2;
        private static int _operation;
        private static CancellationTokenSource? _cancellation;
        private static ContentAcquireOutcome? _last;

        public static bool IsRunning => _cancellation != null;

        /// <summary>直近の結果の操作の番号（まだ無ければ 0）。</summary>
        public static int LastOperation => _last?.operation ?? 0;

        /// <summary>直近の結果の JSON（まだ無ければ空）。</summary>
        public static string LastResultJson => _last == null ? string.Empty : JsonUtility.ToJson(_last, true);

        internal static ContentAcquireOutcome? LastResult => _last;

        /// <summary>取得を始め、この Editor の process の中で 1 ずつ増える操作の番号を返す。拒否した時も番号と理由を直近の結果に残す。</summary>
        public static int Start() => Start(DefaultPorts());

        public static void Cancel() => _cancellation?.Cancel();

        internal static int Start(ContentAcquirePorts ports)
        {
            var operation = ++_operation;
            var rejection = IsRunning ? "Another acquisition is running." : ports.EditorBusyReason();
            if (rejection != null)
            {
                _last = new ContentAcquireOutcome
                {
                    operation = operation, state = "Rejected", reason = rejection,
                    startedUtc = ports.UtcNow().ToString("O"), finishedUtc = ports.UtcNow().ToString("O"),
                };
                Debug.Log(LogPrefix + "#" + operation + " end state=Rejected " + rejection);
                return operation;
            }
            var cancellation = new CancellationTokenSource();
            _cancellation = cancellation;
            Running = RunAndStore(ports, operation, cancellation);
            return operation;
        }

        /// <summary>直近に始めた操作の task（例外を外へ出さない）。単体 test が終わりを待つための口。</summary>
        internal static Task Running { get; private set; } = Task.CompletedTask;

        private static async Task RunAndStore(ContentAcquirePorts ports, int operation, CancellationTokenSource cancellation)
        {
            ContentAcquireOutcome outcome;
            try { outcome = await RunAsync(ports, operation, cancellation.Token); }
            catch (Exception ex)
            {
                outcome = new ContentAcquireOutcome { operation = operation, state = "Failed", reason = ex.Message, finishedUtc = ports.UtcNow().ToString("O") };
                Debug.LogException(ex);
            }
            // 直近の結果を置いてから「走っていない」にする（読み手は IsRunning が偽で番号が今回なら終わりと読む）。
            _last = outcome;
            _cancellation = null;
            cancellation.Dispose();
            Debug.Log(LogPrefix + "#" + operation + " end state=" + outcome.state + " revision=" + outcome.revision + " play=" + outcome.playRevision);
        }

        internal static async Task<ContentAcquireOutcome> RunAsync(ContentAcquirePorts ports, int operation, CancellationToken token)
        {
            var settings = ports.LoadSettings();
            var declaration = SampleGameBuildPurposes.Find(settings.RunPurpose);
            var outcome = new ContentAcquireOutcome
            {
                operation = operation, cacheRoot = settings.CacheRoot, contentSet = declaration.Name, target = VerifyPlayLauncher.Target,
                startedUtc = ports.UtcNow().ToString("O"),
            };
            var warnings = new List<string>(settings.Notes);
            Debug.Log(LogPrefix + "#" + operation + " start " + declaration.Name + " cache=" + settings.CacheRoot);
            var request = new ContentAcquisitionRequest(declaration.Name, VerifyPlayLauncher.Target, Application.unityVersion, RootSchemaVersion,
                settings.DiskBudgetBytes);
            var result = await ports.Acquire(request, settings.Origins, new ContentCacheStore(settings.CacheRoot), token);
            Fill(outcome, result);
            if (result.RemovedInstalls.Count != 0)
                warnings.Add("Installs observed to disappear from the cache during this acquisition (the install's capacity check can remove "
                             + "old installs; other operations may also have removed them): " + string.Join(", ", result.RemovedInstalls));

            ContentBaseSelectionRead selection;
            if (result.State is ContentAcquisitionState.Acquired or ContentAcquisitionState.AlreadyInstalled)
            {
                var entry = new ContentBaseSelectionEntry
                {
                    cacheRoot = ContentBaseSelection.NormalizeCacheRoot(settings.CacheRoot), contentSet = declaration.Name,
                    target = VerifyPlayLauncher.Target, revision = result.Revision!, manifestSha256 = result.ManifestSha256!,
                    targetOrigin = result.TargetOriginId ?? "", bytesOrigin = result.BytesOriginId ?? "",
                    digestAuthority = result.DigestAuthority?.ToString() ?? "", selectedUtc = ports.UtcNow().ToUniversalTime().ToString("O"),
                };
                try
                {
                    ports.SaveSelection(entry);
                    outcome.selectionSaved = true;
                    selection = new ContentBaseSelectionRead(entry, string.Empty);
                }
                catch (Exception ex)
                {
                    warnings.Add("The acquired base could not be recorded (" + ex.Message + "); Play orders the content by install time.");
                    selection = ContentBaseSelection.Read(settings.SelectionPath, settings.CacheRoot, declaration.Name, VerifyPlayLauncher.Target);
                }
            }
            else selection = ContentBaseSelection.Read(settings.SelectionPath, settings.CacheRoot, declaration.Name, VerifyPlayLauncher.Target);

            // Play の入口と同じ選択を走らせて、Play が使う revision を取得の成否と分けて書く。
            var play = PlayEntryCandidates.Select(settings.CacheRoot, declaration, VerifyPlayLauncher.Target, SampleGameBuildPurposes.FirstSceneOf, selection);
            outcome.playRevision = play.Candidate?.Revision ?? "";
            outcome.playReason = PlayReason(play, selection.Entry);
            warnings.AddRange(play.Warnings);
            if (result.State is ContentAcquisitionState.Acquired or ContentAcquisitionState.AlreadyInstalled && outcome.playRevision != result.Revision)
                warnings.Add("Play will not use the acquired " + result.Revision + ": " + (play.Candidate == null ? play.Failure : "it uses " + outcome.playRevision) + ".");
            outcome.warnings = warnings.ToArray();
            outcome.finishedUtc = ports.UtcNow().ToString("O");
            foreach (var warning in outcome.warnings) Debug.LogWarning(LogPrefix + "#" + operation + " " + warning);
            return outcome;
        }

        private static void Fill(ContentAcquireOutcome outcome, ContentAcquisitionResult result)
        {
            outcome.state = result.State.ToString();
            outcome.reason = result.Reason;
            outcome.revision = result.Revision ?? "";
            outcome.manifestSha256 = result.ManifestSha256 ?? "";
            outcome.targetOrigin = result.TargetOriginId ?? "";
            outcome.bytesOrigin = result.BytesOriginId ?? "";
            outcome.digestAuthority = result.DigestAuthority?.ToString() ?? "";
            outcome.attempts = result.Attempts.Select(x => new ContentAcquireOutcome.Attempt
            {
                stage = x.Stage.ToString(), origin = x.OriginId, outcome = x.Outcome, reason = x.Reason, readManifest = x.ReadManifest,
            }).ToArray();
            outcome.removedInstalls = result.RemovedInstalls.ToArray();
            outcome.diagnostics = result.Diagnostics.ToArray();
        }

        private static string PlayReason(PlayEntryCandidateSelection play, ContentBaseSelectionEntry? selected)
        {
            if (play.Candidate == null)
                return play.Failure.Length == 0 ? "No content of this declaration is installed." : "No installed content passes verification: " + play.Failure;
            if (selected != null && play.Candidate.Revision == selected.revision)
                return "It is the acquired base (selected " + selected.selectedUtc + "). A revision newly installed after that time comes first; "
                       + "re-using an already installed revision (Delivery window, Verify Play) does not count. The Play Entry warning "
                       + "'using the latest' means the first in this order.";
            return "It is first in the install-time order (a revision newly installed after the acquired base comes first).";
        }

        /// <summary>結果を窓に出す短い文。</summary>
        internal static string Describe(ContentAcquireOutcome outcome)
        {
            var lines = new List<string> { "#" + outcome.operation + " " + outcome.state + (outcome.reason.Length == 0 ? "" : ": " + outcome.reason) };
            if (outcome.revision.Length != 0)
                lines.Add("Target " + outcome.revision + " (" + outcome.digestAuthority + " digest, chosen by " + Or(outcome.targetOrigin, "a pin")
                          + "; bytes from " + Or(outcome.bytesOrigin, "none (already installed)") + ")");
            lines.AddRange(outcome.attempts.Select(x => "  " + x.stage + " " + x.origin + ": " + x.outcome + (x.reason.Length == 0 ? "" : " — " + x.reason)));
            if (outcome.state != "Rejected")
                lines.Add("Play (Built) uses " + Or(outcome.playRevision, "nothing") + ". " + outcome.playReason);
            lines.AddRange(outcome.warnings.Select(x => "Warning: " + x));
            lines.AddRange(outcome.diagnostics.Select(x => "Diagnostic: " + x));
            return string.Join("\n", lines);
        }

        private static string Or(string value, string fallback) => value.Length == 0 ? fallback : value;

        private static ContentAcquirePorts DefaultPorts() => new(
            () => EditorApplication.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode ? "Stop Play first."
                : EditorApplication.isCompiling || EditorApplication.isUpdating ? "Wait until compiling and asset import finish." : null,
            LoadSettings,
            () => DateTime.UtcNow,
            entry => ContentBaseSelection.Write(ContentBaseSelection.DefaultPath, entry),
            ContentAcquisition.AcquireAsync);

        private static ContentAcquireSettings LoadSettings()
        {
            var delivery = ContentDeliveryWindow.LoadSettings();
            var (root, note) = ContentOriginSettings.ResolveRoot(ContentOriginSettings.SettingsPath, ContentOriginSettings.DefaultRoot);
            return new ContentAcquireSettings(VerifyPlayLauncher.ResolveCacheRoot(delivery.cacheRoot), delivery.budget, PlayEntrySettings.RunPurpose,
                ContentOriginSettings.Origins(root), ContentBaseSelection.DefaultPath, note.Length == 0 ? Array.Empty<string>() : new[] { note });
        }
    }
}
