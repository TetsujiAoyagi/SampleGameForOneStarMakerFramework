#nullable enable

using System;
using System.Collections.Generic;
using UnityEditor;

namespace OneStarMaker.Tests.Editor.Spikes.ContentDemandRouting
{
    /// <summary>Enter Play Mode の 4 通り（HANDOFF E4）と、設定を変えない基準の実行。</summary>
    internal enum EpmSetting
    {
        ProjectDefault,
        DomainReloadSceneReload,
        DomainReloadNoSceneReload,
        NoDomainReloadSceneReload,
        NoDomainReloadNoSceneReload,
    }

    /// <summary>
    /// Enter Play Mode の設定を退避・変更・復元する。退避した元の値は SessionState に置き、
    /// Domain Reload の後の TearDown からも戻せるようにする（`TestObservationBootstrap` の前例に倣うが、その code は呼ばない）。
    /// 設定は Play の往復より前にだけ変え、Play の途中では変えない（HANDOFF §5 1 回の実行の構成）。
    /// </summary>
    internal static class EnterPlayModeMatrix
    {
        private const string SavedKey = "OsmSpikeCdr.Epm.Saved";
        private const string EnabledKey = "OsmSpikeCdr.Epm.Enabled";
        private const string OptionsKey = "OsmSpikeCdr.Epm.Options";

        internal static void Apply(EpmSetting setting)
        {
            if (setting == EpmSetting.ProjectDefault) return;
            if (!SessionState.GetBool(SavedKey, false))
            {
                SessionState.SetBool(EnabledKey, EditorSettings.enterPlayModeOptionsEnabled);
                SessionState.SetInt(OptionsKey, (int)EditorSettings.enterPlayModeOptions);
                SessionState.SetBool(SavedKey, true);
            }
            var options = EnterPlayModeOptions.None;
            if (!DomainReload(setting)) options |= EnterPlayModeOptions.DisableDomainReload;
            if (!SceneReload(setting)) options |= EnterPlayModeOptions.DisableSceneReload;
            EditorSettings.enterPlayModeOptionsEnabled = true;
            EditorSettings.enterPlayModeOptions = options;
        }

        /// <summary>退避した値へ戻し、戻ったことを読み直して確かめる。退避が無ければ何もせず true。</summary>
        internal static bool Restore()
        {
            if (!SessionState.GetBool(SavedKey, false)) return true;
            var enabled = SessionState.GetBool(EnabledKey, false);
            var options = SessionState.GetInt(OptionsKey, 0);
            EditorSettings.enterPlayModeOptions = (EnterPlayModeOptions)options;
            EditorSettings.enterPlayModeOptionsEnabled = enabled;
            var restored = EditorSettings.enterPlayModeOptionsEnabled == enabled && (int)EditorSettings.enterPlayModeOptions == options;
            if (restored)
            {
                SessionState.EraseBool(SavedKey);
                SessionState.EraseBool(EnabledKey);
                SessionState.EraseInt(OptionsKey);
            }
            return restored;
        }

        /// <summary>現在の設定で Play に入ると Domain Reload が起きるか。`EnterPlayMode(expectDomainReload)` に渡す。</summary>
        internal static bool CurrentExpectsDomainReload()
            => !EditorSettings.enterPlayModeOptionsEnabled
               || (EditorSettings.enterPlayModeOptions & EnterPlayModeOptions.DisableDomainReload) == 0;

        internal static Dictionary<string, object?> Describe()
            => new()
            {
                ["enterPlayModeOptionsEnabled"] = EditorSettings.enterPlayModeOptionsEnabled,
                ["enterPlayModeOptions"] = EditorSettings.enterPlayModeOptions.ToString(),
                ["expectsDomainReload"] = CurrentExpectsDomainReload(),
                ["expectsSceneReload"] = !EditorSettings.enterPlayModeOptionsEnabled
                    || (EditorSettings.enterPlayModeOptions & EnterPlayModeOptions.DisableSceneReload) == 0,
            };

        private static bool DomainReload(EpmSetting setting)
            => setting is EpmSetting.DomainReloadSceneReload or EpmSetting.DomainReloadNoSceneReload;

        private static bool SceneReload(EpmSetting setting)
            => setting is EpmSetting.DomainReloadSceneReload or EpmSetting.NoDomainReloadSceneReload;
    }
}
