# S-4c Phase A revision 1 — frozen decision snapshot

- Approved: 2026-09-25 by the human owner, explicitly authorizing removal of the post-unload baked-floor re-entry check from this slice's minimum conditions.
- Supersedes only A3-frozen.md item 9 and the related minimum-condition / required-play wording in A1-snapshot.md. All other A0–A3 decisions remain in force.
- Slice question remains: can one App-lifetime RenderEnvironment own sun / ambient / fog through a lease, and can the representative Spring multi-scene bake work as Scene-scoped content?

## Minimum conditions for this revision

1. Pure C# tests prove single ownership, immediate failure for a second owner, stale Dispose / stale Apply safety, and restoration of baseline on release.
2. Game code (at least PlayerScene) has no RenderSettings assignments.
3. Each seasonal SeasonLightingScene acquires and applies its preset on load and releases on unload; a second season fails to load.
4. Spring_Lighting_4_2 and Spring_Lighting_5_2 are authored in World Workspace, contain no Directional Light, and do not alter global sun / sky / fog / Volume.
5. The Progressive bake of the representative seven Scenes has committed lightmaps.
6. Human observation records no obvious lightmap seam at the (4,2)/(5,2) boundary.
7. During Cell unload, the Season Lighting lease keeps Spring fog and sun at the Spring preset.

The post-unload return to a Cell and baked-floor reappearance are explicitly outside this slice's minimum conditions. They remain unverified and are transferred to later Content Directory / streaming validation. Do not treat them as passed or claim evidence for them.

## Decision rule

GO requires all seven revised minimum conditions and no always-on contract violation. No other A3 condition, implementation responsibility, API, dependency, owner, or lifetime changes in this revision.

## Approval boundary

This revision is an explicit human-approved scope exception replacing the former post-unload baked-floor re-entry requirement. It does not change fail-closed Content Directory startup behavior or authorize implementation changes.
