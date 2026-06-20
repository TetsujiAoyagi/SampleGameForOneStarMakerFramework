#nullable enable

namespace DebugStudio.Contracts.Schema;

/// <summary>
/// 将来の inspector / hierarchy / log を同じ「型付きフィールド列」として扱うための共有 value type 識別子。
///
/// <para>
/// ここを ViewModel とは別 namespace / 別 project に置く理由は、
/// wire format 上の意味論を WPF 表示都合と切り離すためである。
/// UI は表示用に string 化や列並び替えを行うが、schema は
/// 「受信データが本来どの型として読めるか」を安定して表現し続ける責務だけを持つ。
/// </para>
/// </summary>
public enum SchemaValueTypeId : ushort
{
    Unknown = 0,
    Boolean = 1,
    Int32 = 2,
    Int64 = 3,
    Float64 = 4,
    Utf16String = 5,
    Utf8Binary = 6,
    Guid = 7,
    UnixTimeMilliseconds = 8,
}
