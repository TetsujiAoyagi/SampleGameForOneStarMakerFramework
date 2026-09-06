#nullable enable

using System;

namespace SampleGame.InGame.Streaming
{
    internal static class CellCompanionRoleClassifier
    {
        internal static bool TryClassify(string identity, out CellCompanionRole role)
        {
            role = default;
            if (identity == null)
            {
                return false;
            }

            var tokens = identity.Split('_');
            if (tokens.Length < 3)
            {
                return false;
            }

            return tokens[tokens.Length - 3] switch
            {
                "Environment" => Set(CellCompanionRole.Environment, out role),
                "Lighting" => Set(CellCompanionRole.Lighting, out role),
                "VFX" => Set(CellCompanionRole.Vfx, out role),
                "Events" => Set(CellCompanionRole.Events, out role),
                _ => false,
            };
        }

        private static bool Set(CellCompanionRole value, out CellCompanionRole role)
        {
            role = value;
            return true;
        }
    }
}
