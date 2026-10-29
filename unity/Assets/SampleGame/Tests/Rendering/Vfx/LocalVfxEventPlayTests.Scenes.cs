#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using Cysharp.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using OneStarMaker.Runtime;
using OneStarMaker.Runtime.AssetManagement;
using OneStarMaker.Runtime.CameraSystem.Abstractions;
using OneStarMaker.Runtime.Rendering.Environments;
using OneStarMaker.Runtime.Rendering.Vfx;
using OneStarMaker.Runtime.SceneSystem;
using OneStarMaker.Tests.CameraSystem;
using OneStarMaker.Tests.Rendering.Vfx;
using OneStarMaker.Tests.SceneSystem.Helpers;
using R3;
using SampleGame.Common.Rendering.Vfx;
using SampleGame.DependOnAll;
using SampleGame.InGame.Streaming;
using SampleGame.InGame.World;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.VFX;
using ILogger = Microsoft.Extensions.Logging.ILogger;
using Object = UnityEngine.Object;
using RuntimeCameraSystem = OneStarMaker.Runtime.CameraSystem.Core.CameraSystem;

namespace SampleGame.Tests.Rendering.Vfx
{
    /// <summary>
    /// <see cref="LocalVfxEventPlayTests"/> の Scene の載せ方と観測（test の SceneDirector、記録する backend、ログの計数）。
    /// </summary>
    public sealed partial class LocalVfxEventPlayTests
    {
        // ─── 偽物と観測 ───

        /// <summary>
        /// 呼び出しを <see cref="FakeVfxBackend"/> へ委ね、Create の位置と、Destroy の時点のフレーム・source の所有・
        /// trigger と Events の Unity Scene の生存を記録する backend。
        /// </summary>
        private sealed class RecordingBackend : IVfxBackend
        {
            public FakeVfxBackend Inner { get; } = new();

            public List<(VfxInstanceId Id, Vector3 Position, Quaternion Rotation)> Created { get; } = new();

            public List<DestroyRecord> Destroyed { get; } = new();

            /// <summary>Destroy の時点の観測（Events が載っている間だけ設定する）。</summary>
            public Func<VfxInstanceId, DestroyRecord>? Observe { get; set; }

            public bool QueryCapability(out string? reason) => Inner.QueryCapability(out reason);

            public VfxInstanceId Create(in VfxSpawnRequest request)
            {
                var id = Inner.Create(request);
                Created.Add((id, request.Position, request.Rotation));
                return id;
            }

            public bool Play(VfxInstanceId id) => Inner.Play(id);

            public void SetPlayback(VfxInstanceId id, bool paused, float rate) => Inner.SetPlayback(id, paused, rate);

            public VfxInstanceId Adopt(Object placed) => Inner.Adopt(placed);

            public void Release(VfxInstanceId id) => Inner.Release(id);

            public void StopEmitting(VfxInstanceId id) => Inner.StopEmitting(id);

            public bool HasAliveParticles(VfxInstanceId id) => Inner.HasAliveParticles(id);

            public bool SetFloat(VfxInstanceId id, string name, float value) => Inner.SetFloat(id, name, value);

            public bool SetInt(VfxInstanceId id, string name, int value) => Inner.SetInt(id, name, value);

            public bool SetBool(VfxInstanceId id, string name, bool value) => Inner.SetBool(id, name, value);

            public bool SetVector3(VfxInstanceId id, string name, Vector3 value) => Inner.SetVector3(id, name, value);

            public bool SetVector4(VfxInstanceId id, string name, Vector4 value) => Inner.SetVector4(id, name, value);

            public void Destroy(VfxInstanceId id)
            {
                Destroyed.Add(Observe?.Invoke(id) ?? new DestroyRecord(id, Time.frameCount, false, false, false));
                Inner.Destroy(id);
            }
        }

        private readonly struct DestroyRecord
        {
            public DestroyRecord(VfxInstanceId id, int frame, bool ownedByEvents, bool triggerAlive, bool eventsSceneLoaded)
            {
                Id = id;
                Frame = frame;
                OwnedByEvents = ownedByEvents;
                TriggerAlive = triggerAlive;
                EventsSceneLoaded = eventsSceneLoaded;
            }

            public VfxInstanceId Id { get; }

            public int Frame { get; }

            public bool OwnedByEvents { get; }

            public bool TriggerAlive { get; }

            public bool EventsSceneLoaded { get; }
        }

        /// <summary>全 category の Warning 以上のログを数える factory（非対応の診断ログの回数の判定）。</summary>
        private sealed class CountingLoggerFactory : ILoggerFactory
        {
            public int Count { get; private set; }

            public ILogger CreateLogger(string categoryName) => new CountingLogger(this);

            public void AddProvider(ILoggerProvider provider)
            {
            }

            public void Dispose()
            {
            }

            private sealed class CountingLogger : ILogger
            {
                private readonly CountingLoggerFactory _owner;

                public CountingLogger(CountingLoggerFactory owner)
                {
                    _owner = owner;
                }

                public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
                {
                    if (logLevel >= LogLevel.Warning)
                    {
                        _owner.Count++;
                    }
                }

                public bool IsEnabled(LogLevel logLevel) => true;

                public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            }
        }

        /// <summary>
        /// test が所有する SceneDirector と、Season_Spring・Cell (4,2)・Events (4,2) だけの複製の Map。
        /// Events の lifecycle の通知を時系列に記録し、Removed の時点の Destroy 回数と source の owner 数を取る。
        /// </summary>
        private sealed class EventsScenes
        {
            private static readonly BindingFlags Instance = BindingFlags.Instance | BindingFlags.NonPublic;
            private readonly SceneResourceMap _map;
            private readonly List<SceneResource> _clones;
            private readonly IAssetDiagnostics _assets;
            private readonly RecordingBackend? _backend;
            private readonly IDisposable _subscription;
            private int _removedCount;
            private string? _address;

            private EventsScenes(SceneDirector director, SceneResourceMap map, List<SceneResource> clones, IAssetDiagnostics assets, RecordingBackend? backend)
            {
                Director = director;
                _map = map;
                _clones = clones;
                _assets = assets;
                _backend = backend;
                _subscription = director.OnSceneEvent.Subscribe(OnSceneEvent);
            }

            internal SceneDirector Director { get; }

            internal LocalVfxEventTrigger Trigger { get; private set; } = null!;

            internal List<string> Timeline { get; } = new();

            internal int DestroyCountAtRemoved { get; private set; } = -1;

            internal int OwnersAtRemoved { get; private set; } = -1;

            internal static EventsScenes Create(VfxRuntimeHost host, RecordingBackend? backend)
            {
                var season = Clone(LoadResource(SeasonResourcePath), null);
                var cell = Clone(LoadResource(CellResourcePath), season);
                var events = Clone(LoadResource(EventsResourcePath), cell);
                var clones = new List<SceneResource> { season, cell, events };
                var map = SceneTestHelper.CreateSceneResourceMap(clones.ToArray());

                var initializer = Type.GetType("SampleGame.DependOnAll.AppInitializer, SampleGame.DependOnAll")
                    ?.GetField("s_instance", BindingFlags.Static | BindingFlags.NonPublic)?.GetValue(null);
                Assert.That(initializer, Is.Not.Null, "実アプリの起動 instance が無い");
                var appDirector = typeof(AbstractApplicationInitializer).GetField("_sceneDirector", Instance)?.GetValue(initializer);
                Assert.That(appDirector, Is.Not.Null, "起動の失敗: アプリの SceneDirector が無い");
                object Field(string name) => typeof(SceneDirector).GetField(name, Instance)!.GetValue(appDirector)!;

                var factory = new GameSceneFactory(
                    NullLoggerFactory.Instance,
                    new RuntimeCameraSystem(new FakeCameraBackend()),
                    new NoopCameraBackgroundApplier(),
                    CellCompanionSet.Planner,
                    new UnusedRenderEnvironment(),
                    host.System);
                var ctor = typeof(SceneDirector).GetConstructors(Instance).Single(c => c.GetParameters().Length == 7);
                var assets = Field("_assetManagement");
                var director = (SceneDirector)ctor.Invoke(new[]
                {
                    factory, Field("_uiCommon"), map, Field("_loadingDisplay"), assets,
                    Field("_sceneVariant"), Field("_useContentDirectory"),
                });

                host.BindSceneLifecycle(director.OnSceneEvent, director);
                Assert.That(host.IsSceneLifecycleBound, Is.True, "前提: テストの VfxSystem が director に接続した");
                return new EventsScenes(director, map, clones, (IAssetDiagnostics)assets, backend);
            }

            internal async UniTask AddCellAndEventsAsync()
            {
                _removedCount = 0;
                DestroyCountAtRemoved = -1;
                OwnersAtRemoved = -1;
                await Director.AddScene(Cell, null, CancellationToken.None);
                await Director.AddScene(Events, null, CancellationToken.None);
                Assert.That(Director.IsSceneStable(Events), Is.True);
                var triggers = Object.FindObjectsByType<LocalVfxEventTrigger>(FindObjectsInactive.Include)
                    .Where(trigger => trigger.gameObject.scene.path == EventsScenePath)
                    .ToArray();
                Assert.That(triggers, Has.Length.EqualTo(1), "前提: Events Scene に trigger が 1 つ");
                Trigger = triggers[0];
                _address = Trigger.VfxAddress;
                if (_backend != null)
                {
                    var trigger = Trigger;
                    var key = AssetKey.FromAddress(trigger.VfxAddress);
                    _backend.Observe = id =>
                    {
                        var record = new DestroyRecord(
                            id,
                            Time.frameCount,
                            Owners(key).Contains(AssetOwner.Scene(Events)),
                            trigger != null,
                            SceneManager.GetSceneByPath(EventsScenePath).isLoaded);
                        Log($"destroy id={id.Value}");
                        return record;
                    };
                }
            }

            internal IReadOnlyList<AssetOwner> Owners(AssetKey key) => _assets.GetOwners(key);

            internal bool IsLoaded(string identity) => Director.IsSceneLoaded(identity);

            internal void Log(string line) => Timeline.Add(line);

            internal async UniTask WaitRemovedAsync(string identity)
            {
                for (var frame = 0; frame < SceneFrameLimit && Director.IsSceneLoaded(identity); frame++)
                {
                    await UniTask.NextFrame();
                }

                Assert.That(Director.IsSceneLoaded(identity), Is.False, $"{identity} が外れない");
                Assert.That(_removedCount, Is.EqualTo(1), $"{identity} の Removed が 1 回でない");
            }

            internal async UniTask DisposeAsync()
            {
                try
                {
                    if (Director.IsSceneLoaded(Season))
                    {
                        await Director.UnloadScene(Season);
                    }
                }
                finally
                {
                    _subscription.Dispose();
                    Director.Dispose();
                    Object.Destroy(_map);
                    foreach (var clone in _clones)
                    {
                        if (clone != null)
                        {
                            Object.Destroy(clone);
                        }
                    }
                }
            }

            private void OnSceneEvent(SceneEvent e)
            {
                if (e.SceneIdentify != Events)
                {
                    return;
                }

                if (e.Type == SceneEventType.StateChanged)
                {
                    Log($"events {e.State}");
                }
                else if (e.Type == SceneEventType.Removed)
                {
                    _removedCount++;
                    DestroyCountAtRemoved = _backend?.Inner.DestroyCount ?? -1;
                    OwnersAtRemoved = CountOwnersOfTriggerSource();
                    Log("events Removed");
                }
            }

            // trigger は Removed の時点で破棄済みなので、ロード時に読んだ address を使う。
            private int CountOwnersOfTriggerSource()
                => _address == null ? -1 : _assets.GetOwners(AssetKey.FromAddress(_address)).Count;

            private static SceneResource LoadResource(string path)
            {
                var resource = AssetDatabase.LoadAssetAtPath<SceneResource>(path);
                Assert.That(resource != null, Is.True, $"実 SceneResource が無い: {path}");
                return resource!;
            }

            private static SceneResource Clone(SceneResource original, SceneResource? parent)
            {
                var clone = Object.Instantiate(original);
                clone.name = original.name;
                typeof(SceneResource).GetField("_children", Instance)!.SetValue(clone, new List<SceneResource>());
                typeof(SceneResource).GetField("_parent", Instance)!.SetValue(clone, null);
                if (parent != null)
                {
                    SceneTestHelper.AddChild(parent, clone);
                }

                return clone;
            }
        }

        private sealed class NoopCameraBackgroundApplier : ICameraBackgroundApplier
        {
            public void SetClearFlag(ICameraView view, ClearFlag clearFlag, Color color)
            {
            }
        }

        private sealed class UnusedRenderEnvironment : IRenderEnvironment
        {
            public bool HasActiveOwner => false;

            public RenderEnvironmentLease Acquire(object ownerKey)
                => throw new InvalidOperationException("局所 VFX の Play テストは RenderEnvironment を使わない");
        }
    }
}
