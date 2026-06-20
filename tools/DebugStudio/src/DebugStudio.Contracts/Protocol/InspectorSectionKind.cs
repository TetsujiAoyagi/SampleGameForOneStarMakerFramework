#nullable enable

namespace DebugStudio.Contracts.Protocol;

/// <summary>
/// inspector section の大分類。
/// まずは「GameObject ヘッダー」「Component」「Metadata」の粒度だけを固定し、
/// section 内の詳細は property 行に委ねる。
/// </summary>
public enum InspectorSectionKind
{
    Unknown = 0,
    Header = 1,
    Component = 2,
    Metadata = 3,
}
