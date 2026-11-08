#nullable enable

using System;
using System.Collections.Generic;

namespace OneStarMaker.Tests.Editor.Spikes.ContentDemandRouting
{
    /// <summary>
    /// シナリオ（HANDOFF §2 受け入れ条件の各行）の名前と、行列の round の並び。
    /// 名前は record の `scenario` に入り、判定担当が cell を組み立てる鍵になる。
    /// </summary>
    internal static class Scenarios
    {
        // 基準の test（Enter Play Mode の設定に依らない）
        internal const string Baseline = "ac1-baseline";
        internal const string BaselineOtherNoIsolation = "ac5-control-no-isolation";
        // 隔離なしで Other が map に無い対照（AC5 の「map に無い場合」）。行列の round は Play の起点が Boot なので
        // Editor の additive な Other が Play に載らず、map の有無の差をここでしか観測できない。
        internal const string BaselineOtherNotInMapNoIsolation = "ac5-control-no-isolation-not-in-map";
        internal const string DetectorRewriteSave = "ac0-rewrite-save";
        internal const string DetectorClearDirty = "ac0-clear-dirty";
        internal const string SaveAsCopyControl = "ac2-saveascopy-control";
        internal const string OsmGateConstraint = "osm-gate-second-registration";

        // 行列（機構 × Enter Play Mode の設定）
        internal const string Main = "main-dirty";
        internal const string NewUnsaved = "ac2-new-unsaved";
        internal const string AdditiveTwo = "ac2-additive-two";
        internal const string PrefabOverride = "ac2-prefab-override";
        internal const string RefPrefabChanged = "ac2-ref-prefab-changed";
        internal const string OtherInMap = "ac5-other-in-map";
        internal const string OtherNotInMap = "ac5-other-not-in-map";
        internal const string CancelGate = "ac6-cancel-gate";
        internal const string StopWithoutNext = "ac7a-stop-without-next";
        internal const string StopDuringGate = "ac7b-stop-during-gate";
        internal const string Reentry = "ac8-reentry";
        internal const string FailSnapshot = "ac9a-fail-snapshot";
        internal const string FailBuild = "ac9a-fail-build";
        internal const string StaleRemnant = "ac9b-stale-remnant";
        internal const string LeaseConflict = "ac10-lease-conflict";
        internal const string NoTypeTree = "ac11-no-type-tree";

        internal const string NoMechanism = "baseline";

        /// <summary>native の timing が絡む cell の反復回数（HANDOFF §2 反復）。</summary>
        internal const int Repetitions = 3;

        internal static List<RoundSpec> BaselineRounds()
        {
            var rounds = new List<RoundSpec>();
            Add(rounds, NoMechanism, OsmGateConstraint);
            Add(rounds, NoMechanism, SaveAsCopyControl);
            Add(rounds, NoMechanism, DetectorRewriteSave);
            Add(rounds, NoMechanism, DetectorClearDirty);
            Add(rounds, NoMechanism, Baseline);
            Add(rounds, NoMechanism, BaselineOtherNoIsolation);
            Add(rounds, NoMechanism, BaselineOtherNotInMapNoIsolation);
            return rounds;
        }

        /// <summary>
        /// 行列の round を scenario 名で絞る Process の環境変数（カンマ区切り。例 `ac6-cancel-gate`）。未設定または空なら全 round。
        /// 判定 C が修正した cell だけを新しい head で再実行し、他の cell は前の head の evidence を再利用するための手段
        /// （HANDOFF の決定記録 D-1）。基準の test には効かない（AC0 の検出器の件数を assert しているため）。
        /// 絞った実行は record の `harness.started` の `matrixScenarioFilter` に値が残る。全件の実行では null。
        /// </summary>
        internal const string MatrixFilterVariable = "OSM_CDR_MATRIX_SCENARIOS";

        /// <summary>環境変数の値（前後の空白を除く）。未設定または空なら null。</summary>
        internal static string? MatrixFilter()
        {
            var value = Environment.GetEnvironmentVariable(MatrixFilterVariable)?.Trim();
            return string.IsNullOrEmpty(value) ? null : value;
        }

        internal static List<RoundSpec> MatrixRounds()
        {
            var all = AllMatrixRounds();
            var filter = MatrixFilter();
            if (filter == null) return all;
            var wanted = new HashSet<string>(StringComparer.Ordinal);
            foreach (var name in filter.Split(',')) if (name.Trim().Length != 0) wanted.Add(name.Trim());
            var known = new HashSet<string>(StringComparer.Ordinal);
            foreach (var round in all) known.Add(round.scenario);
            // 綴りの誤りで round が黙って 0 件や一部欠けにならないよう、行列に無い名前は test の開始で失敗させる。
            foreach (var name in wanted)
                if (!known.Contains(name))
                    throw new ArgumentException(MatrixFilterVariable + " names an unknown matrix scenario: " + name);
            return all.FindAll(round => wanted.Contains(round.scenario));
        }

        private static List<RoundSpec> AllMatrixRounds()
        {
            var rounds = new List<RoundSpec>();
            foreach (var mechanism in new[] { MechanismAlreadyLoaded.Name, MechanismTempDirectory.Name })
            {
                Add(rounds, mechanism, LeaseConflict);
                Add(rounds, mechanism, FailSnapshot);
                if (mechanism == MechanismTempDirectory.Name) Add(rounds, mechanism, FailBuild);
                Add(rounds, mechanism, StaleRemnant);
                Add(rounds, mechanism, Main);
                Add(rounds, mechanism, NewUnsaved);
                Add(rounds, mechanism, AdditiveTwo);
                Add(rounds, mechanism, PrefabOverride);
                Add(rounds, mechanism, RefPrefabChanged);
                Add(rounds, mechanism, OtherInMap);
                Add(rounds, mechanism, OtherNotInMap);
                for (var i = 0; i < Repetitions; i++)
                {
                    Add(rounds, mechanism, CancelGate, i);
                    Add(rounds, mechanism, StopWithoutNext, i);
                    Add(rounds, mechanism, StopDuringGate, i);
                    // Play → 停止 → 再 Play を 2 回繰り返す。再 Play の前に毎回内容とマーカーを変えて snapshot を作り直す（AC8）。
                    for (var play = 0; play < 3; play++) Add(rounds, mechanism, Reentry, i, play);
                }
                // 機構 (ii) の失敗経路（AC11）。native がどう失敗するか未知で、Editor ごと落ちると後の round を失うので、行列の最後に置く。
                if (mechanism == MechanismTempDirectory.Name) Add(rounds, mechanism, NoTypeTree);
            }
            return rounds;
        }

        /// <summary>Play の初期 Scene を隔離しない（Unity 標準の Play で元の Scene を開いたまま入る）基準の round か。</summary>
        internal static bool IsUnisolatedBaseline(string scenario)
            => scenario is Baseline or BaselineOtherNoIsolation or BaselineOtherNotInMapNoIsolation;

        /// <summary>Other を fixture の SceneResourceMap に載せない round か（AC5 の「map に無い場合」）。</summary>
        internal static bool OtherOutsideMap(string scenario)
            => scenario is OtherNotInMap or BaselineOtherNotInMapNoIsolation;

        internal static bool Plays(string scenario)
            => scenario is not (SaveAsCopyControl or OsmGateConstraint or LeaseConflict or FailSnapshot or FailBuild);

        private static void Add(List<RoundSpec> rounds, string mechanism, string scenario, int iteration = 0, int play = 0)
            => rounds.Add(new RoundSpec { mechanism = mechanism, scenario = scenario, iteration = iteration, play = play });
    }
}
