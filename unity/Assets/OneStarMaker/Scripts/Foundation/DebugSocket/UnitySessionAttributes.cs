#nullable enable

using UnityEngine;

namespace OneStarMaker.Foundation.DebugSocket
{
    /// <summary>
    /// Unity 起動時の環境値をメインスレッドで焼き込み、Welcome 組み立てで参照する。
    ///
    /// <para>
    /// <see cref="Application"/> / <see cref="SystemInfo"/> はメインスレッド専用のため、
    /// ソケット受信経路で直接読まず、bootstrap で採取した値だけを公開する。
    /// </para>
    /// </summary>
    public static class UnitySessionAttributes
    {
        private static readonly object Gate = new();
        private static Snapshot _snapshot = Snapshot.Empty;

        public static string ProductName { get { lock (Gate) { return _snapshot.ProductName; } } }
        public static string BuildVersion { get { lock (Gate) { return _snapshot.BuildVersion; } } }
        public static string Platform { get { lock (Gate) { return _snapshot.Platform; } } }
        public static string DeviceModel { get { lock (Gate) { return _snapshot.DeviceModel; } } }
        public static string OsVersion { get { lock (Gate) { return _snapshot.OsVersion; } } }
        public static string UnityVersion { get { lock (Gate) { return _snapshot.UnityVersion; } } }

        /// <summary>
        /// メインスレッドから環境値を採取して保持する。domain reload で複数回呼ばれてもよい。
        /// </summary>
        public static void Capture()
        {
            var next = new Snapshot(
                Application.productName ?? string.Empty,
                Application.version ?? string.Empty,
                Application.platform.ToString(),
                SystemInfo.deviceModel ?? string.Empty,
                SystemInfo.operatingSystem ?? string.Empty,
                Application.unityVersion ?? string.Empty);

            lock (Gate)
            {
                _snapshot = next;
            }
        }

        /// <summary>テスト用。本番 bootstrap とは別経路で state を初期化する。</summary>
        internal static void ResetForTests()
        {
            lock (Gate)
            {
                _snapshot = Snapshot.Empty;
            }
        }

        private readonly struct Snapshot
        {
            public Snapshot(
                string productName,
                string buildVersion,
                string platform,
                string deviceModel,
                string osVersion,
                string unityVersion)
            {
                ProductName = productName;
                BuildVersion = buildVersion;
                Platform = platform;
                DeviceModel = deviceModel;
                OsVersion = osVersion;
                UnityVersion = unityVersion;
            }

            public string ProductName { get; }
            public string BuildVersion { get; }
            public string Platform { get; }
            public string DeviceModel { get; }
            public string OsVersion { get; }
            public string UnityVersion { get; }

            public static Snapshot Empty { get; } = new(
                string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty);
        }
    }
}
