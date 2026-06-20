#nullable enable

using System;
using System.Collections.Generic;

namespace DebugStudio.App.Core.Models;

/// <summary>
/// ログエントリの検索結果をまとめたレコード。
/// 
/// <para>
/// QueryLogs() メソッドが返す検索結果。フィルタにマッチしたエントリのリストのほか、
/// 総マッチ数、フィルタ前のエントリ数、クエリ実行時間などの統計情報を含みます。
/// UI 層がページング、パフォーマンス表示、ユーザーフィードバック生成に使用します。
/// </para>
/// </summary>
public sealed record LogSearchResult
{
    /// <summary>
    /// 検索条件にマッチしたログエントリのリスト（検索順序に従う）。
    /// </summary>
    public IReadOnlyList<LogRecord> Matches { get; init; } = Array.Empty<LogRecord>();

    /// <summary>
    /// マッチしたエントリの総数。Matches.Count と同等。
    /// </summary>
    public int MatchCount { get; init; }

    /// <summary>
    /// フィルタ適用前のストア内全エントリ数。マッチ率の算出に用いられます。
    /// </summary>
    public int TotalEntries { get; init; }

    /// <summary>
    /// クエリ実行にかかった経過時間（ミリ秒単位）。パフォーマンス監視・最適化の指標となります。
    /// </summary>
    public long ElapsedMilliseconds { get; init; }

    /// <summary>
    /// 検索結果が空であるか（マッチしたエントリが 0 件）を示します。
    /// </summary>
    public bool IsEmpty => MatchCount == 0;

    /// <summary>
    /// マッチ率（0.0 ～ 1.0）。TotalEntries が 0 の場合は 0.0 を返します。
    /// </summary>
    public double MatchRatio => TotalEntries > 0 ? (double)MatchCount / TotalEntries : 0.0;
}
