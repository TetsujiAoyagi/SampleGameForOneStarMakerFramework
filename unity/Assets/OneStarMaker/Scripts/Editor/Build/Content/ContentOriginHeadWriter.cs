#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using OneStarMaker.Runtime.BuildContent.Distribution;

namespace OneStarMaker.Editor.Build.Content
{
    /// <summary>head の書き込みの結果。<see cref="Diagnostics"/> は head の段の成否を変えない後片付けの失敗。</summary>
    public sealed class ContentOriginHeadWriteResult
    {
        internal ContentOriginHeadWriteResult(string headPath, ContentOriginHeadFile head, IReadOnlyList<string> diagnostics)
        {
            HeadPath = headPath;
            Head = head;
            Diagnostics = diagnostics;
        }

        public string HeadPath { get; }
        public ContentOriginHeadFile Head { get; }
        public IReadOnlyList<string> Diagnostics { get; }
    }

    /// <summary>head を書き換えなかった失敗。head は前の bytes のまま（無かった時は無いまま）で、戻せなかった時だけ診断に backup の path がある。</summary>
    public sealed class ContentOriginHeadWriteException : IOException
    {
        internal ContentOriginHeadWriteException(string message, IReadOnlyList<string> diagnostics, Exception? inner = null)
            : base(message + (diagnostics.Count == 0 ? "" : " " + string.Join(" ", diagnostics)), inner) => Diagnostics = diagnostics;

        public IReadOnlyList<string> Diagnostics { get; }
    }

    /// <summary>
    /// 公開済みの 1 revision の directory の head を、その親の directory（取得元の root）へ置き換えで書く。head の bytes は
    /// <see cref="ContentOriginHead.Create"/> が作り、ここは file の操作だけを持つ。取得元の root は公開先の親に固定し、呼び手は渡さない。
    /// <para>
    /// 契約: lock は持たず、後から書いた方が勝つ（同じ root の書き手は同時に 1 つを前提にし、破れても他の書き手の head を壊さない）。
    /// 取り消しの口は持たないので、呼び手は確定の後の最後の段で呼ぶ。置き換え（<c>File.Replace</c> か <c>File.Move</c>）が成功した時点が
    /// 成功で、その後の backup・別名の file の削除の失敗は診断に残すだけ。読み手に「head が無い」瞬間を見せない。
    /// 今ある head が未対応の版か壊れている時は、書き換えずに失敗する（古い道具が新しい版の head を潰さない）。
    /// </para>
    /// </summary>
    public static class ContentOriginHeadWriter
    {
        public static ContentOriginHeadWriteResult Write(string publishedDirectory) => Write(publishedDirectory, Operations.Default);

        /// <summary>file の操作と待ちと時計。test は置き換えの失敗（部分失敗を含む）と並んだ書き手を注入する。</summary>
        internal sealed class Operations
        {
            internal Operations(Action<string, string, string> replace, Action<string, string> move, Action<int> wait, Func<long>? elapsedMilliseconds,
                bool windows, Action<string, byte[]>? writeAllBytes = null)
            {
                WriteAllBytes = writeAllBytes ?? File.WriteAllBytes;
                Replace = replace;
                Move = move;
                Wait = wait;
                ElapsedMilliseconds = elapsedMilliseconds;
                Windows = windows;
            }

            internal static Operations Default { get; } = new(
                (source, destination, backup) => File.Replace(source, destination, backup, ignoreMetadataErrors: true),
                File.Move, System.Threading.Thread.Sleep, null, Environment.OSVersion.Platform == PlatformID.Win32NT);

            /// <summary>別名の file へ head の bytes を書く。test は途中まで書いてから失敗する書き込みを注入する。</summary>
            internal Action<string, byte[]> WriteAllBytes { get; }
            /// <summary>(別名の file, head, backup)。head を backup へ移してから別名を head にする。</summary>
            internal Action<string, string, string> Replace { get; }
            /// <summary>既にある宛先を上書きしない移動。</summary>
            internal Action<string, string> Move { get; }
            internal Action<int> Wait { get; }
            internal Func<long>? ElapsedMilliseconds { get; }
            internal bool Windows { get; }
        }

        internal static ContentOriginHeadWriteResult Write(string publishedDirectory, Operations operations)
        {
            if (string.IsNullOrWhiteSpace(publishedDirectory)) throw new ArgumentException("Published directory is required.", nameof(publishedDirectory));
            var published = Path.GetFullPath(publishedDirectory).TrimEnd(Path.DirectorySeparatorChar);
            var root = Path.GetDirectoryName(published) ?? throw new ArgumentException("Published directory has no parent.", nameof(publishedDirectory));
            var manifestPath = Path.Combine(published, "transport.json");
            var draft = ContentOriginHead.Create(File.ReadAllBytes(manifestPath), Path.GetFileName(published));
            var headPath = Path.Combine(root, draft.RelativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(headPath)!);
            RejectUnreadableHead(headPath, draft.Head);

            var token = Guid.NewGuid().ToString("N");
            var temporary = headPath + "." + token + ".tmp";
            var backup = headPath + "." + token + ".bak";
            var diagnostics = new List<string>();
            var clock = Stopwatch.StartNew();
            long Elapsed() => operations.ElapsedMilliseconds?.Invoke() ?? clock.ElapsedMilliseconds;
            string? failure = null;
            Exception? failureCause = null;
            try
            {
                // 別名の file の書き込みも後片付けの範囲に入れる（途中まで書いて失敗した file を残さない）。
                try { operations.WriteAllBytes(temporary, draft.Bytes); }
                catch (Exception error)
                {
                    failure = "The origin head could not be written: " + temporary + ". " + error.Message;
                    failureCause = error;
                }
                for (var attempt = 1; failureCause == null; attempt++)
                {
                    try
                    {
                        Place(temporary, headPath, backup, operations);
                        break;
                    }
                    catch (Exception error)
                    {
                        RestoreIfHeadMissing(headPath, backup, diagnostics, operations);
                        // 一時的な失敗（開発の PC の監視 tool）だけを、ContentStagingMove と同じ判定・同じ上限でやり直す。
                        // directory 用の移し直しの条件は使えないので、ここでは別名の file が残っていることだけを条件にする。
                        var transient = operations.Windows && ContentStagingMove.IsTransient(error) && File.Exists(temporary);
                        if (!transient || attempt >= ContentStagingMove.MaxAttempts
                                       || Elapsed() + ContentStagingMove.WaitMilliseconds > ContentStagingMove.BudgetMilliseconds)
                        {
                            failure = "The origin head could not be replaced: " + headPath + ". " + error.Message;
                            failureCause = error;
                            break;
                        }
                        operations.Wait(ContentStagingMove.WaitMilliseconds);
                    }
                }
            }
            finally
            {
                TryDelete(temporary, diagnostics);
            }
            // 失敗の例外は後片付けの後に作る（後片付けの失敗の診断を、呼び手が記録する Message に含める）。
            if (failureCause != null) throw new ContentOriginHeadWriteException(failure!, diagnostics, failureCause);
            // 置き換えは成功した。backup の削除の失敗は head の段を失敗にしない（公開先を消すと head が消えた revision を指す）。
            TryDelete(backup, diagnostics);
            return new ContentOriginHeadWriteResult(headPath, draft.Head, diagnostics);
        }

        private static void Place(string temporary, string headPath, string backup, Operations operations)
        {
            if (File.Exists(headPath))
            {
                operations.Replace(temporary, headPath, backup);
                return;
            }
            try { operations.Move(temporary, headPath); }
            catch (IOException) when (File.Exists(headPath) && File.Exists(temporary))
            {
                // head が無い時の最初の書き込みが並び、相手が先に置いた。置き換えでやり直す（後から書いた方が勝つ）。
                operations.Replace(temporary, headPath, backup);
            }
        }

        // Win32 の ReplaceFile は、置き換えられる側を backup へ移したまま失敗しうる（ERROR_UNABLE_TO_MOVE_REPLACEMENT_2）。
        // head が無ければ backup を戻す。Move は既にある head を上書きしないので、その間に別の書き手が書いた head は壊さない。
        private static void RestoreIfHeadMissing(string headPath, string backup, List<string> diagnostics, Operations operations)
        {
            if (!File.Exists(backup)) return;
            if (File.Exists(headPath))
            {
                diagnostics.Add("Another writer placed a head meanwhile; the previous head is left at " + backup + ".");
                return;
            }
            try { operations.Move(backup, headPath); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                diagnostics.Add(File.Exists(headPath)
                    ? "Another writer placed a head meanwhile; the previous head is left at " + backup + "."
                    : "The previous head could not be restored and remains at " + backup + " (" + ex.Message + ").");
            }
        }

        private static void RejectUnreadableHead(string headPath, ContentOriginHeadFile head)
        {
            if (!File.Exists(headPath)) return;
            var read = ContentOriginHead.Read(File.ReadAllBytes(headPath), head.contentSet, head.target);
            if (read.State is ContentOriginHeadState.Unsupported or ContentOriginHeadState.Invalid)
                throw new ContentOriginHeadWriteException("The existing origin head is " + read.State + " and is left unchanged: " + headPath
                                                          + " (" + read.Reason + ")", Array.Empty<string>());
        }

        private static void TryDelete(string path, List<string> diagnostics)
        {
            try { if (File.Exists(path)) File.Delete(path); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                diagnostics.Add("Could not delete " + path + " (" + ex.Message + ").");
            }
        }
    }
}
