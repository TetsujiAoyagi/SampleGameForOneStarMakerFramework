#nullable enable

using System;
using System.Collections.Generic;
using OneStarMaker.Runtime.Rendering.Vfx;
using UnityEngine;

namespace OneStarMaker.Tests.Rendering.Vfx
{
    /// <summary>
    /// 呼び出し回数・順序を記録し、例外・戻り値・再入を注入できる偽 backend。
    /// d1 の受け入れ条件は Framework が backend に何をいつ何回要求するかで判定するので、
    /// この記録がそのまま判定材料になる。実 VFX の挙動の証明には使わない。
    /// </summary>
    internal sealed class FakeVfxBackend : IVfxBackend
    {
        private ulong _nextId = 1;

        // ─── 設定 ───

        public bool Supported { get; set; } = true;

        public string? UnsupportedReason { get; set; } = "fake-unsupported";

        public Exception? CapabilityException { get; set; }

        /// <summary>null でなければ Create はこの値を返す（無効 id・重複 id の注入）。</summary>
        public Func<VfxInstanceId>? NextIdOverride { get; set; }

        public bool PlayResult { get; set; } = true;

        public bool SetResult { get; set; } = true;

        public bool AliveParticles { get; set; } = true;

        /// <summary>操作名と id を受け取り、投げる例外を返す。null を返せば正常に進む。</summary>
        public Func<string, VfxInstanceId, Exception?>? Fault { get; set; }

        // ─── 再入の注入（backend 呼び出しの中で呼ばれる） ───

        public Action<VfxInstanceId>? OnCreate { get; set; }

        public Action<VfxInstanceId>? OnPlay { get; set; }

        public Action<VfxInstanceId>? OnStopEmitting { get; set; }

        public Action<VfxInstanceId>? OnHasAliveParticles { get; set; }

        public Action<VfxInstanceId>? OnSet { get; set; }

        public Action<VfxInstanceId>? OnDestroy { get; set; }

        // ─── 記録 ───

        public int QueryCapabilityCount { get; private set; }

        public int CreateCount { get; private set; }

        public int PlayCount { get; private set; }

        public int StopEmittingCount { get; private set; }

        public int HasAliveParticlesCount { get; private set; }

        public int SetCount { get; private set; }

        public int DestroyCount { get; private set; }

        /// <summary>全呼び出しの順序。QueryCapability は id を持たないので 0 で記録する。</summary>
        public List<(string Op, ulong Id)> Calls { get; } = new();

        public List<(string Kind, ulong Id, string Name, object Value)> SetCalls { get; } = new();

        public int TotalCallCount => Calls.Count;

        public int DestroyCountOf(VfxInstanceId id)
        {
            var count = 0;
            foreach (var call in Calls)
            {
                if (call.Op == "Destroy" && call.Id == id.Value)
                {
                    count++;
                }
            }

            return count;
        }

        /// <summary>指定 id への呼び出し（全操作）の数。Destroy 後の呼び出し0件の判定に使う。</summary>
        public int CallCountOf(VfxInstanceId id)
        {
            var count = 0;
            foreach (var call in Calls)
            {
                if (call.Id == id.Value)
                {
                    count++;
                }
            }

            return count;
        }

        public bool QueryCapability(out string? reason)
        {
            QueryCapabilityCount++;
            Calls.Add(("QueryCapability", 0));
            if (CapabilityException != null)
            {
                throw CapabilityException;
            }

            reason = Supported ? null : UnsupportedReason;
            return Supported;
        }

        public VfxInstanceId Create(in VfxSpawnRequest request)
        {
            CreateCount++;
            var id = NextIdOverride != null ? NextIdOverride() : new VfxInstanceId(_nextId++);
            Calls.Add(("Create", id.Value));
            ThrowIfFaulted("Create", id);
            OnCreate?.Invoke(id);
            return id;
        }

        public bool Play(VfxInstanceId id)
        {
            PlayCount++;
            Calls.Add(("Play", id.Value));
            ThrowIfFaulted("Play", id);
            OnPlay?.Invoke(id);
            return PlayResult;
        }

        public void StopEmitting(VfxInstanceId id)
        {
            StopEmittingCount++;
            Calls.Add(("StopEmitting", id.Value));
            ThrowIfFaulted("StopEmitting", id);
            OnStopEmitting?.Invoke(id);
        }

        public bool HasAliveParticles(VfxInstanceId id)
        {
            HasAliveParticlesCount++;
            Calls.Add(("HasAliveParticles", id.Value));
            ThrowIfFaulted("HasAliveParticles", id);
            OnHasAliveParticles?.Invoke(id);
            return AliveParticles;
        }

        public bool SetFloat(VfxInstanceId id, string name, float value) => RecordSet("Float", id, name, value);

        public bool SetInt(VfxInstanceId id, string name, int value) => RecordSet("Int", id, name, value);

        public bool SetBool(VfxInstanceId id, string name, bool value) => RecordSet("Bool", id, name, value);

        public bool SetVector3(VfxInstanceId id, string name, Vector3 value) => RecordSet("Vector3", id, name, value);

        public bool SetVector4(VfxInstanceId id, string name, Vector4 value) => RecordSet("Vector4", id, name, value);

        public void Destroy(VfxInstanceId id)
        {
            DestroyCount++;
            Calls.Add(("Destroy", id.Value));
            // 再入を先に起こしてから例外を投げる。回収中の再入と Destroy 失敗を同じテストで組み合わせられるようにする。
            OnDestroy?.Invoke(id);
            ThrowIfFaulted("Destroy", id);
        }

        private bool RecordSet(string kind, VfxInstanceId id, string name, object value)
        {
            SetCount++;
            Calls.Add(("Set" + kind, id.Value));
            SetCalls.Add((kind, id.Value, name, value));
            ThrowIfFaulted("Set", id);
            OnSet?.Invoke(id);
            return SetResult;
        }

        private void ThrowIfFaulted(string op, VfxInstanceId id)
        {
            var exception = Fault?.Invoke(op, id);
            if (exception != null)
            {
                throw exception;
            }
        }
    }
}
