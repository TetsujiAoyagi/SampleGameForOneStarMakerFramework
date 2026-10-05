#nullable enable

using System;

namespace OneStarMaker.Runtime.InputSystem
{
    /// <summary>
    /// デバイス側の現在値を、呼び出し側が渡したバッファへ書く。
    /// 毎フレームの実装は割り当てない。AI の代理入力はこのインターフェースへ流さない。
    /// </summary>
    internal interface IInputDeviceReader
    {
        void Read(Span<InputActionValue> destination);
    }
}
