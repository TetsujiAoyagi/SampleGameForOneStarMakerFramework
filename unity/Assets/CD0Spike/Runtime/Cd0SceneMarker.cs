#nullable enable

using UnityEngine;

namespace CD0Spike
{
    internal sealed class Cd0SceneMarker : MonoBehaviour
    {
        [SerializeField] private string _value = string.Empty;
        [SerializeField] private Cd0ProbeAsset? _probeAsset;

        internal string Value => _value;

        internal void Initialize(string value, Cd0ProbeAsset probeAsset)
        {
            _value = value;
            _probeAsset = probeAsset;
        }

        private void OnEnable()
        {
            var probeValue = _probeAsset != null ? _probeAsset.Value : "<missing>";
            Debug.Log($"[CD0] Payload marker enabled value={_value} probe={probeValue}");
        }
    }
}
