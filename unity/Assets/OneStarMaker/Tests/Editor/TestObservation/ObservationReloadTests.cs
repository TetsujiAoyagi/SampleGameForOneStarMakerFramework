#nullable enable
using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using NUnit.Framework;
using OneStarMaker.Editor.TestObservation;
using OneStarMaker.Runtime;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace OneStarMaker.Tests.Editor.TestObservation
{
    public sealed class ObservationReloadTests
    {
        [UnityTest]
        public IEnumerator RealDomainReloadRoundTrip()
        {
            if (TestObservationBootstrap.IsActive) TestObservationBootstrap.PrepareReloadSettings();
            try
            {
                GameContentMode.Apply();
                yield return new EnterPlayMode();
                yield return new ExitPlayMode();
            }
            finally
            {
                // fixture が再開しなくても quitting が private recovery record から戻す。
                if (TestObservationBootstrap.IsActive) TestObservationBootstrap.RestoreReloadSettings();
                GameContentMode.Restore();
            }
            Assert.Pass();
        }

        // finally が domain reload をまたいで走らなかった場合の二重の復元。記録が無ければ何もしない。
        [TearDown]
        public void TearDown() => GameContentMode.Restore();

        // Play に入ると、project の Game の initializer が bootstrap を起動する。Editor Play は
        // content:runtimeMode が無いと fail-closed で止まるので、このテストは Game の config に
        // content mode を与える。Game の名前（環境変数の prefix）は Framework のテストに書かず、
        // Game が initializer で宣言した prefix を型から発見する。
        // 設定は Process スコープの環境変数だけで、テストの終了時に元の値（未設定を含む）へ戻す。
        // 戻せなかった場合も、値は Unity の process の終了で消える。
        private static class GameContentMode
        {
            private const string Value = "addressables";
            // Process の環境変数は domain reload を越えて残るが、static field は消える。
            // 復元に要る元の値は SessionState に置き、reload 後の finally / TearDown からも戻せるようにする。
            private const string KeyState = "OneStarMaker.Tests.ObservationReload.ContentModeKey";
            private const string HadValueState = "OneStarMaker.Tests.ObservationReload.ContentModeHadValue";
            private const string PreviousState = "OneStarMaker.Tests.ObservationReload.ContentModePrevious";

            internal static void Apply()
            {
                Restore();
                var prefix = FindGamePrefix();
                if (prefix == null) return;
                // EnvironmentVariableConfigProvider は prefix を除き、"__" を ":" に読み替える。
                var key = prefix + "CONTENT__RUNTIMEMODE";
                var previous = Environment.GetEnvironmentVariable(key, EnvironmentVariableTarget.Process);
                SessionState.SetString(KeyState, key);
                SessionState.SetBool(HadValueState, previous != null);
                SessionState.SetString(PreviousState, previous ?? string.Empty);
                Environment.SetEnvironmentVariable(key, Value, EnvironmentVariableTarget.Process);
            }

            internal static void Restore()
            {
                var key = SessionState.GetString(KeyState, string.Empty);
                if (key.Length == 0) return;
                var previous = SessionState.GetBool(HadValueState, false)
                    ? SessionState.GetString(PreviousState, string.Empty)
                    : null;
                Environment.SetEnvironmentVariable(key, previous, EnvironmentVariableTarget.Process);
                SessionState.EraseString(KeyState);
                SessionState.EraseBool(HadValueState);
                SessionState.EraseString(PreviousState);
            }

            private static string? FindGamePrefix()
            {
                // 数えるのは、[RuntimeInitializeOnLoadMethod] の static method を自身で宣言する派生型だけ。
                // Play で bootstrap を起動するのはその型で、テストの中の private な派生型は起動しない
                // （HANDOFF の決定記録 DR-1）。0 個なら bootstrap が動かないので何も設定しない。
                // 2 個以上なら、どの Game の config を与えるべきか決められないので失敗にする。
                var initializers = TypeCache.GetTypesDerivedFrom<AbstractApplicationInitializer>()
                    .Where(type => !type.IsAbstract && DeclaresRuntimeInitialize(type))
                    .ToArray();
                if (initializers.Length == 0) return null;
                if (initializers.Length > 1)
                {
                    Assert.Fail("bootstrap を起動する initializer が複数ある: "
                        + string.Join(", ", initializers.Select(type => type.FullName)));
                }

                // prefix は Game が protected の GetEnvironmentVariablePrefix で宣言する。constructor の
                // 副作用を避けるため、初期化しない instance で override を呼ぶ。override は定数を返す想定。
                var type = initializers[0];
                var method = type.GetMethod("GetEnvironmentVariablePrefix",
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public, null, Type.EmptyTypes, null);
                Assert.That(method, Is.Not.Null, $"{type.FullName} に GetEnvironmentVariablePrefix が無い");
                var instance = FormatterServices.GetUninitializedObject(type);
                var prefix = method!.Invoke(instance, null) as string;
                Assert.That(prefix, Is.Not.Null, $"{type.FullName} の環境変数の prefix が null");
                return prefix;
            }

            private static bool DeclaresRuntimeInitialize(Type type)
                => type.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                    .Any(method => method.IsDefined(typeof(RuntimeInitializeOnLoadMethodAttribute), false));
        }
    }

    public sealed class ObservationFaultFixture
    {
        [Test]
        public void IntentionalMissingCallback()
        {
            // observer の明示 filter によって started だけを落とす。テスト XML は Passed のままになる。
            Assert.Pass();
        }
    }
}
