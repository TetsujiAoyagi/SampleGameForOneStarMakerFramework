#nullable enable

using System.IO;
using NUnit.Framework;
using OneStarMaker.Editor.Build;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEngine;
using UnityEngine.TestTools;

namespace OneStarMaker.Tests.Editor.Build
{
    public sealed class RetiredAddressablesMenuNoOpTests
    {
        [Test]
        public void RetiredMenus_DoNotMutateAddressablesOrAppConfig()
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            var builderCount = settings != null ? settings.DataBuilders.Count : 0;
            var groupCount = settings != null ? settings.groups.Count : 0;
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
                Assert.That(settings.DataBuilders.Count, Is.EqualTo(builderCount));
                Assert.That(settings.groups.Count, Is.EqualTo(groupCount));
            }
            if (originalConfig != null)
                Assert.That(File.ReadAllBytes(configPath), Is.EqualTo(originalConfig));
        }
    }
}
