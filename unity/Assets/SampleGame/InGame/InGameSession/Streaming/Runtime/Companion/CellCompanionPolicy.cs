#nullable enable

using System;

namespace SampleGame.InGame.Streaming
{
    public enum CellCompanionSet
    {
        Full,
        Planner,
        Lighting,
        Vfx,
    }

    internal enum CellCompanionRole
    {
        Environment,
        Lighting,
        Vfx,
        Events,
    }

    public static class CellCompanionSetParser
    {
        public static CellCompanionSet Parse(string? value, bool keyExists)
        {
            if (!keyExists)
            {
                return CellCompanionSet.Full;
            }

            return value switch
            {
                "Full" => CellCompanionSet.Full,
                "Planner" => CellCompanionSet.Planner,
                "Lighting" => CellCompanionSet.Lighting,
                "VFX" => CellCompanionSet.Vfx,
                _ => throw new FormatException(
                    $"world:cellCompanionSet must be exactly Full, Planner, Lighting, or VFX; actual='{value ?? "<null>"}'."),
            };
        }
    }

    internal static class CellCompanionPolicy
    {
        internal static bool Includes(CellCompanionSet set, CellCompanionRole role)
        {
            return set switch
            {
                CellCompanionSet.Full => true,
                CellCompanionSet.Planner => role == CellCompanionRole.Events,
                CellCompanionSet.Lighting => role is CellCompanionRole.Environment or CellCompanionRole.Lighting,
                CellCompanionSet.Vfx => role is CellCompanionRole.Environment or CellCompanionRole.Lighting or CellCompanionRole.Vfx,
                _ => throw new ArgumentOutOfRangeException(nameof(set), set, null),
            };
        }
    }
}
