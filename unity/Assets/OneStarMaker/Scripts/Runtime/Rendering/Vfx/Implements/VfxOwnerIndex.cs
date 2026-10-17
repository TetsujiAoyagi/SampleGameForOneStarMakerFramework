#nullable enable

using System;
using System.Collections.Generic;
using OneStarMaker.Runtime.AssetManagement;

namespace OneStarMaker.Runtime.Rendering.Vfx
{
    /// <summary>
    /// 所有者 → handle の索引と、Scene 所有者の終端状態。
    /// <para>
    /// SceneEvent を知らない。どの事象が終端かは VfxSystem が決め、ここは適用だけ行う。
    /// 回収中の再入（Destroy から別の回収や Spawn が同期で走る）に備え、列挙が要る呼び出し元には
    /// 常に新しい snapshot を返す。内部の集合を直接列挙させない。
    /// </para>
    /// </summary>
    internal sealed class VfxOwnerIndex
    {
        private readonly Dictionary<AssetOwner, List<VfxHandle>> _byOwner = new();
        private readonly Dictionary<VfxInstanceId, VfxHandle> _byId = new();

        // AssetOwner の等価性は BoundObject を比べないので、偽 null 検査に使う GameObject は登録時の owner から取る。
        private readonly Dictionary<ulong, AssetOwner> _bindOwners = new();

        // PreUnloading の時点では ISceneQuery.IsSceneLoaded がまだ true なので、
        // 終端中の Scene への Spawn を拒否できるのはこの集合だけである。
        private readonly HashSet<string> _terminatingScenes = new(StringComparer.Ordinal);

        internal int Count => _byId.Count;

        /// <summary>
        /// Scene(id) の所有を受け付けられる状態か。lifecycle 未接続、OnCompleted 後、接続の差し替え中は false。
        /// </summary>
        internal bool IsSceneLifecycleOpen { get; set; }

        internal bool ContainsLive(VfxInstanceId id) => _byId.ContainsKey(id);

        internal void Add(VfxHandle handle)
        {
            _byId.Add(handle.Id, handle);

            if (!_byOwner.TryGetValue(handle.Owner, out var handles))
            {
                handles = new List<VfxHandle>();
                _byOwner.Add(handle.Owner, handles);
            }

            handles.Add(handle);

            if (handle.Owner.Kind == AssetOwnerKind.GameObject && !_bindOwners.ContainsKey(handle.Owner.GameObjectId))
            {
                _bindOwners.Add(handle.Owner.GameObjectId, handle.Owner);
            }
        }

        /// <summary>索引から外す。登録の途中で失敗した handle にも呼ばれるので、未登録でも例外にしない。</summary>
        internal void Remove(VfxHandle handle)
        {
            if (_byId.TryGetValue(handle.Id, out var registered) && ReferenceEquals(registered, handle))
            {
                _byId.Remove(handle.Id);
            }

            if (!_byOwner.TryGetValue(handle.Owner, out var handles))
            {
                return;
            }

            handles.Remove(handle);
            if (handles.Count > 0)
            {
                return;
            }

            _byOwner.Remove(handle.Owner);
            if (handle.Owner.Kind == AssetOwnerKind.GameObject)
            {
                _bindOwners.Remove(handle.Owner.GameObjectId);
            }
        }

        internal void EnterTerminal(string sceneIdentity) => _terminatingScenes.Add(sceneIdentity);

        internal void ExitTerminal(string sceneIdentity) => _terminatingScenes.Remove(sceneIdentity);

        internal bool IsTerminating(string sceneIdentity) => _terminatingScenes.Contains(sceneIdentity);

        internal void ClearTerminal() => _terminatingScenes.Clear();

        internal List<VfxHandle> SnapshotOwner(AssetOwner owner)
        {
            return _byOwner.TryGetValue(owner, out var handles)
                ? new List<VfxHandle>(handles)
                : new List<VfxHandle>();
        }

        internal List<VfxHandle> SnapshotAll() => new(_byId.Values);

        internal List<VfxHandle> SnapshotSceneOwned()
        {
            var result = new List<VfxHandle>();
            foreach (var pair in _byOwner)
            {
                if (pair.Key.Kind == AssetOwnerKind.Scene)
                {
                    result.AddRange(pair.Value);
                }
            }

            return result;
        }

        internal List<AssetOwner> SnapshotBindOwners() => new(_bindOwners.Values);
    }
}
