using UnityEngine;

namespace SampleGame.Common.TransitionArgs
{
    public struct InGameArgs
    {
        public SceneIds TransitionLevel;

        public InGameArgs(SceneIds LevelId)
        {
            TransitionLevel = LevelId;
        }
    }
}
