#nullable enable

using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using OneStarMaker.Runtime;
using SampleGame.InGame.Player;
using SampleGame.InGame.Streaming;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace SampleGame.Tests.Streaming
{
    /// <summary>
    /// 補間つきの Rigidbody を持つ飛行 rig を <see cref="FlyController.Teleport"/> で動かした後、位置が保たれること。
    /// <para>
    /// 製品では Teleport の直後に距離 streaming の Focus が登録され、最初の Tick は次のフレームの Update で
    /// <see cref="IFlightReadModel.Position"/> を読む。Teleport が transform だけを書くと、補間が次のフレームで
    /// transform を Rigidbody の古い位置（原点）へ戻し、原点の側の Cell が載る。終点だけを見ると戻りを見落とすので、
    /// 窓の間は毎フレームの Update の後に transform と Rigidbody の両方を確かめる。
    /// </para>
    /// <para>
    /// 起動と後始末は <c>VfxPlayTestFixture</c> と同じ原則（共通化はしない）: Play に入ると AppInitializer が必ず起動するので、
    /// 空の Scene・addressables の content mode・DebugSocket 無効で起動し、起動の完了をフレーム数の上限つきで待つ。
    /// 入力は OFF のまま確かめる（位置の保持は入力に依らず、このアセンブリは Input System を参照しないので
    /// 外部のキーボード入力が無いことを検査できない）。回転は確かめない。
    /// </para>
    /// </summary>
    public sealed class FlyControllerTeleportTests
    {
        private const string ContentModeKey = "SAMPLEGAME_CONTENT__RUNTIMEMODE";
        private const string DebugSocketEnabledKey = "SAMPLEGAME_DEBUGSOCKET__ENABLED";
        private const int StartupFrameLimit = 3000;

        // Teleport の後に位置を見続けるフレーム数。A1 では Teleport の次のフレームで原点へ戻った。
        private const int ObservationFrames = 60;
        private const float Tolerance = 1e-3f;

        private SceneSetup[]? _sceneSetup;
        private string? _previousContentMode;
        private bool _contentModeChanged;
        private string? _previousDebugSocketEnabled;
        private bool _debugSocketEnabledChanged;
        private bool _cursorSaved;
        private CursorLockMode _previousCursorLockState;
        private bool _previousCursorVisible;
        private GameObject? _rig;

        [UnitySetUp]
        public IEnumerator EnterPlayModeWithApp()
        {
            _sceneSetup = EditorSceneManager.GetSceneManagerSetup();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // 同じ全件回帰で他のテストが SAMPLEGAME_CONTENT__* を書くので、directory mode の残りで起動させない。
            _previousContentMode = Environment.GetEnvironmentVariable(ContentModeKey, EnvironmentVariableTarget.Process);
            Environment.SetEnvironmentVariable(ContentModeKey, "addressables", EnvironmentVariableTarget.Process);
            _contentModeChanged = true;

            // DebugSocket の接続失敗の warning を Play の間に出させない（このテストは使わない）。
            _previousDebugSocketEnabled = Environment.GetEnvironmentVariable(DebugSocketEnabledKey, EnvironmentVariableTarget.Process);
            Environment.SetEnvironmentVariable(DebugSocketEnabledKey, "false", EnvironmentVariableTarget.Process);
            _debugSocketEnabledChanged = true;

            // FlyController は既定で入力 ON のため OnEnable で Cursor をロックする。TearDown で元に戻す。
            _previousCursorLockState = Cursor.lockState;
            _previousCursorVisible = Cursor.visible;
            _cursorSaved = true;

            yield return new EnterPlayMode();

            // 起動の失敗を Teleport の不具合と取り違えないよう、先に起動の完了を確かめる。
            var frameworkHost = AbstractApplicationInitializer.ActiveVfxRuntimeHost;
            Assert.That(frameworkHost, Is.Not.Null, "起動の失敗: Framework の composition が作られていない");
            for (var frame = 0; frame < StartupFrameLimit && !frameworkHost!.IsSceneLifecycleBound; frame++)
            {
                yield return null;
            }

            Assert.That(frameworkHost!.IsSceneLifecycleBound, Is.True,
                "起動の失敗（AfterSceneLoad が SceneDirector を作っていない）、または Scene lifecycle への接続の失敗");
        }

        [UnityTearDown]
        public IEnumerator ExitPlayModeAndRestore()
        {
            var stillPlaying = false;
            try
            {
                if (_rig != null)
                {
                    Object.Destroy(_rig);
                }

                _rig = null;
                if (_cursorSaved)
                {
                    Cursor.lockState = _previousCursorLockState;
                    Cursor.visible = _previousCursorVisible;
                    _cursorSaved = false;
                }

                // SetUp が Play に入る前に失敗したときに ExitPlayMode の例外で元の失敗を上書きしない。
                if (EditorApplication.isPlaying)
                {
                    yield return null;
                    yield return new ExitPlayMode();
                    stillPlaying = EditorApplication.isPlaying;
                }

                if (_sceneSetup != null)
                {
                    if (_sceneSetup.Any(scene => scene.isLoaded && scene.isActive))
                        EditorSceneManager.RestoreSceneManagerSetup(_sceneSetup);
                    else
                        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                    _sceneSetup = null;
                }
            }
            finally
            {
                if (_contentModeChanged)
                {
                    Environment.SetEnvironmentVariable(ContentModeKey, _previousContentMode, EnvironmentVariableTarget.Process);
                    _contentModeChanged = false;
                }

                if (_debugSocketEnabledChanged)
                {
                    Environment.SetEnvironmentVariable(DebugSocketEnabledKey, _previousDebugSocketEnabled, EnvironmentVariableTarget.Process);
                    _debugSocketEnabledChanged = false;
                }
            }

            Assert.That(stillPlaying, Is.False, "後始末の失敗: ExitPlayMode の後も Play mode のまま");
        }

        [UnityTest]
        public IEnumerator Teleport_InterpolatedBody_KeepsTargetPositionEveryFrame()
        {
            // Play に入る前に作った GameObject は Play 入りで破棄されるので、Play に入った後に作る。
            // RequireComponent が Rigidbody を付け、Awake が Interpolate を設定する。
            _rig = new GameObject("FlyControllerTeleportTestRig");
            var fly = _rig.AddComponent<FlyController>();
            var body = _rig.GetComponent<Rigidbody>();
            Assert.That(body.interpolation, Is.EqualTo(RigidbodyInterpolation.Interpolate), "前提: 補間つきの Rigidbody");
            fly.InputEnabled = false;

            var target = WorldCellCatalog.SpawnPosition();
            Assert.That(Vector3.Distance(_rig.transform.position, target), Is.GreaterThan(1f), "前提: 開始位置が目標から離れている");

            // fixed step の明けに呼ぶ（位相を作り分ける仕組みは置かない）。
            yield return new WaitForFixedUpdate();
            fly.Teleport(target, Vector3.forward);

            // (i) 同じフレーム: Focus が読む値。
            var immediate = ((IFlightReadModel)fly).Position;
            Assert.That(Vector3.Distance(immediate, target), Is.LessThanOrEqualTo(Tolerance),
                $"Teleport の直後の Position {Format(immediate)} が目標 {Format(target)} から外れている");

            // (ii) 次のフレームから窓の終わりまで、毎フレームの Update の後に両方を確かめる。
            for (var frame = 1; frame <= ObservationFrames; frame++)
            {
                yield return null;
                var transformPosition = _rig.transform.position;
                var bodyPosition = body.position;
                if (Vector3.Distance(transformPosition, target) > Tolerance || Vector3.Distance(bodyPosition, target) > Tolerance)
                {
                    Assert.Fail(
                        $"Teleport の {frame} フレーム後（frameCount={Time.frameCount}）に位置が目標 {Format(target)} から外れた: " +
                        $"transform={Format(transformPosition)} rigidbody={Format(bodyPosition)}");
                }
            }
        }

        private static string Format(Vector3 v) => $"({v.x:F3}, {v.y:F3}, {v.z:F3})";
    }
}
