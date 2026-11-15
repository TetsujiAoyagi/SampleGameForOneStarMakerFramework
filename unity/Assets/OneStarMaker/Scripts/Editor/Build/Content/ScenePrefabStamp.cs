#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.ExceptionServices;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OneStarMaker.Editor.Build.Content
{
    // 1 回の Content Directory build の間だけ、Scene の .meta の userData に焼き込む prefab の版の印を書き、build の後に戻す I/O。
    // 寿命は 1 回の build（adapter が Prepare → Apply → Dispose の順に呼ぶ）。記録は Library の下に置き、異常終了の後は次の build が戻す。
    //
    // 利用者の既存の file（Scene の .meta）を書き換えて戻す唯一の経路なので、次を守る（HANDOFF CONTENT_PREFAB_STAMP §2.7）。
    // - 書く前に、今回書く全ての .meta を検査し、止める理由が 1 つでもあれば何も書かずに止める（Prepare）。
    // - Scene 本体は書かず、Scene を Unity の API で開かない。.meta の userData の行の値だけを置き換える。
    // - 書く前に元のバイトを記録し、Library の下の一時 file に書いてから置き換える（途中で落ちても半端なバイトを残さない）。
    // - 印を読ませるための Refresh を呼ばない（adapter の製品の Refresh が読む）。戻した Scene だけを ImportAsset する。
    internal sealed class ScenePrefabStamp : IDisposable
    {
        // 記録 1 件。file 名は Scene の GUID から作り、Scene の path の長さに依らせない。path は診断用で、戻す時は GUID から今の path を引く。
        [Serializable]
        private sealed class Record
        {
            public string guid = "";
            public string path = "";
            public string originalSha256 = "";
            public string writtenSha256 = "";
            public string original = ""; // 元の .meta のバイト（Base64）
        }

        private sealed class Target
        {
            public Target(string guid, string path, string hash) { Guid = guid; Path = path; Hash = hash; }
            public string Guid { get; }
            public string Path { get; }
            public string Hash { get; }
            public Record? Written { get; set; }
        }

        // 次の build の始めに戻す対象。Record が null は、記録が無いのに userData が印の形を持つ root の Scene（Library が消された後）。
        private sealed class Leftover
        {
            public Leftover(string guid, string? path, Record? record) { Guid = guid; Path = path; Record = record; }
            public string Guid { get; }
            public string? Path { get; }
            public Record? Record { get; }
        }

        private const string RecordExtension = ".json";
        private readonly Func<string, StampProbe> _probe;
        private readonly string _recordDirectory;
        private readonly Dictionary<string, StampProbe> _probed = new Dictionary<string, StampProbe>(StringComparer.Ordinal);
        private readonly List<Target> _targets = new List<Target>();
        private readonly List<Leftover> _leftovers = new List<Leftover>();
        private readonly List<string> _unreadableRecords = new List<string>();
        private bool _prepared;
        private bool _applied;
        private bool _disposed;

        // probe と recordDirectory は test の継ぎ目。製品は AssetDatabase の probe と Library の下の既定の場所を使う。
        internal ScenePrefabStamp(Func<string, StampProbe>? probe = null, string? recordDirectory = null)
        {
            _probe = probe ?? UnityProbe;
            _recordDirectory = recordDirectory ?? DefaultRecordDirectory;
        }

        internal static string DefaultRecordDirectory =>
            Path.Combine(ProjectRoot, "Library", "OneStarMaker", "ScenePrefabStamp");

        private static string ProjectRoot => Path.GetDirectoryName(Application.dataPath)!;

        // 今回印を書く Scene の path（Prepare の後）。test と診断のため。
        internal IReadOnlyList<string> TargetPaths => _targets.Select(x => x.Path).ToArray();

        // 印の対象は main asset が GameObject の物（prefab・model）と ModelImporter の物。鍵は GetAssetDependencyHash。
        // どちらも AssetDatabase の記録を引くだけで、asset の file のバイトを読まない（検知を軽く保つ owner の条件）。
        private static StampProbe UnityProbe(string path)
        {
            var target = AssetDatabase.GetMainAssetTypeAtPath(path) == typeof(GameObject)
                         || AssetImporter.GetAtPath(path) is ModelImporter;
            return new StampProbe(target, target ? AssetDatabase.GetAssetDependencyHash(path).ToString() : "");
        }

        // 書く前の検査。source へ書く操作と import を一切しない。止める理由があれば全てを集めて例外で止める
        // （coordinator の既存の経路で BuildFailure になる）。止めた時は前回の残りも戻さない（次に書き込める build が戻す）。
        internal void Prepare(IReadOnlyList<ProjectedContent> roots)
        {
            if (_prepared) throw new InvalidOperationException("Prepare was already called.");
            _prepared = true;
            var problems = new List<string>();
            var open = OpenScenes();
            var records = ReadRecords();
            var targetGuids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var root in roots.Where(x => ScenePrefabStampRules.IsScene(x.Path)))
            {
                var hash = ScenePrefabStampRules.StampHash(root.Closure.Select(x => x.Path), Probe);
                var guid = AssetDatabase.AssetPathToGUID(root.Path);
                var meta = Disk(root.Path + ".meta");
                if (hash == null)
                {
                    // 印を要らない Scene は、記録の無い残った印の戻しの対象でない限り読むだけで触れない。
                    if (records.ContainsKey(guid) || !File.Exists(meta)) continue;
                    if (ScenePrefabStampRules.FindUserData(ReadText(meta)).State == UserDataState.OwnStamp)
                    {
                        _leftovers.Add(new Leftover(guid, root.Path, null));
                        CheckWritable(root.Path, meta, open, problems);
                    }
                    continue;
                }
                if (string.IsNullOrEmpty(guid) || !File.Exists(meta))
                {
                    problems.Add(root.Path + ": the scene or its .meta is missing");
                    continue;
                }
                var state = ScenePrefabStampRules.FindUserData(ReadText(meta)).State;
                if (state == UserDataState.Other)
                    problems.Add(root.Path + ": the .meta userData is not empty (the build writes its stamp only into an empty userData)");
                else if (state == UserDataState.OwnStamp && !records.ContainsKey(guid))
                    _leftovers.Add(new Leftover(guid, root.Path, null));
                CheckWritable(root.Path, meta, open, problems);
                _targets.Add(new Target(guid, root.Path, hash));
                targetGuids.Add(guid);
            }
            foreach (var record in records.Values)
            {
                var path = AssetDatabase.GUIDToAssetPath(record.guid);
                var exists = !string.IsNullOrEmpty(path) && File.Exists(Disk(path + ".meta"));
                _leftovers.Add(new Leftover(record.guid, exists ? path : null, record));
                if (exists && !targetGuids.Contains(record.guid)) CheckWritable(path, Disk(path + ".meta"), open, problems);
            }
            if (problems.Count != 0)
                throw new InvalidOperationException("The scene prefab stamp stopped before writing any file: " + string.Join("; ", problems));
        }

        // 前回の残りを戻し、今回の印を書く。adapter は 1 つ目の .meta を書く前に、この呼び出しを戻しの scope（try/finally の Dispose）に入れる。
        internal void Apply()
        {
            if (!_prepared || _applied) throw new InvalidOperationException("Apply must follow Prepare once.");
            _applied = true;
            Directory.CreateDirectory(_recordDirectory);
            foreach (var file in _unreadableRecords) File.Delete(file);
            var originals = new Dictionary<string, string>(StringComparer.Ordinal);
            var targetGuids = new HashSet<string>(_targets.Select(x => x.Guid), StringComparer.Ordinal);
            foreach (var leftover in _leftovers)
            {
                if (leftover.Path == null)
                {
                    Debug.LogWarning("Scene prefab stamp: the scene of a leftover record is missing; record dropped (guid " + leftover.Guid + ").");
                    DeleteRecord(leftover.Guid);
                    continue;
                }
                var meta = Disk(leftover.Path + ".meta");
                var current = ReadText(meta);
                var kind = Decide(current, leftover.Record);
                Warn(kind, leftover.Path, leftover.Record == null);
                var original = leftover.Record == null ? current : Original(leftover.Record);
                if (targetGuids.Contains(leftover.Guid))
                {
                    // 今回も印を書く Scene は、戻した後の text を file に書かず import もせず、今回の記録の元にする。
                    // 前の記録は同じ GUID の file 名なので、今回の記録の保存がそれを置き換える（保存してから消す順になる）。
                    originals[leftover.Guid] = ScenePrefabStampRules.EffectiveOriginal(kind, current, original);
                    continue;
                }
                var restored = ScenePrefabStampRules.Restored(kind, current, original);
                if (restored != null)
                {
                    ReplaceAtomically(meta, ScenePrefabStampRules.ToBytes(restored));
                    AssetDatabase.ImportAsset(leftover.Path, ImportAssetOptions.ForceUpdate);
                }
                DeleteRecord(leftover.Guid);
            }
            foreach (var target in _targets)
            {
                var meta = Disk(target.Path + ".meta");
                var original = originals.TryGetValue(target.Guid, out var o) ? o : ReadText(meta);
                var stamped = ScenePrefabStampRules.WriteStamp(original, target.Hash);
                var originalBytes = ScenePrefabStampRules.ToBytes(original);
                var stampedBytes = ScenePrefabStampRules.ToBytes(stamped);
                var record = new Record
                {
                    guid = target.Guid, path = target.Path,
                    originalSha256 = ScenePrefabStampRules.Sha256Hex(originalBytes),
                    writtenSha256 = ScenePrefabStampRules.Sha256Hex(stampedBytes),
                    original = Convert.ToBase64String(originalBytes),
                };
                SaveRecord(record);
                try { ReplaceAtomically(meta, stampedBytes); }
                catch
                {
                    // 書けなかった Scene の記録は残さない（残すと、元のままの .meta を次の build が「印でない値」と読んで Warning を出す）。
                    // 前回の残りを今回の元にした Scene では、その元は戻した後の text なので、file は前回の印のまま残り、
                    // userData の形から次の build が戻す。
                    DeleteRecord(target.Guid);
                    throw;
                }
                target.Written = record;
            }
        }

        // build の後の戻し。印を書いた Scene ごとに戻して import し、その Scene の記録を消す。
        // 1 つの Scene の失敗で残りを止めず、最初の例外を最後に投げ直す。
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            ExceptionDispatchInfo? first = null;
            foreach (var target in _targets.Where(x => x.Written != null))
            {
                try { RestoreWritten(target); }
                catch (Exception ex) { first ??= ExceptionDispatchInfo.Capture(ex); }
            }
            first?.Throw();
        }

        private void RestoreWritten(Target target)
        {
            var record = target.Written!;
            var path = AssetDatabase.GUIDToAssetPath(target.Guid);
            if (string.IsNullOrEmpty(path)) path = target.Path;
            var meta = Disk(path + ".meta");
            if (!File.Exists(meta))
            {
                Warn(StampRestoreKind.Missing, path, false);
                DeleteRecord(target.Guid);
                return;
            }
            var current = ReadText(meta);
            var kind = Decide(current, record);
            Warn(kind, path, false);
            var restored = ScenePrefabStampRules.Restored(kind, current, Original(record));
            if (restored != null) ReplaceAtomically(meta, ScenePrefabStampRules.ToBytes(restored));
            // build の製品の Refresh が印の版を import したので、戻した版を import し直させる（Refresh は使わない）。
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            DeleteRecord(target.Guid);
        }

        private StampProbe Probe(string path)
        {
            // 同じ path の鍵は 1 回の build の中で 1 回だけ引く（複数の Scene が共有する prefab）。値は変わらない。
            if (!_probed.TryGetValue(path, out var result)) _probed[path] = result = _probe(path);
            return result;
        }

        private static StampRestoreKind Decide(string current, Record? record)
        {
            var state = ScenePrefabStampRules.FindUserData(current).State;
            if (record == null)
                return state == UserDataState.OwnStamp ? StampRestoreKind.RestoreLineOnly : StampRestoreKind.LeaveForeign;
            var sha = ScenePrefabStampRules.Sha256Hex(ScenePrefabStampRules.ToBytes(current));
            return ScenePrefabStampRules.DecideRestore(true, sha, record.originalSha256, record.writtenSha256, state);
        }

        private static void Warn(StampRestoreKind kind, string path, bool recordless)
        {
            switch (kind)
            {
                case StampRestoreKind.RestoreLineOnly:
                    Debug.LogWarning("Scene prefab stamp: restored only the userData line of " + path + ".meta" +
                                     (recordless ? " (no record; the stamp was left by an earlier build)." : " (other lines were changed during or after the build)."));
                    break;
                case StampRestoreKind.LeaveForeign:
                    Debug.LogWarning("Scene prefab stamp: " + path + ".meta no longer holds this build's stamp; left as it is.");
                    break;
                case StampRestoreKind.Missing:
                    Debug.LogWarning("Scene prefab stamp: " + path + ".meta is missing; nothing restored.");
                    break;
            }
        }

        // 止める理由: 読み取り専用の .meta と、Editor で開かれている Scene（active でない Scene を含み、変更の有無を問わない）。
        // 開いている Scene の .meta を書いて import させると、未保存の変更が無くても、Editor が次の更新で
        // 「The open scene(s) have been modified externally」（Reload / Ignore）のモーダルダイアログを出し、
        // 戻しの import でもう一度出す（Phase B の Editor の確認。HANDOFF CONTENT_PREFAB_STAMP §6）。Reload を選ぶと未保存の変更が消えうるので、
        // 開いていれば書かずに止める（A3 で合意した代替の AC8'）。印を書かず戻しの対象でもない Scene は、開いていても止めない（file に触れない）。
        private static void CheckWritable(string path, string meta, IReadOnlyDictionary<string, bool> open, List<string> problems)
        {
            if ((File.GetAttributes(meta) & FileAttributes.ReadOnly) != 0)
                problems.Add(path + ": the .meta is read-only");
            if (open.TryGetValue(path, out var dirty))
                problems.Add(path + ": the scene is open in the Editor" + (dirty ? " with unsaved changes" : "") +
                             "; close it before building (the build writes its .meta)");
        }

        private static IReadOnlyDictionary<string, bool> OpenScenes()
        {
            var open = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (string.IsNullOrEmpty(scene.path)) continue;
                open[scene.path] = (open.TryGetValue(scene.path, out var dirty) && dirty) || scene.isDirty;
            }
            return open;
        }

        private Dictionary<string, Record> ReadRecords()
        {
            var records = new Dictionary<string, Record>(StringComparer.Ordinal);
            if (!Directory.Exists(_recordDirectory)) return records;
            foreach (var file in Directory.GetFiles(_recordDirectory, "*" + RecordExtension).OrderBy(x => x, StringComparer.Ordinal))
            {
                Record? record = null;
                try { record = JsonUtility.FromJson<Record>(File.ReadAllText(file)); }
                catch (ArgumentException) { }
                if (record == null || string.IsNullOrEmpty(record.guid) || Path.GetFileNameWithoutExtension(file) != record.guid)
                {
                    // 読めない記録で build を止め続けない。記録が失われた印は userData の形から、その Scene を root に含む build が戻す。
                    Debug.LogWarning("Scene prefab stamp: ignored an unreadable record " + Path.GetFileName(file) + ".");
                    _unreadableRecords.Add(file);
                    continue;
                }
                records[record.guid] = record;
            }
            return records;
        }

        private void SaveRecord(Record record)
        {
            var path = RecordPath(record.guid);
            var temp = TempPath();
            File.WriteAllText(temp, JsonUtility.ToJson(record));
            try
            {
                if (File.Exists(path)) File.Replace(temp, path, null);
                else File.Move(temp, path);
            }
            finally { if (File.Exists(temp)) File.Delete(temp); }
        }

        private void DeleteRecord(string guid)
        {
            var path = RecordPath(guid);
            if (File.Exists(path)) File.Delete(path);
        }

        // Library の下（記録の場所）の一時 file に書いてから置き換える。一時 file を Assets/ の下に置かない（Unity に import させない）。
        private void ReplaceAtomically(string destination, byte[] bytes)
        {
            var temp = TempPath();
            File.WriteAllBytes(temp, bytes);
            try { File.Replace(temp, destination, null); }
            finally { if (File.Exists(temp)) File.Delete(temp); }
        }

        private string RecordPath(string guid) => Path.Combine(_recordDirectory, guid + RecordExtension);
        private string TempPath() => Path.Combine(_recordDirectory, "write-" + Guid.NewGuid().ToString("N") + ".tmp");
        private static string Original(Record record) => ScenePrefabStampRules.ToText(Convert.FromBase64String(record.original));
        private static string ReadText(string file) => ScenePrefabStampRules.ToText(File.ReadAllBytes(file));
        private static string Disk(string assetPath) => Path.Combine(ProjectRoot, assetPath);
    }
}
