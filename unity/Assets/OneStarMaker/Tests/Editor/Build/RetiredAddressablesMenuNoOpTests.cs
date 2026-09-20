#nullable enable

using System;
using System.Collections;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using OneStarMaker.Editor.Build;
using UnityEngine;
using UnityEngine.TestTools;

namespace OneStarMaker.Tests.Editor.Build
{
    public sealed class RetiredAddressablesMenuNoOpTests
    {
        [Test]
        public void RetiredMenus_DoNotMutateAddressablesOrAppConfig()
        {
            var settings = TryGetAddressableSettings();
            var builderCount = CountMembers(settings, "DataBuilders");
            var groupCount = CountMembers(settings, "groups");
            var configPath = Path.GetFullPath(Path.Combine(Application.dataPath, "SampleGame/Config/app-config.json"));
            var originalConfig = File.Exists(configPath) ? File.ReadAllBytes(configPath) : null;

            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("Hybrid Play Mode registration is retired"));
            VariantHybridPlayModeRegistrar.Register();

            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("Remote Addressables setup is retired"));
            VariantRemoteBuildSetup.Setup();

            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("Active Variant Player overlay is retired"));
            VariantPlayerBuild.BuildActiveVariant();

            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("Checkout Report is retired"));
            VariantCheckoutReportWindow.Open();

            if (settings != null)
            {
                Assert.That(CountMembers(settings, "DataBuilders"), Is.EqualTo(builderCount));
                Assert.That(CountMembers(settings, "groups"), Is.EqualTo(groupCount));
            }
            if (originalConfig != null)
                Assert.That(File.ReadAllBytes(configPath), Is.EqualTo(originalConfig));
        }

        // Tests.Editor は Unity.Addressables.Editor を参照しない。WorldCompanion テストと同じ reflection 境界。
        private static object? TryGetAddressableSettings()
        {
            var type = Type.GetType(
                "UnityEditor.AddressableAssets.AddressableAssetSettingsDefaultObject, Unity.Addressables.Editor");
            return type?.GetProperty("Settings", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
        }

        private static int CountMembers(object? settings, string propertyName)
        {
            if (settings == null) return 0;
            var value = settings.GetType().GetProperty(propertyName)?.GetValue(settings);
            if (value is ICollection collection) return collection.Count;
            return 0;
        }
    }
}
