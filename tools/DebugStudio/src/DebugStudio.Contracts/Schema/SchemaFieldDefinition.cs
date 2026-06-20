#nullable enable

namespace DebugStudio.Contracts.Schema;

/// <summary>
/// 1 つの schema field が持つ最小限の定義情報。
///
/// <para>
/// 現段階では「field id / 論理名 / value type」だけに留め、
/// inspector や hierarchy の列設計が固まってから拡張できるようにしている。
/// 先に巨大なメタモデルを作らず、しかし後続 wave が同じ語彙を共有できる最低限の芯を置く。
/// </para>
/// </summary>
/// <param name="FieldId">バイナリ列上で安定させたい field 識別子。</param>
/// <param name="Name">デバッグ表示やエクスポート時に再利用できる論理名。</param>
/// <param name="ValueType">field に格納される値の論理型。</param>
/// <param name="IsOptional">payload に存在しないことを許容するかどうか。</param>
public readonly record struct SchemaFieldDefinition(
    int FieldId,
    string Name,
    SchemaValueTypeId ValueType,
    bool IsOptional = false);
