#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace OneStarMaker.Tests.Editor.Spikes.ContentDemandRouting
{
    /// <summary>行列の 1 行（Play の往復 1 回、または Play を伴わない観測 1 回）。</summary>
    [Serializable]
    internal sealed class RoundSpec
    {
        public string mechanism = "";
        public string scenario = "";
        public int iteration;
        public int play;
    }

    /// <summary>
    /// test 1 件の実行状態。Domain Reload は Play に入るたびに managed の状態を消し、test の enumerator の
    /// 局所変数も戻らないことがある。そこで round の進み具合と round の観測の途中結果を全てこの file に置き、
    /// yield の後は必ず file から読み直す。file の場所は SessionState に置く（Editor の session の間だけ残る）。
    /// </summary>
    [Serializable]
    internal sealed class HarnessState
    {
        private const string StateFileKey = "OsmSpikeCdr.StateFile";

        public string testName = "";
        public string runId = "";
        public string epm = "";
        public string recordPath = "";
        public string leaseId = "";
        public string leaseToken = "";
        public string gameBootstrapEnvKey = "";
        public bool fixtureReady;
        public string fixtureError = "";
        public FixturePaths fixture = new();
        public bool fixtureDirectoryReady;
        public string fixtureDirectoryError = "";
        public BuiltDirectory fixtureDirectory = new();
        public List<RoundSpec> rounds = new();
        public int index;
        public bool finished;

        // 現在の round。round の開始時に初期化する。
        public bool roundPlays;
        public string roundError = "";
        public List<SnapshotInfo> snapshots = new();
        public bool hasTempDirectory;
        public BuiltDirectory tempDirectory = new();
        public List<string> mapIdentities = new();
        public bool isolation;
        public string expectedMarker = "";
        public string t0 = "";
        public string t1 = "";
        public string prefabT0 = "";
        public string prefabBackupPath = "";
        public string staleLeasePath = "";

        // 直前の round が残した物の痕跡。次の round の開始時の検出（N4、AC8）に使う。
        public List<string> previousSnapshotGuids = new();
        public List<string> previousSnapshotPaths = new();
        public List<string> previousTempDirectories = new();

        // test 全体で集める健全さの観測（HANDOFF §5 の test の assert）。
        public List<string> detectorResults = new();
        public List<string> harnessErrors = new();

        internal RoundSpec Round => rounds[index];
        internal bool Done => index >= rounds.Count;
        internal Cell Cell => Done ? Cell.Of("-", epm, "finish") : Cell.Of(Round.mechanism, epm, Round.scenario, Round.iteration, Round.play);

        internal static string? CurrentPath()
        {
            var path = SessionState.GetString(StateFileKey, string.Empty);
            return path.Length == 0 ? null : path;
        }

        internal static HarnessState? TryLoad(string testName)
        {
            var path = CurrentPath();
            if (path == null || !File.Exists(path)) return null;
            var state = JsonUtility.FromJson<HarnessState>(File.ReadAllText(path));
            return state.testName == testName && !state.finished ? state : null;
        }

        internal static HarnessState Load()
        {
            var path = CurrentPath() ?? throw new InvalidOperationException("No harness state is active.");
            return JsonUtility.FromJson<HarnessState>(File.ReadAllText(path));
        }

        internal static HarnessState Begin(string testName, EpmSetting epm, List<RoundSpec> rounds)
        {
            var runId = DateTime.UtcNow.ToString("yyyyMMddTHHmmssZ") + "-" + Guid.NewGuid().ToString("N").Substring(0, 6);
            var folder = Path.Combine(ExperimentRecord.OutputRoot(), runId);
            Directory.CreateDirectory(folder);
            var state = new HarnessState
            {
                testName = testName, runId = runId, epm = epm.ToString(), rounds = rounds,
                recordPath = Path.Combine(folder, testName + ".jsonl"),
            };
            SessionState.SetString(StateFileKey, Path.Combine(folder, testName + ".state.json"));
            state.Save();
            return state;
        }

        internal void Save()
        {
            var path = CurrentPath() ?? throw new InvalidOperationException("No harness state is active.");
            File.WriteAllText(path, JsonUtility.ToJson(this, true));
        }

        internal static void Forget() => SessionState.EraseString(StateFileKey);

        internal ExperimentRecord OpenRecord() => ExperimentRecord.Open(recordPath);

        internal TempRootLease? Lease() => leaseId.Length == 0 ? null : TempRootLease.Attach(leaseId, leaseToken);

        internal void ResetRound()
        {
            roundPlays = false; roundError = ""; snapshots = new List<SnapshotInfo>(); hasTempDirectory = false;
            tempDirectory = new BuiltDirectory(); mapIdentities = new List<string>(); isolation = true; expectedMarker = "";
            t0 = ""; t1 = ""; prefabT0 = ""; prefabBackupPath = ""; staleLeasePath = "";
        }

        /// <summary>map の各 identity の、Editor 上の元の Scene の GUID（SceneResource の payload に入れる。directory mode では使われない）。</summary>
        internal Dictionary<string, string> SceneGuids()
        {
            var guids = new Dictionary<string, string>(StringComparer.Ordinal);
            void Add(string identity, string path) { if (path.Length != 0) guids[identity] = AssetDatabase.AssetPathToGUID(path); }
            Add("Entry", fixture.entry);
            Add("Other", fixture.other);
            Add("Dup", fixture.dup);
            Add("Next", fixture.buildNext);
            return guids;
        }
    }
}
