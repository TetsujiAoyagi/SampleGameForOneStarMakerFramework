#nullable enable

using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace OneStarMaker.Runtime.BuildContent.Distribution
{
    /// <summary>
    /// 取得元の head の file の形（版 1）。取得元の root の <c>.origin/&lt;contentSet&gt;/&lt;target&gt;.json</c> に置く。
    /// path は root からの相対の 1 revision の directory（その下に <c>transport.json</c> と <c>content/</c>）。時刻の欄は持たない。
    /// 書き手が別の assembly（Editor の publish）に居るので public にする。
    /// </summary>
    [Serializable]
    public sealed class ContentOriginHeadFile
    {
        public int version = ContentOriginHead.Version;
        public string contentSet = "";
        public string target = "";
        public string revision = "";
        public string manifestSha256 = "";
        public string path = "";
    }

    public enum ContentOriginHeadState
    {
        /// <summary>版 1 として読めて、照会の contentSet・target と合う。</summary>
        Present,
        /// <summary>版 0（head なし）。file の不在は取得元が自分で確かめて「head なし」と答える。</summary>
        None,
        /// <summary>版 2 以上。読み手は使わない。</summary>
        Unsupported,
        /// <summary>形の失敗（大きさ、JSON、型、欄の欠け、相対でない path、hash の形、照会と違う contentSet・target）。</summary>
        Invalid,
    }

    public sealed class ContentOriginHeadRead
    {
        internal ContentOriginHeadRead(ContentOriginHeadState state, ContentOriginHeadFile? head, string reason)
        {
            State = state;
            Head = head;
            Reason = reason;
        }

        public ContentOriginHeadState State { get; }
        /// <summary><see cref="ContentOriginHeadState.Present"/> の時だけ在る。</summary>
        public ContentOriginHeadFile? Head { get; }
        public string Reason { get; }
    }

    /// <summary><see cref="ContentOriginHead.Create"/> が作った head の bytes と、その中身。</summary>
    public sealed class ContentOriginHeadDraft
    {
        internal ContentOriginHeadDraft(byte[] bytes, ContentOriginHeadFile head)
        {
            Bytes = bytes;
            Head = head;
        }

        public byte[] Bytes { get; }
        public ContentOriginHeadFile Head { get; }
        /// <summary>取得元の root からの head の file の相対 path。</summary>
        public string RelativePath => ContentOriginHead.RelativePath(Head.contentSet, Head.target);
    }

    /// <summary>
    /// head の形の規則を 1 か所に持つ: 作り方（書き手用）と、bytes からの読みと検査（全ての取得元用）。
    /// 読みの規則は <see cref="ContentPurposeIndex"/> に揃える（版 0 は「なし」、2 以上は「未対応」、形の失敗は「壊れている」）。
    /// </summary>
    public static class ContentOriginHead
    {
        public const int Version = 1;
        public const int MaxBytes = 64 * 1024;
        private const string HeadDirectory = ".origin";

        public static string RelativePath(string contentSet, string target)
        {
            ContentOriginQuery.RequireSegment(contentSet, nameof(contentSet));
            ContentOriginQuery.RequireSegment(target, nameof(target));
            return HeadDirectory + "/" + contentSet + "/" + target + ".json";
        }

        /// <summary>
        /// 公開した <c>transport.json</c> の bytes から contentSet・target・revision と SHA-256 を読んで head の bytes を作る。
        /// 書き手が hash と manifest の解析を写さずに済むよう、ここに置く。manifest の互換（Unity の版など）は見ない
        /// （読み手の installer が見る。head は「何を勧めるか」だけを運ぶ）。
        /// </summary>
        public static ContentOriginHeadDraft Create(byte[] manifestBytes, string relativePath)
        {
            if (manifestBytes == null) throw new ArgumentNullException(nameof(manifestBytes));
            if (manifestBytes.Length > ContentManifestValidation.MaxManifestBytes)
                throw new ContentDeliveryException(ContentDeliveryFailureCode.InvalidManifest, "Manifest exceeds its byte limit.");
            if (!ContentManifestValidation.Relative(relativePath))
                throw new ArgumentException("Head path must be relative to the origin root.", nameof(relativePath));
            ContentTransportManifest manifest;
            try
            {
                var json = new UTF8Encoding(false, true).GetString(manifestBytes);
                ContentManifestValidation.ValidateRequiredFields(json);
                manifest = JsonUtility.FromJson<ContentTransportManifest>(json) ?? throw new FormatException();
            }
            catch (Exception ex) when (ex is not ContentDeliveryException)
            {
                throw new ContentDeliveryException(ContentDeliveryFailureCode.InvalidManifest, "Transport manifest JSON is invalid.", ex);
            }
            if (!ContentManifestValidation.Segment(manifest.contentSet) || !ContentManifestValidation.Segment(manifest.target)
                || !ContentManifestValidation.Segment(manifest.revision))
                throw new ContentDeliveryException(ContentDeliveryFailureCode.InvalidManifest, "Manifest identity is invalid.");
            var head = new ContentOriginHeadFile
            {
                contentSet = manifest.contentSet,
                target = manifest.target,
                revision = manifest.revision,
                manifestSha256 = ContentDeliveryFiles.Sha256(manifestBytes),
                path = relativePath,
            };
            return new ContentOriginHeadDraft(new UTF8Encoding(false).GetBytes(JsonUtility.ToJson(head, true)), head);
        }

        public static ContentOriginHeadRead Read(byte[] bytes, string contentSet, string target)
        {
            if (bytes == null) throw new ArgumentNullException(nameof(bytes));
            if (bytes.Length > MaxBytes) return Invalid("Head exceeds " + MaxBytes + " bytes.");
            Dictionary<string, char> types;
            string json;
            try
            {
                json = new UTF8Encoding(false, true).GetString(bytes);
                // 構造（重複 key、過深、末尾の残り）は manifest と同じ読み手で拒み、型は平らな object の値の先頭の文字で見る。
                ContentManifestValidation.RequireObjectFields(json, "version");
                types = FlatValueTypes(json);
            }
            catch (Exception ex) when (ex is ContentDeliveryException or ArgumentException or FormatException)
            {
                return Invalid("Head is not a JSON object: " + ex.Message);
            }
            if (types["version"] != 'n') return Invalid("Head version must be a number.");
            ContentOriginHeadFile head;
            try { head = JsonUtility.FromJson<ContentOriginHeadFile>(json) ?? throw new FormatException(); }
            catch (Exception ex) when (ex is ArgumentException or FormatException) { return Invalid("Head cannot be parsed: " + ex.Message); }
            if (head.version == 0) return new ContentOriginHeadRead(ContentOriginHeadState.None, null, "Head version 0 has no head.");
            if (head.version < 0) return Invalid("Head version is negative.");
            if (head.version > Version) return new ContentOriginHeadRead(ContentOriginHeadState.Unsupported, null,
                "Head version " + head.version + " is not supported.");
            foreach (var name in new[] { "contentSet", "target", "revision", "manifestSha256", "path" })
                if (!types.TryGetValue(name, out var type) || type != 's') return Invalid("Head field must be a string: " + name);
            if (!ContentManifestValidation.Segment(head.contentSet) || !ContentManifestValidation.Segment(head.target)
                || !ContentManifestValidation.Segment(head.revision))
                return Invalid("Head identity is invalid.");
            if (!ContentManifestValidation.Hash(head.manifestSha256)) return Invalid("Head digest must be 64 lowercase hex characters.");
            if (!ContentManifestValidation.Relative(head.path)) return Invalid("Head path must be relative.");
            if (!string.Equals(head.contentSet, contentSet, StringComparison.Ordinal) || !string.Equals(head.target, target, StringComparison.Ordinal))
                return Invalid("Head is for " + head.contentSet + "/" + head.target + ", not " + contentSet + "/" + target + ".");
            return new ContentOriginHeadRead(ContentOriginHeadState.Present, head, string.Empty);
        }

        private static ContentOriginHeadRead Invalid(string reason) => new(ContentOriginHeadState.Invalid, null, reason);

        // 構造の検査を通った JSON の object について、最上位の key ごとの値の種類（s 文字列、n 数、o object、a 配列、b 真偽、z null）を返す。
        // JsonUtility は型の違う値を黙って既定値や別の型に読むので、型の規則は raw の字面で見る。
        private static Dictionary<string, char> FlatValueTypes(string json)
        {
            var types = new Dictionary<string, char>(StringComparer.Ordinal);
            var i = Skip(json, 0);
            if (i >= json.Length || json[i] != '{') throw new FormatException("Expected an object.");
            i = Skip(json, i + 1);
            while (i < json.Length && json[i] != '}')
            {
                var keyEnd = StringEnd(json, i);
                var key = JsonUtility.FromJson<KeyHolder>("{\"k\":" + json.Substring(i, keyEnd - i) + "}").k;
                i = Skip(json, keyEnd);
                i = Skip(json, i + 1); // ':'
                var c = json[i];
                types[key] = c == '"' ? 's' : c == '{' ? 'o' : c == '[' ? 'a' : c == 't' || c == 'f' ? 'b' : c == 'n' ? 'z' : 'n';
                i = Skip(json, ValueEnd(json, i));
                if (i < json.Length && json[i] == ',') i = Skip(json, i + 1);
            }
            return types;
        }

        [Serializable] private sealed class KeyHolder { public string k = ""; }

        private static int Skip(string json, int i)
        {
            while (i < json.Length && json[i] is ' ' or '\r' or '\n' or '\t') i++;
            return i;
        }

        private static int StringEnd(string json, int i)
        {
            for (i++; i < json.Length; i++)
            {
                if (json[i] == '\\') { i++; continue; }
                if (json[i] == '"') return i + 1;
            }
            throw new FormatException("Unterminated string.");
        }

        private static int ValueEnd(string json, int i)
        {
            if (json[i] == '"') return StringEnd(json, i);
            if (json[i] != '{' && json[i] != '[')
            {
                while (i < json.Length && json[i] is not (',' or '}' or ' ' or '\r' or '\n' or '\t')) i++;
                return i;
            }
            var depth = 0;
            for (; i < json.Length; i++)
            {
                if (json[i] == '"') { i = StringEnd(json, i) - 1; continue; }
                if (json[i] is '{' or '[') depth++;
                else if (json[i] is '}' or ']' && --depth == 0) return i + 1;
            }
            throw new FormatException("Unterminated value.");
        }
    }
}
