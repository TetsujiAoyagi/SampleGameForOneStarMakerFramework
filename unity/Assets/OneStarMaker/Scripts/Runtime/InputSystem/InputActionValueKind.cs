#nullable enable

namespace OneStarMaker.Runtime.InputSystem
{
    /// <summary>
    /// 公開するアクション値の形。物理コントロールの型名ではなく、消費者が読む値の種類である。
    /// Vector3 と Quaternion は既存アセットに残るが、このスライスの公開形には入れない。
    /// </summary>
    public enum InputActionValueKind : byte
    {
        /// <summary>現在の押下量。X が 0 から 1、Y は 0。</summary>
        Button = 0,

        /// <summary>現在の二次元量。X と Y。</summary>
        Axis2 = 1,
    }
}
