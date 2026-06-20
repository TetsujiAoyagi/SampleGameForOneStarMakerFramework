#nullable enable

using UnityEngine;

namespace OneStarMaker.Debug
{
    /// <summary>
    /// <see cref="FrameTimeSampler"/> のリングバッファを <see cref="Texture2D"/> に描画する。
    /// CPU = 緑、GPU = 青。16ms / 33ms にターゲットラインを描画する。
    /// </summary>
    public sealed class FrameTimeGraphRenderer
    {
        private const float MaxMs = 50f;
        private const float TargetLine60Fps = 16.67f;
        private const float TargetLine30Fps = 33.33f;

        private static readonly Color ClearColor = new(0f, 0f, 0f, 0.5f);
        private static readonly Color CpuColor = new(0.2f, 1f, 0.2f, 0.9f);      // 緑
        private static readonly Color GpuColor = new(0.3f, 0.6f, 1f, 0.9f);       // 青
        private static readonly Color TargetLineColor = new(1f, 1f, 0f, 0.4f);    // 黄 半透明
        private static readonly Color OverBudgetColor = new(1f, 0.2f, 0.2f, 0.7f); // 赤

        private readonly Texture2D _texture;
        private readonly Color[] _clearPixels;
        private readonly int _width;
        private readonly int _height;

        public Texture2D Texture => _texture;

        /// <summary>
        /// グラフ用テクスチャを生成する。
        /// </summary>
        /// <param name="width">テクスチャ幅（≒表示フレーム数）。</param>
        /// <param name="height">テクスチャ高さ。</param>
        public FrameTimeGraphRenderer(int width, int height)
        {
            _width = width;
            _height = height;
            _texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };

            _clearPixels = new Color[width * height];
            for (int i = 0; i < _clearPixels.Length; i++)
                _clearPixels[i] = ClearColor;
        }

        /// <summary>
        /// サンプラーの現在のバッファ内容でテクスチャを再描画する。
        /// </summary>
        public void Render(FrameTimeSampler sampler)
        {
            // クリア
            _texture.SetPixels(_clearPixels);

            int sampleCount = sampler.Count;
            int drawCount = Mathf.Min(sampleCount, _width);

            // ターゲットライン描画
            DrawHorizontalLine(TargetLine60Fps, TargetLineColor);
            DrawHorizontalLine(TargetLine30Fps, TargetLineColor);

            // サンプル描画（右端が最新）
            int sampleOffset = sampleCount - drawCount;
            for (int x = 0; x < drawCount; x++)
            {
                sampler.GetSample(sampleOffset + x, out float cpuMs, out float gpuMs);

                // GPU バー（背面）
                if (sampler.IsGpuTimingAvailable && gpuMs > 0f)
                {
                    int gpuHeight = MsToPixel(gpuMs);
                    DrawBar(x, gpuHeight, gpuMs > TargetLine30Fps ? OverBudgetColor : GpuColor);
                }

                // CPU バー（前面、半透明で重ねる）
                int cpuHeight = MsToPixel(cpuMs);
                DrawBar(x, cpuHeight, cpuMs > TargetLine30Fps ? OverBudgetColor : CpuColor);
            }

            _texture.Apply();
        }

        private int MsToPixel(float ms)
        {
            return Mathf.Clamp(Mathf.RoundToInt(ms / MaxMs * _height), 0, _height);
        }

        private void DrawBar(int x, int height, Color color)
        {
            for (int y = 0; y < height; y++)
            {
                _texture.SetPixel(x, y, color);
            }
        }

        private void DrawHorizontalLine(float ms, Color color)
        {
            int y = MsToPixel(ms);
            if (y <= 0 || y >= _height) return;

            for (int x = 0; x < _width; x++)
            {
                _texture.SetPixel(x, y, color);
            }
        }

        /// <summary>テクスチャリソースを破棄する。</summary>
        public void Dispose()
        {
            if (_texture != null)
                Object.Destroy(_texture);
        }
    }
}
