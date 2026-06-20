#nullable enable

using Microsoft.Extensions.Logging;
using OneStarMaker.Foundation.Logging;
using UnityEngine;

using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace OneStarMaker.Runtime.UpdateSystem.Adapters
{
    /// <summary>
    /// MonoBehaviour と update system の橋渡しを担う adapter。
    /// </summary>
    public abstract class UpdateBehaviourAdapter : MonoBehaviour,
        global::OneStarMaker.Foundation.UpdateSystem.IUpdateElement,
        global::OneStarMaker.Foundation.UpdateSystem.IMainThreadApplyElement
    {
        private static readonly AppLoggerFactory s_loggerFactory = new(minimumLevel: LogLevel.Error);
        private static readonly ILogger s_logger = s_loggerFactory.CreateLogger(nameof(UpdateBehaviourAdapter));

        protected virtual string LayerId => "Gameplay";

        protected virtual int LayerOrder => 0;

        protected virtual int ExecutionOrder => 0;

        protected virtual void Awake()
        {
            if (!Api.UpdateSystemRuntime.RegisterElement(LayerId, this, LayerOrder, ExecutionOrder))
            {
                s_logger.LogError(
                    "[{Component}] type={Type} layerId={LayerId} layerOrder={LayerOrder} executionOrder={ExecutionOrder} message=登録に失敗しました。UpdateSystemRuntime が初期化される前に Awake が走っている可能性があります。",
                    nameof(UpdateBehaviourAdapter),
                    GetType().Name,
                    LayerId,
                    LayerOrder,
                    ExecutionOrder);
            }
        }

        protected virtual void OnDestroy()
        {
            Api.UpdateSystemRuntime.UnregisterElement(this);
        }

        protected virtual void OnElementStarted()
        {
        }

        protected virtual void OnElementUpdate(in global::OneStarMaker.Foundation.UpdateSystem.UpdateFrameContext context)
        {
        }

        protected virtual void OnElementLateUpdate(in global::OneStarMaker.Foundation.UpdateSystem.UpdateFrameContext context)
        {
        }

        protected virtual void OnMainThreadApply(in global::OneStarMaker.Foundation.UpdateSystem.MainThreadApplyContext context)
        {
        }

        void global::OneStarMaker.Foundation.UpdateSystem.IUpdateElement.OnElementStart() => OnElementStarted();

        void global::OneStarMaker.Foundation.UpdateSystem.IUpdateElement.OnElementUpdate(in global::OneStarMaker.Foundation.UpdateSystem.UpdateFrameContext context) => OnElementUpdate(in context);

        void global::OneStarMaker.Foundation.UpdateSystem.IUpdateElement.OnElementLateUpdate(in global::OneStarMaker.Foundation.UpdateSystem.UpdateFrameContext context) => OnElementLateUpdate(in context);

        void global::OneStarMaker.Foundation.UpdateSystem.IMainThreadApplyElement.ApplyMainThread(in global::OneStarMaker.Foundation.UpdateSystem.MainThreadApplyContext context) => OnMainThreadApply(in context);
    }
}
