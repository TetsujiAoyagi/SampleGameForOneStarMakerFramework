#nullable enable

using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OneStarMaker.Runtime.AssetManagement;
using OneStarMaker.Runtime.Rendering.Vfx;
using UnityEngine;
using ILogger = Microsoft.Extensions.Logging.ILogger;
using Object = UnityEngine.Object;

namespace SampleGame.InGame.World
{
    /// <summary>
    /// 1 つの Scene に置かれた <see cref="LocalVfxEventTrigger"/> と、その session の一覧を Scene の lifecycle に接続する。
    /// <para>
    /// Scene class が instance を 1 つ持ち、寿命は Scene と同じ。Scene の unload の開始では root の一覧が空になるので、
    /// 初期化の時点で集めた session をここで保持する。trigger が 1 つも無い Scene では作らない
    /// （trigger の無い companion は asset をロードせず、振る舞いを変えない）。
    /// </para>
    /// </summary>
    internal sealed class LocalVfxEventBinder
    {
        private readonly List<(LocalVfxEventSession Session, string Address)> _entries;

        private LocalVfxEventBinder(List<(LocalVfxEventSession Session, string Address)> entries)
        {
            _entries = entries;
        }

        internal int Count => _entries.Count;

        /// <summary>
        /// root から trigger を集め（非 active も含む）、trigger ごとに session を作って付ける。trigger が無ければ null。
        /// 起動する instance の所有者は各 trigger の GameObject（<c>Bind</c>）。
        /// </summary>
        internal static LocalVfxEventBinder? Create(
            IReadOnlyList<GameObject> roots,
            IVfxSystem vfx,
            ILoggerFactory loggerFactory,
            string sceneIdentity)
        {
            List<(LocalVfxEventSession, string)>? entries = null;
            ILogger? logger = null;
            foreach (var root in roots)
            {
                if (root == null)
                {
                    continue;
                }

                foreach (var trigger in root.GetComponentsInChildren<LocalVfxEventTrigger>(includeInactive: true))
                {
                    var transform = trigger.transform;
                    logger ??= loggerFactory.CreateLogger<LocalVfxEventSession>();
                    var address = trigger.VfxAddress;
                    var session = new LocalVfxEventSession(
                        vfx,
                        AssetOwner.Bind(trigger.gameObject),
                        transform.position,
                        transform.rotation,
                        $"{sceneIdentity}/{trigger.name} ({address})",
                        logger);
                    trigger.Attach(session);
                    (entries ??= new List<(LocalVfxEventSession, string)>()).Add((session, address));
                }
            }

            return entries == null ? null : new LocalVfxEventBinder(entries);
        }

        /// <summary>
        /// 各 session の source を順にロードする。ロードの失敗は session が trigger ごとに記録し、外へ出さない。
        /// </summary>
        /// <param name="load">address から source をロードする。所有者（Scene）は呼び出し側が決める。</param>
        internal async UniTask PrepareAllAsync(Func<string, UniTask<Object?>> load)
        {
            if (load == null)
            {
                throw new ArgumentNullException(nameof(load));
            }

            foreach (var (session, address) in _entries)
            {
                await session.PrepareAsync(() => load(address));
            }
        }

        /// <summary>全 session を終わらせる（生存中の instance を回収し、以後は起動しない）。</summary>
        internal void EndAll()
        {
            foreach (var (session, _) in _entries)
            {
                session.End();
            }
        }
    }
}
