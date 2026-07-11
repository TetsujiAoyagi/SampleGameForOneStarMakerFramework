#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using OneStarMaker.Foundation.DebugSocket;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OneStarMaker.Runtime.DebugSocketServices.Inspector
{
    /// <summary>
    /// 有効な <see cref="GameObject"/> と <see cref="Scene"/> から inspector DTO セクションを構築する。
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="InspectorQueryFlags"/> は取得コストと公開範囲を決める。
    /// Metadata / Components / Properties / RawValues を個別に有効化できるため、
    /// viewer が必要な粒度だけを要求し、不要な Unity API 走査や wire 量を抑えられる。
    /// </para>
    /// <para>
    /// builder は <see cref="GameObject"/> / <see cref="Component"/> など Unity API を直接読むため、
    /// 呼び出し元は main thread 上で実行すること。target token 解決や fault frame 制御は service 側の責務。
    /// </para>
    /// </remarks>
    internal static class DebugSocketInspectorBuilder
    {
        public static InspectorSectionDtoV1[] BuildInspectorSections(
            long targetId,
            GameObject gameObject,
            Scene scene,
            InspectorQueryFlags queryFlags)
        {
            var sections = new List<InspectorSectionDtoV1>();
            var sectionId = 1;
            var propertyId = 1;
            var includeMetadata = (queryFlags & InspectorQueryFlags.IncludeMetadata) != 0;
            var includeComponents = (queryFlags & InspectorQueryFlags.IncludeComponents) != 0;
            var includeProperties = (queryFlags & InspectorQueryFlags.IncludeProperties) != 0;
            var includeRawValues = (queryFlags & InspectorQueryFlags.IncludeRawValues) != 0;

            sections.Add(new InspectorSectionDtoV1
            {
                SectionId = sectionId++,
                Kind = InspectorSectionKind.Header,
                TypeId = 1,
                DisplayName = "GameObject",
                TypeName = nameof(GameObject),
                Properties = BuildGameObjectProperties(targetId, gameObject, scene, includeMetadata, includeRawValues, ref propertyId),
            });

            if (includeProperties)
            {
                sections.Add(new InspectorSectionDtoV1
                {
                    SectionId = sectionId++,
                    Kind = InspectorSectionKind.Component,
                    TypeId = 2,
                    DisplayName = "Transform",
                    TypeName = nameof(Transform),
                    Properties = BuildTransformProperties(gameObject.transform, includeRawValues, ref propertyId),
                });
            }

            if (includeComponents)
            {
                var components = gameObject.GetComponents<Component>();
                for (var index = 0; index < components.Length; index++)
                {
                    var component = components[index];
                    if (component == null || component is Transform)
                    {
                        continue;
                    }

                    sections.Add(new InspectorSectionDtoV1
                    {
                        SectionId = sectionId++,
                        Kind = InspectorSectionKind.Component,
                        TypeId = 3,
                        DisplayName = component.GetType().Name,
                        TypeName = component.GetType().FullName,
                        Properties = BuildComponentProperties(component, includeProperties, includeRawValues, ref propertyId),
                    });
                }
            }

            return sections.ToArray();
        }

        private static InspectorPropertyDtoV1[] BuildGameObjectProperties(
            long targetId,
            GameObject gameObject,
            Scene scene,
            bool includeMetadata,
            bool includeRawValues,
            ref int propertyId)
        {
            var properties = new List<InspectorPropertyDtoV1>
            {
                CreateInspectorProperty(ref propertyId, "Name", gameObject.name, ValueTypeId.Utf16String, path: "GameObject.Name"),
                CreateInspectorProperty(ref propertyId, "ActiveSelf", gameObject.activeSelf, ValueTypeId.Boolean, path: "GameObject.ActiveSelf"),
                CreateInspectorProperty(ref propertyId, "ActiveInHierarchy", gameObject.activeInHierarchy, ValueTypeId.Boolean, path: "GameObject.ActiveInHierarchy"),
                CreateInspectorProperty(ref propertyId, "ChildCount", gameObject.transform.childCount, ValueTypeId.Int32, path: "GameObject.ChildCount"),
                CreateInspectorProperty(ref propertyId, "SiblingIndex", gameObject.transform.GetSiblingIndex(), ValueTypeId.Int32, path: "GameObject.SiblingIndex"),
            };

            if (includeMetadata)
            {
                // 以前は Unity 内部の InstanceId / SceneHandle をそのまま露出していたが、
                // Unity 6.5 以降は API 変更の影響を強く受けるうえ、
                // viewer 側が本当に必要としているのは「このノードを安定して識別できること」だけだった。
                // そこで inspector metadata には service-local token を文字列で載せ、
                // 生の engine identity は wire へ出さないようにする。
                properties.Add(CreateInspectorProperty(ref propertyId, "Scene", scene.name, ValueTypeId.Utf16String, path: "GameObject.Scene"));
                properties.Add(CreateInspectorProperty(ref propertyId, "Tag", gameObject.tag, ValueTypeId.Utf16String, path: "GameObject.Tag"));
                properties.Add(CreateInspectorProperty(ref propertyId, "Layer", gameObject.layer, ValueTypeId.Int32, path: "GameObject.Layer"));
                properties.Add(CreateInspectorProperty(ref propertyId, "NodeToken", targetId.ToString(CultureInfo.InvariantCulture), ValueTypeId.Utf16String, path: "GameObject.NodeToken"));
            }

            if (includeRawValues)
            {
                for (var index = 0; index < properties.Count; index++)
                {
                    // bool など、すでに canonical な rawValue を持っている項目は上書きしない。
                    // display 用の "True"/"False" で raw を潰すと、viewer 側で機械処理しづらくなる。
                    properties[index].RawValue ??= properties[index].ValueText;
                }
            }

            return properties.ToArray();
        }

        private static InspectorPropertyDtoV1[] BuildTransformProperties(
            Transform transform,
            bool includeRawValues,
            ref int propertyId)
        {
            var properties = new List<InspectorPropertyDtoV1>
            {
                CreateInspectorProperty(ref propertyId, "Parent", transform.parent == null ? "(root)" : transform.parent.name, ValueTypeId.Utf16String, path: "Transform.Parent"),
                CreateInspectorProperty(ref propertyId, "LocalPosition", FormatVector3(transform.localPosition), ValueTypeId.Utf16String, path: "Transform.LocalPosition", rawValue: includeRawValues ? FormatVector3Raw(transform.localPosition) : null),
                CreateInspectorProperty(ref propertyId, "LocalRotation", FormatVector3(transform.localEulerAngles), ValueTypeId.Utf16String, path: "Transform.LocalEulerAngles", rawValue: includeRawValues ? FormatVector3Raw(transform.localEulerAngles) : null, unit: "deg"),
                CreateInspectorProperty(ref propertyId, "LocalScale", FormatVector3(transform.localScale), ValueTypeId.Utf16String, path: "Transform.LocalScale", rawValue: includeRawValues ? FormatVector3Raw(transform.localScale) : null),
                CreateInspectorProperty(ref propertyId, "Position", FormatVector3(transform.position), ValueTypeId.Utf16String, path: "Transform.Position", rawValue: includeRawValues ? FormatVector3Raw(transform.position) : null),
            };

            return properties.ToArray();
        }

        private static InspectorPropertyDtoV1[] BuildComponentProperties(
            Component component,
            bool includeProperties,
            bool includeRawValues,
            ref int propertyId)
        {
            // component 単位の内部 ID は hierarchy / inspector 往復には使っていない。
            // ここで engine 依存の identity を露出すると将来また同じ種類の破綻を招くため、
            // metadata は type 名と公開プロパティ中心に絞る。
            var properties = new List<InspectorPropertyDtoV1>
            {
                CreateInspectorProperty(ref propertyId, "Type", component.GetType().Name, ValueTypeId.Utf16String, path: "Component.Type"),
            };

            if (component is Behaviour behaviour)
            {
                properties.Add(CreateInspectorProperty(ref propertyId, "Enabled", behaviour.enabled, ValueTypeId.Boolean, path: $"{component.GetType().Name}.Enabled"));
            }

            if (!includeProperties)
            {
                return properties.ToArray();
            }

            // 型別プロパティは viewer 表示用の読み取り専用最小セット。
            // 全 Component を reflection で列挙すると取得コストと wire 量が膨らむため、
            // よく使う型だけを明示分岐で公開する。
            if (component is Renderer renderer)
            {
                properties.Add(CreateInspectorProperty(ref propertyId, "SortingLayerId", renderer.sortingLayerID, ValueTypeId.Int32, path: $"{component.GetType().Name}.SortingLayerId"));
                properties.Add(CreateInspectorProperty(ref propertyId, "SortingOrder", renderer.sortingOrder, ValueTypeId.Int32, path: $"{component.GetType().Name}.SortingOrder"));
            }

            if (component is Collider collider)
            {
                properties.Add(CreateInspectorProperty(ref propertyId, "IsTrigger", collider.isTrigger, ValueTypeId.Boolean, path: $"{component.GetType().Name}.IsTrigger"));
            }

            if (component is Rigidbody rigidbody)
            {
                properties.Add(CreateInspectorProperty(ref propertyId, "Mass", rigidbody.mass, ValueTypeId.Float64, path: $"{component.GetType().Name}.Mass", rawValue: includeRawValues ? rigidbody.mass.ToString("R", CultureInfo.InvariantCulture) : null));
                properties.Add(CreateInspectorProperty(ref propertyId, "UseGravity", rigidbody.useGravity, ValueTypeId.Boolean, path: $"{component.GetType().Name}.UseGravity"));
                properties.Add(CreateInspectorProperty(ref propertyId, "IsKinematic", rigidbody.isKinematic, ValueTypeId.Boolean, path: $"{component.GetType().Name}.IsKinematic"));
            }

            if (component is AudioSource audioSource)
            {
                properties.Add(CreateInspectorProperty(ref propertyId, "Volume", audioSource.volume, ValueTypeId.Float64, path: $"{component.GetType().Name}.Volume", rawValue: includeRawValues ? audioSource.volume.ToString("R", CultureInfo.InvariantCulture) : null));
                properties.Add(CreateInspectorProperty(ref propertyId, "Loop", audioSource.loop, ValueTypeId.Boolean, path: $"{component.GetType().Name}.Loop"));
                properties.Add(CreateInspectorProperty(ref propertyId, "PlayOnAwake", audioSource.playOnAwake, ValueTypeId.Boolean, path: $"{component.GetType().Name}.PlayOnAwake"));
            }

            if (component is Camera camera)
            {
                properties.Add(CreateInspectorProperty(ref propertyId, "FieldOfView", camera.fieldOfView, ValueTypeId.Float64, path: $"{component.GetType().Name}.FieldOfView", rawValue: includeRawValues ? camera.fieldOfView.ToString("R", CultureInfo.InvariantCulture) : null, unit: "deg"));
                properties.Add(CreateInspectorProperty(ref propertyId, "NearClipPlane", camera.nearClipPlane, ValueTypeId.Float64, path: $"{component.GetType().Name}.NearClipPlane", rawValue: includeRawValues ? camera.nearClipPlane.ToString("R", CultureInfo.InvariantCulture) : null));
                properties.Add(CreateInspectorProperty(ref propertyId, "FarClipPlane", camera.farClipPlane, ValueTypeId.Float64, path: $"{component.GetType().Name}.FarClipPlane", rawValue: includeRawValues ? camera.farClipPlane.ToString("R", CultureInfo.InvariantCulture) : null));
            }

            return properties.ToArray();
        }

        private static InspectorPropertyDtoV1 CreateInspectorProperty(
            ref int propertyId,
            string displayName,
            string valueText,
            int valueTypeId,
            string? path = null,
            string? rawValue = null,
            string? unit = null)
        {
            return new InspectorPropertyDtoV1
            {
                PropertyId = propertyId++,
                ValueTypeId = valueTypeId,
                Flags = InspectorPropertyFlags.ReadOnly,
                DisplayName = displayName,
                ValueText = valueText,
                RawValue = rawValue,
                Unit = unit,
                Path = path,
            };
        }

        private static InspectorPropertyDtoV1 CreateInspectorProperty(
            ref int propertyId,
            string displayName,
            bool value,
            int valueTypeId,
            string? path = null)
        {
            return CreateInspectorProperty(ref propertyId, displayName, value ? "True" : "False", valueTypeId, path, value ? "true" : "false");
        }

        private static InspectorPropertyDtoV1 CreateInspectorProperty(
            ref int propertyId,
            string displayName,
            int value,
            int valueTypeId,
            string? path = null)
        {
            var invariant = value.ToString(CultureInfo.InvariantCulture);
            return CreateInspectorProperty(ref propertyId, displayName, invariant, valueTypeId, path, invariant);
        }

        private static InspectorPropertyDtoV1 CreateInspectorProperty(
            ref int propertyId,
            string displayName,
            float value,
            int valueTypeId,
            string? path = null,
            string? rawValue = null,
            string? unit = null)
        {
            return CreateInspectorProperty(ref propertyId, displayName, value.ToString("0.###", CultureInfo.InvariantCulture), valueTypeId, path, rawValue, unit);
        }

        private static string FormatVector3(Vector3 value)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "({0:0.###}, {1:0.###}, {2:0.###})",
                value.x,
                value.y,
                value.z);
        }

        private static string FormatVector3Raw(Vector3 value)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0:R},{1:R},{2:R}",
                value.x,
                value.y,
                value.z);
        }

        private static class ValueTypeId
        {
            public const int Boolean = 1;
            public const int Int32 = 2;
            public const int Float64 = 4;
            public const int Utf16String = 5;
        }
    }
}
