#nullable enable

using System;
using Cysharp.Threading.Tasks;
using OneStarMaker.Runtime.AssetDescriptions;
using OneStarMaker.Runtime.SceneSystem;
using ZLogger;

namespace SampleGame.InGame.Streaming
{
    /// <summary>major Event の要求の結果。</summary>
    internal enum MajorEventRequestResult
    {
        /// <summary>Add を 1 回発行した。</summary>
        Accepted,

        /// <summary>同じ identity の Event がロード中か生存中。Add しない。</summary>
        AlreadyActive,

        /// <summary>受理しない。Add しない。理由は <see cref="MajorEventRejectReason"/>。</summary>
        Rejected,
    }

    /// <summary>
    /// 拒否の理由。この集合は受け入れ条件で凍結している。増やすときは設計判断として計画へ戻す。
    /// </summary>
    internal enum MajorEventRejectReason
    {
        None,
        Stopped,
        BranchBusy,
        SeasonNotLoaded,
        SeasonReplaced,
        SeasonNotStable,
        NotSeasonChild,
        NotOnDemand,
        StreamByDistance,
        Ending,
        OtherEventActive,
    }

    /// <summary>driver の状態。Idle → Loading（受理）→ Live（Stable を観測）→ Ending（完了・失敗）→ Idle（終端を観測）。</summary>
    internal enum MajorEventDriverState
    {
        Idle,
        Loading,
        Live,
        Ending,
    }

    /// <summary>
    /// Season 枝 1 本の major Event（Season 直下の独立 Event Scene）の受理・発行・完了・終端の観測。
    /// 枝ごとに controller が作り、枝の終了と Session の停止で <see cref="Stop"/> する。Season は載せない。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 1 枝に同時に生きる Event は最大 1 件。Idle に戻るのは、Add の task の終わり（完了・例外）と、
    /// 捕捉した個体の終端（Removed / CancelCleanedUp）の両方を観測してから。どちらかだけでは戻さない
    /// （終端の後に Add の await が巻き戻る途中の再要求や、unload 中の個体と新しい個体の同時生存を作らない）。
    /// </para>
    /// <para>
    /// 受理・状態の記録・Add の開始は <see cref="Request"/> の同期区間で行い、await を挟まない。
    /// Season の unload 中・枝の切替中・停止後の要求は Add しないので、祖先の暗黙のロードも、unload 中の親への子の取り残しも起きない。
    /// 要求は物理の callback や frame の処理から届く前提で、SceneDirector のイベントの同期の購読者からは呼ばない。
    /// </para>
    /// <para>
    /// 停止の後は完了の通知でも unload を発行しない。生き残った Event は Framework の親子の unload が回収する。
    /// </para>
    /// </remarks>
    internal sealed class MajorEventDriver
    {
        private readonly SceneBase _season;
        private readonly ISceneQuery _query;
        private readonly Func<string, (SceneBase? Captured, UniTask Add)> _issueAdd;
        private readonly Func<string, UniTask> _unload;
        private readonly Action<SceneBase, Action> _attachCompletion;
        private readonly Microsoft.Extensions.Logging.ILogger _logger;
        private IDisposable? _terminalSubscription;

        private MajorEventDriverState _state = MajorEventDriverState.Idle;
        private string? _eventIdentity;
        private SceneBase? _captured;
        private int _issueId;
        private bool _addInFlight;
        private bool _terminalObserved;
        private bool _stopped;

        /// <param name="season">この枝の Season の個体（枝の確立時に捕捉したもの）。</param>
        /// <param name="issueAdd">
        /// Add を発行し、最初の await より前に捕捉して観測登録した個体（捕捉できなければ null）と、Add の task を返す。
        /// </param>
        /// <param name="unload">identity の Event を unload する。</param>
        /// <param name="attachCompletion">
        /// 捕捉した個体へ完了の delegate を付ける。捕捉の時点（Add の最初の await より前）に呼ぶ。
        /// Stable の処理は Add の await の中で走るので、await の後に付けると完了の受付が delegate より先に始まる。
        /// </param>
        internal MajorEventDriver(
            SceneBase season,
            ISceneQuery query,
            ISceneTerminalEvents terminalEvents,
            Func<string, (SceneBase? Captured, UniTask Add)> issueAdd,
            Func<string, UniTask> unload,
            Action<SceneBase, Action> attachCompletion,
            Microsoft.Extensions.Logging.ILogger logger)
        {
            _season = season ?? throw new ArgumentNullException(nameof(season));
            _query = query ?? throw new ArgumentNullException(nameof(query));
            _issueAdd = issueAdd ?? throw new ArgumentNullException(nameof(issueAdd));
            _unload = unload ?? throw new ArgumentNullException(nameof(unload));
            _attachCompletion = attachCompletion ?? throw new ArgumentNullException(nameof(attachCompletion));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            if (terminalEvents == null)
            {
                throw new ArgumentNullException(nameof(terminalEvents));
            }

            _terminalSubscription = terminalEvents.Subscribe(OnTerminal);
        }

        internal MajorEventDriverState State => _state;

        internal string? ActiveEventIdentity => _state == MajorEventDriverState.Idle ? null : _eventIdentity;

        /// <summary>
        /// major Event を要求する。受理なら Add を 1 回発行して <see cref="MajorEventRequestResult.Accepted"/> を返す。
        /// 返る前に状態は Loading で、捕捉できた個体は観測登録済み。拒否は Warning を 1 回出す。
        /// </summary>
        /// <param name="branchBusy">controller の枝の操作（確立・切替）の最中か。</param>
        internal MajorEventRequestResult Request(string eventIdentity, bool branchBusy)
        {
            var seasonIdentity = _season.SceneResource.Identity;
            var result = Admit(
                eventIdentity,
                _season,
                _query.GetLoadedScene(seasonIdentity),
                _query.IsSceneStable(seasonIdentity),
                branchBusy,
                _stopped,
                _state,
                _eventIdentity,
                out var reason);
            switch (result)
            {
                case MajorEventRequestResult.Rejected:
                    _logger.ZLogWarning($"Major event request rejected: '{eventIdentity}' ({reason})");
                    return result;
                case MajorEventRequestResult.AlreadyActive:
                    return result;
                default:
                    Issue(eventIdentity);
                    return result;
            }
        }

        /// <summary>
        /// 以後の要求を拒否し、完了の通知と終端の観測をやめる。Event の回収は呼び出し側の Season の unload
        /// （枝の切替では発行済み操作と観測個体の待ち、Session の停止では Framework の親子の unload）が担う。
        /// </summary>
        internal void Stop()
        {
            if (_stopped)
            {
                return;
            }

            _stopped = true;
            _terminalSubscription?.Dispose();
            _terminalSubscription = null;
        }

        /// <summary>
        /// 受理の判定（副作用なし）。構造の判定は Season の SceneResource の子で見て、名前の規則は使わない。
        /// </summary>
        /// <remarks>
        /// 判定は「停止 → 枝と Season の生存 → driver の状態 → 構造」の順に見て、最初に当たったもので答える。
        /// 枝と Season の生存を driver の状態より先に見るのは、親が既に Event を生かせない状態（Season の unload の開始後、
        /// 枝の切替中）なら、消えかけの Event に AlreadyActive（動いている）と答えないため。Season が Stable を外れるのは
        /// unload の開始だけなので、親が健全な間の driver の状態の答えはこの順でも変わらない。
        /// 生存の内部の順（枝 → 未ロード → 差し替え → Stable でない）も同じ契約の一部で、入れ替えない。
        /// 親の unload の開始後に届く開始 trigger の要求は拒否の Warning になるが、Add の有無は変わらないので抑制しない。
        /// </remarks>
        /// <param name="capturedSeason">枝の確立時に捕捉した Season の個体。</param>
        /// <param name="currentSeason">いま Query が返す Season の個体（unload 開始後は null）。</param>
        /// <param name="activeEventIdentity">driver が Idle でないときの Event の identity。</param>
        internal static MajorEventRequestResult Admit(
            string eventIdentity,
            SceneBase? capturedSeason,
            SceneBase? currentSeason,
            bool seasonStable,
            bool branchBusy,
            bool stopped,
            MajorEventDriverState state,
            string? activeEventIdentity,
            out MajorEventRejectReason reason)
        {
            reason = MajorEventRejectReason.None;
            if (stopped)
            {
                return Reject(MajorEventRejectReason.Stopped, out reason);
            }

            if (branchBusy)
            {
                return Reject(MajorEventRejectReason.BranchBusy, out reason);
            }

            if (capturedSeason == null || currentSeason == null)
            {
                return Reject(MajorEventRejectReason.SeasonNotLoaded, out reason);
            }

            if (!ReferenceEquals(capturedSeason, currentSeason))
            {
                return Reject(MajorEventRejectReason.SeasonReplaced, out reason);
            }

            if (!seasonStable)
            {
                return Reject(MajorEventRejectReason.SeasonNotStable, out reason);
            }

            if (state != MajorEventDriverState.Idle)
            {
                if (!string.Equals(activeEventIdentity, eventIdentity, StringComparison.Ordinal))
                {
                    return Reject(MajorEventRejectReason.OtherEventActive, out reason);
                }

                // Ending の個体は unload 中か保留中。新しい個体を同時に生かさないため、終端を観測するまで受けない。
                return state == MajorEventDriverState.Ending
                    ? Reject(MajorEventRejectReason.Ending, out reason)
                    : MajorEventRequestResult.AlreadyActive;
            }

            var child = FindChild(capturedSeason.SceneResource, eventIdentity);
            if (child == null)
            {
                return Reject(MajorEventRejectReason.NotSeasonChild, out reason);
            }

            if (child.LoadType != LoadType.OnDemand)
            {
                return Reject(MajorEventRejectReason.NotOnDemand, out reason);
            }

            if (child.StreamByDistance)
            {
                return Reject(MajorEventRejectReason.StreamByDistance, out reason);
            }

            return MajorEventRequestResult.Accepted;
        }

        private static MajorEventRequestResult Reject(MajorEventRejectReason value, out MajorEventRejectReason reason)
        {
            reason = value;
            return MajorEventRequestResult.Rejected;
        }

        private static SceneResource? FindChild(SceneResource season, string identity)
        {
            var children = season.Children;
            for (var i = 0; i < children.Count; i++)
            {
                var child = children[i];
                if (child != null && string.Equals(child.Identity, identity, StringComparison.Ordinal))
                {
                    return child;
                }
            }

            return null;
        }

        private void Issue(string eventIdentity)
        {
            var issueId = ++_issueId;
            _state = MajorEventDriverState.Loading;
            _eventIdentity = eventIdentity;
            _captured = null;
            _addInFlight = true;
            _terminalObserved = false;

            SceneBase? captured;
            UniTask add;
            try
            {
                (captured, add) = _issueAdd(eventIdentity);
            }
            catch (Exception ex)
            {
                (captured, add) = (null, UniTask.FromException(ex));
            }

            _captured = captured;
            if (captured != null)
            {
                _attachCompletion(captured, () => OnCompletionNotified(issueId));
            }
            else
            {
                // 捕捉できなくても「発行は無かった」とはしない。Add の task の終わりを観測するまで Ending にとどめ、
                // その間の要求で 2 本目の Add を出さない（再試行もしない）。
                _state = MajorEventDriverState.Ending;
            }

            ObserveAddAsync(issueId, add).Forget();
        }

        private async UniTaskVoid ObserveAddAsync(int issueId, UniTask add)
        {
            Exception? failure = null;
            try
            {
                await add;
            }
            catch (Exception ex)
            {
                failure = ex;
            }

            if (issueId != _issueId)
            {
                return;
            }

            _addInFlight = false;
            var identity = _eventIdentity!;
            if (failure != null)
            {
                // 失敗した Add の個体は Framework が回収し終端を出す。ここでは観測して閉じるだけ。
                _state = MajorEventDriverState.Ending;
                _logger.ZLogError(failure, $"Major event add failed: '{identity}'");
            }
            else if (_captured == null)
            {
                // await より前に捕捉できないのは計画の前提（Stable な親の下の子は最初の await より前に辞書へ入る）の外。
                // 追跡できない個体を残さないよう unload を出して閉じる。
                _logger.ZLogError($"Major event instance was not captured before the first await: '{identity}'");
                if (!_stopped)
                {
                    UnloadAsync(identity).Forget();
                }
            }
            else if (_state == MajorEventDriverState.Loading
                && ReferenceEquals(_query.GetLoadedScene(identity), _captured)
                && _query.IsSceneStable(identity))
            {
                // Stable でなければ Loading のまま。要求と完了の扱いは Loading と Live で同じ。
                _state = MajorEventDriverState.Live;
            }

            TryReturnToIdle();
        }

        private void OnCompletionNotified(int issueId)
        {
            if (_stopped || issueId != _issueId)
            {
                return;
            }

            // Add の await が戻る前（Loading）に届いても取りこぼさない。unload 中・保留の位相は SceneDirector が扱う。
            if (_state != MajorEventDriverState.Loading && _state != MajorEventDriverState.Live)
            {
                return;
            }

            _state = MajorEventDriverState.Ending;
            UnloadAsync(_eventIdentity!).Forget();
        }

        private async UniTaskVoid UnloadAsync(string identity)
        {
            try
            {
                await _unload(identity);
            }
            catch (Exception ex)
            {
                _logger.ZLogError(ex, $"Major event unload failed: '{identity}'");
            }
        }

        private void OnTerminal(SceneTerminalEvent terminalEvent)
        {
            if (_stopped
                || _state == MajorEventDriverState.Idle
                || !string.Equals(terminalEvent.Identity, _eventIdentity, StringComparison.Ordinal))
            {
                return;
            }

            _terminalObserved = true;
            TryReturnToIdle();
        }

        private void TryReturnToIdle()
        {
            if (_state == MajorEventDriverState.Idle || _addInFlight)
            {
                return;
            }

            // 捕捉した個体があるなら、その終端まで待つ。捕捉できなかった発行は Add の終わりだけで閉じる。
            if (_captured != null && !_terminalObserved)
            {
                return;
            }

            _state = MajorEventDriverState.Idle;
            _captured = null;
        }
    }
}
