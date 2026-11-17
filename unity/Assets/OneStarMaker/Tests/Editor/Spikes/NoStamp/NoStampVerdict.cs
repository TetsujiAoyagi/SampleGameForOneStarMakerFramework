#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using S = OneStarMaker.Tests.Editor.Spikes.NoStamp.NstSpec;

namespace OneStarMaker.Tests.Editor.Spikes.NoStamp
{
    /// <summary>
    /// 判定の process（HANDOFF CONTENT_PREFAB_STAMP_REMOVAL §5 の判定必須 3）。build の process の build-facts.json と、その process が写した
    /// build 履歴（`Library/BuildHistory` に残っていれば同じ bytes かも記録する）と出力から、AC1〜AC5b と帰属の確認（ATTR）を
    /// fixture の source の値から計算し直して record に書く。期待値は実装の出力から作らない。
    /// test が失敗するのは harness 自身の例外の時だけ。AC の合否は record の ac の行で、判定 C が読む。
    /// </summary>
    public sealed class NoStampVerdictProcess
    {
        private sealed class Observed
        {
            public readonly Dictionary<string, string> SceneFile = new Dictionary<string, string>(StringComparer.Ordinal);
            public readonly Dictionary<string, string> SceneSha = new Dictionary<string, string>(StringComparer.Ordinal);
            // Scene の file が持つ識別値: 「<prefab>.<版>」（子の位置の float 列）、「Model.<版>」（版だけが持つ object の名前）、「Unsaved」。
            public readonly Dictionary<string, HashSet<string>> Marks = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
            public readonly HashSet<string> ScenesWritten = new HashSet<string>(StringComparer.Ordinal);
            // WriteSerializedFile の event が trace から 1 つ以上見つかったか（見つからなければ数え方が働いていない。H1）。
            public int WrittenEvents;
            public readonly Dictionary<string, string> Outputs = new Dictionary<string, string>(StringComparer.Ordinal);
            public readonly List<string> StampInOutputs = new List<string>();
        }

        [Test, Explicit]
        public void Verdict()
        {
            var run = NstRun.Open("verdict");
            var facts = ReadFacts(File.ReadAllText(run.FactsPath));
            run.Write("build-facts", ("finished", facts.finished), ("problems", facts.problems), ("steps", facts.steps.Select(x => x.step)),
                ("buildUnity", facts.unity));
            var observed = new Dictionary<string, Observed>(StringComparer.Ordinal);
            foreach (var step in facts.steps.Where(x => x.success))
            {
                var o = Observe(run, step);
                observed[step.step] = o;
                run.Write("step-observed", ("step", step.step), ("mode", step.mode),
                    ("sceneFiles", o.SceneFile.Select(x => x.Key + "=" + x.Value + " " + o.SceneSha[x.Key])),
                    ("marks", o.Marks.Select(x => x.Key + "=" + string.Join(";", x.Value.OrderBy(t => t, StringComparer.Ordinal)))),
                    ("scenesWritten", o.ScenesWritten.OrderBy(x => x, StringComparer.Ordinal)), ("writtenEvents", o.WrittenEvents),
                    ("stampInOutputs", o.StampInOutputs));
            }
            var complete = facts.finished && S.Steps.All(x => observed.ContainsKey(x.Name));
            ModelProbe(run, observed);
            Ac1(run, observed);
            Ac2(run, facts, observed);
            Ac3(run, observed);
            Ac4(run, observed);
            Ac5(run, facts, observed);
            Ac5b(run, facts, observed);
            Attribution(run, facts, observed);
            run.Write("process-end", ("finished", true), ("buildProcessFinished", facts.finished), ("allStepsObserved", complete));
            Assert.That(File.Exists(run.RecordPath), Is.True, "record was not written");
        }

        private static NstBuildFacts ReadFacts(string text) => UnityEngine.JsonUtility.FromJson<NstBuildFacts>(text);

        private static Observed Observe(NstRun run, NstStepFacts step)
        {
            var o = new Observed();
            var layoutPath = Path.Combine(step.historyCopy, "ContentLayout.json");
            var tepPath = Path.Combine(step.historyCopy, "BuildContentTEP.json");
            var library = string.IsNullOrEmpty(step.historyName) ? null : Path.Combine(NstHistory.Root, step.historyName);
            run.Write("history", ("step", step.step), ("copy", step.historyCopy), ("library", library),
                ("libraryMatchesCopy", library != null && NstHistory.Files.All(f =>
                    File.Exists(Path.Combine(library, f)) && File.Exists(Path.Combine(step.historyCopy, f)) &&
                    File.ReadAllBytes(Path.Combine(library, f)).SequenceEqual(File.ReadAllBytes(Path.Combine(step.historyCopy, f))))));
            if (!File.Exists(layoutPath) || !File.Exists(tepPath)) return o;
            var layout = UnityEngine.JsonUtility.FromJson<NstLayout>(File.ReadAllText(layoutPath));
            var written = NstHistory.WrittenFileIds(File.ReadAllText(tepPath));
            o.WrittenEvents = written.Count;
            foreach (var file in Directory.GetFiles(step.contentPath, "*", SearchOption.AllDirectories))
            {
                var bytes = File.ReadAllBytes(file);
                var name = Path.GetRelativePath(step.contentPath, file).Replace('\\', '/');
                o.Outputs[name] = S.Sha256(bytes);
                if (IndexOf(bytes, Encoding.ASCII.GetBytes(S.RemovedStampPrefix)) >= 0) o.StampInOutputs.Add(name);
            }
            foreach (var name in S.Scenes)
            {
                var id = layout.LoadableSceneIds.FirstOrDefault(x => x.Path == S.Scene(name));
                if (id == null) continue;
                var serialized = layout.SerializedFiles.FirstOrDefault(x => x.Index == id.SerializedFile);
                if (serialized == null) continue;
                if (written.Contains(serialized.ID)) o.ScenesWritten.Add(name);
                var cf = Path.Combine(step.contentPath, serialized.ContentHash + ".cf");
                o.SceneFile[name] = serialized.ContentHash + ".cf";
                var bytes = File.Exists(cf) ? File.ReadAllBytes(cf) : Array.Empty<byte>();
                o.SceneSha[name] = File.Exists(cf) ? S.Sha256(bytes) : "missing";
                var marks = new HashSet<string>(S.Prefabs.SelectMany(p => new[] { S.V1, S.V2 }.Select(v => (p, v)))
                    .Where(x => IndexOf(bytes, S.FloatBytes(S.Position(x.p, x.v))) >= 0).Select(x => x.p + "." + x.v), StringComparer.Ordinal);
                foreach (var v in new[] { S.V1, S.V2 })
                    if (IndexOf(bytes, Encoding.ASCII.GetBytes(S.ModelName(v))) >= 0) marks.Add(S.Model + "." + v);
                if (IndexOf(bytes, Encoding.ASCII.GetBytes(S.UnsavedName)) >= 0) marks.Add("Unsaved");
                o.Marks[name] = marks;
            }
            return o;
        }

        // ある回の出力の識別値の不一致。その Scene が焼き込む prefab・model だけ、その回の版を全て持ち、他の版を持たない。
        // SPlain はどの識別値も持たない。B3o の SShared1 は、変えた名前（未保存の変更）を持たない。
        private static List<string> IdentityFailures(S.Step step, Observed o)
        {
            var failures = new List<string>();
            foreach (var scene in o.Marks.Keys)
            {
                var marks = o.Marks[scene];
                foreach (var prefab in S.BakedPrefabs(scene))
                {
                    var current = step.Versions[prefab];
                    var other = current == S.V1 ? S.V2 : S.V1;
                    if (!marks.Contains(prefab + "." + current)) failures.Add(step.Name + " " + scene + " lacks " + prefab + "." + current);
                    if (marks.Contains(prefab + "." + other)) failures.Add(step.Name + " " + scene + " has " + prefab + "." + other);
                }
                if (S.BakesModel(scene))
                {
                    var current = step.Versions[S.Model];
                    var other = current == S.V1 ? S.V2 : S.V1;
                    if (!marks.Contains(S.Model + "." + current)) failures.Add(step.Name + " " + scene + " lacks Model." + current);
                    if (marks.Contains(S.Model + "." + other)) failures.Add(step.Name + " " + scene + " has Model." + other);
                }
                if (scene == S.SPlain && marks.Count != 0) failures.Add(step.Name + " SPlain has " + string.Join(";", marks));
                if (marks.Contains("Unsaved")) failures.Add(step.Name + " " + scene + " has the unsaved name");
            }
            if (!step.Overlay && o.Marks.Count != S.Scenes.Length) failures.Add(step.Name + " has " + o.Marks.Count + " scenes");
            return failures;
        }

        // model の識別値（版だけが持つ group の名前）が Scene の .cf に出るかの観測経路の確認（HANDOFF §5 の未知の経路、§6 の B 適応）。
        // どの回の SModel にもどちらの版の名前も無ければ、AC1 の SModel の不成立は「古い出力」ではなく観測経路の不成立で、
        // B 適応で別の model の編集を選ぶ（ATTR (1) の B5' の SModel でも同じ形で崩れる）。判定 C がこの行を先に読めるように独立に書く。
        private static void ModelProbe(NstRun run, Dictionary<string, Observed> observed)
        {
            var perStep = S.Steps.Where(x => observed.ContainsKey(x.Name)).Select(x =>
            {
                var marks = observed[x.Name].Marks.GetValueOrDefault(S.SModel);
                var found = marks == null ? "no-SModel" : string.Join(";", marks.Where(m => m.StartsWith(S.Model + ".", StringComparison.Ordinal))
                    .OrderBy(m => m, StringComparer.Ordinal));
                return x.Name + "=" + (found.Length == 0 ? "none" : found);
            }).ToList();
            var observable = observed.Values.Any(o => o.Marks.TryGetValue(S.SModel, out var m) && m.Any(x => x.StartsWith(S.Model + ".", StringComparison.Ordinal)));
            run.Write("model-identifier", ("names", new[] { S.ModelName(S.V1), S.ModelName(S.V2) }), ("observable", observable), ("perStep", perStep));
        }

        // AC1: 各回（B3o を含む）の出力に含まれる各 Scene の .cf が、その回の版の識別値を全て持ち、他の版の識別値を持たない。
        private static void Ac1(NstRun run, Dictionary<string, Observed> observed)
        {
            var failures = new List<string>();
            foreach (var step in S.Steps)
            {
                if (!observed.TryGetValue(step.Name, out var o)) { failures.Add(step.Name + " not observed"); continue; }
                failures.AddRange(IdentityFailures(step, o));
            }
            Ac(run, "AC1", failures);
        }

        // AC2: 上書き（B6）の SOuter が (Outer v1, Inner v2) の列を持ち、Outer v2 を持たず、file が B2 の SOuter と同じ sha256。逆引きの root は SOuter だけ。
        private static void Ac2(NstRun run, NstBuildFacts facts, Dictionary<string, Observed> observed)
        {
            var failures = new List<string>();
            var b6 = facts.steps.FirstOrDefault(x => x.step == "B6");
            if (b6 == null || !observed.TryGetValue("B6", out var o) || !observed.TryGetValue("B2", out var b2)) { Ac(run, "AC2", new List<string> { "B2 or B6 not observed" }); return; }
            if (!b6.roots.SequenceEqual(new[] { S.SOuter })) failures.Add("B6 roots " + string.Join(";", b6.roots));
            if (!o.Marks.TryGetValue(S.SOuter, out var marks)) failures.Add("B6 has no SOuter");
            else
            {
                if (!marks.Contains("Outer.v1") || !marks.Contains("Inner.v2")) failures.Add("B6 SOuter lacks Outer.v1/Inner.v2");
                if (marks.Contains("Outer.v2")) failures.Add("B6 SOuter has Outer.v2");
                if (o.SceneSha[S.SOuter] != b2.SceneSha.GetValueOrDefault(S.SOuter)) failures.Add("B6 SOuter sha differs from B2");
            }
            Ac(run, "AC2", failures);
        }

        // AC3: Incremental の回だけを数える。B3 は Scene を 1 つも書かず、B2 は SOuter だけ、B4b は SShared1・SShared2・SVariant・SModel だけを書く。
        // B2 と B4b は数え方の対照を兼ねる（event が 1 つも見つからない時は harness の欠陥で、B3 の 0 を合格に数えない。H1）。
        // B4 の各 Scene の sha256 は B1 と一致する。B3o の書き出しは数えない。
        private static void Ac3(NstRun run, Dictionary<string, Observed> observed)
        {
            var failures = new List<string>();
            if (!observed.TryGetValue("B1", out var b1) || !observed.TryGetValue("B2", out var b2) || !observed.TryGetValue("B3", out var b3) ||
                !observed.TryGetValue("B4", out var b4) || !observed.TryGetValue("B4b", out var b4b))
            { Ac(run, "AC3", new List<string> { "B1..B4b not observed" }); return; }
            if (b2.WrittenEvents == 0 || b4b.WrittenEvents == 0)
                failures.Add("H1 harness defect: no WriteSerializedFile event in B2 (" + b2.WrittenEvents + ") or B4b (" + b4b.WrittenEvents + ")");
            if (b3.ScenesWritten.Count != 0) failures.Add("B3 wrote " + string.Join(";", b3.ScenesWritten));
            if (!b2.ScenesWritten.SetEquals(new[] { S.SOuter })) failures.Add("B2 wrote " + string.Join(";", b2.ScenesWritten.OrderBy(x => x, StringComparer.Ordinal)));
            if (!b4b.ScenesWritten.SetEquals(new[] { S.SShared1, S.SShared2, S.SVariant, S.SModel }))
                failures.Add("B4b wrote " + string.Join(";", b4b.ScenesWritten.OrderBy(x => x, StringComparer.Ordinal)));
            foreach (var scene in S.Scenes)
                if (b4.SceneSha.GetValueOrDefault(scene) != b1.SceneSha.GetValueOrDefault(scene)) failures.Add("B4 " + scene + " differs from B1");
            Ac(run, "AC3", failures, ("b4Written", string.Join(";", b4.ScenesWritten.OrderBy(x => x, StringComparer.Ordinal))));
        }

        // AC4: B5 と B5'（CleanBuildCache の後）の各 Scene の file が同じ sha256。Scene 以外の差は記録だけ。
        private static void Ac4(NstRun run, Dictionary<string, Observed> observed)
        {
            var failures = new List<string>();
            if (!observed.TryGetValue("B5", out var b5) || !observed.TryGetValue("B5p", out var b5p)) failures.Add("B5 or B5p not observed");
            else
            {
                foreach (var scene in S.Scenes)
                    if (b5.SceneSha.GetValueOrDefault(scene) != b5p.SceneSha.GetValueOrDefault(scene)) failures.Add(scene + " differs between B5 and B5p");
                var sceneFiles = new HashSet<string>(b5.SceneFile.Values.Concat(b5p.SceneFile.Values), StringComparer.Ordinal);
                var others = b5.Outputs.Keys.Union(b5p.Outputs.Keys).Where(x => !sceneFiles.Contains(x))
                    .Where(x => b5.Outputs.GetValueOrDefault(x) != b5p.Outputs.GetValueOrDefault(x)).OrderBy(x => x, StringComparer.Ordinal);
                run.Write("ac4-non-scene-differences", ("files", others));
            }
            Ac(run, "AC4", failures);
        }

        // AC5: 各 build の直後に、各 Scene の file と .meta の sha256 と更新時刻が build の前と一致し、Assets/ と ProjectSettings/ の全 file が前と一致し、
        // 生成 folder が残らず、`Library/OneStarMaker/ScenePrefabStamp/` が run の始めと同じ状態で、どの出力にも印の接頭辞が無い。
        private static void Ac5(NstRun run, NstBuildFacts facts, Dictionary<string, Observed> observed)
        {
            var failures = new List<string>();
            foreach (var step in facts.steps)
            {
                foreach (var before in step.before)
                {
                    var after = step.after.FirstOrDefault(x => x.scene == before.scene);
                    if (after == null) { failures.Add(step.step + " " + before.scene + " missing after"); continue; }
                    if (after.metaSha256 != before.metaSha256 || after.metaTicks != before.metaTicks) failures.Add(step.step + " " + before.scene + " .meta changed");
                    if (after.fileSha256 != before.fileSha256 || after.fileTicks != before.fileTicks) failures.Add(step.step + " " + before.scene + " file changed");
                }
                if (step.inventoryDiff.Count != 0) failures.Add(step.step + " inventory: " + string.Join(";", step.inventoryDiff));
                if (step.generatedFolderAfter) failures.Add(step.step + " generated folder remains");
                if (step.stampDirectoryAfter != facts.stampDirectoryStart) failures.Add(step.step + " stamp directory " + step.stampDirectoryAfter);
                if (observed.TryGetValue(step.step, out var o) && o.StampInOutputs.Count != 0)
                    failures.Add(step.step + " outputs with the stamp prefix: " + string.Join(";", o.StampInOutputs));
            }
            if (facts.steps.Count != S.Steps.Length) failures.Add("steps " + facts.steps.Count);
            Ac(run, "AC5", failures);
        }

        // AC5b: B3o は成功し、SShared1 の file と .meta の sha256 が build の前と同じで、build の後も開かれたまま isDirty で変更が残り、
        // 出力の SShared1 が変えた名前を持たない（disk の版を焼き込む）。書き出しの数は見ない。
        private static void Ac5b(NstRun run, NstBuildFacts facts, Dictionary<string, Observed> observed)
        {
            var failures = new List<string>();
            var b3o = facts.steps.FirstOrDefault(x => x.step == "B3o");
            if (b3o == null || !b3o.success) { Ac(run, "AC5b", new List<string> { "B3o did not succeed" }); return; }
            var before = b3o.before.First(x => x.scene == S.SShared1);
            var after = b3o.after.First(x => x.scene == S.SShared1);
            if (!b3o.unsavedOpened) failures.Add("the scene was not opened with unsaved changes");
            if (before.fileSha256 != after.fileSha256) failures.Add("SShared1 file changed");
            if (before.metaSha256 != after.metaSha256) failures.Add("SShared1 .meta changed");
            if (!b3o.unsavedLoadedAfter) failures.Add("SShared1 was closed by the build");
            if (!b3o.unsavedDirtyAfter) failures.Add("SShared1 is no longer dirty");
            if (b3o.unsavedRootNameAfter != S.UnsavedName) failures.Add("the unsaved change is gone: " + b3o.unsavedRootNameAfter);
            if (!observed.TryGetValue("B3o", out var o) || !o.Marks.TryGetValue(S.SShared1, out var marks)) failures.Add("B3o SShared1 not observed");
            else if (marks.Contains("Unsaved")) failures.Add("the output SShared1 has the unsaved name");
            Ac(run, "AC5b", failures);
        }

        // 帰属の確認（HANDOFF §2.5）。ATTR が成り立たない時の AC1〜AC4 の不成立は、答えではなく harness か adapter の欠陥として読む。
        // (1) B5' の全 Scene の識別値が期待どおり。(2) 各回で、harness が書いた asset の file の sha256 が書いたバイトと一致し、dependency hash が直前の回と違い、
        // 書かなかった asset の sha256 が直前の回と同じ。入れ子の外側と variant の dependency hash は file を変えなくても動くので、書かなかった asset の hash は見ない。
        private static void Attribution(NstRun run, NstBuildFacts facts, Dictionary<string, Observed> observed)
        {
            var failures = new List<string>();
            if (!observed.TryGetValue("B5p", out var b5p)) failures.Add("B5p not observed");
            else failures.AddRange(IdentityFailures(S.Steps.First(x => x.Name == "B5p"), b5p).Select(x => "B5p identity: " + x));
            for (var i = 0; i < facts.steps.Count; i++)
            {
                var step = facts.steps[i];
                var spec = S.Steps.First(x => x.Name == step.step);
                var previous = i == 0 ? null : facts.steps[i - 1];
                var previousSpec = previous == null ? null : S.Steps.First(x => x.Name == previous.step);
                var expectedChanged = new HashSet<string>(
                    previousSpec == null ? Array.Empty<string>() : S.Prefabs.Append(S.Model)
                        .Where(k => spec.Versions[k] != previousSpec.Versions[k]).Select(k => k == S.Model ? S.ModelPath : S.Prefab(k)),
                    StringComparer.Ordinal);
                if (!expectedChanged.SetEquals(step.changedAssets)) failures.Add(step.step + " changed " + string.Join(";", step.changedAssets) + " expected " + string.Join(";", expectedChanged));
                foreach (var asset in step.assets)
                {
                    var prior = previous?.assets.FirstOrDefault(x => x.path == asset.path);
                    if (step.changedAssets.Contains(asset.path))
                    {
                        var written = step.writtenAssets.FirstOrDefault(x => x.path == asset.path);
                        if (written == null || written.sha256 != asset.sha256) failures.Add(step.step + " " + asset.path + " bytes differ from what was written");
                        if (prior != null && prior.dependencyHash == asset.dependencyHash) failures.Add(step.step + " " + asset.path + " dependency hash did not change");
                    }
                    else if (prior != null && prior.sha256 != asset.sha256) failures.Add(step.step + " " + asset.path + " changed without being written");
                }
            }
            Ac(run, "ATTR", failures);
        }

        private static void Ac(NstRun run, string ac, List<string> failures, params (string key, object? value)[] extra) =>
            run.Write("ac", new (string key, object? value)[] { ("ac", ac), ("pass", failures.Count == 0), ("failures", failures) }
                .Concat(extra).ToArray());

        private static int IndexOf(byte[] haystack, byte[] needle)
        {
            if (needle.Length == 0 || haystack.Length < needle.Length) return -1;
            for (var i = 0; i <= haystack.Length - needle.Length; i++)
            {
                var j = 0;
                while (j < needle.Length && haystack[i + j] == needle[j]) j++;
                if (j == needle.Length) return i;
            }
            return -1;
        }
    }
}
