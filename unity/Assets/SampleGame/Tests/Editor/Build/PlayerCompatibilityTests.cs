#nullable enable

using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using NUnit.Framework;
using SampleGame.DependOnAll.Editor.Build;

namespace SampleGame.Tests.Editor.Build
{
    /// <summary>Player の入力の指紋の集合の規則・書式・比較と、<c>player-compat.json</c> の読み書き（Unity を使わない）。</summary>
    public sealed class PlayerCompatibilityTests
    {
        private static string Sha(string text)
        {
            using var sha = SHA256.Create();
            return string.Concat(sha.ComputeHash(Encoding.UTF8.GetBytes(text)).Select(x => x.ToString("x2")));
        }

        [Test]
        public void Select_KeepsPlayerInputs_AndDropsEditorOnlySettingsAndMutationFiles()
        {
            var selected = PlayerCompatibility.Select(new[]
            {
                "ProjectSettings/ProjectSettings.asset",
                "ProjectSettings/ProjectVersion.txt",
                "ProjectSettings/TagManager.asset",
                "ProjectSettings/Packages/com.example/Settings.json",
                "ProjectSettings/EditorSettings.asset",
                "ProjectSettings/EditorBuildSettings.asset",
                "ProjectSettings/PackageManagerSettings.asset",
                "ProjectSettings/VersionControlSettings.asset",
                "ProjectSettings/PresetManager.asset",
                "Packages/manifest.json",
                "Packages/packages-lock.json",
                "Assets/SampleGame/DependOnAll/PlayerStart/PlayablePlayerStart.unity",
                "Assets/SampleGame/DependOnAll/PlayerStart/PlayablePlayerStart.unity.meta",
                "Assets/Resources/BillingMode.json",
                "Assets/Resources/BS4/20261124T000000000Z-x/.bs4-owner",
                "Assets/Resources/BS4.meta",
                "Assets/StreamingAssets/bs4-runtime.json",
                "Assets/StreamingAssets/bs4-runtime.json.meta",
                "Assets/StreamingAssets/other.bin",
                @"Assets\Settings\PC_RPAsset.asset",
                "Packages/manifest.json",
            });
            Assert.That(selected, Is.EqualTo(new[]
            {
                "Assets/Resources/BillingMode.json",
                "Assets/SampleGame/DependOnAll/PlayerStart/PlayablePlayerStart.unity",
                "Assets/SampleGame/DependOnAll/PlayerStart/PlayablePlayerStart.unity.meta",
                "Assets/Settings/PC_RPAsset.asset",
                "Assets/StreamingAssets/other.bin",
                "Packages/manifest.json",
                "Packages/packages-lock.json",
                "ProjectSettings/Packages/com.example/Settings.json",
                "ProjectSettings/ProjectSettings.asset",
                "ProjectSettings/ProjectVersion.txt",
                "ProjectSettings/TagManager.asset",
            }));
        }

        [Test]
        public void Fingerprint_UsesTheOverlayDigestFormat_InOrdinalOrder()
        {
            var entries = new[] { new PlayerInputFileEntry("b.txt", "22"), new PlayerInputFileEntry("B.txt", "11") };
            Assert.That(PlayerCompatibility.Fingerprint(entries), Is.EqualTo(Sha("B.txt\n11\nb.txt\n22\n")));
            Assert.That(PlayerCompatibility.Fingerprint(entries.Reverse()), Is.EqualTo(PlayerCompatibility.Fingerprint(entries)));
        }

        [Test]
        public void Compare_ListsChangedAddedAndRemovedFiles()
        {
            var record = PlayerCompatibility.CreateRecord("id", new[]
            {
                new PlayerInputFileEntry("ProjectSettings/TagManager.asset", "aa"),
                new PlayerInputFileEntry("Packages/manifest.json", "bb"),
                new PlayerInputFileEntry("Assets/Resources/gone.txt", "cc"),
            });
            Assert.That(PlayerCompatibility.Compare(record, record.files), Is.Empty);
            var differences = PlayerCompatibility.Compare(record, new[]
            {
                new PlayerInputFileEntry("ProjectSettings/TagManager.asset", "AA"),
                new PlayerInputFileEntry("Packages/manifest.json", "b0"),
                new PlayerInputFileEntry("Assets/Resources/new.txt", "dd"),
            });
            Assert.That(differences, Is.EqualTo(new[]
            {
                "Assets/Resources/gone.txt (removed)",
                "Assets/Resources/new.txt (added)",
                "Packages/manifest.json (changed)",
            }), "hash の大小文字の違いは同じ値");
        }

        [Test]
        public void Record_RoundTrips_AndRejectsATamperedFileList()
        {
            var record = PlayerCompatibility.CreateRecord("20261124T000000000Z-a", new[] { new PlayerInputFileEntry("x", "1"), new PlayerInputFileEntry("a", "2") });
            var read = PlayerCompatibility.FromJson(PlayerCompatibility.ToJson(record));
            Assert.That(read.identity, Is.EqualTo(record.identity));
            Assert.That(read.digest, Is.EqualTo(record.digest));
            Assert.That(read.files.Select(x => x.path), Is.EqualTo(new[] { "a", "x" }));

            var tampered = PlayerCompatibility.ToJson(record).Replace("\"1\"", "\"9\"");
            Assert.Throws<FormatException>(() => PlayerCompatibility.FromJson(tampered));
            Assert.Throws<FormatException>(() => PlayerCompatibility.FromJson(PlayerCompatibility.ToJson(record).Replace("\"schemaVersion\": 1", "\"schemaVersion\": 2")));
            Assert.Throws<FormatException>(() => PlayerCompatibility.FromJson("{}"));
        }
    }
}
