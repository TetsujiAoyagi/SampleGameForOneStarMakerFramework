#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;

namespace OneStarMaker.Foundation.Config
{
    /// <summary>
    /// 環境変数から設定値を読み込むプロバイダ。
    /// 指定プレフィックスで始まる環境変数のみを対象とし、"__" を ":" に変換する
    /// （Microsoft.Extensions.Configuration 互換）。
    ///
    /// <para>変換例（プレフィックス "ONESM_" の場合）:</para>
    /// <list type="bullet">
    ///   <item>ONESM_SERVER__HOST=localhost → Server:Host = localhost</item>
    ///   <item>ONESM_SERVER__PORT=8080     → Server:Port = 8080</item>
    ///   <item>ONESM_DEBUG__ENABLED=true   → Debug:Enabled = true</item>
    ///   <item>MYAPP_UNRELATED=foo         → （対象外、スキップ）</item>
    /// </list>
    /// </summary>
    public sealed class EnvironmentVariableConfigProvider : IConfigProvider
    {
        private readonly string _prefix;

        /// <param name="prefix">
        /// 対象とする環境変数のプレフィックス（例: "ONESM_"）。
        /// 空文字の場合は全環境変数を対象とする。
        /// </param>
        public EnvironmentVariableConfigProvider(string prefix = "")
        {
            _prefix = prefix ?? "";
        }

        public void Load(Dictionary<string, string> store)
        {
            IDictionary envVars;
            try
            {
                envVars = Environment.GetEnvironmentVariables();
            }
            catch (System.Security.SecurityException)
            {
                // サンドボックス環境など、環境変数へのアクセスが制限されている場合
                return;
            }

            foreach (DictionaryEntry entry in envVars)
            {
                if (entry.Key is not string key || entry.Value is not string value)
                    continue;

                if (_prefix.Length > 0)
                {
                    if (!key.StartsWith(_prefix, StringComparison.OrdinalIgnoreCase))
                        continue;

                    key = key.Substring(_prefix.Length);
                }

                // "__" → ":" に変換（階層区切り）
                key = key.Replace("__", ":");

                store[key] = value;
            }
        }
    }
}
