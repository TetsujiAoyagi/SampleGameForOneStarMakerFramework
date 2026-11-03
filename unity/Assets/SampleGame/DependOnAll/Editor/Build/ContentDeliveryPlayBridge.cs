#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OneStarMaker.Runtime.BuildContent.Distribution;
using UnityEditor;
using UnityEngine;

namespace SampleGame.DependOnAll.Editor.Build
{
    /// <summary>
    /// 次の Editor Play の config を process 環境変数に置き、置く前の値へ戻す ledger。Delivery 窓・Play As・検証 Play が共有する。
    /// <para>
    /// ledger は 1 つで、戻し方の印を持つ。Delivery 窓の Use For Next Play は「手で戻す」（窓の Reset まで残る）。
    /// Play As と検証 Play は「1 回の Play で戻す」で、Play を抜けて Edit mode に戻った時（<c>EnteredEditMode</c>）か、
    /// Play に至らずに Edit mode に留まったと分かった時（要求が受理されなかった、移行が中断した）に戻す。
    /// <c>ExitingEditMode</c> は移行の開始で Play への到達を意味しないので、戻す契機にしない。
    /// </para>
    /// <para>
    /// 汎用の transaction や状態機械にはしない。Play 1 回分の寿命だけを、playModeStateChanged・update・domain reload 後の
    /// 初期化の 3 つの契機で見る（project は Play に入る時の domain reload を切っているが、Play 中の再 compile では起きる）。
    /// </para>
    /// </summary>
    [InitializeOnLoad]
    internal static class ContentDeliveryPlayBridge
    {
        // 戻し方の欄を足しても key は v1 のまま。欄の無い v1 の ledger は「手で戻す」として読む（JsonUtility の既定値 false）。
        private const string LedgerKey = "OSM.ContentDelivery.PlayBridge.v1";
        private const string Target = "StandaloneWindows64-Player";
        private const string NullMarker = "<null>";
        /// <summary>directory の verified pair の config key。環境変数名は <see cref="PurposeLaunchSettings.EnvironmentName"/>（例 SAMPLEGAME_CONTENT__RUNTIMEMODE）。</summary>
        internal static readonly IReadOnlyList<string> ContentConfigKeys = new[]
        {
            "content:runtimeMode", "content:installedRevisionPath", "content:manifestSha256", "content:buildIdentity", "content:representation"
        };

        static ContentDeliveryPlayBridge()
        {
            EditorApplication.playModeStateChanged += HandlePlayModeStateChanged;
            EditorApplication.update += OnUpdate;
            // domain reload の後。Play への移行中・Play 中なら戻さない（HandleIdle が 2 値で判定する）。
            HandleIdle(EditorApplication.isPlaying, EditorApplication.isPlayingOrWillChangePlaymode);
        }

        /// <summary>Delivery 窓の手入力経路（Use For Next Play）。値と検証は従来の 5 key のまま、ledger は「手で戻す」。</summary>
        internal static void Apply(string root, string digest, string identity, string contentSet, string representation)
        {
            // ExitingPlayMode の間は isPlaying だけが真。残った「1 回の Play で戻す」ledger を先に戻すのは Play 中でも移行中でもない時だけ。
            if (EditorApplication.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Content delivery settings cannot change during Play.");

            // Use For Next Play だけが次の Editor Play の verified pair になる。
            // 未適用のままでは runtimeMode 未指定の fail-closed になり、known-good は読まない。
            var revisionRoot = Path.GetFullPath(root);
            var revisionDirectory = new DirectoryInfo(revisionRoot);
            var setDirectory = revisionDirectory.Parent;
            var installedDirectory = setDirectory?.Parent;
            var cacheDirectory = installedDirectory?.Parent;
            if (setDirectory == null || installedDirectory == null || cacheDirectory == null ||
                !string.Equals(revisionDirectory.Name, identity, StringComparison.Ordinal) ||
                !string.Equals(setDirectory.Name, contentSet, StringComparison.Ordinal) ||
                !string.Equals(installedDirectory.Name, "installed", StringComparison.OrdinalIgnoreCase))
                throw new ContentDeliveryException(ContentDeliveryFailureCode.InvalidManifest,
                    "Prepared revision root is outside the managed cache layout.");

            var validated = new ContentCacheStore(cacheDirectory.FullName).ValidateInstalled(
                revisionRoot, digest, contentSet, identity, Target);
            ApplyValues(ContentValues(validated.RevisionRoot, validated.ManifestSha256, validated.Revision, representation),
                resetAfterOnePlay: false);
        }

        /// <summary>Play As・検証 Play の経路。置いた値は 1 回の Play（または Play に至らなかったこと）で戻る。</summary>
        internal static void ApplyForOnePlay(IReadOnlyList<KeyValuePair<string, string>> environment)
        {
            if (EditorApplication.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Play settings cannot change during Play.");
            ApplyValues(environment, resetAfterOnePlay: true);
        }

        /// <summary>directory の verified pair の 5 key。Delivery 窓と検証 Play の両方がこの値を置く。</summary>
        internal static IReadOnlyList<KeyValuePair<string, string>> ContentValues(
            string revisionRoot, string manifestSha256, string identity, string representation)
        {
            var values = new[] { "directory", revisionRoot, manifestSha256, identity, representation };
            return ContentConfigKeys.Select((key, i) =>
                new KeyValuePair<string, string>(PurposeLaunchSettings.EnvironmentName(key), values[i])).ToArray();
        }

        /// <summary>
        /// 置こうとする環境変数のうち、この ledger 以外の値で既に process 環境にあるもの。
        /// ledger が置いたままの値は自分の値として数えない（「1 回の Play で戻す」ledger は Apply の前に戻すため）。
        /// </summary>
        internal static IReadOnlyList<string> FindForeignValues(IEnumerable<string> environmentNames)
        {
            var ledger = Load();
            var foreign = new List<string>();
            foreach (var name in environmentNames)
            {
                var current = Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.Process);
                if (current == null) continue;
                var index = IndexOf(ledger, name);
                if (index >= 0 && ledger.installed[index] == current) continue;
                foreign.Add(name);
            }
            return foreign;
        }

        /// <summary>Delivery 窓の「手で戻す」ledger がある。別の経路はこれを黙って消さない。</summary>
        internal static bool HasManualLedger
        {
            get { var ledger = Load(); return ledger.keys.Length != 0 && !ledger.resetAfterOnePlay; }
        }

        internal static bool HasOnePlayLedger
        {
            get { var ledger = Load(); return ledger.keys.Length != 0 && ledger.resetAfterOnePlay; }
        }

        internal static void Reset()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Content delivery settings cannot change during Play.");
            ResetCore();
        }

        /// <summary>Play を抜けて Edit mode に戻ったら「1 回の Play で戻す」ledger を戻す。「手で戻す」ledger は触らない。</summary>
        internal static void HandlePlayModeStateChanged(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.EnteredEditMode && HasOnePlayLedger) ResetCore();
        }

        /// <summary>
        /// update と domain reload 後の初期化。「1 回の Play で戻す」ledger が残っていて、Play 中でも移行中でもなければ
        /// Play に至らなかった（要求が受理されなかった、移行が中断した）ので戻す。<c>ExitingPlayMode</c> の間は
        /// isPlaying が真のまま isPlayingOrWillChangePlaymode が偽になるので、2 値の両方が偽の時だけ戻し、
        /// Play を抜けた戻しは <see cref="HandlePlayModeStateChanged"/> に任せる。
        /// </summary>
        internal static void HandleIdle(bool isPlaying, bool isPlayingOrWillChangePlaymode)
        {
            if (isPlaying || isPlayingOrWillChangePlaymode) return;
            if (HasOnePlayLedger) ResetCore();
        }

        private static void OnUpdate()
        {
            // ledger が無い間は SessionState の文字列の長さだけを見る。
            if (SessionState.GetString(LedgerKey, string.Empty).Length == 0) return;
            HandleIdle(EditorApplication.isPlaying, EditorApplication.isPlayingOrWillChangePlaymode);
        }

        private static void ApplyValues(IReadOnlyList<KeyValuePair<string, string>> environment, bool resetAfterOnePlay)
        {
            var ledger = Load();
            if (ledger.keys.Length != 0)
            {
                if (ledger.resetAfterOnePlay)
                {
                    // 前の「1 回の Play で戻す」値と混ぜない。Play に入らなかった残りもここで戻す。
                    ResetCore();
                    ledger = new Ledger();
                }
                else if (resetAfterOnePlay)
                    throw new InvalidOperationException(
                        "Content Delivery settings from 'Use For Next Play' are active. Reset them in Tools/OSM/Content/Delivery first.");
                // 手の ledger どうしは従来どおり重ね、最初に置いた時の以前の値を残す。
            }
            ledger.resetAfterOnePlay = resetAfterOnePlay;
            try
            {
                foreach (var pair in environment)
                {
                    var index = IndexOf(ledger, pair.Key);
                    if (index < 0)
                    {
                        Array.Resize(ref ledger.keys, ledger.keys.Length + 1);
                        Array.Resize(ref ledger.previous, ledger.previous.Length + 1);
                        Array.Resize(ref ledger.installed, ledger.installed.Length + 1);
                        index = ledger.keys.Length - 1;
                        ledger.keys[index] = pair.Key;
                        ledger.previous[index] = Environment.GetEnvironmentVariable(pair.Key, EnvironmentVariableTarget.Process) ?? NullMarker;
                    }
                    // User/Machine へ書くと次の Editor/Player が暗黙の directory mode になる。
                    Environment.SetEnvironmentVariable(pair.Key, pair.Value, EnvironmentVariableTarget.Process);
                    ledger.installed[index] = pair.Value;
                }
            }
            finally
            {
                // 途中で失敗しても、置いた key を ledger に残す（呼び手の Reset が戻せるように）。
                SessionState.SetString(LedgerKey, JsonUtility.ToJson(ledger));
            }
        }

        private static void ResetCore()
        {
            var ledger = Load();
            for (var i = 0; i < ledger.keys.Length; i++)
            {
                // 自分が適用した値を他者が変更した場合は、その新しい値を巻き戻さない。
                if (Environment.GetEnvironmentVariable(ledger.keys[i], EnvironmentVariableTarget.Process) == ledger.installed[i])
                    Environment.SetEnvironmentVariable(ledger.keys[i],
                        ledger.previous[i] == NullMarker ? null : ledger.previous[i],
                        EnvironmentVariableTarget.Process);
            }
            SessionState.EraseString(LedgerKey);
        }

        private static int IndexOf(Ledger ledger, string key)
        {
            // Windows の環境変数名は大文字小文字を区別しない。
            for (var i = 0; i < ledger.keys.Length; i++)
                if (string.Equals(ledger.keys[i], key, StringComparison.OrdinalIgnoreCase)) return i;
            return -1;
        }

        private static Ledger Load()
        {
            var json = SessionState.GetString(LedgerKey, string.Empty);
            return json.Length == 0
                ? new Ledger()
                : JsonUtility.FromJson<Ledger>(json) ?? new Ledger();
        }

        [Serializable]
        private sealed class Ledger
        {
            public string[] keys = Array.Empty<string>();
            public string[] previous = Array.Empty<string>();
            public string[] installed = Array.Empty<string>();
            public bool resetAfterOnePlay;
        }
    }
}
