#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using OneStarMaker.Editor.Build.Content;

namespace OneStarMaker.Tests.Editor.Build
{
    /// <summary>
    /// Scene に焼き込む prefab の版の印の純粋な規則（HANDOFF CONTENT_PREFAB_STAMP §5 の単体テスト、AC9 の前半）。
    /// Unity と AssetDatabase を使わない。
    /// </summary>
    public sealed class ScenePrefabStampRulesTests
    {
        private const string Hash = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

        private static string Meta(string userDataLine, string newline = "\n") =>
            string.Join(newline, "fileFormatVersion: 2", "guid: 00000000000000000000000000000001", "DefaultImporter:",
                "  externalObjects: {}", userDataLine, "  assetBundleName: ", "  assetBundleVariant: ", "");

        private static readonly Dictionary<string, StampProbe> Probes = new Dictionary<string, StampProbe>
        {
            ["Assets/S.unity"] = new StampProbe(false, ""),
            ["Assets/A.prefab"] = new StampProbe(true, "ka"),
            ["Assets/B.fbx"] = new StampProbe(true, "kb"),
            ["Assets/T.png"] = new StampProbe(false, ""),
        };

        private static StampProbe Probe(string path) => Probes[path];

        [Test]
        public void IsScene_OnlyUnityFiles()
        {
            Assert.That(ScenePrefabStampRules.IsScene("Assets/S.unity"), Is.True);
            Assert.That(ScenePrefabStampRules.IsScene("Assets/S.UNITY"), Is.True);
            Assert.That(ScenePrefabStampRules.IsScene("Assets/A.prefab"), Is.False);
            Assert.That(ScenePrefabStampRules.IsScene("Assets/S.unity.meta"), Is.False);
        }

        [Test]
        public void StampHash_IsNullWithoutTargets()
        {
            Assert.That(ScenePrefabStampRules.StampHash(new[] { "Assets/S.unity", "Assets/T.png" }, Probe), Is.Null);
        }

        [Test]
        public void StampHash_DoesNotDependOnClosureOrder()
        {
            var a = ScenePrefabStampRules.StampHash(new[] { "Assets/S.unity", "Assets/A.prefab", "Assets/B.fbx", "Assets/T.png" }, Probe);
            var b = ScenePrefabStampRules.StampHash(new[] { "Assets/T.png", "Assets/B.fbx", "Assets/S.unity", "Assets/A.prefab" }, Probe);
            Assert.That(a, Is.Not.Null);
            Assert.That(b, Is.EqualTo(a));
            Assert.That(a, Does.Match("^[0-9a-f]{64}$"));
        }

        [Test]
        public void StampHash_ChangesWithKeyAndPath_ButNotWithNonTargets()
        {
            var closure = new[] { "Assets/S.unity", "Assets/A.prefab", "Assets/T.png" };
            var baseline = ScenePrefabStampRules.StampHash(closure, Probe);
            var keyChanged = ScenePrefabStampRules.StampHash(closure, p => p == "Assets/A.prefab" ? new StampProbe(true, "ka2") : Probe(p));
            var pathChanged = ScenePrefabStampRules.StampHash(new[] { "Assets/S.unity", "Assets/A2.prefab", "Assets/T.png" },
                p => p == "Assets/A2.prefab" ? new StampProbe(true, "ka") : Probe(p));
            var nonTargetAdded = ScenePrefabStampRules.StampHash(closure.Concat(new[] { "Assets/U.mat" }),
                p => p == "Assets/U.mat" ? new StampProbe(false, "") : Probe(p));
            var nonTargetKey = ScenePrefabStampRules.StampHash(closure, p => p == "Assets/T.png" ? new StampProbe(false, "other") : Probe(p));
            Assert.That(keyChanged, Is.Not.EqualTo(baseline));
            Assert.That(pathChanged, Is.Not.EqualTo(baseline));
            Assert.That(nonTargetAdded, Is.EqualTo(baseline));
            Assert.That(nonTargetKey, Is.EqualTo(baseline));
        }

        [Test]
        public void StampHash_DecidedOnlyByPathsAndProbe()
        {
            // 同じ path の列と同じ probe の結果なら、呼ぶたびに同じ値（外の状態を読まない）。
            var closure = new[] { "Assets/A.prefab", "Assets/B.fbx" };
            Assert.That(ScenePrefabStampRules.StampHash(closure, Probe), Is.EqualTo(ScenePrefabStampRules.StampHash(closure, Probe)));
            Assert.That(ScenePrefabStampRules.StampHash(closure.Concat(closure), Probe),
                Is.EqualTo(ScenePrefabStampRules.StampHash(closure, Probe)));
        }

        [TestCase("  userData: ", "Empty")]
        [TestCase("  userData:", "Empty")]
        [TestCase("  userData:  ", "Other")]
        [TestCase("  userData:\t", "Other")]
        [TestCase("  userData: \t", "Other")]
        [TestCase("  userData: value", "Other")]
        [TestCase("  userData: |", "Other")]
        [TestCase("  userData: osm-prefab-stamp-n" + Hash, "OwnStamp")]
        [TestCase("  userData: osm-prefab-stamp-s" + Hash, "OwnStamp")]
        [TestCase("  userData: osm-prefab-stamp-x" + Hash, "Other")]
        [TestCase("  userData: osm-prefab-stamp-n" + Hash + "0", "Other")]
        [TestCase("  userData: osm-prefab-stamp-n0123456789ABCDEF0123456789abcdef0123456789abcdef0123456789abcdef", "Other")]
        [TestCase("  userData: osm-prefab-stamp-n", "Other")]
        [TestCase("  userData:  osm-prefab-stamp-n" + Hash, "Other")]
        [TestCase("  userData: osm-prefab-stamp-n" + Hash + " ", "Other")]
        public void FindUserData_ClassifiesValue(string line, string expected)
        {
            Assert.That(ScenePrefabStampRules.FindUserData(Meta(line)).State.ToString(), Is.EqualTo(expected));
        }

        [Test]
        public void FindUserData_StopsOnZeroOrTwoLines()
        {
            Assert.That(ScenePrefabStampRules.FindUserData(Meta("  other: x")).State, Is.EqualTo(UserDataState.Other));
            Assert.That(ScenePrefabStampRules.FindUserData(Meta("  userData: \n  userData: ")).State, Is.EqualTo(UserDataState.Other));
        }

        [Test]
        public void FindUserData_MultiLineValueIsOther()
        {
            // 複数行の値は、1 行目の値が空にならない（ブロックの指示子・引用の始まり）ので止まる。
            Assert.That(ScenePrefabStampRules.FindUserData(Meta("  userData: >-\n    first\n    second")).State, Is.EqualTo(UserDataState.Other));
            Assert.That(ScenePrefabStampRules.FindUserData(Meta("  userData: 'a\n    b'")).State, Is.EqualTo(UserDataState.Other));
        }

        [TestCase("\n")]
        [TestCase("\r\n")]
        public void WriteStamp_KeepsOtherLinesAndNewlines_AndRoundTrips(string newline)
        {
            foreach (var empty in new[] { "  userData: ", "  userData:" })
            {
                var original = Meta(empty, newline);
                var stamped = ScenePrefabStampRules.WriteStamp(original, Hash);
                var expectedLine = "  userData: " + ScenePrefabStampRules.Prefix + (empty.EndsWith(" ") ? "s" : "n") + Hash;
                Assert.That(stamped, Is.EqualTo(Meta(expectedLine, newline)));
                Assert.That(ScenePrefabStampRules.FindUserData(stamped).State, Is.EqualTo(UserDataState.OwnStamp));
                Assert.That(ScenePrefabStampRules.RestoreStampLine(stamped), Is.EqualTo(original));
            }
        }

        [Test]
        public void WriteStamp_KeepsNonAsciiBytes()
        {
            var bytes = new byte[] { 0xEF, 0xBB, 0xBF }.Concat(ScenePrefabStampRules.ToBytes(Meta("  userData: ")))
                .Concat(new byte[] { 0xE3, 0x81, 0x82, 0x0A }).ToArray();
            var text = ScenePrefabStampRules.ToText(bytes);
            var restored = ScenePrefabStampRules.ToBytes(ScenePrefabStampRules.RestoreStampLine(ScenePrefabStampRules.WriteStamp(text, Hash)));
            Assert.That(restored, Is.EqualTo(bytes));
        }

        [Test]
        public void StampString_UsesOnlyAlphanumericsAndHyphen()
        {
            var line = ScenePrefabStampRules.FindUserData(ScenePrefabStampRules.WriteStamp(Meta("  userData: "), Hash));
            Assert.That(line.Value.Substring(1), Does.Match("^[A-Za-z0-9-]+$"));
        }

        [Test]
        public void WriteStamp_RefusesNonEmptyUserDataAndBadHash()
        {
            Assert.Throws<InvalidOperationException>(() => ScenePrefabStampRules.WriteStamp(Meta("  userData: value"), Hash));
            Assert.Throws<InvalidOperationException>(() => ScenePrefabStampRules.WriteStamp(Meta("  userData: osm-prefab-stamp-n" + Hash), Hash));
            Assert.Throws<ArgumentException>(() => ScenePrefabStampRules.WriteStamp(Meta("  userData: "), "abc"));
            Assert.Throws<ArgumentException>(() => ScenePrefabStampRules.WriteStamp(Meta("  userData: "), Hash.ToUpperInvariant()));
        }

        [Test]
        public void DecideRestore_FollowsTheFourCases()
        {
            Assert.That(ScenePrefabStampRules.DecideRestore(false, "", "o", "w", UserDataState.Other), Is.EqualTo(StampRestoreKind.Missing));
            Assert.That(ScenePrefabStampRules.DecideRestore(true, "w", "o", "w", UserDataState.OwnStamp), Is.EqualTo(StampRestoreKind.RestoreOriginal));
            Assert.That(ScenePrefabStampRules.DecideRestore(true, "o", "o", "w", UserDataState.Empty), Is.EqualTo(StampRestoreKind.AlreadyOriginal));
            Assert.That(ScenePrefabStampRules.DecideRestore(true, "x", "o", "w", UserDataState.OwnStamp), Is.EqualTo(StampRestoreKind.RestoreLineOnly));
            Assert.That(ScenePrefabStampRules.DecideRestore(true, "x", "o", "w", UserDataState.Empty), Is.EqualTo(StampRestoreKind.LeaveForeign));
            Assert.That(ScenePrefabStampRules.DecideRestore(true, "x", "o", "w", UserDataState.Other), Is.EqualTo(StampRestoreKind.LeaveForeign));
        }

        [Test]
        public void Restored_LineOnlyKeepsOtherEdits()
        {
            var original = Meta("  userData: ");
            var editedAfterStamp = ScenePrefabStampRules.WriteStamp(original, Hash).Replace("assetBundleName: ", "assetBundleName: edited");
            var restored = ScenePrefabStampRules.Restored(StampRestoreKind.RestoreLineOnly, editedAfterStamp, original);
            Assert.That(restored, Is.EqualTo(original.Replace("assetBundleName: ", "assetBundleName: edited")));
            Assert.That(ScenePrefabStampRules.Restored(StampRestoreKind.LeaveForeign, editedAfterStamp, original), Is.Null);
            Assert.That(ScenePrefabStampRules.Restored(StampRestoreKind.AlreadyOriginal, original, original), Is.Null);
            Assert.That(ScenePrefabStampRules.Restored(StampRestoreKind.RestoreOriginal, editedAfterStamp, original), Is.EqualTo(original));
        }

        [Test]
        public void EffectiveOriginal_UsesRestoredTextForALeftoverThatIsStampedAgain()
        {
            // 前回の印が残った Scene を今回も印の対象にする時、今回の記録の元は戻した後の text（file には書かない）。
            var original = Meta("  userData:", "\r\n");
            var leftover = ScenePrefabStampRules.WriteStamp(original, Hash);
            Assert.That(ScenePrefabStampRules.EffectiveOriginal(StampRestoreKind.RestoreOriginal, leftover, original), Is.EqualTo(original));
            // 記録が無い（Library が消された）時は、印の行だけを戻した text。
            Assert.That(ScenePrefabStampRules.EffectiveOriginal(StampRestoreKind.RestoreLineOnly, leftover, leftover), Is.EqualTo(original));
            // 戻す物が無い時は今の text。
            Assert.That(ScenePrefabStampRules.EffectiveOriginal(StampRestoreKind.AlreadyOriginal, original, original), Is.EqualTo(original));
        }
    }
}
