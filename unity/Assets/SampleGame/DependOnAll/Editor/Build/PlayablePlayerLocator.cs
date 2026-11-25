#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace SampleGame.DependOnAll.Editor.Build
{
    /// <summary><c>players/&lt;identity&gt;</c> の 1 つの folder を読んだ値。読めなかった時は <see cref="ReadFailure"/> に理由。</summary>
    internal sealed class PlayablePlayerCandidate
    {
        internal PlayablePlayerCandidate(string directoryName, string fullPath, string? receiptIdentity, string? transportManifestSha256,
            bool hasCompatibility, string? readFailure)
        {
            DirectoryName = directoryName; FullPath = fullPath; ReceiptIdentity = receiptIdentity;
            TransportManifestSha256 = transportManifestSha256; HasCompatibility = hasCompatibility; ReadFailure = readFailure;
        }

        /// <summary>folder の名前（publish が build の identity で作る）。</summary>
        internal string DirectoryName { get; }
        internal string FullPath { get; }
        /// <summary><c>build-receipt.json</c> の <c>identity</c>。読めなければ null。</summary>
        internal string? ReceiptIdentity { get; }
        internal string? TransportManifestSha256 { get; }
        /// <summary><c>player-compat.json</c> がある（このスライスより前の build には無い）。</summary>
        internal bool HasCompatibility { get; }
        internal string? ReadFailure { get; }
    }

    /// <summary>入口が使う Player の決定。<see cref="IsFound"/> が偽なら <see cref="Failure"/> に理由と案内。</summary>
    internal sealed class PlayablePlayerChoice
    {
        private PlayablePlayerChoice(string identity, string directory, string transportManifestSha256, string failure)
        { Identity = identity; Directory = directory; TransportManifestSha256 = transportManifestSha256; Failure = failure; }

        internal static PlayablePlayerChoice Found(PlayablePlayerCandidate candidate) =>
            new(candidate.ReceiptIdentity!, candidate.FullPath, candidate.TransportManifestSha256!, string.Empty);
        internal static PlayablePlayerChoice NotFound(string failure) => new(string.Empty, string.Empty, string.Empty, failure);

        internal bool IsFound => Failure.Length == 0;
        internal string Identity { get; }
        /// <summary><c>players/&lt;identity&gt;</c> の絶対 path。</summary>
        internal string Directory { get; }
        /// <summary>Player と同じ build の transport の <c>transport.json</c> の SHA-256（base の install を選ぶ鍵）。</summary>
        internal string TransportManifestSha256 { get; }
        internal string Failure { get; }
    }

    /// <summary>
    /// 入口が上書きを重ねる遊べる Player を 1 つ決める（D6 の (1)）。identity は build の UTC 時刻で始まるので、ordinal で最大の
    /// identity が最新の build である。最新の Player が使えない（receipt が読めない、compat が無い）時は止め、古い Player へ戻らない
    /// （利用者は最新の build で確かめていると思っているので、黙って古い exe を起動しない）。
    /// </summary>
    internal static class PlayablePlayerLocator
    {
        internal const string ReceiptFileName = "build-receipt.json";
        internal const string BuildGuidance = "Build the playable Player with Tools/OSM/Content/Build Spring Playable Player.";

        /// <summary><c>artifacts/playable/players</c> を読んで決める。</summary>
        internal static PlayablePlayerChoice Locate(string playersRoot) => Choose(Scan(playersRoot));

        /// <summary>規則の本体。folder の列だけから決め、disk を触らない。</summary>
        internal static PlayablePlayerChoice Choose(IEnumerable<PlayablePlayerCandidate> candidates)
        {
            var newest = candidates.OrderByDescending(x => x.DirectoryName, StringComparer.Ordinal).FirstOrDefault();
            if (newest == null)
                return PlayablePlayerChoice.NotFound("No playable Player was found under artifacts/playable/players. " + BuildGuidance);
            if (newest.ReadFailure != null || newest.ReceiptIdentity == null || string.IsNullOrEmpty(newest.TransportManifestSha256))
                return PlayablePlayerChoice.NotFound("The newest playable Player (" + newest.DirectoryName + ") has no readable " + ReceiptFileName +
                                                     ": " + (newest.ReadFailure ?? "identity or transportManifestSha256 is missing") + ". " + BuildGuidance);
            if (!string.Equals(newest.ReceiptIdentity, newest.DirectoryName, StringComparison.Ordinal))
                return PlayablePlayerChoice.NotFound("The newest playable Player folder (" + newest.DirectoryName + ") holds a receipt for another identity (" +
                                                     newest.ReceiptIdentity + "). " + BuildGuidance);
            if (!newest.HasCompatibility)
                return PlayablePlayerChoice.NotFound("The newest playable Player (" + newest.DirectoryName + ") has no " + PlayerCompatibility.FileName +
                                                     " (it was built before local edits could be layered on a Player). " + BuildGuidance);
            return PlayablePlayerChoice.Found(newest);
        }

        /// <summary>folder を列挙して receipt を読む薄い口。folder が無ければ空。</summary>
        internal static IReadOnlyList<PlayablePlayerCandidate> Scan(string playersRoot)
        {
            if (!Directory.Exists(playersRoot)) return Array.Empty<PlayablePlayerCandidate>();
            var candidates = new List<PlayablePlayerCandidate>();
            foreach (var directory in Directory.GetDirectories(playersRoot))
            {
                var name = Path.GetFileName(directory);
                var hasCompatibility = File.Exists(Path.Combine(directory, PlayerCompatibility.FileName));
                try
                {
                    var receipt = JsonUtility.FromJson<Receipt>(File.ReadAllText(Path.Combine(directory, ReceiptFileName)));
                    candidates.Add(new PlayablePlayerCandidate(name, Path.GetFullPath(directory),
                        string.IsNullOrEmpty(receipt?.identity) ? null : receipt!.identity, receipt?.transportManifestSha256, hasCompatibility, null));
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
                {
                    candidates.Add(new PlayablePlayerCandidate(name, Path.GetFullPath(directory), null, null, hasCompatibility, ex.Message));
                }
            }
            return candidates;
        }

        [Serializable]
        private sealed class Receipt
        {
            public string identity = "";
            public string transportManifestSha256 = "";
        }
    }
}
