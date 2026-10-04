#nullable enable

using OneStarMaker.Runtime.SceneSystem;

namespace OneStarMaker.Runtime.InputSystem
{
    /// <summary>
    /// ユーザー操作を公開してよいか。SceneState のコメントどおり Stable だけが受理状態である。
    /// 未定義の整数も受理しない。
    /// </summary>
    internal static class InputAcceptance
    {
        public static bool IsAccepting(SceneState state)
        {
            return state == SceneState.Stable;
        }
    }
}
