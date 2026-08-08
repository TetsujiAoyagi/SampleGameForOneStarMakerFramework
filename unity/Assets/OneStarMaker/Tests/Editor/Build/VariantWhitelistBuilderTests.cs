#nullable enable

using System.Collections.Generic;
using NUnit.Framework;
using OneStarMaker.Editor.Build;
using OneStarMaker.Runtime.AssetDescriptions;

namespace OneStarMaker.Tests.Editor.Build
{
    /// <summary>
    /// variant whitelist の解決規則を検証する。
    ///
    /// <para>
    /// 未指定なら既定 variant のみ。always-included な asset は variant 一致を問わず載る。
    /// 一致する variant が無いとき required は error になり optional はならない、が肝。
    /// </para>
    /// </summary>
    public sealed class VariantWhitelistBuilderTests
    {
        [Test]
        public void ResolveVariantWhitelist_EmptyConfiguredList_UsesDefaultVariantOnly()
        {
            var whitelist = VariantWhitelistBuilder.ResolveVariantWhitelist(new List<string>());

            Assert.That(whitelist, Is.EquivalentTo(new[] { string.Empty }));
        }

        [Test]
        public void Build_AlwaysIncludedAssets_AddsGuidWithoutVariantMatch()
        {
            const string AlwaysIncludedGuid = "dddddddddddddddddddddddddddddddd";
            var profile = UnityEngine.ScriptableObject.CreateInstance<BuildVariantProfile>();
            var field = typeof(BuildVariantProfile).GetField(
                "_alwaysIncludedAssets",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            field!.SetValue(profile, new List<UnityEngine.AddressableAssets.AssetReference>
            {
                new(AlwaysIncludedGuid),
            });

            var result = VariantWhitelistBuilder.Build(profile, new IAssetDescriptionSource[] { });

            Assert.That(result.HasErrors, Is.False);
            Assert.That(result.IncludedGuids, Does.Contain(AlwaysIncludedGuid));
        }

        [Test]
        public void Build_RequiredDescriptionWithoutMatchingVariant_AddsError()
        {
            var profile = UnityEngine.ScriptableObject.CreateInstance<BuildVariantProfile>();
            var source = new FakeSource(false, new AssetPayload
                {
                    Reference = new UnityEngine.AddressableAssets.AssetReference("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"),
                    Variant = "Whitebox",
                });

            var result = VariantWhitelistBuilder.Build(profile, new[] { source });
            Assert.That(result.HasErrors, Is.True);
        }

        [Test]
        public void Build_OptionalDescriptionWithoutMatchingVariant_DoesNotError()
        {
            var profile = UnityEngine.ScriptableObject.CreateInstance<BuildVariantProfile>();
            var source = new FakeSource(true, new AssetPayload
                {
                    Reference = new UnityEngine.AddressableAssets.AssetReference("bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb"),
                    Variant = "Whitebox",
                });

            var result = VariantWhitelistBuilder.Build(profile, new[] { source });
            Assert.That(result.HasErrors, Is.False);
            Assert.That(result.ExcludedGuids, Does.Contain("bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb"));
        }

        private sealed class FakeSource : IAssetDescriptionSource
        {
            private readonly bool _isOptional;
            private readonly FakeProvider _provider;

            public FakeSource(bool isOptional, params AssetPayload[] payloads)
            {
                _isOptional = isOptional;
                _provider = new FakeProvider(payloads);
            }

            public IEnumerable<CollectedAssetDescription> Collect(BuildVariantProfile profile)
            {
                yield return new CollectedAssetDescription(_provider, _isOptional, "Fake");
            }
        }

        private sealed class FakeProvider : IAssetPayloadProvider
        {
            public FakeProvider(params AssetPayload[] payloads)
                => Payloads = new List<AssetPayload>(payloads);

            public IReadOnlyList<AssetPayload> Payloads { get; }

            public string DisplayName => "Fake";
        }
    }
}
