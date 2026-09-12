# Frozen Phase A contract extract for C'

Source: `docs/handoff/S-4b_WORLD_GENERATION_A0.md`, frozen Phase A. This is a scoped extract, not the full HANDOFF.

## Scope and invariants

- Review only P2 generated assets, startup wiring, and P3 generator removal. P1 is already in `develop` and is excluded.
- Dependencies remain Game → Framework. Do not add asmdef references, public APIs, `SceneState`, or `SceneEventType` values.
- Session lifetime owns the season controller, terminal tracking, issued-operation registry, distance driver, and companion driver. Session shutdown stops new work and does not await branch draining in a lifecycle callback.
- An Add return is not Stable. Terminal completion is keyed by identity plus instance generation and is not inferred only from a null query.
- Initial order is Spring Stable, then `Spring_Cell_0_4` Stable, then player input enabled, then distance ticking begins.
- The factory maps four `Season_*` identities and four `*_Lighting` identities and has no `World` branch.
- `WorldCellCatalog` is 9 × 6 and the source spawn cell is `(0, 4)`.
- Generated inventory is 652 Scene files and 440 logical SceneResource assets. Expected Scene paths are in `expected-set.txt`; missing and extra sets must both be empty.
- Do not accept Whitebox absence through runtime fallback. Graph, Map, Addressables, volume, identity/GUID uniqueness, load type, and parent-child relationships are machine-inspection concerns.
- The temporary `SeasonWorld*` tools, old bulk WorldCell generator, supporting planning/state code, and their dedicated tests are absent from the final implementation head.
- Do not delete the `World/` directory wholesale. Keep runtime cell/companion types and shared-material support; remove the old `World.unity`, grid asset, and `WorldScene` replacement residue.
- Generate is single-shot and must not be rerun. Human representative operations are a separate later activity.

## Structural map

- `InGameSessionScene` is the composition/lifetime hub only.
- `SessionSeasonController` serializes branch changes and coordinates delegated drivers/trackers; it does not absorb concrete SceneDirector internals.
- `SceneTerminalTracker` observes terminal events; `IssuedSceneOperationRegistry` owns operations issued by the branch.
- `SeasonCandidateSelection` is a pure selection boundary; `WorldCellCatalog` owns the 9 × 6 authoring coordinates.
- `PlayerWorldReadySequence` owns only Teleport → focus registration → input enable.
- `SeasonScene` owns Scene-scoped shared Lit preload; `SeasonLightingScene` remains an empty scaffold for the later slice.
- `GameSceneFactory` is the composition mapping boundary. `ResultScene` calls the stop entrance before switching without awaiting drain.

## Machine and human checks

- Review structure before behavior, and explain material line-count divergence rather than automatically splitting.
- Run contract audit and Unity EditMode tests. A zero-test result is not evidence.
- Inspect all generated files by expected sets and raw tooling output, not by visually sampling thousands of files.
- Record Editor-unavailable checks and Addressables build status explicitly.
- Prepare representative-operation steps without making the human judgment.
