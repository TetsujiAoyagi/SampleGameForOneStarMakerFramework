#nullable enable

using UnityEngine;

namespace CD0Spike
{
    internal sealed class Cd0ProbeAsset : ScriptableObject
    {
        [SerializeField] private string _value = string.Empty;

        internal string Value => _value;

        internal void SetValue(string value)
        {
            _value = value;
        }
    }
}
