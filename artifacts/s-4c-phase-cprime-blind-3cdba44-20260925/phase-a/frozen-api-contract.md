# Frozen API contract from the canonical Phase A HANDOFF

The A3 freeze record says the slice handoff is canonical. This immutable excerpt preserves its pre-implementation API clauses without including Phase C/C' review records.

## `RenderEnvironmentState` member representation (canonical HANDOFF §2.2)

Unity-side `record` is prohibited. `RenderEnvironmentState` is a `readonly struct` with a constructor. `init` is prohibited. Its eight value members are public get-only properties:

- `Vector3 SunEulerDegrees { get; }`
- `Color SunColor { get; }`
- `float SunIntensity { get; }`
- `Color AmbientSkyColor { get; }`
- `bool FogEnabled { get; }`
- `Color FogColor { get; }`
- `float FogDensity { get; }`
- `float GlobalVolumeWeight { get; }`

## Public API namespace (canonical HANDOFF §2.4)

The frozen namespace is `OneStarMaker.Runtime.Rendering.Environments`. The public API set includes `RenderEnvironmentState`, `IRenderEnvironment`, `RenderEnvironmentLease`, `IRenderEnvironmentSink`, `RenderEnvironment`, and `UnityRenderEnvironmentSink`; `RenderEnvironmentValidator` is internal.

## Why this excerpt is supplied

The initial A1 draft used the parent namespace and described the property shape in a comment. A3 says the HANDOFF is canonical; this excerpt records the final Phase A contract for these two API details. It was copied from the frozen HANDOFF before Phase C/C' result sections and is provided to prevent treating superseded A1 draft text as the final contract.
