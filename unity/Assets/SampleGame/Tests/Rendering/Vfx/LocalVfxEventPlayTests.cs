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
using OneStarMaker.Runtime.UpdateSystem.Api;
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
using Object = UnityEngine.Object;
using RuntimeCameraSystem = OneStarMaker.Runtime.CameraSystem.Core.CameraSystem;

namespace SampleGame.Tests.Rendering.Vfx
{
    /// <summary>
    /// Cell (4,2) の Events Scene（Spring_Events_4_2）に置いた局所 VFX の trigger を、実 Scene として Play 中に載せて確かめる。
    /// 進入は kinematic Rigidbody と Collider を持つ test の物体を trigger の中へ動かし、physics の callback を通して起こす。
    /// <para>
    /// Scene は test が持つ SceneDirector で載せる（<see cref="VfxScenePlayTests"/> と同じ理由: アプリの director では
    /// 距離ストリーミングが Cell を外しうる）。Map は実 SceneResource の複製で Season_Spring・Cell・Events だけを持つ
    /// （VFX・Environment の companion と Atmosphere は載せない。Planner の profile で VFX Scene に依存しないことの確認を兼ねる）。
    /// </para>
    /// <para>
    /// 寿命の観測は偽 backend（<see cref="FakeVfxBackend"/>）の呼び出しの記録と、アプリの AssetManagement の
    /// <see cref="IAssetDiagnostics.GetOwners"/> で行い、graphics なしでも走る。実 <c>VisualEffect</c> の有無だけを
    /// 実 backend のテスト（graphics が必要）で見る。
    /// </para>
    /// </summary>
    [TestFixture]
    public sealed partial class LocalVfxEventPlayTests : VfxPlayTestFixture
    {
        private const string SeasonsRoot = "Assets/SampleGame/InGame/InGameSession/Seasons/Spring";
        private const string SeasonResourcePath = SeasonsRoot + "/Season_Spring.asset";
        private const string CellResourcePath = SeasonsRoot + "/Cells/Spring_Cell_4_2/Spring_Cell_4_2.asset";
        private const string EventsResourcePath = SeasonsRoot + "/Cells/Spring_Cell_4_2/Spring_Events_4_2/Spring_Events_4_2.asset";
        private const string EventsScenePath = SeasonsRoot + "/Cells/Spring_Cell_4_2/Spring_Events_4_2/Spring_Events_4_2.unity";

        private const string Season = "Season_Spring";
        private const string Cell = "Spring_Cell_4_2";
        private const string Events = "Spring_Events_4_2";

        // physics の callback が届くまで・Scene が外れるまでに待つフレーム数の上限（時間待ちは使わない）。
        private const int PhysicsFrameLimit = 120;
        private const int SceneFrameLimit = 3000;

        // ═══ AC7・AC8: 進入で起動し、Cell の unload で source の解放より前に 1 回だけ回収される ═══

        [UnityTest]
        public IEnumerator Enter_SpawnsAtTrigger_AndCellUnloadDestroysBeforeSourceRelease()
            => UniTask.ToCoroutine(async () =>
            {
                var backend = new RecordingBackend();
                var host = CreateFakeHost(backend);
                await WithEventsAsync(host, backend, async scenes =>
                {
                    var trigger = scenes.Trigger;
                    var key = AssetKey.FromAddress(trigger.VfxAddress);
                    Assert.That(scenes.Owners(key), Does.Contain(AssetOwner.Scene(Events)), "前提: source を Events Scene が所有する");
                    Assert.That(scenes.IsLoaded(CellVfxIdentity), Is.False, "VFX companion を載せていない");
                    scenes.Log($"source loaded owners={string.Join(",", scenes.Owners(key))}");

                    await EnterAsync(trigger);

                    Assert.That(backend.Inner.CreateCount, Is.EqualTo(1));
                    var created = backend.Created.Single();
                    Assert.That(backend.Inner.CountOf("Play", created.Id), Is.EqualTo(1));
                    Assert.That(created.Position, Is.EqualTo(trigger.transform.position));
                    Assert.That(created.Rotation, Is.EqualTo(trigger.transform.rotation));
                    scenes.Log($"create id={created.Id.Value} position={created.Position:R}");

                    await scenes.Director.UnloadScene(Cell);
                    await scenes.WaitRemovedAsync(Events);

                    var destroyed = backend.Destroyed.Single();
                    scenes.Log($"destroyed-record id={destroyed.Id.Value} ownersHadEvents={destroyed.OwnedByEvents} " +
                               $"triggerAlive={destroyed.TriggerAlive} eventsSceneLoaded={destroyed.EventsSceneLoaded}");
                    scenes.Log($"removed destroyCount={scenes.DestroyCountAtRemoved} owners={scenes.OwnersAtRemoved}");
                    TestContext.Out.WriteLine(string.Join("\n", scenes.Timeline));

                    Assert.That(destroyed.Id, Is.EqualTo(created.Id));
                    Assert.That(backend.Inner.DestroyCountOf(created.Id), Is.EqualTo(1));
                    var preUnloading = scenes.Timeline.IndexOf("events PreUnloading");
                    var destroyLine = scenes.Timeline.FindIndex(line => line.StartsWith("destroy id=", StringComparison.Ordinal));
                    Assert.That(preUnloading, Is.GreaterThanOrEqualTo(0), "Events の PreUnloading が記録されていない");
                    Assert.That(destroyLine, Is.GreaterThan(preUnloading), "Destroy が Events の PreUnloading より前");
                    Assert.That(destroyed.OwnedByEvents, Is.True, "Destroy の時点で source がまだ Events Scene に所有されている");
                    Assert.That(destroyed.TriggerAlive, Is.True, "Destroy は Unity Scene の unload（trigger の破棄）より前");
                    Assert.That(destroyed.EventsSceneLoaded, Is.True, "Destroy は Unity Scene の unload より前");
                    Assert.That(scenes.DestroyCountAtRemoved, Is.EqualTo(1), "Removed の時点で Destroy は 1 回");
                    Assert.That(scenes.OwnersAtRemoved, Is.Zero, "Removed の時点で source が残る");
                    Assert.That(backend.Inner.DestroyCount, Is.EqualTo(1), "Removed の後に Destroy が増えた");
                });
            });

        // ═══ AC9: 実 backend で、起動中は trigger の位置に有効な VisualEffect が 1 つあり、Removed の次のフレームに 0 件 ═══

        [UnityTest]
        public IEnumerator Supported_RealBackend_OneEffectAtTrigger_AndNoneTheFrameAfterRemoved()
            => UniTask.ToCoroutine(async () =>
            {
                WriteGraphicsFacts();
                AssumeGraphicsSupported();
                UsePcQuality();
                var host = CreateHost(RealFacts);
                await WithEventsAsync(host, null, async scenes =>
                {
                    Assert.That(CountVisualEffects(), Is.Zero, "前提: Cell と Events は VisualEffect を持たない");
                    await EnterAsync(scenes.Trigger);

                    // 有効化は登録の後の最初の反映点（LateUpdate）で起きる。
                    var frames = 0;
                    for (; frames < SceneFrameLimit && !AllEffects().Any(effect => effect.enabled); frames++)
                    {
                        await UniTask.NextFrame();
                    }

                    var effects = AllEffects();
                    TestContext.Out.WriteLine($"effects={effects.Length} enabledAfterFrames={frames}");
                    Assert.That(effects, Has.Length.EqualTo(1));
                    Assert.That(effects[0].enabled, Is.True);
                    Assert.That(effects[0].transform.position, Is.EqualTo(scenes.Trigger.transform.position));

                    await scenes.Director.UnloadScene(Cell);
                    await scenes.WaitRemovedAsync(Events);
                    await UniTask.NextFrame();

                    TestContext.Out.WriteLine($"effectsTheFrameAfterRemoved={CountVisualEffects()}");
                    Assert.That(CountVisualEffects(), Is.Zero, "Removed の次のフレームに VisualEffect が残る");
                });
            });

        // ═══ AC10: trigger の GameObject を破棄したフレームに回収が要求される ═══

        [UnityTest]
        public IEnumerator DestroyTriggerObject_DestroysInSameFrame_AndCellUnloadDoesNotDestroyAgain()
            => UniTask.ToCoroutine(async () =>
            {
                var backend = new RecordingBackend();
                var host = CreateFakeHost(backend);
                await WithEventsAsync(host, backend, async scenes =>
                {
                    await EnterAsync(scenes.Trigger);
                    var created = backend.Created.Single();

                    var frame = Time.frameCount;
                    Object.Destroy(scenes.Trigger.gameObject);
                    await UniTask.NextFrame();

                    var destroyed = backend.Destroyed.Single();
                    TestContext.Out.WriteLine($"destroyFrame={destroyed.Frame} objectDestroyFrame={frame}");
                    Assert.That(destroyed.Id, Is.EqualTo(created.Id));
                    Assert.That(destroyed.Frame, Is.EqualTo(frame), "回収が次のフレームの Tick まで遅れた");

                    await scenes.Director.UnloadScene(Cell);
                    await scenes.WaitRemovedAsync(Events);
                    Assert.That(backend.Inner.DestroyCountOf(created.Id), Is.EqualTo(1));
                });
            });

        // ═══ AC11: Cell を載せ直すと、同じ trigger から再び起動できる ═══

        [UnityTest]
        public IEnumerator ReloadCellAndEvents_TriggerSpawnsAgain_AndOldInstanceStaysDestroyedOnce()
            => UniTask.ToCoroutine(async () =>
            {
                var backend = new RecordingBackend();
                var host = CreateFakeHost(backend);
                await WithEventsAsync(host, backend, async scenes =>
                {
                    await EnterAsync(scenes.Trigger);
                    var first = backend.Created.Single();
                    await scenes.Director.UnloadScene(Cell);
                    await scenes.WaitRemovedAsync(Events);

                    // 載せ直した trigger がロードの時点で物体と重なって進入を受けないよう、先に外へ出しておく。
                    await LeaveAsync();
                    await scenes.AddCellAndEventsAsync();
                    await EnterAsync(scenes.Trigger);

                    Assert.That(backend.Inner.CreateCount, Is.EqualTo(2), "再ロードの後に起動しない");
                    Assert.That(backend.Inner.DestroyCountOf(first.Id), Is.EqualTo(1));
                });
            });

        // ═══ AC12: 非対応では例外・backend 呼び出し・追加の診断ログが無く、source も残らない ═══

        [UnityTest]
        public IEnumerator Unsupported_EnterAndUnload_NoBackendCallsNoExtraLogs_AndNoSourceLeft()
            => UniTask.ToCoroutine(async () =>
            {
                var backend = new RecordingBackend();
                backend.Inner.Supported = false;
                var loggers = new CountingLoggerFactory();
                var host = Track(VfxRuntimeHost.Create(backend, loggers, UpdateSystemRuntime.Coordinator!));
                Assert.That(loggers.Count, Is.EqualTo(1), "前提: 起動時の診断ログは 1 件");
                await WithEventsAsync(host, backend, async scenes =>
                {
                    await EnterAsync(scenes.Trigger);
                    await scenes.Director.UnloadScene(Cell);
                    await scenes.WaitRemovedAsync(Events);

                    TestContext.Out.WriteLine($"backendCalls={string.Join(",", backend.Inner.Calls.Select(call => call.Op))} logs={loggers.Count}");
                    Assert.That(backend.Inner.Calls.All(call => call.Op == "QueryCapability"), Is.True, "QueryCapability 以外の backend 呼び出し");
                    Assert.That(loggers.Count, Is.EqualTo(1), "起動時の 1 件以外の診断ログ");
                    Assert.That(scenes.OwnersAtRemoved, Is.Zero);
                    LogAssert.NoUnexpectedReceived();
                });
            });

        // ─── 操作 ───

        private const string CellVfxIdentity = "Spring_VFX_4_2";

        private VfxRuntimeHost CreateFakeHost(RecordingBackend backend)
            => Track(VfxRuntimeHost.Create(backend, NullLoggerFactory.Instance, UpdateSystemRuntime.Coordinator!));

        private static VisualEffect[] AllEffects()
            => Object.FindObjectsByType<VisualEffect>(FindObjectsInactive.Include);

        /// <summary>test の物体（kinematic Rigidbody + Collider）を trigger の外に置いて physics に載せ、trigger の中へ動かす。</summary>
        private async UniTask EnterAsync(LocalVfxEventTrigger trigger)
        {
            var target = trigger.transform.position;
            var probe = _probe;
            if (probe == null)
            {
                probe = NewObject("LocalVfxEventPlayTests.Probe");
                probe.AddComponent<SphereCollider>().radius = 0.25f;
                var body = probe.AddComponent<Rigidbody>();
                body.isKinematic = true;
                body.useGravity = false;
                _probe = probe;
            }

            var rigidbody = probe.GetComponent<Rigidbody>();
            rigidbody.position = target + Vector3.up * 500f;
            probe.transform.position = rigidbody.position;
            await WaitFixedUpdatesAsync(2);
            rigidbody.MovePosition(target);
            await WaitFixedUpdatesAsync(3);
            await UniTask.NextFrame();
        }

        private GameObject? _probe;

        private async UniTask LeaveAsync()
        {
            if (_probe == null)
            {
                return;
            }

            var rigidbody = _probe.GetComponent<Rigidbody>();
            rigidbody.position += Vector3.up * 500f;
            _probe.transform.position = rigidbody.position;
            await WaitFixedUpdatesAsync(2);
        }

        private static async UniTask WaitFixedUpdatesAsync(int count)
        {
            for (var i = 0; i < count; i++)
            {
                await UniTask.WaitForFixedUpdate();
            }
        }

        /// <summary>test の director が載せた Scene を、本体の成否にかかわらず本体の直後に外す（VfxScenePlayTests と同じ理由）。</summary>
        private async UniTask WithEventsAsync(VfxRuntimeHost host, RecordingBackend? backend, Func<EventsScenes, UniTask> body)
        {
            _probe = null;
            var scenes = EventsScenes.Create(host, backend);
            try
            {
                await scenes.AddCellAndEventsAsync();
                await body(scenes);
            }
            finally
            {
                await scenes.DisposeAsync();
            }
        }
    }
}
