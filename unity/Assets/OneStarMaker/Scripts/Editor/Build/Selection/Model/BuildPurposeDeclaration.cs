#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace OneStarMaker.Build.Selection
{
    /// <summary>表現の優先順から何件採るか。</summary>
    public enum BuildRepresentationMode
    {
        /// <summary>優先順で、node が宣言している最初の表現を 1 つだけ要求する。</summary>
        First,
        /// <summary>優先順にあって node が宣言している表現を、それぞれ 1 つずつ要求する。</summary>
        All
    }

    /// <summary>
    /// 宣言が要求する表現の優先順と選び方。表現名の意味は Framework が解釈しない。
    /// 優先順の先頭が「表現の主値」で、Content Directory の宣言と manifest に運ばれる。
    /// </summary>
    public sealed class BuildRepresentationSelection
    {
        /// <summary>候補と node の表現を付ける tag の dimension 名。materializer・resolver・selector が同じ名前を使う。</summary>
        public const string Dimension = "Representation";

        private readonly ReadOnlyCollection<string> _preference;

        public BuildRepresentationSelection(IEnumerable<string> preference, BuildRepresentationMode mode)
        {
            if (preference == null) throw new ArgumentNullException(nameof(preference));
            if (!Enum.IsDefined(typeof(BuildRepresentationMode), mode)) throw new ArgumentOutOfRangeException(nameof(mode));
            var snapshot = preference.ToArray();
            if (snapshot.Length == 0)
                throw new ArgumentException("Representation preference must not be empty.", nameof(preference));
            if (snapshot.Any(value => !IsStable(value)))
                throw new ArgumentException("Representation names must be non-empty and have no surrounding whitespace.", nameof(preference));
            if (snapshot.Distinct(StringComparer.Ordinal).Count() != snapshot.Length)
                throw new ArgumentException("Representation preference must not repeat a name.", nameof(preference));
            _preference = Array.AsReadOnly(snapshot);
            Mode = mode;
        }

        /// <summary>優先順。先頭が最も優先される。</summary>
        public IReadOnlyList<string> Preference => _preference;

        public BuildRepresentationMode Mode { get; }

        /// <summary>宣言の表現の主値。directory の実効表現と比べる側（Runtime）はこの 1 値だけを見る。</summary>
        public string Primary => _preference[0];

        internal static bool IsStable(string? value) =>
            !string.IsNullOrEmpty(value) && string.Equals(value, value.Trim(), StringComparison.Ordinal);
    }

    /// <summary>
    /// 「何が必要か」の宣言。目的（root と OnDemand の子へ掛ける filter）、表現、Runtime の既定値を 1 つにまとめる。
    /// pure かつ immutable。Framework は filter の dimension 名・runtime の key と値の意味を知らない。
    /// 設定（target、compression、defines）は本型の欄にしない（adapter の定数のまま）。
    /// </summary>
    public sealed class BuildPurposeDeclaration
    {
        /// <summary>
        /// Runtime の既定値に置けない key の接頭辞。content の mode・identity・表現は読む側（起動設定・directory の宣言）が
        /// 決めるので、同じ値を 2 箇所に持たせない。
        /// </summary>
        public const string ReservedRuntimeDefaultPrefix = "content:";

        private const int MaxNameLength = 64;
        private readonly ReadOnlyCollection<string> _roots;
        private readonly ReadOnlyCollection<KeyValuePair<string, string>> _runtimeDefaults;

        public BuildPurposeDeclaration(
            string name,
            IEnumerable<string> roots,
            BuildRequest filter,
            BuildRepresentationSelection representation,
            IEnumerable<KeyValuePair<string, string>>? runtimeDefaults = null)
        {
            if (!IsContentSetName(name))
                throw new ArgumentException("Declaration name must be a safe ASCII content set segment (1-64 of [A-Za-z0-9-_]).", nameof(name));
            if (roots == null) throw new ArgumentNullException(nameof(roots));
            var rootSnapshot = roots.ToArray();
            if (rootSnapshot.Length == 0) throw new ArgumentException("At least one root is required.", nameof(roots));
            if (rootSnapshot.Any(root => !BuildRepresentationSelection.IsStable(root)))
                throw new ArgumentException("Root identities must be non-empty and have no surrounding whitespace.", nameof(roots));
            if (rootSnapshot.Distinct(StringComparer.Ordinal).Count() != rootSnapshot.Length)
                throw new ArgumentException("Root identities must be unique.", nameof(roots));
            Name = name;
            _roots = Array.AsReadOnly(rootSnapshot.OrderBy(root => root, StringComparer.Ordinal).ToArray());
            Filter = filter ?? throw new ArgumentNullException(nameof(filter));
            Representation = representation ?? throw new ArgumentNullException(nameof(representation));

            var defaults = new SortedDictionary<string, string>(StringComparer.Ordinal);
            foreach (var pair in runtimeDefaults ?? Array.Empty<KeyValuePair<string, string>>())
            {
                if (!BuildRepresentationSelection.IsStable(pair.Key) || !BuildRepresentationSelection.IsStable(pair.Value))
                    throw new ArgumentException("Runtime default keys and values must be non-empty and have no surrounding whitespace.", nameof(runtimeDefaults));
                if (pair.Key.StartsWith(ReservedRuntimeDefaultPrefix, StringComparison.OrdinalIgnoreCase))
                    throw new ArgumentException("Runtime defaults must not declare content:* keys; the content mode, identity and representation come from the build itself.", nameof(runtimeDefaults));
                // 設定の key は読む側（AppConfig）が大文字小文字を区別しないため、同じ名前の異なる綴りも重複とする。
                if (defaults.Keys.Any(existing => string.Equals(existing, pair.Key, StringComparison.OrdinalIgnoreCase)))
                    throw new ArgumentException("Runtime default keys must be unique.", nameof(runtimeDefaults));
                defaults.Add(pair.Key, pair.Value);
            }
            _runtimeDefaults = Array.AsReadOnly(defaults.ToArray());
        }

        /// <summary>contentSet と同じ規則の名前。Content Directory の workspace と宣言の識別に使う。</summary>
        public string Name { get; }

        /// <summary>到達の起点になる logical key（node identity）。ordinal 順。</summary>
        public IReadOnlyList<string> Roots => _roots;

        /// <summary>OnDemand の子に掛ける tag の述語。dimension 内は OR、dimension 間は AND、tag の無い dimension は中立。</summary>
        public BuildRequest Filter { get; }

        public BuildRepresentationSelection Representation { get; }

        /// <summary>Game が決める不透明な key=value。key の ordinal 順。</summary>
        public IReadOnlyList<KeyValuePair<string, string>> RuntimeDefaults => _runtimeDefaults;

        /// <summary>key に対応する既定値。無ければ null。読む側の設定と同じく大文字小文字を区別しない。</summary>
        public string? FindRuntimeDefault(string key)
        {
            foreach (var pair in _runtimeDefaults)
                if (string.Equals(pair.Key, key, StringComparison.OrdinalIgnoreCase)) return pair.Value;
            return null;
        }

        /// <summary>coordinator が path segment として受け付ける contentSet と同じ規則。</summary>
        public static bool IsContentSetName(string? value) =>
            !string.IsNullOrEmpty(value) && value.Length <= MaxNameLength &&
            value.All(c => (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == '-' || c == '_');
    }
}
