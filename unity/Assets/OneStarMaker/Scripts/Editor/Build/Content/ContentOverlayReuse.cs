#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Text;
using OneStarMaker.Build.Selection;
using OneStarMaker.Editor.Build.Materialization;
using UnityEngine;

namespace OneStarMaker.Editor.Build.Content
{
    /// <summary>
    /// 上書きの再利用: 入力の鍵と、contentSet ごとの最後の成功 1 件を指す記録（成果物の root の下の <c>overlay/&lt;contentSet&gt;.json</c>）。
    /// 記録は鍵と、成功した build の <c>content/&lt;identity&gt;/</c> の全ての file の相対 path と SHA-256 を持ち、返す前に全て確かめる
    /// （鍵は入力しか見ないので、成果物の file が欠けた・書き換わった時に同じ鍵で返り続けないように）。
    /// 記録は reports/・workspace・publish 先・installed cache の外に置く（Full の build が workspace を消しても残り、走査も要らない）。
    /// </summary>
    /// <remarks>
    /// 鍵に入らない入力（script、project の設定、package）は再利用の既知の制約である。Framework の build の code の意味を変えた時は
    /// <see cref="KeyVersion"/> を上げて、古い上書きを返さないようにする。
    /// </remarks>
    internal static class ContentOverlayReuse
    {
        internal const int KeyVersion = 1;
        private const int RecordVersion = 1;

        internal static string RecordPath(string artifactsRoot, string contentSet) =>
            Path.Combine(Path.GetFullPath(artifactsRoot), "overlay", contentSet + ".json");

        // 上書きの入力の鍵。base の識別の 5 欄、上書きの contentSet、target、Unity の版、root に写る宣言の値、
        // 上書きの root ごとの stable key・logical key・表現・root の GUID と path、上書きの閉包の全ての file の GUID・path・file と .meta の SHA-256。
        // 認めた集合は鍵に入れない（上書きの中身を変えない。認めは受け取りの口が毎回求める）。
        internal static string ComputeKey(ContentOverlayBaseIdentity baseIdentity, string contentSet, string target, string unityVersion,
            BuildPurposeDeclaration? declaration, IEnumerable<(BuildContentCandidate Candidate, string Representation, string Path)> roots,
            IEnumerable<BuildDependencyEntry> closure, IReadOnlyDictionary<string, string?> hashes)
        {
            var text = new StringBuilder();
            void Line(params string?[] parts)
            {
                // 長さを前置して連結境界を一意にする（区切り文字を値に含む入力が別の鍵と衝突しないように）。
                foreach (var part in parts)
                {
                    var value = part ?? "\0";
                    text.Append(value.Length.ToString(CultureInfo.InvariantCulture)).Append(':').Append(value);
                }
                text.Append('\n');
            }
            Line("osm-overlay-key", KeyVersion.ToString(CultureInfo.InvariantCulture));
            Line("base", baseIdentity.ContentSet, baseIdentity.Revision, baseIdentity.Target, baseIdentity.ManifestDigest, baseIdentity.DeclarationName);
            Line("overlay", contentSet, target, unityVersion);
            if (declaration == null) Line("declaration");
            else
            {
                Line("declaration", declaration.Name, declaration.Representation.Primary);
                foreach (var pair in declaration.RuntimeDefaults.OrderBy(x => x.Key, StringComparer.Ordinal)) Line("default", pair.Key, pair.Value);
            }
            foreach (var root in roots.OrderBy(x => x.Candidate.StableKey, StringComparer.Ordinal))
                Line("root", root.Candidate.StableKey, root.Candidate.LogicalKey, root.Representation, root.Candidate.PhysicalKey, root.Path);
            foreach (var entry in closure.OrderBy(x => x.Path, StringComparer.Ordinal))
            {
                hashes.TryGetValue(entry.Path, out var file);
                hashes.TryGetValue(entry.Path + ContentSourceHashes.MetaSuffix, out var meta);
                Line("file", entry.Guid, entry.Path, file, meta);
            }
            return ContentSourceHashes.Sha256Text(text.ToString());
        }

        // 成功した上書きの後に呼ぶ。成果物の file を hash して、contentSet の記録をこの build に差し替える。
        // 失敗は例外で返す。呼び手（coordinator）は成功した build を失敗にせず、理由を結果に写す。
        // 置き換えは 1 回の操作（File.Replace）で行い、既存の記録を先に消す段を持たない。途中で失敗しても既存の記録は
        // 前の bytes のまま読める（鍵が違うので、今の計画には返らない）。別名の file は成否によらず消しにいくが、その削除自体が
        // 失敗すると残りうる。その時は黙って残さず、別名を消せなかったことと別名の絶対 path を例外の文言に書く（呼び手は文言だけを
        // 結果に写すので、内側の例外だけに置くと落ちる）。名前は contentSet ごとに 1 つで固定なので、残っても増えない。次の Write が
        // 書き込みまで進んでその path を開ければ書き直すが、削除を拒んだ lock や読み取り専用が続いていれば残り続ける。TryGet は読まない。
        internal static void Write(string artifactsRoot, string contentSet, string key, BuildContentResult result)
            => Write(artifactsRoot, contentSet, key, result, (temp, path) =>
            {
                if (File.Exists(path)) File.Replace(temp, path, null);
                else File.Move(temp, path);
            }, File.Delete);

        // 置き換え（別名 → 記録）と別名の削除を注入できる形。注入した操作はこの呼び出しだけのもので、test は両方の失敗を起こす。
        internal static void Write(string artifactsRoot, string contentSet, string key, BuildContentResult result,
            Action<string, string> replace, Action<string> deleteTemp)
        {
            var root = Path.GetFullPath(artifactsRoot);
            var content = Path.GetFullPath(result.ContentPath!);
            var record = new ReuseRecord
            {
                version = RecordVersion, key = key, identity = result.Identity,
                outcomePath = Path.GetRelativePath(root, result.OutcomePath).Replace('\\', '/'),
                contentPath = Path.GetRelativePath(root, content).Replace('\\', '/'),
                files = Directory.GetFiles(content, "*", SearchOption.AllDirectories)
                    .Select(x => new ReuseFile { path = Path.GetRelativePath(content, x).Replace('\\', '/'), sha256 = ContentSourceHashes.Sha256File(x) ?? "" })
                    .OrderBy(x => x.path, StringComparer.Ordinal).ToArray(),
            };
            var path = RecordPath(root, contentSet);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            // 書きかけの記録を読まないよう、別名に書いてから置き換える。
            var temp = path + ".tmp";
            Exception? failure = null;
            try
            {
                File.WriteAllText(temp, JsonUtility.ToJson(record, true), new UTF8Encoding(false));
                replace(temp, path);
            }
            catch (Exception error) { failure = error; }

            // 置き換えが成功していれば別名は普通は既に無い。成否によらず残った別名は消しにいく。
            Exception? cleanup = null;
            try { if (File.Exists(temp)) deleteTemp(temp); }
            catch (Exception error) { cleanup = error; }

            if (cleanup == null)
            {
                // 別名が残らない時は今までどおり元の失敗だけを返す。
                if (failure != null) ExceptionDispatchInfo.Capture(failure).Throw();
                return;
            }
            // 元の失敗も後片付けの失敗も結果から消さない。どちらの文言も、別名の path も、呼び手が写す Message に書く。
            var remains = "the temporary reuse record file could not be deleted and remains at '" + temp + "': " + cleanup.Message;
            if (failure == null) throw new IOException("The reuse record was replaced, but " + remains, cleanup);
            throw new IOException(failure.Message + " Also, " + remains, new AggregateException(failure, cleanup));
        }

        // 鍵が同じで、前回の outcome が成功で、記録した成果物の file が全て同じ内容で残る時だけ前回の結果を返す。それ以外は null。
        // 記録に無い file が成果物の directory に増えただけでは拒まない（Play の間に Unity がこの directory へ書くかは確かめていない）。
        internal static BuildContentResult? TryGet(string artifactsRoot, string contentSet, string key)
        {
            var root = Path.GetFullPath(artifactsRoot);
            var recordPath = RecordPath(root, contentSet);
            if (!File.Exists(recordPath)) return null;
            ReuseRecord? record;
            ContentReportFile? outcome;
            try
            {
                record = JsonUtility.FromJson<ReuseRecord>(File.ReadAllText(recordPath));
                if (record == null || record.version != RecordVersion || !string.Equals(record.key, key, StringComparison.Ordinal)
                    || string.IsNullOrEmpty(record.outcomePath) || string.IsNullOrEmpty(record.contentPath) || record.files == null || record.files.Length == 0)
                    return null;
                var outcomePath = Path.GetFullPath(Path.Combine(root, record.outcomePath));
                if (!File.Exists(outcomePath)) return null;
                outcome = JsonUtility.FromJson<ContentReportFile>(File.ReadAllText(outcomePath));
            }
            catch (Exception) { return null; }
            if (outcome == null || outcome.identity != record.identity || !outcome.overlay || outcome.issues.Length != 0
                || string.IsNullOrEmpty(outcome.contentPath) || outcome.buildMode != nameof(BuildContentMode.Full)
                || !string.Equals(outcome.overlayKey, key, StringComparison.Ordinal))
                return null;
            var content = Path.GetFullPath(Path.Combine(root, record.contentPath));
            if (!Directory.Exists(content) || !File.Exists(outcome.manifestPointer) || !Directory.Exists(outcome.metadataPath)) return null;
            foreach (var file in record.files)
            {
                var full = Path.GetFullPath(Path.Combine(content, file.path));
                if (!full.StartsWith(content + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) return null;
                if (!string.Equals(ContentSourceHashes.Sha256File(full), file.sha256, StringComparison.Ordinal)) return null;
            }
            var reportDirectory = Path.GetDirectoryName(Path.GetFullPath(Path.Combine(root, record.outcomePath)))!;
            return new BuildContentResult(record.identity, Path.Combine(reportDirectory, "preflight.json"), Path.Combine(reportDirectory, "outcome.json"),
                content, outcome.manifestPointer, outcome.metadataPath, outcome.summary, Array.Empty<BuildContentIssue>(), null,
                BuildContentMode.Full, null, key);
        }

        [Serializable] private sealed class ReuseRecord
        {
            public int version;
            public string key = "";
            public string identity = "";
            public string outcomePath = "";
            public string contentPath = "";
            public ReuseFile[] files = Array.Empty<ReuseFile>();
        }

        [Serializable] private sealed class ReuseFile
        {
            public string path = "";
            public string sha256 = "";
        }
    }
}
