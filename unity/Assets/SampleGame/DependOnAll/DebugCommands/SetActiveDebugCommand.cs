#nullable enable

using System;
using System.Globalization;
using OneStarMaker.Runtime.DebugCommands;
using OneStarMaker.Runtime.DebugSocketServices;
using UnityEngine;

namespace SampleGame.DependOnAll.DebugCommands
{
    /// <summary>GameObject に現在割り当てられている hierarchy の NodeId を引く。<see cref="DebugSocketService.TryGetHierarchyNodeId"/> の形。</summary>
    internal delegate bool HierarchyNodeIdResolver(GameObject gameObject, out long nodeId);

    /// <summary>
    /// DebugStudio の既存 <c>send</c> から、SampleGame の検証用 GameObject の activeSelf を指定値にするデバッグコマンド。
    /// </summary>
    /// <remarks>
    /// <para>
    /// payload は <c>{"nodeId":&lt;正の整数&gt;,"active":true|false}</c> の 2 つだけで、どちらも省略不可。反転ではなく値の指定なので、
    /// 結果が分からなかった後に同じ要求を送り直しても逆に戻らない。
    /// </para>
    /// <para>
    /// 書き換えてよいのは検証用 GameObject だけ。照合は「検証用 GameObject の現在の NodeId」と payload の nodeId の一致だけで、
    /// 名前・印・パスは使わず、NodeId から GameObject を引くこともしない。したがってどの NodeId を送っても、書き換わりうるのは
    /// 検証用 GameObject に限られる。前の Play の NodeId が次の Play の検証用 GameObject の NodeId と一致した場合は適用されるが、
    /// これは許容した既知の制約で、Play をまたぐ識別は後続の作業が持つ。
    /// </para>
    /// <para>
    /// 拒否は SetActive より前に決め、message を <c>rejected: </c> で始める（利用者はこれを「変更していない」と読む）。
    /// それ以外の失敗（handler の例外は socket router が payload の無い失敗にする）は結果不明として読ませる。
    /// handler は socket の main thread dispatcher から呼ばれる前提で Unity API を触る。
    /// </para>
    /// </remarks>
    internal static class SetActiveDebugCommand
    {
        internal const string CommandName = "debug.set-active";
        internal const string AppliedMessage = "applied";
        internal const string RejectedInvalidPayload = "rejected: invalid-payload";
        internal const string RejectedTargetUnavailable = "rejected: target-unavailable";
        internal const string RejectedNotVerificationTarget = "rejected: not-verification-target";

        /// <summary>
        /// Editor のときだけ catalog に <see cref="CommandName"/> を登録する。
        /// </summary>
        /// <param name="isEditor"><see cref="Application.isEditor"/> を渡す。false なら登録しない（Player にコマンドを出さない）。</param>
        /// <param name="getDebugSocket">実行時に DebugSocketService を読む。catalog は service より先に作られるので、登録時には読まない。</param>
        /// <param name="getTarget">実行時に検証用 GameObject を読む（Play ごとに作り直されるので保持しない）。</param>
        /// <returns>登録したときだけ true。</returns>
        internal static bool Register(
            DebugCommandCatalog catalog,
            bool isEditor,
            Func<DebugSocketService?> getDebugSocket,
            Func<GameObject?> getTarget)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            if (getDebugSocket == null) throw new ArgumentNullException(nameof(getDebugSocket));
            if (getTarget == null) throw new ArgumentNullException(nameof(getTarget));

            if (!isEditor)
            {
                return false;
            }

            catalog.Register(CommandName, payloadJson =>
            {
                HierarchyNodeIdResolver? resolveNodeId = null;
                Action? notifyHierarchyChanged = null;
                var debugSocket = getDebugSocket();
                if (debugSocket != null)
                {
                    resolveNodeId = debugSocket.TryGetHierarchyNodeId;
                    notifyHierarchyChanged = debugSocket.NotifyHierarchyChanged;
                }

                return Execute(payloadJson, getTarget(), resolveNodeId, notifyHierarchyChanged);
            });
            return true;
        }

        /// <summary>
        /// 1 要求を処理する。解決を差し替えられるよう、DebugSocketService ではなく delegate を受ける。
        /// </summary>
        /// <param name="target">検証用 GameObject。無い・破棄済みなら target-unavailable。</param>
        /// <param name="resolveNodeId">null は DebugSocket が無いことを表し、target-unavailable。</param>
        /// <param name="notifyHierarchyChanged">適用後に hierarchy の再送を要求する。null なら何もしない。</param>
        internal static DebugCommandResult Execute(
            string? payloadJson,
            GameObject? target,
            HierarchyNodeIdResolver? resolveNodeId,
            Action? notifyHierarchyChanged)
        {
            // (1) 解析。拒否はすべて SetActive より前に決める。
            if (!TryParse(payloadJson, out var requestedNodeId, out var active))
            {
                return DebugCommandResult.Fail(RejectedInvalidPayload);
            }

            // (2) 検証用 GameObject の現在の NodeId を得る。破棄済みは偽 null なので == で判定する。
            if (target == null || resolveNodeId == null || !resolveNodeId(target, out var currentNodeId))
            {
                return DebugCommandResult.Fail(RejectedTargetUnavailable);
            }

            // (3) 照合は NodeId の一致だけ。一致しない NodeId が指す GameObject は解決しない。
            if (currentNodeId != requestedNodeId)
            {
                return DebugCommandResult.Fail(RejectedNotVerificationTarget);
            }

            // (4) 適用。hierarchy の flags（activeSelf）も DebugStudio 側で読み直せるよう再送を要求する。
            target.SetActive(active);
            notifyHierarchyChanged?.Invoke();

            var payload = "{\"nodeId\":" + currentNodeId.ToString(CultureInfo.InvariantCulture) +
                          ",\"activeSelf\":" + (target.activeSelf ? "true" : "false") + "}";
            return DebugCommandResult.Ok(AppliedMessage, payload);
        }

        /// <summary>
        /// <c>JsonUtility.FromJsonOverwrite</c> は JSON に無い field（型が合わず読めない field を含む）を初期値のまま残す。
        /// そこで <c>active</c> の初期値が false の DTO と true の DTO へ同じ payload を読み、2 つが食い違えば
        /// <c>active</c> が無い（bool として読めない）と判定する。欠落を false と読むと、利用者が値を書き忘れた要求で
        /// 検証用 GameObject を非アクティブにしてしまうため。厳密な JSON パーサや schema の規約はここでは足さない。
        /// nodeId は採番が 1 から始まるので、無い場合の 0 を 1 未満として弾く。
        /// </summary>
        private static bool TryParse(string? payloadJson, out long nodeId, out bool active)
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

        // JsonUtility が field 名で読むため、field 名は payload の key と同じ小文字にする。
        [Serializable]
        private sealed class SetActivePayload
        {
            public long nodeId;
            public bool active;
        }
    }
}
