#nullable enable

using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using OneStarMaker.Foundation.DebugSocket;
using OneStarMaker.Runtime.DebugSocketServices.Inspector;
using UnityEngine;

namespace OneStarMaker.Tests.Foundation
{
    /// <summary>
    /// DS-05: inspector builder の section / property 構築契約を固定する。
    /// </summary>
    [TestFixture]
    public sealed class DebugSocketInspectorBuilderTests
    {
        private GameObject _gameObject = null!;

        [SetUp]
        public void SetUp()
        {
            _gameObject = new GameObject("InspectorBuilderTestObject");
            _gameObject.tag = "Player";
            _gameObject.layer = 8;
        }

        [TearDown]
        public void TearDown()
        {
            if (_gameObject != null)
            {
                Object.DestroyImmediate(_gameObject);
            }
        }

        [Test]
        public void BuildInspectorSections_None_IncludesHeaderOnly()
        {
            // 守る契約: query flags なしでは GameObject header だけを返すこと。
            // 退行時の障害: 不要な Transform / Component 走査で取得コストが増える。
            const long targetId = 42;
            var sections = DebugSocketInspectorBuilder.BuildInspectorSections(
                targetId,
                _gameObject,
                _gameObject.scene,
                InspectorQueryFlags.None);

            Assert.That(sections, Has.Length.EqualTo(1));
            Assert.That(sections[0].SectionId, Is.EqualTo(1));
            Assert.That(sections[0].Kind, Is.EqualTo(InspectorSectionKind.Header));
            Assert.That(sections[0].DisplayName, Is.EqualTo("GameObject"));
            Assert.That(sections[0].Properties.Select(property => property.DisplayName), Is.EqualTo(new[]
            {
                "Name",
                "ActiveSelf",
                "ActiveInHierarchy",
                "ChildCount",
                "SiblingIndex",
            }));
        }

        [Test]
        public void BuildInspectorSections_FullQuery_IncludesTransformAndComponents()
        {
            // 守る契約: 既定相当の全 flag で Transform と付加 Component セクションが出ること。
            // 退行時の障害: viewer が component 一覧や transform 値を欠落する。
            _gameObject.AddComponent<BoxCollider>();
            const long targetId = 7;
            var queryFlags =
                InspectorQueryFlags.IncludeMetadata |
                InspectorQueryFlags.IncludeComponents |
                InspectorQueryFlags.IncludeProperties |
                InspectorQueryFlags.IncludeRawValues;

            var sections = DebugSocketInspectorBuilder.BuildInspectorSections(
                targetId,
                _gameObject,
                _gameObject.scene,
                queryFlags);

            Assert.That(sections, Has.Length.EqualTo(3));
            Assert.That(sections[0].DisplayName, Is.EqualTo("GameObject"));
            Assert.That(sections[1].DisplayName, Is.EqualTo("Transform"));
            Assert.That(sections[1].Kind, Is.EqualTo(InspectorSectionKind.Component));
            Assert.That(sections[2].DisplayName, Is.EqualTo("BoxCollider"));
            Assert.That(sections[2].TypeName, Does.EndWith("BoxCollider"));
        }

        [Test]
        public void BuildInspectorSections_PropertyIds_AreSequentialAcrossSections()
        {
            // 守る契約: section を跨いで PropertyId が 1 から連番であること。
            // 退行時の障害: viewer が property を再マップできず差分表示が壊れる。
            _gameObject.AddComponent<Camera>();
            const long targetId = 99;
            var queryFlags =
                InspectorQueryFlags.IncludeMetadata |
                InspectorQueryFlags.IncludeComponents |
                InspectorQueryFlags.IncludeProperties;

            var sections = DebugSocketInspectorBuilder.BuildInspectorSections(
                targetId,
                _gameObject,
                _gameObject.scene,
                queryFlags);

            var propertyIds = sections
                .SelectMany(section => section.Properties)
                .Select(property => property.PropertyId)
                .ToArray();

            Assert.That(propertyIds, Is.EqualTo(Enumerable.Range(1, propertyIds.Length)));
        }

        [Test]
        public void BuildInspectorSections_IncludeMetadata_AddsSceneTagLayerNodeToken()
        {
            // 守る契約: IncludeMetadata で scene-local 識別子だけを追加すること。
            // 退行時の障害: inspector metadata の Scene / NodeToken が欠落する。
            const long targetId = 123;
            var sections = DebugSocketInspectorBuilder.BuildInspectorSections(
                targetId,
                _gameObject,
                _gameObject.scene,
                InspectorQueryFlags.IncludeMetadata);

            var headerProperties = sections[0].Properties.ToDictionary(property => property.DisplayName);
            Assert.That(headerProperties["Scene"].ValueText, Is.EqualTo(_gameObject.scene.name));
            Assert.That(headerProperties["Tag"].ValueText, Is.EqualTo("Player"));
            Assert.That(headerProperties["Layer"].ValueText, Is.EqualTo("8"));
            Assert.That(headerProperties["NodeToken"].ValueText, Is.EqualTo("123"));
            Assert.That(headerProperties["NodeToken"].Path, Is.EqualTo("GameObject.NodeToken"));
        }

        [Test]
        public void BuildInspectorSections_IncludeRawValues_PreservesBooleanRawAndFillsTransformRaw()
        {
            // 守る契約: bool の canonical raw を維持し、Transform には machine-readable raw を載せること。
            // 退行時の障害: viewer 側の機械処理が display 文字列へ退化する。
            _gameObject.SetActive(true);
            const long targetId = 5;
            var queryFlags =
                InspectorQueryFlags.IncludeProperties |
                InspectorQueryFlags.IncludeRawValues;

            var sections = DebugSocketInspectorBuilder.BuildInspectorSections(
                targetId,
                _gameObject,
                _gameObject.scene,
                queryFlags);

            var headerProperties = sections[0].Properties.ToDictionary(property => property.DisplayName);
            Assert.That(headerProperties["ActiveSelf"].ValueText, Is.EqualTo("True"));
            Assert.That(headerProperties["ActiveSelf"].RawValue, Is.EqualTo("true"));
            Assert.That(headerProperties["Name"].RawValue, Is.EqualTo(_gameObject.name));

            var localRotation = sections[1].Properties.Single(property => property.DisplayName == "LocalRotation");
            Assert.That(localRotation.Unit, Is.EqualTo("deg"));
            Assert.That(localRotation.RawValue, Does.Match(@"^-?[\d\.Ee\+\-]+,-?[\d\.Ee\+\-]+,-?[\d\.Ee\+\-]+$"));
        }

        [Test]
        public void BuildInspectorSections_ComponentSections_ExcludeTransform()
        {
            // 守る契約: IncludeComponents でも Transform は独立セクションとして重複列挙しないこと。
            // 退行時の障害: Transform が component 一覧に二重出現する。
            const long targetId = 11;
            var sections = DebugSocketInspectorBuilder.BuildInspectorSections(
                targetId,
                _gameObject,
                _gameObject.scene,
                InspectorQueryFlags.IncludeComponents);

            Assert.That(sections, Has.Length.EqualTo(1));
            Assert.That(sections[0].DisplayName, Is.EqualTo("GameObject"));
        }

        [Test]
        public void BuildInspectorSections_CameraComponent_IncludesTypedPropertiesWithUnit()
        {
            // 守る契約: 型別プロパティの最小セットと単位表現を維持すること。
            // 退行時の障害: Camera の FOV など型固有表示が欠落する。
            var camera = _gameObject.AddComponent<Camera>();
            camera.fieldOfView = 55f;
            const long targetId = 21;
            var queryFlags =
                InspectorQueryFlags.IncludeComponents |
                InspectorQueryFlags.IncludeProperties |
                InspectorQueryFlags.IncludeRawValues;

            var sections = DebugSocketInspectorBuilder.BuildInspectorSections(
                targetId,
                _gameObject,
                _gameObject.scene,
                queryFlags);

            var cameraSection = sections.Single(section => section.DisplayName == "Camera");
            var properties = cameraSection.Properties.ToDictionary(property => property.DisplayName);

            Assert.That(properties["FieldOfView"].ValueText, Is.EqualTo("55"));
            Assert.That(properties["FieldOfView"].Unit, Is.EqualTo("deg"));
            Assert.That(properties["FieldOfView"].RawValue, Is.EqualTo("55"));
            Assert.That(properties["NearClipPlane"].ValueTypeId, Is.EqualTo(4));
            Assert.That(properties["FarClipPlane"].ValueTypeId, Is.EqualTo(4));
        }
    }
}
