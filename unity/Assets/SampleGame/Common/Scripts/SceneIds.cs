using System;

namespace SampleGame.Common
{
    public enum SceneIds
    {
        None,
        OutGameScene,
        Title,
        OutGameSession,
        HomeScene,
        InGame,
        InGameSession,
        PlayerScene,
        InGameUI,
        World,
        Result,
    }

    public static class SceneIdFanctions
    {
        public static string idToName(this SceneIds idValue)
        {
            return idValue switch
            {
                SceneIds.None => string.Empty,
                SceneIds.OutGameScene => "OutGameScene",
                SceneIds.Title => "Title",
                SceneIds.OutGameSession => "OutGameSession",
                SceneIds.HomeScene => "HomeScene",
                SceneIds.InGame => "InGameScene",
                SceneIds.InGameSession => "InGameSession",
                SceneIds.PlayerScene => "PlayerScene",
                SceneIds.InGameUI => "InGameUI",
                SceneIds.World => "World",
                SceneIds.Result => "Result",
                _ => throw new ArgumentOutOfRangeException(nameof(idValue)),
            };

        }
    }
}
