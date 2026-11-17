#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace OneStarMaker.Tests.Editor.Spikes.NoStamp
{
    /// <summary>
    /// 実 build の harness の仕様（HANDOFF CONTENT_PREFAB_STAMP_REMOVAL §5 の fixture と build の列）。期待値は fixture の source の値から導き、
    /// 実装の出力から作らない。Phase D で harness ごと削除する。
    /// </summary>
    internal static class NstSpec
    {
        public const string Folder = "Assets/OsmNst";
        public const string Inner = "Inner", Outer = "Outer", Shared = "Shared", Variant = "SharedVariant", Model = "Model";
        public const string SOuter = "SOuter", SShared1 = "SShared1", SShared2 = "SShared2", SVariant = "SVariant", SModel = "SModel", SPlain = "SPlain";
        public const string V1 = "v1", V2 = "v2";
        public const string ContentSet = "nst", OverlaySet = "nst_ovl";
        // 開いている未保存の Scene（B3o）に付ける名前。出力の Scene の file にこの文字列が無いことを見る（disk の版を焼き込む）。
        public const string UnsavedName = "OsmNstUnsavedRename";
        // 撤去した印の接頭辞（出力に出ないことを見る。製品の code は参照しない）。
        public const string RemovedStampPrefix = "osm-prefab-stamp-";
        public static readonly string[] Prefabs = { Inner, Outer, Shared };
        public static readonly string[] Scenes = { SOuter, SShared1, SShared2, SVariant, SModel, SPlain };

        public static string Prefab(string name) => Folder + "/" + name + ".prefab";
        public static string ModelPath => Folder + "/" + Model + ".obj";
        public static string Scene(string name) => Folder + "/" + name + ".unity";
        public static string Child(string prefab) => prefab + "Child";
        // 編集の対象の asset（prefab の file と model の file）。variant の file は編集しない。
        public static string[] EditableAssets => Prefabs.Select(Prefab).Append(ModelPath).ToArray();
        public static string[] AllAssets => Prefabs.Select(Prefab).Append(Prefab(Variant)).Append(ModelPath).ToArray();

        // 子の位置。prefab ごとに v1 / v2 で重ならない float の列にする。
        public static float[] Position(string prefab, string version) => (prefab, version) switch
        {
            (Inner, V1) => new[] { 1f, 2f, 3f }, (Inner, V2) => new[] { 4f, 5f, 6f },
            (Outer, V1) => new[] { 7f, 8f, 9f }, (Outer, V2) => new[] { 10f, 11f, 12f },
            (Shared, V1) => new[] { 13f, 14f, 15f }, (Shared, V2) => new[] { 16f, 17f, 18f },
            _ => throw new ArgumentException(prefab + "/" + version),
        };

        // model の識別値: 版だけが持つ object（group）の名前。Scene に焼き込まれる子の GameObject の名前として出る見込み。
        public static string ModelName(string version) => "ModelOnly" + version.ToUpperInvariant();

        // `g` の行で group に分ける（`o` の行は Unity の importer が名前を捨てて default になる。2026-11-17 に Editor で確認）。
        public static string ModelText(string version) =>
            "g ModelCommon\nv 0 0 0\nv 1 0 0\nv 0 1 0\nf 1 2 3\ng " + ModelName(version) + "\nv 0 0 1\nv 1 0 1\nv 0 1 1\nf 4 5 6\n";

        // Scene ごとに焼き込む prefab（入れ子と variant の基を含む）。model は SModel だけ。
        public static string[] BakedPrefabs(string scene) => scene switch
        {
            SOuter => new[] { Inner, Outer },
            SShared1 or SShared2 or SVariant => new[] { Shared },
            _ => Array.Empty<string>(),
        };
        public static bool BakesModel(string scene) => scene == SModel;

        internal sealed class Step
        {
            public Step(string name, string inner, string outer, string shared, string model, bool full,
                bool overlay = false, bool cleanCache = false, bool openUnsaved = false)
            {
                Name = name; Full = full; Overlay = overlay; CleanCache = cleanCache; OpenUnsaved = openUnsaved;
                Versions = new Dictionary<string, string> { [Inner] = inner, [Outer] = outer, [Shared] = shared, [Model] = model };
            }
            public string Name { get; }
            public IReadOnlyDictionary<string, string> Versions { get; }
            public bool Full { get; }
            public bool Overlay { get; }
            public bool CleanCache { get; }
            // B3o: SShared1 を開いて未保存の変更を持たせた状態で build する。
            public bool OpenUnsaved { get; }
        }

        // B6 は製品の上書きのコンストラクタで build する（そのコンストラクタは mode を Full に固定する）。
        public static readonly Step[] Steps =
        {
            new Step("B1", V1, V1, V1, V1, full: true),
            new Step("B2", V2, V1, V1, V1, full: false),
            new Step("B3", V2, V1, V1, V1, full: false),
            new Step("B3o", V2, V1, V1, V1, full: false, openUnsaved: true),
            new Step("B4", V1, V1, V1, V1, full: false),
            new Step("B4b", V1, V1, V2, V2, full: false),
            new Step("B5", V2, V2, V2, V2, full: true),
            new Step("B6", V2, V1, V2, V2, full: true, overlay: true),
            new Step("B5p", V2, V2, V2, V2, full: true, cleanCache: true),
        };

        // B6 の上書きで逆引きに渡す編集（B5 から B6 で戻した prefab）。
        public static readonly string[] OverlayEdits = { Prefab(Outer) };

        public static string Sha256(byte[] bytes)
        {
            using var sha = SHA256.Create();
            return string.Concat(sha.ComputeHash(bytes).Select(x => x.ToString("x2")));
        }

        // float32 little-endian の 3 つを並べたバイト列（.cf の中で子の位置を探す形。S2-2 と S2-2b-0 と同じ観測）。
        public static byte[] FloatBytes(float[] xyz) => xyz.SelectMany(BitConverter.GetBytes).ToArray();
    }

    /// <summary>fixture（`Assets/OsmNst`）。marker で所有を示し、marker の無い folder には触れない。build の process の終わりに必ず消す。</summary>
    internal static class NstFixture
    {
        private const string MarkerName = "osm-nst.marker";
        private const string MarkerText = "osm-nst fixture; deleted at the end of the build process";

        public static string Disk(string assetPath) => Path.Combine(Path.GetDirectoryName(Application.dataPath)!, assetPath);
        public static bool Exists => Directory.Exists(Disk(NstSpec.Folder)) || File.Exists(Disk(NstSpec.Folder) + ".meta");

        public static bool RemoveLeftovers()
        {
            if (!Exists) return false;
            var marker = Path.Combine(Disk(NstSpec.Folder), MarkerName);
            if (!File.Exists(marker) || File.ReadAllText(marker) != MarkerText)
                throw new InvalidOperationException(NstSpec.Folder + " exists without the harness marker; not touching it.");
            Delete();
            return true;
        }

        public static void Delete()
        {
            AssetDatabase.DeleteAsset(NstSpec.Folder);
            if (Directory.Exists(Disk(NstSpec.Folder))) Directory.Delete(Disk(NstSpec.Folder), true);
            if (File.Exists(Disk(NstSpec.Folder) + ".meta")) File.Delete(Disk(NstSpec.Folder) + ".meta");
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        }

        /// <summary>全て v1 の fixture を作る。Scene の作成は開いている Scene を置き換えるので、呼び手が先に構成を退避する。</summary>
        public static void Create()
        {
            AssetDatabase.CreateFolder("Assets", Path.GetFileName(NstSpec.Folder));
            File.WriteAllText(Path.Combine(Disk(NstSpec.Folder), MarkerName), MarkerText);
            // marker は File で書くので Unity はまだ import しておらず `.meta` が無い。ここで import しないと、
            // 最初の build の製品の Refresh が marker の `.meta` を足し、build の前後の inventory の差（AC5）に出る。
            var markerAsset = NstSpec.Folder + "/" + MarkerName;
            AssetDatabase.ImportAsset(markerAsset, ImportAssetOptions.ForceSynchronousImport);
            if (!File.Exists(Disk(markerAsset) + ".meta"))
                throw new InvalidOperationException("The fixture marker was not imported: " + markerAsset);
            WriteModel(NstSpec.V1);
            SavePrefab(NstSpec.Inner, WithChild(NstSpec.Inner));
            var outer = WithChild(NstSpec.Outer);
            Instance(NstSpec.Prefab(NstSpec.Inner)).transform.SetParent(outer.transform, false);
            SavePrefab(NstSpec.Outer, outer);
            SavePrefab(NstSpec.Shared, WithChild(NstSpec.Shared));
            // variant は override を持たない（基の instance をそのまま別の asset として保存する）。
            SavePrefab(NstSpec.Variant, Instance(NstSpec.Prefab(NstSpec.Shared)));
            SaveScene(NstSpec.SOuter, () => Instance(NstSpec.Prefab(NstSpec.Outer)));
            SaveScene(NstSpec.SShared1, () => Instance(NstSpec.Prefab(NstSpec.Shared)));
            SaveScene(NstSpec.SShared2, () => Instance(NstSpec.Prefab(NstSpec.Shared)));
            SaveScene(NstSpec.SVariant, () => Instance(NstSpec.Prefab(NstSpec.Variant)));
            SaveScene(NstSpec.SModel, () => (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(NstSpec.ModelPath)));
            SaveScene(NstSpec.SPlain, () => new GameObject("Plain"));
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }

        /// <summary>prefab の子の位置と model の版を、その step の版にする（今の版と違う asset だけを書き直し、import させる）。</summary>
        public static List<string> Apply(IReadOnlyDictionary<string, string> versions)
        {
            var changed = new List<string>();
            foreach (var prefab in NstSpec.Prefabs)
            {
                var path = NstSpec.Prefab(prefab);
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var child = root.transform.Find(NstSpec.Child(prefab));
                    if (child == null) throw new InvalidOperationException(path + " has no " + NstSpec.Child(prefab));
                    var p = NstSpec.Position(prefab, versions[prefab]);
                    var target = new Vector3(p[0], p[1], p[2]);
                    if (child.localPosition == target) continue;
                    child.localPosition = target;
                    PrefabUtility.SaveAsPrefabAsset(root, path, out var saved);
                    if (!saved) throw new InvalidOperationException("Prefab save failed: " + path);
                    changed.Add(path);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            if (File.ReadAllText(Disk(NstSpec.ModelPath)) != NstSpec.ModelText(versions[NstSpec.Model]))
            {
                WriteModel(versions[NstSpec.Model]);
                changed.Add(NstSpec.ModelPath);
            }
            return changed;
        }

        // 書いた後に import させる（製品の snapshot の前提。HANDOFF §5）。
        private static void WriteModel(string version)
        {
            File.WriteAllText(Disk(NstSpec.ModelPath), NstSpec.ModelText(version));
            AssetDatabase.ImportAsset(NstSpec.ModelPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
        }

        private static GameObject WithChild(string prefab)
        {
            var root = new GameObject(prefab);
            var child = new GameObject(NstSpec.Child(prefab));
            child.transform.SetParent(root.transform, false);
            var p = NstSpec.Position(prefab, NstSpec.V1);
            child.transform.localPosition = new Vector3(p[0], p[1], p[2]);
            return root;
        }

        private static GameObject Instance(string assetPath) =>
            (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(assetPath));

        private static void SavePrefab(string name, GameObject root)
        {
            try
            {
                PrefabUtility.SaveAsPrefabAsset(root, NstSpec.Prefab(name), out var saved);
                if (!saved) throw new InvalidOperationException("Prefab save failed: " + name);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        private static void SaveScene(string name, Func<GameObject> build)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            build();
            if (!EditorSceneManager.SaveScene(scene, NstSpec.Scene(name))) throw new InvalidOperationException("Scene save failed: " + name);
        }
    }

    // ───── build の process が書く構造化された事実（build-facts.json）。判定の process が読む。 ─────

    [Serializable]
    internal sealed class NstSceneState
    {
        public string scene = "";
        public string fileSha256 = "";
        public long fileTicks;
        public string metaSha256 = "";
        public long metaTicks;
        public string userData = "";
    }

    // 帰属の確認（HANDOFF §2.5）の入力: asset の file の sha256 と import が見ている dependency hash。
    [Serializable]
    internal sealed class NstAssetState
    {
        public string path = "";
        public string sha256 = "";
        public string dependencyHash = "";
    }

    [Serializable]
    internal sealed class NstStepFacts
    {
        public string step = "";
        public string mode = "";
        public bool overlay;
        public List<string> changedAssets = new List<string>();
        public List<NstAssetState> writtenAssets = new List<NstAssetState>();
        public List<NstAssetState> assets = new List<NstAssetState>();
        public List<string> roots = new List<string>();
        public string identity = "";
        public bool success;
        public string summary = "";
        public string contentPath = "";
        public string historyCopy = "";
        public string historyName = "";
        public List<NstSceneState> before = new List<NstSceneState>();
        public List<NstSceneState> after = new List<NstSceneState>();
        public string inventoryBefore = "";
        public string inventoryAfter = "";
        public List<string> inventoryDiff = new List<string>();
        public bool generatedFolderAfter;
        public string stampDirectoryAfter = "";
        // B3o の観測（変更を捨てる前）。
        public bool unsavedOpened;
        public bool unsavedLoadedAfter;
        public bool unsavedDirtyAfter;
        public string unsavedRootNameAfter = "";
    }

    [Serializable]
    internal sealed class NstBuildFacts
    {
        public string runId = "";
        public string head = "";
        public string unity = "";
        public string stampDirectoryStart = "";
        public List<NstStepFacts> steps = new List<NstStepFacts>();
        public List<string> problems = new List<string>();
        public bool fixtureDeleted;
        public bool finished;
    }
}
