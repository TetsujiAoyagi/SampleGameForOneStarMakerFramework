#nullable enable

#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using OneStarMaker.Runtime.ContentResolution;

namespace OneStarMaker.Runtime
{
    /// <summary>取り込みの照合が見る loaded の Scene の 1 件（Unity の Scene の値を写したもの）。</summary>
    internal readonly struct EditorPlaySnapshotLoadedScene
    {
        internal EditorPlaySnapshotLoadedScene(string name, string path)
        {
            Name = name ?? string.Empty;
            Path = path ?? string.Empty;
        }

        internal string Name { get; }
        internal string Path { get; }
    }

    /// <summary>
    /// 宣言と、取り込みの段が load を終えた時点の loaded の Scene の照合。状態を持たず、Unity の Scene を使わない。
    /// <para>
    /// 照合が合わない Play は起動の失敗にする。合わないまま進むと、後の登録や初回 Scene の load が同じ名前を Content Directory から読み、
    /// 編集中の Scene の代わりに build 済みの Scene が黙って動く。
    /// </para>
    /// </summary>
    internal static class EditorPlaySnapshotIntake
    {
        /// <summary>宣言のとおりに 1 Scene 1 個ずつ複製が loaded なら null、そうでなければ理由。</summary>
        internal static string? Verify(IReadOnlyList<EditorPlaySnapshotScene> declaration, IReadOnlyList<EditorPlaySnapshotLoadedScene> loaded)
        {
            if (declaration == null) throw new ArgumentNullException(nameof(declaration));
            if (loaded == null) throw new ArgumentNullException(nameof(loaded));
            if (declaration.Count == 0) return "The play snapshot declaration is empty.";

            var duplicateIdentity = declaration.GroupBy(x => x.Identity, StringComparer.Ordinal).FirstOrDefault(x => x.Count() > 1);
            if (duplicateIdentity != null)
                return "The play snapshot declares scene '" + duplicateIdentity.Key + "' more than once.";
            if (declaration.Count(x => x.IsActive) > 1)
                return "The play snapshot declares more than one active scene.";

            // 1 Scene の照合は、要求時に複製を採る・読み直す時と同じ関数を使う（規則を 1 か所に置く）。
            var captured = loaded.Select(x => new EditorSnapshotLoadedScene(x.Name, x.Path)).ToArray();
            foreach (var scene in declaration)
            {
                var failure = EditorSnapshotSceneMatch.Verify(scene.Identity, scene.CopyPath, captured);
                if (failure != null) return failure;
            }
            return null;
        }

        /// <summary>
        /// 起動の計画。取り込み（複製の load）より前に決め、失敗なら複製を 1 つも load せずに起動の失敗にする。状態を持たない。
        /// </summary>
        /// <param name="snapshotExpected">この Play の config に <see cref="EditorPlaySnapshotBridge.ExpectedConfigKey"/> が真で置かれたか。</param>
        /// <param name="directoryMode">Editor の directory mode の起動か（Player の構成ではない）。</param>
        /// <param name="snapshotProviderWired">要求時解決の loader を配線し、その policy に snapshot の供給元を置いたか（検証済みの directory だけ）。</param>
        /// <param name="declaration">bridge の宣言。置かれていなければ null。</param>
        /// <param name="isMapped">Scene 名が SceneResourceMap にあるか。</param>
        internal static EditorPlaySnapshotStartupPlan PlanStartup(
            bool snapshotExpected,
            bool directoryMode,
            bool snapshotProviderWired,
            IReadOnlyList<EditorPlaySnapshotScene>? declaration,
            Func<string, bool> isMapped)
        {
            if (isMapped == null) throw new ArgumentNullException(nameof(isMapped));
            // key の無い Play は、残った宣言があっても読まず、今までどおり load 済みの map の Scene を登録する。
            if (!snapshotExpected)
                return EditorPlaySnapshotStartupPlan.RegisterLoadedScenes;

            if (!directoryMode)
                return EditorPlaySnapshotStartupPlan.Fail(
                    "This Play declared Editor scene snapshots, but it does not start in the content directory mode.");
            if (declaration == null)
                return EditorPlaySnapshotStartupPlan.Fail(
                    "This Play declared Editor scene snapshots, but the snapshot declaration is not available (it may have been lost in a domain reload).");
            if (!snapshotProviderWired)
                return EditorPlaySnapshotStartupPlan.Fail(
                    "This Play declared Editor scene snapshots, but on-demand content resolution is not wired with the snapshot provider " +
                    "(the content directory must be registered from a verified installed revision).");

            var requested = new List<EditorPlaySnapshotScene>();
            foreach (var scene in declaration)
            {
                if (!scene.RequestAtStartup) continue;
                // 明示の要求を黙って飛ばさない。
                if (!isMapped(scene.Identity))
                    return EditorPlaySnapshotStartupPlan.Fail(
                        "The play snapshot requests scene '" + scene.Identity + "' at startup, but the SceneResourceMap has no such scene.");
                requested.Add(scene);
            }
            return EditorPlaySnapshotStartupPlan.RequestSnapshotScenes(requested);
        }

        /// <summary>要求時解決の供給元に渡す宣言（値に写す。連携部分は Bootstrap の型を参照しない）。</summary>
        internal static IReadOnlyList<EditorSnapshotDeclaredScene> ToDeclaredScenes(IReadOnlyList<EditorPlaySnapshotScene> declaration)
        {
            if (declaration == null) throw new ArgumentNullException(nameof(declaration));
            return declaration.Select(x => new EditorSnapshotDeclaredScene(x.Identity, x.CopyPath, x.SourcePath, x.WasDirty, x.Sha256)).ToArray();
        }

        /// <summary>
        /// 取り込みが途中で失敗した時に unload する Scene。path が宣言の複製と一致する loaded の Scene だけで、
        /// 空の開始 Scene、Content Directory から来た Scene（UI 等）、宣言に無い Scene は含めない。
        /// </summary>
        internal static IReadOnlyList<EditorPlaySnapshotLoadedScene> SelectUnloadTargets(
            IReadOnlyList<EditorPlaySnapshotScene> declaration, IReadOnlyList<EditorPlaySnapshotLoadedScene> loaded)
        {
            if (declaration == null) throw new ArgumentNullException(nameof(declaration));
            if (loaded == null) throw new ArgumentNullException(nameof(loaded));
            var copyPaths = new HashSet<string>(declaration.Select(x => x.CopyPath), StringComparer.Ordinal);
            return loaded.Where(x => x.Path.Length != 0 && copyPaths.Contains(x.Path)).ToArray();
        }

        /// <summary>取り込みの成功の log。観測 UI はこの接頭辞と <c>scene=</c> で Scene 名を拾う。</summary>
        internal const string LogPrefix = "[AppInit] Editor play snapshot intake: ";

        internal static string FormatLoadedLog(EditorPlaySnapshotScene scene) =>
            LogPrefix + "scene=" + scene.Identity + " copy=" + scene.CopyPath + " active=" + (scene.IsActive ? "true" : "false");

        /// <summary>取り込みの成功の log に、起動時に要求するかを足した行（起動が出す行）。欄は空白区切りで、観測は名前で拾う。</summary>
        internal static string FormatIntakeLog(EditorPlaySnapshotScene scene) =>
            FormatLoadedLog(scene) + " requested=" + (scene.RequestAtStartup ? "true" : "false");

        /// <summary>起動時の要求の 1 件の log の接頭辞。</summary>
        internal const string RequestLogPrefix = "[AppInit] Editor play snapshot request: ";

        internal static string FormatRequestLog(string identity, bool stable) =>
            RequestLogPrefix + "scene=" + identity + " stable=" + (stable ? "true" : "false");
    }

    /// <summary>起動の計画の結果: 今までの既ロード登録、印のある複製だけの要求、または起動の失敗（理由つき）。</summary>
    internal sealed class EditorPlaySnapshotStartupPlan
    {
        internal static readonly EditorPlaySnapshotStartupPlan RegisterLoadedScenes = new(false, Array.Empty<EditorPlaySnapshotScene>(), null);

        private EditorPlaySnapshotStartupPlan(bool requestsSnapshotScenes, IReadOnlyList<EditorPlaySnapshotScene> requested, string? failure)
        {
            RequestsSnapshotScenes = requestsSnapshotScenes;
            Requested = requested;
            Failure = failure;
        }

        /// <summary>
        /// 真なら snapshot の Play: 取り込みの後、<see cref="Requested"/> だけを要求し、load 済みの Scene の走査（既ロード登録）はしない。
        /// 偽なら今までの既ロード登録（<see cref="Failure"/> が null の時）。
        /// </summary>
        internal bool RequestsSnapshotScenes { get; }

        /// <summary>起動時に要求する Scene（宣言の並び順）。0 件なら何も要求せずに起動を続ける。</summary>
        internal IReadOnlyList<EditorPlaySnapshotScene> Requested { get; }

        /// <summary>起動の失敗の理由。成功の計画では null。</summary>
        internal string? Failure { get; }

        internal static EditorPlaySnapshotStartupPlan RequestSnapshotScenes(IReadOnlyList<EditorPlaySnapshotScene> requested)
            => new(true, requested ?? throw new ArgumentNullException(nameof(requested)), null);

        internal static EditorPlaySnapshotStartupPlan Fail(string reason)
            => new(false, Array.Empty<EditorPlaySnapshotScene>(), reason ?? throw new ArgumentNullException(nameof(reason)));
    }
}
#endif
