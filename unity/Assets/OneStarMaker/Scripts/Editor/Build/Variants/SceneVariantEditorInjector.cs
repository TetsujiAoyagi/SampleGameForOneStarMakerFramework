#nullable enable

using OneStarMaker.Runtime.AssetManagement;
using UnityEditor;

namespace OneStarMaker.Editor.Build
{
    /// <summary>active profile query を Runtime bridge へ domain load ごとに登録する。</summary>
    [InitializeOnLoad]
    internal static class SceneVariantEditorInjector
    {
        static SceneVariantEditorInjector()
        {
            Install();
        }

        internal static void Install()
        {
            SceneVariantRuntimeBridge.EditorSceneVariantResolver =
                () => DeveloperVariantSettings.instance.GetActiveSceneVariant();
        }
    }
}
