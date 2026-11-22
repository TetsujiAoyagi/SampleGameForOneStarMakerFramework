#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using E = OneStarMaker.Tests.Editor.Spikes.PlayerOverlay.PlayerOverlayExpected;

namespace OneStarMaker.Tests.Editor.Spikes.PlayerOverlay
{
    /// <summary>
    /// スパイクの fixture（HANDOFF §3。中身は S2-1 と同じ）と、runner だけを持つ Scene（PovBoot）。S2-1 の OverlayFixture
    /// （commit a304963b）から、固定 GUID・marker の所有・残りの回収を写した。各 asset を一度 Unity に作らせ、.meta の GUID を名前から
    /// 決まる値に置き換えてから最終の path に書く。v1 で作り、Player の build の後に <see cref="ApplyV2"/> で SceneEdit と EditedTex だけを
    /// 同じ GUID のまま書き換える。fixture の folder は marker で所有を示し、marker の無い folder には触れない。第 1 process の終わりに必ず消す。
    /// probe と runner の component は test 用 assembly の型を参照せず、.meta の GUID から引いた MonoScript の class で付ける。
    /// </summary>
    internal sealed class PlayerOverlayFixture
    {
        public const string Folder = "Assets/OsmSpikePov";
        private const string Stage = Folder + "/_stage";
        private const string MarkerName = "osm-spike-pov.marker";
        private const string MarkerText = "osm-spike-pov fixture; deleted at the end of the first process";
        private const int PatternSize = 16;

        // probe と runner の script の .meta の GUID（Tests/Spikes/PlayerOverlayProbes・PlayerOverlayRunner）。P1 は layout の一覧の path を
        // この GUID と結びつけて照合する（HANDOFF §2 の P1）。
        public static readonly IReadOnlyDictionary<string, string> ProbeGuids = new Dictionary<string, string>
        {
            [E.BaseOnly] = "191e328ef3604e52a06d1b452e92b2f4",
            [E.OverlayOnly] = "25b24de65b734b47aaa4ae13097ce84c",
            [E.Both] = "b87bbdf09410408a970b7703409e501c",
        };
        public const string RunnerGuid = "535097cdb15e4c38940cba109d765e81";

        public static string SharedShader => Folder + "/SharedShader.shader";
        public static string SharedTex => Folder + "/SharedTex.png";
        public static string SharedMat => Folder + "/SharedMat.mat";
        public static string EditedTex => Folder + "/EditedTex.png";
        public static string EditedMat => Folder + "/EditedMat.mat";
        public static string KeepTex => Folder + "/KeepTex.png";
        public static string KeepMat => Folder + "/KeepMat.mat";
        public static string SharedPrefab => Folder + "/SharedPrefab.prefab";
        public static string SceneKeep => Folder + "/SceneKeep.unity";
        public static string SceneEdit => Folder + "/SceneEdit.unity";
        public static string BootScene => Folder + "/" + E.BootScene + ".unity";
        // base の root と上書きの root は別の固定 GUID の別の asset にする（同じ asset を書き換えない。HANDOFF I-1 (e)）。
        public static string RootPath(string directory) => Folder + "/Root_" + directory + ".asset";

        private static string Disk(string assetPath) => Path.Combine(Path.GetDirectoryName(Application.dataPath)!, assetPath);

        public static bool Exists => Directory.Exists(Disk(Folder)) || File.Exists(Disk(Folder) + ".meta");

        /// <summary>前回の異常終了の残りを、自分の marker がある時だけ消す。marker が無ければ何も消さずに投げる（HANDOFF I-3 (f)）。</summary>
        public static bool RemoveLeftovers()
        {
            if (!Exists) return false;
            var marker = Path.Combine(Disk(Folder), MarkerName);
            if (!File.Exists(marker) || File.ReadAllText(marker) != MarkerText)
                throw new InvalidOperationException(Folder + " exists without the spike marker; not touching it.");
            Delete();
            return true;
        }

        public static void Delete()
        {
            AssetDatabase.DeleteAsset(Folder);
            if (Directory.Exists(Disk(Folder))) Directory.Delete(Disk(Folder), true);
            if (File.Exists(Disk(Folder) + ".meta")) File.Delete(Disk(Folder) + ".meta");
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        }

        /// <summary>fixture の folder の全 asset の GUID と path（Player の packed assets の照合に使う。runner の Scene を除く）。</summary>
        public static IReadOnlyDictionary<string, string> FixtureAssetGuids() =>
            AssetDatabase.FindAssets("", new[] { Folder }).Distinct().Select(g => (g, path: AssetDatabase.GUIDToAssetPath(g)))
                .Where(x => x.path != BootScene && !AssetDatabase.IsValidFolder(x.path)).ToDictionary(x => x.g, x => x.path);

        /// <summary>v1 の fixture と runner の Scene を作る。Scene の作成は開いている Scene を置き換えるので、未保存の Scene があれば始めない。</summary>
        public void CreateV1()
        {
            for (var i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
                if (UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty)
                    throw new InvalidOperationException("An open scene has unsaved changes; the fixture does not replace it.");
            AssetDatabase.CreateFolder("Assets", "OsmSpikePov");
            File.WriteAllText(Path.Combine(Disk(Folder), MarkerName), MarkerText);
            AssetDatabase.CreateFolder(Folder, "_stage");

            CreateTexture(SharedTex, Pattern(1));
            CreateTexture(KeepTex, Pattern(2));
            CreateTexture(EditedTex, Solid(E.V1));
            File.WriteAllText(Disk(Stage + "/SharedShader.shader"), ShaderSource);
            AssetDatabase.ImportAsset(Stage + "/SharedShader.shader", ImportAssetOptions.ForceSynchronousImport);
            Commit(Stage + "/SharedShader.shader", SharedShader);
            var shared = CreateMaterial(SharedMat, SharedTex);
            var edited = CreateMaterial(EditedMat, EditedTex);
            var keep = CreateMaterial(KeepMat, KeepTex);
            CreatePrefab(shared);
            // Scene は prefab instance を置かず、Scene 内の Renderer が material を直接参照する（HANDOFF §3）。
            WithScenes(() =>
            {
                CreateScene(SceneKeep, E.Keeper, new[] { shared, edited, keep }, E.Probes(E.SceneKeep, ""));
                CreateScene(SceneEdit, E.MarkerV1, new[] { shared, edited }, E.Probes(E.SceneEdit, E.V1));
                CreateBootScene();
            });
            AssetDatabase.DeleteAsset(Stage);
        }

        /// <summary>source を v2 に変える。SceneEdit と EditedTex を同じ GUID のまま書き換え、他の asset には触れない（HANDOFF §2 の base / 上書き）。</summary>
        public void ApplyV2()
        {
            // EditedTex は .meta（GUID と import の設定）を残し、pixel だけを v2 の単色にして import し直す。
            File.WriteAllBytes(Disk(EditedTex), Solid(E.V2));
            AssetDatabase.ImportAsset(EditedTex, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            AssetDatabase.CreateFolder(Folder, "_stage");
            var shared = AssetDatabase.LoadAssetAtPath<Material>(SharedMat);
            var edited = AssetDatabase.LoadAssetAtPath<Material>(EditedMat);
            WithScenes(() => CreateScene(SceneEdit, E.MarkerV2, new[] { shared, edited }, E.Probes(E.SceneEdit, E.V2)));
            AssetDatabase.DeleteAsset(Stage);
        }

        /// <summary>build の root（呼び出し側が作った ScriptableObject）を固定 GUID で置く。</summary>
        public string AddRoot(string directory, ScriptableObject root)
        {
            AssetDatabase.CreateFolder(Folder, "_stage");
            var stagePath = Stage + "/" + Path.GetFileName(RootPath(directory));
            AssetDatabase.CreateAsset(root, stagePath);
            AssetDatabase.SaveAssets();
            Commit(stagePath, RootPath(directory));
            AssetDatabase.DeleteAsset(Stage);
            return RootPath(directory);
        }

        public static string FixedGuid(string finalPath)
        {
            using var md5 = MD5.Create();
            var relative = finalPath.Substring(Folder.Length + 1);
            return string.Concat(md5.ComputeHash(Encoding.UTF8.GetBytes("osm-spike-pov/" + relative)).Select(x => x.ToString("x2")));
        }

        /// <summary>runner の型（.meta の GUID から引く。test の assembly は runner の assembly を参照しない）。</summary>
        public static Type RunnerType() => ScriptClass(RunnerGuid, E.RunnerType);

        /// <summary>GameObject 1 つに runner だけを持つ Scene の中身を、今開いている Scene に作る（PovBoot と Editor の対照が同じ形を使う）。</summary>
        public static GameObject AddRunnerObject() => new GameObject(E.RunnerObject, RunnerType());

        private static void WithScenes(Action create)
        {
            var previous = EditorSceneManager.GetSceneManagerSetup();
            try { create(); }
            finally
            {
                if (previous.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(previous);
                else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }
        }

        // runner だけを持つ Scene。名前は UICommon にしない（製品の動作確認の Player の経路に入らない。HANDOFF §1 の現況）。
        // fixture の asset を参照しない。
        private void CreateBootScene()
        {
            var stagePath = Stage + "/" + Path.GetFileName(BootScene);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            AddRunnerObject();
            if (!EditorSceneManager.SaveScene(scene, stagePath)) throw new InvalidOperationException("Scene save failed: " + BootScene);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Commit(stagePath, BootScene);
        }

        // 読める・圧縮しない・mip なしの texture。版と「壊れた」の判定に load 後の pixel の sha256 を使う（HANDOFF §3）。
        private void CreateTexture(string finalPath, byte[] png)
        {
            var stagePath = Stage + "/" + Path.GetFileName(finalPath);
            File.WriteAllBytes(Disk(stagePath), png);
            AssetDatabase.ImportAsset(stagePath, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(stagePath);
            importer.isReadable = true;
            importer.mipmapEnabled = false;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
            Commit(stagePath, finalPath);
        }

        private Material CreateMaterial(string finalPath, string texturePath)
        {
            var mat = new Material(AssetDatabase.LoadAssetAtPath<Shader>(SharedShader));
            mat.SetTexture("_MainTex", AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath));
            var stagePath = Stage + "/" + Path.GetFileName(finalPath);
            AssetDatabase.CreateAsset(mat, stagePath);
            AssetDatabase.SaveAssets();
            Commit(stagePath, finalPath);
            return AssetDatabase.LoadAssetAtPath<Material>(finalPath);
        }

        private void CreatePrefab(Material material)
        {
            var stagePath = Stage + "/SharedPrefab.prefab";
            var go = Body(E.SharedPrefab, new[] { material }, E.Probes(E.SharedPrefab, ""));
            try { PrefabUtility.SaveAsPrefabAsset(go, stagePath); }
            finally { UnityEngine.Object.DestroyImmediate(go); }
            Commit(stagePath, SharedPrefab);
        }

        private void CreateScene(string finalPath, string objectName, Material[] materials, IReadOnlyList<ProbeExpectation> probes)
        {
            var stagePath = Stage + "/" + Path.GetFileName(finalPath);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Body(objectName, materials, probes);
            if (!EditorSceneManager.SaveScene(scene, stagePath)) throw new InvalidOperationException("Scene save failed: " + finalPath);
            // stage の Scene を消す前に別の空の Scene へ移る。
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Commit(stagePath, finalPath);
        }

        // probe を持つ GameObject。MeshFilter・MeshRenderer の後に、期待の並び（BaseOnly か OverlayOnly、Both）の順で probe を付ける。
        // collider などの、fixture が意図しない組み込みの component を持たせない（base が使わない組み込みの型は Player に残らない）。
        private static GameObject Body(string name, Material[] materials, IReadOnlyList<ProbeExpectation> probes)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.GetComponent<MeshFilter>().sharedMesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            go.GetComponent<MeshRenderer>().sharedMaterials = materials;
            foreach (var p in probes)
            {
                var component = go.AddComponent(ScriptClass(ProbeGuids[p.FullName], p.FullName));
                var so = new SerializedObject(component);
                var tag = so.FindProperty("_tag");
                var value = so.FindProperty("_value");
                if (tag == null || value == null) throw new InvalidOperationException(p.FullName + " has no _tag or _value.");
                tag.stringValue = p.Tag;
                value.intValue = p.Value;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            return go;
        }

        private static Type ScriptClass(string guid, string fullName)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var script = AssetDatabase.LoadAssetAtPath<MonoScript>(path);
            // MonoScript は UnityEngine.Object なので == null で判定する。
            var type = script == null ? null : script.GetClass();
            if (type == null || type.FullName != fullName) throw new InvalidOperationException("Script " + fullName + " is not at " + path);
            return type;
        }

        // stage に Unity が書いた bytes を、固定の GUID の .meta と一緒に最終の path へ移す。既にある時は同じ GUID のまま上書きする（ApplyV2）。
        private static void Commit(string stagePath, string finalPath)
        {
            var bytes = File.ReadAllBytes(Disk(stagePath));
            var meta = File.ReadAllText(Disk(stagePath + ".meta"));
            AssetDatabase.DeleteAsset(stagePath);
            var guid = FixedGuid(finalPath);
            meta = Regex.Replace(meta, @"(?m)^guid: [0-9a-f]{32}", "guid: " + guid);
            File.WriteAllText(Disk(finalPath + ".meta"), meta);
            File.WriteAllBytes(Disk(finalPath), bytes);
            AssetDatabase.ImportAsset(finalPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            if (AssetDatabase.AssetPathToGUID(finalPath) != guid)
                throw new InvalidOperationException("Fixture GUID was not kept: " + finalPath);
        }

        private static byte[] Pattern(int variant) => Png(PatternSize, (x, y) => new Color32((byte)(x * 16), (byte)(y * 16 + variant * 64), (byte)(variant * 97), 255));

        private static byte[] Solid(string version)
        {
            var c = E.EditedColor(version);
            return Png(E.EditedTexSize, (x, y) => new Color32(c[0], c[1], c[2], c[3]));
        }

        private static byte[] Png(int size, Func<int, int, Color32> pixel)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            try
            {
                var pixels = new Color32[size * size];
                for (var y = 0; y < size; y++)
                for (var x = 0; x < size; x++)
                    pixels[y * size + x] = pixel(x, y);
                texture.SetPixels32(pixels);
                texture.Apply();
                return texture.EncodeToPNG();
            }
            finally { UnityEngine.Object.DestroyImmediate(texture); }
        }

        // keyword も multi_compile も持たない自作 shader（URP の Lit を使わず、shader の file を build の組み合わせで変えない）。
        private const string ShaderSource = @"Shader ""OsmSpikePov/SharedShader""
{
    Properties { _MainTex (""Texture"", 2D) = ""white"" {} }
    SubShader
    {
        Tags { ""RenderType"" = ""Opaque"" }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include ""UnityCG.cginc""
            sampler2D _MainTex;
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };
            v2f vert(appdata_base v) { v2f o; o.pos = UnityObjectToClipPos(v.vertex); o.uv = v.texcoord.xy; return o; }
            fixed4 frag(v2f i) : SV_Target { return tex2D(_MainTex, i.uv); }
            ENDCG
        }
    }
}
";
    }
}
