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

namespace OneStarMaker.Tests.Editor.Spikes.SharedFileRegistration
{
    /// <summary>
    /// スパイクの fixture（HANDOFF §3）。S2-0 の IdentityFixture（commit f632707a）から、固定 GUID・固定 fileID・文書の並べ替え・
    /// marker の所有を写して縮めた。各 asset を一度 Unity に作らせ、text の fileID を class ごとの固定値に、.meta の GUID を
    /// 名前から決まる値に置き換えてから最終の path に書く（prefab の fileID は作るたびに乱数になる。S2-0 で確認済み）。
    /// fixture の folder は marker で所有を示し、marker の無い folder には触れない。第 1 process の終わりに必ず消す。
    /// 自作の script を持たせない（両 directory の script をまとめた file を揃えるため。HANDOFF §3、自己点検 S-3）。
    /// 製品の型は参照しない。root は呼び出し側（RegistrationBuilds）が作った ScriptableObject を受け取る。
    /// </summary>
    internal sealed class RegistrationFixture
    {
        public const string Folder = "Assets/OsmSpikeSfr";
        private const string Stage = Folder + "/_stage";
        private const string MarkerName = "osm-spike-sfr.marker";
        private const string MarkerText = "osm-spike-sfr fixture; deleted at the end of the first process";
        // Unity が固定値で書く fileID（Scene の設定、material と ScriptableObject の main object、SceneRoots）。
        private static readonly HashSet<string> FixedFileIds = new HashSet<string> { "1", "2", "3", "4", "2100000", "11400000", "9223372036854775807" };
        // SharedTex は Profiler で差が見え、.resS に出る大きさ。読める texture にし、圧縮しない（pixel の sha256 を load 後に採る）。
        public const int SharedTexSize = 1024;
        private const int TwinSize = 256;

        // asset の名前（Object.name）。Play 中の列挙はこの名前で数える。正の対照は 2 つとも "Twin"。
        public const string SharedTexName = "SharedTex", SharedMatName = "SharedMat", SharedPrefabName = "SharedPrefab",
            SharedShaderName = "SharedShader", TwinName = "Twin", SceneAName = "SceneA", SceneBName = "SceneB";
        public static readonly string[] CountedNames = { SharedTexName, SharedMatName, SharedPrefabName, SharedShaderName, TwinName };

        public static string SharedTex => Folder + "/SharedTex.png";
        public static string SharedShader => Folder + "/SharedShader.shader";
        public static string SharedMat => Folder + "/SharedMat.mat";
        public static string SharedPrefab => Folder + "/SharedPrefab.prefab";
        public static string SceneA => Folder + "/SceneA.unity";
        public static string SceneB => Folder + "/SceneB.unity";
        // 同じ asset 名にするため別の subfolder に置く（自己点検 S-4）。
        public static string Twin(string directory) => Folder + "/" + directory + "/Twin.png";
        public static string RootPath(string directory) => Folder + "/Root_" + directory + ".asset";

        private static string Disk(string assetPath) => Path.Combine(Path.GetDirectoryName(Application.dataPath)!, assetPath);

        public static bool Exists => Directory.Exists(Disk(Folder)) || File.Exists(Disk(Folder) + ".meta");

        /// <summary>前回の異常終了の残りを、自分の marker がある時だけ消す。marker が無ければ何も消さずに投げる（AC5）。</summary>
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

        /// <summary>fixture の asset を作る。Scene の作成は開いている Scene を置き換えるので、未保存の Scene があれば始めない。</summary>
        public void Create()
        {
            for (var i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
                if (UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty)
                    throw new InvalidOperationException("An open scene has unsaved changes; the fixture does not replace it.");
            AssetDatabase.CreateFolder("Assets", "OsmSpikeSfr");
            File.WriteAllText(Path.Combine(Disk(Folder), MarkerName), MarkerText);
            AssetDatabase.CreateFolder(Folder, "_stage");
            AssetDatabase.CreateFolder(Folder, "A");
            AssetDatabase.CreateFolder(Folder, "B");

            CreateTexture("SharedTex.png", Folder + "/SharedTex.png", SharedTexSize, 0, true);
            CreateTexture("Twin.png", Twin("A"), TwinSize, 1, false);
            CreateTexture("Twin.png", Twin("B"), TwinSize, 2, false);
            File.WriteAllText(Disk(Stage + "/SharedShader.shader"), ShaderSource);
            AssetDatabase.ImportAsset(Stage + "/SharedShader.shader", ImportAssetOptions.ForceSynchronousImport);
            Commit(Stage + "/SharedShader.shader", SharedShader, false);

            var mat = new Material(AssetDatabase.LoadAssetAtPath<Shader>(SharedShader));
            mat.SetTexture("_MainTex", AssetDatabase.LoadAssetAtPath<Texture2D>(SharedTex));
            CreateNative(mat, SharedMat);
            var shared = AssetDatabase.LoadAssetAtPath<Material>(SharedMat);
            CreatePrefab(shared);
            // Scene は prefab instance を置かず、Scene 内の Renderer が SharedMat を直接参照する（HANDOFF §3）。
            var previous = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                CreateScene(SceneAName, SceneA, shared);
                CreateScene(SceneBName, SceneB, shared);
            }
            finally
            {
                if (previous.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(previous);
                else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }
            AssetDatabase.DeleteAsset(Stage);
        }

        /// <summary>build の root（呼び出し側が作った ScriptableObject）を固定 GUID で置く。</summary>
        public string AddRoot(string directory, ScriptableObject root)
        {
            AssetDatabase.CreateFolder(Folder, "_stage");
            CreateNative(root, RootPath(directory));
            AssetDatabase.DeleteAsset(Stage);
            return RootPath(directory);
        }

        public static string FixedGuid(string finalPath)
        {
            using var md5 = MD5.Create();
            var relative = finalPath.Substring(Folder.Length + 1);
            return string.Concat(md5.ComputeHash(Encoding.UTF8.GetBytes("osm-spike-sfr/" + relative)).Select(x => x.ToString("x2")));
        }

        public static string Sha256(byte[] bytes)
        {
            using var sha = SHA256.Create();
            return string.Concat(sha.ComputeHash(bytes).Select(x => x.ToString("x2")));
        }

        private void CreateTexture(string stageName, string finalPath, int size, int variant, bool readable)
        {
            var stagePath = Stage + "/" + stageName;
            File.WriteAllBytes(Disk(stagePath), Pattern(size, variant));
            AssetDatabase.ImportAsset(stagePath, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(stagePath);
            importer.isReadable = readable;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
            Commit(stagePath, finalPath, false);
        }

        private void CreateNative(UnityEngine.Object asset, string finalPath)
        {
            var stagePath = Stage + "/" + Path.GetFileName(finalPath);
            AssetDatabase.CreateAsset(asset, stagePath);
            AssetDatabase.SaveAssets();
            Commit(stagePath, finalPath, true);
        }

        private void CreatePrefab(Material material)
        {
            var stagePath = Stage + "/SharedPrefab.prefab";
            var go = new GameObject(SharedPrefabName, typeof(MeshFilter), typeof(MeshRenderer));
            try
            {
                go.GetComponent<MeshFilter>().sharedMesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
                go.GetComponent<MeshRenderer>().sharedMaterial = material;
                PrefabUtility.SaveAsPrefabAsset(go, stagePath);
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
            Commit(stagePath, SharedPrefab, true);
        }

        private void CreateScene(string name, string finalPath, Material material)
        {
            var stagePath = Stage + "/" + name + ".unity";
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var go = new GameObject(name + "Renderer", typeof(MeshFilter), typeof(MeshRenderer));
            go.GetComponent<MeshFilter>().sharedMesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            go.GetComponent<MeshRenderer>().sharedMaterial = material;
            if (!EditorSceneManager.SaveScene(scene, stagePath)) throw new InvalidOperationException("Scene save failed: " + name);
            // stage の Scene を消す前に別の空の Scene へ移る。
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Commit(stagePath, finalPath, true);
        }

        // stage に Unity が書いた bytes を、固定の fileID と GUID に置き換えて最終の path へ移す。
        private static void Commit(string stagePath, string finalPath, bool text)
        {
            var bytes = File.ReadAllBytes(Disk(stagePath));
            var meta = File.ReadAllText(Disk(stagePath + ".meta"));
            AssetDatabase.DeleteAsset(stagePath);
            if (text) bytes = Encoding.UTF8.GetBytes(FixFileIds(Encoding.UTF8.GetString(bytes), finalPath));
            var guid = FixedGuid(finalPath);
            meta = Regex.Replace(meta, @"(?m)^guid: [0-9a-f]{32}", "guid: " + guid);
            File.WriteAllText(Disk(finalPath + ".meta"), meta);
            File.WriteAllBytes(Disk(finalPath), bytes);
            AssetDatabase.ImportAsset(finalPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            if (AssetDatabase.AssetPathToGUID(finalPath) != guid)
                throw new InvalidOperationException("Fixture GUID was not kept: " + finalPath);
        }

        // 乱数の fileID を class id から決まる値にし、文書を固定後の fileID の昇順に並べる（S2-0 と同じ。同じ class が 2 つあれば止める）。
        private static string FixFileIds(string yaml, string name)
        {
            var anchors = Regex.Matches(yaml, @"(?m)^--- !u!(\d+) &(-?\d+)").Cast<Match>()
                .Select(m => (cls: m.Groups[1].Value, id: m.Groups[2].Value)).Where(x => !FixedFileIds.Contains(x.id)).ToArray();
            foreach (var group in anchors.GroupBy(x => x.cls))
                if (group.Count() > 1) throw new InvalidOperationException(name + ": class " + group.Key + " appears twice; fileIDs cannot be fixed.");
            foreach (var (cls, id) in anchors)
                yaml = Regex.Replace(yaml, @"(?<![0-9-])" + Regex.Escape(id) + @"(?![0-9])", cls + "001");
            var starts = Regex.Matches(yaml, @"(?m)^--- !u!\d+ &(-?\d+)").Cast<Match>().ToArray();
            if (starts.Length < 2) return yaml;
            var header = yaml.Substring(0, starts[0].Index);
            var documents = starts.Select((m, i) => (id: long.Parse(m.Groups[1].Value),
                text: yaml.Substring(m.Index, (i + 1 < starts.Length ? starts[i + 1].Index : yaml.Length) - m.Index))).ToArray();
            return header + string.Concat(documents.OrderBy(x => x.id).Select(x => x.text.EndsWith("\n", StringComparison.Ordinal) ? x.text : x.text + "\n"));
        }

        // 決定的な模様。variant で pixel を変える（正の対照の TwinA と TwinB は file 名が一致しないよう pixel が違う。外部レビュー E-1）。
        private static byte[] Pattern(int size, int variant)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            try
            {
                var pixels = new Color32[size * size];
                for (var y = 0; y < size; y++)
                for (var x = 0; x < size; x++)
                    pixels[y * size + x] = new Color32((byte)(x ^ y), (byte)(x + variant * 97), (byte)(y * 3 + variant * 31), 255);
                texture.SetPixels32(pixels);
                texture.Apply();
                return texture.EncodeToPNG();
            }
            finally { UnityEngine.Object.DestroyImmediate(texture); }
        }

        // keyword も multi_compile も持たない自作 shader（URP の Lit を使わず、shader の file を build の組み合わせで変えない）。
        private const string ShaderSource = @"Shader ""OsmSpikeSfr/SharedShader""
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
