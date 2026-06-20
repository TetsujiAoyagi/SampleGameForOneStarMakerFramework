namespace DebugStudio.Server;

/// <summary>
/// DebugStudio WebSocket server の設定。
/// </summary>
public record DebugStudioServerOptions
{
    /// <summary>
    /// リッスンアドレス（既定: 127.0.0.1）。
    /// </summary>
    public string Host { get; set; } = "127.0.0.1";

    /// <summary>
    /// リッスンポート（既定: 5011）。
    /// </summary>
    public int Port { get; set; } = 5011;

    /// <summary>
    /// WebSocket エンドポイントのパス（既定: /debugsocket/）。
    /// </summary>
    public string WebSocketPath { get; set; } = "/debugsocket/";

    /// <summary>
    /// サーバーを有効にするか。
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// クライアント接続を待つ最大時間（秒）。
    /// </summary>
    public int AcceptTimeoutSeconds { get; set; } = 60;

    /// <summary>
    /// HttpListener に登録する prefix を返す。
    /// path の先頭/末尾スラッシュゆれはここで正規化する。
    /// </summary>
    public string GetListenerPrefix()
    {
        var normalizedPath = string.IsNullOrWhiteSpace(WebSocketPath) ? "/" : WebSocketPath.Trim();

        if (!normalizedPath.StartsWith('/'))
        {
            normalizedPath = "/" + normalizedPath;
        }

        if (!normalizedPath.EndsWith('/'))
        {
            normalizedPath += "/";
        }

        return $"http://{Host}:{Port}{normalizedPath}";
    }
}
