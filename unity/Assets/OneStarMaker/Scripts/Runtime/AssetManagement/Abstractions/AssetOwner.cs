#nullable enable

using System;
using UnityEngine;

namespace OneStarMaker.Runtime.AssetManagement
{
    /// <summary>
    /// ロードしたリソースの寿命スコープ。
    /// </summary>
    public readonly struct AssetOwner : IEquatable<AssetOwner>
    {
        private AssetOwner(AssetOwnerKind kind, string id, ulong gameObjectId, GameObject? boundObject = null)
        {
            Kind = kind;
            Id = id;
            GameObjectId = gameObjectId;
            BoundObject = boundObject;
        }

        /// <summary>アプリ終了まで保持するスコープ。</summary>
        public static readonly AssetOwner App = new(AssetOwnerKind.App, "app", 0);

        /// <summary>呼び出し側が Release するスコープ。</summary>
        public static readonly AssetOwner Manual = new(AssetOwnerKind.Manual, "manual", 0);

        internal AssetOwnerKind Kind { get; }

        internal string Id { get; }

        internal ulong GameObjectId { get; }

        internal GameObject? BoundObject { get; }

        /// <summary>指定シーンのアンロード時に解放するスコープ。</summary>
        public static AssetOwner Scene(string sceneIdentity)
        {
            if (string.IsNullOrWhiteSpace(sceneIdentity))
            {
                throw new ArgumentException("Scene identity is required.", nameof(sceneIdentity));
            }

            return new AssetOwner(AssetOwnerKind.Scene, sceneIdentity, 0);
        }

        /// <summary>GameObject の破棄に紐付けるスコープ。</summary>
        public static AssetOwner Bind(GameObject go)
        {
            if (go == null)
            {
                throw new ArgumentNullException(nameof(go));
            }

            var id = EntityId.ToULong(go.GetEntityId());
            return new AssetOwner(AssetOwnerKind.GameObject, id.ToString(), id, go);
        }

        internal static AssetOwner FromGameObjectId(ulong id)
        {
            return new AssetOwner(AssetOwnerKind.GameObject, id.ToString(), id);
        }

        public bool Equals(AssetOwner other)
        {
            return Kind == other.Kind
                   && GameObjectId == other.GameObjectId
                   && string.Equals(Id, other.Id, StringComparison.Ordinal);
        }

        public override bool Equals(object? obj) => obj is AssetOwner other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = 17;
                hash = hash * 31 + (int)Kind;
                hash = hash * 31 + StringComparer.Ordinal.GetHashCode(Id);
                hash = hash * 31 + GameObjectId.GetHashCode();
                return hash;
            }
        }

        public override string ToString()
        {
            switch (Kind)
            {
                case AssetOwnerKind.App:
                    return "App";
                case AssetOwnerKind.Manual:
                    return "Manual";
                case AssetOwnerKind.Scene:
                    return $"Scene({Id})";
                case AssetOwnerKind.GameObject:
                    return $"GameObject({GameObjectId})";
                default:
                    return Kind.ToString();
            }
        }

        public static bool operator ==(AssetOwner left, AssetOwner right) => left.Equals(right);

        public static bool operator !=(AssetOwner left, AssetOwner right) => !left.Equals(right);
    }

    internal enum AssetOwnerKind
    {
        App,
        Manual,
        Scene,
        GameObject,
    }
}
