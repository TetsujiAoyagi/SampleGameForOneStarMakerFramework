#nullable enable

using System;
using SampleGame.InGame.Player;
using UnityEngine;

namespace SampleGame.InGame.Streaming
{
    /// <summary>
    /// WaitUntilWorldReady 成功後の Teleport → RegisterFlight → 入力 ON だけ。
    /// PlayerScene の private bootstrap から順序の中核を切り離す。
    /// </summary>
    /// <remarks>
    /// 失敗時は入力 OFF のまま、既に登録した Focus を外す。例外 catch での入力 ON はしない。
    /// </remarks>
    internal static class PlayerWorldReadySequence
    {
        internal static void Run(
            IInGameSessionServices session,
            IPlayerReadyActor actor,
            Vector3 spawnPosition)
        {
            if (session == null)
            {
                throw new ArgumentNullException(nameof(session));
            }

            if (actor == null)
            {
                throw new ArgumentNullException(nameof(actor));
            }

            var registered = false;
            try
            {
                actor.Teleport(spawnPosition, Vector3.forward);
                session.RegisterFlight(actor);
                registered = true;
                actor.InputEnabled = true;
            }
            catch
            {
                actor.InputEnabled = false;
                if (registered)
                {
                    session.UnregisterFlight(actor);
                }

                throw;
            }
        }
    }
}
