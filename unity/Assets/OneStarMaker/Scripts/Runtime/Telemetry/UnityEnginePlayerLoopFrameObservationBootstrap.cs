#nullable enable

using System.Threading;
using OneStarMaker.Foundation.Telemetry;
using UnityEngine;

namespace OneStarMaker.Runtime.Telemetry
{
    /// <summary>
    /// UnityEngine の main thread / <see cref="Time.frameCount"/> を
    /// <see cref="UnityPlayerLoopFrameObservation"/> へ bind する bootstrap。
    /// </summary>
    internal static class UnityEnginePlayerLoopFrameObservationBootstrap
    {
        private static int s_mainThreadManagedId;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void RegisterObservation()
        {
            s_mainThreadManagedId = Thread.CurrentThread.ManagedThreadId;
            UnityPlayerLoopFrameObservation.Register(TryGetMainThreadFrameCount);
        }

        /// <summary>
        /// main thread の player-loop frame のみ返す。それ以外は null（未観測）。
        /// </summary>
        private static int? TryGetMainThreadFrameCount()
        {
            if (Thread.CurrentThread.ManagedThreadId != s_mainThreadManagedId)
            {
                return null;
            }

            return Time.frameCount;
        }
    }
}
