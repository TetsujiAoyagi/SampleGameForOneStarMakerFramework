#nullable enable

using System;
using System.Linq;
using Cysharp.Threading.Tasks;
using OneStarMaker.Runtime.AssetDescriptions;
using OneStarMaker.Runtime.AssetManagement;
using OneStarMaker.Runtime.UISystem;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;

namespace OneStarMaker.Runtime
{
    /// <summary>
    /// <see cref="BootstrapUiCommonLoader.LoadAsync"/> が UICommon をどこから取るかを決める入力。
    /// <para>
    /// 規則は initializer が起動の最初に <see cref="BootstrapSourcePolicy.Plan"/> で決めた値で受ける。
    /// 2 つの hook（logical key と address）は値ではなく関数で受け、読む経路が決まった後にだけ評価する
    /// （借用の経路では 1 つも呼ばない。派生の hook が呼ばれる時点と回数を今までと同じにする）。
    /// directory か否かだけは副作用の無い field の読み取りなので、呼び出し時の bool で受ける。
    /// </para>
    /// </summary>
    internal readonly struct BootstrapUiCommonSource
    {
        internal BootstrapUiCommonSource(
            bool useContentDirectory,
            BootstrapUiCommonRule rule,
            Func<string> directorySceneLogicalKey,
            Func<string> uiCommonPrefabAddress)
        {
            UseContentDirectory = useContentDirectory;
            Rule = rule;
            DirectorySceneLogicalKey = directorySceneLogicalKey
                ?? throw new ArgumentNullException(nameof(directorySceneLogicalKey));
            UICommonPrefabAddress = uiCommonPrefabAddress ?? throw new ArgumentNullException(nameof(uiCommonPrefabAddress));
        }

        internal bool UseContentDirectory { get; }
        internal BootstrapUiCommonRule Rule { get; }
        internal Func<string> DirectorySceneLogicalKey { get; }
        internal Func<string> UICommonPrefabAddress { get; }
    }

    /// <summary><see cref="BootstrapUiCommonLoader.LoadAsync"/> の結果。取得した UICommon と、それを読んだ先（起動の log に出す）。</summary>
    internal readonly struct BootstrapUiCommonLoadResult
    {
        internal BootstrapUiCommonLoadResult(UICommon uiCommon, BootstrapUiCommonOrigin origin)
        {
            UICommon = uiCommon;
            Origin = origin;
        }

        internal UICommon UICommon { get; }
        internal BootstrapUiCommonOrigin Origin { get; }
    }

    /// <summary>
    /// 起動時の UICommon の取得（ロード済み Scene の借用 / Content Directory の logical key / Addressables の address の 3 経路）と、
    /// 自分がロードした UICommon・自分が作った EventSystem の寿命を持つ。
    /// <para>
    /// 所有は経路で違う。active Scene の UICommon を見つけた経路は借用で、何も記録せず破棄もしない（EventSystem も作らない）。
    /// directory と Addressables の経路は scene handle と root の GameObject を component の検査より前に記録するので、
    /// 検査で失敗しても <see cref="Dispose"/> が root を破棄する。scene handle は参照を持つだけで unload しない。
    /// scene の解放は AssetManagement の ReleaseAll が担い、ここで unload すると Play 終了時に二重解放になる。
    /// </para>
    /// <para>
    /// initializer は <see cref="LoadAsync"/> を await する前にこのインスタンスを field へ入れ、ReleaseAll の中で
    /// AssetManagement.ReleaseAll と SceneResourceMap の解放の後に <see cref="Dispose"/> する。取得の途中で失敗しても、
    /// それまでに記録した GameObject は同じ位置で破棄される。
    /// </para>
    /// </summary>
    internal sealed class BootstrapUiCommonLoader
    {
        private GameObject? _uiCommonObject;
        private GameObject? _eventSystemObject;

        /// <summary>LoadAsync でロードした UICommon シーンのハンドル。</summary>
        private ISceneHandle? _uiSceneHandle;

        internal async UniTask<BootstrapUiCommonLoadResult> LoadAsync(IAssetManagement assetManagement, BootstrapUiCommonSource source)
        {
            // active な Scene の検査は今までと同じ（名前が UICommon で、root UICommon に component がある）。
            // 借用するかは規則と合わせて policy が決める。遊べる Player は UICommon を持つ Scene から始まったら借用せずに失敗する。
            UICommon? borrowed = null;
            var activeScene = SceneManager.GetActiveScene();
            if (activeScene.name == "UICommon")
            {
                var tempGo = activeScene.GetRootGameObjects().FirstOrDefault(g => g.name == "UICommon");
                if (tempGo != null)
                    borrowed = tempGo.GetComponent<UICommon>();
            }

            var decision = BootstrapSourcePolicy.DecideUiCommon(source.Rule, borrowed != null, source.UseContentDirectory);
            if (decision.Failure != null)
                throw new InvalidOperationException(decision.Failure);
            if (decision.Origin == BootstrapUiCommonOrigin.ActiveScene)
                return new BootstrapUiCommonLoadResult(borrowed!, BootstrapUiCommonOrigin.ActiveScene);

            string bootstrapIdentity;
            if (decision.Origin == BootstrapUiCommonOrigin.Content)
            {
                var logicalKey = source.DirectorySceneLogicalKey();
                if (string.IsNullOrEmpty(logicalKey))
                    throw new InvalidOperationException("Content directory startup requires a bootstrap Scene logical key.");
                bootstrapIdentity = logicalKey;
                _uiSceneHandle = await assetManagement.LoadContentSceneAsync(
                    logicalKey,
                    "Full",
                    new SceneLoadOptions(LoadSceneMode.Additive, activateOnLoad: true, priority: 100));
            }
            else
            {
                bootstrapIdentity = source.UICommonPrefabAddress();
                // UICommon は SceneDirector 管理外の App 常駐シーン。ReleaseAppAll で解放される
                // bool / int / SceneReleaseMode の並びを位置引数で固定しないとオーバーロードが曖昧になる
                var desc = new SceneAssetDescription();
                desc.AddPayload(string.Empty, new AssetReference(bootstrapIdentity));
                _uiSceneHandle = await assetManagement.LoadSceneAsync(
                    "UICommon",
                    desc,
                    string.Empty,
                    new SceneLoadOptions(LoadSceneMode.Additive, activateOnLoad: true, priority: 100));
            }

            var go = _uiSceneHandle.GetRootGameObjects().FirstOrDefault();
            if (go == null)
            {
                throw new InvalidOperationException($"UICommon scene has no root object: {bootstrapIdentity}");
            }
            go.name = "[UICommon]";
            _uiCommonObject = go;

            var uiCommon = go.GetComponent<UICommon>();
            if (uiCommon == null)
            {
                throw new InvalidOperationException(
                    $"UICommon component not found on prefab: {bootstrapIdentity}");
            }

            EnsureEventSystem();

            return new BootstrapUiCommonLoadResult(uiCommon, decision.Origin);
        }

        /// <summary>
        /// 所有する UICommon の GameObject と、自分が作った EventSystem の GameObject を破棄する。2 回呼んでも安全。
        /// 破棄の例外はここで握らない（initializer の ReleaseAll と同じく呼び出し元へ出す）。
        /// </summary>
        internal void Dispose()
        {
            if (_uiCommonObject != null)
            {
                UnityEngine.Object.Destroy(_uiCommonObject);
                _uiCommonObject = null;
            }

            _uiSceneHandle = null;

            if (_eventSystemObject != null)
            {
                UnityEngine.Object.Destroy(_eventSystemObject);
                _eventSystemObject = null;
            }
        }

        private void EnsureEventSystem()
        {
            if (!Application.isPlaying) return;
            if (EventSystem.current != null) return;

            var go = new GameObject("[EventSystem]");
            go.AddComponent<EventSystem>();
            go.AddComponent<InputSystemUIInputModule>();
            UnityEngine.Object.DontDestroyOnLoad(go);
            _eventSystemObject = go;
        }
    }
}
