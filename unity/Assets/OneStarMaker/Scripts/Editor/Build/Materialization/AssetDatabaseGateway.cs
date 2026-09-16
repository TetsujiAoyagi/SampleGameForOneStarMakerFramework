#nullable enable

using System.IO;
using UnityEditor;

namespace OneStarMaker.Editor.Build.Materialization
{
    // AssetDatabase への問い合わせを切り離し、依存閉包の組み立てを Editor 実体なしで検証可能にする。
    internal interface IAssetDatabaseGateway
    {
        string GuidToPath(string guid);
        string PathToGuid(string path);
        string[] GetDependencies(string path);
        bool FileExists(string path);
        bool IsFolder(string path);
    }

    internal sealed class UnityAssetDatabaseGateway : IAssetDatabaseGateway
    {
        public string GuidToPath(string guid) => AssetDatabase.GUIDToAssetPath(guid);
        public string PathToGuid(string path) => AssetDatabase.AssetPathToGUID(path);
        public string[] GetDependencies(string path) => AssetDatabase.GetDependencies(path, true);
        public bool FileExists(string path) => File.Exists(path);
        public bool IsFolder(string path) => AssetDatabase.IsValidFolder(path);
    }
}
