#nullable enable

using System;
using OneStarMaker.Foundation.Config;

namespace OneStarMaker.Foundation.DebugSocket
{
    public enum DebugSocketTransportMode
    {
        Listen,
        Connect,
    }

    /// <summary>
    /// DebugSocket サービスの起動設定。
    ///
    /// <para>
    /// 設定値そのものは <see cref="AppConfig"/> から読み出すが、
    /// 実際の listener 構築時にはこの型に正規化して扱う。
    /// こうしておくと Runtime 側は「Config の細かいキー名」ではなく
    /// 「検証済みの起動オプション」だけを見ればよくなる。
    /// </para>
    /// </summary>
    public sealed class DebugSocketOptions
    {
        private const string DefaultHost = "127.0.0.1";
        private const int DefaultPort = 5010;
        private const string DefaultPath = "/debugsocket/";
        private const int DefaultMaxQueueLength = 1024;

        /// <summary>サービス全体を有効化するか。</summary>
        public bool Enabled { get; private set; }

        /// <summary>起動時に自動で待受を開始するか。</summary>
        public bool AutoStart { get; private set; } = true;

        /// <summary>
        /// transport の動作モード。
        /// v1 既定は Unity が待受側になる listen。
        /// </summary>
        public DebugSocketTransportMode TransportMode { get; private set; } = DebugSocketTransportMode.Listen;

        /// <summary>
        /// listener を bind するホスト名または IP。
        /// v1 では 127.0.0.1 を既定とし、ローカルデバッグを基本にする。
        /// </summary>
        public string Host { get; private set; } = DefaultHost;

        /// <summary>待受ポート。</summary>
        public int Port { get; private set; } = DefaultPort;

        /// <summary>
        /// WebSocket upgrade を受け付けるパス。
        /// HttpListener の prefix 仕様に合わせ、先頭と末尾の '/' を保証して扱う。
        /// </summary>
        public string Path { get; private set; } = DefaultPath;

        /// <summary>
        /// transportMode=connect のときに Unity が接続しに行く URI。
        /// listen 既定の移行準備用に保持しておき、実装時はこの正規化済み値だけを見る。
        /// </summary>
        public Uri? ConnectUri { get; private set; }

        /// <summary>
        /// true のときだけ loopback 以外の host を許可する。
        /// 誤って LAN 公開しないための安全弁。
        /// </summary>
        public bool AllowRemote { get; private set; }

        /// <summary>ログ転送を有効化するか。</summary>
        public bool SendLogs { get; private set; } = true;

        /// <summary>テレメトリ転送を有効化するか。</summary>
        public bool SendTelemetry { get; private set; } = true;

        /// <summary>
        /// 送信キューの最大件数。
        /// あふれたときは v1 方針どおり oldest drop にする。
        /// </summary>
        public int MaxQueueLength { get; private set; } = DefaultMaxQueueLength;

        /// <summary>
        /// HttpListener に渡す prefix。
        /// 例: http://127.0.0.1:5010/debugsocket/
        /// </summary>
        public string ListenerPrefix => $"http://{Host}:{Port}{NormalizePath(Path)}";

        /// <summary>
        /// 現在の transport 設定を人間向けに表した endpoint。
        /// diagnostics / bootstrap log ではこちらを優先して使う。
        /// </summary>
        public string EndpointDisplayName => TransportMode == DebugSocketTransportMode.Connect
            ? ConnectUri?.AbsoluteUri ?? ListenerPrefix
            : ListenerPrefix;

        /// <summary>
        /// AppConfig から DebugSocketOptions を構築する。
        /// ConfigFile → Environment → CommandLine の優先順は AppConfig 側で解決済み。
        /// </summary>
        public static DebugSocketOptions FromConfig(AppConfig config)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));

            var options = new DebugSocketOptions
            {
                Enabled = config.GetBool("debugSocket:enabled", false),
                AutoStart = config.GetBool("debugSocket:autoStart", true),
                TransportMode = ParseTransportMode(config.GetString("debugSocket:mode", "listen")),
                Host = config.GetString("debugSocket:host", DefaultHost),
                Port = config.GetInt("debugSocket:port", DefaultPort),
                Path = NormalizePath(config.GetString("debugSocket:path", DefaultPath)),
                AllowRemote = config.GetBool("debugSocket:allowRemote", false),
                SendLogs = config.GetBool("debugSocket:sendLogs", true),
                SendTelemetry = config.GetBool("debugSocket:sendTelemetry", true),
                MaxQueueLength = config.GetInt("debugSocket:maxQueueLength", DefaultMaxQueueLength),
                ConnectUri = ParseConnectUri(config.GetString("debugSocket:connectUri", string.Empty)),
            };

            ApplyTransportSpecificOverrides(options);

            Validate(options);
            return options;
        }

        /// <summary>
        /// path の書式ゆれを吸収し、常に /xxx/ 形式へ正規化する。
        /// </summary>
        public static string NormalizePath(string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return DefaultPath;
            }

            var normalized = path.Trim();
            if (!normalized.StartsWith("/", StringComparison.Ordinal))
            {
                normalized = "/" + normalized;
            }

            if (!normalized.EndsWith("/", StringComparison.Ordinal))
            {
                normalized += "/";
            }

            return normalized;
        }

        private static DebugSocketTransportMode ParseTransportMode(string? rawMode)
        {
            if (string.IsNullOrWhiteSpace(rawMode))
            {
                return DebugSocketTransportMode.Listen;
            }

            if (string.Equals(rawMode, "listen", StringComparison.OrdinalIgnoreCase))
            {
                return DebugSocketTransportMode.Listen;
            }

            if (string.Equals(rawMode, "connect", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(rawMode, "outbound", StringComparison.OrdinalIgnoreCase))
            {
                return DebugSocketTransportMode.Connect;
            }

            throw new InvalidOperationException(
                $"debugSocket:mode must be 'listen' or 'connect'. actual={rawMode}");
        }

        private static Uri? ParseConnectUri(string? rawConnectUri)
        {
            if (string.IsNullOrWhiteSpace(rawConnectUri))
            {
                return null;
            }

            if (!Uri.TryCreate(rawConnectUri.Trim(), UriKind.Absolute, out var connectUri) || connectUri == null)
            {
                throw new InvalidOperationException(
                    $"debugSocket:connectUri must be an absolute ws:// or wss:// URI. actual={rawConnectUri}");
            }

            if (!connectUri.Scheme.Equals("ws", StringComparison.OrdinalIgnoreCase) &&
                !connectUri.Scheme.Equals("wss", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"debugSocket:connectUri must use ws:// or wss://. actual={rawConnectUri}");
            }

            var builder = new UriBuilder(connectUri)
            {
                Path = NormalizePath(connectUri.AbsolutePath),
            };

            return builder.Uri;
        }

        private static void ApplyTransportSpecificOverrides(DebugSocketOptions options)
        {
            if (options.TransportMode != DebugSocketTransportMode.Connect || options.ConnectUri == null)
            {
                return;
            }

            options.Host = options.ConnectUri.Host;
            options.Port = options.ConnectUri.Port;
            options.Path = NormalizePath(options.ConnectUri.AbsolutePath);
        }

        private static void Validate(DebugSocketOptions options)
        {
            if (options.Port is <= 0 or > 65535)
            {
                throw new InvalidOperationException(
                    $"debugSocket:port must be between 1 and 65535. actual={options.Port}");
            }

            if (options.MaxQueueLength <= 0)
            {
                throw new InvalidOperationException(
                    $"debugSocket:maxQueueLength must be greater than 0. actual={options.MaxQueueLength}");
            }

            if (string.IsNullOrWhiteSpace(options.Host))
            {
                throw new InvalidOperationException("debugSocket:host must not be empty.");
            }

            if (!options.AllowRemote && !IsLoopbackHost(options.Host))
            {
                throw new InvalidOperationException(
                    $"debugSocket:host '{options.Host}' is not allowed while debugSocket:allowRemote=false.");
            }

            if (options.TransportMode == DebugSocketTransportMode.Connect && options.ConnectUri == null)
            {
                throw new InvalidOperationException(
                    "debugSocket:connectUri is required while debugSocket:mode=connect.");
            }
        }

        private static bool IsLoopbackHost(string host)
        {
            return host.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase)
                || host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
                || host.Equals("::1", StringComparison.OrdinalIgnoreCase);
        }
    }
}
