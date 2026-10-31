#nullable enable

using System;
using System.Collections.Generic;
using UnityEngine;

namespace OneStarMaker.Runtime.BuildContent
{
    [Serializable]
    // 宣言の Runtime 既定値 1 件。key と値は Game の語彙で、Framework は文字列として運ぶだけ。
    public sealed class BuildContentDeclarationValue
    {
        [SerializeField] private string _key = "";
        [SerializeField] private string _value = "";

        public BuildContentDeclarationValue() { }

        public BuildContentDeclarationValue(string key, string value)
        {
            _key = key;
            _value = value;
        }

        public string Key => _key;
        public string Value => _value;
    }

    [Serializable]
    // directory が「何の目的で作られたか」を持つための宣言の写し。build 側の宣言の名前・表現の主値・runtime 既定値だけを運び、
    // 選択の規則（root・filter）は運ばない。Runtime は起動設定との不一致を Warning で見せるためにだけ読む。
    // Unity の serializer は class 型の field を null にできないため、名前が空なら「宣言無し」と読む（宣言の無い旧 root をそのまま受理する）。
    public sealed class BuildContentDeclaration
    {
        [SerializeField] private string _name = "";
        [SerializeField] private string _representation = "";
        [SerializeField] private BuildContentDeclarationValue[] _runtimeDefaults = Array.Empty<BuildContentDeclarationValue>();

        public BuildContentDeclaration() { }

        public BuildContentDeclaration(string name, string representation, IReadOnlyList<KeyValuePair<string, string>> runtimeDefaults)
        {
            if (string.IsNullOrEmpty(name)) throw new ArgumentException("Declaration name is required.", nameof(name));
            if (string.IsNullOrEmpty(representation)) throw new ArgumentException("Declaration representation is required.", nameof(representation));
            if (runtimeDefaults == null) throw new ArgumentNullException(nameof(runtimeDefaults));
            _name = name;
            _representation = representation;
            _runtimeDefaults = new BuildContentDeclarationValue[runtimeDefaults.Count];
            for (var i = 0; i < runtimeDefaults.Count; i++)
                _runtimeDefaults[i] = new BuildContentDeclarationValue(runtimeDefaults[i].Key, runtimeDefaults[i].Value);
        }

        public string Name => _name;

        /// <summary>宣言の表現の主値（優先順の先頭）。directory の実効表現と比べる。</summary>
        public string Representation => _representation;

        public IReadOnlyList<BuildContentDeclarationValue> RuntimeDefaults => Array.AsReadOnly(_runtimeDefaults);

        /// <summary>宣言を持つか。serialized の既定値（名前が空）は宣言無し。</summary>
        public bool IsDeclared => !string.IsNullOrEmpty(_name);
    }
}
