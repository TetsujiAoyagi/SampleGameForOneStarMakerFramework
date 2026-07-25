using System;
using UnityEngine;

namespace SampleGame.Common
{
    public enum SceneIds
    {
        None,
        OutGameScene,
        Title,
        InGame,
        InGameSession,
        SpringLevel,
        SummerLevel,
        AutumLevel,
        WinterLevel,
        Result,
    }

    public static class SceneIdFanctions
    {
        public static string idToName(this SceneIds idValue)
        {
            return idValue switch
            {
                SceneIds.None => string.Empty,
                SceneIds.OutGameScene => "OutGGameSceene",
                SceneIds.Title => "Title",
                SceneIds.InGame => "InGameScene",
                SceneIds.InGameSession => "InGameSession",
                SceneIds.SpringLevel => "ScprinLevel",
                SceneIds.SummerLevel => "SummerLevel",
                SceneIds.AutumLevel => "AutumLevel",
                SceneIds.WinterLevel => "WinterLevevl",
                SceneIds.Result => "Result",
                _ => throw new ArgumentOutOfRangeException(nameof(idValue)),
            };

        }
    }
}
