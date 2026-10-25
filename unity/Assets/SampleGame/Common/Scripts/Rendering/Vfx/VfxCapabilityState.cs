#nullable enable

namespace SampleGame.Common.Rendering.Vfx
{
    public enum VfxCapability
    {
        /// <summary>backend がまだ判定していない、または判定した backend が Dispose された。非対応と同じに扱う。</summary>
        Undetermined = 0,
        Supported = 1,
        Unsupported = 2,
    }

    /// <summary>
    /// VFX Graph の能力判定の結果の、唯一の出所。<see cref="VfxGraphBackend"/> の能力判定だけが書き、
    /// <see cref="PlacedVfxActivator"/> は読むだけで自分では判定しない（判定を一か所に保ち、結果の食い違いを作らない）。
    /// <para>
    /// static にするのは、Scene の deserialize で生成される MonoBehaviour に composition から注入できないため。
    /// 結果は BeforeSceneLoad で公開済みになる（Framework が VfxSystem を作るときに判定する）。
    /// </para>
    /// <para>
    /// 最後に書いた backend（writer）を持つ。後から書いた backend が勝ち、writer の Dispose だけが <see cref="VfxCapability.Undetermined"/>
    /// に戻す。Play 中のテストが本番と別の backend を作っても、テストの backend の Dispose が本番以外の値を残さず、
    /// 本番以外の backend の Dispose が本番の値を消さない。
    /// </para>
    /// </summary>
    public static class VfxCapabilityState
    {
        private static object? s_writer;

        public static VfxCapability Current { get; private set; }

        internal static void Publish(object writer, VfxCapability value)
        {
            s_writer = writer;
            Current = value;
        }

        /// <summary><paramref name="writer"/> が最後に書いた backend のときだけ未判定に戻す。</summary>
        internal static void ResetIfWriter(object writer)
        {
            if (!ReferenceEquals(s_writer, writer))
            {
                return;
            }

            s_writer = null;
            Current = VfxCapability.Undetermined;
        }
    }
}
