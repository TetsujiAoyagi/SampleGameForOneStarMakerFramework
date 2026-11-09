#nullable enable

using System;
using System.Collections.Generic;

namespace SampleGame.DependOnAll.Editor.Build
{
    /// <summary><see cref="PlayEntryTransaction"/> が使う Editor の事実と副作用。Unity 無しの単体テストで差し替える。</summary>
    internal sealed class PlayEntryPorts
    {
        /// <summary>引数が真なら候補の content まで検証する（content ツリー全体を見るので、最終判定だけで真にする）。</summary>
        internal Func<bool, PlayEntryFacts> CollectFacts { get; set; } = _ => throw new InvalidOperationException("CollectFacts is not set.");
        internal Action CancelPlay { get; set; } = () => { };
        internal Func<bool> IsIdle { get; set; } = () => true;
        internal Action<IReadOnlyList<KeyValuePair<string, string>>> ApplyForOnePlay { get; set; } = _ => { };
        internal Action AlignFastMode { get; set; } = () => { };
        internal Action EnterPlaymode { get; set; } = () => { };
        /// <summary>これから Play に入る計画を record に書く。観測はここから始まる。</summary>
        internal Action<PlayEntryFacts?, PlayEntryPlan> PublishPlanned { get; set; } = (_, _) => { };
        internal Action<PlayEntryFacts?, PlayEntryPlan> PublishBlocked { get; set; } = (_, _) => { };
        /// <summary>未保存の Scene の確認。偽なら利用者が拒んだ。</summary>
        internal Func<bool> ConfirmSaveScenes { get; set; } = () => true;
        internal Action ReplaceWithEmptyScene { get; set; } = () => { };
    }

    /// <summary>
    /// Play の取り消し → tick → 適用 → 再 Play の順序を持つ状態機械。1 回の介入の間だけメモリに状態を持ち、Play 中の再 compile で
    /// 消えても Play に至らないだけで、置いた値は ledger の規則で戻る。
    /// <para>
    /// 入口は ledger の Reset を呼ばない。Reset は移行中に例外を投げ、手動の ledger も消すので、拒否・例外のどの経路でも
    /// 一回の ledger は次の idle tick で ContentDeliveryPlayBridge.HandleIdle が戻す。
    /// </para>
    /// </summary>
    internal sealed class PlayEntryTransaction
    {
        internal enum Phase { Idle, WaitingForTick, Entering }

        private readonly PlayEntryPorts _ports;
        private Phase _phase = Phase.Idle;

        internal PlayEntryTransaction(PlayEntryPorts ports) => _ports = ports ?? throw new ArgumentNullException(nameof(ports));

        internal Phase CurrentPhase => _phase;

        /// <summary>ExitingEditMode。ここの判定は取り消すかどうかにだけ使い、最終の判断は tick で行う。</summary>
        internal void OnExitingEditMode()
        {
            // 取り消しの次の tick までに 2 度目の Play が来ても、計画は増やさずに取り消すだけにする。
            if (_phase == Phase.WaitingForTick)
            {
                _ports.CancelPlay();
                return;
            }

            var entering = _phase == Phase.Entering;
            var facts = TryCollect(validateCandidate: false, out var failure);
            var plan = facts != null ? PlayEntryDecision.Decide(facts) : failure!;

            if (plan.Action == PlayEntryAction.PassThrough)
            {
                // 再入の素通りは tick で書いた計画のまま。行 3 の一回の ledger は入口自身が置いたもの。
                if (!entering && plan.Rule != PlayEntryRule.BatchMode) _ports.PublishPlanned(facts, plan);
                return;
            }

            _ports.CancelPlay();
            // 再入は再 Play の中なのでこれ以上割り込まない。一回の ledger を見た行 2 の拒否は、次の tick までに ledger が戻ると
            // 判定の前提が消えて別の Play に化けるので、ここで確定する。
            if (entering || plan.Rule == PlayEntryRule.DirectoryWithReusableScenes && facts is { HasOnePlayLedger: true })
                _ports.PublishBlocked(facts, plan);
            else
                _phase = Phase.WaitingForTick;
        }

        /// <summary>Play に入った。取り消しが効かなかった時に、Play を抜けた後の tick で再 Play しない。</summary>
        internal void OnEnteredPlayMode() => _phase = Phase.Idle;

        /// <summary>update の tick。再判定・適用・EnterPlaymode は 1 つの同期呼び出しの中で続けて行い、間に他の callback を挟まない。</summary>
        internal void OnTick()
        {
            if (_phase != Phase.WaitingForTick || !_ports.IsIdle()) return;
            _phase = Phase.Idle;

            var facts = TryCollect(validateCandidate: true, out var failure);
            var plan = facts != null ? PlayEntryDecision.Decide(facts) : failure!;
            if (plan.Action == PlayEntryAction.Reject)
            {
                _ports.PublishBlocked(facts, plan);
                return;
            }

            try
            {
                // Play Mode Script は拒否の時に変えない。ここまで来た後の失敗でも元に戻さない（永続の変更を仕様にしている）。
                if (plan.AlignFastMode) _ports.AlignFastMode();
                if (plan.Action == PlayEntryAction.Intervene) _ports.ApplyForOnePlay(plan.Environment);
                _ports.PublishPlanned(facts, plan);
                _phase = Phase.Entering;
                _ports.EnterPlaymode();
            }
            catch (Exception ex)
            {
                _ports.PublishBlocked(facts, PlayEntryDecision.EntryFailed("Play could not start: " + ex.Message));
            }
            finally
            {
                // EnterPlaymode の中で同期の ExitingEditMode まで済んでいる。以後の ExitingEditMode は新しい Play の押下として扱う。
                _phase = Phase.Idle;
            }
        }

        /// <summary>
        /// 利用者が選んだ回復操作「空 Scene で Play」。未保存の内容は Unity の保存確認を通し、拒まれたら Scene を残して Play しない。
        /// 後は通常の Play と同じ判定に通す。
        /// </summary>
        internal bool RecoverInEmptyScene()
        {
            if (!_ports.ConfirmSaveScenes()) return false;
            _ports.ReplaceWithEmptyScene();
            _ports.EnterPlaymode();
            return true;
        }

        private PlayEntryFacts? TryCollect(bool validateCandidate, out PlayEntryPlan? failure)
        {
            failure = null;
            try { return _ports.CollectFacts(validateCandidate); }
            catch (Exception ex)
            {
                failure = PlayEntryDecision.EntryFailed("Could not read the Editor state: " + ex.Message);
                return null;
            }
        }
    }
}
