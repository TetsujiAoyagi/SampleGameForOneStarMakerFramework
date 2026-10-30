#nullable enable

using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using OneStarMaker.Runtime.SceneSystem;
using UnityEngine;
using ZLogger;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace SampleGame.InGame.World
{
    /// <summary>
    /// 1 つの Events Scene に置かれた <see cref="MajorEventStartTrigger"/> を集め、進入の出来事を
    /// <see cref="InGameSession"/> への major Event の要求に変える。trigger が 1 つも無い Scene では作らない。
    /// <para>
    /// 要求先は Events → Cell → Season → InGameSession の固定の段数でたどる。任意の祖先を型まで歩く汎用の解決にはしない
    /// （段数が変わる配置は、この binder を置く Scene の契約の変更として扱う）。状態は持たない。
    /// </para>
    /// </summary>
    internal sealed class MajorEventStartBinder
    {
        private readonly SceneResource _events;
        private readonly ISceneQuery _query;
        private readonly ILogger _logger;

        private MajorEventStartBinder(SceneResource events, ISceneQuery query, ILogger logger)
        {
            _events = events;
            _query = query;
            _logger = logger;
        }

        /// <summary>root から trigger を集め（非 active も含む）、受け口を付ける。trigger が無ければ null。</summary>
        internal static MajorEventStartBinder? Create(
            IReadOnlyList<GameObject> roots,
            SceneResource events,
            ISceneQuery query,
            ILoggerFactory loggerFactory)
        {
            if (events == null)
            {
                throw new ArgumentNullException(nameof(events));
            }

            MajorEventStartBinder? binder = null;
            foreach (var root in roots)
            {
                if (root == null)
                {
                    continue;
                }

                foreach (var trigger in root.GetComponentsInChildren<MajorEventStartTrigger>(includeInactive: true))
                {
                    binder ??= new MajorEventStartBinder(events, query, loggerFactory.CreateLogger<MajorEventStartBinder>());
                    var owner = binder;
                    var eventIdentity = trigger.EventIdentity;
                    trigger.Attach(() => owner.Request(eventIdentity));
                }
            }

            return binder;
        }

        private void Request(string eventIdentity)
        {
            var session = ResolveSession();
            if (session == null)
            {
                _logger.ZLogWarning($"Major event start ignored: no loaded InGameSession above '{_events.Identity}' ('{eventIdentity}')");
                return;
            }

            session.RequestMajorEvent(eventIdentity);
        }

        private InGameSession? ResolveSession()
        {
            // SceneResource は UnityEngine.Object なので ?. を使わず == null で判定する。
            var cell = _events.Parent;
            var season = cell == null ? null : cell.Parent;
            var session = season == null ? null : season.Parent;
            return session == null ? null : _query.GetLoadedScene(session.Identity) as InGameSession;
        }
    }
}
