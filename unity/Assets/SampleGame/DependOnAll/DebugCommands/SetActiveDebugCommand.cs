#nullable enable

using System;
using OneStarMaker.Runtime.DebugCommands;
using OneStarMaker.Runtime.DebugSocketServices;
using UnityEngine;

namespace SampleGame.DependOnAll.DebugCommands
{
    /// <summary>今の hierarchy で NodeId が指す GameObject を引く。<see cref="DebugSocketService.TryGetHierarchyGameObject"/> の形。</summary>
    internal delegate bool HierarchyGameObjectResolver(long nodeId, out GameObject? gameObject);

    /// <summary>
    /// DebugStudio の既存 <c>send</c> から、hierarchy にある既存の GameObject を NodeId で 1 つ選び、
    /// その activeSelf を指定した値にするデバッグコマンド（Editor の Play 専用）。
    /// </summary>
    /// <remarks>
    /// <para>
    /// payload は <c>{"nodeId":&lt;正の整数&gt;,"active":true|false}</c> の 2 つだけで、どちらも省略不可。反転ではなく値の指定なので、
    /// 結果が分からなかった後に同じ要求を送り直しても逆に戻らない。対象を名前・タグ・印・パス・許可リストで絞らない。
    /// 結果に GameObject の名前を返すのは、利用者が変えた GameObject を出力だけで確かめられるようにするため
    /// （前の Play の NodeId は次の Play で同じ番号を持つ別の GameObject に当たりうる。Play をまたぐ識別はしない）。
    /// </para>
    /// <para>
    /// 拒否は SetActive より前に決め、message を <c>rejected: </c> で始める（利用者はこれを「何も変えていない」と読む）。
    /// それ以外の失敗（handler の例外は socket の router が payload の無い失敗にする）は結果不明として読ませる。
    /// handler が直接 SetActive を呼ぶのは解決した 1 つだけだが、SetActive が呼ぶ OnEnable / OnDisable などの
    /// ゲーム側のコードが他の GameObject や対象自身を変えることはあり、それは保証しない。
    /// handler は socket の main thread dispatcher から呼ばれる前提で Unity API を触る。
    /// </para>
    /// </remarks>
    internal static class SetActiveDebugCommand
    {
        internal const string CommandName = "debug.set-active";
        internal const string AppliedMessage = "applied";
        internal const string RejectedInvalidPayload = "rejected: invalid-payload";
        internal const string RejectedNodeNotFound = "rejected: node-not-found";

        /// <summary>
        /// Editor のときだけ catalog に <see cref="CommandName"/> を登録する。
        /// </summary>
        /// <param name="isEditor"><see cref="Application.isEditor"/> を渡す。false なら登録しない（Player にコマンドを出さない）。</param>
        /// <param name="getDebugSocket">実行時に DebugSocketService を読む。catalog は service より先に作られるので、登録時には読まない。</param>
        /// <returns>登録したときだけ true。</returns>
        internal static bool Register(
            DebugCommandCatalog catalog,
            bool isEditor,
            Func<DebugSocketService?> getDebugSocket)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            if (getDebugSocket == null) throw new ArgumentNullException(nameof(getDebugSocket));

            if (!isEditor)
            {
                return false;
            }

            catalog.Register(CommandName, payloadJson =>
            {
                var debugSocket = getDebugSocket();
                if (debugSocket == null)
                {
                    return Execute(payloadJson, null, null);
                }

                return Execute(payloadJson, debugSocket.TryGetHierarchyGameObject, debugSocket.NotifyHierarchyChanged);
            });
            return true;
        }

        /// <summary>
        /// 1 要求を処理する。解決を差し替えられるよう、DebugSocketService ではなく delegate を受ける。
        /// </summary>
        /// <param name="resolveGameObject">null は DebugSocket が無いことを表し、node-not-found。</param>
        /// <param name="notifyHierarchyChanged">適用後に hierarchy の再送を要求する。null なら何もしない。</param>
        internal static DebugCommandResult Execute(
            string? payloadJson,
            HierarchyGameObjectResolver? resolveGameObject,
            Action? notifyHierarchyChanged)
        {
            // (1) 解析。拒否はすべて SetActive より前に決める。解析に失敗した要求では解決も呼ばない。
            if (!TryParse(payloadJson, out var nodeId, out var active))
            {
                return DebugCommandResult.Fail(RejectedInvalidPayload);
            }

            // (2) 今の hierarchy で NodeId が指す GameObject を引く。破棄済みは偽 null なので == で判定する。
            if (resolveGameObject == null ||
                !resolveGameObject(nodeId, out var gameObject) ||
                gameObject == null)
            {
                return DebugCommandResult.Fail(RejectedNodeNotFound);
            }

            // (3) 適用。
            return Apply(nodeId, gameObject, active, notifyHierarchyChanged);
        }

        /// <summary>
        /// payload を解析する。GameObject に触れないので、拒否判定は Unity の scene なしで試せる。
        /// </summary>
        /// <remarks>
        /// <c>JsonUtility.FromJsonOverwrite</c> は JSON に無い field（型が合わず読めない field を含む）を初期値のまま残す。
        /// そこで <c>active</c> の初期値が false の DTO と true の DTO へ同じ payload を読み、2 つが食い違えば
        /// <c>active</c> が無いと判定する。欠落を false と読むと、利用者が値を書き忘れた要求で GameObject を非アクティブにしてしまうため。
        /// これは欠落の検出であって bool 以外の値の拒否ではない。<c>JsonUtility</c> は文字列や null を bool に変換することがあり、
        /// 実測では <c>"false"</c> と <c>null</c> が false として適用された。適用された値は結果の activeSelf で読ませる。
        /// 厳密な JSON パーサや schema の規約はここでは足さない。nodeId は採番が 1 から始まるので、無い場合の 0 を 1 未満として弾く。
        /// </remarks>
        internal static bool TryParse(string? payloadJson, out long nodeId, out bool active)
        {
            nodeId = 0;
            active = false;
            if (string.IsNullOrWhiteSpace(payloadJson))
            {
                return false;
            }

            var withFalseDefault = new SetActivePayload { active = false };
            var withTrueDefault = new SetActivePayload { active = true };
            try
            {
                JsonUtility.FromJsonOverwrite(payloadJson, withFalseDefault);
                JsonUtility.FromJsonOverwrite(payloadJson, withTrueDefault);
            }
            catch (Exception)
            {
                // JsonUtility は解析できない JSON で ArgumentException 等を投げる。理由を問わず invalid-payload にする。
                return false;
            }

            if (withFalseDefault.active != withTrueDefault.active || withFalseDefault.nodeId < 1)
            {
                return false;
            }

            nodeId = withFalseDefault.nodeId;
            active = withFalseDefault.active;
            return true;
        }

        private static DebugCommandResult Apply(
            long nodeId,
            GameObject gameObject,
            bool active,
            Action? notifyHierarchyChanged)
        {
            gameObject.SetActive(active);

            // hierarchy の flags（ActiveSelf / ActiveInHierarchy）を DebugStudio 側で読み直せるよう再送を要求する。
            notifyHierarchyChanged?.Invoke();

            return DebugCommandResult.Ok(AppliedMessage, CreateResultPayload(nodeId, gameObject));
        }

        /// <summary>
        /// 結果の payload <c>{"nodeId":N,"name":"...","activeSelf":bool}</c>。activeSelf は SetActive の後に読んだ値。
        /// 名前のエスケープを手で書かないよう、DTO を <c>JsonUtility.ToJson</c> で直列化する。
        /// </summary>
        internal static string CreateResultPayload(long nodeId, GameObject gameObject)
        {
            return JsonUtility.ToJson(new SetActiveResultPayload
            {
                nodeId = nodeId,
                name = gameObject.name,
                activeSelf = gameObject.activeSelf,
            });
        }

        // JsonUtility が field 名で読み書きするため、field 名は JSON の key と同じ小文字にする。
        [Serializable]
        private sealed class SetActivePayload
        {
            public long nodeId;
            public bool active;
        }

        // field の宣言順が JSON の key の順になる。
        [Serializable]
        private sealed class SetActiveResultPayload
        {
            public long nodeId;
            public string name = string.Empty;
            public bool activeSelf;
        }
    }
}
