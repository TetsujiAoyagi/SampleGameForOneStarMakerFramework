#nullable enable

using System;
using System.Collections.Generic;
using OneStarMaker.Runtime.Rendering.Vfx;
using UnityEngine;
using UnityEngine.VFX;
using Object = UnityEngine.Object;

namespace SampleGame.Common.Rendering.Vfx
{
    /// <summary>
    /// VFX Graph（<see cref="VisualEffect"/>）による <see cref="IVfxBackend"/>。
    /// <para>
    /// 所有と寿命: Framework の initializer が <c>CreateVfxBackend</c> で受け取り、Framework の VFX composition が
    /// VfxSystem の Dispose の後に <see cref="Dispose"/> する（App 常駐）。asset はロードしない。source は呼び出し側が
    /// ロードした <see cref="VisualEffectAsset"/> を借りるだけで、解放もしない。
    /// </para>
    /// <para>
    /// 能力判定は <see cref="QueryCapability"/> だけが行う。ctor は環境を読まず、環境に依存する例外を投げない。
    /// Mobile の非対応環境でも起動を失敗させないため。ログは出さない（診断は VfxSystem が出す）。
    /// 非対応環境では VfxSystem が以後この backend を呼ばないので、配置済み VFX も有効にならない。
    /// </para>
    /// <para>
    /// Scene に配置済みの VFX（<see cref="Adopt"/>）は登録するだけで、GameObject も component も Scene の所有のまま。
    /// 最初の <see cref="Play"/> で有効にする（VFX Graph の標準の開始で、有効化で初期イベントが送られる）。
    /// 回収（<see cref="Release"/>）は無効化だけで、再び有効にはしない（有効化のたびに初期イベントが再送されるため）。
    /// </para>
    /// <para>
    /// 時間: <c>.vfx</c> は Update Mode を Delta Time + Ignore Time Scale にする約束で、VFX Graph の 1 フレームの進みは
    /// <c>min(unscaledDeltaTime, VFXManager.maxDeltaTime) × playRate</c> になる。<see cref="SetPlayback"/> は
    /// Framework の倍率（実時間に対する倍率）をこの頭打ちに合わせて <c>playRate</c> へ換算する。
    /// </para>
    /// <para>
    /// VFX Graph の上限: 対応と判定した最初の <see cref="QueryCapability"/> で 1 回だけ、<c>VFXManager.maxDeltaTime</c> を
    /// 実行時の <c>Time.maximumDeltaTime</c> に揃え、<see cref="Dispose"/> で手放す（<see cref="VfxGraphMaxDeltaTimeAlignment"/>。
    /// backend が重なっても最後の 1 つが元に戻し、アプリの終了でも戻す）。揃えないと、VFX Graph の上限と Unity の頭打ちの
    /// 間のフレームで Framework の倍率が変わらず、playRate が書き直されないまま VFX だけが上限で止まる。
    /// この設定はプロセス内の全ての VisualEffect（VfxSystem の外のものを含む）に効く。<c>Time.maximumDeltaTime</c> の
    /// 実行中の変更には追従しないので、backend を作る前に決め、実行中は変えないこと。
    /// </para>
    /// </summary>
    public sealed class VfxGraphBackend : IVfxBackend, IDisposable
    {
        private readonly Func<VfxPlatformFacts> _readFacts;
        private readonly Dictionary<ulong, Entry> _instances = new();

        // backend の生涯で単調増加させ、再利用しない（破棄済み id への遅れた呼び出しが別の instance に届かないように）。
        private ulong _lastId;
        private GameObject? _root;
        private bool _disposed;

        // VFX Graph の上限を揃えた値を持っているときの世代（VfxGraphMaxDeltaTimeAlignment）。持っていなければ null。
        private int? _maxDeltaTimeAlignment;

        public VfxGraphBackend()
            : this(ReadPlatformFacts)
        {
        }

        /// <summary>テスト用: platform の事実の読み取りを差し替える（Mobile 等の非対応環境を再現する）。</summary>
        internal VfxGraphBackend(Func<VfxPlatformFacts> readFacts)
        {
            _readFacts = readFacts ?? throw new ArgumentNullException(nameof(readFacts));
        }

        /// <summary>テスト用: 親なしの instance を置く root。まだ作っていなければ null。</summary>
        internal GameObject? RootForTest => _root;

        /// <summary>テスト用: id に対応する VisualEffect。未知・破棄済みなら null。</summary>
        internal VisualEffect? FindForTest(VfxInstanceId id)
            => _instances.TryGetValue(id.Value, out var entry) && entry.Effect != null ? entry.Effect : null;

        /// <summary>
        /// 能力を判定する。対応と判定した最初の 1 回だけ VFX Graph の上限を揃える（2 回目以降と非対応の判定では書かない。
        /// 非対応環境では VFX Graph の設定に触れない）。
        /// </summary>
        public bool QueryCapability(out string? reason)
        {
            var supported = VfxGraphCapabilityPolicy.Evaluate(_readFacts(), out reason);
            if (supported && !_disposed && !_maxDeltaTimeAlignment.HasValue)
            {
                _maxDeltaTimeAlignment = VfxGraphMaxDeltaTimeAlignment.Acquire();
            }

            return supported;
        }

        public VfxInstanceId Create(in VfxSpawnRequest request)
        {
            if (_disposed)
            {
                return VfxInstanceId.Invalid;
            }

            // GameObject を作る前に拒否する。破棄済みの asset も Unity の == で弾く。
            if (request.Source is not VisualEffectAsset asset || asset == null)
            {
                return VfxInstanceId.Invalid;
            }

            var parent = request.HasParent ? request.Parent : null;
            if (request.HasParent && parent == null)
            {
                return VfxInstanceId.Invalid;
            }

            var id = new VfxInstanceId(++_lastId);
            GameObject? go = null;
            try
            {
                // 非 active のまま組み立てる。active な GameObject に VisualEffect を足すと、その場で有効化されて
                // 初期イベント（既定 OnPlay）で発射する。Create は作るだけで、発射は Play だけが行う。
                go = new GameObject("VFX " + id.Value);
                go.SetActive(false);
                var effect = go.AddComponent<VisualEffect>();
                // 有効化のたびに送られる初期イベントを止める。以後 enabled の切り替えと Reinit は使わない（再び送られるため）。
                // 順序に意味がある: asset を設定した後に書くと、getter は空を返すのに有効化で asset 既定の初期イベント
                // （OnPlay）が送られる（Unity 6000.6 / VFX Graph 17.6 の Editor で実測）。asset より先に書くと送られない。
                effect.initialEventName = string.Empty;
                effect.visualEffectAsset = asset;

                // 親なしは DontDestroyOnLoad の root に置く。active Scene に置くと、所有者と無関係な Scene の unload で
                // Unity が先に壊してしまう。
                var parentTransform = parent != null ? parent : EnsureRoot().transform;
                go.transform.SetParent(parentTransform, false);
                // 要求は world 座標（VfxSpawnRequest の契約）。親の移動・回転に関係なく最終の world transform を一致させる。
                go.transform.SetPositionAndRotation(request.Position, request.Rotation);
                go.SetActive(true);

                _instances.Add(id.Value, new Entry(go, effect));
                return id;
            }
            catch
            {
                // id を返す前の失敗は backend が後始末する（IVfxBackend の契約）。
                _instances.Remove(id.Value);
                if (go != null)
                {
                    Object.Destroy(go);
                }

                throw;
            }
        }

        public bool Play(VfxInstanceId id)
        {
            if (!TryGetEntry(id, out var entry))
            {
                return false;
            }

            // 配置済み VFX の最初の開始は有効化だけにする（初期イベントで自動再生される、VFX Graph の標準の開始）。
            // 有効のまま保存された（authoring の約束違反の）component は既に再生しているので、通常の再トリガにする。
            var firstAdoptedStart = entry.Adopted && !entry.Played && !entry.Effect.enabled;
            entry.Played = true;
            if (firstAdoptedStart)
            {
                entry.Effect.enabled = true;
            }
            else
            {
                entry.Effect.Play();
            }

            return true;
        }

        /// <summary>
        /// 再生状態を書く。倍率は <c>rate × u ÷ min(u, VFXManager.maxDeltaTime)</c>（u = unscaledDeltaTime）で
        /// <c>playRate</c> へ換算する。VFX Graph は 1 フレームの進みを <c>VFXManager.maxDeltaTime</c> で頭打ちにするので、
        /// その分だけ playRate を上げて、Framework が求めた進み（rate × u）に合わせる。
        /// u か上限が 0・負・NaN・∞ のフレームは換算できないので、playRate は前回の値のままにする（pause は書く）。
        /// 上限を <c>Time.maximumDeltaTime</c> に揃えてあるので、Framework の倍率が変わらないフレーム（呼ばれないフレーム）でも
        /// VFX の進みは倍率 × 頭打ちの時間になり、gameplay と一致する。
        /// </summary>
        public void SetPlayback(VfxInstanceId id, bool paused, float rate)
        {
            if (!TryGetEffect(id, out var effect))
            {
                return;
            }

            effect.pause = paused;
            var playRate = ToPlayRate(rate, Time.unscaledDeltaTime, VFXManager.maxDeltaTime);
            if (playRate.HasValue)
            {
                effect.playRate = playRate.Value;
            }
        }

        /// <summary>
        /// 配置済みの <see cref="VisualEffect"/> を登録する。有効化も開始もしない。VisualEffect 以外・破棄済みは無効 id。
        /// </summary>
        public VfxInstanceId Adopt(Object placed)
        {
            if (_disposed)
            {
                return VfxInstanceId.Invalid;
            }

            if (placed is not VisualEffect effect || effect == null)
            {
                return VfxInstanceId.Invalid;
            }

            var id = new VfxInstanceId(++_lastId);
            _instances.Add(id.Value, new Entry(effect.gameObject, effect, adopted: true));
            return id;
        }

        /// <summary>
        /// 配置済み VFX の登録を外す。無効化するだけで、GameObject も component も破棄しない（Scene の所有）。
        /// 破棄済みなら何もしない。未知の id・2 回目の呼び出しは何もしない。
        /// </summary>
        public void Release(VfxInstanceId id)
        {
            if (!_instances.Remove(id.Value, out var entry))
            {
                return;
            }

            ReleaseEntry(entry);
        }

        public void StopEmitting(VfxInstanceId id)
        {
            if (TryGetEffect(id, out var effect))
            {
                effect.Stop();
            }
        }

        public bool HasAliveParticles(VfxInstanceId id)
        {
            // 外から壊された instance には粒子が無いとみなす（消え切り待ちをすぐ終わらせる）。
            if (!TryGetEntry(id, out var entry))
            {
                return false;
            }

            // Play の前は aliveParticleCount だけで答える。system は有効化された直後から粒子が無くても awake を返すので、
            // awake を数えると Create 直後に「粒子あり」と誤る。発射が始まっていなければ読み戻しの遅れも無いので、
            // 本当に発射していれば（初期イベントの抑止が効かなかった等）この値で見える。
            if (!entry.Played)
            {
                return entry.Effect.aliveParticleCount > 0;
            }

            // Play の後は aliveParticleCount を単独では使わない（GPU からの非同期の読み戻しで遅れうる）。
            // 誤判定は回収を遅らせる側に倒し、上限は VfxSystem の timeout が保証する。
            return entry.Effect.HasAnySystemAwake() || entry.Effect.aliveParticleCount > 0;
        }

        // Set*: 名前が無い・型が合わない・instance が壊れている場合は書かずに false を返す（例外にしない）。
        public bool SetFloat(VfxInstanceId id, string name, float value)
        {
            if (!TryGetEffect(id, out var effect) || !effect.HasFloat(name))
            {
                return false;
            }

            effect.SetFloat(name, value);
            return true;
        }

        public bool SetInt(VfxInstanceId id, string name, int value)
        {
            if (!TryGetEffect(id, out var effect) || !effect.HasInt(name))
            {
                return false;
            }

            effect.SetInt(name, value);
            return true;
        }

        public bool SetBool(VfxInstanceId id, string name, bool value)
        {
            if (!TryGetEffect(id, out var effect) || !effect.HasBool(name))
            {
                return false;
            }

            effect.SetBool(name, value);
            return true;
        }

        public bool SetVector3(VfxInstanceId id, string name, Vector3 value)
        {
            if (!TryGetEffect(id, out var effect) || !effect.HasVector3(name))
            {
                return false;
            }

            effect.SetVector3(name, value);
            return true;
        }

        public bool SetVector4(VfxInstanceId id, string name, Vector4 value)
        {
            if (!TryGetEffect(id, out var effect) || !effect.HasVector4(name))
            {
                return false;
            }

            effect.SetVector4(name, value);
            return true;
        }

        /// <summary>
        /// 対応表から先に外し、GameObject が残っていれば同期で無効化してから破棄を要求する。
        /// native の破棄完了はフレーム末なので、それまで source を参照しない状態にしておく
        /// （呼び出し側は Destroy の後に source を解放してよい）。未知の id・2 回目の呼び出しは何もしない。
        /// DestroyImmediate は Bind 所有者の破棄中に呼ばれると失敗しうるので使わない。
        /// </summary>
        public void Destroy(VfxInstanceId id)
        {
            if (!_instances.Remove(id.Value, out var entry))
            {
                return;
            }

            // VfxSystem は配置済みの instance に Destroy を呼ばない（IVfxBackend の契約）。契約違反でも Scene の
            // GameObject は壊さない。
            if (entry.Adopted)
            {
                ReleaseEntry(entry);
                return;
            }

            DestroyEntry(entry);
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            if (_maxDeltaTimeAlignment.HasValue)
            {
                VfxGraphMaxDeltaTimeAlignment.Release(_maxDeltaTimeAlignment.Value);
                _maxDeltaTimeAlignment = null;
            }

            // VfxSystem は先に全 instance を回収しているので、通常ここに残りは無い。残っていたら回収する
            // （配置済みは無効化だけで、Scene の GameObject は壊さない）。
            var remaining = new List<Entry>(_instances.Values);
            _instances.Clear();
            foreach (var entry in remaining)
            {
                if (entry.Adopted)
                {
                    ReleaseEntry(entry);
                }
                else
                {
                    DestroyEntry(entry);
                }
            }

            if (_root != null)
            {
                Object.Destroy(_root);
            }

            _root = null;
        }

        private static VfxPlatformFacts ReadPlatformFacts()
        {
            return new VfxPlatformFacts(
                SystemInfo.supportsComputeShaders,
                SystemInfo.maxComputeBufferInputsVertex,
                QualitySettings.activeColorSpace,
                SystemInfo.graphicsDeviceType);
        }

        /// <summary>
        /// 倍率を playRate に換算する。換算できない（u か上限が 0・負・NaN・∞、結果が有限の 0 以上でない）なら null。
        /// </summary>
        internal static float? ToPlayRate(float rate, float unscaledDeltaTime, float maxDeltaTime)
        {
            if (!IsUsable(unscaledDeltaTime) || !IsUsable(maxDeltaTime))
            {
                return null;
            }

            var playRate = rate * unscaledDeltaTime / Mathf.Min(unscaledDeltaTime, maxDeltaTime);
            return playRate >= 0f && !float.IsInfinity(playRate) ? playRate : null;
        }

        // NaN は比較が全て false になるので > 0 で落ちる。
        private static bool IsUsable(float value) => value > 0f && !float.IsInfinity(value);

        private static void ReleaseEntry(Entry entry)
        {
            // Unity が先に壊した（Scene の unload・Play Mode の終了）component には書かない。
            if (entry.Effect != null)
            {
                entry.Effect.enabled = false;
            }
        }

        private static void DestroyEntry(Entry entry)
        {
            // Unity が先に壊した（Scene の unload・Play Mode の終了）GameObject には書かない。
            if (entry.Effect != null)
            {
                entry.Effect.enabled = false;
                entry.Effect.visualEffectAsset = null;
            }

            if (entry.GameObject != null)
            {
                entry.GameObject.SetActive(false);
                Object.Destroy(entry.GameObject);
            }
        }

        private bool TryGetEffect(VfxInstanceId id, out VisualEffect effect)
        {
            if (TryGetEntry(id, out var entry))
            {
                effect = entry.Effect;
                return true;
            }

            effect = null!;
            return false;
        }

        /// <summary>id に対応し、VisualEffect がまだ破棄されていない entry。</summary>
        private bool TryGetEntry(VfxInstanceId id, out Entry entry)
        {
            if (_instances.TryGetValue(id.Value, out var found) && found.Effect != null)
            {
                entry = found;
                return true;
            }

            entry = null!;
            return false;
        }

        private GameObject EnsureRoot()
        {
            if (_root != null)
            {
                return _root;
            }

            _root = new GameObject("[VfxGraphBackend]");
            Object.DontDestroyOnLoad(_root);
            return _root;
        }

        private sealed class Entry
        {
            public Entry(GameObject gameObject, VisualEffect effect, bool adopted = false)
            {
                GameObject = gameObject;
                Effect = effect;
                Adopted = adopted;
            }

            public GameObject GameObject { get; }

            public VisualEffect Effect { get; }

            /// <summary>Scene に配置済みの VFX を登録したものか。GameObject は Scene の所有で、回収は無効化だけ。</summary>
            public bool Adopted { get; }

            /// <summary>一度でも Play を送ったか。粒子の有無の判定方法を切り替える（<see cref="HasAliveParticles"/>）。</summary>
            public bool Played { get; set; }
        }
    }
}
