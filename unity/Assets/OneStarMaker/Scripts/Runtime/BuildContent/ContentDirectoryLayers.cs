#nullable enable

using System;
using System.Collections.Generic;
using OneStarMaker.Runtime.BuildContent.Distribution;

namespace OneStarMaker.Runtime.BuildContent
{
    /// <summary>
    /// 1 つの session が所有する、順のある層の列（base と、任意の上書き 1 つ）。層ごとの native の登録・root・索引と、
    /// 層をまたぐ key の解決、層の部分を持つ cache の key、登録の順と逆順の解除、巻き戻しの再試行をここに置く。
    /// token・発行済みの処理・失敗した後始末の数え方は session に残し（session 全体で 1 組）、ここは数えない。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 層は登録の口の引数だけで決まり、作った後に足す・外す member を持たない。上書きは最初の load より前に base と一緒に揃う。
    /// </para>
    /// <para>
    /// 解除は層の列の逆順に 1 層ずつ行い、ある層の解除が失敗したらそれより前の層（base）を解除しない。後から登録した directory の解除は、
    /// 先に登録した directory から載せた物を壊すことがある（Unity の挙動）ので、上書きが残ったまま base を外す順を作らない。
    /// 各層の成功した解除は高々 1 回で、どこまで解除したかをここで保持する。全ての層を解除してから gate の予約を返す。
    /// session の解除の 3 経路（明示 close・自動の解除、Play 停止、登録の巻き戻し）と、巻き戻しの再試行は全て
    /// <see cref="TryUnregisterAll"/> を呼ぶだけにし、どれが先に走っても同じ層を 2 回解除しない。
    /// </para>
    /// <para>
    /// 巻き戻しの再試行は process に 1 つだけ保留できる。gate は登録を 1 件しか持たず（層は 1 件の登録の中にある）、
    /// 次の登録は native の登録の前に保留中の再試行を走らせ、失敗すれば登録を拒むので、2 つ目が要る時点が無い。
    /// script の reload で static が消えても、その時点で native の登録は Unity 側でも消えている（実測）ので、static のままでよい。
    /// </para>
    /// </remarks>
    internal sealed class ContentDirectoryLayers
    {
        private static readonly object PendingSync = new();
        private static ContentDirectoryLayers? _pendingRollback;

        private readonly Layer[] _layers;
        private readonly IDisposable _reservation;
        private readonly object _sync = new();
        private bool _reservationReturned;
        // 上書きの root にある native の id と同じ id を持つ base の entry。native の load は directory を指定せず id で読むので、
        // この entry を base から読んでも上書きの版が出る。base の層の解決から除き、record と base の pin を嘘にしない。
        private readonly HashSet<BuildContentEntry> _shadowed = new();

        private ContentDirectoryLayers(Layer[] layers, IDisposable reservation)
        { _layers = layers; _reservation = reservation; }

        internal Layer Base => _layers[0];

        /// <summary>上書きの層。上書きを渡さない登録では null。</summary>
        internal Layer? Overlay => _layers.Length > 1 ? _layers[1] : null;

        /// <summary>全ての層の解除と予約の返却が済んだか。</summary>
        internal bool IsReleased
        {
            get { lock (_sync) return _reservationReturned; }
        }

        /// <summary>
        /// 予約し、base → 上書きの順に native へ登録し、root と索引を作り、上書きの記録を base の検証済みの値と照合する。
        /// 途中で失敗したら上書き → base の順に巻き戻して例外を返す。巻き戻しが失敗したら予約を残し、次の登録の前に再試行する。
        /// </summary>
        /// <param name="verifyInsideLease">予約の中で base を検証し直す（検証済みでない登録では null を返す）。native の登録の前に呼ぶ。</param>
        internal static ContentDirectoryLayers Register(string target, Layer baseLayer, Layer? overlayLayer,
            Func<VerifiedInstalledRevision?> verifyInsideLease, out VerifiedInstalledRevision? verified)
        {
            var identity = baseLayer.Identity;
            RetryPendingRollback(identity, target);
            var specs = overlayLayer == null
                ? new[] { new ContentRevisionLayer(identity, target, baseLayer.Path) }
                : new[] { new ContentRevisionLayer(identity, target, baseLayer.Path), new ContentRevisionLayer(overlayLayer.Identity, target, overlayLayer.Path) };
            var reservation = ContentRevisionGate.Reserve(specs);
            var layers = new ContentDirectoryLayers(overlayLayer == null ? new[] { baseLayer } : new[] { baseLayer, overlayLayer }, reservation);
            try
            {
                verified = verifyInsideLease();
                layers.RegisterNative(target, verified);
                return layers;
            }
            catch (Exception ex)
            {
                if (!layers.TryUnregisterAll(out var cleanupException))
                {
                    // native 登録が残ったまま予約を返すと、別処理が物理削除を許可される。予約は gate に残し、次の登録の前に再試行する。
                    layers.DeferRollback();
                    throw new ContentDirectoryException(ContentDirectoryFailureCode.RegistrationFailed,
                        "Content directory registration rollback failed.", identity, target,
                        innerException: new AggregateException(ex, cleanupException!));
                }
                if (ex is ContentDirectoryException) throw;
                throw new ContentDirectoryException(ContentDirectoryFailureCode.RegistrationFailed, "Content directory registration failed.", identity, target, innerException: ex);
            }
        }

        private void RegisterNative(string target, VerifiedInstalledRevision? verified)
        {
            var identity = Base.Identity;
            var baseRoot = RegisterLayer(Base, Base.Identity, target, identity);
            // 記録を持つ root は上書きとして作った directory。単独の directory・base として登録すると、照合を経ずに読まれる。
            if (baseRoot.OverlayBase != null)
                throw new ContentDirectoryException(ContentDirectoryFailureCode.InvalidRoot,
                    "Content directory root carries an overlay base record; it can only be registered as an overlay layer.", identity, target);
            var overlay = Overlay;
            if (overlay == null) return;

            var overlayRoot = RegisterLayer(overlay, overlay.Identity, target, identity);
            var record = overlayRoot.OverlayBase
                ?? throw new ContentDirectoryException(ContentDirectoryFailureCode.OverlayBaseMissing,
                    "The overlay content directory root has no base record.", identity, target);
            if (verified == null)
                throw new ContentDirectoryException(ContentDirectoryFailureCode.InvalidConfiguration,
                    "An overlay can only be stacked on a verified installed revision.", identity, target);
            var mismatch = BuildContentOverlayBase.FindMismatch(record, verified, baseRoot.Declaration?.Name);
            if (mismatch != null)
                throw new ContentDirectoryException(ContentDirectoryFailureCode.OverlayBaseMismatch,
                    "The overlay base record does not match the registered base (" + mismatch + ").", identity, target);
            ComputeShadow();
        }

        private static BuildContentRoot RegisterLayer(Layer layer, string expectedIdentity, string target, string sessionIdentity)
        {
            var registered = layer.Native.Register(layer.Path);
            // 有効な handle を得た層だけを解除の対象にする（1 directory の登録の巻き戻しと同じ）。
            layer.Registered = registered;
            if (!registered)
                throw new ContentDirectoryException(ContentDirectoryFailureCode.RegistrationFailed,
                    "Content directory registration returned an invalid handle.", sessionIdentity, target);
            var roots = layer.Native.GetRoots();
            if (roots.Length != 1 || roots[0] == null)
                throw new ContentDirectoryException(ContentDirectoryFailureCode.InvalidRoot, "Content directory must contain exactly one BuildContentRoot.", sessionIdentity, target);
            if (!ContentDirectoryIndex.TryCreate(roots[0], expectedIdentity, target, out var index, out var issue))
                throw new ContentDirectoryException(issue.Code, issue.Message, sessionIdentity, target, issue.LogicalKey, issue.Representation);
            layer.Root = roots[0];
            layer.Index = index;
            return roots[0];
        }

        private void ComputeShadow()
        {
            var overlay = Overlay!.Index!.Entries;
            foreach (var entry in Base.Index!.Entries)
                foreach (var other in overlay)
                    if (SameNativeId(entry, other)) { _shadowed.Add(entry); break; }
        }

        // Scene は loadable scene id、Object は loadable object id。種別をまたいでは比べない。
        private static bool SameNativeId(BuildContentEntry a, BuildContentEntry b)
        {
            if (a.Kind != b.Kind) return false;
            if (a.Kind == BuildContentKind.Scene) return a.SceneId.Equals(b.SceneId);
            return a.Object != null && b.Object != null && a.Object.LoadableObjectId.Equals(b.Object.LoadableObjectId);
        }

        /// <summary>
        /// (logical key, kind, 実効の表現) の単位で層を選ぶ。上書きを渡さない登録では今の 1 directory の規則のまま。
        /// 上書きを重ねた登録: 要求の表現 R を上書き → base（影を除く）の順に引き、どちらにも無く R が Full でなければ Full を同じ順に引く。
        /// 表現の指定が無い要求は Full だけを引く。
        /// </summary>
        internal LayerResolution Resolve(string logicalKey, BuildContentKind kind, string representation)
        {
            var overlay = Overlay;
            if (overlay == null)
            {
                var found = kind == BuildContentKind.Scene
                    ? Base.Index!.TryResolveScene(logicalKey, representation, out var single, out _)
                    : Base.Index!.TryResolveObject(logicalKey, representation, out single, out _);
                return found ? new LayerResolution(Base, single, false) : default;
            }

            var requested = ContentEntryMatch.EffectiveRepresentation(representation);
            if (overlay.Index!.TryGetExact(logicalKey, requested, kind, out var entry))
                return new LayerResolution(overlay, entry, false);
            var shadowedAtRequested = false;
            if (Base.Index!.TryGetExact(logicalKey, requested, kind, out entry))
            {
                if (!_shadowed.Contains(entry!)) return new LayerResolution(Base, entry, false);
                shadowedAtRequested = true;
            }
            if (!string.Equals(requested, ContentEntryMatch.Full, StringComparison.Ordinal))
            {
                if (overlay.Index.TryGetExact(logicalKey, ContentEntryMatch.Full, kind, out entry))
                    return new LayerResolution(overlay, entry, shadowedAtRequested);
                if (Base.Index.TryGetExact(logicalKey, ContentEntryMatch.Full, kind, out entry) && !_shadowed.Contains(entry!))
                    return new LayerResolution(Base, entry, shadowedAtRequested);
            }
            return new LayerResolution(null, null, shadowedAtRequested);
        }

        /// <summary>
        /// 層の列の逆順に、未解除の層を 1 つずつ解除し、全て済んだら予約を返す。失敗したらそこで止め（前の層を解除しない）、false と例外を返す。
        /// 何度呼んでも、成功した層は再び解除しない。
        /// </summary>
        internal bool TryUnregisterAll(out Exception? failure)
        {
            lock (_sync)
            {
                failure = null;
                if (_reservationReturned) return true;
                for (var i = _layers.Length - 1; i >= 0; i--)
                {
                    var layer = _layers[i];
                    if (!layer.Registered || layer.Unregistered) continue;
                    try { layer.Native.Unregister(); layer.Unregistered = true; }
                    catch (Exception ex) { failure = ex; return false; }
                }
                try { _reservation.Dispose(); _reservationReturned = true; }
                catch (Exception ex) { failure = ex; return false; }
                return true;
            }
        }

        /// <summary>次の登録の前に走る巻き戻しの再試行が保留されているか（test が途中の状態を見るため）。</summary>
        internal static bool HasPendingRollback
        {
            get { lock (PendingSync) return _pendingRollback != null; }
        }

        /// <summary>解除に失敗した層の列を、次の登録の前の再試行に預ける（保留は process に 1 つ）。</summary>
        internal void DeferRollback()
        {
            lock (PendingSync) _pendingRollback ??= this;
        }

        private static void RetryPendingRollback(string identity, string target)
        {
            lock (PendingSync)
            {
                // 別 path への再試行でも、先の解除が成功するまで新規登録しない。
                var pending = _pendingRollback;
                if (pending == null) return;
                if (pending.TryUnregisterAll(out var failure)) { _pendingRollback = null; return; }
                throw new ContentDirectoryException(ContentDirectoryFailureCode.RegistrationFailed,
                    "Earlier content directory rollback is still pending.", identity, target, innerException: failure);
            }
        }

        /// <summary>session に共通の cache の前置き（base の directory の値。1 層の登録の今の前置きと同じ）。</summary>
        internal string CachePrefix(string target) => ContentDirectoryIndex.CachePrefix(Base.Path, Base.Identity, target);

        /// <summary>選ばれた層の entry の cache の key。上書きの層は共通の前置きの後に層の部分を続ける。</summary>
        internal string CacheKey(string target, Layer layer, BuildContentEntry entry)
        {
            var prefix = CachePrefix(target);
            return ContentDirectoryIndex.CacheKey(layer.IsOverlay ? ContentDirectoryIndex.OverlayLayerPrefix(prefix, layer.Path, layer.Identity) : prefix, entry);
        }

        /// <summary>層 1 つ。path は gate で正規化した値。</summary>
        internal sealed class Layer
        {
            internal Layer(bool isOverlay, string path, string identity, IContentNativeDirectory native)
            { IsOverlay = isOverlay; Path = path; Identity = identity; Native = native; }

            internal bool IsOverlay { get; }
            internal string Path { get; }
            internal string Identity { get; }
            internal IContentNativeDirectory Native { get; }
            internal BuildContentRoot? Root { get; set; }
            internal ContentDirectoryIndex? Index { get; set; }
            internal bool Registered { get; set; }
            internal bool Unregistered { get; set; }
        }
    }

    /// <summary>
    /// 層の解決の答え。<see cref="Entry"/> が null なら見つからない。
    /// <see cref="BaseShadowedAtRequested"/> は「base の root に要求の表現そのものの entry があったが、上書きの id による影で除いた」。
    /// </summary>
    internal readonly struct LayerResolution
    {
        internal LayerResolution(ContentDirectoryLayers.Layer? layer, BuildContentEntry? entry, bool baseShadowedAtRequested)
        { Layer = layer; Entry = entry; BaseShadowedAtRequested = baseShadowedAtRequested; }

        internal ContentDirectoryLayers.Layer? Layer { get; }
        internal BuildContentEntry? Entry { get; }
        internal bool BaseShadowedAtRequested { get; }
    }
}
