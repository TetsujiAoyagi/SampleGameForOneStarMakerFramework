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
    /// initializer の virtual（<c>UseRequiredPlayerFileConfiguration</c> と 2 つの hook）は値ではなく関数で受け、
    /// loader の中の、initializer にあったときと同じ時点・同じ分岐でだけ評価する。先に評価して mode を確定すると、
    /// 派生クラスの getter / hook が呼ばれる回数と時点が変わる（ロード済み UICommon の経路では 1 つも呼ばない）。
    /// directory か否かだけは副作用の無い field の読み取りなので、呼び出し時の bool で受ける。
    /// </para>
    /// </summary>
    internal readonly struct BootstrapUiCommonSource
    {
        internal BootstrapUiCommonSource(
            bool useContentDirectory,
            Func<bool> useRequiredPlayerFileConfiguration,
            Func<string> directorySceneLogicalKey,
            Func<string> uiCommonPrefabAddress)
        {
            UseContentDirectory = useContentDirectory;
            UseRequiredPlayerFileConfiguration = useRequiredPlayerFileConfiguration
                ?? throw new ArgumentNullException(nameof(useRequiredPlayerFileConfiguration));
            DirectorySceneLogicalKey = directorySceneLogicalKey
                ?? throw new ArgumentNullException(nameof(directorySceneLogicalKey));
            UICommonPrefabAddress = uiCommonPrefabAddress ?? throw new ArgumentNullException(nameof(uiCommonPrefabAddress));
        }

        internal bool UseContentDirectory { get; }
        internal Func<bool> UseRequiredPlayerFileConfiguration { get; }
        internal Func<string> DirectorySceneLogicalKey { get; }
        internal Func<string> UICommonPrefabAddress { get; }
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

        internal async UniTask<UICommon> LoadAsync(IAssetManagement assetManagement, BootstrapUiCommonSource source)
        {
            UICommon? uiCommon = null;

            var activeScene = SceneManager.GetActiveScene();
            if(activeScene.name == "UICommon")
            {
                // 既に UICommon シーンがロードされている場合は、そこから UICommon を探す
                var tempGo = activeScene.GetRootGameObjects().FirstOrDefault(g => g.name == "UICommon");
                if (tempGo != null)
                {
                    uiCommon = tempGo.GetComponent<UICommon>();
                    if (uiCommon != null)
                    {
                        return uiCommon;
                    }
                }
            }

            if (source.UseRequiredPlayerFileConfiguration())
                throw new InvalidOperationException("The Player bootstrap scene must contain UICommon; Addressables fallback is forbidden.");

            string bootstrapIdentity;
            if (source.UseContentDirectory)
            {
                var logicalKey = source.DirectorySceneLogicalKey();
                if (string.IsNullOrEmpty(logicalKey))
                    throw new InvalidOperationException("Directory Editor Play requires a bootstrap Scene logical key.");
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

            uiCommon = go.GetComponent<UICommon>();
            if (uiCommon == null)
            {
                throw new InvalidOperationException(
                    $"UICommon component not found on prefab: {bootstrapIdentity}");
            }

            EnsureEventSystem();

            return uiCommon;
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
