#nullable enable

using System;
using OneStarMaker.Runtime.BuildContent.Distribution;
using UnityEngine;

namespace OneStarMaker.Runtime.BuildContent
{
    [Serializable]
    // 上書きの directory の root が持つ「どの base に重ねる物として作ったか」の記録。上書きの build が書き、登録が読む。
    // 値は上書きの build の呼び手が渡した base の値の写しで、上書きがその base の内容から作られたことの証明ではない。
    // 登録はこの記録と base の検証済みの値を照合し、記録した base 以外に重ねさせない（それだけを保証する）。
    // Unity の serializer は class 型の field を null にできないため、必須の欄が 1 つでも空なら「記録無し」と読む
    // （上書きでない root、記録を知らない旧 build をそのまま受理する）。
    public sealed class BuildContentOverlayBase
    {
        [SerializeField] private string _baseContentSet = "";
        [SerializeField] private string _baseRevision = "";
        [SerializeField] private string _baseTarget = "";
        [SerializeField] private string _baseManifestSha256 = "";
        // 空文字は「base は宣言を持たない」。宣言の名前に空文字は無いので区別できる。
        [SerializeField] private string _baseDeclarationName = "";
        [SerializeField] private string _overlayContentSet = "";
        [SerializeField] private string _unityVersion = "";

        public BuildContentOverlayBase() { }

        /// <param name="baseManifestSha256">base の transport manifest の bytes の SHA-256（小文字 16 進 64 字）。</param>
        /// <param name="baseDeclarationName">base の宣言の名前。base が宣言を持たなければ null。</param>
        /// <param name="overlayContentSet">上書き自身の contentSet。</param>
        /// <param name="unityVersion">上書きを build した Editor の Unity の版。</param>
        public BuildContentOverlayBase(string baseContentSet, string baseRevision, string baseTarget, string baseManifestSha256,
            string? baseDeclarationName, string overlayContentSet, string unityVersion)
        {
            _baseContentSet = Require(baseContentSet, nameof(baseContentSet));
            _baseRevision = Require(baseRevision, nameof(baseRevision));
            _baseTarget = Require(baseTarget, nameof(baseTarget));
            if (!IsManifestSha256(baseManifestSha256))
                throw new ArgumentException("The base manifest digest must be 64 lowercase hexadecimal characters.", nameof(baseManifestSha256));
            _baseManifestSha256 = baseManifestSha256;
            if (baseDeclarationName != null && string.IsNullOrWhiteSpace(baseDeclarationName))
                throw new ArgumentException("A declaration name must be null (no declaration) or non-empty.", nameof(baseDeclarationName));
            _baseDeclarationName = baseDeclarationName ?? "";
            _overlayContentSet = Require(overlayContentSet, nameof(overlayContentSet));
            _unityVersion = Require(unityVersion, nameof(unityVersion));
        }

        public string BaseContentSet => _baseContentSet;
        public string BaseRevision => _baseRevision;
        public string BaseTarget => _baseTarget;
        public string BaseManifestSha256 => _baseManifestSha256;

        /// <summary>base の宣言の名前。base が宣言を持たなければ null。</summary>
        public string? BaseDeclarationName => string.IsNullOrEmpty(_baseDeclarationName) ? null : _baseDeclarationName;

        public string OverlayContentSet => _overlayContentSet;
        public string UnityVersion => _unityVersion;

        /// <summary>記録を持つか。必須の欄（宣言の名前以外）が 1 つでも空なら記録無し。</summary>
        public bool IsRecorded =>
            !string.IsNullOrEmpty(_baseContentSet) && !string.IsNullOrEmpty(_baseRevision) && !string.IsNullOrEmpty(_baseTarget)
            && !string.IsNullOrEmpty(_baseManifestSha256) && !string.IsNullOrEmpty(_overlayContentSet) && !string.IsNullOrEmpty(_unityVersion);

        /// <summary>
        /// transport manifest の digest の唯一の形（receipt の manifestSha256 と同じ、小文字 16 進 64 字）。
        /// 大文字・<c>sha256:</c> の前置き・空白を許すと、照合の ordinal 比較で正しい組が落ちるか、正規化の規則が 2 か所に分かれる。
        /// </summary>
        public static bool IsManifestSha256(string? value)
        {
            if (value == null || value.Length != 64) return false;
            foreach (var ch in value)
                if (!((ch >= '0' && ch <= '9') || (ch >= 'a' && ch <= 'f'))) return false;
            return true;
        }

        /// <summary>
        /// 記録と、同じ時に登録する base の検証済みの値を 5 欄とも ordinal で比べる。一致しない欄の名前を返す（一致なら null）。
        /// 宣言は片方だけ無い組も不一致（宣言付きの base に宣言なしの上書きを重ねない。逆も同じ）。
        /// </summary>
        internal static string? FindMismatch(BuildContentOverlayBase record, VerifiedInstalledRevision baseRevision, string? baseDeclarationName)
        {
            if (record == null) throw new ArgumentNullException(nameof(record));
            if (baseRevision == null) throw new ArgumentNullException(nameof(baseRevision));
            return FindMismatch(record, baseRevision.ContentSet, baseRevision.Identity, baseRevision.Target,
                baseRevision.ManifestSha256, baseDeclarationName);
        }

        internal static string? FindMismatch(BuildContentOverlayBase record, string contentSet, string revision, string target,
            string manifestSha256, string? declarationName)
        {
            if (record == null) throw new ArgumentNullException(nameof(record));
            if (!string.Equals(record.BaseContentSet, contentSet, StringComparison.Ordinal)) return "contentSet";
            if (!string.Equals(record.BaseRevision, revision, StringComparison.Ordinal)) return "revision";
            if (!string.Equals(record.BaseTarget, target, StringComparison.Ordinal)) return "target";
            if (!string.Equals(record.BaseManifestSha256, manifestSha256, StringComparison.Ordinal)) return "manifestSha256";
            if (!string.Equals(record.BaseDeclarationName, declarationName, StringComparison.Ordinal)) return "declarationName";
            return null;
        }

        private static string Require(string? value, string name)
        {
            if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("Overlay base record fields must not be empty.", name);
            return value!;
        }
    }
}
