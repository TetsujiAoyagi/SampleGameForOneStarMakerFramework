#nullable enable

using System;
using OneStarMaker.Runtime.AssetDescriptions;

namespace OneStarMaker.Runtime.AssetManagement
{
    /// <summary>
    /// AssetRegistry の正規化キー。
    /// </summary>
    public readonly struct AssetKey : IEquatable<AssetKey>
    {
        private const string AddressPrefix = "address:";
        private const string DescriptionPrefix = "description:";

        private AssetKey(string canonical, string address, AssetType type)
        {
            Canonical = canonical;
            Address = address;
            Type = type;
        }

        /// <summary>辞書キー。variant を含めて一意になる。</summary>
        public string Canonical { get; }

        /// <summary>backend に渡す Addressables key。</summary>
        internal string Address { get; }

        /// <summary>将来のカテゴリ別キャッシュ用メタ情報。</summary>
        public AssetType Type { get; }

        /// <summary>Addressables address 文字列からキーを作る。</summary>
        public static AssetKey FromAddress(string address)
        {
            if (string.IsNullOrWhiteSpace(address))
            {
                throw new ArgumentException("Address is required.", nameof(address));
            }

            return new AssetKey(AddressPrefix + address, address, InferType(address));
        }

        /// <summary>AssetDescription と variant からキーを作る。</summary>
        public static AssetKey FromDescription(AssetDescription desc, string variant = "")
        {
            if (desc == null)
            {
                throw new ArgumentNullException(nameof(desc));
            }

            var resolvedVariant = variant ?? string.Empty;
            var reference = desc.ResolveReference(resolvedVariant);
            if (reference == null)
            {
                throw new InvalidOperationException(
                    $"Asset reference not found. description={desc.DisplayName}, variant={resolvedVariant}");
            }

            var runtimeKey = reference.RuntimeKey?.ToString();
            var address = !string.IsNullOrWhiteSpace(runtimeKey)
                ? runtimeKey
                : reference.AssetGUID;

            if (string.IsNullOrWhiteSpace(address))
            {
                throw new InvalidOperationException(
                    $"Asset reference has no runtime key. description={desc.DisplayName}, variant={resolvedVariant}");
            }

            // アセットの同一性は address + variant で決まる。description の型・DisplayName には依存させない
            // （同一アセットを別 description 経由でロードしても dedupe されるようにするため）。
            var canonical = string.Concat(
                DescriptionPrefix,
                address,
                ":variant:",
                resolvedVariant);

            var type = desc is SceneAssetDescription ? AssetType.Scene : InferType(address);
            return new AssetKey(canonical, address, type);
        }

        public bool Equals(AssetKey other) => string.Equals(Canonical, other.Canonical, StringComparison.Ordinal);

        public override bool Equals(object? obj) => obj is AssetKey other && Equals(other);

        public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Canonical);

        public override string ToString() => Canonical;

        public static bool operator ==(AssetKey left, AssetKey right) => left.Equals(right);

        public static bool operator !=(AssetKey left, AssetKey right) => !left.Equals(right);

        private static AssetType InferType(string address)
        {
            var lower = address.ToLowerInvariant();
            if (lower.EndsWith(".unity", StringComparison.Ordinal))
            {
                return AssetType.Scene;
            }

            if (lower.EndsWith(".prefab", StringComparison.Ordinal))
            {
                return AssetType.Prefab;
            }

            if (lower.EndsWith(".png", StringComparison.Ordinal)
                || lower.EndsWith(".jpg", StringComparison.Ordinal)
                || lower.EndsWith(".jpeg", StringComparison.Ordinal)
                || lower.EndsWith(".tga", StringComparison.Ordinal)
                || lower.EndsWith(".psd", StringComparison.Ordinal))
            {
                return AssetType.Texture;
            }

            if (lower.EndsWith(".wav", StringComparison.Ordinal)
                || lower.EndsWith(".mp3", StringComparison.Ordinal)
                || lower.EndsWith(".ogg", StringComparison.Ordinal))
            {
                return AssetType.Audio;
            }

            return AssetType.Other;
        }
    }
}
