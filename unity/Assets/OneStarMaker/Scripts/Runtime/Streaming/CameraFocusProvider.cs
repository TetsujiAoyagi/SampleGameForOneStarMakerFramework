#nullable enable

using System;
using System.Collections.Generic;
using OneStarMaker.Runtime.CameraSystem;
using UnityEngine;
using OneStarMaker.Runtime.CameraSystem.Abstractions;
using OneStarMaker.Runtime.CameraSystem.Core;
using OneStarMaker.Runtime.CameraSystem.Effects;
using OneStarMaker.Runtime.CameraSystem.Geometry;
using OneStarMaker.Runtime.CameraSystem.Hosting;
using OneStarMaker.Runtime.CameraSystem.Modifiers;
using OneStarMaker.Runtime.CameraSystem.Stacking;
using OneStarMaker.Runtime.CameraSystem.Telemetry;

namespace OneStarMaker.Runtime.Streaming
{
    /// <summary>
    /// CameraSystem の View 群から SceneStreaming 向け注視点を抽出する純 C# アダプタ（正典 §9）。
    /// Cinemachine / CameraSystemHost には依存せず、<see cref="ICameraView"/> の Snapshot のみを読む。
    /// </summary>
    public sealed class CameraFocusProvider
    {
        /// <summary>
        /// 登録済み View 群から注視点座標を収集する。
        /// 各 View の Snapshot 位置に加え、ブレンド中は IncomingSnapshot 位置も先読みで含める（F-8）。
        /// </summary>
        /// <param name="sources">抽出対象。RT ミニマップ等は <see cref="CameraFocusSource.IncludeInStreaming"/> で除外する。</param>
        /// <returns>収集した注視点（順序は sources の走査順）。</returns>
        public IReadOnlyList<Vector3> CollectFocusPositions(IReadOnlyList<CameraFocusSource> sources)
        {
            if (sources is null)
            {
                throw new ArgumentNullException(nameof(sources));
            }

            var result = new List<Vector3>(sources.Count);

            for (var i = 0; i < sources.Count; i++)
            {
                var source = sources[i];
                if (!source.IncludeInStreaming)
                {
                    continue;
                }

                if (source.View is null)
                {
                    throw new ArgumentException("View は null にできません。", nameof(sources));
                }

                result.Add(source.View.Snapshot.Pose.Position);

                // ブレンド先 POV は完了前からストリーミング先読みの入力になる（I-3: Snapshot 確定後の値を読む前提）。
                if (source.View.IncomingSnapshot is { } incoming)
                {
                    result.Add(incoming.Pose.Position);
                }
            }

            return result;
        }
    }

    /// <summary>
    /// 注視点抽出対象の View とストリーミング包含フラグのペア。
    /// <see cref="ICameraView"/> だけでは RT 出力かどうか判定できないため、呼び出し側が包含可否を渡す。
    /// </summary>
    public readonly struct CameraFocusSource
    {
        /// <summary>注視点の取得元 View。</summary>
        public ICameraView View { get; init; }

        /// <summary>
        /// <see langword="true"/> のとき Collect に含める。ミニマップ等の RT View は false にする。
        /// </summary>
        public bool IncludeInStreaming { get; init; }
    }
}
