#nullable enable

namespace OneStarMaker.Runtime.InputSystem
{
    /// <summary>
    /// Player と UI のどちらか一方のアクションマップだけを有効にする。
    /// 公開バッファの零化とは別で、デバイス側の有効マップを切り替える。
    /// </summary>
    internal interface IInputMapControl
    {
        void Apply(InputControlMap map);

        void Disable();
    }
}
