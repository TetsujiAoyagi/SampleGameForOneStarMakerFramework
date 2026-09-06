#nullable enable

namespace SampleGame.InGame.Streaming
{
    internal static class CellCompanionLoadRules
    {
        internal static bool ShouldAdd(
            bool parentIsStable,
            bool roleIsIncluded,
            bool childIsLoaded,
            bool childIsInFlight)
            => parentIsStable && roleIsIncluded && !childIsLoaded && !childIsInFlight;
    }
}
