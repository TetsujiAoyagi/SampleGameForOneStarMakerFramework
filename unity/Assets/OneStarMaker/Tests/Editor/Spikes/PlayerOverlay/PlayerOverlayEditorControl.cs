#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using I = OneStarMaker.Tests.Editor.Spikes.PlayerOverlay.PlayerOverlayInventory;
using V = OneStarMaker.Tests.Editor.Spikes.PlayerOverlay.PlayerOverlayVerdict;

namespace OneStarMaker.Tests.Editor.Spikes.PlayerOverlay
{
    /// <summary>
    /// Editor の対照（HANDOFF AC6。条件付き）。スパイクの答えが「同じ」でなかった時だけ、判定 C の実行担当が主の順の round ごとに別の
    /// Unity process で実行する。同じ runner を、同じ 2 つの directory に対して Editor の Play で走らせる（Enter Play Mode の設定は project の
    /// 今の値のまま変えず、record に残す）。runner の Scene は保存しない一時の Scene に作り、`SAMPLEGAME_` の環境変数を退避してから Play に入る。
    /// 観測は runner が record に書き、ここは Play の出入りと待ち合わせと後始末だけを持つ。終わりに、Player の round と全ての対照の record から
    /// 補助の判定の file を書き直す。Domain Reload が有効だと test の enumerator が Play の出入りで失われるので、その時は Play に入らずに止まる。
    /// </summary>
    public sealed class PlayerOverlayEditorControl
    {
        private const int LimitSeconds = 180;

        [UnityTest, Explicit, Timeout(1800000)] public IEnumerator Read() => Run(V.Read);
        [UnityTest, Explicit, Timeout(1800000)] public IEnumerator Class() => Run(V.Class);
        [UnityTest, Explicit, Timeout(1800000)] public IEnumerator Lifetime() => Run(V.Lifetime);

        private static IEnumerator Run(string round)
        {
            var run = PovRun.Open("control-" + round);
            var first = JsonUtility.FromJson<FirstFacts>(File.ReadAllText(Path.Combine(run.Directory, I.FirstFactsFile)));
            var digests = I.Digests(run.Directory);
            // 2 つの directory が第 1 process の出力と同じ bytes か（Player の出力は使わないが、照合は同じ形で残す）。
            var inputMatched = first.RunId == run.RunId && digests.Take(2).SequenceEqual(first.Digests.Take(2));
            var domainReloadDisabled = EditorSettings.enterPlayModeOptionsEnabled && (EditorSettings.enterPlayModeOptions & EnterPlayModeOptions.DisableDomainReload) != 0;
            var label = "control-" + round + "-" + DateTime.UtcNow.ToString("HHmmss", CultureInfo.InvariantCulture);
            var record = Path.Combine(run.Directory, "control", label + ".jsonl");
            Directory.CreateDirectory(Path.GetDirectoryName(record)!);
            run.Write("control-start", ("round", round), ("label", label), ("digests", digests), ("inputMatched", inputMatched), ("domainReloadDisabled", domainReloadDisabled),
                ("enterPlayMode", EditorSettings.enterPlayModeOptionsEnabled + "/" + EditorSettings.enterPlayModeOptions));
            Assert.That(inputMatched, Is.True, "the directories are not the first process output");
            Assert.That(domainReloadDisabled, Is.True, "Enter Play Mode must keep Domain Reload disabled (the project value); not changing it");
            for (var i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
                Assert.That(UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty, Is.False, "an open scene has unsaved changes");

            var setup = EditorSceneManager.GetSceneManagerSetup();
            var startScene = EditorSceneManager.playModeStartScene;
            var ignoreBefore = LogAssert.ignoreFailingMessages;
            var saved = new Dictionary<string, string?>();
            var env = PlayerOverlayLaunch.RoundEnvironment(run, round, label, record, LimitSeconds, domainReloadDisabled);
            var ended = false;
            try
            {
                // 製品の設定の読み口が読む環境変数を退避して外し、round の設定を置く（Player の起動と同じ条件。HANDOFF AC6）。
                foreach (DictionaryEntry kv in Environment.GetEnvironmentVariables())
                    if (kv.Key is string key && key.StartsWith("SAMPLEGAME_", StringComparison.OrdinalIgnoreCase)) saved[key] = kv.Value as string;
                foreach (var key in env.Keys) saved[key] = Environment.GetEnvironmentVariable(key);
                foreach (var key in saved.Keys.Where(k => k.StartsWith("SAMPLEGAME_", StringComparison.OrdinalIgnoreCase)).ToList()) Environment.SetEnvironmentVariable(key, null);
                foreach (var kv in env) Environment.SetEnvironmentVariable(kv.Key, kv.Value);
                run.Write("control-environment", ("removed", saved.Keys.Where(k => k.StartsWith("SAMPLEGAME_", StringComparison.OrdinalIgnoreCase))), ("passed", env.Select(x => x.Key + "=" + x.Value)));
                LogAssert.ignoreFailingMessages = true;
                EditorSceneManager.playModeStartScene = null;
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                PlayerOverlayFixture.AddRunnerObject();
                yield return new EnterPlayMode(false);
                var watch = Stopwatch.StartNew();
                var poll = Stopwatch.StartNew();
                while (watch.Elapsed.TotalSeconds < LimitSeconds + PlayerOverlayLaunch.LauncherMarginSeconds)
                {
                    yield return null;
                    if (poll.ElapsedMilliseconds < 1000) continue;
                    poll.Restart();
                    if (PlayerOverlayLaunch.HasEndLine(record)) { ended = true; break; }
                }
                yield return new ExitPlayMode();
            }
            finally
            {
                foreach (var kv in saved) Environment.SetEnvironmentVariable(kv.Key, kv.Value);
                LogAssert.ignoreFailingMessages = ignoreBefore;
                EditorSceneManager.playModeStartScene = startScene;
                if (!EditorApplication.isPlaying)
                {
                    if (setup.Length > 0 && setup.All(x => File.Exists(Path.Combine(PovRun.RepoRoot, "unity", x.path))) && setup.Any(x => x.isActive))
                        EditorSceneManager.RestoreSceneManagerSetup(setup);
                    else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                }
                run.Write("control-end", ("round", round), ("label", label), ("ended", ended), ("stillPlaying", EditorApplication.isPlaying));
            }

            // 補助の判定を、Player の round と、この run の全ての対照の record で書き直す。
            var summaryPath = Path.Combine(run.Directory, PlayerOverlayLaunch.EntriesFile);
            if (File.Exists(summaryPath))
            {
                var summary = JsonUtility.FromJson<LaunchSummary>(File.ReadAllText(summaryPath));
                var controls = Directory.GetFiles(Path.Combine(run.Directory, "control"), "*.jsonl").OrderBy(x => x, StringComparer.Ordinal).Select(path =>
                {
                    var name = Path.GetFileNameWithoutExtension(path);
                    var r = V.MainRounds.First(x => name.StartsWith("control-" + x + "-", StringComparison.Ordinal));
                    return new ProcessInput { Launch = new LaunchEntry { Round = r, Label = name, RecordFile = path }, Record = I.ReadRecord(path) };
                }).ToList();
                PlayerOverlayLaunch.WriteVerdict(run, label, first, summary, controls);
            }
            Assert.That(ended, Is.True, "the runner did not write its end line in the Editor Play");
            Assert.That(EditorApplication.isPlaying, Is.False, "the control did not leave Play");
        }
    }
}
