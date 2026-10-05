#nullable enable

namespace OneStarMaker.Runtime.InputSystem
{
    /// <summary>
    /// マネージャが要求するアクションマップ名。
    /// ゲーム固有のアクション名ではない。既存の InputSystem_Actions が既に持つ Player と UI である。
    /// </summary>
    public static class InputActionMapNames
    {
        public const string Player = "Player";

        public const string UI = "UI";
    }
}
