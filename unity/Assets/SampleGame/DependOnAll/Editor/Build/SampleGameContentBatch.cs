#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using OneStarMaker.Build.Selection;
using OneStarMaker.Editor.Build.Content;
using UnityEditor;
using UnityEngine;

namespace SampleGame.DependOnAll.Editor.Build
{
    /// <summary>
    /// Editor の外（CI・別 PC）から宣言名だけで Full build と publish を行う batch 入口。
    /// <code>
    /// Unity.exe -batchmode -nographics -projectPath unity
    ///   -executeMethod SampleGame.DependOnAll.Editor.Build.SampleGameContentBatch.BuildAndPublish
    ///   -osmPurpose spring-full -logFile &lt;log&gt;
    /// </code>
    /// 成否の正は process の終了コード。結果 file（<c>artifacts/bs2b/batch/last-result.json</c>）は終了コード 0 の時だけ
    /// 今回の結果として読む。Unity がこの method を呼ぶ前に終わる場合（project が別の Editor で開かれている、compile error）は
    /// 結果 file に触れられず、前回の file が残るため。Unity の起動の仕方（wrapper、CI への組み込み）は呼ぶ側の責務。
    /// <para>
    /// 最後の段で、公開先の親（取得元の root）の head を今回の publish に向ける。head の失敗は公開先と結果 file を残し、
    /// 結果 file の <c>originHeadFailure</c> と終了コード <see cref="ExitOriginHeadFailed"/> で示す（正しく作れた公開物を消さない）。
    /// head を書き終えたら、head の path・revision・digest を持つ最後の log の行を書く。
    /// </para>
    /// </summary>
    public static class SampleGameContentBatch
    {
        public const int ExitSuccess = 0;
        public const int ExitUnexpected = 1;
        public const int ExitInvalidArguments = 2;
        public const int ExitSelectionFailed = 3;
        public const int ExitBuildFailed = 4;
        public const int ExitPublishFailed = 5;
        /// <summary>publish と結果 file は残っているが、取得元の head を今回の publish に向けられなかった。</summary>
        public const int ExitOriginHeadFailed = 6;

        internal const string PurposeArgument = "-osmPurpose";
        internal const string ResultFileName = "last-result.json";
        // publish する build は Full だけ（workspace に残った前回の選択の file を配布しない）。
        internal const BuildContentMode BuildMode = BuildContentMode.Full;
        private const string LogPrefix = "[SampleGameContentBatch] ";

        public static void BuildAndPublish()
        {
            int code;
            try
            {
                var artifactsRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "artifacts", "bs2b"));
                code = Run(Environment.GetCommandLineArgs(), Stages.Default, artifactsRoot);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                code = ExitUnexpected;
            }
            // -quit に頼らず、全ての分岐で終了コードを返す。
            EditorApplication.Exit(code);
        }

        /// <summary>
        /// 1 回の batch。published directory（<c>transport/&lt;identity&gt;</c>）はこの実行が作ったものだけを所有し、
        /// 成功結果の確定より前に失敗すればこの実行が消す。他の published directory には触らない。
        /// </summary>
        internal static int Run(IReadOnlyList<string> args, Stages stages, string artifactsRoot)
        {
            var batchDirectory = Path.Combine(artifactsRoot, "batch");
            var resultPath = Path.Combine(batchDirectory, ResultFileName);
            // 前回の結果を今回の結果と読める状態を残さない。ここで消せなければ例外のまま終了コード 1 になる。
            Directory.CreateDirectory(batchDirectory);
            if (File.Exists(resultPath)) File.Delete(resultPath);

            string? published = null;
            int Fail(int code, string reason)
            {
                stages.LogError(LogPrefix + "exitCode=" + code + " " + reason);
                if (published != null && Directory.Exists(published))
                {
                    try { Directory.Delete(published, true); }
                    catch (Exception ex) { stages.LogError(LogPrefix + ex); }
                }
                // 失敗の結果 file は exitCode と理由だけ。書けなくても終了コードが正なので、失敗の上塗りはしない。
                try { stages.Commit(resultPath, JsonUtility.ToJson(new FailureFile { exitCode = code, reason = reason }, true)); }
                catch (Exception ex) { stages.LogError(LogPrefix + ex); }
                return code;
            }

            try
            {
                var name = ReadPurpose(args);
                if (name == null) return Fail(ExitInvalidArguments, "Missing " + PurposeArgument + " <declaration name>.");
                BuildPurposeDeclaration declaration;
                try { declaration = SampleGameBuildPurposes.Find(name); }
                catch (ArgumentException ex) { return Fail(ExitInvalidArguments, ex.Message); }

                BuildContentResult result;
                try { result = stages.Build(declaration); }
                catch (PurposeSelectionException ex) { return Fail(ExitSelectionFailed, ex.Message); }
                if (!result.IsSuccess)
                {
                    // preflight の issue は選択の失敗、BuildFailure は Unity の build（とその前の workspace の削除）の失敗。
                    var code = result.Issues.Length != 0 && result.Issues.All(x => x.Code != BuildContentIssueCode.BuildFailure)
                        ? ExitSelectionFailed : ExitBuildFailed;
                    return Fail(code, "Content build failed: " + result.OutcomePath + " / " +
                        string.Join("; ", result.Issues.Select(x => x.Code + ":" + x.Key + ":" + x.Detail)));
                }

                var publishRoot = Path.Combine(artifactsRoot, "transport", result.Identity);
                try { published = stages.Publish(result, publishRoot); }
                catch (Exception ex) { return Fail(ExitPublishFailed, ex.ToString()); }

                // 欄は BS5b-2 の From published directory が期待値として読む。名前と意味を変えない。
                var success = new SuccessFile
                {
                    identity = result.Identity,
                    publishPath = published,
                    manifestSha256 = Sha256(Path.Combine(published, "transport.json")),
                    declaration = declaration.Name,
                    buildMode = result.Mode.ToString(),
                    exitCode = ExitSuccess,
                };
                stages.Commit(resultPath, JsonUtility.ToJson(success, true));
                Debug.Log(LogPrefix + "published " + published);
                return WriteOriginHead(stages, published, resultPath, success);
            }
            catch (Exception ex)
            {
                // publish の後の結果の確定の失敗もここに来る。published directory を消してから 1 を返す。
                return Fail(ExitUnexpected, ex.ToString());
            }
        }

        /// <summary>
        /// 結果の確定の後の最後の段。head の取り消し（巻き戻し）は持たず、読み手は確定していない publish を指す head を見ない。
        /// 置き換えが成功した時点が成功で、その後の掃除と log の失敗は診断だけ（<c>Fail</c> に入れると、公開先を消して head が
        /// 消えた revision を指す）。失敗は公開先を残し、成功の結果に head の失敗の欄を足した結果で置き換える。その置き換えも
        /// 失敗した時は欄の無い成功の結果 file が残りうるので、成否の正は終了コードのまま。
        /// </summary>
        private static int WriteOriginHead(Stages stages, string published, string resultPath, SuccessFile success)
        {
            ContentOriginHeadWriteResult head;
            try { head = stages.WriteHead(published); }
            catch (Exception ex)
            {
                stages.LogError(LogPrefix + "exitCode=" + ExitOriginHeadFailed + " The origin head was not updated: " + ex);
                success.exitCode = ExitOriginHeadFailed;
                success.originHeadFailure = ex.Message;
                try { stages.Commit(resultPath, JsonUtility.ToJson(success, true)); }
                catch (Exception commit) { stages.LogError(LogPrefix + "The result file could not record the head failure: " + commit); }
                return ExitOriginHeadFailed;
            }
            try
            {
                foreach (var diagnostic in head.Diagnostics) Debug.LogWarning(LogPrefix + diagnostic);
                Debug.Log(LogPrefix + HeadLogLine(head));
            }
            catch (Exception)
            {
                // head の置き換えは成功している。log の失敗で終了コードを変えない。
            }
            return ExitSuccess;
        }

        /// <summary>batch の最後の log の行。判定の担当が、期限の後に止めた batch の全ての段が終わった証拠として読む。</summary>
        internal static string HeadLogLine(ContentOriginHeadWriteResult head) =>
            "origin head written path=" + head.HeadPath + " revision=" + head.Head.revision + " manifestSha256=" + head.Head.manifestSha256;

        private static string? ReadPurpose(IReadOnlyList<string> args)
        {
            for (var i = 0; i < args.Count - 1; i++)
                if (string.Equals(args[i], PurposeArgument, StringComparison.Ordinal))
                    return string.IsNullOrWhiteSpace(args[i + 1]) || args[i + 1].StartsWith("-", StringComparison.Ordinal)
                        ? null : args[i + 1];
            return null;
        }

        // 一時 file に書いてから move で確定する。読み手が書きかけの結果を読まない。
        // head の失敗の後に、確定した成功の結果を欄つきの結果で置き換えるので、既にある file は File.Replace で置き換える。
        internal static void CommitResult(string path, string json)
        {
            var temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllText(temp, json, new UTF8Encoding(false));
                if (File.Exists(path)) File.Replace(temp, path, null);
                else File.Move(temp, path);
            }
            finally
            {
                if (File.Exists(temp)) File.Delete(temp);
            }
        }

        private static string Sha256(string path)
        {
            using var stream = File.OpenRead(path);
            using var hash = System.Security.Cryptography.SHA256.Create();
            return string.Concat(hash.ComputeHash(stream).Select(x => x.ToString("x2")));
        }

        /// <summary>
        /// batch の段。テストは偽の段を注入し、Unity の build なしで終了コードと所有の規則を確かめる。
        /// 失敗の log も段にする。Unity Test Framework は期待していない error log をテストの失敗にし、
        /// テストの asmdef は LogAssert（UnityEngine.TestRunner）を参照しないため、失敗経路のテストは log を受け取って確かめる。
        /// </summary>
        internal sealed class Stages
        {
            public Stages(Func<BuildPurposeDeclaration, BuildContentResult> build, Func<BuildContentResult, string, string> publish,
                Action<string, string> commit, Action<string> logError, Func<string, ContentOriginHeadWriteResult> writeHead)
            { Build = build; Publish = publish; Commit = commit; LogError = logError; WriteHead = writeHead; }

            public static Stages Default { get; } = new Stages(
                declaration => SampleGameContentBuild.BuildUnchecked(declaration, BuildMode),
                ContentTransportPublisher.Publish,
                CommitResult,
                message => Debug.LogError(message),
                ContentOriginHeadWriter.Write);

            public Func<BuildPurposeDeclaration, BuildContentResult> Build { get; }
            public Func<BuildContentResult, string, string> Publish { get; }
            public Action<string, string> Commit { get; }
            public Action<string> LogError { get; }
            /// <summary>公開先の directory を受け、その親（取得元の root）の head を書く。置き換えの失敗と掃除の失敗を注入する口。</summary>
            public Func<string, ContentOriginHeadWriteResult> WriteHead { get; }
        }

        [Serializable]
        internal sealed class SuccessFile
        {
            public string identity = "";
            public string publishPath = "";
            public string manifestSha256 = "";
            public string declaration = "";
            public string buildMode = "";
            public int exitCode;
            // head の書き込みが失敗した時の理由（終了コード 6）。成功の時は空。
            public string originHeadFailure = "";
        }

        [Serializable]
        internal sealed class FailureFile
        {
            public int exitCode;
            public string reason = "";
        }
    }
}
