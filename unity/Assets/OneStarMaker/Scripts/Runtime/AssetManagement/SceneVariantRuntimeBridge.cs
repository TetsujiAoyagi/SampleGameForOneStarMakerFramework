#nullable enable

using System;

namespace OneStarMaker.Runtime.AssetManagement
{
    /// <summary>
    /// Editor の active build profile から起動時 Scene Variant を問い合わせるための橋渡し。
    /// 値は保持せず、Player では参照されない。
    /// </summary>
    public static class SceneVariantRuntimeBridge
    {
        /// <summary>
        /// null は active profile 未選択、空文字は default Variant の明示選択を表す。
        /// </summary>
        public static Func<string?>? EditorSceneVariantResolver { get; set; }
    }
}
