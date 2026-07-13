using OneStarMaker.Runtime.CameraSystem.Abstractions;
using OneStarMaker.Runtime.CameraSystem.Core;
using OneStarMaker.Runtime.CameraSystem.Hosting;
using UnityEngine;

namespace OneStarMaker.Runtime.CameraSystem.BackgroundApplier
{
    public class CameraBackgroundApplier : ICameraBackgroundApplier
    {
        private readonly CameraSystemHost _host;
        public CameraBackgroundApplier(CameraSystemHost host)
        {
            _host = host;// ?? new System.InvalidCastException();
        }
        public void SetClearFlag(ICameraView view, ClearFlag clearFlag, Color color)
        {
            CameraView cameraView = view as CameraView ?? throw new System.InvalidCastException();

            if(_host.Views.TryGetValue(cameraView.ViewId, out var value))
            {
                CameraClearFlags flags = value.Camera.clearFlags;
                if(clearFlag == ClearFlag.Skybox)
                {
                    flags = CameraClearFlags.Skybox;
                }
                else if( clearFlag == ClearFlag.Color)
                {
                    flags = CameraClearFlags.Color;
                }
                value.Camera.clearFlags = flags;
                value.Camera.backgroundColor = color;
            }
            else
            {
                throw new System.InvalidOperationException();
            }

        }
    }
}
