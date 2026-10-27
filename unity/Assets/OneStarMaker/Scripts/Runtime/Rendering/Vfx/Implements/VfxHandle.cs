#nullable enable

using System;
using OneStarMaker.Runtime.AssetManagement;
using OneStarMaker.Runtime.UpdateSystem.Api;
using UnityEngine;

namespace OneStarMaker.Runtime.Rendering.Vfx
{
    /// <summary>
    /// 1 instance の状態機械と、backend 呼び出しの例外境界。
    /// <para>
    /// 状態は Created → Playing → Stopping → Released の一方向で、Released は吸収状態。
    /// Released への遷移は <see cref="Release"/> の1か所だけを通り、「状態を書く → 索引から外す → backend の回収」
    /// の順に行う。backend の回収は、動的な instance なら Destroy、Scene に配置済みの VFX（adopted）なら Release
    /// （無効化と登録解除だけで GameObject を壊さない）。backend 呼び出しより先に状態と索引を確定させるので、
    /// 回収の中から破棄通知や Spawn が同期で再入しても二回目の回収は起きない。
    /// </para>
    /// <para>
    /// 時間: handle は所属する時間の領域（Layer id、null は unscaled）と、backend へ前回書いた再生状態
    /// (paused, rate) と、開始の保留を持つ。計算は <see cref="VfxTimeLayerBinding"/> に任せ、ここは
    /// 「値が変わったときだけ書く」「止まっている間は開始を backend に届けない」「開始は状態を書いた後」の順序だけを守る。
    /// 消え切り待ちの timeout は領域の時間で数える（止まっている間は進まない）。
    /// </para>
    /// <para>
    /// backend の Play / StopEmitting / HasAliveParticles / Set* の呼び出し中にも、同じ handle の Stop や
    /// 所有者の終端が再入しうる。呼び出しの後で状態を書くのは、戻った時点の状態が呼び出し前と同じときだけにする。
    /// 再入で先へ進んでいたら書き戻さない（前の状態へ戻すと Released から復活してしまう）。
    /// </para>
    /// <para>
    /// VfxSystem の型を参照しない。回収 callback と失敗記録 callback だけを受け取るので、状態機械を単体でテストできる。
    /// </para>
    /// </summary>
    internal sealed class VfxHandle : IVfxHandle
    {
        internal enum HandleState
        {
            Created,
            Playing,
            Stopping,
            Released,
        }

        private readonly IVfxBackend _backend;
        private readonly Action<VfxHandle> _onReleased;
        private readonly Action<VfxHandle, Exception> _onFailed;
        private readonly IVfxTimeSource? _timeSource;
        private float _stoppingElapsed;
        private bool _failureReported;

        // backend へ前回書いた再生状態。初期値は backend の契約上の初期状態（止まっていない、倍率 1）。
        private bool _writtenPaused;
        private float _writtenRate = VfxTimeLayerBinding.InitialRate;

        // 開始の保留。止まった領域での Play と、adopted の自動開始で立つ。止まっていない最初の反映点で backend へ届ける。
        private bool _startPending;

        /// <param name="timeSource">null なら領域は常に「止まっていない」とみなし、再生状態を書かない。</param>
        /// <param name="timeLayerId">所属する時間の領域の Layer id。null は unscaled。</param>
        /// <param name="placed">adopted の instance なら登録した配置済みの component。動的な instance なら null。</param>
        internal VfxHandle(
            IVfxBackend backend,
            VfxInstanceId id,
            AssetOwner owner,
            Action<VfxHandle> onReleased,
            Action<VfxHandle, Exception> onFailed,
            IVfxTimeSource? timeSource = null,
            string? timeLayerId = UpdateLayerIds.Gameplay,
            UnityEngine.Object? placed = null)
        {
            _backend = backend;
            Id = id;
            Owner = owner;
            _onReleased = onReleased;
            _onFailed = onFailed;
            _timeSource = timeSource;
            TimeLayerId = timeLayerId;
            Placed = placed;

            // 参照の有無で種別を決める（破棄済みでも adopted であることは変わらない）。偽 null の判定ではない。
            IsAdopted = !ReferenceEquals(placed, null);
            State = HandleState.Created;

            // 配置済み VFX は「自動で開始するもの」。登録の時点では開始せず、最初の止まっていない反映点で開始する。
            _startPending = IsAdopted;
        }

        internal VfxInstanceId Id { get; }

        internal AssetOwner Owner { get; }

        internal HandleState State { get; private set; }

        /// <summary>所属する時間の領域の Layer id。null は unscaled。</summary>
        internal string? TimeLayerId { get; }

        /// <summary>Scene に配置済みの VFX を登録した instance か。回収で Destroy ではなく backend の Release を呼ぶ。</summary>
        internal bool IsAdopted { get; }

        /// <summary>adopted の instance が登録した component（二重登録の検出に使う）。破棄済みでも参照は残る。</summary>
        internal UnityEngine.Object? Placed { get; }

        /// <summary>テスト用: 開始が保留されているか。</summary>
        internal bool IsStartPending => _startPending;

        public bool IsActive => State != HandleState.Released;

        public bool Play()
        {
            var before = State;
            if (before != HandleState.Created && before != HandleState.Playing)
            {
                // Stopping は Released にしか進まない。消え切り待ちを Play で取り消すことはしない。
                return false;
            }

            VfxTimeLayerBinding.EvaluateDomain(_timeSource, TimeLayerId, out var paused, out var domainDelta);
            if (paused || _startPending)
            {
                // 止まった領域では開始を backend に届けない。保留済み（adopted の自動開始を含む）なら反映点に任せる。
                _startPending = true;
                return true;
            }

            // 開始は常に「状態の反映 → 開始」の順。そのフレームの反映点より後に作られた instance でも、
            // 領域の倍率を書いてから発射する。
            var rate = VfxTimeLayerBinding.ComputeRate(paused, domainDelta, UnscaledDeltaTime, _writtenRate);
            if (!WritePlayback(paused, rate) || State != before)
            {
                return false;
            }

            return StartOnBackend(before);
        }

        public void Stop() => Release();

        public void Dispose() => Release();

        public void StopAllowingParticlesToFinish()
        {
            switch (State)
            {
                case HandleState.Created:
                    // backend 契約で Create・Adopt は発射しないので、未再生なら粒子は無い。開始の保留中でも同じで、
                    // 開始を届けずに回収する。
                    Release();
                    return;
                case HandleState.Playing:
                    // 保留中の再トリガは消え切り待ちと両立しないので捨てる。
                    _startPending = false;
                    // StopEmitting の前に Stopping を書く。呼び出し中に同じ操作が再入しても StopEmitting は1回で済み、
                    // Dispose が再入した場合は Released が上書きするので Stopping へ戻らない。
                    State = HandleState.Stopping;
                    _stoppingElapsed = 0f;
                    try
                    {
                        _backend.StopEmitting(Id);
                    }
                    catch (Exception e)
                    {
                        Fail(e);
                    }

                    return;
                default:
                    // Stopping 中の再呼び出しは待ちを延長しない。Released は何もしない。
                    return;
            }
        }

        public bool SetFloat(string name, float value)
            => SetParameter(name, value, static (b, id, n, v) => b.SetFloat(id, n, v));

        public bool SetInt(string name, int value)
            => SetParameter(name, value, static (b, id, n, v) => b.SetInt(id, n, v));

        public bool SetBool(string name, bool value)
            => SetParameter(name, value, static (b, id, n, v) => b.SetBool(id, n, v));

        public bool SetVector3(string name, Vector3 value)
            => SetParameter(name, value, static (b, id, n, v) => b.SetVector3(id, n, v));

        public bool SetVector4(string name, Vector4 value)
            => SetParameter(name, value, static (b, id, n, v) => b.SetVector4(id, n, v));

        /// <summary>
        /// 消え切り待ちを進める。timeout に達したら粒子判定を呼ばずに回収し、そうでなければ粒子が無くなった時点で回収する。
        /// delta が負・NaN・∞なら 0 として扱う（待ち時間を減らさず、NaN で即 timeout にもしない）。delta が 0 でも粒子判定は行う。
        /// </summary>
        internal void AdvanceStopping(float deltaSeconds, float timeoutSeconds)
        {
            if (State != HandleState.Stopping)
            {
                return;
            }

            if (!(deltaSeconds > 0f) || float.IsInfinity(deltaSeconds))
            {
                deltaSeconds = 0f;
            }

            _stoppingElapsed += deltaSeconds;
            if (_stoppingElapsed >= timeoutSeconds)
            {
                Release();
                return;
            }

            bool alive;
            try
            {
                alive = _backend.HasAliveParticles(Id);
            }
            catch (Exception e)
            {
                Fail(e);
                return;
            }

            if (State == HandleState.Stopping && !alive)
            {
                Release();
            }
        }

        /// <summary>
        /// 反映点（<c>Vfx</c> Layer の LateUpdate）での 1 instance の処理。順序は
        /// 「領域の状態を書く（変わったときだけ）→ 保留した開始を届ける（止まっていなければ）→ 消え切り待ちを領域の時間で進める」。
        /// 所属 Layer が止まっていても呼ばれるので、Pause と Resume のどちらも同じフレームの simulation の前に反映される。
        /// </summary>
        internal void ApplyFrame(float timeoutSeconds)
        {
            if (State == HandleState.Released)
            {
                return;
            }

            VfxTimeLayerBinding.EvaluateDomain(_timeSource, TimeLayerId, out var paused, out var domainDelta);
            var rate = VfxTimeLayerBinding.ComputeRate(paused, domainDelta, UnscaledDeltaTime, _writtenRate);
            if (!WritePlayback(paused, rate))
            {
                return;
            }

            if (_startPending && !paused)
            {
                _startPending = false;
                var before = State;
                if (before == HandleState.Created || before == HandleState.Playing)
                {
                    StartOnBackend(before);
                }
            }

            if (State == HandleState.Stopping)
            {
                AdvanceStopping(domainDelta, timeoutSeconds);
            }
        }

        /// <summary>
        /// 回収の唯一の入口。backend の回収（動的は Destroy、adopted は Release）の要求は高々1回で、
        /// 例外を投げても再試行しない（資源が残った場合の回復は backend の責任）。
        /// </summary>
        internal void Release()
        {
            if (State == HandleState.Released)
            {
                return;
            }

            State = HandleState.Released;
            _startPending = false;
            _onReleased(this);
            try
            {
                if (IsAdopted)
                {
                    _backend.Release(Id);
                }
                else
                {
                    _backend.Destroy(Id);
                }
            }
            catch (Exception e)
            {
                ReportFailure(e);
            }
        }

        private float UnscaledDeltaTime => _timeSource?.UnscaledDeltaTime ?? 0f;

        /// <summary>
        /// 再生状態が前回書いた値と違うときだけ backend に書く。backend の失敗で回収されたら false。
        /// 値は呼び出しの前に記録する（呼び出し中の再入で同じ値を二度書かないため）。
        /// </summary>
        private bool WritePlayback(bool paused, float rate)
        {
            if (_timeSource == null || (paused == _writtenPaused && rate == _writtenRate))
            {
                return State != HandleState.Released;
            }

            _writtenPaused = paused;
            _writtenRate = rate;
            try
            {
                _backend.SetPlayback(Id, paused, rate);
            }
            catch (Exception e)
            {
                Fail(e);
                return false;
            }

            return State != HandleState.Released;
        }

        private bool StartOnBackend(HandleState before)
        {
            bool played;
            try
            {
                played = _backend.Play(Id);
            }
            catch (Exception e)
            {
                Fail(e);
                return false;
            }

            if (State != before || !played)
            {
                return false;
            }

            State = HandleState.Playing;
            return true;
        }

        private bool SetParameter<T>(string name, T value, Func<IVfxBackend, VfxInstanceId, string, T, bool> call)
        {
            var before = State;
            if (before == HandleState.Released)
            {
                return false;
            }

            // 名前の誤りは呼び出し側の入力誤りで、instance を失わせない。
            if (string.IsNullOrEmpty(name))
            {
                return false;
            }

            bool applied;
            try
            {
                applied = call(_backend, Id, name, value);
            }
            catch (Exception e)
            {
                Fail(e);
                return false;
            }

            return State == before && applied;
        }

        private void Fail(Exception e)
        {
            ReportFailure(e);
            Release();
        }

        /// <summary>handle ごとに最初の失敗だけを記録する。回収中の Destroy の失敗は、先に記録があれば追記しない。</summary>
        private void ReportFailure(Exception e)
        {
            if (_failureReported)
            {
                return;
            }

            _failureReported = true;
            _onFailed(this, e);
        }
    }
}
