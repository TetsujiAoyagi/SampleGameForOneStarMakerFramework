using UnityEngine;

namespace OneStarMaker.Runtime.CameraSystem.Abstractions
{
    public enum ClearFlag
    {
        Color,
        Skybox,
        None,
    }
    public interface ICameraBackgroundApplier
    {
        void SetClearFlag(ICameraView view, ClearFlag clearFlag, Color color);
    }
}
