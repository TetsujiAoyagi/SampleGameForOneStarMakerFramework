#nullable enable
using System;
using UnityEngine;
namespace SampleGame.DependOnAll
{
    public sealed class Bs4ContentOnlyProbe : MonoBehaviour
    {
        [SerializeField] private string serializedToken = "";
        public void Configure(string token) => serializedToken = token;
        private void VerifyBs4Probe(string expected) { if (!string.Equals(serializedToken, expected, StringComparison.Ordinal)) throw new InvalidOperationException("BS4 probe behavior mismatch."); }
    }
}
