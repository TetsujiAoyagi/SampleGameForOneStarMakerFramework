#nullable enable

namespace DebugStudio.Contracts.Schema;

/// <summary>
/// log payload が将来 typed field 列へ移行するときの共有 field 定義。
///
/// <para>
/// Wave 2 では既存 <c>LogEnvelopeV1</c> を壊さず、将来の zero-allocation 指向 parser が
/// 「どの field がどの型か」を参照できる基点だけを用意する。
/// ViewModel 側の表示列名や並び順はここに入れず、wire/schema の安定軸だけを置く。
/// </para>
/// </summary>
public static class LogFieldSchema
{
    public static readonly SchemaFieldDefinition Sequence =
        new(1, "sequence", SchemaValueTypeId.Int64);

    public static readonly SchemaFieldDefinition ApplicationName =
        new(2, "applicationName", SchemaValueTypeId.Utf16String);

    public static readonly SchemaFieldDefinition TimestampUnixTimeMilliseconds =
        new(3, "timestampUnixTimeMilliseconds", SchemaValueTypeId.UnixTimeMilliseconds);

    public static readonly SchemaFieldDefinition Category =
        new(4, "category", SchemaValueTypeId.Utf16String);

    public static readonly SchemaFieldDefinition Kind =
        new(5, "kind", SchemaValueTypeId.Int32);

    public static readonly SchemaFieldDefinition EventId =
        new(6, "eventId", SchemaValueTypeId.Int32);

    public static readonly SchemaFieldDefinition EventName =
        new(7, "eventName", SchemaValueTypeId.Utf16String, IsOptional: true);

    public static readonly SchemaFieldDefinition Message =
        new(8, "message", SchemaValueTypeId.Utf16String);

    public static readonly SchemaFieldDefinition Exception =
        new(9, "exception", SchemaValueTypeId.Utf16String, IsOptional: true);

    public static readonly SchemaFieldDefinition ThreadId =
        new(10, "threadId", SchemaValueTypeId.Int32);

    public static readonly SchemaFieldDefinition ThreadName =
        new(11, "threadName", SchemaValueTypeId.Utf16String, IsOptional: true);

    public static readonly SchemaFieldDefinition MemberName =
        new(12, "memberName", SchemaValueTypeId.Utf16String, IsOptional: true);

    public static readonly SchemaFieldDefinition FilePath =
        new(13, "filePath", SchemaValueTypeId.Utf16String, IsOptional: true);

    public static readonly SchemaFieldDefinition LineNumber =
        new(14, "lineNumber", SchemaValueTypeId.Int32);
}
