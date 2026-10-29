#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using OneStarMaker.Runtime.AssetManagement;
using OneStarMaker.Runtime.Rendering.Vfx;
using OneStarMaker.Runtime.SceneSystem;
using OneStarMaker.Runtime.UpdateSystem.Api;
using SampleGame.InGame.Streaming;
using SampleGame.InGame.World;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.VFX;
using Object = UnityEngine.Object;

namespace SampleGame.Tests.Rendering.Vfx
{
    /// <summary>
    /// Season 直下の major Event Scene（Spring_Event_MultiCellProof）を、実 InGameSession と Season の排他 controller の下で
    /// 実 Scene として Play 中に載せて確かめる。進入は kinematic Rigidbody と Collider を持つ test の物体を trigger の中へ動かす。
    /// <para>
    /// Scene は test が持つ SceneDirector で載せ、Map は実 SceneResource の複製（根は実 InGameSession）。Session の初期 Ensure、
    /// 距離 streaming、companion driver は製品のまま動かし、Focus だけを偽の Flight で与える。factory は Planner の companion set。
    /// </para>
    /// <para>
    /// 寿命の観測は記録する backend と AssetManagement の <see cref="IAssetDiagnostics.GetOwners"/>（graphics なしでも走る）、
    /// 実 <c>VisualEffect</c> の有無は実 backend（graphics が必要）で見る。実 backend では Event が起動した <c>VisualEffect</c> だけを
    /// 数える（起動の前から在るものは除く）。
    /// </para>
    /// </summary>
    [TestFixture]
    public sealed partial class SeasonMajorEventPlayTests : VfxPlayTestFixture
    {
        private const string SpringRoot = "Assets/SampleGame/InGame/InGameSession/Seasons/Spring";
        private const string SessionResourcePath = "Assets/OneStarMakerCommon/SceneMap/InGameSession.asset";
        private const string SeasonResourcePath = SpringRoot + "/Season_Spring.asset";
        private const string OriginResourcePath = SpringRoot + "/Cells/Spring_Cell_0_4/Spring_Cell_0_4.asset";
        private const string CellResourcePath = SpringRoot + "/Cells/Spring_Cell_4_2/Spring_Cell_4_2.asset";
        private const string EnvironmentResourcePath = SpringRoot + "/Cells/Spring_Cell_4_2/Spring_Environment_4_2/Spring_Environment_4_2.asset";
        private const string EventsResourcePath = SpringRoot + "/Cells/Spring_Cell_4_2/Spring_Events_4_2/Spring_Events_4_2.asset";
        private const string EventsScenePath = SpringRoot + "/Cells/Spring_Cell_4_2/Spring_Events_4_2/Spring_Events_4_2.unity";
        private const string EventFolder = SpringRoot + "/Events/Spring_Event_MultiCellProof";
        private const string EventResourcePath = EventFolder + "/Spring_Event_MultiCellProof.asset";
        private const string EventScenePath = EventFolder + "/Spring_Event_MultiCellProof.unity";
        private const string EventVfxAddress = EventFolder + "/Spring_Event_MultiCellProof_Burst.vfx";
        private const string SummerResourcePath = "Assets/SampleGame/InGame/InGameSession/Seasons/Summer/Season_Summer.asset";
        private const string SummerOriginResourcePath = "Assets/SampleGame/InGame/InGameSession/Seasons/Summer/Cells/Summer_Cell_0_4/Summer_Cell_0_4.asset";

        private const string SessionIdentity = "InGameSession";
        private const string SeasonIdentity = "Season_Spring";
        private const string Cell = "Spring_Cell_4_2";
        private const string Environment = "Spring_Environment_4_2";
        private const string Events = "Spring_Events_4_2";
        private const string EventIdentity = "Spring_Event_MultiCellProof";

        // Spring_Cell_4_2 の体積の中心（XZ）。高さは Event の起動位置・trigger と同じ。
        private static readonly Vector3 Cell42Center = new(1125f, 3f, 625f);

        // physics の callback が届くまで・Scene の状態が変わるまでに待つフレーム数の上限（時間待ちは使わない）。
        private const int SceneFrameLimit = 6000;

        private GameObject? _probe;

        // ═══ AC6: 開始 → Stable → 起動位置 2 か所に VFX 2 件。Cell (4,2) と Events を外しても Event と VFX は残る ═══

        [UnityTest]
        public IEnumerator Start_SpawnsAtBothAnchors_AndSurvivesCellUnload()
            => UniTask.ToCoroutine(async () =>
            {
                var backend = new RecordingBackend();
                await WithSessionAsync(CreateFakeHost(backend), async scenes =>
                {
                    Assert.That(scenes.RequestEvent(), Is.EqualTo(MajorEventRequestResult.Accepted));
                    await scenes.WaitEventStableAsync();
                    var anchors = Object.FindObjectsByType<MajorEventVfxAnchor>(FindObjectsInactive.Include)
                        .Select(anchor => anchor.transform.position)
                        .ToArray();
                    Assert.That(backend.Created.Select(created => created.Position), Is.EquivalentTo(anchors));
                    Assert.That(backend.Created, Has.Count.EqualTo(2));

                    // AC6 の時点は「Cell の Removed の次のフレーム」。IsSceneLoaded は unload の開始で false になり、
                    // その時点では子の Events がまだ unload 中なので、Removed の通知を数えて待つ。
                    scenes.MoveFocusAway();
                    await WaitFramesUntilAsync(() => scenes.CellRemovedCount == 1, "Cell (4,2) の Removed が届かない");
                    var cellRemovedFrame = Time.frameCount;
                    await UniTask.NextFrame();

                    TestContext.Out.WriteLine($"cellRemovedFrame={cellRemovedFrame} eventsLoaded={scenes.Director.IsSceneLoaded(Events)}");
                    Assert.That(scenes.Director.IsSceneLoaded(Events), Is.False, "Events (4,2) が残る");
                    Assert.That(scenes.Director.IsSceneStable(EventIdentity), Is.True, "Cell の unload で Event が外れた");
                    Assert.That(backend.Inner.DestroyCount, Is.Zero, "Cell の unload で Event の VFX が回収された");
                });
            });

        // ═══ AC7 (a)・AC8 (a): 記録 backend。完了の trigger でも枝の終了でも、Event の PreUnloading → Destroy 2 回 →
        // source の解放 → Event の Removed の順。枝の終了では、その後に Season の Removed ═══

        [UnityTest]
        public IEnumerator CompletionAndBranchSwitch_RecordingBackend_DestroyBothBeforeSourceRelease()
            => UniTask.ToCoroutine(async () =>
            {
                var backend = new RecordingBackend();
                await WithSessionAsync(CreateFakeHost(backend), async scenes =>
                {
                    ObserveDestroy(scenes, backend);
                    Assert.That(scenes.RequestEvent(), Is.EqualTo(MajorEventRequestResult.Accepted));
                    await scenes.WaitEventStableAsync();
                    Assert.That(scenes.Owners(EventVfxAddress), Does.Contain(AssetOwner.Scene(EventIdentity)), "前提: source を Event Scene が所有する");

                    // AC7 (a): 完了の trigger。
                    await EnterAsync(scenes.Single<MajorEventCompletionTrigger>(EventScenePath).transform.position);
                    await scenes.WaitEventRemovedAsync(1);
                    TestContext.Out.WriteLine("completion:\n" + string.Join("\n", scenes.Timeline));
                    AssertRecoveryOrder(scenes.Timeline);
                    Assert.That(backend.Inner.DestroyCount, Is.EqualTo(2));
                    Assert.That(scenes.EventOwnersAtRemoved, Is.Zero, "Removed の時点で source が残る");
                    await LeaveAsync();

                    // AC8 (a): 再要求した Event が Stable の間に枝を終了する。
                    scenes.Timeline.Clear();
                    Assert.That(scenes.RequestEvent(), Is.EqualTo(MajorEventRequestResult.Accepted), "完了の後の再要求");
                    await scenes.WaitEventStableAsync();
                    var toSummer = scenes.SwitchToSummerAsync();
                    await WaitFramesUntilAsync(() => scenes.SeasonRemovedCount == 1, "Season_Spring の Removed が届かない");
                    TestContext.Out.WriteLine("branch:\n" + string.Join("\n", scenes.Timeline));
                    AssertRecoveryOrder(scenes.Timeline);
                    Assert.That(
                        scenes.Timeline.IndexOf("season Removed"),
                        Is.GreaterThan(scenes.Timeline.FindIndex(line => line.StartsWith("event Removed", StringComparison.Ordinal))),
                        "Season の Removed が Event の Removed より前");
                    Assert.That(backend.Inner.DestroyCount, Is.EqualTo(4));
                    Assert.That(scenes.EventOwnersAtRemoved, Is.Zero);
                    await toSummer;
                });
            });

        // ═══ AC7 (b)・AC8 (b): 実 backend。Event が起動した VisualEffect 2 件が、完了では Event の Removed の次のフレームに、
        // 枝の終了では Season の Removed の次のフレームに 0 件。完了の後の再要求で再び 2 件 ═══

        [UnityTest]
        public IEnumerator CompletionAndBranchSwitch_RealBackend_EventEffectsGoneTheFrameAfterRemoved()
            => UniTask.ToCoroutine(async () =>
            {
                WriteGraphicsFacts();
                AssumeGraphicsSupported();
                UsePcQuality();
                await WithSessionAsync(CreateHost(RealFacts), async scenes =>
                {
                    // AC7 (b): 完了 → 再要求 → 完了。
                    for (var round = 1; round <= 2; round++)
                    {
                        var baseline = AllEffects();
                        Assert.That(scenes.RequestEvent(), Is.EqualTo(MajorEventRequestResult.Accepted), $"round {round} の要求");
                        await scenes.WaitEventStableAsync();
                        var started = await WaitStartedEffectsAsync(baseline, 2);

                        await EnterAsync(scenes.Single<MajorEventCompletionTrigger>(EventScenePath).transform.position);
                        await scenes.WaitEventRemovedAsync(round);
                        await UniTask.NextFrame();
                        Assert.That(started.Count(effect => effect != null), Is.Zero, $"round {round}: Removed の次のフレームに Event の VisualEffect が残る");
                        await LeaveAsync();
                    }

                    // AC8 (b): Event が Stable の間に枝を終了する。
                    var before = AllEffects();
                    Assert.That(scenes.RequestEvent(), Is.EqualTo(MajorEventRequestResult.Accepted));
                    await scenes.WaitEventStableAsync();
                    var live = await WaitStartedEffectsAsync(before, 2);
                    var toSummer = scenes.SwitchToSummerAsync();
                    await WaitFramesUntilAsync(() => scenes.SeasonRemovedCount == 1, "Season_Spring の Removed が届かない");
                    await UniTask.NextFrame();
                    Assert.That(scenes.Director.IsSceneLoaded(EventIdentity), Is.False, "Season の Removed の次のフレームに Event の個体が残る");
                    Assert.That(live.Count(effect => effect != null), Is.Zero, "Season の Removed の次のフレームに Event の VisualEffect が残る");
                    await toSummer;
                });
            });

        // ═══ AC7 (c): 非対応の VfxSystem でも開始・完了が例外なく進み、backend へ何も届かない ═══

        [UnityTest]
        public IEnumerator Unsupported_StartAndComplete_NoBackendCalls()
            => UniTask.ToCoroutine(async () =>
            {
                var backend = new RecordingBackend();
                backend.Inner.Supported = false;
                await WithSessionAsync(CreateFakeHost(backend), async scenes =>
                {
                    Assert.That(scenes.RequestEvent(), Is.EqualTo(MajorEventRequestResult.Accepted));
                    await scenes.WaitEventStableAsync();
                    await EnterAsync(scenes.Single<MajorEventCompletionTrigger>(EventScenePath).transform.position);
                    await scenes.WaitEventRemovedAsync(1);

                    TestContext.Out.WriteLine($"backendCalls={string.Join(",", backend.Inner.Calls.Select(call => call.Op))}");
                    Assert.That(backend.Inner.Calls.All(call => call.Op == "QueryCapability"), Is.True, "QueryCapability 以外の backend 呼び出し");
                    Assert.That(scenes.EventOwnersAtRemoved, Is.Zero);
                });
            });

        // ═══ AC9: Event の窓の外のロード中に Session を unload しても、Event は保留の後に終端し、VFX と source が残らない ═══

        [UnityTest]
        public IEnumerator SessionUnloadDuringEventLoad_EventEndsAfterPending_AndRequestsAreRejected()
            => UniTask.ToCoroutine(async () =>
            {
                WriteGraphicsFacts();
                AssumeGraphicsSupported();
                UsePcQuality();
                await WithSessionAsync(CreateHost(RealFacts), async scenes =>
                {
                    var baseline = AllEffects();
                    var session = scenes.Session;
                    Assert.That(scenes.RequestEvent(), Is.EqualTo(MajorEventRequestResult.Accepted));

                    // キャンセル窓（PreLoading）を出て、Stable より前（Unity Scene のロード中）に Session を unload する。
                    // 本番の退出と同じく controller の待ちを通さない。
                    await WaitFramesUntilAsync(
                        () => scenes.EventState is SceneState.Loading or SceneState.Loaded or SceneState.WaitLoadChildScene,
                        "Event が窓の外のロードに入らない");
                    var stateAtUnload = scenes.EventState;
                    Assert.That(scenes.Director.IsSceneStable(EventIdentity), Is.False, "前提: Session の unload の時点で Event は Stable でない");

                    // 親が健全な間（Season は Event の Add 中も Stable）のロード中の同じ identity は AlreadyActive で、Add しない。
                    Assert.That(session.RequestMajorEvent(EventIdentity), Is.EqualTo(MajorEventRequestResult.AlreadyActive), "Session の unload の前のロード中の要求");
                    Assert.That(scenes.EventPreLoadingCount, Is.EqualTo(1), "ロード中の要求で Event の Add が発行された");

                    // 表示付きの unload は表示の await の後に unload へ入るので、開始の次のフレームに Season の unload が始まるよう表示なしにする。
                    var unload = scenes.Director.UnloadScene(SessionIdentity, LoadingDisplayType.None);

                    await UniTask.NextFrame();
                    // 前提が崩れたときは AlreadyActive の合否で決めず、前提不成立として落とす。
                    Assert.That(scenes.Director.GetLoadedScene(SeasonIdentity) == null, Is.True, "前提不成立: Session の unload の開始の次のフレームに Season の unload が始まっていない");
                    Assert.That(scenes.EventRemovedCount, Is.Zero, "前提不成立: 要求の前に Event が終端した");
                    // driver は Session の停止（子の保留の後）より前で Loading のまま。拒否は Season の未ロードの判定による（理由は単体で見る）。
                    Assert.That(session.RequestMajorEvent(EventIdentity), Is.EqualTo(MajorEventRequestResult.Rejected), "Session の unload の開始の後の要求");
                    await unload;

                    await scenes.WaitEventRemovedAsync(1);
                    await UniTask.NextFrame();
                    TestContext.Out.WriteLine($"stateAtUnload={stateAtUnload}\n" + string.Join("\n", scenes.Timeline));
                    Assert.That(scenes.Director.IsSceneLoaded(EventIdentity), Is.False, "Removed の次のフレームに Event の個体が残る");
                    Assert.That(StartedEffects(baseline).Count, Is.Zero, "Removed の次のフレームに Event の VisualEffect が残る");
                    Assert.That(scenes.Owners(EventVfxAddress), Has.No.Member(AssetOwner.Scene(EventIdentity)), "Event の所有者が残る");
                    Assert.That(scenes.EventPreLoadingCount, Is.EqualTo(1), "Session の unload の後に Event の Add が発行された");
                });
            });

        // ═══ AC10: Planner の companion set で Environment を載せず、Events の開始 trigger から Event が起動し、完了で回収される ═══

        [UnityTest]
        public IEnumerator Planner_StartTriggerInCellEvents_StartsEvent_AndCompletionRecoversIt()
            => UniTask.ToCoroutine(async () =>
            {
                var backend = new RecordingBackend();
                await WithSessionAsync(CreateFakeHost(backend), async scenes =>
                {
                    Assert.That(scenes.Director.IsSceneLoaded(Environment), Is.False, "Planner で Environment が載っている");
                    Assert.That(scenes.Session.LoadedChildSceneIdentities, Does.Contain(Events), "Events (4,2) を companion driver が載せていない");

                    await EnterAsync(scenes.Single<MajorEventStartTrigger>(EventsScenePath).transform.position);
                    await scenes.WaitEventStableAsync();
                    Assert.That(backend.Created, Has.Count.EqualTo(2));
                    await LeaveAsync();

                    await EnterAsync(scenes.Single<MajorEventCompletionTrigger>(EventScenePath).transform.position);
                    await scenes.WaitEventRemovedAsync(1);
                    Assert.That(backend.Inner.DestroyCount, Is.EqualTo(2));
                    Assert.That(scenes.Director.IsSceneLoaded(Environment), Is.False, "Planner で Environment が載っている");
                    TestContext.Out.WriteLine(string.Join("\n", scenes.Timeline));
                });
            });

        // ─── 操作と観測 ───

        private VfxRuntimeHost CreateFakeHost(RecordingBackend backend)
            => Track(VfxRuntimeHost.Create(backend, NullLoggerFactory.Instance, UpdateSystemRuntime.Coordinator!));

        private static VisualEffect[] AllEffects()
            => Object.FindObjectsByType<VisualEffect>(FindObjectsInactive.Include);

        private static List<VisualEffect> StartedEffects(VisualEffect[] baseline)
            => AllEffects().Where(effect => !baseline.Contains(effect)).ToList();

        /// <summary>起動の前に無かった VisualEffect が <paramref name="count"/> 件そろって有効になるまで待つ（有効化は登録の後の LateUpdate）。</summary>
        private static async UniTask<List<VisualEffect>> WaitStartedEffectsAsync(VisualEffect[] baseline, int count)
        {
            await WaitFramesUntilAsync(
                () => StartedEffects(baseline) is { } started && started.Count == count && started.All(effect => effect.enabled),
                $"Event が起動した VisualEffect が {count} 件そろって有効にならない");
            return StartedEffects(baseline);
        }

        // Destroy の時点で source がまだ Event Scene に所有されていることを時系列に残す（source の解放より前の回収）。
        private static void ObserveDestroy(SessionScenes scenes, RecordingBackend backend)
        {
            backend.OnDestroy = id => scenes.Log(
                $"destroy id={id.Value} ownedByEvent={scenes.Owners(EventVfxAddress).Contains(AssetOwner.Scene(EventIdentity))}");
        }

        /// <summary>
        /// Event の PreUnloading の dispatch で Destroy 2 回 → source の解放 → Removed の順を見る。
        /// VfxSystem は test より先に director のイベントを購読するので、同じ PreUnloading の dispatch の中の Destroy は
        /// test の「event PreUnloading」の行より前に並ぶ。購読順に依存しないよう、Destroy は直前の Stable の後・
        /// PreUnloaded の前にあり、その時点で source を Event が所有している（解放より前）ことで読む。
        /// </summary>
        private static void AssertRecoveryOrder(List<string> timeline)
        {
            var preUnloading = timeline.IndexOf("event PreUnloading");
            var preUnloaded = timeline.IndexOf("event PreUnloaded");
            var stable = preUnloading < 0 ? -1 : timeline.LastIndexOf("event Stable", preUnloading);
            var destroys = timeline.Select((line, index) => (line, index))
                .Where(item => item.line.StartsWith("destroy id=", StringComparison.Ordinal))
                .ToArray();
            var removed = timeline.FindIndex(line => line.StartsWith("event Removed", StringComparison.Ordinal));
            Assert.That(preUnloading, Is.GreaterThanOrEqualTo(0), "Event の PreUnloading が記録されていない");
            Assert.That(stable, Is.GreaterThanOrEqualTo(0), "PreUnloading の前に Event の Stable が記録されていない");
            Assert.That(preUnloaded, Is.GreaterThan(preUnloading), "Event の PreUnloaded が PreUnloading の後に記録されていない");
            Assert.That(removed, Is.GreaterThan(preUnloaded), "Event の Removed が PreUnloaded の後に記録されていない");
            Assert.That(destroys, Has.Length.EqualTo(2), "Event の VFX の Destroy が 2 回でない");
            Assert.That(destroys.All(item => item.index > stable && item.index < preUnloaded), Is.True, "Destroy が Event の Stable と PreUnloaded の間に無い");
            Assert.That(destroys.All(item => item.line.EndsWith("ownedByEvent=True", StringComparison.Ordinal)), Is.True, "Destroy の時点で source が解放済み");
        }

        /// <summary>test の物体（kinematic Rigidbody + Collider）を目標の上空に置いて physics に載せ、目標へ動かす。</summary>
        private async UniTask EnterAsync(Vector3 target)
        {
            var probe = _probe;
            if (probe == null)
            {
                probe = NewObject("SeasonMajorEventPlayTests.Probe");
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

        /// <summary>test の director が載せた Scene を、本体の成否にかかわらず本体の直後に外す。</summary>
        private async UniTask WithSessionAsync(VfxRuntimeHost host, Func<SessionScenes, UniTask> body)
        {
            _probe = null;
            var scenes = SessionScenes.Create(host);
            try
            {
                await scenes.StartSessionAsync();
                await body(scenes);
            }
            finally
            {
                await scenes.DisposeAsync();
            }
        }
    }
}
