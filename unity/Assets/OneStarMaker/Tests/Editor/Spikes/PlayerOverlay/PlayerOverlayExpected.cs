#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;

// Windows の Player での上書きの登録のスパイク（docs/handoff/PLAYER_OVERLAY_REGISTRATION_SPIKE.md）の、fixture の定義と
// Editor の実測の数。期待値の出所はこの file だけで（HANDOFF §2 I-2 の (k)）、fixture の生成、runner が読む fixture の事実の file、
// 判定の写像がこの表を読む。Player の runner は期待値を持たず、key と名前だけを fixture の事実の file から受け取る。
// 実験 code であり、Phase D で git 履歴へ退避して削除する。
namespace OneStarMaker.Tests.Editor.Spikes.PlayerOverlay
{
    /// <summary>probe の component 1 つの期待（fixture が付けた順の 1 位置）。</summary>
    internal sealed class ProbeExpectation
    {
        public ProbeExpectation(string fullName, string tag, int value) { FullName = fullName; Tag = tag; Value = value; }
        public string FullName { get; }
        public string Tag { get; }
        public int Value { get; }
    }

    internal static class PlayerOverlayExpected
    {
        // probe の 3 型。runner は型を参照せず、この完全名と assembly 名の文字列で P4 を引く（HANDOFF §2 の P4、§4 の禁止）。
        public const string ProbeNamespace = "OneStarMaker.Tests.Spikes.PlayerOverlayProbes";
        public const string ProbeAssembly = "OneStarMaker.Spikes.PlayerOverlayProbes";
        public const string BaseOnly = ProbeNamespace + ".ProbeBaseOnly", OverlayOnly = ProbeNamespace + ".ProbeOverlayOnly", Both = ProbeNamespace + ".ProbeBoth";
        public static readonly string[] ProbeTypes = { BaseOnly, Both, OverlayOnly };

        // runner の型（Player の Scene に置く。fixture が MonoScript の GUID から引いて付ける）。
        public const string RunnerType = "OneStarMaker.Tests.Spikes.PlayerOverlayRunner.PlayerOverlayRunner";

        // 物の名前（root の logical key と、Player の中で列挙する Object.name）。shader の Object.name は shader の source の名前。
        public const string SceneKeep = "SceneKeep", SceneEdit = "SceneEdit", SharedPrefab = "SharedPrefab", EditedTex = "EditedTex",
            SharedTex = "SharedTex", SharedMat = "SharedMat", SharedShader = "SharedShader", EditedMat = "EditedMat", KeepTex = "KeepTex", KeepMat = "KeepMat";
        public const string ShaderObjectName = "OsmSpikePov/SharedShader";
        public const string Keeper = "Keeper", MarkerPrefix = "Marker_", MarkerV1 = "Marker_v1", MarkerV2 = "Marker_v2";
        public const string BootScene = "PovBoot", RunnerObject = "PovRunner";
        public const string V1 = "v1", V2 = "v2", NoVersion = "none";

        // build の名前（root の BuildIdentity と Unity の build name）。登録の handle の root の照合に使う（HANDOFF §2 の (c)）。
        public const string BaseDirectory = "base", OverlayDirectory = "overlay";
        public static string BuildName(string directory) => "pov-" + directory;

        // P0 の (1): 製品の AfterSceneLoad が、BeforeSceneLoad の失敗で何もしなかったことを示す 1 行
        // （AbstractApplicationInitializer の Error の文面の一部。HANDOFF §2 の P0）。
        public const string ProductStoppedLine = "BeforeSceneLoad が失敗したため AfterSceneLoad をスキップします";
        // runner が round の開始の印として Debug.Log に出す行の先頭（Player の log の「起動」の段の終わり）。
        public const string StartMark = "[OSM-POV] round-start";

        // Q3 の対象と、Editor の実測の数（実測の事実「同じ id で中身の違う file を持つ 2 つの directory の登録」と
        // 「6000.6.9f1 での上書きと登録の測り直し」。HANDOFF §1 の現況 (d)）。
        public static readonly string[] Counted = { SharedMat, SharedTex, SharedShader, EditedMat, EditedTex, SharedPrefab, KeepTex };
        public static readonly string[] CountedAtMostTwo = { SharedMat, SharedTex, SharedShader, EditedMat, EditedTex };
        public static int EditorMeasuredCount(string item) => CountedAtMostTwo.Contains(item) ? 2 : 1;
        // 1 つの directory だけを登録した時の数（fixture の定義。共通物も 1 つの file にしか無い）。
        public const int SingleDirectoryCount = 1;

        // Renderer の slot の定義（fixture が付けた順）。runner はこの名前で、参照をたどった EntityId を物に帰属させる。
        public static readonly IReadOnlyDictionary<string, string[]> Slots = new Dictionary<string, string[]>
        {
            [Keeper] = new[] { SharedMat, EditedMat, KeepMat },
            [MarkerV1] = new[] { SharedMat, EditedMat },
            [MarkerV2] = new[] { SharedMat, EditedMat },
            [SharedPrefab] = new[] { SharedMat },
        };
        public static readonly IReadOnlyDictionary<string, string> TextureOf = new Dictionary<string, string>
            { [SharedMat] = SharedTex, [EditedMat] = EditedTex, [KeepMat] = KeepTex };
        // SceneKeep の Renderer の、EditedMat の slot（X2 の観測点）。
        public const int KeepEditedSlot = 1;

        // 列挙（Resources.FindObjectsOfTypeAll）の型と Object.name。runner は文字列の型の種類で引く。
        public static string KindOf(string item) => item switch
        {
            SharedShader => "Shader",
            SharedPrefab => "GameObject",
            SharedMat or EditedMat or KeepMat => "Material",
            _ => "Texture2D",
        };
        public static string ObjectNameOf(string item) => item == SharedShader ? ShaderObjectName : item;

        // P0 の (2) で 0 件を確かめる、fixture の名前の物（Scene の root の GameObject を含む）。
        public static readonly string[] FixtureObjectNames = { SharedTex, SharedMat, ShaderObjectName, EditedTex, EditedMat, KeepTex, KeepMat, SharedPrefab, Keeper, MarkerV1, MarkerV2 };

        // EditedTex は v1 と v2 で違う単色の、読める小さな texture（HANDOFF §2 の「版」）。
        public const int EditedTexSize = 8;
        public static byte[] EditedColor(string version) => version == V1 ? new byte[] { 200, 40, 40, 255 } : new byte[] { 40, 40, 200, 255 };

        // probe の値は fixture の全ての component で互いに違える（HANDOFF §2 の「正しい class」）。並びは fixture が付けた順。
        public static IReadOnlyList<ProbeExpectation> Probes(string item, string version)
        {
            switch (item)
            {
                case SceneKeep: return new[] { new ProbeExpectation(BaseOnly, "keep.baseOnly", 101), new ProbeExpectation(Both, "keep.both", 102) };
                case SharedPrefab: return new[] { new ProbeExpectation(BaseOnly, "prefab.baseOnly", 401), new ProbeExpectation(Both, "prefab.both", 402) };
                case SceneEdit when version == V1: return new[] { new ProbeExpectation(BaseOnly, "edit.v1.baseOnly", 201), new ProbeExpectation(Both, "edit.v1.both", 202) };
                case SceneEdit when version == V2: return new[] { new ProbeExpectation(OverlayOnly, "edit.v2.overlayOnly", 301), new ProbeExpectation(Both, "edit.v2.both", 302) };
                default: throw new ArgumentException("No probes are defined for " + item + " " + version);
            }
        }

        // probe を持つ GameObject の名前（Scene の root か prefab）。
        public static string ProbeObjectName(string item, string version) => item switch
        {
            SceneKeep => Keeper,
            SceneEdit => MarkerName(version),
            _ => SharedPrefab,
        };

        public static string MarkerName(string version) => version == V1 ? MarkerV1 : MarkerV2;
        public static string VersionOfMarker(string objectName) => objectName == MarkerV1 ? V1 : objectName == MarkerV2 ? V2 : NoVersion;

        /// <summary>EditedTex の全 pixel を RGBA の順に並べた bytes の sha256（版の判定の基準）。</summary>
        public static string PixelSha(string version)
        {
            var color = EditedColor(version);
            var bytes = new byte[EditedTexSize * EditedTexSize * 4];
            for (var i = 0; i < bytes.Length; i++) bytes[i] = color[i % 4];
            return Sha256(bytes);
        }

        public static string VersionOfPixels(string sha) => sha == PixelSha(V1) ? V1 : sha == PixelSha(V2) ? V2 : NoVersion;

        public static string Sha256(byte[] bytes)
        {
            using var sha = SHA256.Create();
            return string.Concat(sha.ComputeHash(bytes).Select(x => x.ToString("x2")));
        }
    }
}
