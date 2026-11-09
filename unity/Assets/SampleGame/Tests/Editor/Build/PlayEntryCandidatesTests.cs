#nullable enable

using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using OneStarMaker.Runtime.BuildContent.Distribution;
using SampleGame.DependOnAll.Editor.Build;
using UnityEngine;

namespace SampleGame.Tests.Editor.Build
{
    /// <summary>候補の選択（AC3）。一時 cache root へ実物の installer で install し、receipt・manifest・content の実 file で確かめる。</summary>
    public sealed class PlayEntryCandidatesTests
    {
        private string _root = "";
        private string _cache = "";

        [SetUp]
        public void SetUp()
        {
            _root = Path.Combine(Path.GetTempPath(), "osm-pe", Guid.NewGuid().ToString("N").Substring(0, 12));
            _cache = Path.Combine(_root, "cache");
            Directory.CreateDirectory(_root);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_root)) Directory.Delete(_root, true);
        }

        private PlayEntryCandidateSelection Select(string? target = null) =>
            PlayEntryCandidates.Select(_cache, SampleGameBuildPurposes.SpringFull, target ?? VerifyPlayLauncher.Target, SampleGameBuildPurposes.FirstSceneOf);

        [Test]
        public void MissingCache_HasNoCandidate_AndDoesNotCreateTheRoot()
        {
            var selection = Select();
            Assert.That(selection.Candidate, Is.Null);
            Assert.That(selection.Failure, Is.Empty);
            Assert.That(Directory.Exists(_cache), Is.False);
        }

        [Test]
        public async Task ValidRevision_IsSelected_WithTheDirectoryPlayValues()
        {
            var installed = await InstallAsync("rev-a", "spring-full", "Full", "Full");
            var selection = Select();
            Assert.That(selection.Candidate, Is.Not.Null, selection.Failure);
            Assert.That(selection.Candidate!.Revision, Is.EqualTo("rev-a"));
            Assert.That(selection.Candidate.HasMultiple, Is.False);
            var values = selection.Candidate.PlacedEnvironment.ToDictionary(x => x.Key, x => x.Value);
            Assert.That(values["SAMPLEGAME_CONTENT__RUNTIMEMODE"], Is.EqualTo("directory"));
            Assert.That(values["SAMPLEGAME_CONTENT__INSTALLEDREVISIONPATH"], Is.EqualTo(installed.RevisionRoot));
            Assert.That(values["SAMPLEGAME_CONTENT__MANIFESTSHA256"], Is.EqualTo(installed.ManifestSha256));
            Assert.That(values["SAMPLEGAME_CONTENT__BUILDIDENTITY"], Is.EqualTo("rev-a"));
            Assert.That(values["SAMPLEGAME_CONTENT__REPRESENTATION"], Is.EqualTo("Full"));
            Assert.That(values["SAMPLEGAME_CONTENT__FIRSTSCENE"], Is.EqualTo("Title"));
            Assert.That(values["SAMPLEGAME_WORLD__CELLCOMPANIONSET"], Is.EqualTo("Full"));
        }

        [Test]
        public async Task DifferentDeclarationRecordedInTheManifest_IsNotACandidate()
        {
            await InstallAsync("rev-a", "spring-planner-whitebox", "Whitebox", "Planner");
            var selection = Select();
            Assert.That(selection.Candidate, Is.Null);
            StringAssert.Contains("spring-planner-whitebox", selection.Failure);
        }

        [Test]
        public async Task OtherTarget_IsNotACandidate()
        {
            await InstallAsync("rev-a", "spring-full", "Full", "Full");
            var selection = Select("Android-Player");
            Assert.That(selection.Candidate, Is.Null);
            Assert.That(selection.Failure, Is.Empty);
        }

        [Test]
        public async Task CorruptedContent_FailsValidation_WithTheReason()
        {
            var installed = await InstallAsync("rev-a", "spring-full", "Full", "Full");
            File.WriteAllText(Path.Combine(installed.RevisionRoot, "content", "payload.bin"), "tampered bytes");
            var selection = Select();
            Assert.That(selection.Candidate, Is.Null);
            StringAssert.Contains("rev-a", selection.Failure);
        }

        [Test]
        public async Task CorruptReceipt_IsReported_NotSelected()
        {
            var installed = await InstallAsync("rev-a", "spring-full", "Full", "Full");
            File.WriteAllText(Path.Combine(installed.RevisionRoot, "receipt.json"), "{ not json");
            var selection = Select();
            Assert.That(selection.Candidate, Is.Null);
            StringAssert.Contains("receipt", selection.Failure);
        }

        [Test]
        public async Task SeveralRevisions_TheLatestInstallIsSelected()
        {
            var older = await InstallAsync("rev-older", "spring-full", "Full", "Full");
            var newer = await InstallAsync("rev-newer", "spring-full", "Full", "Full");
            SetInstalledUtc(older.RevisionRoot, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
            SetInstalledUtc(newer.RevisionRoot, new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc));
            var selection = Select();
            Assert.That(selection.Candidate!.Revision, Is.EqualTo("rev-newer"));
            Assert.That(selection.Candidate.HasMultiple, Is.True);
        }

        [Test]
        public async Task NewestFailsValidation_OlderIsUsed_AndTheSkipIsNamed()
        {
            var older = await InstallAsync("rev-older", "spring-full", "Full", "Full");
            var newer = await InstallAsync("rev-newer", "spring-full", "Full", "Full");
            SetInstalledUtc(older.RevisionRoot, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
            SetInstalledUtc(newer.RevisionRoot, new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc));
            File.WriteAllText(Path.Combine(newer.RevisionRoot, "content", "payload.bin"), "tampered bytes");
            var selection = Select();
            Assert.That(selection.Candidate!.Revision, Is.EqualTo("rev-older"));
            Assert.That(selection.Warnings.Single(), Does.Contain("rev-newer"), "黙って別の revision へ切り替えない");
        }

        // ─── fixture ───

        private async Task<InstalledContent> InstallAsync(string revision, string declaration, string representation, string companion)
        {
            var published = Path.Combine(_root, "published-" + revision);
            var digest = WritePublished(published, revision, declaration, representation, companion);
            var stages = new VerifyPlayLauncher.ReceivedStages("unused", () => null, false, SampleGameBuildPurposes.FirstSceneOf,
                new ContentDeliveryWindow.Settings { cacheRoot = _cache });
            var manifest = JsonUtility.FromJson<ContentTransportManifest>(File.ReadAllText(Path.Combine(published, "transport.json")));
            return await stages.InstallAsync(VerifyPlayLauncher.ToPublished(published, digest, manifest), CancellationToken.None);
        }

        private static void SetInstalledUtc(string revisionRoot, DateTime value)
        {
            var path = Path.Combine(revisionRoot, "receipt.json");
            var text = Regex.Replace(File.ReadAllText(path), "\"installedUtc\"\\s*:\\s*\"[^\"]*\"", "\"installedUtc\":\"" + value.ToString("o") + "\"");
            File.WriteAllText(path, text);
        }

        /// <summary>content set は常に spring-full で、manifest の宣言だけを変える（cache の entry と宣言の食い違いを作るため）。</summary>
        private static string WritePublished(string published, string revision, string declaration, string representation, string companion)
        {
            Directory.CreateDirectory(Path.Combine(published, "content"));
            var payload = Encoding.UTF8.GetBytes("play entry payload");
            File.WriteAllBytes(Path.Combine(published, "content", "payload.bin"), payload);
            var manifest = new ContentTransportManifest
            {
                version = 1, product = "OneStarMaker", contentSet = "spring-full", revision = revision,
                target = VerifyPlayLauncher.Target, unityVersion = Application.unityVersion,
                rootSchemaVersion = 2, playerConfigSchemaVersion = 1,
                files = new[] { new ContentTransportFile { path = "payload.bin", size = payload.Length, sha256 = Sha(payload) } },
                declaration = new ContentManifestDeclaration
                {
                    name = declaration, representation = representation,
                    runtimeDefaults = new[] { new ContentManifestDeclarationValue { key = "world:cellCompanionSet", value = companion } },
                },
            };
            var bytes = Encoding.UTF8.GetBytes(JsonUtility.ToJson(manifest));
            File.WriteAllBytes(Path.Combine(published, "transport.json"), bytes);
            return Sha(bytes);
        }

        private static string Sha(byte[] value)
        {
            using var hash = SHA256.Create();
            return BitConverter.ToString(hash.ComputeHash(value)).Replace("-", "").ToLowerInvariant();
        }
    }
}
