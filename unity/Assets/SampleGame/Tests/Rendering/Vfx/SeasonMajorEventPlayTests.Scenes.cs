#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using Cysharp.Threading.Tasks;
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
using SampleGame.DependOnAll;
using SampleGame.InGame;
using SampleGame.InGame.Player;
using SampleGame.InGame.Streaming;
using SampleGame.InGame.World;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;
using RuntimeCameraSystem = OneStarMaker.Runtime.CameraSystem.Core.CameraSystem;

namespace SampleGame.Tests.Rendering.Vfx
{
    /// <summary>
    /// <see cref="SeasonMajorEventPlayTests"/> の Scene の載せ方と観測（test の SceneDirector、実 InGameSession を根にした複製の Map、
    /// 記録する backend）。
    /// </summary>
    public sealed partial class SeasonMajorEventPlayTests
    {
        /// <summary>
        /// 呼び出しを <see cref="FakeVfxBackend"/> へ委ね、Create の位置と、Destroy の時点の source の所有を記録する backend
        /// （<see cref="LocalVfxEventPlayTests"/> と同じ分け方で、共通のテスト基盤には足さない）。
        /// </summary>
        private sealed class RecordingBackend : IVfxBackend
        {
            public FakeVfxBackend Inner { get; } = new();

            public List<(VfxInstanceId Id, Vector3 Position)> Created { get; } = new();

            /// <summary>Destroy の時点で呼ぶ観測（時系列に 1 行足す）。</summary>
            public Action<VfxInstanceId>? OnDestroy { get; set; }

            public bool QueryCapability(out string? reason) => Inner.QueryCapability(out reason);

            public VfxInstanceId Create(in VfxSpawnRequest request)
            {
                var id = Inner.Create(request);
                Created.Add((id, request.Position));
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
                OnDestroy?.Invoke(id);
                Inner.Destroy(id);
            }
        }

        private sealed class FakeFlight : IFlightReadModel
        {
            public Vector3 Position { get; set; }

            public bool InputEnabled => true;
        }

        /// <summary>
        /// test が所有する SceneDirector と、実 SceneResource の複製の Map。根は実 InGameSession で、Season_Spring の枝には
        /// Cell (0,4)（初期 Ensure が固定で載せる）、Cell (4,2) とその Environment・Events、major Event だけを残す。
        /// Season_Summer と Cell (0,4) は枝の切替の先。Lighting・Atmosphere は載せない（Event の VFX だけを数える）。
        /// </summary>
        private sealed class SessionScenes
        {
            private static readonly BindingFlags Instance = BindingFlags.Instance | BindingFlags.NonPublic;
            private readonly SceneResourceMap _map;
            private readonly List<SceneResource> _clones;
            private readonly IAssetDiagnostics _assets;
            private readonly IDisposable _subscription;
            private readonly FakeFlight _flight = new();

            private SessionScenes(SceneDirector director, SceneResourceMap map, List<SceneResource> clones, IAssetDiagnostics assets)
            {
                Director = director;
                _map = map;
                _clones = clones;
                _assets = assets;
                _subscription = director.OnSceneEvent.Subscribe(OnSceneEvent);
            }

            internal SceneDirector Director { get; }

            internal InGameSession Session { get; private set; } = null!;

            internal List<string> Timeline { get; } = new();

            internal int EventPreLoadingCount { get; private set; }

            internal int EventRemovedCount { get; private set; }

            internal int SeasonRemovedCount { get; private set; }

            internal int CellRemovedCount { get; private set; }

            internal SceneState? EventState { get; private set; }

            /// <summary>Event の Removed の時点の、Event の VFX source の所有者の数（-1 は未観測）。</summary>
            internal int EventOwnersAtRemoved { get; private set; } = -1;

            internal static SessionScenes Create(VfxRuntimeHost host)
            {
                var session = Clone(LoadResource(SessionResourcePath), null);
                var season = Clone(LoadResource(SeasonResourcePath), session);
                var origin = Clone(LoadResource(OriginResourcePath), season);
                var cell = Clone(LoadResource(CellResourcePath), season);
                var environment = Clone(LoadResource(EnvironmentResourcePath), cell);
                var events = Clone(LoadResource(EventsResourcePath), cell);
                var majorEvent = Clone(LoadResource(EventResourcePath), season);
                var summer = Clone(LoadResource(SummerResourcePath), session);
                var summerOrigin = Clone(LoadResource(SummerOriginResourcePath), summer);
                var clones = new List<SceneResource> { session, season, origin, cell, environment, events, majorEvent, summer, summerOrigin };
                var map = SceneTestHelper.CreateSceneResourceMap(clones.ToArray());

                var initializer = Type.GetType("SampleGame.DependOnAll.AppInitializer, SampleGame.DependOnAll")
                    ?.GetField("s_instance", BindingFlags.Static | BindingFlags.NonPublic)?.GetValue(null);
                Assert.That(initializer, Is.Not.Null, "実アプリの起動 instance が無い");
                var appDirector = typeof(AbstractApplicationInitializer).GetField("_sceneDirector", Instance)?.GetValue(initializer);
                Assert.That(appDirector, Is.Not.Null, "起動の失敗: アプリの SceneDirector が無い");
                object Field(string name) => typeof(SceneDirector).GetField(name, Instance)!.GetValue(appDirector)!;

                // Planner の companion set: Cell の companion は Events だけを載せる（Environment を載せない）。
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
                return new SessionScenes(director, map, clones, (IAssetDiagnostics)assets);
            }

            /// <summary>
            /// 実 InGameSession を載せ、既存の初期 Ensure（Spring と Cell (0,4)）の完了を待ち、Focus を Cell (4,2) に置いて
            /// 既存の距離 streaming に Cell (4,2) を、既存の companion driver に Events (4,2) を載せさせる（テストは直接 Add しない）。
            /// </summary>
            internal async UniTask StartSessionAsync()
            {
                await Director.AddScene(SessionIdentity, null, CancellationToken.None);
                Session = (InGameSession)Director.GetLoadedScene(SessionIdentity)!;
                await WaitFramesUntilAsync(() => Session.IsWorldReady, "初期 Ensure が完了しない");

                _flight.Position = Cell42Center;
                Session.RegisterFlight(_flight);
                await WaitFramesUntilAsync(() => Director.IsSceneStable(Cell) && Director.IsSceneStable(Events), "Cell (4,2) と Events が載らない");
                Log($"events loaded by companion driver: {Session.LoadedChildSceneIdentities.Contains(Events)}");
            }

            /// <summary>Focus を全ての Cell から遠ざけ、距離 streaming に Cell (4,2)（と子の Events）を外させる。</summary>
            internal void MoveFocusAway() => _flight.Position = Cell42Center + new Vector3(0f, 0f, 10000f);

            internal MajorEventRequestResult RequestEvent() => Session.RequestMajorEvent(EventIdentity);

            internal UniTask WaitEventStableAsync()
                => WaitFramesUntilAsync(() => Director.IsSceneStable(EventIdentity), "Event が Stable にならない");

            internal async UniTask WaitEventRemovedAsync(int expectedRemoved)
            {
                await WaitFramesUntilAsync(() => EventRemovedCount >= expectedRemoved, "Event の Removed が届かない");
                Assert.That(EventRemovedCount, Is.EqualTo(expectedRemoved), "Event の Removed の回数");
            }

            internal IReadOnlyList<AssetOwner> Owners(string address) => _assets.GetOwners(AssetKey.FromAddress(address));

            internal T Single<T>(string scenePath) where T : Component
            {
                var found = Object.FindObjectsByType<T>(FindObjectsInactive.Include)
                    .Where(component => component.gameObject.scene.path == scenePath)
                    .ToArray();
                Assert.That(found, Has.Length.EqualTo(1), $"前提: {scenePath} に {typeof(T).Name} が 1 つ");
                return found[0];
            }

            /// <summary>枝の切替（Summer の Cell (0,4) へ）で Season_Spring を unload する。controller の本番の入口を reflection で呼ぶ。</summary>
            internal UniTask SwitchToSummerAsync()
            {
                var controller = (SessionSeasonController)typeof(InGameSession)
                    .GetField("_seasonController", Instance)!.GetValue(Session)!;
                return controller.RequestSeasonAsync("Summer", 0, 4, CancellationToken.None);
            }

            internal void Log(string line) => Timeline.Add(line);

            internal async UniTask DisposeAsync()
            {
                try
                {
                    if (Director.IsSceneLoaded(SessionIdentity))
                    {
                        await Director.UnloadScene(SessionIdentity);
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
                if (e.SceneIdentify == SeasonIdentity && e.Type == SceneEventType.Removed)
                {
                    SeasonRemovedCount++;
                    Log("season Removed");
                    return;
                }

                if (e.SceneIdentify == Cell && e.Type == SceneEventType.Removed)
                {
                    CellRemovedCount++;
                    Log("cell Removed");
                    return;
                }

                if (e.SceneIdentify != EventIdentity)
                {
                    return;
                }

                if (e.Type == SceneEventType.StateChanged)
                {
                    EventState = e.State;
                    if (e.State == SceneState.PreLoading)
                    {
                        EventPreLoadingCount++;
                    }

                    Log($"event {e.State}");
                }
                else if (e.Type == SceneEventType.Removed)
                {
                    EventRemovedCount++;
                    EventState = null;
                    EventOwnersAtRemoved = _assets.GetOwners(AssetKey.FromAddress(EventVfxAddress)).Count;
                    Log($"event Removed owners={EventOwnersAtRemoved}");
                }
            }

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

        private static async UniTask WaitFramesUntilAsync(Func<bool> condition, string message)
        {
            for (var frame = 0; frame < SceneFrameLimit && !condition(); frame++)
            {
                await UniTask.NextFrame();
            }

            Assert.That(condition(), Is.True, message);
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
                => throw new InvalidOperationException("major Event の Play テストは Lighting を載せない");
        }
    }
}
