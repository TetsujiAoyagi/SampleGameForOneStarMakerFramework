#nullable enable

using System;

namespace OneStarMaker.Runtime.ContentResolution
{
    /// <summary>
    /// Play 前に固定する、1 候補の互換の基準。ホストが作り、<see cref="ContentCompatibilityRule"/> が保持する不変の値。
    /// 比較は持たない（比べ方は規則が決める）。
    /// </summary>
    /// <remarks>
    /// 汎用の既定値を持たない。既定値を置くと、この層が特定の engine・target を知ることになるためである。
    /// 値の出所（Player に焼いた値、Editor の build target、配布の定数等）はホストか後続の adapter が決める。
    /// revision は任意で、null は「固定しない」（互換な候補ならどの revision でも採ってよい）を意味する。
    /// </remarks>
    public sealed class ContentCompatibilityEnvelope
    {
        // 事実の正規表記（9 桁以内）と同じ範囲に制限し、基準と事実の表記の範囲を食い違わせない。
        private const int MinRootSchemaVersion = 1;
        private const int MaxRootSchemaVersion = 999999999;

        /// <param name="contentSet">必須。空・空白だけは拒む。</param>
        /// <param name="target">必須。空・空白だけは拒む。</param>
        /// <param name="engineVersion">必須。空・空白だけは拒む。</param>
        /// <param name="rootSchemaVersion">1〜999,999,999。</param>
        /// <param name="revision">任意。null は「固定しない」。空・空白だけは拒む（「固定しない」と取り違えさせないため）。</param>
        public ContentCompatibilityEnvelope(
            string contentSet,
            string target,
            string engineVersion,
            int rootSchemaVersion,
            string? revision = null)
        {
            if (string.IsNullOrWhiteSpace(contentSet))
            {
                throw new ArgumentException("Content set is required.", nameof(contentSet));
            }

            if (string.IsNullOrWhiteSpace(target))
            {
                throw new ArgumentException("Target is required.", nameof(target));
            }

            if (string.IsNullOrWhiteSpace(engineVersion))
            {
                throw new ArgumentException("Engine version is required.", nameof(engineVersion));
            }

            if (rootSchemaVersion < MinRootSchemaVersion || rootSchemaVersion > MaxRootSchemaVersion)
            {
                throw new ArgumentOutOfRangeException(nameof(rootSchemaVersion), rootSchemaVersion, "Root schema version must be between 1 and 999999999.");
            }

            if (revision != null && string.IsNullOrWhiteSpace(revision))
            {
                throw new ArgumentException("Pass null instead of an empty revision when the revision is not fixed.", nameof(revision));
            }

            ContentSet = contentSet;
            Target = target;
            EngineVersion = engineVersion;
            RootSchemaVersion = rootSchemaVersion;
            Revision = revision;
        }

        public string ContentSet { get; }

        public string Target { get; }

        public string EngineVersion { get; }

        public int RootSchemaVersion { get; }

        /// <summary>固定した revision。固定しないなら null。</summary>
        public string? Revision { get; }
    }
}
