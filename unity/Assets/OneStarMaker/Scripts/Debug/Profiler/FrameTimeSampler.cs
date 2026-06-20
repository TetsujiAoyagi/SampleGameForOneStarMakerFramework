#nullable enable

using UnityEngine;
using UnityEngine.Rendering;

namespace OneStarMaker.Debug
{
    /// <summary>
    /// 毎フレーム <see cref="FrameTimingManager"/> から CPU / GPU 時間 (ms) を取得し、
    /// リングバッファに蓄積する。1 秒間隔でサマリ (avg / min / max) を計算する。
    /// </summary>
    public sealed class FrameTimeSampler
    {
        // ── リングバッファ ──
        private readonly float[] _cpuBuffer;
        private readonly float[] _gpuBuffer;
        private int _head;
        private int _count;

        // ── サマリ計算用 ──
        private float _summaryTimer;
        private int _summaryFrameCount;
        private float _cpuSum, _cpuMin, _cpuMax;
        private float _gpuSum, _gpuMin, _gpuMax;

        // ── FrameTimingManager 用 ──
        private readonly FrameTiming[] _timings = new FrameTiming[1];

        /// <summary>リングバッファの容量（フレーム数）。</summary>
        public int Capacity { get; }

        /// <summary>現在蓄積されているフレーム数。</summary>
        public int Count => _count;

        /// <summary>GPU 計測が有効かどうか。プラットフォーム非対応時は false。</summary>
        public bool IsGpuTimingAvailable { get; private set; } = true;

        // ── 最新サマリ（1 秒ごとに更新） ──
        public float CpuAvgMs { get; private set; }
        public float CpuMinMs { get; private set; }
        public float CpuMaxMs { get; private set; }
        public float GpuAvgMs { get; private set; }
        public float GpuMinMs { get; private set; }
        public float GpuMaxMs { get; private set; }

        /// <summary>最新サマリが更新されたとき true になる。読み取り後にリセットすること。</summary>
        public bool SummaryUpdated { get; set; }

        public FrameTimeSampler(int capacity = 300)
        {
            Capacity = capacity;
            _cpuBuffer = new float[capacity];
            _gpuBuffer = new float[capacity];
            ResetSummaryAccumulators();
        }

        /// <summary>
        /// 毎フレーム呼び出す。FrameTimingManager からデータを取得しバッファに格納する。
        /// </summary>
        public void Sample()
        {
            FrameTimingManager.CaptureFrameTimings();
            uint count = FrameTimingManager.GetLatestTimings(1, _timings);

            float cpuMs;
            float gpuMs;

            if (count > 0)
            {
                cpuMs = (float)_timings[0].cpuFrameTime;
                gpuMs = (float)_timings[0].gpuFrameTime;

                // GPU 非対応環境では gpuFrameTime が 0 のまま
                if (gpuMs <= 0f)
                {
                    IsGpuTimingAvailable = false;
                    gpuMs = 0f;
                }
            }
            else
            {
                // FrameTimingManager 非対応
                cpuMs = Time.unscaledDeltaTime * 1000f;
                gpuMs = 0f;
                IsGpuTimingAvailable = false;
            }

            // リングバッファ書き込み
            _cpuBuffer[_head] = cpuMs;
            _gpuBuffer[_head] = gpuMs;
            _head = (_head + 1) % Capacity;
            if (_count < Capacity) _count++;

            // サマリ蓄積
            _cpuSum += cpuMs;
            _cpuMin = Mathf.Min(_cpuMin, cpuMs);
            _cpuMax = Mathf.Max(_cpuMax, cpuMs);
            _gpuSum += gpuMs;
            _gpuMin = Mathf.Min(_gpuMin, gpuMs);
            _gpuMax = Mathf.Max(_gpuMax, gpuMs);
            _summaryFrameCount++;

            _summaryTimer += Time.unscaledDeltaTime;
            if (_summaryTimer >= 1f)
            {
                FlushSummary();
            }
        }

        /// <summary>
        /// リングバッファから指定インデックスの値を取得する。
        /// index=0 が最も古いデータ、index=Count-1 が最新。
        /// </summary>
        public void GetSample(int index, out float cpuMs, out float gpuMs)
        {
            int actualIndex = (_head - _count + index + Capacity) % Capacity;
            cpuMs = _cpuBuffer[actualIndex];
            gpuMs = _gpuBuffer[actualIndex];
        }

        private void FlushSummary()
        {
            if (_summaryFrameCount > 0)
            {
                CpuAvgMs = _cpuSum / _summaryFrameCount;
                CpuMinMs = _cpuMin;
                CpuMaxMs = _cpuMax;
                GpuAvgMs = _gpuSum / _summaryFrameCount;
                GpuMinMs = _gpuMin;
                GpuMaxMs = _gpuMax;
                SummaryUpdated = true;
            }

            ResetSummaryAccumulators();
        }

        private void ResetSummaryAccumulators()
        {
            _summaryTimer = 0f;
            _summaryFrameCount = 0;
            _cpuSum = 0f;
            _cpuMin = float.MaxValue;
            _cpuMax = 0f;
            _gpuSum = 0f;
            _gpuMin = float.MaxValue;
            _gpuMax = 0f;
        }
    }
}
