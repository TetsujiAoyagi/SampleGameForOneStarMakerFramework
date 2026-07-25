using Cysharp.Threading.Tasks;
using OneStarMaker.Foundation.Telemetry;
using System;
using System.Collections.Generic;
using System.Threading;

namespace OneStarMaker.Runtime.SceneSystem
{
    public interface ISceneController
    {
        /// <summary>
        /// シーンを追加する。必要に応じて親シーンも再帰的にロードする。
        /// キャンセルは SceneBase PreLoad フェーズのみ有効（キャンセル窓）。
        /// Unity Scene ロード開始以降はキャンセル不可（ポイント・オブ・ノーリターン）。
        /// </summary>
        /// <param name="sceneIdentify">シーンの一意識別子。</param>
        /// <param name="afterOnLoadedTask">ロード後に追加実行するタスク。</param>
        /// <param name="ct">キャンセルトークン（PreLoad フェーズのみ有効）。</param>
        /// <param name="context">遷移先シーンに渡す型付きコンテキスト（Shared Context）。</param>
        /// <param name="progress">進捗通知。IsCancelable で窓の状態を確認可能。</param>
        /// <param name="loadingDisplay">ローディング表示モード。</param>
        /// <param name="telemetryTags">テレメトリスパンに付与する追加タグ。</param>
        /// <param name="priority">Unity Scene ロードの優先度。同一 identity で in-flight に合流した後発呼び出しの priority は無視される（I-5 と同じ意味論）。</param>
        /// <param name="telemetryLevel">AddScene スパンのテレメトリ出力レベル。</param>
        UniTask AddScene(
            string sceneIdentify,
            Func<UniTask>? afterOnLoadedTask,
            CancellationToken ct,
            SceneContext? context = null,
            IProgress<SceneLoadProgress>? progress = null,
            LoadingDisplayType loadingDisplay = LoadingDisplayType.None,
            IReadOnlyDictionary<string, string>? telemetryTags = null,
            int priority = 100,
            TelemetryLevel telemetryLevel = TelemetryLevel.Summary);

        /// <summary>
        /// シーンをアンロードする。子シーンも再帰的にアンロードされる。
        /// アンロード処理はキャンセル不可。開始したら必ず最終状態まで到達する。
        /// ロード中のシーンに対しては、キャンセル窓内ならキャンセル、窓外なら Stable 到達を待ってからアンロードする。
        /// </summary>
        /// <param name="sceneIdentify">シーンの一意識別子。</param>
        /// <param name="loadingDisplay">ローディング表示モード。</param>
        /// <param name="telemetryTags">テレメトリスパンに付与する追加タグ。</param>
        /// <param name="telemetryLevel">UnloadScene スパンのテレメトリ出力レベル。</param>
        UniTask UnloadScene(
            string sceneIdentify,
            LoadingDisplayType loadingDisplay = LoadingDisplayType.None,
            IReadOnlyDictionary<string, string>? telemetryTags = null,
            TelemetryLevel telemetryLevel = TelemetryLevel.Summary);

        /// <summary>
        /// 兄弟シーンを切り替える。
        /// ローディング表示 → 旧シーンアンロード → 新シーンロード → ローディング終了
        /// をアトミックに行い、遷移中のちらつきを防ぐ。
        /// 遷移履歴をスタックに記録し、<see cref="GoBack"/> による巻き戻しを可能にする。
        ///
        /// キャンセルポリシー:
        ///   ct が効くのは _loadingDisplay.Show() のみ（フェードイン中のキャンセル）。
        ///   Show 完了後は PoNR（ポイント・オブ・ノーリターン）。
        ///   以降の Unload / AddScene / Hide は全て CancellationToken.None で実行し、
        ///   旧シーンが消えたまま宙ぶらりんになる状態を原理的に排除する。
        ///
        /// Unload → Add の順序は意図的:
        ///   旧シーンのメモリを先に解放してから新シーンをロードすることで、
        ///   両シーンが同時に存在するメモリピークを回避する。
        ///   PoNR 通過後のため、キャンセルによる画面消失リスクはない。
        /// </summary>
        /// <param name="fromSceneIdentify">アンロードするシーンの ID。null なら Unload なし（Add のみ）。</param>
        /// <param name="toSceneIdentify">ロードするシーンの ID。</param>
        /// <param name="ct">キャンセルトークン（Show フェーズのみ有効）。</param>
        /// <param name="context">遷移先シーンに渡す型付きコンテキスト。</param>
        /// <param name="loadingDisplay">ローディング表示モード。</param>
        /// <param name="telemetryTags">テレメトリスパンに付与する追加タグ。</param>
        UniTask SwitchScene(
               string? fromSceneIdentify,
               string toSceneIdentify,
               CancellationToken ct,
               SceneContext? context = null,
               LoadingDisplayType loadingDisplay = LoadingDisplayType.BlackScreen,
               IReadOnlyDictionary<string, string>? telemetryTags = null);


        /// <summary>
        /// ひとつ前のシーンに戻る。
        /// <see cref="SwitchScene"/> で記録された遷移履歴をスタックから取り出し、
        /// 逆方向の遷移を行う。
        ///
        /// キャンセルポリシーは SwitchScene と同一:
        ///   ct が効くのは Show のフェードイン中のみ。
        ///   キャンセルされた場合、履歴はそのまま残る（何も変化しない）。
        /// </summary>
        /// <param name="ct">キャンセルトークン（Show フェーズのみ有効）。</param>
        /// <param name="context">戻り先シーンに渡す型付きコンテキスト。</param>
        /// <param name="loadingDisplay">ローディング表示モード。</param>
        /// <param name="telemetryTags">テレメトリスパンに付与する追加タグ。</param>
        /// <exception cref="InvalidOperationException">履歴が空の場合。</exception>
        UniTask GoBack(
            CancellationToken ct,
            SceneContext? context = null,
            LoadingDisplayType loadingDisplay = LoadingDisplayType.BlackScreen,
            IReadOnlyDictionary<string, string>? telemetryTags = null);


        /// <summary>
        /// シーン遷移履歴をクリアする。
        /// タイトル画面への復帰など、履歴をリセットしたい場合に使用する。
        /// </summary>
        void ClearHistory();
    }
}
