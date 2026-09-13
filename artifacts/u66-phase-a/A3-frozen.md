# U66 Unity 6.6 migration — Phase A revision 2 frozen snapshot

## Metadata

- type: slice
- status: A3 frozen
- frozen: 2026-09-13 JST
- branch: `codex/u66-phase-a`
- implementation base: `3244f3635c6e18fffc1bbc40d3126e6d1eee009a`
- retained implementation head: `4263a33ee0d5e75b67e82e6260ff556f12fd1dce`
- risk: high
- human decision: retain the existing Unity 6.6 implementation; do not roll it back. This revises authorization and remaining gates, not history.

## Objective and exclusions

Migrate the project from `6000.5.0f1 (88b47c5e7076)` to exact `6000.6.0f1 (f7f8ed4d1e24)`, preserve OSM boundaries, and prove compile/EditMode plus representative Play, authoring, Addressables, and Windows Player behavior. Content Directories APIs, build-system redesign, new SceneResource payloads, world regeneration, arbitrary reserialization, CI, and multi-platform certification remain excluded.

## Accepted implementation

1. Keep the exact Editor pin and the UPM `updateDependencies` result recorded by `unity/Logs/Editor.log`: Addressables 2.11.2, Ads 4.19.0, AI Navigation 2.0.14, Cinemachine 6.6.0, collab-proxy 2.13.6, Input System 1.20.0, Multiplayer Center 2.0.1, URP 17.6.0, Test Framework 1.8.0, Timeline 6.6.0, uGUI 2.6.0, VisualScripting 1.9.12, and tetgen 1.0.0. Keep transitive Burst 2.0.0, Collections 6.6.0, Performance Tests 6.6.0, SBP 3.0.3, Splines 2.9.0, graph-authoring 1.0.0, profiling.core 1.0.3, and Searcher 4.9.5 as resolved in the tracked lock.
2. Keep the four third-party Git manifest URLs and tracked lock as one restore unit. Their lock hashes must remain LitMotion `2053ef5c23f2ae755dd85d5865b698c542c351b6`, CsprojModifier `3c9be1a827ce7a2e0d9518e34905fc2fc7a6d5df`, UniTask `e5acc106ee196bc5a32fb14cdf2987b0f96d11e0`, and NuGetForUnity `c2af83c9d4f8cdaada9d4a0e94de2f195d8e1d01`. A missing/deleted lock is not an equivalent restore.
3. Keep the exact generated serialization deltas: URP Global Settings schema/resources and stripping-field migration, Addressables `m_DisableWriteTypeTree: 0`, and Project Auditor key ordering. These do not authorize Scene/Prefab/SceneResource/GUID changes or arbitrary reserialization.
4. Keep the one-line TMP repair in `DebugProfilerView.cs`: `enableWordWrapping = false` to `textWrappingMode = TextWrappingModes.NoWrap`. The source error is CS0619 under 6.6. The change stays inside existing display setup, adds no responsibility or dependency, and must retain no-wrap plus truncate behavior.
5. Keep all existing NuGet package versions, 12 asmdef edges, Game→Framework direction, AssetOwner lifetimes, SceneState order, UpdateSystem contract, Scene graph, and 661 Scene paths/GUIDs.

## Responsibility map

- `ProjectVersion.txt`: project-lifetime Editor pin only.
- `manifest.json` and `packages-lock.json`: direct input and generated dependency graph; no runtime owner or public API. The lock is a cohesive generated graph even above 500 lines.
- `UniversalRenderPipelineGlobalSettings.asset`, `AddressableAssetSettings.asset`, `ProjectAuditorSettings.asset`: package/Editor-owned declarative schema migration only. Visual and build semantics require Phase C verification.
- `DebugProfilerView.cs`: existing Debug UI display responsibility and existing view/GameObject lifetime. TMP input only; no new public surface or pure logic. Compile plus runtime warning layout is the test boundary.
- No new class, namespace, manager, state, owner, lifetime, asmdef, or core logic is authorized.

## Deferred/rejected changes

- Defer adding SHA fragments to Git URLs; do not modify the retained implementation in this slice solely for that cleanup.
- Defer `tools/run-tests.ps1` behavior changes to a separate reviewed slice. Phase C must inspect raw XML/log for total, failed, skipped, compiler errors, and final process result.
- Reject Content Directories work, build orchestration redesign, optional cleanup/removal of packages, world regeneration, global scene save, and unrelated asset changes.

## Phase B conformance and stop conditions

The retained head is the authorized Phase B output. Any further implementation change, package drift, source repair, serialized asset beyond the three accepted assets, Git hash drift, NuGet drift, Scene/Prefab/GUID/asmdef change, or new dependency/owner/lifetime/public API invalidates the existing evidence and returns to A.

## Phase C gates

1. Reuse the head-fixed compile/EditMode evidence only while implementation head remains `4263a33e...`; verify raw XML has total > 0, failed 0, unexpected skipped 0 and raw log has no compile error.
2. Verify all Scene/GUID/resource/addressable relationships mechanically and representative Spring Full/Whitebox/Environment/Lighting/bootstrap/UI scenes through the live Editor.
3. With domain reload disabled, run Full and Whitebox separately, repeat Play/Stop twice, exercise Spring streaming traversal, camera/input/UI, unload/reload, UniTask/LitMotion cleanup, and DebugSocket/telemetry reconnect.
4. Exercise World Workspace representative open/close and dirty-cancel preservation without destructive generation.
5. Capture PC before/after visual evidence and inspect URP resources, stripping, pink/missing materials, Volume and camera blend. Mobile remains asset/compile only and must be reported unverified as a Player target.
6. Use existing tracked profiles `Assets/OneStarMaker/Editor/BuildProfiles/Default.asset`, `Production.asset`, and `WorldWhitebox.asset`. Record the active profile GUID, selected set, graphics API, Windows backend, Managed Code Variant, build report, output hash, Player log, and app-config/group restoration. Run packed Addressables and Windows x64 Player for Full and Whitebox without inventing a new profile or pipeline.
7. The missing base-bound 6.5 raw baseline is accepted as a documented residual risk, not reconstructed evidence. Compare structural content to the base commit and current runtime behavior to the existing public contract. Do not claim performance equivalence or prove rollback by reusing the 6.6 Library.
8. Preserve `PRE_PHASE_A_BUILDSYSTEM_REBUILD_UNITY66_V3.md` and all other user-owned untracked work throughout validation.

## A2/A3 integration

- Architecture review recommendations 1–5: accepted. Exact packages, source repair, and three serialization assets are now explicit; no redesign is introduced.
- Package review recommendation 1: accepted with documented exception. The local Editor log supplies `Update Mode: updateDependencies` and the exact added/updated list; the human explicitly requires retaining the implementation.
- Package review recommendations 2–4: accepted as the manifest+lock restore contract, generated Addressables evidence, and logged CS0619 minimal repair.
- Package review recommendation 5: accepted for raw evidence; wrapper modification deferred.
- Package review recommendation 6: accepted. Rollback/base behavior remains an explicit residual risk and untracked user work is protected.
- Human A3 decision: current implementation is accepted and retained. Phase C success still depends on the remaining gates; freezing this plan is not a migration PASS.
