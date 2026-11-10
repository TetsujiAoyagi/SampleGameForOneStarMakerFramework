#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OneStarMaker.Build.Selection;
using OneStarMaker.Runtime.BuildContent.Distribution;
using UnityEngine;

namespace SampleGame.DependOnAll.Editor.Build
{
    /// <summary>候補の選択結果。Candidate が null なら Failure に理由（0 件なら空）。Warnings は選んだ時の注意。</summary>
    internal sealed class PlayEntryCandidateSelection
    {
        internal PlayEntryCandidateSelection(PlayEntryCandidate? candidate, string failure, IReadOnlyList<string> warnings)
        { Candidate = candidate; Failure = failure; Warnings = warnings; }

        internal PlayEntryCandidate? Candidate { get; }
        internal string Failure { get; }
        internal IReadOnlyList<string> Warnings { get; }
    }

    /// <summary>
    /// installed cache から、選択した宣言の revision を選んで検証する。新しい install から順に検証し、最初に通ったものを使う。
    /// 検証に失敗した新しい revision を飛ばした時は Warning に名前を出す（黙って別の revision へ切り替えない）。
    /// <para>
    /// 検証は呼び出しの時点の主張で、利用権ではない。起動が渡された pair を再検証するので、検証の後の差し替えは起動の fail-closed が受ける。
    /// </para>
    /// </summary>
    internal static class PlayEntryCandidates
    {
        internal static PlayEntryCandidateSelection Select(string cacheRoot, BuildPurposeDeclaration declaration, string target,
            Func<string, string> firstSceneOf)
        {
            if (declaration == null) throw new ArgumentNullException(nameof(declaration));
            if (firstSceneOf == null) throw new ArgumentNullException(nameof(firstSceneOf));

            // 未作成の root を候補探索のためだけに作らない（ContentCacheStore は lock のために control を作る）。
            if (!Directory.Exists(Path.Combine(cacheRoot, "installed")))
                return new PlayEntryCandidateSelection(null, string.Empty, Array.Empty<string>());

            ContentCacheStore store;
            ContentCacheSnapshot snapshot;
            try
            {
                store = new ContentCacheStore(cacheRoot);
                snapshot = store.Inspect();
            }
            catch (Exception ex) when (ex is ContentDeliveryException or ArgumentException)
            {
                return new PlayEntryCandidateSelection(null, "The content cache cannot be read: " + ex.Message, Array.Empty<string>());
            }

            var problems = new List<string>();
            var setRoot = Path.GetFullPath(Path.Combine(cacheRoot, "installed", declaration.Name)) + Path.DirectorySeparatorChar;
            problems.AddRange(snapshot.CorruptPaths
                .Where(path => Path.GetFullPath(path).StartsWith(setRoot, StringComparison.OrdinalIgnoreCase))
                .Select(path => "Corrupt receipt: " + path));

            var entries = snapshot.Entries
                .Where(x => string.Equals(x.ContentSet, declaration.Name, StringComparison.Ordinal)
                            && string.Equals(x.Target, target, StringComparison.Ordinal))
                .OrderByDescending(x => x.InstalledUtc)
                .ThenBy(x => x.Revision, StringComparer.Ordinal)
                .ToArray();

            foreach (var entry in entries)
            {
                var values = TryValidate(store, entry, declaration, target, firstSceneOf, out var problem);
                if (values != null)
                    return new PlayEntryCandidateSelection(new PlayEntryCandidate(entry.Revision, values, entries.Length > 1),
                        string.Empty, problems.Select(x => "Skipped while selecting content: " + x).ToArray());
                problems.Add(entry.Revision + ": " + problem);
            }
            return new PlayEntryCandidateSelection(null, string.Join(" ", problems), Array.Empty<string>());
        }

        private static IReadOnlyList<KeyValuePair<string, string>>? TryValidate(ContentCacheStore store, ContentCacheEntry entry,
            BuildPurposeDeclaration declaration, string target, Func<string, string> firstSceneOf, out string problem)
        {
            problem = string.Empty;
            try
            {
                var validated = store.ValidateInstalled(entry.RevisionRoot, entry.ManifestSha256, entry.ContentSet, entry.Revision, target);
                // transport.json の digest は上の検証で receipt と照合済みなので、ここで読むのは検証した bytes と同じ内容。
                var manifest = JsonUtility.FromJson<ContentTransportManifest>(File.ReadAllText(Path.Combine(validated.RevisionRoot, "transport.json")));
                var mismatch = VerifyPlayLauncher.CompareDeclaration(manifest?.declaration ?? new ContentManifestDeclaration(), declaration);
                if (mismatch != null) { problem = mismatch; return null; }
                var recorded = manifest!.declaration;
                return PurposeLaunchSettings.DirectoryPlay(validated.RevisionRoot, validated.ManifestSha256, validated.Revision,
                    recorded.representation, recorded.runtimeDefaults.Select(x => new KeyValuePair<string, string>(x.key, x.value)),
                    firstSceneOf(declaration.Name));
            }
            catch (Exception ex) when (ex is ContentDeliveryException or IOException or ArgumentException or UnauthorizedAccessException)
            {
                problem = ex.Message;
                return null;
            }
        }
    }
}
