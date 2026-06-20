#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;

namespace OneStarMaker.Foundation.Config
{
    /// <summary>
    /// アプリケーション設定。
    /// 複数の <see cref="IConfigProvider"/> から値をマージし、型安全なアクセスを提供する。
    /// 優先順位: CommandLine &gt; EnvironmentVariable &gt; ConfigFile
    /// （リストの後方ほど優先度が高い）。
    /// キーは大文字小文字を区別しない。"." と ":" はどちらも区切り文字として扱う。
    /// </summary>
    public sealed class AppConfig
    {
        private readonly Dictionary<string, string> _store =
            new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// 指定された優先順位でプロバイダを登録し、設定値をロードする。
        /// リストの後方ほど優先度が高い（上書きする）。
        /// </summary>
        /// <param name="providers">設定プロバイダのリスト（低優先 → 高優先の順）。</param>
        public AppConfig(IReadOnlyList<IConfigProvider> providers)
        {
            if (providers == null) throw new ArgumentNullException(nameof(providers));

            foreach (var provider in providers)
            {
                provider.Load(_store);
            }
        }

        // ─── Query API ───

        /// <summary>指定キーが存在するか。</summary>
        public bool ContainsKey(string key) => _store.ContainsKey(NormalizeKey(key));

        /// <summary>文字列値を取得する。</summary>
        public string GetString(string key, string defaultValue = "")
            => _store.TryGetValue(NormalizeKey(key), out var v) ? v : defaultValue;

        /// <summary>整数値を取得する。パース失敗時は defaultValue。</summary>
        public int GetInt(string key, int defaultValue = 0)
            => _store.TryGetValue(NormalizeKey(key), out var v)
               && int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i)
                ? i : defaultValue;

        /// <summary>浮動小数点値を取得する。パース失敗時は defaultValue。</summary>
        public float GetFloat(string key, float defaultValue = 0f)
            => _store.TryGetValue(NormalizeKey(key), out var v)
               && float.TryParse(v, NumberStyles.Float | NumberStyles.AllowThousands,
                   CultureInfo.InvariantCulture, out var f)
                ? f : defaultValue;

        /// <summary>
        /// 真偽値を取得する。
        /// "true" / "1" / "yes" → true、"false" / "0" / "no" → false（大文字小文字不問）。
        /// </summary>
        public bool GetBool(string key, bool defaultValue = false)
        {
            if (!_store.TryGetValue(NormalizeKey(key), out var v))
                return defaultValue;

            return v.ToLowerInvariant() switch
            {
                "true" or "1" or "yes" => true,
                "false" or "0" or "no" => false,
                _ => defaultValue,
            };
        }

        /// <summary>
        /// 指定プレフィックスで始まるキーをサブセクションとして取得する。
        /// 例: GetSection("Server") → { "Host": "localhost", "Port": "8080" }
        /// </summary>
        public IReadOnlyDictionary<string, string> GetSection(string prefix)
        {
            var normalized = NormalizeKey(prefix);
            var sectionPrefix = normalized + ":";
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var kvp in _store)
            {
                if (kvp.Key.StartsWith(sectionPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    result[kvp.Key.Substring(sectionPrefix.Length)] = kvp.Value;
                }
            }
            return result;
        }

        /// <summary>全設定値の読み取り専用ビュー。デバッグ・ログ用。</summary>
        public IReadOnlyDictionary<string, string> All => _store;

        // ─── Internal ───

        /// <summary>"." を ":" に正規化する。</summary>
        private static string NormalizeKey(string key) => key.Replace('.', ':');
    }
}
