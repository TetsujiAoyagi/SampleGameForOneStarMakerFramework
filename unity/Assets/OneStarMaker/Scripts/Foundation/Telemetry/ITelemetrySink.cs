#nullable enable

using System;

namespace OneStarMaker.Foundation.Telemetry
{
    /// <summary>
    /// テレメトリレコードの出力先インターフェース。
    /// 実装例: JSON ファイル出力、Elastic 送信、コンソール出力。
    /// </summary>
    public interface ITelemetrySink : IDisposable
    {
        /// <summary>
        /// テレメトリレコードを書き込む。
        /// スレッドセーフであること。
        /// </summary>
        /// <param name="record">書き込むレコード。</param>
        void Write(in TelemetryRecord record);

        /// <summary>
        /// バッファをフラッシュする。アプリ終了時等に呼び出す。
        /// </summary>
        void Flush();
    }
}
