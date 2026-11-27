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
        /// <summary>
        /// <paramref name="selection"/>（取得で選んだ base の記録）を渡さない時は今と同じ並び。渡した時は、記録の組（revision と digest）と
        /// 同じ entry の時刻を「install の時刻と、選んだ時刻の新しい方」と読み、UTC で比べて並べる。「直近の明示の操作が先」ではなく
        /// 「選んだ後の新しい install が先」の限定した規則で、既にある revision の再利用（receipt の時刻を変えない）は後の install に数えない。
        /// receipt の時刻は installer が copy の前に作るので、「後の install」は receipt の時刻の順で、同時の操作の公開の順ではない。
        /// 記録の revision が無い・digest が違う・記録を読めない時は、記録を使わずに今の並びで答えて Warning に出す（Play を拒まない）。
        /// </summary>
        internal static PlayEntryCandidateSelection Select(string cacheRoot, BuildPurposeDeclaration declaration, string target,
            Func<string, string> firstSceneOf, ContentBaseSelectionRead? selection = null)
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

            var matching = snapshot.Entries
                .Where(x => string.Equals(x.ContentSet, declaration.Name, StringComparison.Ordinal)
                            && string.Equals(x.Target, target, StringComparison.Ordinal))
                .ToArray();
            var selectionWarnings = new List<string>();
            var selected = SelectedEntry(selection, matching, selectionWarnings);
            var entries = (selected == null
                    ? matching.OrderByDescending(x => x.InstalledUtc)
                    : matching.OrderByDescending(x => x == selected && selection!.Entry!.SelectedUtc > x.InstalledUtc.ToUniversalTime()
                        ? selection.Entry.SelectedUtc : x.InstalledUtc.ToUniversalTime()))
                .ThenBy(x => x.Revision, StringComparer.Ordinal)
                .ToArray();

            foreach (var entry in entries)
            {
                var values = TryValidate(store, entry, declaration, target, firstSceneOf, out var problem, out var install);
                if (values != null)
                    return new PlayEntryCandidateSelection(new PlayEntryCandidate(entry.Revision, values, entries.Length > 1, install),
                        string.Empty, selectionWarnings.Concat(problems.Select(x => "Skipped while selecting content: " + x)).ToArray());
                problems.Add(entry.Revision + ": " + problem);
            }
            return new PlayEntryCandidateSelection(null, string.Join(" ", problems), Array.Empty<string>());
        }

        // 記録の組と revision・digest が同じ entry。使えない記録は理由を Warning に足して null を返す（今の並びに戻る）。
        private static ContentCacheEntry? SelectedEntry(ContentBaseSelectionRead? selection, IReadOnlyList<ContentCacheEntry> entries,
            List<string> warnings)
        {
            if (selection == null) return null;
            if (selection.Problem.Length != 0)
            {
                warnings.Add(selection.Problem + " The acquired base is not used; the content is ordered by install time.");
                return null;
            }
            var record = selection.Entry;
            if (record == null) return null;
            var sameRevision = entries.Where(x => string.Equals(x.Revision, record.revision, StringComparison.Ordinal)).ToArray();
            var entry = sameRevision.FirstOrDefault(x => string.Equals(x.ManifestSha256, record.manifestSha256, StringComparison.Ordinal));
            if (entry != null) return entry;
            warnings.Add("The acquired base " + record.revision + (sameRevision.Length == 0
                ? " is no longer installed"
                : " is installed with a different digest") + "; the content is ordered by install time.");
            return null;
        }

        private static IReadOnlyList<KeyValuePair<string, string>>? TryValidate(ContentCacheStore store, ContentCacheEntry entry,
            BuildPurposeDeclaration declaration, string target, Func<string, string> firstSceneOf, out string problem,
            out ContentInstallResult? install)
        {
            install = Validate(store, entry, declaration, target, out problem, out var recorded);
            if (install == null) return null;
            return PurposeLaunchSettings.DirectoryPlay(install.RevisionRoot, install.ManifestSha256, install.Revision,
                recorded!.representation, recorded.runtimeDefaults.Select(x => new KeyValuePair<string, string>(x.key, x.value)),
                firstSceneOf(declaration.Name));
        }

        /// <summary>
        /// install の検証（<c>ValidateInstalled</c>）と、manifest に記録した宣言の照合。Editor の Play の候補と、Player の base の決定が
        /// 同じ検証を通る（複製しない）。失敗は null で、理由は <paramref name="problem"/>。
        /// </summary>
        private static ContentInstallResult? Validate(ContentCacheStore store, ContentCacheEntry entry, BuildPurposeDeclaration declaration,
            string target, out string problem, out ContentManifestDeclaration? recorded)
        {
            problem = string.Empty;
            recorded = null;
            try
            {
                var validated = store.ValidateInstalled(entry.RevisionRoot, entry.ManifestSha256, entry.ContentSet, entry.Revision, target);
                // transport.json の digest は上の検証で receipt と照合済みなので、ここで読むのは検証した bytes と同じ内容。
                var manifest = JsonUtility.FromJson<ContentTransportManifest>(File.ReadAllText(Path.Combine(validated.RevisionRoot, "transport.json")));
                var mismatch = VerifyPlayLauncher.CompareDeclaration(manifest?.declaration ?? new ContentManifestDeclaration(), declaration);
                if (mismatch != null) { problem = mismatch; return null; }
                recorded = manifest!.declaration;
                // 検証した install の結果を捨てずに返す（上書きの計画が base の manifest をこの値と照合して読む）。
                return validated;
            }
            catch (Exception ex) when (ex is ContentDeliveryException or IOException or ArgumentException or UnauthorizedAccessException)
            {
                problem = ex.Message;
                return null;
            }
        }

        /// <summary>
        /// 遊べる Player の base を決める（D6 の (2)）。宣言と target が合い、revision が Player の identity と同じで、manifest の digest が
        /// Player の <c>build-receipt.json</c> の <c>transportManifestSha256</c> と同じ install を 1 つだけ選び、Editor の Play の候補と
        /// 同じ検証を通す。最新の install や別の revision へ縮退しない（Player が指す 1 つだけを使う）。Delivery の ledger は読まない。
        /// </summary>
        internal static PlayerBaseResolution ResolvePlayerBase(string cacheRoot, BuildPurposeDeclaration declaration, string target,
            string playerIdentity, string transportManifestSha256, string transportDirectory)
        {
            if (declaration == null) throw new ArgumentNullException(nameof(declaration));
            var installGuidance = "Install " + transportDirectory + " with the Content Delivery window.";
            // 未作成の root を探索のためだけに作らない（ContentCacheStore は lock のために control を作る）。
            if (!Directory.Exists(Path.Combine(cacheRoot, "installed")))
                return PlayerBaseResolution.NotInstalled("No content is installed for the Player " + playerIdentity + ". " + installGuidance);

            ContentCacheStore store;
            ContentCacheSnapshot snapshot;
            try
            {
                store = new ContentCacheStore(cacheRoot);
                snapshot = store.Inspect();
            }
            catch (Exception ex) when (ex is ContentDeliveryException or ArgumentException)
            {
                return PlayerBaseResolution.Invalid("The content cache cannot be read: " + ex.Message);
            }

            var sameRevision = snapshot.Entries
                .Where(x => string.Equals(x.ContentSet, declaration.Name, StringComparison.Ordinal)
                            && string.Equals(x.Target, target, StringComparison.Ordinal)
                            && string.Equals(x.Revision, playerIdentity, StringComparison.Ordinal))
                .ToArray();
            var entry = sameRevision.FirstOrDefault(x => string.Equals(x.ManifestSha256, transportManifestSha256, StringComparison.OrdinalIgnoreCase));
            if (entry == null)
                return PlayerBaseResolution.NotInstalled(sameRevision.Length == 0
                    ? "The Player's content revision " + playerIdentity + " is not installed. " + installGuidance
                    : "The installed revision " + playerIdentity + " has a different manifest digest from the Player's transport. " + installGuidance);
            var install = Validate(store, entry, declaration, target, out var problem, out _);
            return install == null
                ? PlayerBaseResolution.Invalid("The installed revision " + playerIdentity + " failed verification: " + problem)
                : PlayerBaseResolution.Resolved(install);
        }
    }

    /// <summary>遊べる Player の base の決定の結果。<see cref="Install"/> が null なら <see cref="IsNotInstalled"/> と <see cref="Failure"/>。</summary>
    internal sealed class PlayerBaseResolution
    {
        private PlayerBaseResolution(ContentInstallResult? install, bool isNotInstalled, string failure)
        { Install = install; IsNotInstalled = isNotInstalled; Failure = failure; }

        internal static PlayerBaseResolution Resolved(ContentInstallResult install) => new(install, false, string.Empty);
        internal static PlayerBaseResolution NotInstalled(string failure) => new(null, true, failure);
        internal static PlayerBaseResolution Invalid(string failure) => new(null, false, failure);

        internal ContentInstallResult? Install { get; }
        /// <summary>Player が指す install が無い（install の案内を出す）。偽で <see cref="Install"/> が null なら検証の失敗。</summary>
        internal bool IsNotInstalled { get; }
        internal string Failure { get; }
    }
}
