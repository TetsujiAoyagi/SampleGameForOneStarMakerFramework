#nullable enable

#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using OneStarMaker.Runtime.AssetManagement.Internal;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OneStarMaker.Runtime.ContentResolution
{
    /// <summary>
    /// 宣言の 1 Scene（Editor の Play の入口が Play の前に作った複製）。起動の配線が Runtime の bridge の宣言から値に写して渡す
    /// （この連携部分は Bootstrap の型を参照しない）。
    /// </summary>
    internal sealed class EditorSnapshotDeclaredScene
    {
        internal EditorSnapshotDeclaredScene(string identity, string copyPath, string sourcePath, bool wasDirty, string sha256)
        {
            if (string.IsNullOrEmpty(identity)) throw new ArgumentException("Identity is required.", nameof(identity));
            if (string.IsNullOrEmpty(copyPath)) throw new ArgumentException("Copy path is required.", nameof(copyPath));
            Identity = identity;
            CopyPath = copyPath;
            SourcePath = sourcePath ?? string.Empty;
            WasDirty = wasDirty;
            Sha256 = sha256 ?? string.Empty;
        }

        /// <summary>Scene 名。複製の file 名と同じで、Unity の Scene の name として見える。</summary>
        internal string Identity { get; }

        /// <summary><c>Assets/</c> から始まる複製の asset path。</summary>
        internal string CopyPath { get; }

        /// <summary>複製の元の Scene の path（record の事実。照合には使わない）。</summary>
        internal string SourcePath { get; }

        /// <summary>複製を作った時に元の Scene が未保存の変更を持っていたか（record の事実）。</summary>
        internal bool WasDirty { get; }

        /// <summary>Play の前に作った複製の bytes の SHA-256（record の事実。Play の間は再検証しない）。</summary>
        internal string Sha256 { get; }
    }

    /// <summary>照合が見る load 済みの Scene の 1 件（Unity の Scene の値を写したもの）。</summary>
    internal readonly struct EditorSnapshotLoadedScene
    {
        private readonly string? _name;
        private readonly string? _path;

        internal EditorSnapshotLoadedScene(string name, string path)
        {
            _name = name;
            _path = path;
        }

        // default の値（constructor を通らない）でも空文字を返す。
        internal string Name => _name ?? string.Empty;

        internal string Path => _path ?? string.Empty;
    }

    /// <summary>
    /// 1 Scene の照合。起動の取り込みの照合と、要求時に採る・読み直す時の照合が同じ規則を使う（規則を 1 か所に置く）。
    /// 状態を持たず、Unity の Scene を使わない。
    /// </summary>
    internal static class EditorSnapshotSceneMatch
    {
        /// <summary>
        /// <paramref name="identity"/> の名前で load 済みの Scene が、<paramref name="copyPath"/> の複製ちょうど 1 つだけなら null、そうでなければ理由。
        /// 名前だけで照合しない（同名の別の path の Scene を複製と取り違えると、build 済みの版が編集中の Scene として動く）。
        /// </summary>
        internal static string? Verify(string identity, string copyPath, IReadOnlyList<EditorSnapshotLoadedScene> loaded)
        {
            if (identity == null) throw new ArgumentNullException(nameof(identity));
            if (copyPath == null) throw new ArgumentNullException(nameof(copyPath));
            if (loaded == null) throw new ArgumentNullException(nameof(loaded));

            var sameName = loaded.Where(x => string.Equals(x.Name, identity, StringComparison.Ordinal)).ToArray();
            var copies = sameName.Count(x => string.Equals(x.Path, copyPath, StringComparison.Ordinal));
            if (copies == 0)
                return "The play snapshot of scene '" + identity + "' is not loaded from " + copyPath + ".";
            if (copies > 1)
                return "The play snapshot of scene '" + identity + "' is loaded more than once.";
            if (sameName.Length != 1)
            {
                var other = sameName.First(x => !string.Equals(x.Path, copyPath, StringComparison.Ordinal));
                return "Another scene named '" + identity + "' is already loaded from " +
                       (other.Path.Length == 0 ? "(no path)" : other.Path) + "; the snapshot would run twice.";
            }

            return null;
        }
    }

    /// <summary>
    /// 預け元が使う Unity の Scene の I/O。test は Scene の実体を差し替える（Unity の Scene の load をせずに預け元の判断を確かめる）。
    /// </summary>
    internal interface IEditorSnapshotSceneIo
    {
        /// <summary>今 load 済みの Scene の名前と path。</summary>
        IReadOnlyList<EditorSnapshotLoadedScene> CaptureLoadedScenes();

        /// <summary><paramref name="copyPath"/> の Scene が load 済みならその Scene、無ければ null。</summary>
        IBackendScene? FindLoaded(string copyPath);

        /// <summary>
        /// 複製の path を additive で load して完了を待つ。Unity が受け付けなかった（file が無い等）時は false を返す。
        /// </summary>
        UniTask<bool> LoadAsync(string copyPath);

        /// <summary>Scene を unload して完了を待つ。Unity が受け付けなかった時は false を返す。</summary>
        UniTask<bool> UnloadAsync(IBackendScene scene);
    }

    /// <summary>
    /// 預け元: 宣言の複製を、load 済みなら採り、無ければ複製の path を読み直して、照合を通した Scene を台帳へ渡す。unload もする。
    /// 状態は宣言（不変）だけで、Scene も token も持たない（持つのは台帳）。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 採れない時（file が無い、load が受け付けられない、load しても Scene が無い、同名の別の path の Scene が load 済み、照合が合わない）は、
    /// Scene の identity と理由を文に持つ <see cref="InvalidOperationException"/> を投げ、自分が読んだ Scene は unload してから投げる。
    /// loader はこの例外を包まない（AssetManagement の load の失敗と同じ扱い）。別の経路（directory）で読み直す者はいない。
    /// </para>
    /// <para>
    /// 照合は、load（または採用）を終えた時点の Scene で行う。起動の取り込みの照合を使い回さない（その後に別の Scene が載りうる）。
    /// </para>
    /// <para>
    /// 採った Scene（預かる前から load 済みだった、未要求の複製）も、読み直した Scene と同じく、離す時は unload する。次の要求は読み直す。
    /// </para>
    /// </remarks>
    internal sealed class EditorSnapshotSceneSource : IAdoptedSceneSource
    {
        private readonly IEditorSnapshotSceneIo _io;
        private readonly Dictionary<string, EditorSnapshotDeclaredScene> _byIdentity;

        /// <exception cref="ArgumentException">宣言が空、または同じ identity が 2 回ある。</exception>
        internal EditorSnapshotSceneSource(IReadOnlyList<EditorSnapshotDeclaredScene> declaration, IEditorSnapshotSceneIo io)
        {
            if (declaration == null) throw new ArgumentNullException(nameof(declaration));
            _io = io ?? throw new ArgumentNullException(nameof(io));
            if (declaration.Count == 0) throw new ArgumentException("A snapshot declaration needs at least one scene.", nameof(declaration));

            _byIdentity = new Dictionary<string, EditorSnapshotDeclaredScene>(StringComparer.Ordinal);
            foreach (var scene in declaration)
            {
                if (scene == null) throw new ArgumentException("The snapshot declaration has a null scene.", nameof(declaration));
                if (_byIdentity.ContainsKey(scene.Identity))
                    throw new ArgumentException("The snapshot declaration declares scene '" + scene.Identity + "' more than once.", nameof(declaration));
                _byIdentity.Add(scene.Identity, scene);
            }

            Declaration = declaration.ToArray();
        }

        /// <summary>宣言（並び順は入口の順）。供給元はこの instance の宣言から候補を作る（供給元と load の口が別の宣言を見ない）。</summary>
        internal IReadOnlyList<EditorSnapshotDeclaredScene> Declaration { get; }

        public IReadOnlyCollection<string> ReservedIdentities => _byIdentity.Keys;

        /// <summary>宣言にある identity なら真で、その 1 件を返す。</summary>
        internal bool TryGetDeclared(string identity, out EditorSnapshotDeclaredScene scene)
        {
            if (identity != null && _byIdentity.TryGetValue(identity, out var found))
            {
                scene = found;
                return true;
            }

            scene = null!;
            return false;
        }

        public async UniTask<IBackendScene> AcquireAsync(string sceneIdentity)
        {
            if (!TryGetDeclared(sceneIdentity, out var declared))
                throw new InvalidOperationException("Scene '" + sceneIdentity + "' is not declared by this play snapshot.");

            var existing = _io.FindLoaded(declared.CopyPath);
            if (existing != null && existing.IsLoaded)
            {
                // 既に load 済みの複製を採る。読み直さないので、照合が合わなくても自分が読んだ Scene は無い（unload しない）。
                var adoptFailure = EditorSnapshotSceneMatch.Verify(declared.Identity, declared.CopyPath, _io.CaptureLoadedScenes());
                if (adoptFailure != null)
                    throw new InvalidOperationException("Scene '" + declared.Identity + "' cannot be taken from the play snapshot: " + adoptFailure);
                return existing;
            }

            // 同名の別の path の Scene が load 済みなら読まない（読むと同名が 2 つになる）。
            var sameName = _io.CaptureLoadedScenes()
                .Where(x => string.Equals(x.Name, declared.Identity, StringComparison.Ordinal)).ToArray();
            if (sameName.Length != 0)
                throw new InvalidOperationException("Scene '" + declared.Identity + "' cannot be loaded from the play snapshot: another scene with the same name is already loaded from " +
                                                    (sameName[0].Path.Length == 0 ? "(no path)" : sameName[0].Path) + ".");

            if (!await _io.LoadAsync(declared.CopyPath))
                throw new InvalidOperationException("Scene '" + declared.Identity + "' cannot be loaded from the play snapshot: Unity did not accept " +
                                                    declared.CopyPath + " (the copy file may be missing).");

            var loaded = _io.FindLoaded(declared.CopyPath);
            if (loaded == null || !loaded.IsLoaded)
                throw new InvalidOperationException("Scene '" + declared.Identity + "' cannot be loaded from the play snapshot: " + declared.CopyPath + " did not load.");

            var failure = EditorSnapshotSceneMatch.Verify(declared.Identity, declared.CopyPath, _io.CaptureLoadedScenes());
            if (failure != null)
            {
                // 照合が合わない Scene を load 済みのまま残さない（台帳に入らないので、ほかに離す者がいない）。
                await UnloadAfterFailureAsync(loaded, declared);
                throw new InvalidOperationException("Scene '" + declared.Identity + "' cannot be taken from the play snapshot: " + failure);
            }

            return loaded;
        }

        public async UniTask UnloadAsync(IBackendScene scene)
        {
            if (scene == null) throw new ArgumentNullException(nameof(scene));
            // 既に無効・load 済みでない Scene は unload 済みとして扱う（Unity が先に解体した等）。
            if (!scene.IsLoaded) return;
            if (!await _io.UnloadAsync(scene) && scene.IsLoaded)
                throw new InvalidOperationException("Unity did not unload the play snapshot scene '" + scene.Name + "'.");
        }

        private async UniTask UnloadAfterFailureAsync(IBackendScene scene, EditorSnapshotDeclaredScene declared)
        {
            try
            {
                await UnloadAsync(scene);
            }
            catch (Exception ex)
            {
                // 元の失敗を隠さない。unload の失敗は Error の log に残す。
                Debug.LogError("[ContentResolution] Could not unload the rejected play snapshot of scene '" + declared.Identity + "': " + ex.Message);
            }
        }
    }

    /// <summary>
    /// 実物の Unity の I/O。load は Editor の assembly が渡す delegate（<c>EditorSceneManager.LoadSceneAsyncInPlayMode</c>）で行う
    /// （Runtime の assembly は <c>UnityEditor</c> を参照しない）。
    /// </summary>
    internal sealed class UnityEditorSnapshotSceneIo : IEditorSnapshotSceneIo
    {
        private readonly Func<string, AsyncOperation?> _loadScene;

        internal UnityEditorSnapshotSceneIo(Func<string, AsyncOperation?> loadScene)
        {
            _loadScene = loadScene ?? throw new ArgumentNullException(nameof(loadScene));
        }

        public IReadOnlyList<EditorSnapshotLoadedScene> CaptureLoadedScenes()
        {
            var loaded = new List<EditorSnapshotLoadedScene>(SceneManager.sceneCount);
            for (var i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (scene.isLoaded) loaded.Add(new EditorSnapshotLoadedScene(scene.name, scene.path));
            }

            return loaded;
        }

        public IBackendScene? FindLoaded(string copyPath)
        {
            var scene = SceneManager.GetSceneByPath(copyPath);
            return scene.IsValid() && scene.isLoaded ? new LoadedScene(scene) : null;
        }

        public async UniTask<bool> LoadAsync(string copyPath)
        {
            // 読み直しは取消を受けない（Unity の Scene の load は途中で止まらない）。取消は台帳の口が完了の後に扱う。
            var operation = _loadScene(copyPath);
            if (operation == null) return false;
            await operation.ToUniTask();
            return true;
        }

        public async UniTask<bool> UnloadAsync(IBackendScene scene)
        {
            if (!(scene is LoadedScene unity))
                throw new ArgumentException("The scene was not returned by this I/O.", nameof(scene));
            var operation = SceneManager.UnloadSceneAsync(unity.Scene);
            if (operation == null) return false;
            await operation.ToUniTask();
            return true;
        }

        /// <summary>Unity の Scene（struct の handle）。無効になった Scene は load 済みでないと答える。</summary>
        private sealed class LoadedScene : IBackendScene
        {
            internal LoadedScene(Scene scene)
            {
                Scene = scene;
            }

            internal Scene Scene { get; }

            public bool IsLoaded => Scene.IsValid() && Scene.isLoaded;

            public string Name => Scene.name;

            public GameObject[] GetRootGameObjects() => Scene.GetRootGameObjects();
        }
    }
}
#endif
