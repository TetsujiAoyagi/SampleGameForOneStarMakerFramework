#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using SampleGame.DependOnAll.Editor.WorldAuthoring;
using UnityEngine;

namespace OneStarMaker.Tests.Editor.WorldAuthoring
{
    // World Workspace の companion 生成器が作る path は、tools/path-budget.json の repo 相対の予算に
    // 収まらなければならない。Unity 同梱の Mono は 260 文字以上の path を File.OpenRead などで開けず、
    // 予算を超えた生成物は深い checkout でだけ所有の証明に失敗する。tracked の file は contract-audit が
    // 見るが、生成器がこれから作る path はまだ tree に無いので、ここで生成器の出力そのものを確かめる。
    // 保証するのは列挙した生成器（WorldCompanionCreationPlan の Cell companion と Season Atmosphere、回復 journal）だけ。
    [TestFixture]
    public sealed class WorldCompanionPathBudgetTests
    {
        [Serializable]
        private sealed class PathBudgetFile
        {
            public int schemaVersion;
            public int maxRepoRelativePathLength;
        }

        [Test]
        public void CompanionGeneratorOutputs_StayWithinRepoRelativeBudget()
        {
            var budget = ReadBudget();
            var paths = new List<string>();
            foreach (var plan in AcceptedProductionPlans()) AddPlanPaths(paths, plan);
            foreach (WorldSeason season in Enum.GetValues(typeof(WorldSeason)))
                AddPlanPaths(paths, WorldCompanionCreationPlan.CreateSeasonAtmosphere(season));
            AddPlanPaths(paths, WorldCompanionTestScope.CreateFixturePlan(WorldCompanionTestScope.NewToken()));
            AddPlanPaths(paths, WorldCompanionTestScope.CreateFixturePlan(
                WorldCompanionTestScope.NewToken(), WorldCompanionCreationKind.SeasonAtmosphere));
            AddJournalPaths(paths);

            var longest = paths.OrderByDescending(path => path.Length).First();
            Assert.That(longest.Length, Is.LessThanOrEqualTo(budget),
                $"生成器の path が tools/path-budget.json の予算を超えた: {longest}（{longest.Length} 文字）");
        }

        private static string RepoRoot
        {
            get
            {
                var projectRoot = Path.GetDirectoryName(Application.dataPath)
                    ?? throw new InvalidOperationException("Project root is unavailable.");
                return Path.GetDirectoryName(projectRoot)
                    ?? throw new InvalidOperationException("Repository root is unavailable.");
            }
        }

        private static int ReadBudget()
        {
            // 予算の正本の検証（未知の key、整数の表記など）は contract-audit と run-tests が持つ。
            // ここでは R を読み、明らかに不正な値で検査を素通りさせないことだけを確かめる。
            var path = Path.Combine(RepoRoot, "tools", "path-budget.json");
            Assert.That(File.Exists(path), Is.True, $"path 予算の正本がありません: {path}");
            var budget = JsonUtility.FromJson<PathBudgetFile>(File.ReadAllText(path));
            Assert.That(budget, Is.Not.Null);
            Assert.That(budget!.schemaVersion, Is.EqualTo(1));
            Assert.That(budget.maxRepoRelativePathLength, Is.InRange(1, 258));
            return budget.maxRepoRelativePathLength;
        }

        private static List<WorldCompanionCreationPlan> AcceptedProductionPlans()
        {
            // 受理される座標と役割の範囲は、WorldWorkspaceSelection と Create が拒否するかどうかから
            // 導く。範囲の定数をここへ複製すると、生成器の範囲だけが広がった時に検査が追従しない。
            // 座標は 0〜99 の格子を 1 組の季節・役割・payload で探る。全ての組で格子を探ると、拒否の
            // 例外が数十万回になって遅い。見つけた座標は全ての組と掛け合わせ、constructor にもう一度
            // 判定させるので、組によって受理が変わっても受理されない組は混ざらない。
            const int probeLimit = 99;
            var seasons = Enum.GetValues(typeof(WorldSeason)).Cast<WorldSeason>().ToArray();
            var roles = Enum.GetValues(typeof(WorldWorkspaceRole)).Cast<WorldWorkspaceRole>().ToArray();
            var payloads = Enum.GetValues(typeof(WorldLevelPayload)).Cast<WorldLevelPayload>().ToArray();
            var coordinates = new List<(int X, int Y)>();
            for (var x = 0; x <= probeLimit; x++)
            for (var y = 0; y <= probeLimit; y++)
            {
                if (TrySelect(seasons[0], x, y, roles[0], payloads[0]) != null) coordinates.Add((x, y));
            }
            Assert.That(coordinates, Is.Not.Empty, "受理される座標が 1 つも無い。列挙が壊れている");

            var plans = new List<WorldCompanionCreationPlan>();
            foreach (var season in seasons)
            foreach (var role in roles)
            foreach (var payload in payloads)
            foreach (var (x, y) in coordinates)
            {
                var selection = TrySelect(season, x, y, role, payload);
                if (selection == null) continue;
                // Create は companion を作れない役割（Level / Environment）を拒否する。
                try { plans.Add(WorldCompanionCreationPlan.Create(selection)); }
                catch (InvalidOperationException) { }
            }
            Assert.That(plans, Is.Not.Empty, "生成器が受理する組が 1 つも無い。列挙が壊れている");
            return plans;
        }

        private static WorldWorkspaceSelection? TrySelect(
            WorldSeason season, int x, int y, WorldWorkspaceRole role, WorldLevelPayload payload)
        {
            try { return new WorldWorkspaceSelection(season, x, y, role, payload); }
            catch (ArgumentOutOfRangeException) { return null; }
        }

        private static void AddPlanPaths(List<string> paths, WorldCompanionCreationPlan plan)
        {
            foreach (var assetPath in new[] { plan.ScenePath, plan.ResourcePath, plan.NodePath, plan.SceneResourceMapPath })
            {
                Assert.That(assetPath, Does.StartWith("Assets/"), "生成器の path は project 相対の Assets/ にある");
                // Unity は asset ごとに .meta を並べて作る。repo 相対には project の folder（unity/）を加える。
                paths.Add($"unity/{assetPath}");
                paths.Add($"unity/{assetPath}.meta");
            }
        }

        private static void AddJournalPaths(List<string> paths)
        {
            var journal = Path.GetRelativePath(RepoRoot, WorldCompanionRecoveryJournal.JournalPath).Replace('\\', '/');
            Assert.That(journal, Does.StartWith("unity/"), "回復 journal は project の下にある");
            paths.Add(journal);
            // Save は同じ folder に一時 file と backup を並べてから置き換える。
            paths.Add(journal + ".tmp");
            paths.Add(journal + ".bak");
        }
    }
}
