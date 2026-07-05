#nullable enable

using UnityEditor;

namespace OneStarMaker.Editor.Build
{
    /// <summary>
    /// Editor 起動時に Runtime 側のリモートカタログ URL 解決子へ、
    /// 開発者ローカル設定を注入する。
    /// </summary>
    /// <remarks>
    /// <para>
    /// Unity のアセンブリ分離により、Runtime (OneStarMaker.Runtime) は
    /// Editor (OneStarMaker.Editor) を参照できない。
    /// そのため Runtime 側には <see cref="OneStarMaker.Runtime.AssetManagement.RemoteCatalogRuntimeBridge.EditorRemoteCatalogUrlResolver"/>
    /// という静的デリゲートのみを用意し、Editor 側が <see cref="InitializeOnLoadAttribute"/> の
    /// 静的コンストラクタで本クラスから代入する。
    /// </para>
    /// <para>
    /// これにより Play Mode 起動時も、Project Settings で選択中の
    /// <see cref="BuildVariantProfile"/> の <see cref="BuildVariantProfile.RemoteCatalogUrl"/> が
    /// Addressables リモートフォールバックに使われる。
    /// </para>
    /// </remarks>
    [InitializeOnLoad]
    internal static class RemoteCatalogEditorInjector
    {
        /// <summary>
        /// Editor アセンブリロード時に Runtime ブリッジへ解決子を登録する。
        /// </summary>
        static RemoteCatalogEditorInjector()
        {
            OneStarMaker.Runtime.AssetManagement.RemoteCatalogRuntimeBridge.EditorRemoteCatalogUrlResolver =
                () => DeveloperVariantSettings.instance.GetActiveRemoteCatalogUrl();
        }
    }
}
