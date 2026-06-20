using Cysharp.Text;
using System;
using System.Collections.Generic;

namespace OneStarMaker.Foundation.Core
{
    /// <summary>
    /// テレメトリスパンの操作種別。
    /// 「何の処理を計測したか」を表す単一選択の enum。
    ///
    /// <para>
    /// 設計方針:
    ///   - [Flags] ではない通常 enum。スパン 1 つにつき 1 種別のみ。
    ///   - 異常検出やアラートの分類は <see cref="TelemetryTagType"/> 側で行う。
    ///   - 追加は連番でよい（bit 位置管理不要）。
    /// </para>
    /// </summary>
    public enum TelemetryStartType
    {
        /// <summary>デフォルト値。種別未設定。</summary>
        Unknown = 0,

        // ── アプリ・フレームワーク共通 ──

        /// <summary>アプリ起動処理全体。</summary>
        AppStartup = 1,

        /// <summary>シーン切り替え（SwitchScene / GoBack）。</summary>
        SceneTransition = 2,

        /// <summary>シーンロード処理。</summary>
        SceneLoad = 3,

        /// <summary>シーン のアンロード処理。</summary>
        SceneUnload = 4,

        /// <summary>Profiler の定期サマリ記録。</summary>
        ProfilerSummary = 5,

        /// <summary>GC スパイク検出イベント。</summary>
        GcSpike = 6,

        /// <summary>UI 再構築/バッチ増大の検出イベント。</summary>
        UiCost = 7,

    }

    /// <summary>
    /// テレメトリレコードに付与する補助分類タグ。
    /// [Flags] 複数同時付与可。
    ///
    /// <para>
    /// 設計方針:
    ///   - スパン名（<see cref="TelemetryStartType"/>）には「何の処理か」を、
    ///     タグには「どんな性質・異常が検出されたか」を持たせる。
    ///   - アラート系（CpuTimeOver 等）はここで管理し、StartType と重複させない。
    /// </para>
    /// </summary>
    [Flags]
    public enum TelemetryTagType
    {
        /// <summary>タグなし。</summary>
        None = 0,
        // ── パフォーマンス異常タグ ──

        /// <summary>ボトルネック（複合要因）が検出された。</summary>
        Bottleneck = 1 << 0,

        /// <summary>CPU 時間が閾値超過。</summary>
        CpuTimeOver = 1 << 1,

        /// <summary>GPU 時間が閾値超過。</summary>
        GpuTimeOver = 1 << 2,

        /// <summary>マネージドメモリが閾値超過。</summary>
        ManagedMemoryOver = 1 << 3,

        /// <summary>ネイティブメモリが閾値超過。</summary>
        NativeMemoryOver = 1 << 4,

        /// <summary>フレームレートが閾値以下に低下。</summary>
        FrameRateDrop = 1 << 5,

        /// <summary>GC Alloc のスパイクが検出された。</summary>
        AllocSpike = 1 << 6,

        /// <summary>入力遅延が閾値超過。</summary>
        InputLatency = 1 << 7,

        // ── その他 ──

        /// <summary>ネットワーク遅延・切断を伴う。</summary>
        NetworkIssue = 1 << 8,

        /// <summary>致命的例外・クラッシュ記録を伴う。</summary>
        FatalError = 1 << 9,

    }

    /// <summary>
    /// テレメトリ enum の拡張メソッド。
    /// ゼロアロケーションを優先するため、返す文字列は事前キャッシュ配列から取得する。
    /// </summary>
    public static class TelemetryEnumExtensions
    {
        // 事前キャッシュ: Enum.GetNames は起動時 1 回のみ呼ぶ（ゼロアロケーション維持）
        private static readonly string[] s_startTypeNames = Enum.GetNames(typeof(TelemetryStartType));
        private static readonly string[] s_tagTypeNames   = Enum.GetNames(typeof(TelemetryTagType));

        /// <summary>
        /// <see cref="TelemetryStartType"/> を文字列に変換する。
        /// 事前キャッシュ配列を直接返すためアロケーションなし。
        /// </summary>
        public static string ToStartTypeString(this TelemetryStartType startType)
        {
            // TelemetryStartType は連番 enum。値をそのまま配列インデックスとして使う。
            var index = (int)startType;
            if ((uint)index < (uint)s_startTypeNames.Length)
                return s_startTypeNames[index];
            return "Unknown";
        }

        /// <summary>
        /// <see cref="TelemetryTagType"/> を "Tag1,Tag2" 形式の文字列に変換する。
        /// ZString の ValueStringBuilder を使用してヒープアロケーションを最小化する。
        /// ※ 最終的な string 化は 1 回のみ発生する。
        /// </summary>
        public static string ToTagString(this TelemetryTagType tag)
        {
            if (tag == TelemetryTagType.None) return string.Empty;

            // ZString の ValueStringBuilder でゼロアロケーション結合
            using var sb = ZString.CreateStringBuilder(notNested: true);
            var first = true;

            for (var i = 0; i < s_tagTypeNames.Length; i++)
            {
                // s_tagTypeNames[0] は "None" なのでスキップ（i=0 は None=0）
                if (i == 0) continue;
                var bit = (TelemetryTagType)(1 << (i - 1));
                if ((tag & bit) == 0) continue;

                if (!first) sb.Append(',');
                sb.Append(s_tagTypeNames[i]);
                first = false;
            }

            return sb.ToString();
        }
    }

}
