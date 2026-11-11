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

namespace OneStarMaker.Tests.Editor.Spikes.ContentFileIdentity
{
    [Serializable] internal sealed class SourceRecord
    {
        public string Path = "";
        public string Sha256 = "";
        public string Guid = "";
    }

    /// <summary>
    /// スパイクの fixture（HANDOFF §3）。別の Unity process で作り直しても source の bytes と GUID が同じになるよう、
    /// 各 asset を一度 Unity に作らせ、その text の fileID を class ごとの固定値に、.meta の GUID を名前から決まる値に
    /// 置き換えてから最終の path に書く（prefab と URP material の fileID は作るたびに乱数になることを Editor で確認した）。
    /// fixture の folder は marker で所有を示し、marker の無い folder には触れない。process の終わりに必ず消す。
    /// 製品の型（BuildContentRoot と Runtime の component）は参照しない。製品への依存は IdentityBuilds だけに置き（HANDOFF §3）、
    /// component は System.Type で、root は作り終えた ScriptableObject で受け取る。
    /// </summary>
    internal sealed class IdentityFixture
    {
        public const string Folder = "Assets/OsmSpikeCfi";
        private const string Stage = Folder + "/_stage";
        private const string MarkerName = "osm-spike-cfi.marker";
        private const string MarkerText = "osm-spike-cfi fixture; deleted at the end of each process";
        // Unity が固定値で書く fileID（Scene の設定、material と ScriptableObject の main object、SceneRoots）。
        private static readonly HashSet<string> FixedFileIds = new HashSet<string> { "1", "2", "3", "4", "2100000", "11400000", "9223372036854775807" };

        public string SharedTex => Folder + "/SharedTex.png";
        public string SharedShader => Folder + "/SharedShader.shader";
        public string SharedMat => Folder + "/SharedMat.mat";
        public string LitPlain => Folder + "/LitPlain.mat";
        public string LitEmissive => Folder + "/LitEmissive.mat";
        public string SharedPrefab => Folder + "/SharedPrefab.prefab";
        public string OnlyB => Folder + "/OnlyB.prefab";
        public string SceneA => Folder + "/SceneA.unity";
        public string SceneB => Folder + "/SceneB.unity";
        public static string RootPath(string build) => Folder + "/Root_" + build + ".asset";

        private readonly List<string> _assets = new List<string>();
        private readonly Dictionary<string, byte[]> _original = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        private readonly Type _sharedPrefabComponent;
        private readonly Type _onlyBComponent;

        // SharedPrefab と OnlyB に付ける component の型。互いに別の Runtime の既存 component を渡す（HANDOFF §3）。
        public IdentityFixture(Type sharedPrefabComponent, Type onlyBComponent)
        {
            if (!typeof(Component).IsAssignableFrom(sharedPrefabComponent) || !typeof(Component).IsAssignableFrom(onlyBComponent))
                throw new ArgumentException("Prefab components must derive from UnityEngine.Component.");
            if (sharedPrefabComponent == onlyBComponent)
                throw new ArgumentException("SharedPrefab and OnlyB must carry different components.");
            _sharedPrefabComponent = sharedPrefabComponent;
            _onlyBComponent = onlyBComponent;
        }

        public KindClassifier Classifier => new KindClassifier(SharedTex, SharedMat, LitPlain, SharedPrefab, SharedShader);

        private static string Disk(string assetPath) => Path.Combine(Path.GetDirectoryName(Application.dataPath)!, assetPath);

        // 前回の異常終了の残りを、自分の marker がある時だけ消す。消したかどうかを返す。
        public static bool RemoveLeftovers()
        {
            if (!Directory.Exists(Disk(Folder)) && !File.Exists(Disk(Folder) + ".meta")) return false;
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

        public static bool Exists => Directory.Exists(Disk(Folder)) || File.Exists(Disk(Folder) + ".meta");

        public void Create()
        {
            AssetDatabase.CreateFolder("Assets", "OsmSpikeCfi");
            File.WriteAllText(Path.Combine(Disk(Folder), MarkerName), MarkerText);
            AssetDatabase.CreateFolder(Folder, "_stage");

            File.WriteAllBytes(Disk(Stage + "/SharedTex.png"), Pattern(0));
            AssetDatabase.ImportAsset(Stage + "/SharedTex.png", ImportAssetOptions.ForceSynchronousImport);
            Commit("SharedTex.png", false);
            File.WriteAllText(Disk(Stage + "/SharedShader.shader"), ShaderSource);
            AssetDatabase.ImportAsset(Stage + "/SharedShader.shader", ImportAssetOptions.ForceSynchronousImport);
            Commit("SharedShader.shader", false);

            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(SharedTex);
            var shared = new Material(AssetDatabase.LoadAssetAtPath<Shader>(SharedShader));
            shared.SetTexture("_MainTex", tex);
            CreateNative(shared, "SharedMat.mat");
            var lit = Shader.Find("Universal Render Pipeline/Lit");
            if (lit == null) throw new InvalidOperationException("URP Lit shader is unavailable.");
            CreateNative(new Material(lit), "LitPlain.mat");
            // LitPlain と同じ shader に emission の keyword を足し、A と B で Lit の keyword の集合を変える。
            var emissive = new Material(lit);
            emissive.EnableKeyword("_EMISSION");
            emissive.SetColor("_EmissionColor", Color.green);
            // 新しい material の既定の GI flag は EmissiveIsBlack で、URP の material の検証がそれを見て _EMISSION を外す
            // （Editor で、保存した LitEmissive.mat の m_ValidKeywords が空になることを確かめた）。keyword を残すため emission を有効にする。
            emissive.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            emissive.SetTexture("_BaseMap", tex);
            CreateNative(emissive, "LitEmissive.mat");

            var mats = new[] { AssetDatabase.LoadAssetAtPath<Material>(SharedMat), AssetDatabase.LoadAssetAtPath<Material>(LitPlain) };
            CreatePrefab("SharedPrefab.prefab", mats, _sharedPrefabComponent);
            CreatePrefab("OnlyB.prefab", new[] { AssetDatabase.LoadAssetAtPath<Material>(LitEmissive) }, _onlyBComponent);
            // Scene は prefab instance を置かず、Scene 内の Renderer が共通の material を直接参照する（HANDOFF §3 の参照の形）。
            // Scene の作成は開いている Scene を置き換える。人の未保存の変更を消さないよう、dirty なら始めない。
            var previous = EditorSceneManager.GetSceneManagerSetup();
            for (var i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
                if (UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty)
                    throw new InvalidOperationException("An open scene has unsaved changes; the fixture does not replace it.");
            try
            {
                CreateScene("SceneA.unity", mats);
                CreateScene("SceneB.unity", mats);
            }
            finally
            {
                if (previous.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(previous);
                else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }
        }

        // build の root（呼び出し側が作った ScriptableObject）を fixture の folder に固定 GUID で置き、source の採取に含める。
        // root の中身は IdentityBuilds が決める。fixture は他の asset と同じ決定性の処理を通すだけ。
        public string AddRoot(string build, ScriptableObject root)
        {
            CreateNative(root, Path.GetFileName(RootPath(build)));
            return RootPath(build);
        }

        // AC5 の変更 round。書き換えた source asset の path を返す。Restore で作成直後の bytes に戻す。
        public string ChangeTexture()
        {
            File.WriteAllBytes(Disk(SharedTex), Pattern(1));
            AssetDatabase.ImportAsset(SharedTex, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            return SharedTex;
        }

        public string ChangeMaterial()
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(SharedMat);
            mat.SetColor("_Color", Color.red);
            EditorUtility.SetDirty(mat);
            AssetDatabase.SaveAssetIfDirty(mat);
            return SharedMat;
        }

        // prefab の component の値として Transform の位置を変える（Runtime の component は直列化する値を持たない）。
        public string ChangePrefab()
        {
            var root = PrefabUtility.LoadPrefabContents(SharedPrefab);
            try
            {
                root.transform.localPosition = new Vector3(1, 0, 0);
                PrefabUtility.SaveAsPrefabAsset(root, SharedPrefab);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            return SharedPrefab;
        }

        public void Restore(string assetPath)
        {
            File.WriteAllBytes(Disk(assetPath + ".meta"), _original[assetPath + ".meta"]);
            File.WriteAllBytes(Disk(assetPath), _original[assetPath]);
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
        }

        // source の file と .meta の sha256 と GUID。folder の .meta と marker は build の入力ではないので含めない。
        public SourceRecord[] CaptureSources() =>
            _assets.SelectMany(x => new[] { x, x + ".meta" }).OrderBy(x => x, StringComparer.Ordinal)
                .Select(x => new SourceRecord { Path = x, Sha256 = Sha256(File.ReadAllBytes(Disk(x))),
                    Guid = AssetDatabase.AssetPathToGUID(x.EndsWith(".meta", StringComparison.Ordinal) ? x.Substring(0, x.Length - 5) : x) })
                .ToArray();

        // 観測（合否にしない）: import 後の hash。再 import と再 build の差を切り分ける材料。
        public string[] ImportHashes() =>
            _assets.Select(x => x + "=" + AssetDatabase.GetAssetDependencyHash(x)).ToArray();

        public static string Sha256(byte[] bytes)
        {
            using var sha = SHA256.Create();
            return string.Concat(sha.ComputeHash(bytes).Select(x => x.ToString("x2")));
        }

        private void CreateNative(UnityEngine.Object asset, string name)
        {
            AssetDatabase.CreateAsset(asset, Stage + "/" + name);
            AssetDatabase.SaveAssets();
            Commit(name, true);
        }

        private void CreatePrefab(string name, Material[] materials, Type component)
        {
            var go = new GameObject(Path.GetFileNameWithoutExtension(name), typeof(MeshFilter), typeof(MeshRenderer));
            try
            {
                go.GetComponent<MeshFilter>().sharedMesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
                go.GetComponent<MeshRenderer>().sharedMaterials = materials;
                go.AddComponent(component);
                PrefabUtility.SaveAsPrefabAsset(go, Stage + "/" + name);
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
            Commit(name, true);
        }

        private void CreateScene(string name, Material[] materials)
        {
            // 追加の新規 Scene は untitled の Scene と並べられないので単独で開く。stage の Scene を消す前に別の空の Scene へ移る。
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var go = new GameObject(Path.GetFileNameWithoutExtension(name), typeof(MeshFilter), typeof(MeshRenderer));
            go.GetComponent<MeshFilter>().sharedMesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            go.GetComponent<MeshRenderer>().sharedMaterials = materials;
            if (!EditorSceneManager.SaveScene(scene, Stage + "/" + name)) throw new InvalidOperationException("Scene save failed: " + name);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Commit(name, true);
        }

        // stage に Unity が書いた bytes を、固定の fileID と GUID に置き換えて最終の path へ移す。
        private void Commit(string name, bool text)
        {
            var stagePath = Stage + "/" + name;
            var finalPath = Folder + "/" + name;
            var bytes = File.ReadAllBytes(Disk(stagePath));
            var meta = File.ReadAllText(Disk(stagePath + ".meta"));
            AssetDatabase.DeleteAsset(stagePath);
            if (text) bytes = Encoding.UTF8.GetBytes(FixFileIds(Encoding.UTF8.GetString(bytes), name));
            var guid = FixedGuid(name);
            meta = Regex.Replace(meta, @"(?m)^guid: [0-9a-f]{32}", "guid: " + guid);
            File.WriteAllText(Disk(finalPath + ".meta"), meta);
            File.WriteAllBytes(Disk(finalPath), bytes);
            AssetDatabase.ImportAsset(finalPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            if (AssetDatabase.AssetPathToGUID(finalPath) != guid)
                throw new InvalidOperationException("Fixture GUID was not kept: " + finalPath);
            _assets.Add(finalPath);
            _original[finalPath] = File.ReadAllBytes(Disk(finalPath));
            _original[finalPath + ".meta"] = File.ReadAllBytes(Disk(finalPath + ".meta"));
        }

        // 乱数の fileID を class id から決まる値にする。同じ class が 2 つあると決まらないので fixture の不備として止める。
        private static string FixFileIds(string yaml, string name)
        {
            var anchors = Regex.Matches(yaml, @"(?m)^--- !u!(\d+) &(-?\d+)").Cast<Match>()
                .Select(m => (cls: m.Groups[1].Value, id: m.Groups[2].Value)).Where(x => !FixedFileIds.Contains(x.id)).ToArray();
            foreach (var group in anchors.GroupBy(x => x.cls))
                if (group.Count() > 1) throw new InvalidOperationException(name + ": class " + group.Key + " appears twice; fileIDs cannot be fixed.");
            foreach (var (cls, id) in anchors)
                yaml = Regex.Replace(yaml, @"(?<![0-9-])" + Regex.Escape(id) + @"(?![0-9])", cls + "001");
            return SortDocuments(yaml);
        }

        // Unity は object の文書を元の乱数の fileID の順に書く。fileID を固定値に置き換えても文書の順は乱数のままなので、
        // 固定後の fileID の昇順に並べ直す（URP material の AssetVersion が Material の前に来るか後に来るかが
        // process ごとに変わり、別 process で LitEmissive.mat の bytes が一致しなかった。HANDOFF §6 の EC-01）。
        // 文書の順は読み込みの意味を変えない。
        private static string SortDocuments(string yaml)
        {
            var starts = Regex.Matches(yaml, @"(?m)^--- !u!\d+ &(-?\d+)").Cast<Match>().ToArray();
            if (starts.Length < 2) return yaml;
            var header = yaml.Substring(0, starts[0].Index);
            var documents = starts.Select((m, i) => (id: long.Parse(m.Groups[1].Value),
                text: yaml.Substring(m.Index, (i + 1 < starts.Length ? starts[i + 1].Index : yaml.Length) - m.Index))).ToArray();
            if (documents.Select(x => x.id).Distinct().Count() != documents.Length)
                throw new InvalidOperationException("Duplicate fileIDs after fixing; documents cannot be ordered.");
            // 末尾の文書が改行で終わらないと、並べ替えで次の文書の区切りと同じ行になる。
            return header + string.Concat(documents.OrderBy(x => x.id).Select(x => x.text.EndsWith("\n", StringComparison.Ordinal) ? x.text : x.text + "\n"));
        }

        private static string FixedGuid(string name)
        {
            using var md5 = MD5.Create();
            return string.Concat(md5.ComputeHash(Encoding.UTF8.GetBytes("osm-spike-cfi/" + name)).Select(x => x.ToString("x2")));
        }

        // .resS が出る大きさの決定的な模様。variant 1 は AC5 の texture の変更。
        private static byte[] Pattern(int variant)
        {
            var texture = new Texture2D(256, 256, TextureFormat.RGBA32, false);
            try
            {
                var pixels = new Color32[256 * 256];
                for (var y = 0; y < 256; y++)
                for (var x = 0; x < 256; x++)
                    pixels[y * 256 + x] = new Color32((byte)(x ^ y), (byte)(x + variant * 97), (byte)(y * 3), 255);
                texture.SetPixels32(pixels);
                texture.Apply();
                return texture.EncodeToPNG();
            }
            finally { UnityEngine.Object.DestroyImmediate(texture); }
        }

        // keyword も multi_compile も持たない自作 shader（variant の無い shader）。
        private const string ShaderSource = @"Shader ""OsmSpikeCfi/SharedShader""
{
    Properties { _MainTex (""Texture"", 2D) = ""white"" {} _Color (""Color"", Color) = (1,1,1,1) }
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
            float4 _Color;
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };
            v2f vert(appdata_base v) { v2f o; o.pos = UnityObjectToClipPos(v.vertex); o.uv = v.texcoord.xy; return o; }
            fixed4 frag(v2f i) : SV_Target { return tex2D(_MainTex, i.uv) * _Color; }
            ENDCG
        }
    }
}
";
    }
}
