# S-4c Phase B Result Snapshot

- Branch: `cursor/s-4c-world-lighting-a3-4a38`
- Implementation base: `553b7b150e13245b369d75dc4baa12d86e9559aa`
- Implementation head: `1241c8e7b4e475f934fc8931ba783c755508ee2d`
- Generated for discovery-C recheck: 2026-09-24 (Asia/Tokyo)
- Source of truth: `docs/handoff/S-4c_WORLD_LIGHTING.md`, §6 Phase B implementation result, corrected by the remediation recorded on this head.

## Implementation and content state

- `RenderEnvironment` and `UnityRenderEnvironmentSink` provide the single-owner lease and Unity I/O. The sink captures/restores fog, ambient colors/mode/intensity, and `RenderSettings.sun`.
- `AppInitializer` owns the App-lifetime instance. `GameSceneFactory` injects it into the four `SeasonLightingScene` variants. Cell Lighting scenes remain companions and do not acquire the lease.
- Spring representative bake settings are in `Spring_Representative.lighting` and shared by exactly the seven representative scenes. The shared `LightingData.asset`, four `_comp_light.exr` lightmaps, and four `_comp_dir.png` directional lightmaps are committed.
- Spring `SeasonSun` is Mixed and contributes GI; the four Cell / Environment mesh sets contain 17 GI contributors. Each Cell Lighting scene has a Point fill and no Directional light.
- SceneResourceMap directly references the two companion `SceneResource` assets; their Scene `.unity` assets have Local Addressables entries. Existing comparable Cell / Environment `.asset` resources are likewise not individually listed in the group.
- Cell Lighting unload retained the authored spring sun / scene brightness in the previously recorded Editor observation. That observation did not check runtime fog / ambient.

## Verification recorded during Phase B remediation

- Limited EditMode filter: 34 passed, 0 failed. Raw log and XML are in this evidence bundle. Full EditMode regression and Play Mode manual verification were not run.
- `pwsh tools/contract-audit.ps1`: exit 0.
- `pwsh tools/docs-audit.ps1`: exit 0.
- `(4,2)` to east `(5,2)` lightmap seam visual check is not recorded.
