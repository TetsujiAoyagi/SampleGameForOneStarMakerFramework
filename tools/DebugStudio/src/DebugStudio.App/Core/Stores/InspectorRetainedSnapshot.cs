#nullable enable

using DebugStudio.App.Core.Models;

namespace DebugStudio.App.Core.Stores;

/// <summary>
/// inspector export 用に、軽量 state と document 正本コピーを同一時点で束ねた snapshot。
/// 詳細文書と状態サマリーがずれないよう、単一 lock 下で複製して返す。
/// </summary>
public readonly record struct InspectorRetainedSnapshot(
    InspectorStoreSnapshot State,
    InspectorDocumentRecord? Document);
