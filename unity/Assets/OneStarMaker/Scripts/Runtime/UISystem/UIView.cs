#nullable enable

using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace OneStarMaker.Runtime.UISystem
{
    /// <summary>
    /// UI ビューの基底クラス。
    /// 1シーン = 0 or 1 UIView の原則に従う。
    /// </summary>
    public abstract class UIView : MonoBehaviour
    {
        /// <summary>
        /// UI のレイヤー。SiblingIndex の並び順として使用される。
        /// 値が大きいほど前面に描画される。
        /// Modal 以上のレイヤーでは背面 Blocker が自動生成される。
        /// </summary>
        public enum UILayer
        {
            /// <summary>背景 UI（スカイボックス、背景エフェクト等）。</summary>
            Background = 0,
            /// <summary>通常 UI（HUD、メニュー等）。</summary>
            Normal = 1,
            /// <summary>モーダル UI（背面入力ブロック付き。例: 設定画面、インベントリ全画面）。</summary>
            Modal = 2,
            /// <summary>ダイアログ UI（確認・選択ポップアップ。例: 「本当に削除しますか？」）。</summary>
            Dialog = 3,
            /// <summary>ローディング UI（最前面、全入力ブロック）。</summary>
            Loading = 4,
            /// <summary>デバッグ UI（FPS、メモリ、デバッグメニュー等）。全レイヤーの最前面。Blocker なし。</summary>
            Debug = 5,
        }

        /// <summary>入場アニメーション。</summary>
        public virtual UniTask ViewIn(CancellationToken ct) => UniTask.CompletedTask;

        /// <summary>退場アニメーション。キャンセル不可。</summary>
        public virtual UniTask ViewOut() => UniTask.CompletedTask;

        /// <summary>この UIView のレイヤーを返す。</summary>
        public virtual UILayer GetUILayer() => UILayer.Normal;
    }
}
