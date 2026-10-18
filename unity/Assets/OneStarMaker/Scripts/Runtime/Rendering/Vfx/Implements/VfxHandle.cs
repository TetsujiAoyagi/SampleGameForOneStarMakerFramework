#nullable enable

using System;
using OneStarMaker.Runtime.AssetManagement;
using UnityEngine;

namespace OneStarMaker.Runtime.Rendering.Vfx
{
    /// <summary>
    /// 1 instance の状態機械と、backend 呼び出しの例外境界。
    /// <para>
    /// 状態は Created → Playing → Stopping → Released の一方向で、Released は吸収状態。
    /// Released への遷移は <see cref="Release"/> の1か所だけを通り、「状態を書く → 索引から外す → backend Destroy」
    /// の順に行う。backend 呼び出しより先に状態と索引を確定させるので、Destroy の中から破棄通知や Spawn が
    /// 同期で再入しても二回目の Destroy は起きない。
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
        private float _stoppingElapsed;
        private bool _failureReported;

        internal VfxHandle(
            IVfxBackend backend,
            VfxInstanceId id,
            AssetOwner owner,
            Action<VfxHandle> onReleased,
            Action<VfxHandle, Exception> onFailed)
        {
            _backend = backend;
            Id = id;
            Owner = owner;
            _onReleased = onReleased;
            _onFailed = onFailed;
            State = HandleState.Created;
        }

        internal VfxInstanceId Id { get; }

        internal AssetOwner Owner { get; }

        internal HandleState State { get; private set; }

        public bool IsActive => State != HandleState.Released;

        public bool Play()
        {
            var before = State;
            if (before != HandleState.Created && before != HandleState.Playing)
            {
                // Stopping は Released にしか進まない。消え切り待ちを Play で取り消すことはしない。
                return false;
            }

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

        public void Stop() => Release();

        public void Dispose() => Release();

        public void StopAllowingParticlesToFinish()
        {
            switch (State)
            {
                case HandleState.Created:
                    // backend 契約で Create は発射しないので、未再生なら粒子は無い。
                    Release();
                    return;
                case HandleState.Playing:
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
        /// 回収の唯一の入口。Destroy の要求は高々1回で、例外を投げても再試行しない
        /// （資源が残った場合の回復は backend の責任）。
        /// </summary>
        internal void Release()
        {
            if (State == HandleState.Released)
            {
                return;
            }

            State = HandleState.Released;
            _onReleased(this);
            try
            {
                _backend.Destroy(Id);
            }
            catch (Exception e)
            {
                ReportFailure(e);
            }
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
