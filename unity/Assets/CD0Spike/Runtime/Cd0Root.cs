#nullable enable

using Unity.Loading;
using UnityEngine;

namespace CD0Spike
{
    internal sealed class Cd0Root : ScriptableObject
    {
        [SerializeField] private LoadableSceneId _payloadScene;
        [SerializeField] private Loadable<Cd0ProbeAsset>? _probeAsset;

        internal LoadableSceneId PayloadScene => _payloadScene;
        internal Loadable<Cd0ProbeAsset> ProbeAsset => _probeAsset!;

        internal void Initialize(LoadableSceneId payloadScene, Loadable<Cd0ProbeAsset> probeAsset)
        {
            _payloadScene = payloadScene;
            _probeAsset = probeAsset;
        }
    }
}
