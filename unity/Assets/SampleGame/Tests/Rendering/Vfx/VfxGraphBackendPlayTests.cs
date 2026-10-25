#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using OneStarMaker.Runtime;
using OneStarMaker.Runtime.AssetManagement;
using OneStarMaker.Runtime.Rendering.Vfx;
using OneStarMaker.Tests.Rendering.Vfx;
using SampleGame.Common.Rendering.Vfx;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.VFX;
using Object = UnityEngine.Object;

namespace SampleGame.Tests.Rendering.Vfx
{
    /// <summary>
    /// 実 backend の契約（発射しない Create・拒否・id・Destroy の耐性・Set*）、非対応・未判定での停止、
    /// 停止と回収（生きた coordinator から Tick される host を通す）、配置先、Framework の composition の存在を Play 中に見る。
    /// 描画は <see cref="VfxGraphRenderPlayTests"/>。
    /// </summary>
    [TestFixture]
    public sealed class VfxGraphBackendPlayTests : VfxPlayTestFixture
    {
        // ═══════════════════════════════════════════
        //  Framework の composition（起動時に作られている）
        // ═══════════════════════════════════════════

        [UnityTest]
        public IEnumerator Startup_FrameworkHostExists_TickActive_AndBoundToSceneLifecycle()
        {
            var host = AbstractApplicationInitializer.ActiveVfxRuntimeHost!;
            Assert.That(host.IsTickElementActive, Is.True);

            // Scene lifecycle への接続は AfterSceneLoad（非同期）の SceneDirector 生成の直後に起きる。
            for (var frame = 0; frame < StartupFrameLimit && !host.IsSceneLifecycleBound; frame++)
            {
                yield return null;
            }

            Assert.That(host.IsSceneLifecycleBound, Is.True,
                "起動の失敗（AfterSceneLoad が SceneDirector を作っていない）、または Scene lifecycle への接続の失敗");
        }

        // ═══════════════════════════════════════════
        //  非対応・未判定（graphics 不要）
        // ═══════════════════════════════════════════

        [UnityTest]
        public IEnumerator Unsupported_WarnsOnceFromCtor_SpawnsNothing_AndPlacedVfxStaysDisabled()
        {
            var logger = new FakeVfxLogger();
            var backend = Track(new VfxGraphBackend(UnsupportedFacts));
            var before = CountVisualEffects();

            // ctor の Warning から数える。
            var system = Track(new VfxSystem(backend, logger));
            Assert.That(logger.WarningCount, Is.EqualTo(1));
            Assert.That(logger.TotalCount, Is.EqualTo(1));
            Assert.That(VfxCapabilityState.Current, Is.EqualTo(VfxCapability.Unsupported));

            for (var i = 0; i < 10; i++)
            {
                IVfxHandle? handle = null;
                Assert.DoesNotThrow(() => handle = system.Spawn(new VfxSpawnRequest(ShortLived, Vector3.zero, Quaternion.identity), AssetOwner.App));
                Assert.That(handle!.IsActive, Is.False);
                Assert.That(handle.Play(), Is.False);
            }

            Assert.That(logger.TotalCount, Is.EqualTo(1), "Spawn の間にログは増えない");
            Assert.That(CountVisualEffects() - before, Is.EqualTo(0));

            var placed = InstantiatePlaced();
            yield return null;
            Assert.That(placed.enabled, Is.False);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator Undetermined_PlacedVfxStaysDisabled()
        {
            var backend = new VfxGraphBackend(UnsupportedFacts);
            Track(new VfxSystem(backend, new FakeVfxLogger())).Dispose();
            backend.Dispose();
            Assert.That(VfxCapabilityState.Current, Is.EqualTo(VfxCapability.Undetermined), "前提: writer の Dispose で未判定");

            var placed = InstantiatePlaced();
            yield return null;
            Assert.That(placed.enabled, Is.False);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator Supported_PlacedVfxIsEnabled()
        {
            AssumeGraphicsSupported();
            var backend = Track(new VfxGraphBackend(RealFacts));
            Track(new VfxSystem(backend, new FakeVfxLogger()));
            Assert.That(VfxCapabilityState.Current, Is.EqualTo(VfxCapability.Supported));

            var placed = InstantiatePlaced();
            yield return null;
            Assert.That(placed.enabled, Is.True);
            LogAssert.NoUnexpectedReceived();
        }

        private VisualEffect InstantiatePlaced()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlacedPrefabPath);
            Assert.That(prefab != null, Is.True, "fixture の prefab が無い");
            Assert.That(prefab!.GetComponent<VisualEffect>().enabled, Is.False, "前提: prefab の VisualEffect は無効のまま保存されている");
            var instance = Object.Instantiate(prefab);
            Objects.Add(instance);
            return instance.GetComponent<VisualEffect>();
        }

        // ═══════════════════════════════════════════
        //  backend の契約
        // ═══════════════════════════════════════════

        [UnityTest]
        public IEnumerator Create_DoesNotEmitUntilPlay()
        {
            AssumeGraphicsSupported();
            var backend = Track(new VfxGraphBackend());
            var id = backend.Create(new VfxSpawnRequest(ShortLived, Vector3.zero, Quaternion.identity));
            Assert.That(id.IsValid, Is.True);

            for (var frame = 0; frame < 120; frame++)
            {
                yield return null;
                Assert.That(backend.HasAliveParticles(id), Is.False, "Create だけでは発射しない（frame " + frame + "）");
            }

            Assert.That(backend.Play(id), Is.True);
            var alive = false;
            for (var frame = 0; frame < ParticleFrameLimit && !alive; frame++)
            {
                yield return null;
                alive = backend.HasAliveParticles(id);
            }

            Assert.That(alive, Is.True, "Play の後、上限フレーム内に粒子が出る");
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void Create_NonVfxSource_ReturnsInvalidWithoutCreatingObjects()
        {
            AssumeGraphicsSupported();
            var backend = Track(new VfxGraphBackend());
            var before = CountVisualEffects();
            var notAnAsset = NewObject("NotAVfxAsset");

            var id = backend.Create(new VfxSpawnRequest(notAnAsset, Vector3.zero, Quaternion.identity));

            Assert.That(id.IsValid, Is.False);
            Assert.That(CountVisualEffects(), Is.EqualTo(before));
            Assert.That(backend.RootForTest == null, Is.True, "拒否は GameObject を作る前に起きる");
        }

        [UnityTest]
        public IEnumerator Ids_AreNeverReusedAcross1000CreateDestroy()
        {
            AssumeGraphicsSupported();
            var backend = Track(new VfxGraphBackend());
            var ids = new HashSet<ulong>();
            for (var i = 0; i < 1000; i++)
            {
                var id = backend.Create(new VfxSpawnRequest(ShortLived, Vector3.zero, Quaternion.identity));
                Assert.That(ids.Add(id.Value), Is.True, "id の重複: " + id.Value);
                backend.Destroy(id);
            }

            yield return null;
            Assert.That(ids.Count, Is.EqualTo(1000));
        }

        [UnityTest]
        public IEnumerator Destroy_IsSafeForUnknownRepeatedExternallyDestroyedAndLostSource()
        {
            AssumeGraphicsSupported();
            var backend = Track(new VfxGraphBackend());
            Assert.DoesNotThrow(() => backend.Destroy(new VfxInstanceId(987654321)), "未知の id");

            var twice = backend.Create(new VfxSpawnRequest(ShortLived, Vector3.zero, Quaternion.identity));
            backend.Destroy(twice);
            Assert.DoesNotThrow(() => backend.Destroy(twice), "Destroy 済みの id");

            var external = backend.Create(new VfxSpawnRequest(ShortLived, Vector3.zero, Quaternion.identity));
            Object.Destroy(backend.FindForTest(external)!.gameObject);
            yield return null;
            Assert.DoesNotThrow(() => backend.Destroy(external), "外から破棄済みの GameObject");

            var sourceCopy = Object.Instantiate(ShortLived);
            var lostSource = backend.Create(new VfxSpawnRequest(sourceCopy, Vector3.zero, Quaternion.identity));
            Assert.That(lostSource.IsValid, Is.True, "前提: 複製した source から作れた");
            Object.Destroy(sourceCopy);
            yield return null;
            Assert.DoesNotThrow(() => backend.Destroy(lostSource), "source を破棄した後");
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void Destroy_WithLiveGameObject_DisablesAndDetachesSourceSynchronously()
        {
            AssumeGraphicsSupported();
            var backend = Track(new VfxGraphBackend());
            var id = backend.Create(new VfxSpawnRequest(ShortLived, Vector3.zero, Quaternion.identity));
            var effect = backend.FindForTest(id)!;

            backend.Destroy(id);

            Assert.That(effect != null, Is.True, "前提: 破棄はフレーム末なので、まだ存在する");
            Assert.That(effect!.enabled, Is.False);
            Assert.That(effect.visualEffectAsset == null, Is.True, "Destroy から戻った時点で source を参照しない");
        }

        [UnityTest]
        public IEnumerator SetParameters_WriteExposedProperties_AndRejectUnknownMismatchedAndDestroyed()
        {
            AssumeGraphicsSupported();
            var backend = Track(new VfxGraphBackend());
            var id = backend.Create(new VfxSpawnRequest(ShortLived, Vector3.zero, Quaternion.identity));
            var effect = backend.FindForTest(id)!;
            Assert.That(effect.HasFloat("FloatProp") && effect.HasInt("IntProp") && effect.HasBool("BoolProp")
                        && effect.HasVector3("Vector3Prop") && effect.HasVector4("Vector4Prop"), Is.True,
                "前提: fixture に Exposed Property が 5 型ある");

            Assert.That(backend.SetFloat(id, "FloatProp", 2.5f), Is.True);
            Assert.That(backend.SetInt(id, "IntProp", 7), Is.True);
            Assert.That(backend.SetBool(id, "BoolProp", true), Is.True);
            Assert.That(backend.SetVector3(id, "Vector3Prop", new Vector3(1f, 2f, 3f)), Is.True);
            Assert.That(backend.SetVector4(id, "Vector4Prop", new Vector4(1f, 2f, 3f, 4f)), Is.True);
            Assert.That(effect.GetFloat("FloatProp"), Is.EqualTo(2.5f));
            Assert.That(effect.GetInt("IntProp"), Is.EqualTo(7));
            Assert.That(effect.GetBool("BoolProp"), Is.True);
            Assert.That(effect.GetVector3("Vector3Prop"), Is.EqualTo(new Vector3(1f, 2f, 3f)));
            Assert.That(effect.GetVector4("Vector4Prop"), Is.EqualTo(new Vector4(1f, 2f, 3f, 4f)));

            Assert.That(backend.SetFloat(id, "NoSuchProp", 1f), Is.False, "未定義の名前");
            Assert.That(backend.SetFloat(id, "IntProp", 1f), Is.False, "型の合わない名前");
            Assert.That(backend.SetVector4(id, "Vector3Prop", Vector4.one), Is.False, "型の合わない名前");

            Object.Destroy(effect.gameObject);
            yield return null;
            Assert.That(backend.SetFloat(id, "FloatProp", 1f), Is.False, "外から破棄済みの instance");
            LogAssert.NoUnexpectedReceived();
        }

        // ═══════════════════════════════════════════
        //  停止と回収（Framework の host を通して生きた coordinator から Tick される）
        // ═══════════════════════════════════════════

        [UnityTest]
        public IEnumerator ImmediateStop_DestroysGameObjectByNextFrame()
        {
            AssumeGraphicsSupported();
            var backend = new VfxGraphBackend();
            var host = CreateHost(backend);
            var handle = host.System.Spawn(new VfxSpawnRequest(ShortLived, Vector3.zero, Quaternion.identity), AssetOwner.App);
            var go = backend.FindForTest(IdOf(handle))!.gameObject;
            handle.Play();
            yield return null;

            handle.Stop();
            yield return null;

            Assert.That(go == null, Is.True);
        }

        [UnityTest]
        public IEnumerator StopAllowingParticlesToFinish_DestroysAfterParticlesDie()
        {
            AssumeGraphicsSupported();
            var backend = new VfxGraphBackend();
            var host = CreateHost(backend);
            var handle = host.System.Spawn(new VfxSpawnRequest(ShortLived, Vector3.zero, Quaternion.identity), AssetOwner.App);
            var id = IdOf(handle);
            var go = backend.FindForTest(id)!.gameObject;
            handle.Play();

            var sawAlive = false;
            for (var frame = 0; frame < ParticleFrameLimit && !sawAlive; frame++)
            {
                yield return null;
                sawAlive = backend.HasAliveParticles(id);
            }

            Assert.That(sawAlive, Is.True, "前提: 粒子が出ている");
            handle.StopAllowingParticlesToFinish();
            for (var frame = 0; frame < ParticleFrameLimit && go != null; frame++)
            {
                yield return null;
            }

            Assert.That(go == null, Is.True, "粒子が消えた後、上限フレーム内に破棄される");
            Assert.That(handle.IsActive, Is.False);
        }

        /// <summary>粒子が消えない fixture は timeout で破棄される。経過実時間が timeout 以上であること（即 timeout の偽合格を除く）。</summary>
        [UnityTest]
        public IEnumerator StopAllowingParticlesToFinish_ImmortalParticles_DestroyedByTimeout()
        {
            AssumeGraphicsSupported();
            const float timeout = 0.5f;
            var backend = new VfxGraphBackend();
            var host = CreateHost(backend, timeout);
            var handle = host.System.Spawn(new VfxSpawnRequest(Immortal, Vector3.zero, Quaternion.identity), AssetOwner.App);
            var id = IdOf(handle);
            var go = backend.FindForTest(id)!.gameObject;
            handle.Play();

            var sawAlive = false;
            for (var frame = 0; frame < ParticleFrameLimit && !sawAlive; frame++)
            {
                yield return null;
                sawAlive = backend.HasAliveParticles(id);
            }

            Assert.That(sawAlive, Is.True, "前提: 粒子が出ている");
            var stoppedAt = Time.realtimeSinceStartupAsDouble;
            handle.StopAllowingParticlesToFinish();
            for (var frame = 0; frame < ParticleFrameLimit * 4 && go != null; frame++)
            {
                yield return null;
            }

            var elapsed = Time.realtimeSinceStartupAsDouble - stoppedAt;
            Assert.That(go == null, Is.True, "上限フレーム内に timeout で破棄される");
            Assert.That(elapsed, Is.GreaterThanOrEqualTo(timeout * 0.99), "経過実時間が timeout 未満で破棄された");
        }

        [UnityTest]
        public IEnumerator BindOwnerDestroyed_InstanceDestroyedByNextFrame()
        {
            AssumeGraphicsSupported();
            var backend = new VfxGraphBackend();
            var host = CreateHost(backend);
            var owner = NewObject("VfxOwner");
            var handle = host.System.Spawn(new VfxSpawnRequest(ShortLived, Vector3.zero, Quaternion.identity), AssetOwner.Bind(owner));
            var go = backend.FindForTest(IdOf(handle))!.gameObject;
            handle.Play();
            yield return null;

            Object.Destroy(owner);
            yield return null;
            yield return null;

            Assert.That(handle.IsActive, Is.False);
            Assert.That(go == null, Is.True);
            LogAssert.NoUnexpectedReceived();
        }

        // ═══════════════════════════════════════════
        //  配置先
        // ═══════════════════════════════════════════

        [UnityTest]
        public IEnumerator Placement_WorldTransformMatchesRequest_WithAndWithoutParent_AndDisposeCleansUp()
        {
            AssumeGraphicsSupported();
            var backend = Track(new VfxGraphBackend());
            var position = new Vector3(1f, 2f, 3f);
            var rotation = Quaternion.Euler(10f, 20f, 30f);

            var loose = backend.Create(new VfxSpawnRequest(ShortLived, position, rotation));
            var looseGo = backend.FindForTest(loose)!.gameObject;
            var root = backend.RootForTest!;
            Assert.That(looseGo.transform.parent, Is.SameAs(root.transform));
            Assert.That(root.scene.name, Is.EqualTo("DontDestroyOnLoad"));
            AssertWorldTransform(looseGo.transform, position, rotation);

            var parent = NewObject("VfxParent");
            parent.transform.SetPositionAndRotation(new Vector3(-4f, 5f, 6f), Quaternion.Euler(0f, 90f, 45f));
            var child = backend.Create(new VfxSpawnRequest(ShortLived, position, rotation, parent.transform));
            var childGo = backend.FindForTest(child)!.gameObject;
            Assert.That(childGo.transform.parent, Is.SameAs(parent.transform));
            AssertWorldTransform(childGo.transform, position, rotation);

            backend.Dispose();
            yield return null;
            Assert.That(root == null, Is.True, "Dispose で root が破棄される");
            Assert.That(looseGo == null && childGo == null, Is.True, "Dispose で残った instance が破棄される");
        }

        private static void AssertWorldTransform(Transform transform, Vector3 position, Quaternion rotation)
        {
            Assert.That(Vector3.Distance(transform.position, position), Is.LessThan(1e-4f));
            Assert.That(Quaternion.Angle(transform.rotation, rotation), Is.LessThan(0.01f));
        }
    }
}
