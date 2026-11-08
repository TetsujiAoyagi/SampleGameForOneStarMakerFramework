#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace OneStarMaker.Runtime.ContentResolution
{
    /// <summary>
    /// Play 前に固定する規則: 供給元の順序と互換の規則。不変の値で、将来の要求を列挙しない。
    /// 供給元の順序は「どこから先に探すか」だけを決め、候補を採ってよいかは規則が別に決める。
    /// </summary>
    public sealed class ResolutionPolicy
    {
        /// <param name="providers">探す順の供給元。1 件以上。id は空不可で ordinal に重複なし。複製して保持する。</param>
        /// <param name="rule">互換の規則。null は拒む。</param>
        public ResolutionPolicy(IEnumerable<IContentProvider> providers, IContentCompatibilityRule rule)
        {
            if (providers == null)
            {
                throw new ArgumentNullException(nameof(providers));
            }

            var copy = new List<IContentProvider>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var provider in providers)
            {
                if (provider == null)
                {
                    throw new ArgumentException("Providers must not contain null.", nameof(providers));
                }

                // id は provider の実装が返す値なので、constructor の時点で 1 回だけ読んで検査する。
                var id = provider.Id;
                if (string.IsNullOrWhiteSpace(id))
                {
                    throw new ArgumentException("Provider id is required.", nameof(providers));
                }

                if (!ids.Add(id))
                {
                    throw new ArgumentException($"Provider id '{id}' is duplicated.", nameof(providers));
                }

                copy.Add(provider);
            }

            if (copy.Count == 0)
            {
                throw new ArgumentException("At least one provider is required.", nameof(providers));
            }

            Providers = new ReadOnlyCollection<IContentProvider>(copy);
            Rule = rule ?? throw new ArgumentNullException(nameof(rule));
        }

        public IReadOnlyList<IContentProvider> Providers { get; }

        public IContentCompatibilityRule Rule { get; }
    }
}
