#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using OneStarMaker.Runtime.BuildContent.Distribution;
using UnityEngine;

namespace SampleGame.DependOnAll.Editor.Build
{
    /// <summary>
    /// この host の取得元の列: 手元の publish 済みの directory 1 つ（batch と検証 Play が publish する <c>artifacts/bs2b/transport</c>）。
    /// 書いた本人の同じ disk なので申告の digest を認める（別の経路を求めても保証が増えない）。
    /// <para>
    /// 場所の上書きは任意で、利用者が <c>UserSettings/OSMContentOrigin.json</c>（git 管理外）を手で編集する（窓に欄を足さない）。
    /// 取得の開始のたびに file を読み、Editor の中に写しを持たない（Editor を開いたまま場所を変えられる）。
    /// 場所の directory が無くても拒まず、取得元が「無い」と答える。
    /// </para>
    /// </summary>
    internal static class ContentOriginSettings
    {
        /// <summary>結果と選んだ base の記録に出る、既定の directory の取得元の id。</summary>
        internal const string LocalPublishOriginId = "samplegame-local-publish";
        internal const string SettingsPath = "UserSettings/OSMContentOrigin.json";

        [Serializable]
        internal sealed class Settings
        {
            /// <summary>取得元の root の絶対 path。空なら既定の root。</summary>
            public string originRoot = "";
        }

        internal static string DefaultRoot =>
            Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "artifacts", "bs2b", "transport"));

        /// <summary>取得元の root と、既定に戻した理由（戻していなければ空）。file が無い・壊れている・相対 path の時は既定の root。</summary>
        internal static (string Root, string Note) ResolveRoot(string settingsPath, string defaultRoot)
        {
            if (!File.Exists(settingsPath)) return (defaultRoot, string.Empty);
            Settings? value;
            try { value = JsonUtility.FromJson<Settings>(File.ReadAllText(settingsPath)); }
            catch (Exception ex) when (ex is ArgumentException or IOException or UnauthorizedAccessException)
            {
                return (defaultRoot, settingsPath + " cannot be read (" + ex.Message + "); the default origin root is used.");
            }
            if (value == null || string.IsNullOrWhiteSpace(value.originRoot)) return (defaultRoot, string.Empty);
            if (!Path.IsPathRooted(value.originRoot))
                return (defaultRoot, "originRoot in " + settingsPath + " is not absolute; the default origin root is used.");
            return (Path.GetFullPath(value.originRoot), string.Empty);
        }

        internal static IReadOnlyList<ContentAcquisitionOrigin> Origins(string root) => new[]
        {
            new ContentAcquisitionOrigin(ContentOrigins.Directory(LocalPublishOriginId, root), ContentOriginTrust.AcceptDeclared),
        };
    }
}
