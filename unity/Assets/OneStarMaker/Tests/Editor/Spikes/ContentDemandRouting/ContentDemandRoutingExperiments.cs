#nullable enable

using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine.TestTools;

namespace OneStarMaker.Tests.Editor.Spikes.ContentDemandRouting
{
    /// <summary>
    /// S0-b の実験（program §8 の (1)〜(6)、HANDOFF docs/handoff/CONTENT_DEMAND_ROUTING_SPIKE.md）。
    /// Editor で開いている Scene の snapshot と built Content Directory の Scene を同じ Editor Play で扱い、
    /// 機構 (i)（封じより前に既ロード Scene として渡す）と (ii)（一時 directory を build して登録する）を観測する。
    ///
    /// 1 つの test が 1 つの Enter Play Mode の設定を受け持ち、その設定の全シナリオの Play の往復を順に行う。
    /// 基準（Unity 標準の Play、AC0 の負の対照、saveAsCopy 単独の対照、製品の 2 件目の登録の拒否）は設定を変えない別の test。
    /// 観測は全て `TestResults/ContentDemandRouting/&lt;run&gt;/&lt;test&gt;.jsonl` の record に残す。
    /// assert は harness 自身の健全さ（record を書けた、設定を復元した、一時 root を回収した、AC0 の検出器が壊れを検出した）に限る。
    /// N1〜N5 の破れは assert にせず record の行として残し、cell の値と機構の判定は判断担当が決める（HANDOFF §5）。
    ///
    /// 通常の全 EditMode 回帰から外すため `[Explicit]` にする（U-b3）。`run-tests.ps1` は `-testFilter` だけを渡すので、
    /// namespace の filter で明示に選ばれた時に実行されるかは Phase C の最初の実行で確かめる。
    /// この folder は製品に残さない。マージ前の Phase D で消す（N6）。
    /// </summary>
    [Explicit("S0-b spike: changes Enter Play Mode settings and builds Content Directories; run only by explicit filter")]
    public sealed class ContentDemandRoutingExperiments
    {
        private const int Timeout = 4 * 60 * 60 * 1000;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (EditorApplication.isPlaying) yield return new ExitPlayMode();
            // 途中で失敗した test の後始末。正常に終わった test では Finish が既に済んでおり、ここは何もしない。
            if (HarnessState.CurrentPath() != null) RoundRunner.Finish();
        }

        [UnityTest, Timeout(Timeout)]
        public IEnumerator Baseline_DetectorAndControls()
        {
            yield return RoundRunner.Run(nameof(Baseline_DetectorAndControls), EpmSetting.ProjectDefault, Scenarios.BaselineRounds);
            var result = RoundRunner.Finish();
            AssertHealthy(result);
            Assert.That(result.detectorResults, Has.Count.EqualTo(2), "AC0 の負の対照が 2 件とも実行されていない");
            Assert.That(result.detectorResults, Has.All.EndsWith("=detected"), "AC0: N1 の比較が意図的な壊れを検出しなかった");
        }

        [UnityTest, Timeout(Timeout)]
        public IEnumerator Matrix_DomainReload_SceneReload()
        {
            yield return RoundRunner.Run(nameof(Matrix_DomainReload_SceneReload), EpmSetting.DomainReloadSceneReload, Scenarios.MatrixRounds);
            AssertHealthy(RoundRunner.Finish());
        }

        [UnityTest, Timeout(Timeout)]
        public IEnumerator Matrix_DomainReload_NoSceneReload()
        {
            yield return RoundRunner.Run(nameof(Matrix_DomainReload_NoSceneReload), EpmSetting.DomainReloadNoSceneReload, Scenarios.MatrixRounds);
            AssertHealthy(RoundRunner.Finish());
        }

        [UnityTest, Timeout(Timeout)]
        public IEnumerator Matrix_NoDomainReload_SceneReload()
        {
            yield return RoundRunner.Run(nameof(Matrix_NoDomainReload_SceneReload), EpmSetting.NoDomainReloadSceneReload, Scenarios.MatrixRounds);
            AssertHealthy(RoundRunner.Finish());
        }

        [UnityTest, Timeout(Timeout)]
        public IEnumerator Matrix_NoDomainReload_NoSceneReload()
        {
            yield return RoundRunner.Run(nameof(Matrix_NoDomainReload_NoSceneReload), EpmSetting.NoDomainReloadNoSceneReload, Scenarios.MatrixRounds);
            AssertHealthy(RoundRunner.Finish());
        }

        private static void AssertHealthy(RoundRunner.FinishResult result)
        {
            Assert.That(result.recordPath, Is.Not.Empty, "record の場所が分からない");
            Assert.That(File.Exists(result.recordPath), Is.True, "record が書かれていない: " + result.recordPath);
            Assert.That(result.recordLines, Is.GreaterThan(0), "record が空");
            Assert.That(result.roundsCompleted, Is.EqualTo(result.roundsTotal), "行列を最後まで進められなかった");
            Assert.That(result.epmRestored, Is.True, "Enter Play Mode の設定を元へ戻せなかった");
            Assert.That(result.startSceneRestored, Is.True, "Play の初期 Scene の設定を元へ戻せなかった");
            Assert.That(result.leaseReleased, Is.True, "一時 root を回収できなかった");
            Assert.That(result.harnessErrors, Is.Empty, "harness 自身の失敗: " + string.Join(" / ", result.harnessErrors));
        }
    }
}
