#nullable enable

using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OneStarMaker.Editor.SceneGraph
{
    /// <summary>
    /// シーンを保存したら、その <see cref="OneStarMaker.Runtime.SceneSystem.SceneResource"/> と祖先の
    /// 体積を自動で焼き直す（34-ondemand-spatial-policy.md §5）。
    ///
    /// <para>
    /// 体積を「生成器が格子定数から焼いて埋め込む値」にすると、人が `.unity` を編集した瞬間に
    /// データが嘘になる。編集のたびに更新されるからこそ体積が正本でいられる。
    /// </para>
    /// </summary>
    [InitializeOnLoad]
    internal static class SceneVolumeSaveHook
    {
        static SceneVolumeSaveHook()
        {
            EditorSceneManager.sceneSaved -= OnSceneSaved;
            EditorSceneManager.sceneSaved += OnSceneSaved;
        }

        private static void OnSceneSaved(Scene scene)
        {
            if (SceneVolumeRecalculator.SaveHookSuspended)
            {
                return;
            }

            try
            {
                SceneVolumeRecalculator.RecalculateForSavedScene(scene);
            }
            catch (System.Exception ex)
            {
                // 保存フックから例外を投げると保存操作そのものが壊れて見える。
                // 全件再計算メニューが正本なので、ここは記録して落とさない。
                Debug.LogError(
                    $"[SceneVolumeSaveHook] '{scene.path}' の体積再計算に失敗しました。"
                    + $"'{SceneVolumeRecalculator.RecalculateAllMenuPath}' を実行してください: {ex}");
            }
        }
    }
}
