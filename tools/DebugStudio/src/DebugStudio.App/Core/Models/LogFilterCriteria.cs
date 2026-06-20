#nullable enable

using System.Text.RegularExpressions;
using DebugStudio.Contracts.Schema;

namespace DebugStudio.App.Core.Models;

/// <summary>
/// ログエントリに対するフィルタ条件をまとめた検索条件。
/// 
/// <para>
/// 複数の検索条件（テキスト検索、ログレベル、カテゴリ、時間範囲）を
/// 組み合わせて、ログストアから条件に合致するエントリを抽出するために使用される。
/// 各プロパティが null の場合は、その条件は無視される（全値を許可）。
/// Factory メソッドおよび With* メソッドでフルエントビルダーパターンをサポート。
/// </para>
/// </summary>
public sealed record LogFilterCriteria
{
    /// <summary>
    /// テキスト検索パターン（正規表現またはプレーンテキスト）。null の場合は検索を実施しない。
    /// </summary>
    public string? TextSearchPattern { get; init; }

    /// <summary>
    /// フィルタ対象のログレベル配列。null の場合は全てのレベルを許可。
    /// </summary>
    public LogEntryKind[]? LevelFilters { get; init; }

    /// <summary>
    /// フィルタ対象のカテゴリタグ配列。null の場合は全てのカテゴリを許可。
    /// </summary>
    public string[]? CategoryTags { get; init; }

    /// <summary>
    /// TextSearchPattern を正規表現として扱うかどうか。デフォルト: false（プレーンテキスト）。
    /// </summary>
    public bool UseRegex { get; init; }

    /// <summary>
    /// テキスト検索時に大文字小文字を区別するかどうか。
    /// 正規表現検索には影響しません（正規表現フラグで制御）。
    /// デフォルト: false（区別しない）
    /// </summary>
    public bool CaseSensitive { get; init; }

    /// <summary>
    /// フィルタ対象期間の開始時刻（UTC）。null の場合は時間下限を設けない。
    /// </summary>
    public DateTimeOffset? StartTime { get; init; }

    /// <summary>
    /// フィルタ対象期間の終了時刻（UTC）。null の場合は時間上限を設けない。
    /// </summary>
    public DateTimeOffset? EndTime { get; init; }

    /// <summary>
    /// このフィルタ条件が空であるか（全条件が未設定か）を示します。
    /// true の場合、すべてのログエントリにマッチします。
    /// </summary>
    public bool IsEmpty => 
        TextSearchPattern == null &&
        LevelFilters == null &&
        CategoryTags == null &&
        StartTime == null &&
        EndTime == null;

    /// <summary>
    /// 全てのログエントリにマッチするフィルタ条件を生成します。
    /// （フィルタ条件が何も指定されていない状態）
    /// </summary>
    /// <returns>空のフィルタ条件インスタンス</returns>
    public static LogFilterCriteria CreateEmpty()
    {
        return new LogFilterCriteria();
    }

    /// <summary>
    /// ログレベル配列でフィルタ条件を作成します。
    /// </summary>
    /// <param name="levels">フィルタ対象のログレベル（LogEntryKind 相当の値）</param>
    /// <returns>フィルタ条件インスタンス</returns>
    public static LogFilterCriteria CreateByLevel(int[] levels)
    {
        ArgumentNullException.ThrowIfNull(levels);
        var kinds = levels.Select(static level => (LogEntryKind)level).ToArray();
        return new LogFilterCriteria { LevelFilters = kinds };
    }

    /// <summary>
    /// テキストキーワードでフィルタ条件を作成します。
    /// </summary>
    /// <param name="text">検索キーワード</param>
    /// <param name="caseSensitive">大文字小文字を区別するかどうか</param>
    /// <returns>フィルタ条件インスタンス</returns>
    public static LogFilterCriteria CreateByText(string text, bool caseSensitive = false)
    {
        ArgumentNullException.ThrowIfNull(text);
        return new LogFilterCriteria 
        { 
            TextSearchPattern = text,
            UseRegex = false,
            CaseSensitive = caseSensitive,
        };
    }

    /// <summary>
    /// 正規表現パターンでフィルタ条件を作成します。
    /// パターンが無効な場合は ArgumentException を投げます（即座検証）。
    /// </summary>
    /// <param name="pattern">正規表現パターン</param>
    /// <returns>フィルタ条件インスタンス</returns>
    /// <exception cref="ArgumentException">パターンが無効な場合</exception>
    public static LogFilterCriteria CreateByRegex(string pattern)
    {
        ArgumentNullException.ThrowIfNull(pattern);
        
        // 正規表現の妥当性を即座に検証
        try
        {
            _ = new Regex(pattern, RegexOptions.Compiled);
        }
        catch (ArgumentException ex)
        {
            throw new ArgumentException($"無効な正規表現パターン: {pattern}", ex);
        }

        return new LogFilterCriteria 
        { 
            TextSearchPattern = pattern,
            UseRegex = true,
        };
    }

    /// <summary>
    /// カテゴリタグでフィルタ条件を作成します。
    /// </summary>
    /// <param name="categories">フィルタ対象のカテゴリ</param>
    /// <returns>フィルタ条件インスタンス</returns>
    public static LogFilterCriteria CreateByCategory(string[] categories)
    {
        ArgumentNullException.ThrowIfNull(categories);
        return new LogFilterCriteria { CategoryTags = categories };
    }

    /// <summary>
    /// 時間範囲でフィルタ条件を作成します。
    /// StartTime > EndTime の場合は ArgumentException を投げます。
    /// </summary>
    /// <param name="startTime">開始時刻（UTC）</param>
    /// <param name="endTime">終了時刻（UTC）</param>
    /// <returns>フィルタ条件インスタンス</returns>
    /// <exception cref="ArgumentException">StartTime > EndTime の場合</exception>
    public static LogFilterCriteria CreateByTimeRange(DateTime startTime, DateTime endTime)
    {
        if (startTime > endTime)
        {
            throw new ArgumentException($"開始時刻 ({startTime}) が終了時刻 ({endTime}) より後です。");
        }

        return new LogFilterCriteria 
        { 
            StartTime = new DateTimeOffset(startTime, TimeSpan.Zero),
            EndTime = new DateTimeOffset(endTime, TimeSpan.Zero),
        };
    }

    /// <summary>
    /// テキスト検索条件を追加した新しいフィルタ条件を返します（ビルダーパターン）。
    /// </summary>
    /// <param name="text">検索キーワード</param>
    /// <param name="caseSensitive">大文字小文字を区別するかどうか</param>
    public LogFilterCriteria WithText(string text, bool caseSensitive = false)
    {
        ArgumentNullException.ThrowIfNull(text);
        return this with { TextSearchPattern = text, UseRegex = false, CaseSensitive = caseSensitive };
    }

    /// <summary>
    /// 正規表現検索条件を追加した新しいフィルタ条件を返します（ビルダーパターン）。
    /// パターンが無効な場合は ArgumentException を投げます。
    /// </summary>
    public LogFilterCriteria WithRegex(string pattern)
    {
        ArgumentNullException.ThrowIfNull(pattern);
        
        // 正規表現の妥当性を即座に検証
        try
        {
            _ = new Regex(pattern, RegexOptions.Compiled);
        }
        catch (ArgumentException ex)
        {
            throw new ArgumentException($"無効な正規表現パターン: {pattern}", ex);
        }

        return this with { TextSearchPattern = pattern, UseRegex = true };
    }

    /// <summary>
    /// カテゴリ条件を追加または置き換えた新しいフィルタ条件を返します（ビルダーパターン）。
    /// </summary>
    public LogFilterCriteria WithCategory(string category)
    {
        ArgumentNullException.ThrowIfNull(category);
        return this with { CategoryTags = new[] { category } };
    }

    /// <summary>
    /// カテゴリ条件を追加または置き換えた新しいフィルタ条件を返します（ビルダーパターン）。
    /// </summary>
    public LogFilterCriteria WithCategory(string[] categories)
    {
        ArgumentNullException.ThrowIfNull(categories);
        return this with { CategoryTags = categories };
    }

    /// <summary>
    /// 時間範囲条件を追加または置き換えた新しいフィルタ条件を返します（ビルダーパターン）。
    /// </summary>
    public LogFilterCriteria WithTimeRange(DateTime startTime, DateTime endTime)
    {
        if (startTime > endTime)
        {
            throw new ArgumentException($"開始時刻 ({startTime}) が終了時刻 ({endTime}) より後です。");
        }

        return this with 
        { 
            StartTime = new DateTimeOffset(startTime, TimeSpan.Zero),
            EndTime = new DateTimeOffset(endTime, TimeSpan.Zero),
        };
    }
}
