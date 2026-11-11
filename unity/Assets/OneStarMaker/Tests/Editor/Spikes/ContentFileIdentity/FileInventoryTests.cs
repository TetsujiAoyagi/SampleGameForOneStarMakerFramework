#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace OneStarMaker.Tests.Editor.Spikes.ContentFileIdentity
{
    /// <summary>
    /// スパイクの判定の写像（HANDOFF §2）を、合成の ContentLayout と出力の一覧だけで確かめる。Unity も file も触らない。
    /// 主眼は「誤って YES を出す」経路（名前だけの比較、同じ root 集合、帰属しない file、届かない書き換え）が
    /// GO / CONDITIONAL ACCEPT にならないこと。
    /// </summary>
    public sealed class FileInventoryTests
    {
        private const string Tex = "Assets/T.png", Mat = "Assets/M.mat", Lit = "Assets/L.mat", Prefab = "Assets/P.prefab",
            Shader = "Assets/S.shader";
        private static readonly KindClassifier Kinds = new KindClassifier(Tex, Mat, Lit, Prefab, Shader);

        private sealed class Spec
        {
            public Spec(string id, string hash, params string[] sources) { Id = id; Hash = hash; Sources = sources; }
            public string Id { get; }
            public string Hash { get; }
            public string[] Sources { get; }
            public string? Sha { get; set; }
            public string? ResS { get; set; }
        }

        private static List<Spec> Common(string scriptHash = "h-sc") => new List<Spec>
        {
            new Spec("t.cfid", "h-t", Tex) { ResS = "h-tr" }, new Spec("m.cfid", "h-m", Mat), new Spec("l.cfid", "h-l", Lit),
            new Spec("p.cfid", "h-p", Prefab), new Spec("s.cfid", "h-s", Shader), new Spec("ls.cfid", "h-ls", "Packages/u/Lit.shader"),
            new Spec("sc.cfid", scriptHash, "Assets/R.cs"),
        };

        // root は build ごとに別の asset（製品と同じく root の file は必ず別になる）。colocated で共通物を同居させる。
        private static BuildInventory Build(string name, IEnumerable<Spec> specs, string[]? colocated = null,
            string? extraFile = null, string? pointer = null)
        {
            var manifest = "h-man-" + name;
            var rootPath = "Assets/Root_" + name + ".asset";
            var all = specs.ToList();
            all.Add(new Spec("root-" + name + ".cfid", "h-root-" + name, new[] { rootPath }.Concat(colocated ?? Array.Empty<string>()).ToArray()));
            var layout = new LayoutDocument { Version = 2, BuildManifestHash = manifest };
            var serialized = new List<LayoutSerializedFile>
                { new LayoutSerializedFile { Index = 0, ID = "Library/unity default resources", IsBuiltIn = true } };
            var artifacts = new List<LayoutBinaryArtifact>();
            var files = new List<OutputFile>();
            foreach (var spec in all)
            {
                serialized.Add(new LayoutSerializedFile { Index = serialized.Count, ID = spec.Id, SourceAssets = spec.Sources, ContentHash = spec.Hash });
                var refs = new List<int>();
                if (spec.ResS != null)
                {
                    refs.Add(artifacts.Count + 1);
                    files.Add(new OutputFile { Name = spec.ResS + ".resS", Size = 20, Sha256 = "sha-" + spec.ResS });
                }
                artifacts.Add(new LayoutBinaryArtifact { Index = artifacts.Count, ContentHash = spec.Hash, Category = "contentfile", Size = 10, ArtifactReferences = refs.ToArray() });
                if (spec.ResS != null)
                    artifacts.Add(new LayoutBinaryArtifact { Index = artifacts.Count, ContentHash = spec.ResS, Category = "texture", Size = 20 });
                files.Add(new OutputFile { Name = spec.Hash + ".cf", Size = 10, Sha256 = spec.Sha ?? "sha-" + spec.Hash });
            }
            artifacts.Add(new LayoutBinaryArtifact { Index = artifacts.Count, ContentHash = manifest, Category = "manifest", Size = 5 });
            files.Add(new OutputFile { Name = manifest + ".json", Size = 5, Sha256 = "sha-" + manifest });
            files.Add(new OutputFile { Name = FileInventory.PointerName, Size = 32, Sha256 = "sha-ptr-" + name });
            if (extraFile != null) files.Add(new OutputFile { Name = extraFile, Size = 1, Sha256 = "sha-extra" });
            layout.SerializedFiles = serialized.ToArray();
            layout.BinaryArtifacts = artifacts.ToArray();
            layout.RootAssets = new[] { "root-object" };
            layout.LoadableObjectIds = new[] { new LayoutObjectId { ObjectIdHash = "root-object", AssetPath = rootPath, SerializedFile = serialized.Count - 1 } };
            return FileInventory.Read(name, layout, files, pointer ?? manifest, Kinds);
        }

        private static Spec SceneA => new Spec("sa.cfid", "h-sa", "Assets/A.unity");
        private static Spec SceneB => new Spec("sb.cfid", "h-sb", "Assets/B.unity");
        private static Spec OnlyB => new Spec("ob.cfid", "h-ob", "Assets/OnlyB.prefab");

        // S・A・B・AB の 4 build と AC3 の 6 対。edit で build ごとの spec を書き換える。
        private static Evidence RootSets(Action<string, List<Spec>>? edit = null)
        {
            var e = new Evidence();
            void Add(string name, params Spec[] own)
            {
                var specs = Common().Concat(own).ToList();
                edit?.Invoke(name, specs);
                e.Builds[name] = Build(name, specs);
            }
            Add("S");
            Add("A", SceneA);
            Add("B", SceneB, OnlyB);
            Add("AB", SceneA, SceneB, OnlyB);
            foreach (var pair in new[] { ("S", "A"), ("S", "B"), ("S", "AB"), ("A", "B"), ("A", "AB"), ("B", "AB") })
                e.RootSetPairs.Add(pair);
            return e;
        }

        private static string Judge(Evidence e) => Judgement.Judge(e, Kinds).Conclusion;

        [Test]
        public void AllSharedFilesKeepIdAndBytes_IsGo()
        {
            Assert.That(Judge(RootSets()), Is.EqualTo(nameof(Conclusion.Go)));
        }

        [Test]
        public void SharedShaderInAnotherId_IsConditionalAccept_AndNamesTheShader()
        {
            var e = RootSets((name, specs) =>
            {
                if (name != "B" && name != "AB") return;
                specs.RemoveAll(x => x.Id == "s.cfid");
                specs.Add(new Spec("s2.cfid", "h-s2", Shader));
            });
            var verdict = Judgement.Judge(e, Kinds);
            Assert.That(verdict.Conclusion, Is.EqualTo(nameof(Conclusion.ConditionalAccept)));
            Assert.That(verdict.Notes, Has.Some.Contains("SharedShader differed or moved: yes"));
        }

        [Test]
        public void SceneAInAnotherIdInAB_IsNoGo()
        {
            var e = RootSets((name, specs) =>
            {
                if (name != "AB") return;
                specs.RemoveAll(x => x.Id == "sa.cfid");
                specs.Add(new Spec("sa2.cfid", "h-sa", "Assets/A.unity"));
            });
            var verdict = Judgement.Judge(e, Kinds);
            Assert.That(verdict.Conclusion, Is.EqualTo(nameof(Conclusion.NoGo)));
            Assert.That(verdict.Reasons, Has.Some.StartsWith("N4 A-AB"));
        }

        [Test]
        public void SceneJoiningTheScriptIdOnTheRightOnly_IsNoGo()
        {
            // 左の build の同じ id が script だけでも、右の build でその id に Scene が加わって bytes が違えば、
            // script の差（CONDITIONAL ACCEPT 側）として読まない。
            var e = RootSets((name, specs) =>
            {
                if (name != "B") return;
                specs.RemoveAll(x => x.Id == "sc.cfid");
                specs.Add(new Spec("sc.cfid", "h-sc-b", "Assets/R.cs", "Assets/X.unity"));
            });
            var verdict = Judgement.Judge(e, Kinds);
            Assert.That(verdict.Conclusion, Is.EqualTo(nameof(Conclusion.NoGo)));
            Assert.That(verdict.Reasons, Has.Some.StartsWith("N4 A-B"));
            Assert.That(verdict.Reasons, Has.Some.StartsWith("N4 S-B"));
        }

        [Test]
        public void CoreAssetJoiningTheScriptIdOnTheRightOnly_IsDifferedAsCore()
        {
            var e = RootSets((name, specs) =>
            {
                if (name != "B") return;
                specs.RemoveAll(x => x.Id == "sc.cfid");
                specs.Add(new Spec("sc.cfid", "h-sc-b", "Assets/R.cs", Prefab));
            });
            var item = FileInventory.Compare(e.Builds["A"], e.Builds["B"]).Differed.Single(x => x.Id == "sc.cfid");
            Assert.That(item.Kind, Is.EqualTo(KindClassifier.Script));
            Assert.That(item.KindB, Is.EqualTo(KindClassifier.Prefab));
            Assert.That(item.SourceAssets, Is.EquivalentTo(new[] { "Assets/R.cs", Prefab }));
            Assert.That(Judgement.Judge(e, Kinds).Reasons, Has.Some.StartsWith("N2 A-B"));
        }

        [Test]
        public void SameNameButDifferentBytes_IsNotGo()
        {
            // (b) 名前は内容の hash だという主張を信じない。sha256 で比べる。
            var e = RootSets((name, specs) => { if (name == "B") specs.Single(x => x.Id == "m.cfid").Sha = "sha-other"; });
            var verdict = Judgement.Judge(e, Kinds);
            Assert.That(verdict.Conclusion, Is.EqualTo(nameof(Conclusion.NoGo)));
            Assert.That(verdict.Reasons, Has.Some.StartsWith("N2 S-B"));
        }

        [Test]
        public void IdOnOneSideOnly_IsNotCountedAsDiffered()
        {
            var e = RootSets();
            var pair = FileInventory.Compare(e.Builds["A"], e.Builds["B"]);
            Assert.That(pair.Differed, Is.Empty);
            Assert.That(pair.OneSided.Where(x => !x.Excluded).Select(x => x.Id), Is.EquivalentTo(new[] { "sa.cfid", "sb.cfid", "ob.cfid" }));
            Assert.That(pair.OneSided.Where(x => x.Excluded).Select(x => x.Id), Is.EquivalentTo(new[] { "root-A.cfid", "root-B.cfid" }));
        }

        [Test]
        public void SameRootSetForAAndB_IsUndecided()
        {
            // (e) 比べた 2 build が実は同じ組み合わせなら、別の root 集合の比較にならない。
            var e = RootSets((name, specs) =>
            {
                if (name != "B") return;
                specs.RemoveAll(x => x.Id == "sb.cfid" || x.Id == "ob.cfid");
                specs.Add(SceneA);
            });
            Assert.That(Judge(e), Is.EqualTo(nameof(Conclusion.Undecided)));
        }

        [Test]
        public void FileWithoutLayoutAttribution_IsUndecided()
        {
            // (f) 帰属を確かめていない file を「root か manifest だから」と除かない。
            var e = RootSets();
            e.Builds["B"] = Build("B", Common().Append(SceneB).Append(OnlyB), extraFile: "ffffffffffffffffffffffffffffffff.cf");
            var verdict = Judgement.Judge(e, Kinds);
            Assert.That(verdict.Conclusion, Is.EqualTo(nameof(Conclusion.Undecided)));
            Assert.That(verdict.Reasons, Has.Some.Contains("unknown file"));
        }

        [Test]
        public void RoundWhoseChangeDidNotReachTheBuild_IsUndecided()
        {
            // (g) 書き換えが build に届かなければ「参照元は変わらない」は自明になる。
            var e = RootSets();
            e.Builds["A-tex"] = Build("A", Common().Append(SceneA));
            e.Builds["A-tex"].Build = "A-tex";
            e.Rounds.Add(new Round("A", "A-tex", new[] { Tex }, false));
            Assert.That(Judge(e), Is.EqualTo(nameof(Conclusion.Undecided)));
        }

        [Test]
        public void RoundChangingOnlyTheRewrittenAsset_IsGo_AndRestoreRoundWithDiffIsNoGo()
        {
            var e = RootSets();
            var changed = Common();
            changed[0] = new Spec("t.cfid", "h-t9", Tex) { ResS = "h-tr9" };
            e.Builds["A-tex"] = Build("A-tex", changed.Append(SceneA));
            e.Rounds.Add(new Round("A", "A-tex", new[] { Tex }, false));
            Assert.That(Judge(e), Is.EqualTo(nameof(Conclusion.Go)));

            e.Builds["A-back"] = Build("A-back", changed.Append(SceneA));
            e.Rounds.Add(new Round("A", "A-back", Array.Empty<string>(), true));
            Assert.That(Judgement.Judge(e, Kinds).Reasons, Has.Some.StartsWith("N3 A-A-back"));
        }

        [Test]
        public void KindWithNoComparedFile_IsUndecided()
        {
            var e = RootSets((name, specs) => specs.RemoveAll(x => x.Id == "s.cfid" || x.Id == "ls.cfid"));
            var verdict = Judgement.Judge(e, Kinds);
            Assert.That(verdict.Conclusion, Is.EqualTo(nameof(Conclusion.Undecided)));
            Assert.That(verdict.Reasons, Has.Some.Contains("compared no shader file"));
        }

        [Test]
        public void SharedAssetInsideTheRootFile_IsNeitherGoNorConditionalAccept()
        {
            var e = RootSets();
            e.Builds["A"] = Build("A", Common().Append(SceneA), colocated: new[] { Prefab });
            Assert.That(e.Builds["A"].RootColocated, Is.True);
            Assert.That(e.Builds["A"].Files.Any(x => x.Excluded == "root"), Is.False);
            Assert.That(Judge(e), Is.EqualTo(nameof(Conclusion.Undecided)));
        }

        [Test]
        public void SameSourceDiffOnlyAcrossProcesses_IsNoGoWithNote()
        {
            var e = RootSets();
            e.Builds["A-clean"] = Build("A-clean", Common("h-sc2").Append(SceneA));
            e.SameSourcePairs.Add(("A", "A-clean", true));
            var verdict = Judgement.Judge(e, Kinds);
            Assert.That(verdict.Conclusion, Is.EqualTo(nameof(Conclusion.NoGo)));
            Assert.That(verdict.Notes, Has.Some.Contains("cross-process"));
        }

        [Test]
        public void Read_IgnoresVirtualDefaultResources_AndRejectsPointerMismatch()
        {
            var ok = Build("A", Common().Append(SceneA));
            Assert.That(ok.Unreadable, Is.Empty);
            Assert.That(ok.Unknown, Is.Empty);
            Assert.That(ok.Files.Select(x => x.Excluded).Where(x => x.Length != 0), Is.EquivalentTo(new[] { "root", "manifest", "pointer" }));
            Assert.That(ok.Files.Single(x => x.Id == "t.cfid|texture").Kind, Is.EqualTo(KindClassifier.Texture));
            var bad = Build("A", Common().Append(SceneA), pointer: "something-else");
            Assert.That(bad.Unreadable, Has.Some.StartsWith("pointer"));
        }
    }
}
