#nullable enable

using System;

namespace OneStarMaker.Runtime
{
    /// <summary>設定値と nullable な Editor override から起動時 Variant を一度だけ決定する。</summary>
    internal static class SceneVariantResolver
    {
        internal static string Resolve(string configuredValue, Func<string?>? editorResolver, bool useEditorResolver)
        {
            if (configuredValue == null)
            {
                throw new ArgumentNullException(nameof(configuredValue));
            }

            if (!useEditorResolver || editorResolver == null)
            {
                return configuredValue;
            }

            var editorValue = editorResolver();
            return editorValue ?? configuredValue;
        }
    }
}
