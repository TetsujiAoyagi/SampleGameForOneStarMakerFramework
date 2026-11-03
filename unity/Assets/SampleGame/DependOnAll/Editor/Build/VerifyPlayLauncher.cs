#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using OneStarMaker.Build.Selection;
using OneStarMaker.Editor.Build.Content;
using OneStarMaker.Runtime.BuildContent.Distribution;
using OneStarMaker.Runtime.SceneSystem;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SampleGame.DependOnAll.Editor.Build
{
    internal enum VerifyPlayStage { Start, Produce, Install, BeforePlay, Bridge, EnterPlaymode }

    internal enum VerifyPlayOutcomeKind { PlayRequested, Rejected, Failed, Cancelled }

    internal sealed class VerifyPlayOutcome
    {
        private VerifyPlayOutcome(VerifyPlayOutcomeKind kind, VerifyPlayStage stage, string message, Exception? exception)
        { Kind = kind; Stage = stage; Message = message; Exception = exception; }

        internal VerifyPlayOutcomeKind Kind { get; }
        internal VerifyPlayStage Stage { get; }
        internal string Message { get; }
        internal Exception? Exception { get; }

        internal static VerifyPlayOutcome PlayRequested() => new(VerifyPlayOutcomeKind.PlayRequested, VerifyPlayStage.EnterPlaymode, "", null);
        internal static VerifyPlayOutcome Rejected(VerifyPlayStage stage, string reason) => new(VerifyPlayOutcomeKind.Rejected, stage, reason, null);
        internal static VerifyPlayOutcome Failed(VerifyPlayStage stage, Exception ex) => new(VerifyPlayOutcomeKind.Failed, stage, ex.Message, ex);
        internal static VerifyPlayOutcome Cancelled(VerifyPlayStage stage) => new(VerifyPlayOutcomeKind.Cancelled, stage, "Cancelled.", null);

        public override string ToString() => Kind + " at " + Stage + (Message.Length == 0 ? "" : ": " + Message);
    }

    /// <summary>published directory（その操作で publish したもの、または受け取ったもの）。manifest の宣言は手元の宣言と照合済み。</summary>
    internal sealed class PublishedContent
    {
        internal PublishedContent(string publishPath, string manifestSha256, string identity, string contentSet,
            string declarationName, string representation, IReadOnlyList<KeyValuePair<string, string>> runtimeDefaults)
        {
            PublishPath = publishPath; ManifestSha256 = manifestSha256; Identity = identity; ContentSet = contentSet;
            DeclarationName = declarationName; Representation = representation; RuntimeDefaults = runtimeDefaults;
        }
        internal string PublishPath { get; }
        internal string ManifestSha256 { get; }
        internal string Identity { get; }
        internal string ContentSet { get; }
        internal string DeclarationName { get; }
        internal string Representation { get; }
        internal IReadOnlyList<KeyValuePair<string, string>> RuntimeDefaults { get; }
    }

    internal sealed class InstalledContent
    {
        internal InstalledContent(string revisionRoot, string manifestSha256, string revision)
        { RevisionRoot = revisionRoot; ManifestSha256 = manifestSha256; Revision = revision; }
        internal string RevisionRoot { get; }
        internal string ManifestSha256 { get; }
        internal string Revision { get; }
    }

    /// <summary>検証 Play の段。<see cref="VerifyPlaySequence"/> は段の順序と停止だけを持ち、段の中身はここで差し替える。</summary>
    internal interface IVerifyPlayStages
    {
        /// <summary>副作用（build・publish・install・環境の変更）より前の検査。拒否なら理由。</summary>
        string? CheckBeforeStart();
        PublishedContent Produce();
        Task<InstalledContent> InstallAsync(PublishedContent published, CancellationToken token);
        /// <summary>EnterPlaymode の直前の再検査。install の待ちの間に変わりうる状態を見る。</summary>
        string? CheckBeforePlay();
        void Bridge(PublishedContent published, InstalledContent installed);
        void EnterPlaymode();
        void ResetBridge();
    }

    /// <summary>
    /// 検証 Play と From published directory の段の列。開始前検査 → 成果物 → install → 直前の再検査 → bridge → EnterPlaymode。
    /// どの段が失敗しても以降の段を実行しない。bridge の前の失敗は環境を変えず、bridge 以降の失敗は ledger を戻してから止まる
    /// （Play を要求した後に Play に至らなければ ledger 自身の規則で戻る）。
    /// <para>
    /// 1 つずつ（single-flight）: 2 経路は同じ <see cref="Shared"/> を使い、実行中の新しい要求は成果物の段に入る前に拒否する。
    /// 印は main thread だけで触る（menu・窓・await の継続はどれも Editor の main thread）。install の待ちの間に domain reload が
    /// 起きると操作ごと消え、bridge の前なので環境は変わらない。取り消しは bridge の直前に確かめ、遅れた継続を bridge に通さない。
    /// </para>
    /// </summary>
    internal sealed class VerifyPlaySequence
    {
        internal static VerifyPlaySequence Shared { get; } = new();

        private bool _running;

        internal bool IsRunning => _running;

        internal async Task<VerifyPlayOutcome> RunAsync(IVerifyPlayStages stages, CancellationToken token)
        {
            if (stages == null) throw new ArgumentNullException(nameof(stages));
            if (_running)
                return VerifyPlayOutcome.Rejected(VerifyPlayStage.Start,
                    "Another Verify Play or From published directory operation is in progress.");
            _running = true;
            var stage = VerifyPlayStage.Start;
            var bridged = false;
            try
            {
                var reason = stages.CheckBeforeStart();
                if (reason != null) return VerifyPlayOutcome.Rejected(stage, reason);
                if (token.IsCancellationRequested) return VerifyPlayOutcome.Cancelled(stage);

                stage = VerifyPlayStage.Produce;
                var published = stages.Produce();
                if (token.IsCancellationRequested) return VerifyPlayOutcome.Cancelled(stage);

                stage = VerifyPlayStage.Install;
                var installed = await stages.InstallAsync(published, token);

                stage = VerifyPlayStage.BeforePlay;
                reason = stages.CheckBeforePlay();
                if (reason != null) return VerifyPlayOutcome.Rejected(stage, reason);
                // 窓が閉じた後の遅れた継続を、bridge（環境の変更）に通さない。
                if (token.IsCancellationRequested) return VerifyPlayOutcome.Cancelled(stage);

                stage = VerifyPlayStage.Bridge;
                // bridge が途中で失敗しても置いた分を戻すので、呼ぶ前に印を立てる。
                bridged = true;
                stages.Bridge(published, installed);

                stage = VerifyPlayStage.EnterPlaymode;
                stages.EnterPlaymode();
                return VerifyPlayOutcome.PlayRequested();
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                if (bridged) ResetQuietly(stages);
                return VerifyPlayOutcome.Cancelled(stage);
            }
            catch (Exception ex)
            {
                if (bridged) ResetQuietly(stages);
                return VerifyPlayOutcome.Failed(stage, ex);
            }
            finally
            {
                _running = false;
            }
        }

        private static void ResetQuietly(IVerifyPlayStages stages)
        {
            // 元の失敗を隠さない。戻しの失敗は追加の情報として出す。
            try { stages.ResetBridge(); }
            catch (Exception resetFailure) { Debug.LogException(resetFailure); }
        }
    }

    /// <summary>
    /// 検証 Play（宣言 1 つから build → publish → install → Play）と From published directory（batch の結果 file と published directory
    /// から install → Play）の Editor 側。開始前検査・確認の dialog・段の実装を持ち、段の順序は <see cref="VerifyPlaySequence"/> に任せる。
    /// </summary>
    internal static class VerifyPlayLauncher
    {
        internal const string MenuRoot = "Tools/OSM/Content/Verify Play/";
        private const string AllFull = "all-full";
        private const string Bs4SpringFull = "bs4-spring-full";
        private const string SpringFull = "spring-full";
        private const string SpringFullWhitebox = "spring-full-whitebox";
        private const string SpringPlannerWhitebox = "spring-planner-whitebox";
        private const string SpringWhitebox = "spring-whitebox";
        internal const string Target = "StandaloneWindows64-Player";
        // Delivery 窓の Prepare と同じ root schema。
        private const int RootSchemaVersion = 2;
        private const string LogPrefix = "[VerifyPlay] ";

        [MenuItem(MenuRoot + AllFull)] private static void MenuAllFull() => VerifyFromMenu(AllFull);
        [MenuItem(MenuRoot + Bs4SpringFull)] private static void MenuBs4SpringFull() => VerifyFromMenu(Bs4SpringFull);
        [MenuItem(MenuRoot + SpringFull)] private static void MenuSpringFull() => VerifyFromMenu(SpringFull);
        [MenuItem(MenuRoot + SpringFullWhitebox)] private static void MenuSpringFullWhitebox() => VerifyFromMenu(SpringFullWhitebox);
        [MenuItem(MenuRoot + SpringPlannerWhitebox)] private static void MenuSpringPlannerWhitebox() => VerifyFromMenu(SpringPlannerWhitebox);
        [MenuItem(MenuRoot + SpringWhitebox)] private static void MenuSpringWhitebox() => VerifyFromMenu(SpringWhitebox);

        private static async void VerifyFromMenu(string name)
        {
            // menu の検証 Play は窓を持たず、取り消しの所有者が無いので最後まで続く。
            try
            {
                var outcome = await VerifyAsync(SampleGameBuildPurposes.Find(name), interactive: true);
                if (outcome.Kind != VerifyPlayOutcomeKind.PlayRequested)
                    EditorUtility.DisplayDialog("Verify Play " + name, outcome.ToString(), "OK");
            }
            catch (Exception ex) { Debug.LogException(ex); }
        }

        /// <summary>検証 Play の入口。<paramref name="interactive"/> が偽（eval・テスト）なら dialog を出さず、拒否で止まる。</summary>
        internal static async Task<VerifyPlayOutcome> VerifyAsync(BuildPurposeDeclaration declaration, bool interactive)
        {
            var outcome = await VerifyPlaySequence.Shared.RunAsync(
                new PurposeStages(declaration, interactive, SampleGameBuildPurposes.FirstSceneOf, ContentDeliveryWindow.LoadSettings()),
                CancellationToken.None);
            Report("verify " + declaration.Name, outcome);
            return outcome;
        }

        /// <summary>
        /// From published directory の入口。結果 file（<c>last-result.json</c>）は published directory とは別の経路で受け取ったもので、
        /// manifest SHA-256 の期待値と identity をそこから取る。<paramref name="chooseFolder"/> は結果 file の publishPath が
        /// 手元に無い時だけ呼ぶ。
        /// </summary>
        internal static async Task<VerifyPlayOutcome> ReceiveAsync(string resultFilePath, Func<string?> chooseFolder, bool interactive,
            CancellationToken token)
        {
            var outcome = await VerifyPlaySequence.Shared.RunAsync(
                new ReceivedStages(resultFilePath, chooseFolder, interactive, SampleGameBuildPurposes.FirstSceneOf, ContentDeliveryWindow.LoadSettings()),
                token);
            Report("receive " + resultFilePath, outcome);
            return outcome;
        }

        private static void Report(string operation, VerifyPlayOutcome outcome)
        {
            if (outcome.Kind == VerifyPlayOutcomeKind.PlayRequested) Debug.Log(LogPrefix + operation + ": Play requested.");
            else if (outcome.Exception != null) Debug.LogError(LogPrefix + operation + ": " + outcome + "\n" + outcome.Exception);
            else Debug.LogError(LogPrefix + operation + ": " + outcome);
        }

        // ─── cache root ───

        /// <summary>
        /// 標準の経路（検証 Play・From published directory）の cache root。Delivery 窓の設定が空なら repo の外の既定に解決する
        /// （repo の中は installed/&lt;set&gt;/&lt;revision&gt;/content/... が path の予算を超える）。窓の手入力の Prepare は使わない。
        /// </summary>
        internal static string ResolveCacheRoot(string? configured) =>
            string.IsNullOrWhiteSpace(configured)
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OSM", "ContentCache")
                : configured!;

        // ─── 開始前検査 ───

        /// <summary>
        /// 起動が再利用しうる source の Scene: 名前が SceneResourceMap にある（RegisterAlreadyLoadedScenes が登録し、
        /// PerformUnitySceneLoad が名前で再利用する。宣言の選択の内外を問わない）、名前が UICommon（LoadUICommonAsync が再利用する）、
        /// path が UiCommonPath。Variant の Scene は元と同じ名前なので名前の判定で両表現を覆う。untitled の空 Scene と map に無い Scene は通す。
        /// </summary>
        internal static IReadOnlyList<string> FindReusableScenes(IEnumerable<(string Name, string Path)> loadedScenes, ICollection<string> mapSceneNames)
        {
            var found = new List<string>();
            foreach (var (name, path) in loadedScenes)
            {
                if ((name.Length != 0 && mapSceneNames.Contains(name))
                    || string.Equals(name, "UICommon", StringComparison.Ordinal)
                    || string.Equals(path, SampleGameBootstrapContentComposer.UiCommonPath, StringComparison.Ordinal))
                    found.Add(path.Length == 0 ? name : path);
            }
            return found;
        }

        private static IReadOnlyList<string> FindReusableLoadedScenes()
        {
            var map = AssetDatabase.LoadAssetAtPath<SceneResourceMap>(SampleGameBootstrapContentComposer.SceneResourceMapPath);
            if (map == null) throw new InvalidOperationException("SceneResourceMap is missing: " + SampleGameBootstrapContentComposer.SceneResourceMapPath);
            var names = new HashSet<string>(map.SceneResources.Where(x => x != null).Select(x => x.Identity), StringComparer.Ordinal);
            var loaded = new List<(string, string)>();
            for (var i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (scene.isLoaded) loaded.Add((scene.name, scene.path));
            }
            return FindReusableScenes(loaded, names);
        }

        /// <summary>
        /// 起動が再利用しうる Scene が開いていれば拒否する。menu からなら閉じて空 Scene で続けるかを聞く
        /// （未保存の変更は Unity の保存の確認を通す）。Play 後に元の構成へは戻さない。
        /// </summary>
        private static string? OfferCloseScenes(bool interactive)
        {
            var scenes = FindReusableLoadedScenes();
            if (scenes.Count == 0) return null;
            var message = "Open scenes that startup would reuse instead of the content directory: " + string.Join(", ", scenes);
            if (!interactive || !EditorUtility.DisplayDialog("Source scenes are open", message,
                    "Close them and continue in an empty scene", "Cancel"))
                return message;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return message;
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scenes = FindReusableLoadedScenes();
            return scenes.Count == 0 ? null : message;
        }

        /// <summary>manifest の宣言が記録する欄（名前・表現の主値・runtime 既定値）が手元の宣言と一致するか。Roots 等は記録されないので見ない。</summary>
        internal static string? CompareDeclaration(ContentManifestDeclaration manifest, BuildPurposeDeclaration local)
        {
            if (!string.Equals(manifest.name, local.Name, StringComparison.Ordinal))
                return "Manifest declaration '" + manifest.name + "' is not the selected declaration '" + local.Name + "'.";
            if (!string.Equals(manifest.representation, local.Representation.Primary, StringComparison.Ordinal))
                return "Manifest representation '" + manifest.representation + "' differs from the local declaration '" + local.Representation.Primary + "'.";
            var recorded = manifest.runtimeDefaults ?? Array.Empty<ContentManifestDeclarationValue>();
            var same = recorded.Length == local.RuntimeDefaults.Count && recorded.All(x =>
                local.RuntimeDefaults.Any(y => string.Equals(x.key, y.Key, StringComparison.OrdinalIgnoreCase)
                                               && string.Equals(x.value, y.Value, StringComparison.Ordinal)));
            return same ? null : "Manifest runtime defaults differ from the local declaration '" + local.Name + "'.";
        }

        // ─── From published directory の照合（install の前） ───

        /// <summary>結果 file の読み取り。BS5b-1 の書き手と同じ型で読む（失敗の file は exitCode と欄の欠けで拒否される）。</summary>
        internal static string? ValidateResultFile(SampleGameContentBatch.SuccessFile? result)
        {
            if (result == null) return "The result file is not a batch result.";
            if (result.exitCode != SampleGameContentBatch.ExitSuccess)
                return "The batch result is not a success (exitCode=" + result.exitCode + ").";
            var missing = new[]
            {
                ("identity", result.identity), ("manifestSha256", result.manifestSha256),
                ("declaration", result.declaration), ("publishPath", result.publishPath),
            }.Where(x => string.IsNullOrWhiteSpace(x.Item2)).Select(x => x.Item1).ToArray();
            return missing.Length == 0 ? null : "The batch result is missing: " + string.Join(", ", missing);
        }

        /// <summary>published directory は結果 file の publishPath が手元に有ればそれ、無ければ folder の選択。</summary>
        internal static string? ResolvePublishedDirectory(SampleGameContentBatch.SuccessFile result, Func<string?> chooseFolder) =>
            Directory.Exists(result.publishPath) ? result.publishPath : chooseFolder();

        /// <summary>
        /// 結果 file と published directory の組の照合。SHA-256 → identity → 宣言の順。結果 file が最新の batch の結果であることは
        /// 保証しない（組が一致していれば受け取る）。宣言の無い manifest は Delivery 窓の手入力経路へ案内する。
        /// </summary>
        internal static string? ValidatePublished(SampleGameContentBatch.SuccessFile result, string publishedDirectory,
            out ContentTransportManifest? manifest, out BuildPurposeDeclaration? declaration)
        {
            manifest = null;
            declaration = null;
            var manifestPath = Path.Combine(publishedDirectory, "transport.json");
            if (!File.Exists(manifestPath)) return "transport.json is missing in " + publishedDirectory;
            if (!string.Equals(Sha256(manifestPath), result.manifestSha256, StringComparison.OrdinalIgnoreCase))
                return "transport.json does not match the manifestSha256 of the batch result.";
            manifest = JsonUtility.FromJson<ContentTransportManifest>(File.ReadAllText(manifestPath));
            if (manifest == null) return "transport.json cannot be read.";
            if (!string.Equals(manifest.revision, result.identity, StringComparison.Ordinal))
                return "The published revision '" + manifest.revision + "' is not the batch identity '" + result.identity + "'.";
            var name = manifest.declaration?.name ?? string.Empty;
            if (name.Length == 0)
                return "The manifest has no declaration. Use the manual route in Tools/OSM/Content/Delivery.";
            if (!string.Equals(result.declaration, name, StringComparison.Ordinal))
                return "The batch result declaration '" + result.declaration + "' is not the manifest declaration '" + name + "'.";
            try { declaration = SampleGameBuildPurposes.Find(name); }
            catch (ArgumentException ex) { return ex.Message; }
            return CompareDeclaration(manifest.declaration!, declaration);
        }

        internal static string Sha256(string path)
        {
            using var stream = File.OpenRead(path);
            using var hash = SHA256.Create();
            return string.Concat(hash.ComputeHash(stream).Select(x => x.ToString("x2")));
        }

        internal static PublishedContent ToPublished(string publishPath, string manifestSha256, ContentTransportManifest manifest) =>
            new(publishPath, manifestSha256, manifest.revision, manifest.contentSet, manifest.declaration.name, manifest.declaration.representation,
                (manifest.declaration.runtimeDefaults ?? Array.Empty<ContentManifestDeclarationValue>())
                .Select(x => new KeyValuePair<string, string>(x.key, x.value)).ToArray());

        // ─── 段 ───

        /// <summary>2 経路が共有する段（install・直前の再検査・bridge・EnterPlaymode）。</summary>
        internal abstract class DirectoryPlayStages : IVerifyPlayStages
        {
            private readonly Func<string, string> _firstSceneOf;
            private readonly string _cacheRoot;
            private readonly long _diskBudget;
            protected readonly bool Interactive;
            protected IReadOnlyList<string> ControlledKeys = Array.Empty<string>();

            protected DirectoryPlayStages(bool interactive, Func<string, string> firstSceneOf, ContentDeliveryWindow.Settings settings)
            {
                Interactive = interactive;
                _firstSceneOf = firstSceneOf ?? throw new ArgumentNullException(nameof(firstSceneOf));
                _cacheRoot = ResolveCacheRoot(settings.cacheRoot);
                _diskBudget = settings.budget;
            }

            internal string CacheRoot => _cacheRoot;

            public abstract string? CheckBeforeStart();
            public abstract PublishedContent Produce();

            /// <summary>開始前検査の共通部分（Play mode・compile、開いている Scene、Delivery 窓の ledger、profile・外の値・コマンドライン）。</summary>
            protected string? CheckCommonBeforeStart(IReadOnlyList<string> controlledKeys)
            {
                ControlledKeys = controlledKeys;
                return PlayAsLauncher.CheckEditorState()
                       ?? OfferCloseScenes(Interactive)
                       ?? PlayAsLauncher.OfferDeliveryReset(Interactive)
                       ?? PlayAsLauncher.CheckLaunchConflicts(ControlledKeys, Environment.GetCommandLineArgs());
            }

            public async Task<InstalledContent> InstallAsync(PublishedContent published, CancellationToken token)
            {
                var request = new ContentInstallRequest(published.ManifestSha256, published.ContentSet, published.Identity,
                    Target, Application.unityVersion, RootSchemaVersion, _diskBudget);
                using var source = new LocalContentArtifactSource(published.PublishPath);
                var result = await new ContentInstaller(new ContentCacheStore(_cacheRoot)).InstallAsync(request, source, token);
                Debug.Log(LogPrefix + "installed " + result.RevisionRoot + " (cache root " + _cacheRoot + ")");
                return new InstalledContent(result.RevisionRoot, result.ManifestSha256, result.Revision);
            }

            /// <summary>
            /// install の待ちの間に変わりうる状態の再検査（開いている Scene、Play・compile・asset の更新・移行中、profile、外の値、
            /// ledger）。ここでは dialog を出さない。コマンドラインは process の間変わらないが、同じ関数で一緒に見る。
            /// </summary>
            public string? CheckBeforePlay()
            {
                var scenes = FindReusableLoadedScenes();
                if (scenes.Count != 0) return "Open scenes that startup would reuse: " + string.Join(", ", scenes);
                return PlayAsLauncher.CheckEditorState()
                       ?? PlayAsLauncher.CheckLaunchConflicts(ControlledKeys, Environment.GetCommandLineArgs());
            }

            /// <summary>bridge が置く環境変数。初回 Scene は App がその宣言に決めた値（受け取る側の App の定義）。</summary>
            internal IReadOnlyList<KeyValuePair<string, string>> BridgeValues(PublishedContent published, InstalledContent installed) =>
                PurposeLaunchSettings.DirectoryPlay(installed.RevisionRoot, installed.ManifestSha256, installed.Revision,
                    published.Representation, published.RuntimeDefaults, _firstSceneOf(published.DeclarationName));

            public void Bridge(PublishedContent published, InstalledContent installed) =>
                ContentDeliveryPlayBridge.ApplyForOnePlay(BridgeValues(published, installed));

            public void EnterPlaymode() => EditorApplication.EnterPlaymode();

            public void ResetBridge() => ContentDeliveryPlayBridge.Reset();
        }

        /// <summary>検証 Play: 宣言から Full build → publish。manifest SHA-256 はこの操作の publish の値をそのまま使う（外から受け取る値が無い）。</summary>
        internal sealed class PurposeStages : DirectoryPlayStages
        {
            private readonly BuildPurposeDeclaration _declaration;

            internal PurposeStages(BuildPurposeDeclaration declaration, bool interactive, Func<string, string> firstSceneOf,
                ContentDeliveryWindow.Settings settings) : base(interactive, firstSceneOf, settings) =>
                _declaration = declaration ?? throw new ArgumentNullException(nameof(declaration));

            public override string? CheckBeforeStart() =>
                CheckCommonBeforeStart(PurposeLaunchSettings.DirectoryPlayConfigKeys(_declaration.RuntimeDefaults));

            public override PublishedContent Produce()
            {
                // build は同期（Editor が止まる。既存の Build menu と同じ）。publish は BS5b-1 の経路（Full 以外は拒否される）。
                var result = SampleGameContentBuild.BuildResult(_declaration, BuildContentMode.Full);
                var artifactsRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "artifacts", "bs2b"));
                var published = ContentTransportPublisher.Publish(result, Path.Combine(artifactsRoot, "transport", result.Identity));
                var manifestPath = Path.Combine(published, "transport.json");
                var manifest = JsonUtility.FromJson<ContentTransportManifest>(File.ReadAllText(manifestPath))
                    ?? throw new InvalidOperationException("transport.json cannot be read: " + manifestPath);
                var mismatch = CompareDeclaration(manifest.declaration ?? new ContentManifestDeclaration(), _declaration);
                if (mismatch != null) throw new InvalidOperationException(mismatch);
                Debug.Log(LogPrefix + "published " + published);
                return ToPublished(published, Sha256(manifestPath), manifest);
            }
        }

        /// <summary>From published directory: 結果 file と published directory の組を照合してから install する。</summary>
        internal sealed class ReceivedStages : DirectoryPlayStages
        {
            private readonly string _resultFilePath;
            private readonly Func<string?> _chooseFolder;
            private PublishedContent? _published;

            internal ReceivedStages(string resultFilePath, Func<string?> chooseFolder, bool interactive, Func<string, string> firstSceneOf,
                ContentDeliveryWindow.Settings settings) : base(interactive, firstSceneOf, settings)
            {
                _resultFilePath = resultFilePath ?? throw new ArgumentNullException(nameof(resultFilePath));
                _chooseFolder = chooseFolder ?? throw new ArgumentNullException(nameof(chooseFolder));
            }

            public override string? CheckBeforeStart()
            {
                if (!File.Exists(_resultFilePath)) return "The batch result file does not exist: " + _resultFilePath;
                SampleGameContentBatch.SuccessFile? result;
                try { result = JsonUtility.FromJson<SampleGameContentBatch.SuccessFile>(File.ReadAllText(_resultFilePath)); }
                catch (ArgumentException ex) { return "The batch result file cannot be read: " + ex.Message; }
                var reason = ValidateResultFile(result);
                if (reason != null) return reason;
                var directory = ResolvePublishedDirectory(result!, _chooseFolder);
                if (string.IsNullOrWhiteSpace(directory)) return "No published directory was selected.";
                reason = ValidatePublished(result!, directory!, out var manifest, out var declaration);
                if (reason != null) return reason;
                _published = ToPublished(Path.GetFullPath(directory!), result!.manifestSha256.ToLowerInvariant(), manifest!);
                return CheckCommonBeforeStart(PurposeLaunchSettings.DirectoryPlayConfigKeys(declaration!.RuntimeDefaults));
            }

            public override PublishedContent Produce() =>
                _published ?? throw new InvalidOperationException("The published directory was not validated.");
        }
    }
}
