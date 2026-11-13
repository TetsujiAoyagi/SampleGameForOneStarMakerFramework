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
using S = OneStarMaker.Tests.Editor.Spikes.OverlayBuild.OverlaySpec;

namespace OneStarMaker.Tests.Editor.Spikes.OverlayBuild
{
    /// <summary>
    /// 実機の fixture（HANDOFF CONTENT_OVERLAY_BUILD §2.2）。S2-1 の OverlayFixture（commit a304963b）から、固定 GUID・marker の所有・
    /// 残りの回収を写した。各 asset を一度 Unity に stage へ作らせ、.meta の GUID を名前から決まる値に置き換えてから最終の path に書く。
    /// custom の script を持たず、版は texture の pixel と prefab の子の位置で読む。v2 は <see cref="OverlaySpec.ChangedInV2"/> の 5 file だけを
    /// 同じ GUID のまま書き換える。folder は marker で所有を示し、marker の無い folder には触れない。build の process の終わりに必ず消す。
    /// </summary>
    internal sealed class OverlayBuildFixture
    {
        private const string Stage = S.Folder + "/_stage";
        private const string MarkerName = "osm-ovb.marker";
        private const string MarkerText = "osm-ovb fixture; deleted at the end of the build process";

        private static string Disk(string assetPath) => Path.Combine(Path.GetDirectoryName(Application.dataPath)!, assetPath);

        public static bool Exists => Directory.Exists(Disk(S.Folder)) || File.Exists(Disk(S.Folder) + ".meta");

        /// <summary>前回の異常終了の残りを、自分の marker がある時だけ消す。marker が無ければ何も消さずに投げる。</summary>
        public static bool RemoveLeftovers()
        {
            if (!Exists) return false;
            var marker = Path.Combine(Disk(S.Folder), MarkerName);
            if (!File.Exists(marker) || File.ReadAllText(marker) != MarkerText)
                throw new InvalidOperationException(S.Folder + " exists without the harness marker; not touching it.");
            Delete();
            return true;
        }

        public static void Delete()
        {
            AssetDatabase.DeleteAsset(S.Folder);
            if (Directory.Exists(Disk(S.Folder))) Directory.Delete(Disk(S.Folder), true);
            if (File.Exists(Disk(S.Folder) + ".meta")) File.Delete(Disk(S.Folder) + ".meta");
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        }

        /// <summary>v1 の fixture を作る。Scene の作成は開いている Scene を置き換えるので、呼び手が先に Scene の構成を退避しておく。</summary>
        public void CreateV1()
        {
            AssetDatabase.CreateFolder("Assets", Path.GetFileName(S.Folder));
            File.WriteAllText(Path.Combine(Disk(S.Folder), MarkerName), MarkerText);
            AssetDatabase.CreateFolder(S.Folder, "_stage");

            File.WriteAllText(Disk(Stage + "/Shader.shader"), ShaderSource);
            AssetDatabase.ImportAsset(Stage + "/Shader.shader", ImportAssetOptions.ForceSynchronousImport);
            Commit(Stage + "/Shader.shader", S.Shader);
            foreach (var t in S.Textures) CreateTexture(t);
            var mats = S.MaterialOf.ToDictionary(x => x.Key, x => CreateMaterial(x.Value, x.Key));

            CreatePrefab(S.Prefab(S.InnerPrefab), () => WithChild(S.InnerPrefab, S.InnerProbe, S.V1));
            CreatePrefab(S.Prefab(S.OuterPrefab), () =>
            {
                var outer = new GameObject(S.OuterPrefab);
                Instance(S.Prefab(S.InnerPrefab)).transform.SetParent(outer.transform, false);
                return outer;
            });
            CreatePrefab(S.Prefab(S.EditedPrefab), () => WithChild(S.EditedPrefab, S.Probe, S.V1));
            CreatePrefab(S.RootPath(S.PrefabRoot), () => Body(S.PrefabRoot, S.PrefabRootSlots.Select(x => mats[x])));

            CreateScene(S.SceneDirect, () => Body(S.DirectObject, S.DirectSlots.Select(x => mats[x])));
            CreateScene(S.SceneInstance, () => Instance(S.Prefab(S.EditedPrefab)));
            CreateScene(S.SceneNested, () => Instance(S.Prefab(S.OuterPrefab)));
            CreateScene(S.SceneClean, () => Body(S.CleanObject, S.CleanSlots.Select(x => mats[x])));
            CreateScene(S.SceneUnlisted, () => Body(S.UnlistedObject, S.UnlistedSlots.Select(x => mats[x])));
            AssetDatabase.DeleteAsset(Stage);
        }

        /// <summary>source を v2 に変える。3 つの texture の pixel と、2 つの prefab の子の位置だけを、同じ GUID のまま書き換える。</summary>
        public void ApplyV2()
        {
            foreach (var t in new[] { S.EditedTex, S.UnlistedTex, S.CanaryTex })
            {
                // .meta（GUID と import の設定）を残し、pixel だけを v2 の単色にして import し直す。
                File.WriteAllBytes(Disk(S.Tex(t)), Png(S.Color(t, S.V2)));
                AssetDatabase.ImportAsset(S.Tex(t), ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            }
            MoveChild(S.Prefab(S.EditedPrefab), S.Probe);
            MoveChild(S.Prefab(S.InnerPrefab), S.InnerProbe);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        }

        /// <summary>fixture の source file（.meta を除く）ごとの sha256。前提 P2 の材料。</summary>
        public static Dictionary<string, string> SourceHashes() =>
            S.SourceFiles().ToDictionary(x => x, x => File.Exists(Disk(x)) ? S.Sha256(File.ReadAllBytes(Disk(x))) : "missing", StringComparer.Ordinal);

        /// <summary>
        /// 前提 P1 の材料。各観測点を source の root を通して読む。Scene の観測点は、Editor で開いた Scene の階層にある、
        /// round が Play で読むのと同じ子の値であり、prefab の asset の値ではない。Object の root（PrefabRoot）は asset を読む。
        /// </summary>
        public static List<PointValue> ReadPoints()
        {
            var values = new List<PointValue>();
            foreach (var group in S.Points.GroupBy(x => x.Root))
            {
                if (group.Key == S.PrefabRoot)
                {
                    var asset = AssetDatabase.LoadAssetAtPath<GameObject>(S.RootPath(S.PrefabRoot));
                    foreach (var p in group) values.Add(new PointValue { Id = p.Id, Path = S.RootPath(S.PrefabRoot), Value = Read(p, asset) });
                    continue;
                }
                var scene = EditorSceneManager.OpenScene(S.RootPath(group.Key), OpenSceneMode.Additive);
                try
                {
                    foreach (var p in group)
                    {
                        var go = Find(scene.GetRootGameObjects(), p.ObjectPath);
                        values.Add(new PointValue { Id = p.Id, Path = group.Key + ":" + (go == null ? "missing " + p.ObjectPath : HierarchyPath(go)), Value = Read(p, go) });
                    }
                }
                finally { EditorSceneManager.CloseScene(scene, true); }
            }
            return values;
        }

        // ───── 観測の部品（build の process と round の process が同じ読み方を使う） ─────

        /// <summary>Scene の root の GameObject から、階層 path（`a/b/c`）で引く。</summary>
        public static GameObject? Find(IEnumerable<GameObject> roots, string path)
        {
            var parts = path.Split(new[] { '/' }, 2);
            var root = roots.FirstOrDefault(x => x != null && x.name == parts[0]);
            if (root == null || parts.Length == 1) return root;
            var child = root.transform.Find(parts[1]);
            return child == null ? null : child.gameObject;
        }

        public static string HierarchyPath(GameObject go)
        {
            var names = new List<string>();
            for (var t = go.transform; t != null; t = t.parent) names.Insert(0, t.name);
            return string.Join("/", names);
        }

        // 観測点の値。texture は Renderer の slot の material の mainTexture の pixel、位置は子の localPosition。
        // 読めない時は理由の文字列にする（期待値と一致しないので未判定か不成立になる）。破棄されうる Object は == null で判定する。
        public static string Read(OverlaySpec.Point p, GameObject? go)
        {
            if (go == null) return "missing-object";
            if (!p.IsTexture)
            {
                var v = go.transform.localPosition;
                return S.PositionValue(new[] { v.x, v.y, v.z });
            }
            return Pixels(Texture(go, p.Slot));
        }

        public static Texture2D? Texture(GameObject? go, int slot)
        {
            if (go == null) return null;
            var renderer = go.GetComponent<Renderer>();
            if (renderer == null) return null;
            var materials = renderer.sharedMaterials;
            var m = slot < materials.Length ? materials[slot] : null;
            return m == null ? null : m.mainTexture as Texture2D;
        }

        public static string Pixels(Texture2D? tex)
        {
            if (tex == null) return "missing-texture";
            try
            {
                var pixels = tex.GetPixels32();
                var bytes = new byte[pixels.Length * 4];
                for (var i = 0; i < pixels.Length; i++)
                {
                    bytes[i * 4] = pixels[i].r; bytes[i * 4 + 1] = pixels[i].g; bytes[i * 4 + 2] = pixels[i].b; bytes[i * 4 + 3] = pixels[i].a;
                }
                return S.TextureValueOfBytes(bytes);
            }
            catch (Exception ex) { return "unreadable: " + ex.GetType().Name; }
        }

        public static string FixedGuid(string finalPath)
        {
            using var md5 = MD5.Create();
            var relative = finalPath.Substring(S.Folder.Length + 1);
            return string.Concat(md5.ComputeHash(Encoding.UTF8.GetBytes("osm-ovb/" + relative)).Select(x => x.ToString("x2")));
        }

        // ───── 生成の部品 ─────

        // 読める・圧縮しない・mip なしの単色の texture（版を pixel の sha256 で読む）。
        private static void CreateTexture(string name)
        {
            var stagePath = Stage + "/" + name + ".png";
            File.WriteAllBytes(Disk(stagePath), Png(S.Color(name, S.V1)));
            AssetDatabase.ImportAsset(stagePath, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(stagePath);
            importer.isReadable = true;
            importer.mipmapEnabled = false;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
            Commit(stagePath, S.Tex(name));
        }

        private static Material CreateMaterial(string name, string texture)
        {
            var mat = new Material(AssetDatabase.LoadAssetAtPath<Shader>(S.Shader));
            mat.SetTexture("_MainTex", AssetDatabase.LoadAssetAtPath<Texture2D>(S.Tex(texture)));
            var stagePath = Stage + "/" + name + ".mat";
            AssetDatabase.CreateAsset(mat, stagePath);
            AssetDatabase.SaveAssets();
            Commit(stagePath, S.Mat(name));
            return AssetDatabase.LoadAssetAtPath<Material>(S.Mat(name));
        }

        private static void CreatePrefab(string finalPath, Func<GameObject> build)
        {
            var stagePath = Stage + "/" + Path.GetFileName(finalPath);
            var go = build();
            try
            {
                PrefabUtility.SaveAsPrefabAsset(go, stagePath, out var saved);
                if (!saved) throw new InvalidOperationException("Prefab save failed: " + finalPath);
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
            Commit(stagePath, finalPath);
        }

        private static void CreateScene(string root, Func<GameObject> build)
        {
            var stagePath = Stage + "/" + root + ".unity";
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            build();
            if (!EditorSceneManager.SaveScene(scene, stagePath)) throw new InvalidOperationException("Scene save failed: " + root);
            // stage の Scene を消す前に別の空の Scene へ移る。
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Commit(stagePath, S.RootPath(root));
        }

        // prefab の root と、root ではない子 1 つ。版はその子の位置で読む。
        private static GameObject WithChild(string rootName, string child, string version)
        {
            var root = new GameObject(rootName);
            var c = new GameObject(child);
            c.transform.SetParent(root.transform, false);
            var p = S.Position(child, version);
            c.transform.localPosition = new Vector3(p[0], p[1], p[2]);
            return root;
        }

        // variant ではなく、override を足さず、unpack しない instance（S-2 の測る形）。
        private static GameObject Instance(string prefabPath)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (asset == null) throw new InvalidOperationException("missing " + prefabPath);
            return (GameObject)PrefabUtility.InstantiatePrefab(asset);
        }

        private static GameObject Body(string name, IEnumerable<Material> materials)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.GetComponent<MeshFilter>().sharedMesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            go.GetComponent<MeshRenderer>().sharedMaterials = materials.ToArray();
            return go;
        }

        private static void MoveChild(string prefabPath, string child)
        {
            var root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                var t = root.transform.Find(child);
                if (t == null) throw new InvalidOperationException(prefabPath + " has no " + child);
                var p = S.Position(child, S.V2);
                t.localPosition = new Vector3(p[0], p[1], p[2]);
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath, out var saved);
                if (!saved) throw new InvalidOperationException("Prefab save failed: " + prefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        // stage に Unity が書いた bytes を、固定の GUID の .meta と一緒に最終の path へ移す。
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

        private static byte[] Png(byte[] color)
        {
            var size = S.TextureSize;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            try
            {
                var pixels = Enumerable.Repeat(new Color32(color[0], color[1], color[2], color[3]), size * size).ToArray();
                texture.SetPixels32(pixels);
                texture.Apply();
                return texture.EncodeToPNG();
            }
            finally { UnityEngine.Object.DestroyImmediate(texture); }
        }

        // keyword も multi_compile も持たない自作 shader（URP の Lit を使わず、build の組み合わせで shader の file を変えない）。
        private const string ShaderSource = @"Shader ""OsmOvb/Shader""
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
