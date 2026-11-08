#nullable enable

using System;
using OneStarMaker.Runtime.AssetManagement;

namespace OneStarMaker.Runtime.ContentResolution
{
    /// <summary>
    /// Scene 要求の結果: AssetManagement が返した handle と、選択結果の記録。
    /// 解放は AssetManagement の既存 API（UnloadSceneAsync → ReleaseScene）で行う。
    /// </summary>
    public sealed class SceneLoadOutcome
    {
        internal SceneLoadOutcome(ISceneHandle handle, ContentLoadRecord record)
        {
            Handle = handle ?? throw new ArgumentNullException(nameof(handle));
            Record = record ?? throw new ArgumentNullException(nameof(record));
        }

        public ISceneHandle Handle { get; }

        public ContentLoadRecord Record { get; }
    }

    /// <summary>
    /// Object 要求の結果: AssetManagement が返した handle と、選択結果の記録。
    /// 解放は渡した owner の既存の契約（Manual なら Release 等）で行う。
    /// </summary>
    public sealed class AssetLoadOutcome<T> where T : UnityEngine.Object
    {
        internal AssetLoadOutcome(IAssetHandle<T> handle, ContentLoadRecord record)
        {
            Handle = handle ?? throw new ArgumentNullException(nameof(handle));
            Record = record ?? throw new ArgumentNullException(nameof(record));
        }

        public IAssetHandle<T> Handle { get; }

        public ContentLoadRecord Record { get; }
    }
}
