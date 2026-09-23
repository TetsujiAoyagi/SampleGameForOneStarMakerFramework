#nullable enable

using System;
using System.Collections.Generic;
using OneStarMaker.Runtime.Rendering.Environments;
using UnityEngine;

namespace OneStarMaker.Tests.Rendering.Environments
{
    public sealed class FakeRenderEnvironmentSink : IRenderEnvironmentSink
    {
        private readonly List<RenderEnvironmentState> _applies = new();

        public int CaptureCount { get; private set; }
        public int RestoreCount { get; private set; }
        public bool SunBound { get; private set; }
        public IReadOnlyList<RenderEnvironmentState> Applies => _applies;

        public void CaptureBaseline() => CaptureCount++;

        public void RestoreBaseline() => RestoreCount++;

        public void Apply(in RenderEnvironmentState state, Light sun)
        {
            if (sun == null)
            {
                throw new ArgumentNullException(nameof(sun));
            }

            SunBound = true;
            _applies.Add(state);
        }
    }
}