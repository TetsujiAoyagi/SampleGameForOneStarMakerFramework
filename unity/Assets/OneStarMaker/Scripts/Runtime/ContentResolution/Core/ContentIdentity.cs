#nullable enable

using System;

namespace OneStarMaker.Runtime.ContentResolution
{
    /// <summary>
    /// 候補の identity（content set・revision・target・digest）。
    /// 候補の申告では空の欄を許すが、pin（探索中に固定する期待値）にできるのは 4 欄が全て非空の値だけである。
    /// 等値は ordinal の全欄一致だけで、互換の判定（どの差なら許すか）は持たない。
    /// default の値は全欄が空として振る舞う。
    /// </summary>
    public readonly struct ContentIdentity : IEquatable<ContentIdentity>
    {
        private readonly string? _contentSet;
        private readonly string? _revision;
        private readonly string? _target;
        private readonly string? _digest;

        public ContentIdentity(string? contentSet, string? revision, string? target, string? digest)
        {
            _contentSet = contentSet;
            _revision = revision;
            _target = target;
            _digest = digest;
        }

        public string ContentSet => _contentSet ?? string.Empty;

        public string Revision => _revision ?? string.Empty;

        public string Target => _target ?? string.Empty;

        public string Digest => _digest ?? string.Empty;

        /// <summary>4 欄が全て空（空白だけを含む）。</summary>
        public bool IsEmpty =>
            string.IsNullOrWhiteSpace(_contentSet)
            && string.IsNullOrWhiteSpace(_revision)
            && string.IsNullOrWhiteSpace(_target)
            && string.IsNullOrWhiteSpace(_digest);

        /// <summary>4 欄が全て非空。pin にできるのはこの値だけ。</summary>
        public bool IsComplete =>
            !string.IsNullOrWhiteSpace(_contentSet)
            && !string.IsNullOrWhiteSpace(_revision)
            && !string.IsNullOrWhiteSpace(_target)
            && !string.IsNullOrWhiteSpace(_digest);

        /// <summary>
        /// この identity が pin と全欄一致するか。digest が同じでも target 等の他の欄が違えば一致しない。
        /// 空の identity や一部の欄だけの identity は、完全な pin と全欄一致し得ないので常に false になる。
        /// pin が完全でなければ <see cref="ArgumentException"/>（不完全な pin で「一致」を作らせないため）。
        /// </summary>
        public bool MatchesPin(ContentIdentity pin)
        {
            if (!pin.IsComplete)
            {
                throw new ArgumentException("A pin must have all four identity fields.", nameof(pin));
            }

            return Equals(pin);
        }

        public bool Equals(ContentIdentity other)
        {
            return string.Equals(ContentSet, other.ContentSet, StringComparison.Ordinal)
                   && string.Equals(Revision, other.Revision, StringComparison.Ordinal)
                   && string.Equals(Target, other.Target, StringComparison.Ordinal)
                   && string.Equals(Digest, other.Digest, StringComparison.Ordinal);
        }

        public override bool Equals(object? obj) => obj is ContentIdentity other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = 17;
                hash = hash * 31 + StringComparer.Ordinal.GetHashCode(ContentSet);
                hash = hash * 31 + StringComparer.Ordinal.GetHashCode(Revision);
                hash = hash * 31 + StringComparer.Ordinal.GetHashCode(Target);
                hash = hash * 31 + StringComparer.Ordinal.GetHashCode(Digest);
                return hash;
            }
        }

        public override string ToString() => $"{ContentSet}/{Revision}/{Target}/{Digest}";

        public static bool operator ==(ContentIdentity left, ContentIdentity right) => left.Equals(right);

        public static bool operator !=(ContentIdentity left, ContentIdentity right) => !left.Equals(right);

        /// <summary>pin として渡された値を検査する。無し（null）は許し、空・一部の欄だけの値は拒む。</summary>
        internal static void ValidateOptionalPin(ContentIdentity? pin, string paramName)
        {
            if (!pin.HasValue)
            {
                return;
            }

            if (pin.Value.IsEmpty)
            {
                throw new ArgumentException("Pass null instead of an empty identity when there is no pin.", paramName);
            }

            if (!pin.Value.IsComplete)
            {
                throw new ArgumentException("A pin must have all four identity fields; partial identities are not accepted.", paramName);
            }
        }
    }
}
