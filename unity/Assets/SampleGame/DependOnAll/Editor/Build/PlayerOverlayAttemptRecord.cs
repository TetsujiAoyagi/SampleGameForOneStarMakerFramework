#nullable enable

using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace SampleGame.DependOnAll.Editor.Build
{
    /// <summary>
    /// 入口が Player の process を作るか止めるかを決めた時点の結果（<c>last-attempt.json</c> の <c>outcome</c>。名前で書く）。
    /// 判定はこの値で合否を読むので、名前を変えない。
    /// </summary>
    internal enum PlayerOverlayOutcome
    {
        /// <summary>上書きつきで Player の process を作った。</summary>
        Launched,
        /// <summary>content の編集が無い（作り直せない root だけの時を含む）ので、上書きの key なしで Player の process を作った。照合の失敗の縮退ではない。</summary>
        LaunchedWithoutOverlay,
        NoPlayablePlayer,
        BaseNotInstalled,
        BaseInvalid,
        /// <summary>Player に焼き込む content 以外の入力が手元と違う（D11）。</summary>
        PlayerInputDiffers,
        /// <summary>上書きの directory が build の時の digest と違う（起動の直前の取り直し）。</summary>
        OverlayChangedAfterBuild,
        Cancelled,
        EntryFailed,
    }

    /// <summary>process を作った後の観測の状態（<c>startup</c>）。<see cref="Pending"/> から 1 回だけ書き換わる。</summary>
    internal enum PlayerOverlayStartup { NotStarted, Pending, Observed, Failed, TimedOut }

    /// <summary><c>artifacts/playable/overlays/last-attempt.json</c>。入口・観測・判定の test が読み書きする。</summary>
    [Serializable]
    internal sealed class PlayerOverlayAttempt
    {
        public string outcome = "";
        public string detail = "";
        public string startup = nameof(PlayerOverlayStartup.NotStarted);
        public string startupReason = "";
        public string playerIdentity = "";
        /// <summary>上書きつきの起動か（観測が登録の行を求めるか）。</summary>
        public bool hasOverlay;
        public string overlayIdentity = "";
        public string overlayPath = "";
        public string overlayDigest = "";
        /// <summary>計画が上書きに入れた root の logical key。</summary>
        public string[] overlayRoots = Array.Empty<string>();
        /// <summary>再利用の段の結果（<c>Reused</c> か <c>Built</c>。上書きが無ければ空）。</summary>
        public string reuse = "";
        /// <summary>子 process の環境に置いた <c>SAMPLEGAME_*</c> の名前（入口が実際に組み立てた環境から取る）。</summary>
        public string[] environmentNames = Array.Empty<string>();
        public int pid;
        /// <summary>process の開始時刻（UTC の ticks の 10 進）。pid の再利用と見分ける。</summary>
        public string processStartUtcTicks = "";
        public string playerLogPath = "";
        /// <summary>起動の時刻（UTC の ticks の 10 進）。<c>TimedOut</c> の経過はここから数える。</summary>
        public string launchedUtcTicks = "";

        internal PlayerOverlayStartup Startup =>
            Enum.TryParse<PlayerOverlayStartup>(startup, out var value) ? value : PlayerOverlayStartup.NotStarted;
    }

    /// <summary><c>artifacts/playable/overlays/&lt;上書きの identity&gt;/overlay-record.json</c>。build の時の上書きの digest。</summary>
    [Serializable]
    internal sealed class PlayerOverlayBuildRecord
    {
        public string identity = "";
        public string contentPath = "";
        public string digest = "";
    }

    /// <summary>
    /// 入口の 2 つの記録の置き場と読み書き。どちらも別名に書いてから 1 回の操作で置き換える（途中で失敗しても前の記録は残り、
    /// 読み手が半端な file を読まない）。入口の記録は Editor の再起動をまたいで残る（git は <c>artifacts/</c> を無視する）。
    /// </summary>
    internal static class PlayerOverlayRecords
    {
        internal const string LastAttemptFileName = "last-attempt.json";
        internal const string BuildRecordFileName = "overlay-record.json";

        /// <summary><c>artifacts/playable/overlays</c>（入口の記録と Player の log の置き場）。</summary>
        internal static string OverlaysRoot(string playableArtifactsRoot) => Path.Combine(playableArtifactsRoot, "overlays");

        internal static string LastAttemptPath(string playableArtifactsRoot) => Path.Combine(OverlaysRoot(playableArtifactsRoot), LastAttemptFileName);

        internal static string BuildRecordPath(string playableArtifactsRoot, string overlayIdentity)
        {
            if (string.IsNullOrWhiteSpace(overlayIdentity) || overlayIdentity.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0
                || overlayIdentity.Contains(".."))
                throw new ArgumentException("The overlay identity is not a valid folder name: " + overlayIdentity, nameof(overlayIdentity));
            return Path.Combine(OverlaysRoot(playableArtifactsRoot), overlayIdentity, BuildRecordFileName);
        }

        /// <summary>無い・読めない時は null。</summary>
        internal static PlayerOverlayAttempt? ReadAttempt(string path) => Read<PlayerOverlayAttempt>(path);

        /// <summary>無い・読めない・identity が違う時は null（入口は build し直す）。</summary>
        internal static PlayerOverlayBuildRecord? ReadBuildRecord(string path, string overlayIdentity)
        {
            var record = Read<PlayerOverlayBuildRecord>(path);
            return record != null && string.Equals(record.identity, overlayIdentity, StringComparison.Ordinal) && record.digest.Length != 0
                ? record : null;
        }

        internal static void WriteAttempt(string path, PlayerOverlayAttempt attempt) => Write(path, JsonUtility.ToJson(attempt, true));

        internal static void WriteBuildRecord(string path, PlayerOverlayBuildRecord record) => Write(path, JsonUtility.ToJson(record, true));

        private static T? Read<T>(string path) where T : class
        {
            try
            {
                return File.Exists(path) ? JsonUtility.FromJson<T>(File.ReadAllText(path)) : null;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
            {
                return null;
            }
        }

        private static void Write(string path, string json)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var temp = path + "." + Guid.NewGuid().ToString("N").Substring(0, 8) + ".tmp";
            File.WriteAllText(temp, json, new UTF8Encoding(false));
            try
            {
                if (File.Exists(path)) File.Replace(temp, path, null);
                else File.Move(temp, path);
            }
            finally
            {
                if (File.Exists(temp)) File.Delete(temp);
            }
        }
    }
}
