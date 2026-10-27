#nullable enable

using System;
using OneStarMaker.Runtime.UpdateSystem.Api;

namespace OneStarMaker.Runtime.Rendering.Vfx
{
    /// <summary>
    /// VFX の時間の領域についての計算だけを持つ（状態を持たない）。UpdateSystem の時間の仕様への追従はここに閉じる。
    /// <para>
    /// 領域の時間（domain delta）は、そのフレームにその領域の Layer の要素が受け取る delta と同じ値にする:
    /// gameplay 等の Layer の領域では <c>IsPaused ? 0 : Time.deltaTime × TimeScale</c>、unscaled の領域では
    /// <c>min(Time.unscaledDeltaTime, Time.maximumDeltaTime)</c>。<c>Time.deltaTime</c> は <c>maximumDeltaTime</c> で
    /// 頭打ちになり、<c>unscaledDeltaTime</c> はならないので、負荷スパイクのフレームでは倍率（rate）が下がる。
    /// これで gameplay と同じ量だけ VFX が進む。
    /// </para>
    /// <para>
    /// unscaled の領域は、Unity が頭打ちにした時間から timeScale だけを外したものと定義する
    /// （Unity は <c>Time.deltaTime = min(unscaled, maximumDeltaTime) × timeScale</c> とし、頭打ちは timeScale を掛ける前に効く）。
    /// したがって unscaled の VFX も 1 フレームに <c>maximumDeltaTime</c> より先へは進まず、実時間の長い停止を
    /// またいで一気に進めることはしない。実時間の delta が 0・負・NaN・∞ のフレームでは、unscaled の領域の時間は 0
    /// （消え切り待ちは進まない）。
    /// </para>
    /// <para>
    /// 倍率は「このフレーム、effect が実時間に対して何倍で進むべきか」で、領域の時間 ÷ 実時間の delta とする。
    /// 実時間の delta が 0・負・NaN・∞ のフレームと、止まっているフレームでは計算できない（または意味が無い）ので、
    /// 前回の倍率を保つ。止まっていることは倍率ではなく paused で伝える（IsPaused と TimeScale == 0 は区別する）。
    /// </para>
    /// </summary>
    internal static class VfxTimeLayerBinding
    {
        /// <summary>backend が作った・登録した instance の初期の再生状態（backend の契約）。</summary>
        internal const float InitialRate = 1f;

        /// <summary>
        /// 所属先として受け付けられない Layer id なら <see cref="ArgumentException"/> を投げる。null は unscaled で受け付ける。
        /// </summary>
        internal static void ValidateTimeLayerId(string? timeLayerId, string parameterName)
        {
            if (timeLayerId == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(timeLayerId))
            {
                throw new ArgumentException("A time layer id must not be empty. Use null for unscaled time.", parameterName);
            }

            // Vfx Layer は反映を担うので時間の領域にならない（pause すると全 VFX への反映が止まる）。
            if (string.Equals(timeLayerId.Trim(), UpdateLayerIds.Vfx, StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    $"The '{UpdateLayerIds.Vfx}' layer applies VFX playback and is not a time domain.",
                    parameterName);
            }
        }

        /// <summary>
        /// このフレームの領域の状態。<paramref name="timeLayerId"/> が null なら unscaled（止まらない）。
        /// 時間源が無い・Layer がまだ無い場合は「止まっていない、倍率 1」とみなす。
        /// </summary>
        internal static void EvaluateDomain(
            IVfxTimeSource? source,
            string? timeLayerId,
            out bool paused,
            out float domainDelta)
        {
            if (source == null)
            {
                paused = false;
                domainDelta = 0f;
                return;
            }

            if (timeLayerId == null)
            {
                paused = false;
                domainDelta = UnscaledDomainDelta(source.UnscaledDeltaTime, source.MaximumDeltaTime);
                return;
            }

            if (source.TryGetLayerState(timeLayerId, out var isPaused, out var timeScale))
            {
                paused = isPaused;
                domainDelta = isPaused ? 0f : source.DeltaTime * timeScale;
                return;
            }

            paused = false;
            domainDelta = source.DeltaTime;
        }

        /// <summary>
        /// このフレームの倍率。計算できないフレーム（止まっている、実時間の delta か結果が 0 未満・NaN・∞）は
        /// <paramref name="previousRate"/> を返す。
        /// </summary>
        internal static float ComputeRate(bool paused, float domainDelta, float unscaledDeltaTime, float previousRate)
        {
            if (paused || !IsUsableDelta(unscaledDeltaTime))
            {
                return previousRate;
            }

            var rate = domainDelta / unscaledDeltaTime;
            return rate >= 0f && !float.IsInfinity(rate) ? rate : previousRate;
        }

        /// <summary>
        /// unscaled の領域の時間。異常の判定は min より先に行う（<c>min(NaN, M)</c> は M になり、異常が隠れるため）。
        /// 上限が正でない（Unity では起きない）なら頭打ちにしない。
        /// </summary>
        private static float UnscaledDomainDelta(float unscaledDeltaTime, float maximumDeltaTime)
        {
            if (!IsUsableDelta(unscaledDeltaTime))
            {
                return 0f;
            }

            return maximumDeltaTime > 0f ? Math.Min(unscaledDeltaTime, maximumDeltaTime) : unscaledDeltaTime;
        }

        // NaN は比較が全て false になるので > 0 で落ちる。
        private static bool IsUsableDelta(float delta) => delta > 0f && !float.IsInfinity(delta);
    }
}
