#nullable enable

using UnityEngine;
using UnityEngine.VFX;

namespace SampleGame.Common.Rendering.Vfx
{
    /// <summary>
    /// VFX Graph の 1 フレームの進みの上限（<c>VFXManager.maxDeltaTime</c>）を、実行時の <c>Time.maximumDeltaTime</c> に
    /// 揃える書き換えの所有者。<see cref="VfxGraphBackend"/> だけが使う。
    /// <para>
    /// 揃える理由: VFX Graph は 1 フレームに <c>min(unscaledDeltaTime, maxDeltaTime) × playRate</c> だけ進み、Unity の
    /// <c>Time.deltaTime</c> は <c>min(unscaledDeltaTime, maximumDeltaTime) × timeScale</c> になる。上限が違うと、その間の
    /// フレーム（既定では 0.05 秒より長く 1/3 秒以下）で Framework の倍率が変わらず playRate が書き直されないので、
    /// VFX だけが上限で止まり gameplay とずれる。揃えると倍率だけで全フレームの進みが一致する。
    /// </para>
    /// <para>
    /// 所有と寿命: 書き換えはプロセス全体の設定なので、backend の数を数える。最初に取った backend が書き換える前の値を
    /// 保存して揃え、最後に手放した backend が元に戻す（backend が重なっても、生存中は揃えた値のまま）。
    /// アプリの終了（Editor では Play Mode の終了）では、手放されていなくても元に戻し、書き換えを project 設定に残さない。
    /// 終了の後に残っていた backend の手放しは、古い世代として無視する（新しい世代の数を減らさないため）。
    /// </para>
    /// <para>
    /// 影響範囲と前提: <c>VFXManager.maxDeltaTime</c> は VfxSystem の外のものも含め、プロセス内の全ての VisualEffect に効く。
    /// <c>Time.maximumDeltaTime</c> は最初に揃えた時点の値を写すだけで、実行中の変更には追従しない（VFX の時間の一致は、
    /// <c>Time.maximumDeltaTime</c> を backend の生成より前に決め、実行中は変えないことを前提にする）。値は手で書かない
    /// （0.333 と 1/3 のような食い違いを作らないため）。project 設定（VFXManager.asset）は変えない。
    /// </para>
    /// </summary>
    internal static class VfxGraphMaxDeltaTimeAlignment
    {
        private static int _holders;
        private static int _generation;
        private static float _original;
        private static bool _quittingHooked;

        /// <summary>テスト用: 揃えた値を持っている backend の数。</summary>
        internal static int HoldersForTest => _holders;

        /// <summary>
        /// 揃えた値を 1 つ持つ。戻り値は <see cref="Release"/> に渡す世代。最初の 1 つが元の値を保存して書き換える。
        /// </summary>
        internal static int Acquire()
        {
            if (!_quittingHooked)
            {
                Application.quitting += RestoreOnQuitting;
                _quittingHooked = true;
            }

            if (_holders == 0)
            {
                _original = VFXManager.maxDeltaTime;
                VFXManager.maxDeltaTime = Time.maximumDeltaTime;
            }

            _holders++;
            return _generation;
        }

        /// <summary>
        /// <see cref="Acquire"/> で持った 1 つを手放す。最後の 1 つなら元の値に戻す。終了で戻した後の古い世代は何もしない。
        /// </summary>
        internal static void Release(int generation)
        {
            if (generation != _generation || _holders == 0)
            {
                return;
            }

            _holders--;
            if (_holders == 0)
            {
                VFXManager.maxDeltaTime = _original;
            }
        }

        private static void RestoreOnQuitting()
        {
            if (_holders > 0)
            {
                VFXManager.maxDeltaTime = _original;
            }

            _holders = 0;
            _generation++;
        }
    }
}
