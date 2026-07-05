#nullable enable

using System.Diagnostics;
using UnityEditor;
using Debug = UnityEngine.Debug;

namespace OneStarMaker.Editor.Build
{
    /// <summary>
    /// Editor Play Mode 起動時にローカル作業コピーの Git リビジョンを Runtime へ注入する。
    /// </summary>
    /// <remarks>
    /// <para>
    /// Runtime アセンブリは git コマンドを直接実行できないため、
    /// <see cref="OneStarMaker.Runtime.AssetManagement.RemoteCatalogRuntimeBridge.EditorLocalRevisionResolver"/>
    /// デリゲート経由で HEAD リビジョンを渡す。
    /// </para>
    /// <para>
    /// 起動時のリビジョンずれ検知 (<c>AbstractApplicationInitializer.WarnOnRevisionMismatchAsync</c>) が
    /// 本デリゲートを介してローカルリビジョンを解決する。
    /// </para>
    /// <para>
    /// ベストエフォート。git 取得失敗時は null を返し、比較処理は静かにスキップされる。
    /// </para>
    /// </remarks>
    [InitializeOnLoad]
    internal static class LocalRevisionEditorInjector
    {
        /// <summary>
        /// アセンブリロード時に Runtime ブリッジへローカルリビジョン解決子を登録する。
        /// </summary>
        static LocalRevisionEditorInjector()
        {
            OneStarMaker.Runtime.AssetManagement.RemoteCatalogRuntimeBridge.EditorLocalRevisionResolver = TryGetGitHead;
        }

        /// <summary>
        /// プロジェクトルートで <c>git rev-parse HEAD</c> を実行し、HEAD リビジョンを取得する。
        /// </summary>
        /// <returns>Git リビジョン。取得失敗時は null。</returns>
        private static string? TryGetGitHead()
        {
            try
            {
                var projectRoot = System.IO.Path.GetDirectoryName(UnityEngine.Application.dataPath);
                if (string.IsNullOrEmpty(projectRoot))
                {
                    return null;
                }

                using var process = new Process();
                process.StartInfo = new ProcessStartInfo
                {
                    FileName = "git",
                    Arguments = "rev-parse HEAD",
                    WorkingDirectory = projectRoot,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                };
                process.Start();
                if (!process.WaitForExit(3000))
                {
                    try { process.Kill(); } catch { /* best-effort */ }
                    return null;
                }

                if (process.ExitCode != 0)
                {
                    return null;
                }

                var revision = process.StandardOutput.ReadToEnd().Trim();
                return string.IsNullOrEmpty(revision) ? null : revision;
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[LocalRevisionEditorInjector] git rev-parse HEAD に失敗しました（比較スキップ）: {ex.Message}");
                return null;
            }
        }
    }
}
