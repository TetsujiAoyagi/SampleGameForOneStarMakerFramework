#nullable enable

using System;
using OneStarMaker.Runtime.CameraSystem.Abstractions;
using OneStarMaker.Runtime.CameraSystem.Core;
using OneStarMaker.Runtime.CameraSystem.Effects;
using OneStarMaker.Runtime.CameraSystem.Geometry;
using OneStarMaker.Runtime.CameraSystem.Hosting;
using OneStarMaker.Runtime.CameraSystem.Modifiers;
using OneStarMaker.Runtime.CameraSystem.Stacking;
using OneStarMaker.Runtime.CameraSystem.Telemetry;

namespace OneStarMaker.Runtime.CameraSystem.Telemetry
{
    /// <summary>
    /// カメラ Id 文字列を決定的な int hash へ変換する。テレメトリで生文字列を送らずに済ませ、
    /// 実行間で安定した比較（同一 Id → 同一 hash）を保証するために FNV-1a を採用する。
    /// </summary>
    internal static class CameraTelemetryHash
    {
        // -1 は metadata 側で「未設定」を表す sentinel。hash がたまたま -1 になった場合のみ衝突回避で置換する。
        private const int UnsetSentinel = -1;
        private const int SentinelCollisionReplacement = int.MinValue;

        /// <summary>
        /// FNV-1a で cameraId の決定的 hash を計算する。結果が未設定 sentinel(-1) と一致する場合のみ int.MinValue へ退避する。
        /// </summary>
        internal static int ComputeActiveCameraIdHash(string cameraId)
        {
            if (cameraId == null)
            {
                throw new ArgumentNullException(nameof(cameraId));
            }

            unchecked
            {
                const uint fnvOffsetBasis = 2166136261;
                const uint fnvPrime = 16777619;
                var hash = fnvOffsetBasis;
                for (var i = 0; i < cameraId.Length; i++)
                {
                    hash ^= cameraId[i];
                    hash *= fnvPrime;
                }

                var result = (int)hash;
                return result == UnsetSentinel ? SentinelCollisionReplacement : result;
            }
        }
    }
}
