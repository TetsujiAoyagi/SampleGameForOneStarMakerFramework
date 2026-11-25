#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace SampleGame.DependOnAll.Editor.Build
{
    /// <summary>Player の入力の file 1 つ（project の root からの相対 path、<c>/</c> 区切り）と、その bytes の SHA-256（小文字の 16 進）。</summary>
    [Serializable]
    internal sealed class PlayerInputFileEntry
    {
        public string path = "";
        public string sha256 = "";

        public PlayerInputFileEntry() { }
        internal PlayerInputFileEntry(string path, string sha256) { this.path = path; this.sha256 = sha256; }
    }

    /// <summary>
    /// 遊べる Player の <c>player-compat.json</c>。Player の build が project の mutation の前に取った、content 以外の Player の入力の指紋。
    /// 入口は手元の指紋と比べ、違えば上書きを作らずに止める（古い設定の exe で新しい設定の上書きを遊ばせない）。
    /// </summary>
    [Serializable]
    internal sealed class PlayerCompatibilityRecord
    {
        internal const int CurrentSchemaVersion = 1;
        public int schemaVersion = CurrentSchemaVersion;
        public string identity = "";
        /// <summary><see cref="PlayerCompatibility.Fingerprint"/> の値。</summary>
        public string digest = "";
        public PlayerInputFileEntry[] files = Array.Empty<PlayerInputFileEntry>();
    }

    /// <summary>
    /// Player に焼き込む content 以外の入力（D11）の集合の規則・指紋・比較。file の一覧は引数で受け、Unity も disk も触らない
    /// （集める I/O は <see cref="PlayerInputFiles"/> の 1 か所だけ。build と入口が同じ口を使わないと指紋が食い違う）。
    /// <para>
    /// 集合に入れるか迷う file は入れる側に倒す（止めすぎは「Player を build し直す」で済むが、漏れは古い設定の exe で黙って遊ばせる）。
    /// 除くのは、Player に入らないと言える Editor の設定と、Player の build の mutation が一時に書いて消す file だけである。
    /// </para>
    /// </summary>
    internal static class PlayerCompatibility
    {
        internal const string FileName = "player-compat.json";

        /// <summary>
        /// <c>ProjectSettings/</c> の直下にあって Player に入らない file（Unity 6000.6 の project の一覧から、Editor だけが読む物を選んだ）。
        /// ここに無い file（今後 Unity や package が足す物を含む）は入れる。
        /// </summary>
        internal static readonly IReadOnlyCollection<string> EditorOnlyProjectSettings = new HashSet<string>(StringComparer.Ordinal)
        {
            "ProjectSettings/EditorSettings.asset",
            "ProjectSettings/EditorBuildSettings.asset",
            "ProjectSettings/PackageManagerSettings.asset",
            "ProjectSettings/VersionControlSettings.asset",
            "ProjectSettings/PresetManager.asset",
            "ProjectSettings/ProjectAuditorSettings.asset",
            "ProjectSettings/SceneTemplateSettings.json",
            "ProjectSettings/CsprojModifierSettings.json",
        };

        /// <summary>Player の build の mutation が一時に書いて消す file の印（<c>PlayerBuildProjectMutation</c>）。指紋を取る時点で有無が変わる。</summary>
        private const string MutationAssetRoot = "Assets/Resources/BS4/";
        private const string MutationAssetRootMeta = "Assets/Resources/BS4.meta";
        private const string MutationRuntimeConfig = "Assets/StreamingAssets/bs4-runtime.json";

        /// <summary>
        /// 候補の file の列から、指紋に入れる集合を返す（重複を除き、ordinal で並べる）。path は project の root からの相対で <c>/</c> 区切り。
        /// </summary>
        internal static IReadOnlyList<string> Select(IEnumerable<string> candidates)
        {
            if (candidates == null) throw new ArgumentNullException(nameof(candidates));
            return candidates.Select(Normalize).Where(IsIncluded).Distinct(StringComparer.Ordinal)
                .OrderBy(x => x, StringComparer.Ordinal).ToArray();
        }

        internal static bool IsIncluded(string relativePath)
        {
            var path = Normalize(relativePath);
            if (path.Length == 0) return false;
            if (EditorOnlyProjectSettings.Contains(path)) return false;
            if (path.StartsWith(MutationAssetRoot, StringComparison.Ordinal) || path == MutationAssetRootMeta) return false;
            if (path == MutationRuntimeConfig || path == MutationRuntimeConfig + ".meta") return false;
            return true;
        }

        internal static string Normalize(string relativePath) => (relativePath ?? string.Empty).Replace('\\', '/').TrimStart('/');

        /// <summary>
        /// file の列の指紋。上書きの digest（<c>ContentOverlayDigest</c>）と同じ書式で、相対 path の ordinal 順に
        /// <c>&lt;相対 path&gt;\n&lt;SHA-256&gt;\n</c> を UTF-8 で連ねた bytes の SHA-256（小文字の 16 進）。
        /// </summary>
        internal static string Fingerprint(IEnumerable<PlayerInputFileEntry> entries)
        {
            var text = new StringBuilder();
            foreach (var entry in Sorted(entries))
                text.Append(entry.path).Append('\n').Append(entry.sha256).Append('\n');
            using var sha = SHA256.Create();
            return string.Concat(sha.ComputeHash(Encoding.UTF8.GetBytes(text.ToString())).Select(x => x.ToString("x2")));
        }

        internal static PlayerCompatibilityRecord CreateRecord(string identity, IEnumerable<PlayerInputFileEntry> entries)
        {
            var sorted = Sorted(entries).ToArray();
            return new PlayerCompatibilityRecord { identity = identity, digest = Fingerprint(sorted), files = sorted };
        }

        internal static string ToJson(PlayerCompatibilityRecord record) => JsonUtility.ToJson(record, true);

        /// <summary>読めない・形の違う記録は例外（入口は compat の無い Player と同じく止める）。</summary>
        internal static PlayerCompatibilityRecord FromJson(string json)
        {
            var record = JsonUtility.FromJson<PlayerCompatibilityRecord>(json)
                         ?? throw new FormatException("player-compat.json is empty.");
            if (record.schemaVersion != PlayerCompatibilityRecord.CurrentSchemaVersion)
                throw new FormatException("player-compat.json has an unsupported schema version: " + record.schemaVersion);
            if (string.IsNullOrEmpty(record.digest) || record.files == null)
                throw new FormatException("player-compat.json has no digest or file list.");
            if (!string.Equals(Fingerprint(record.files), record.digest, StringComparison.Ordinal))
                throw new FormatException("player-compat.json digest does not match its file list.");
            return record;
        }

        /// <summary>
        /// Player の記録と手元の指紋の違い。空なら一致。各行は <c>&lt;path&gt; (changed|added|removed)</c> で path の ordinal 順
        /// （added は手元にだけある、removed は Player の記録にだけある）。
        /// </summary>
        internal static IReadOnlyList<string> Compare(PlayerCompatibilityRecord player, IEnumerable<PlayerInputFileEntry> local)
        {
            if (player == null) throw new ArgumentNullException(nameof(player));
            var recorded = player.files.ToDictionary(x => x.path, x => x.sha256, StringComparer.Ordinal);
            var current = local.ToDictionary(x => x.path, x => x.sha256, StringComparer.Ordinal);
            var differences = new List<(string Path, string Kind)>();
            foreach (var pair in current)
            {
                if (!recorded.TryGetValue(pair.Key, out var sha)) differences.Add((pair.Key, "added"));
                else if (!string.Equals(sha, pair.Value, StringComparison.OrdinalIgnoreCase)) differences.Add((pair.Key, "changed"));
            }
            foreach (var path in recorded.Keys)
                if (!current.ContainsKey(path)) differences.Add((path, "removed"));
            return differences.OrderBy(x => x.Path, StringComparer.Ordinal).Select(x => x.Path + " (" + x.Kind + ")").ToArray();
        }

        private static IEnumerable<PlayerInputFileEntry> Sorted(IEnumerable<PlayerInputFileEntry> entries) =>
            entries.OrderBy(x => x.path, StringComparer.Ordinal);
    }
}
