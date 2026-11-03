#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using OneStarMaker.Runtime.BuildContent.Distribution;
using SampleGame.DependOnAll.Editor.Build;
using UnityEditor;
using UnityEngine;

namespace SampleGame.Tests.Editor.Build
{
    /// <summary>
    /// From published directory の照合（結果 file と published directory の組）、標準経路の cache root の既定、
    /// Delivery 窓の手の ledger との関係。published directory と結果 file は合成し、結果 file は BS5b-1 の書き手と同じ
    /// <see cref="SampleGameContentBatch.SuccessFile"/> / <see cref="SampleGameContentBatch.FailureFile"/> を JsonUtility で書く。
    /// </summary>
    public sealed class PublishedDirectoryReceiveTests
    {
        private const string LedgerKey = "OSM.ContentDelivery.PlayBridge.v1";
        private string _root = "";
        private string _published = "";
        private string _revision = "";
        private string _digest = "";
        private string _savedLedger = "";
        private readonly Dictionary<string, string?> _savedEnvironment = new();
        private readonly List<string> _defaultCacheRevisions = new();

        [SetUp]
        public void SetUp()
        {
            _root = Path.Combine(Path.GetTempPath(), "osm-bs5b2", Guid.NewGuid().ToString("N"));
            _revision = "bs5b2test" + Guid.NewGuid().ToString("N").Substring(0, 12);
            _published = Path.Combine(_root, "published");
            _digest = WritePublished(_published, _revision, "spring-full", "Full", "Full");
            _savedLedger = SessionState.GetString(LedgerKey, string.Empty);
            SessionState.EraseString(LedgerKey);
            foreach (var pair in ContentDeliveryPlayBridge.ContentValues("", "", "", ""))
                _savedEnvironment[pair.Key] = Environment.GetEnvironmentVariable(pair.Key);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var pair in _savedEnvironment) Environment.SetEnvironmentVariable(pair.Key, pair.Value);
            if (_savedLedger.Length == 0) SessionState.EraseString(LedgerKey);
            else SessionState.SetString(LedgerKey, _savedLedger);
            if (Directory.Exists(_root)) Directory.Delete(_root, true);
            foreach (var revisionRoot in _defaultCacheRevisions)
                if (Directory.Exists(revisionRoot)) Directory.Delete(revisionRoot, true);
        }

        private SampleGameContentBatch.SuccessFile Success(string? digest = null, string? identity = null, string declaration = "spring-full",
            string? publishPath = null) => new()
        {
            identity = identity ?? _revision, publishPath = publishPath ?? _published, manifestSha256 = digest ?? _digest,
            declaration = declaration, buildMode = "Full", exitCode = SampleGameContentBatch.ExitSuccess,
        };

        private static SampleGameContentBatch.SuccessFile Roundtrip(object written) =>
            JsonUtility.FromJson<SampleGameContentBatch.SuccessFile>(JsonUtility.ToJson(written, true));

        // ═══ AC15 ═══

        [Test]
        public void ResultFile_Success_IsAccepted_AndMatchesThePublishedDirectory()
        {
            var result = Roundtrip(Success());
            Assert.That(VerifyPlayLauncher.ValidateResultFile(result), Is.Null);
            Assert.That(VerifyPlayLauncher.ValidatePublished(result, _published, out var manifest, out var declaration), Is.Null);
            Assert.That(manifest!.revision, Is.EqualTo(_revision));
            Assert.That(declaration!.Name, Is.EqualTo("spring-full"));
        }

        [Test]
        public void ResultFile_FailureFile_IsRejected()
        {
            var result = Roundtrip(new SampleGameContentBatch.FailureFile { exitCode = SampleGameContentBatch.ExitBuildFailed, reason = "x" });
            Assert.That(VerifyPlayLauncher.ValidateResultFile(result), Does.Contain("exitCode=4"));
        }

        [Test]
        public void ResultFile_MissingFields_AreRejected()
        {
            var result = Roundtrip(Success());
            result.identity = "";
            Assert.That(VerifyPlayLauncher.ValidateResultFile(result), Does.Contain("identity"));
        }

        [Test]
        public void ResultFile_ShaMismatch_IsRejectedBeforeInstall()
        {
            var changed = (_digest[0] == '0' ? "1" : "0") + _digest.Substring(1);
            Assert.That(VerifyPlayLauncher.ValidatePublished(Roundtrip(Success(digest: changed)), _published, out _, out _),
                Does.Contain("manifestSha256"));
        }

        [Test]
        public void ResultFile_IdentityMismatch_IsRejected()
        {
            Assert.That(VerifyPlayLauncher.ValidatePublished(Roundtrip(Success(identity: "other")), _published, out _, out _),
                Does.Contain("batch identity"));
        }

        [Test]
        public void ResultFile_DeclarationMismatch_IsRejected()
        {
            Assert.That(VerifyPlayLauncher.ValidatePublished(Roundtrip(Success(declaration: "spring-whitebox")), _published, out _, out _),
                Does.Contain("declaration"));
        }

        [Test]
        public void Manifest_LocalDeclarationFieldsDiffer_IsRejected()
        {
            var other = Path.Combine(_root, "planner-as-full");
            var digest = WritePublished(other, _revision, "spring-planner-whitebox", "Full", "Planner");
            Assert.That(VerifyPlayLauncher.ValidatePublished(Roundtrip(Success(digest: digest, declaration: "spring-planner-whitebox",
                publishPath: other)), other, out _, out _), Does.Contain("representation"));
        }

        [Test]
        public void Manifest_WithoutDeclaration_PointsToTheManualRoute()
        {
            var other = Path.Combine(_root, "no-declaration");
            var digest = WritePublished(other, _revision, "", "", null);
            Assert.That(VerifyPlayLauncher.ValidatePublished(Roundtrip(Success(digest: digest, publishPath: other)), other, out _, out _),
                Does.Contain("Tools/OSM/Content/Delivery"));
        }

        [Test]
        public void PublishPath_Absent_AsksForTheFolder()
        {
            var chosen = 0;
            var result = Roundtrip(Success(publishPath: Path.Combine(_root, "elsewhere")));
            Assert.That(VerifyPlayLauncher.ResolvePublishedDirectory(result, () => { chosen++; return _published; }), Is.EqualTo(_published));
            Assert.That(chosen, Is.EqualTo(1));
            Assert.That(VerifyPlayLauncher.ResolvePublishedDirectory(Roundtrip(Success()), () => { chosen++; return null; }), Is.EqualTo(_published));
            Assert.That(chosen, Is.EqualTo(1), "publishPath が手元に有れば folder を選ばせない");
        }

        // ═══ AC20: cacheRoot="" から install できる ═══

        [Test]
        public async Task EmptyCacheRoot_InstallsUnderTheDefaultRoot()
        {
            var stages = new VerifyPlayLauncher.ReceivedStages("unused", () => null, false, SampleGameBuildPurposes.FirstSceneOf,
                new ContentDeliveryWindow.Settings { cacheRoot = "" });
            Assert.That(stages.CacheRoot, Is.EqualTo(VerifyPlayLauncher.ResolveCacheRoot("")));
            _defaultCacheRevisions.Add(Path.Combine(stages.CacheRoot, "installed", "spring-full", _revision));
            var published = VerifyPlayLauncher.ToPublished(_published, _digest, ReadManifest(_published));
            var installed = await stages.InstallAsync(published, CancellationToken.None);
            Assert.That(Path.GetFullPath(installed.RevisionRoot), Is.EqualTo(Path.GetFullPath(_defaultCacheRevisions[0])));
            Assert.That(installed.Revision, Is.EqualTo(_revision));
        }

        // ═══ AC4: 窓の Use For Next Play と 1 回の Play で戻す ledger ═══

        [Test]
        public async Task WindowUseForNextPlay_ResetsLeftoverOnePlayLedger_AndStaysUntilManualReset()
        {
            var cacheRoot = Path.Combine(_root, "cache");
            var stages = new VerifyPlayLauncher.ReceivedStages("unused", () => null, false, SampleGameBuildPurposes.FirstSceneOf,
                new ContentDeliveryWindow.Settings { cacheRoot = cacheRoot });
            var installed = await stages.InstallAsync(VerifyPlayLauncher.ToPublished(_published, _digest, ReadManifest(_published)),
                CancellationToken.None);

            const string firstScene = "SAMPLEGAME_CONTENT__FIRSTSCENE";
            var savedFirstScene = Environment.GetEnvironmentVariable(firstScene);
            try
            {
                Environment.SetEnvironmentVariable(firstScene, null);
                ContentDeliveryPlayBridge.ApplyForOnePlay(new[] { new KeyValuePair<string, string>(firstScene, "Title") });
                ContentDeliveryPlayBridge.Apply(installed.RevisionRoot, installed.ManifestSha256, installed.Revision, "spring-full", "Full");
                Assert.That(Environment.GetEnvironmentVariable(firstScene), Is.Null, "残った 1 回の Play で戻す値は先に戻す");
                Assert.That(Environment.GetEnvironmentVariable("SAMPLEGAME_CONTENT__RUNTIMEMODE"), Is.EqualTo("directory"));
                Assert.That(ContentDeliveryPlayBridge.HasManualLedger, Is.True);

                ContentDeliveryPlayBridge.HandlePlayModeStateChanged(PlayModeStateChange.EnteredEditMode);
                ContentDeliveryPlayBridge.HandleIdle(isPlaying: false, isPlayingOrWillChangePlaymode: false);
                Assert.That(Environment.GetEnvironmentVariable("SAMPLEGAME_CONTENT__MANIFESTSHA256"), Is.EqualTo(installed.ManifestSha256),
                    "手の ledger は Play の終了でも idle でも戻さない");

                ContentDeliveryPlayBridge.Reset();
                Assert.That(ContentDeliveryPlayBridge.HasManualLedger, Is.False);
            }
            finally { Environment.SetEnvironmentVariable(firstScene, savedFirstScene); }
        }

        // ─── fixture ───

        private static ContentTransportManifest ReadManifest(string published) =>
            JsonUtility.FromJson<ContentTransportManifest>(File.ReadAllText(Path.Combine(published, "transport.json")));

        /// <summary>1 file の content と、宣言を持つ transport.json を書き、transport.json の SHA-256 を返す。</summary>
        private static string WritePublished(string published, string revision, string declaration, string representation, string? companion)
        {
            Directory.CreateDirectory(Path.Combine(published, "content"));
            var payload = Encoding.UTF8.GetBytes("bs5b2 payload");
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
                    runtimeDefaults = companion == null
                        ? Array.Empty<ContentManifestDeclarationValue>()
                        : new[] { new ContentManifestDeclarationValue { key = "world:cellCompanionSet", value = companion } },
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
