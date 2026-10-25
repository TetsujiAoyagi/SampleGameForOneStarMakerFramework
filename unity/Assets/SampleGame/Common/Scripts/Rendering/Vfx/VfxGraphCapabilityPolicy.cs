#nullable enable

using UnityEngine;
using UnityEngine.Rendering;

namespace SampleGame.Common.Rendering.Vfx
{
    /// <summary>VFX Graph の対応可否を決めるのに使う platform の事実。<see cref="VfxGraphBackend"/> が起動時に 1 回読む。</summary>
    public readonly struct VfxPlatformFacts
    {
        public VfxPlatformFacts(
            bool supportsComputeShaders,
            int maxComputeBufferInputsVertex,
            ColorSpace colorSpace,
            GraphicsDeviceType graphicsDeviceType)
        {
            SupportsComputeShaders = supportsComputeShaders;
            MaxComputeBufferInputsVertex = maxComputeBufferInputsVertex;
            ColorSpace = colorSpace;
            GraphicsDeviceType = graphicsDeviceType;
        }

        public bool SupportsComputeShaders { get; }

        /// <summary>頂点 shader から読める compute buffer（SSBO）の数。0 なら VFX Graph の出力が描けない。</summary>
        public int MaxComputeBufferInputsVertex { get; }

        public ColorSpace ColorSpace { get; }

        public GraphicsDeviceType GraphicsDeviceType { get; }
    }

    /// <summary>
    /// platform の事実から VFX Graph（URP）の対応可否を決める規則。Unity の状態を読まない純粋な関数にして、
    /// Mobile 実機の代わりに表駆動で確かめられるようにする。
    /// <para>
    /// 拒否リスト方式: compute 対応、SSBO 対応、Linear color space、graphics device が拒否リストに無い、の全てで対応とする。
    /// platform（Android / iOS 等）は見ない。VFX Graph の同梱文書の実行要件（compute・SSBO が必須、OpenGL ES 非対応、
    /// URP では Gamma 非対応）をそのまま規則にしたもので、個々の端末での動作保証ではない。
    /// </para>
    /// </summary>
    public static class VfxGraphCapabilityPolicy
    {
        public static bool Evaluate(in VfxPlatformFacts facts, out string? reason)
        {
            // graphics device が無い（-nographics の batch 等）を最初に見る。他の値は device が無いと意味を持たない。
            if (facts.GraphicsDeviceType == GraphicsDeviceType.Null)
            {
                reason = "No graphics device (GraphicsDeviceType.Null).";
                return false;
            }

            if (facts.GraphicsDeviceType == GraphicsDeviceType.OpenGLES3)
            {
                reason = "VFX Graph does not support OpenGL ES (GraphicsDeviceType.OpenGLES3).";
                return false;
            }

            if (!facts.SupportsComputeShaders)
            {
                reason = "Compute shaders are not supported.";
                return false;
            }

            if (facts.MaxComputeBufferInputsVertex <= 0)
            {
                reason = "Compute buffers are not readable from vertex shaders (maxComputeBufferInputsVertex == 0).";
                return false;
            }

            if (facts.ColorSpace != ColorSpace.Linear)
            {
                reason = "VFX Graph on URP requires the Linear color space (current: " + facts.ColorSpace + ").";
                return false;
            }

            reason = null;
            return true;
        }
    }
}
