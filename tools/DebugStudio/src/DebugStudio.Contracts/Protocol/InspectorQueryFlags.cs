#nullable enable

using System;

namespace DebugStudio.Contracts.Protocol;

/// <summary>
/// inspector query がどの程度の情報を欲しているかを表すフラグ。
/// v1 は viewer-first なので、編集意図ではなく「どこまで表示に必要か」を中心に定義する。
/// </summary>
[Flags]
public enum InspectorQueryFlags
{
    None = 0,
    IncludeMetadata = 1 << 0,
    IncludeComponents = 1 << 1,
    IncludeProperties = 1 << 2,
    IncludeRawValues = 1 << 3,
}
