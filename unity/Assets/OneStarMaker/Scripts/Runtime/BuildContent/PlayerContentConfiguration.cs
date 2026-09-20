#nullable enable

using System;
using System.IO;
using OneStarMaker.Foundation.Config;
using OneStarMaker.Runtime.BuildContent.Distribution;

namespace OneStarMaker.Runtime.BuildContent
{
    /// <summary>Player package 内の content directory 指定を検証して正規化する。</summary>
    internal sealed class PlayerContentConfiguration
    {
        internal const int SchemaVersion = 1;
        internal PlayerContentConfiguration(string path, string identity, string representation, string firstScene, VerifiedInstalledRevision? verified=null)
        { Path = path; Identity = identity; Representation = representation; FirstScene = firstScene; VerifiedRevision=verified; }
        internal string Path { get; }
        internal string Identity { get; }
        internal string Representation { get; }
        internal string FirstScene { get; }
        internal VerifiedInstalledRevision? VerifiedRevision { get; }

        internal static PlayerContentConfiguration Resolve(PlayerBakedContentConfiguration baked,AppConfig merged,string installRoot,string target)
        {
            if(baked.SchemaVersion!=merged.GetInt("content:schemaVersion",0)||baked.RuntimeMode!=merged.GetString("content:runtimeMode","")||baked.BuildIdentity!=merged.GetString("content:buildIdentity","")||baked.ContentSet!=merged.GetString("content:contentSet","")||baked.Target!=merged.GetString("content:target","")||baked.Representation!=merged.GetString("content:representation","")||baked.FirstScene!=merged.GetString("content:firstScene","")||baked.RelativeDirectory!=merged.GetString("content:relativeDirectory","")||baked.ProbeToken!=merged.GetString("content:probeToken",""))
                throw new ContentDirectoryException(ContentDirectoryFailureCode.InvalidConfiguration,"Protected Player content configuration was overridden.",baked.BuildIdentity,target);
            var root=merged.GetString("content:installedRevisionPath","");var digest=merged.GetString("content:manifestSha256","");
            if((root.Length==0)!=(digest.Length==0))throw new ContentDirectoryException(ContentDirectoryFailureCode.InvalidConfiguration,"Installed revision path and manifest digest must be provided together.",baked.BuildIdentity,target);
            if(root.Length==0)return Read(merged,installRoot,target);
            if(baked.ContentSet.Length==0)throw new ContentDirectoryException(ContentDirectoryFailureCode.InvalidConfiguration,"This Player has no baked content set and cannot use an installed override.",baked.BuildIdentity,target);
            var verified=InstalledRevisionVerifier.Verify(root,digest,baked.ContentSet,baked.BuildIdentity,target);
            return new PlayerContentConfiguration(verified.ContentPath,baked.BuildIdentity,baked.Representation,baked.FirstScene,verified);
        }

        internal static PlayerContentConfiguration Read(AppConfig config, string installRoot, string target)
        {
            if (config.GetInt("content:schemaVersion", 0) != SchemaVersion)
                throw new ContentDirectoryException(ContentDirectoryFailureCode.InvalidConfiguration, "Unsupported Player content configuration schema.");
            var relative = config.GetString("content:relativeDirectory", string.Empty);
            var identity = config.GetString("content:buildIdentity", string.Empty);
            var configuredTarget = config.GetString("content:target", string.Empty);
            var representation = config.GetString("content:representation", string.Empty);
            var firstScene = config.GetString("content:firstScene", string.Empty);
            if (System.IO.Path.IsPathRooted(relative) || string.IsNullOrWhiteSpace(relative) || string.IsNullOrWhiteSpace(identity)
                || string.IsNullOrWhiteSpace(configuredTarget) || string.IsNullOrWhiteSpace(representation) || string.IsNullOrWhiteSpace(firstScene))
                throw new ContentDirectoryException(ContentDirectoryFailureCode.InvalidConfiguration, "Player content configuration is incomplete.");
            if (!string.Equals(configuredTarget, target, StringComparison.Ordinal))
                throw new ContentDirectoryException(ContentDirectoryFailureCode.TargetMismatch, "Player content target does not match this executable.", identity, target);
            var root = System.IO.Path.GetFullPath(installRoot);
            var resolved = System.IO.Path.GetFullPath(System.IO.Path.Combine(root, relative));
            if (!resolved.StartsWith(root + System.IO.Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new ContentDirectoryException(ContentDirectoryFailureCode.InvalidConfiguration, "Player content directory escapes its install root.", identity, target);
            return new PlayerContentConfiguration(resolved, identity, representation, firstScene);
        }
    }
}
