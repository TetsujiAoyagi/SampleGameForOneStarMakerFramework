#nullable enable
using System;
using System.IO;
using OneStarMaker.Runtime.BuildContent.Distribution;
using UnityEditor;
using UnityEngine;

namespace SampleGame.DependOnAll.Editor.Build
{
    internal static class ContentDeliveryPlayBridge
    {
        private const string LedgerKey = "OSM.ContentDelivery.PlayBridge.v1";
        private const string Target = "StandaloneWindows64-Player";
        private static readonly string[] Keys =
        {
            "RUNTIMEMODE", "INSTALLEDREVISIONPATH", "MANIFESTSHA256", "BUILDIDENTITY", "REPRESENTATION"
        };

        internal static void Apply(string root, string digest, string identity, string contentSet, string representation)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Content delivery settings cannot change during Play.");

            // Use For Next Play だけが次の Editor Play の verified pair になる。
            // 未適用のままでは runtimeMode 未指定の fail-closed になり、known-good は読まない。
            var revisionRoot = Path.GetFullPath(root);
            var revisionDirectory = new DirectoryInfo(revisionRoot);
            var setDirectory = revisionDirectory.Parent;
            var installedDirectory = setDirectory?.Parent;
            var cacheDirectory = installedDirectory?.Parent;
            if (setDirectory == null || installedDirectory == null || cacheDirectory == null ||
                !string.Equals(revisionDirectory.Name, identity, StringComparison.Ordinal) ||
                !string.Equals(setDirectory.Name, contentSet, StringComparison.Ordinal) ||
                !string.Equals(installedDirectory.Name, "installed", StringComparison.OrdinalIgnoreCase))
                throw new ContentDeliveryException(ContentDeliveryFailureCode.InvalidManifest,
                    "Prepared revision root is outside the managed cache layout.");

            var validated = new ContentCacheStore(cacheDirectory.FullName).ValidateInstalled(
                revisionRoot, digest, contentSet, identity, Target);
            var ledger = Load();
            var values = new[]
            {
                "directory", validated.RevisionRoot, validated.ManifestSha256,
                validated.Revision, representation
            };
            for (var i = 0; i < Keys.Length; i++)
            {
                var key = "SAMPLEGAME_CONTENT__" + Keys[i];
                var index = Array.IndexOf(ledger.keys, key);
                if (index < 0)
                {
                    Array.Resize(ref ledger.keys, ledger.keys.Length + 1);
                    Array.Resize(ref ledger.previous, ledger.previous.Length + 1);
                    Array.Resize(ref ledger.installed, ledger.installed.Length + 1);
                    index = ledger.keys.Length - 1;
                    ledger.keys[index] = key;
                    ledger.previous[index] = Environment.GetEnvironmentVariable(key, EnvironmentVariableTarget.Process) ?? "<null>";
                }
                // User/Machine へ書くと次の Editor/Player が暗黙の directory mode になる。
                Environment.SetEnvironmentVariable(key, values[i], EnvironmentVariableTarget.Process);
                ledger.installed[index] = values[i];
            }
            SessionState.SetString(LedgerKey, JsonUtility.ToJson(ledger));
        }

        internal static void Reset()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Content delivery settings cannot change during Play.");
            var ledger = Load();
            for (var i = 0; i < ledger.keys.Length; i++)
            {
                // 自分が適用した値を他者が変更した場合は、その新しい値を巻き戻さない。
                if (Environment.GetEnvironmentVariable(ledger.keys[i], EnvironmentVariableTarget.Process) == ledger.installed[i])
                    Environment.SetEnvironmentVariable(ledger.keys[i],
                        ledger.previous[i] == "<null>" ? null : ledger.previous[i],
                        EnvironmentVariableTarget.Process);
            }
            SessionState.EraseString(LedgerKey);
        }

        private static Ledger Load()
        {
            var json = SessionState.GetString(LedgerKey, string.Empty);
            return json.Length == 0
                ? new Ledger()
                : JsonUtility.FromJson<Ledger>(json) ?? new Ledger();
        }

        [Serializable]
        private sealed class Ledger
        {
            public string[] keys = Array.Empty<string>();
            public string[] previous = Array.Empty<string>();
            public string[] installed = Array.Empty<string>();
        }
    }
}
