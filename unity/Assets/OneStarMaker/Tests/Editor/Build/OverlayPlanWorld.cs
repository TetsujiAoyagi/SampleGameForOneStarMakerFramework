#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using OneStarMaker.Build.Selection;
using OneStarMaker.Editor.Build.Content;
using OneStarMaker.Editor.Build.Materialization;
using OneStarMaker.Runtime.BuildContent.Distribution;
using UnityEngine;

namespace OneStarMaker.Tests.Editor.Build
{
    /// <summary>
    /// 上書きの計画の単体 test の世界: 手元の file（path → 中身）と GUID の対応、base の manifest の bytes、手元の plan と snapshot を組む。
    /// test は編集の一覧を渡さず、この世界の file と GUID の対応だけを変える。Unity の AssetDatabase も disk も使わない。
    /// </summary>
    internal sealed class OverlayPlanWorld
    {
        internal const string ContentSet = "base-set";
        internal const string Revision = "20261121T000000000Z-0123456789abcdef0123456789abcdef";
        internal const string OverlaySet = "overlay-set";
        private readonly Dictionary<string, string> _files = new(StringComparer.Ordinal);
        private readonly Dictionary<string, string> _guids = new(StringComparer.Ordinal);
        private readonly List<RootDef> _roots = new();

        /// <summary>
        /// 真なら file の書き換えを project の disk にも写す（coordinator は build の前後に disk の閉包の file を hash するので）。
        /// path は Unity が import しない folder（名前が <c>~</c> で終わる）の下に置く。
        /// </summary>
        internal bool Mirror { get; set; }

        /// <summary>Unity の依存の答えに欠けた依存の path を含めるか（既定は含めない。6000.6.9f1 の観測）。</summary>
        internal bool ReportMissingDependencies { get; set; }

        // file 名から決まる GUID（小文字 16 進 32 字。閉包の記録の読み口が受け付ける形）。
        internal static string Guid(string name) => Sha(name).Substring(0, 32);

        internal string Put(string path, string content = "v1")
        {
            var guid = Guid(System.IO.Path.GetFileName(path));
            _guids[guid] = path;
            Write(path, content);
            Write(path + ".meta", "guid: " + guid);
            return guid;
        }

        internal void Edit(string path, string content) => Write(path, content);
        internal void EditMeta(string path) => Write(path + ".meta", _files[path + ".meta"] + "\nimporter: changed");

        private void Write(string path, string content)
        {
            _files[path] = content;
            if (!Mirror) return;
            var full = System.IO.Path.Combine(ProjectRoot, path);
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(full)!);
            System.IO.File.WriteAllText(full, content);
        }

        private void Delete(string path)
        {
            _files.Remove(path);
            if (Mirror) System.IO.File.Delete(System.IO.Path.Combine(ProjectRoot, path));
        }

        internal static string ProjectRoot => System.IO.Path.GetDirectoryName(Application.dataPath)!;

        // `.meta` と一緒に退かす（GUID は手元で解決しなくなる）。
        internal (string Content, string Meta) Remove(string path)
        {
            var removed = (_files[path], _files[path + ".meta"]);
            Delete(path);
            Delete(path + ".meta");
            return removed;
        }

        internal void Move(string from, string to)
        {
            var guid = _guids.Single(x => x.Value == from).Key;
            var (content, meta) = Remove(from);
            _guids[guid] = to;
            Write(to, content);
            Write(to + ".meta", meta);
        }

        internal string PathOf(string name) => _guids[Guid(name)];

        /// <summary>root を宣言する（stable key は logical key と表現から作る）。deps は root が参照する file の path（GUID で持つ）。</summary>
        internal RootDef Root(string logicalKey, string representation, string rootPath, params string[] deps)
        {
            var def = new RootDef("sk-" + logicalKey + "-" + representation, logicalKey, representation, Guid(System.IO.Path.GetFileName(rootPath)),
                deps.Select(x => Guid(System.IO.Path.GetFileName(x))).ToArray());
            _roots.Add(def);
            return def;
        }

        internal IContentOverlayLocalSource Local => new FakeLocal(this);

        // ─── base の manifest ───

        /// <summary>今の世界の file から base の manifest を作る（base の build の時点の状態として呼ぶ）。</summary>
        internal BaseManifest Publish(IEnumerable<RootDef> baseRoots, int recordVersion = 1)
        {
            var roots = baseRoots.ToArray();
            var closures = roots.Select(x => new ContentSourceClosureRootRecord(x.LogicalKey, KindOf(_guids[x.RootGuid]), x.Representation, x.RootGuid,
                new[] { x.RootGuid }.Concat(x.Deps).Distinct().Select(g => new BuildDependencyEntry(g, _guids[g])))).ToArray();
            var sources = closures.SelectMany(x => x.Assets).Select(x => x.Path).Distinct()
                .SelectMany(x => new[] { x, x + ".meta" })
                .Select(x => new ContentSourceFile { path = x, sha256 = Sha(_files[x]) }).OrderBy(x => x.path, StringComparer.Ordinal).ToArray();
            var record = ContentSourceClosureRecord.Create(closures);
            if (recordVersion != 1) record = new ContentSourceClosure { version = recordVersion };
            var manifest = new ContentTransportManifest
            {
                version = 1, product = "OneStarMaker", contentSet = ContentSet, revision = Revision, target = BuildContentCoordinator.TargetName,
                unityVersion = Application.unityVersion, rootSchemaVersion = 2, playerConfigSchemaVersion = 1,
                files = new[] { new ContentTransportFile { path = "content.bin", size = 1, sha256 = new string('0', 64) } },
                sourceFiles = sources,
                index = new ContentTransportIndex
                {
                    version = 1,
                    entries = closures.Select(x => new ContentTransportIndexEntry { logicalKey = x.LogicalKey, kind = x.Kind, representation = x.Representation }).ToArray()
                },
                sourceClosure = record,
            };
            return new BaseManifest(manifest);
        }

        // ─── 手元の選択 ───

        /// <summary>
        /// 今の世界から手元の plan と snapshot を作る。selected と excluded の root の閉包は Unity と同じく手元で解決する file だけからなり、
        /// root の file が無ければ欠けの記録になる。<see cref="ReportMissingDependencies"/> が真なら、欠けた依存も欠けの記録になる。
        /// </summary>
        internal (BuildPlan Plan, BuildMaterializationSnapshot Snapshot) Select(IEnumerable<RootDef> selected, IEnumerable<RootDef>? excluded = null)
        {
            var sel = selected.ToArray();
            var exc = (excluded ?? Array.Empty<RootDef>()).ToArray();
            var candidates = new List<BuildContentCandidate>();
            var closures = new List<BuildDependencySnapshot>();
            var absences = new List<BuildMaterializationAbsence>();
            var tags = new Dictionary<string, IReadOnlyList<BuildTag>>(StringComparer.Ordinal);
            foreach (var def in sel.Concat(exc))
            {
                var candidate = new BuildContentCandidate(def.StableKey, def.LogicalKey, def.RootGuid, new BuildProvenance("fixture", def.LogicalKey));
                candidates.Add(candidate);
                tags[def.StableKey] = new[] { new BuildTag("Representation", def.Representation) };
                var rootPath = Resolve(def.RootGuid);
                if (rootPath == null)
                {
                    absences.Add(new BuildMaterializationAbsence(def.StableKey, def.LogicalKey, def.RootGuid, null,
                        BuildMaterializationAbsenceReason.RootUnresolved, Array.Empty<string>()));
                    continue;
                }
                var missing = def.Deps.Where(g => Resolve(g) == null).Select(g => _guids[g]).ToArray();
                if (missing.Length != 0 && ReportMissingDependencies)
                {
                    absences.Add(new BuildMaterializationAbsence(def.StableKey, def.LogicalKey, def.RootGuid, rootPath,
                        BuildMaterializationAbsenceReason.DependencyMissing, missing));
                    continue;
                }
                closures.Add(new BuildDependencySnapshot(def.RootGuid, rootPath,
                    new[] { def.RootGuid }.Concat(def.Deps).Distinct().Where(g => Resolve(g) != null).Select(g => new BuildDependencyEntry(g, _guids[g]))));
            }
            var snapshot = new BuildMaterializationSnapshot(candidates, tags, Array.Empty<BuildContentRequirement>(), closures,
                Array.Empty<BuildGraphNode>(), absences);
            var exclusions = exc.Select(def => new BuildContentExclusion(candidates.Single(x => x.StableKey == def.StableKey),
                BuildExclusionReasonCode.RepresentationNotPreferred, "Representation", def.Representation));
            var plan = new BuildPlan(new BuildRequest(Array.Empty<BuildTag>()), candidates.Where(x => sel.Any(s => s.StableKey == x.StableKey)), exclusions);
            return (plan, snapshot);
        }

        internal ContentOverlayPlan Plan(BaseManifest baseManifest, IEnumerable<RootDef> selected, IEnumerable<RootDef>? excluded = null,
            IEnumerable<string>? supplied = null, string artifactsRoot = "unused-artifacts")
        {
            var (plan, snapshot) = Select(selected, excluded);
            return PlanWith(baseManifest, plan, snapshot, supplied, artifactsRoot);
        }

        internal ContentOverlayPlan PlanWith(BaseManifest baseManifest, BuildPlan plan, BuildMaterializationSnapshot snapshot,
            IEnumerable<string>? supplied = null, string artifactsRoot = "unused-artifacts")
            => ContentOverlayPlanner.Plan(baseManifest.Bytes, baseManifest.Digest, ContentSet, Revision, BuildContentCoordinator.TargetName,
                ContentOverlayPlanner.Input(plan, snapshot, supplied ?? Array.Empty<string>(), OverlaySet, artifactsRoot, null, null), Local);

        private string? Resolve(string guid) => _guids.TryGetValue(guid, out var path) && _files.ContainsKey(path) ? path : null;
        internal static string KindOf(string path) => path.EndsWith(".unity", StringComparison.OrdinalIgnoreCase) ? "Scene" : "Object";
        internal static string Sha(string content) => Sha(Encoding.UTF8.GetBytes(content));
        internal static string Sha(byte[] bytes)
        {
            using var hash = System.Security.Cryptography.SHA256.Create();
            return string.Concat(hash.ComputeHash(bytes).Select(x => x.ToString("x2")));
        }

        internal sealed class RootDef
        {
            internal RootDef(string stableKey, string logicalKey, string representation, string rootGuid, string[] deps)
            { StableKey = stableKey; LogicalKey = logicalKey; Representation = representation; RootGuid = rootGuid; Deps = deps; }
            public string StableKey { get; }
            public string LogicalKey { get; }
            public string Representation { get; }
            public string RootGuid { get; }
            public string[] Deps { get; }
        }

        internal sealed class BaseManifest
        {
            internal BaseManifest(ContentTransportManifest manifest) { Manifest = manifest; Refresh(); }
            public ContentTransportManifest Manifest { get; }
            public byte[] Bytes { get; private set; } = Array.Empty<byte>();
            public string Digest { get; private set; } = "";
            public string Json => Encoding.UTF8.GetString(Bytes);
            // Manifest を書き換えた後に bytes と digest を作り直す。
            public BaseManifest Refresh()
            {
                Bytes = new UTF8Encoding(false).GetBytes(JsonUtility.ToJson(Manifest, true));
                Digest = Sha(Bytes);
                return this;
            }
            public void SetBytes(byte[] bytes, string digest) { Bytes = bytes; Digest = digest; }
        }

        private sealed class FakeLocal : IContentOverlayLocalSource
        {
            private readonly OverlayPlanWorld _world;
            public FakeLocal(OverlayPlanWorld world) => _world = world;
            public string? ResolveGuid(string guid) => _world.Resolve(guid);
            public string? Sha256(string path) => _world._files.TryGetValue(path, out var content) ? Sha(content) : null;
        }
    }
}
