#nullable enable

using System;
using System.Collections.Generic;

namespace SampleGame.DependOnAll.Editor.WorldAuthoring
{
    internal enum WorldSeason { Spring, Summer, Autumn, Winter }
    internal enum WorldWorkspaceRole { Level, Environment, Lighting, Vfx, Planner }
    internal enum WorldLevelPayload { Full, Whitebox }

    internal readonly struct WorldWorkspaceOpenItem
    {
        internal WorldWorkspaceOpenItem(string identity, string variant, bool required, bool canCreate)
        {
            Identity = identity;
            Variant = variant;
            Required = required;
            CanCreate = canCreate;
        }

        internal string Identity { get; }
        internal string Variant { get; }
        internal bool Required { get; }
        internal bool CanCreate { get; }
    }

    internal sealed class WorldWorkspaceSelection
    {
        internal WorldWorkspaceSelection(
            WorldSeason season,
            int x,
            int y,
            WorldWorkspaceRole role,
            WorldLevelPayload payload)
        {
            if (x < 0 || x > 8) throw new ArgumentOutOfRangeException(nameof(x));
            if (y < 0 || y > 5) throw new ArgumentOutOfRangeException(nameof(y));
            if (!Enum.IsDefined(typeof(WorldSeason), season)) throw new ArgumentOutOfRangeException(nameof(season));
            if (!Enum.IsDefined(typeof(WorldWorkspaceRole), role)) throw new ArgumentOutOfRangeException(nameof(role));
            if (!Enum.IsDefined(typeof(WorldLevelPayload), payload)) throw new ArgumentOutOfRangeException(nameof(payload));
            Season = season;
            X = x;
            Y = y;
            Role = role;
            Payload = payload;
        }

        internal WorldSeason Season { get; }
        internal int X { get; }
        internal int Y { get; }
        internal WorldWorkspaceRole Role { get; }
        internal WorldLevelPayload Payload { get; }
        internal string CellIdentity => $"{Season}_Cell_{X}_{Y}";

        internal IReadOnlyList<WorldWorkspaceOpenItem> BuildOpenPlan()
        {
            var cellVariant = Role == WorldWorkspaceRole.Planner || (Role == WorldWorkspaceRole.Level && Payload == WorldLevelPayload.Whitebox)
                ? "Whitebox"
                : string.Empty;
            var result = new List<WorldWorkspaceOpenItem>
            {
                new(CellIdentity, cellVariant, required: true, canCreate: false),
            };

            switch (Role)
            {
                case WorldWorkspaceRole.Level:
                    break;
                case WorldWorkspaceRole.Environment:
                    result.Add(Required("Environment"));
                    break;
                case WorldWorkspaceRole.Lighting:
                    result.Add(Required("Environment"));
                    result.Add(new WorldWorkspaceOpenItem($"{Season}_Lighting", string.Empty, true, false));
                    result.Add(Optional("Lighting"));
                    break;
                case WorldWorkspaceRole.Vfx:
                    result.Add(Required("Environment"));
                    result.Add(new WorldWorkspaceOpenItem($"{Season}_Lighting", string.Empty, true, false));
                    result.Add(Optional("VFX"));
                    break;
                case WorldWorkspaceRole.Planner:
                    result.Add(Optional("Events"));
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
            return result;
        }

        internal string CompanionIdentity(string roleToken) => $"{Season}_{roleToken}_{X}_{Y}";

        private WorldWorkspaceOpenItem Required(string role)
            => new(CompanionIdentity(role), string.Empty, required: true, canCreate: false);

        private WorldWorkspaceOpenItem Optional(string role)
            => new(CompanionIdentity(role), string.Empty, required: false, canCreate: true);
    }
}
