#nullable enable

using System;
using System.Collections.Generic;
using System.Text;

namespace OneStarMaker.Runtime.BuildContent
{
    /// <summary>宣言と起動設定の違い 1 件。key は Game の語彙で、表現は <see cref="ContentDeclarationMismatch.RepresentationKey"/>。</summary>
    internal readonly struct ContentDeclarationDifference
    {
        internal ContentDeclarationDifference(string key, string declared, string actual)
        {
            Key = key;
            Declared = declared;
            Actual = actual;
        }

        internal string Key { get; }
        internal string Declared { get; }
        internal string Actual { get; }
    }

    /// <summary>
    /// directory の root が持つ宣言と、起動設定・実効表現との不一致を見つける pure な比較。
    /// Framework は key も値も解釈せず、不透明な文字列として ordinal で比べる。設定に key が無い場合は
    /// Runtime の既定値が宣言と違いうるので <see cref="AbsentValue"/> として不一致に数える。
    /// 目的違いの directory を Play したとき、Cell の Add 失敗 54 回ではなく Warning 1 行で分かるためにある。
    /// </summary>
    internal static class ContentDeclarationMismatch
    {
        internal const string RepresentationKey = "representation";
        internal const string AbsentValue = "<absent>";

        /// <param name="configuration">key の設定値。無ければ null を返す。</param>
        /// <param name="effectiveRepresentation">directory の実効表現（起動設定の content:representation）。</param>
        internal static IReadOnlyList<ContentDeclarationDifference> Find(
            BuildContentDeclaration declaration,
            Func<string, string?> configuration,
            string? effectiveRepresentation)
        {
            if (declaration == null) throw new ArgumentNullException(nameof(declaration));
            if (configuration == null) throw new ArgumentNullException(nameof(configuration));
            var differences = new List<ContentDeclarationDifference>();
            foreach (var item in declaration.RuntimeDefaults)
            {
                if (item == null) continue;
                var actual = configuration(item.Key);
                if (!string.Equals(actual, item.Value, StringComparison.Ordinal))
                    differences.Add(new ContentDeclarationDifference(item.Key, item.Value, actual ?? AbsentValue));
            }

            if (!string.Equals(declaration.Representation, effectiveRepresentation, StringComparison.Ordinal))
                differences.Add(new ContentDeclarationDifference(RepresentationKey, declaration.Representation,
                    string.IsNullOrEmpty(effectiveRepresentation) ? AbsentValue : effectiveRepresentation!));
            return differences;
        }

        /// <summary>Warning 1 行の本文。key ごとに宣言と実際の値を並べる。</summary>
        internal static string Format(IReadOnlyList<ContentDeclarationDifference> differences)
        {
            var text = new StringBuilder();
            for (var i = 0; i < differences.Count; i++)
            {
                if (i != 0) text.Append(", ");
                text.Append(differences[i].Key).Append(": declared=").Append(differences[i].Declared)
                    .Append(" actual=").Append(differences[i].Actual);
            }

            return text.ToString();
        }
    }
}
