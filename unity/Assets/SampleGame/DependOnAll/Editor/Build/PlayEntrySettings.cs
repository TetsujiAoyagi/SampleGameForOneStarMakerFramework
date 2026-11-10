#nullable enable

using System;
using System.Linq;
using UnityEditor;

namespace SampleGame.DependOnAll.Editor.Build
{
    /// <summary>
    /// Play 入口の RunPurpose と経路の永続。保存先は <c>UserSettings/</c>（git 管理外）で、Editor を閉じても残る。
    /// 保存値が無い・読めない時は host の既定（spring-full、Source）にする。
    /// </summary>
    internal static class PlayEntrySettings
    {
        internal const string RunPurposeKey = "OSM.PlayEntry.RunPurpose";
        internal const string RouteKey = "OSM.PlayEntry.Route";
        internal static readonly string DefaultRunPurpose = SampleGameBuildPurposes.SpringFull.Name;

        /// <summary>宣言の表に無い保存値（宣言の改名や削除の後）は既定へ戻す。</summary>
        internal static string RunPurpose
        {
            get
            {
                var stored = EditorUserSettings.GetConfigValue(RunPurposeKey);
                return SampleGameBuildPurposes.All.Any(x => string.Equals(x.Name, stored, StringComparison.Ordinal)) ? stored : DefaultRunPurpose;
            }
            set => EditorUserSettings.SetConfigValue(RunPurposeKey, SampleGameBuildPurposes.Find(value).Name);
        }

        internal static PlayEntryRoute Route
        {
            get => Enum.TryParse<PlayEntryRoute>(EditorUserSettings.GetConfigValue(RouteKey), ignoreCase: false, out var route) ? route : PlayEntryRoute.Source;
            set => EditorUserSettings.SetConfigValue(RouteKey, value.ToString());
        }
    }
}
