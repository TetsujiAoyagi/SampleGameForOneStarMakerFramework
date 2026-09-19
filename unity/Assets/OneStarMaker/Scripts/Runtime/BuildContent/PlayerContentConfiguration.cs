#nullable enable

using System;
using System.IO;
using OneStarMaker.Foundation.Config;

namespace OneStarMaker.Runtime.BuildContent
{
    /// <summary>Player package 内の content directory 指定を検証して正規化する。</summary>
    internal sealed class PlayerContentConfiguration
    {
        internal const int SchemaVersion = 1;
        internal PlayerContentConfiguration(string path, string identity, string representation, string firstScene)
        { Path = path; Identity = identity; Representation = representation; FirstScene = firstScene; }
        internal string Path { get; }
        internal string Identity { get; }
        internal string Representation { get; }
        internal string FirstScene { get; }

        internal static PlayerContentConfiguration Read(AppConfig config, string installRoot, string target)
        {
            if (config.GetInt("content:schemaVersion", 0) != SchemaVersion)
                throw new ContentDirectoryException(ContentDirectoryFailureCode.InvalidConfiguration, "Unsupported Player content configuration schema.");
            var relative = config.GetString("content:relativeDirectory", string.Empty);
            var identity = config.GetString("content:buildIdentity", string.Empty);
            var configuredTarget = config.GetString("content:target", string.Empty);
            var representation = config.GetString("content:representation", string.Empty);
            var firstScene = config.GetString("content:firstScene", string.Empty);
            if (Path.IsPathRooted(relative) || string.IsNullOrWhiteSpace(relative) || string.IsNullOrWhiteSpace(identity)
                || string.IsNullOrWhiteSpace(configuredTarget) || string.IsNullOrWhiteSpace(representation) || string.IsNullOrWhiteSpace(firstScene))
                throw new ContentDirectoryException(ContentDirectoryFailureCode.InvalidConfiguration, "Player content configuration is incomplete.");
            if (!string.Equals(configuredTarget, target, StringComparison.Ordinal))
                throw new ContentDirectoryException(ContentDirectoryFailureCode.TargetMismatch, "Player content target does not match this executable.", identity, target);
            var root = Path.GetFullPath(installRoot);
            var resolved = Path.GetFullPath(Path.Combine(root, relative));
            if (!resolved.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new ContentDirectoryException(ContentDirectoryFailureCode.InvalidConfiguration, "Player content directory escapes its install root.", identity, target);
            return new PlayerContentConfiguration(resolved, identity, representation, firstScene);
        }
    }
}
