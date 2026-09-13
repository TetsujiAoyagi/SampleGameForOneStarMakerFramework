#nullable enable

using UnityEngine;

namespace CD0Spike
{
    internal sealed class Cd0SceneMarker : MonoBehaviour
    {
        [SerializeField] private string _value = string.Empty;

        internal string Value => _value;

        internal void SetValue(string value)
        {
            _value = value;
        }
    }
}
