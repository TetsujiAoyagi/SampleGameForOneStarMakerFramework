#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using OneStarMaker.Foundation.Config;

namespace OneStarMaker.Runtime.Config
{
    /// <summary>Player package が必ず持つ plain JSON を読み込む provider。</summary>
    internal sealed class RequiredJsonFileConfigProvider : IConfigProvider
    {
        private readonly string _path;

        public RequiredJsonFileConfigProvider(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("A required configuration path is needed.", nameof(path));
            _path = path;
        }

        public void Load(Dictionary<string, string> store)
        {
            if (store == null) throw new ArgumentNullException(nameof(store));
            if (!File.Exists(_path)) throw new FileNotFoundException("Required Player configuration is missing.", _path);
            // Player content の登録前には Addressables を使えない。Windows package の plain file に限定する。
            JsonConfigFlattener.Flatten(File.ReadAllText(_path), store);
        }
    }
}
