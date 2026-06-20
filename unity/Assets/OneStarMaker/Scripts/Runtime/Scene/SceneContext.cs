#nullable enable

using System;
using System.Collections.Generic;

namespace OneStarMaker.Runtime.SceneSystem
{
    /// <summary>
    /// シーン遷移時の型付きデータ受け渡し用コンテキスト（Shared Context）。
    /// Android の Intent.putExtra() や ASP.NET の TempData に相当する。
    ///
    /// <para>型をキーにした Dictionary で、1型につき1値を保持する。</para>
    ///
    /// <code>
    /// // 送信側:
    /// var ctx = new SceneContext();
    /// ctx.Set(new InGameArgs(stageId, difficulty));
    /// await sceneDirector.AddScene("InGame", null, ct, context: ctx);
    ///
    /// // 受信側 (SceneBase.OnPreLoadedImpl 等):
    /// var args = Context?.Consume&lt;InGameArgs&gt;();
    /// </code>
    /// </summary>
    public sealed class SceneContext
    {
        private Dictionary<Type, object>? _data;

        /// <summary>
        /// 型付きデータをセットする。同じ型のデータは上書きされる。
        /// </summary>
        public void Set<T>(T value) where T : notnull
        {
            _data ??= new Dictionary<Type, object>();
            _data[typeof(T)] = value;
        }

        /// <summary>
        /// 参照型データを取得する。未登録なら null。
        /// </summary>
        public T? Get<T>() where T : class
        {
            if (_data != null && _data.TryGetValue(typeof(T), out var value))
            {
                return (T)value;
            }
            return default;
        }

        /// <summary>
        /// 値型データを取得する。未登録なら null。
        /// </summary>
        public T? GetValueType<T>() where T : struct
        {
            if (_data != null && _data.TryGetValue(typeof(T), out var value))
            {
                return (T)value;
            }
            return null;
        }

        /// <summary>
        /// 指定型のデータが存在するか。
        /// </summary>
        public bool Has<T>()
        {
            return _data != null && _data.ContainsKey(typeof(T));
        }

        /// <summary>
        /// データを取得し、バッグから削除する（TempData 方式）。
        /// </summary>
        public T? Consume<T>() where T : class
        {
            if (_data != null && _data.Remove(typeof(T), out var value))
            {
                return (T)value;
            }
            return default;
        }
    }
}
