#nullable enable

using System;
using OneStarMaker.Foundation.UpdateSystem;
using OneStarMaker.Foundation.UpdateSystem.World;
using OneStarMaker.Runtime.UpdateSystem.Hosting;

namespace OneStarMaker.Runtime.UpdateSystem.Api
{
    /// <summary>
    /// Runtime 全体で共有する update system の静的入口。
    /// </summary>
    public static class UpdateSystemRuntime
    {
        private static UpdateSystemHost? s_current;

        public static UpdateCoordinator? Coordinator => s_current?.Coordinator;

        internal static void Install(UpdateSystemHost host)
        {
            if (host == null)
            {
                throw new ArgumentNullException(nameof(host));
            }

            s_current = host;
        }

        internal static void Uninstall(UpdateSystemHost host)
        {
            if (ReferenceEquals(s_current, host))
            {
                s_current = null;
            }
        }

        public static bool RegisterElement(
            string layerId,
            IUpdateElement element,
            int layerOrder = 0,
            int executionOrder = 0)
        {
            if (s_current == null)
            {
                return false;
            }

            var registered = s_current.Coordinator.RegisterElement(layerId, element, layerOrder, executionOrder);
            if (registered)
            {
                s_current.RequestActivation();
            }

            return registered;
        }

        public static bool UnregisterElement(IUpdateElement element)
        {
            return s_current != null && s_current.Coordinator.UnregisterElement(element);
        }

        public static bool RequestElementApply(IUpdateElement element)
        {
            return s_current != null && s_current.Coordinator.RequestElementApply(element);
        }
    }
}
