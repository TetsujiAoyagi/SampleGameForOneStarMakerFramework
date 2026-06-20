#nullable enable

using System.Collections.Generic;

namespace OneStarMaker.Foundation.Config
{
    /// <summary>
    /// 設定値を提供するプロバイダ。
    /// 各プロバイダがフラットなキー/値ペアを生成し、<see cref="AppConfig"/> が優先順位に従ってマージする。
    /// キーの区切り文字は ":" を使用する（Microsoft.Extensions.Configuration 互換）。
    /// </summary>
    public interface IConfigProvider
    {
        /// <summary>
        /// 設定値を読み込み、フラットな辞書に追加する。
        /// 既存キーがあれば上書きする（後に呼ばれたプロバイダが優先）。
        /// </summary>
        /// <param name="store">設定値の格納先。</param>
        void Load(Dictionary<string, string> store);
    }
}
