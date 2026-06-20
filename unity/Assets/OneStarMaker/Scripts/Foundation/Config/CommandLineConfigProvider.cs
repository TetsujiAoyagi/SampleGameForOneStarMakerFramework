#nullable enable

using System;
using System.Collections.Generic;

namespace OneStarMaker.Foundation.Config
{
    /// <summary>
    /// コマンドライン引数から設定値を読み込むプロバイダ。
    ///
    /// <para>サポートする形式:</para>
    /// <list type="bullet">
    ///   <item><c>--Key=Value</c></item>
    ///   <item><c>--Key Value</c>（次の引数が "-" で始まらない場合）</item>
    ///   <item><c>-Key=Value</c></item>
    ///   <item><c>-Key Value</c></item>
    ///   <item><c>--Flag</c>（値なし → "true"）</item>
    /// </list>
    ///
    /// キー中の "." は ":" に正規化される。
    ///
    /// <para>使用例（Unity ビルド起動時）:</para>
    /// <code>
    /// MyGame.exe --Server.Host=192.168.1.1 --Server.Port 9090 --Debug.Enabled
    /// </code>
    /// → Server:Host = "192.168.1.1", Server:Port = "9090", Debug:Enabled = "true"
    /// </summary>
    public sealed class CommandLineConfigProvider : IConfigProvider
    {
        private readonly string[] _args;

        /// <summary>
        /// 指定された引数配列からコマンドライン設定を読み込む。
        /// </summary>
        /// <param name="args">
        /// 引数配列。null の場合は <see cref="Environment.GetCommandLineArgs"/> を使用する。
        /// </param>
        public CommandLineConfigProvider(string[]? args = null)
        {
            _args = args ?? GetCommandLineArgsSafe();
        }

        public void Load(Dictionary<string, string> store)
        {
            for (var i = 0; i < _args.Length; i++)
            {
                var arg = _args[i];
                if (!arg.StartsWith("-"))
                    continue;

                // "--" or "-" のプレフィックスを除去
                var keyPart = arg.StartsWith("--")
                    ? arg.Substring(2)
                    : arg.Substring(1);

                if (string.IsNullOrEmpty(keyPart))
                    continue;

                // "=" で分割
                var eqIndex = keyPart.IndexOf('=');
                if (eqIndex >= 0)
                {
                    var key = keyPart.Substring(0, eqIndex).Replace('.', ':');
                    var value = keyPart.Substring(eqIndex + 1);
                    store[key] = value;
                }
                else
                {
                    var key = keyPart.Replace('.', ':');

                    // 次の引数を値として扱う（次の引数が "-" で始まらない場合）
                    if (i + 1 < _args.Length && !_args[i + 1].StartsWith("-"))
                    {
                        store[key] = _args[i + 1];
                        i++; // 値として消費した引数をスキップ
                    }
                    else
                    {
                        // 値なしフラグ → "true"
                        store[key] = "true";
                    }
                }
            }
        }

        private static string[] GetCommandLineArgsSafe()
        {
            try
            {
                return Environment.GetCommandLineArgs();
            }
            catch
            {
                return Array.Empty<string>();
            }
        }
    }
}
