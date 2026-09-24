# S-4c Phase C runtime follow-up

- Date: 2026-09-25 (JST)
- Implementation under review: commit `87a1cd297a0b96bb9743b3e47654a27130ebcffe`; current review-record HEAD before this follow-up: `d550832b934910b3f40b084e2dd09058298d3824`.
- Editor: Unity `6000.6.0f1`, project `D:/repositories/unity/SampleGameForOneStarMakerFramework/unity`.
- The user updated `com.unity.pipeline` from `0.4.0-exp.1` to `0.7.0-exp.1` in `unity/Packages/manifest.json` and `packages-lock.json`. These are uncommitted user changes, recorded in `pipeline-package-overlay.diff`, and excluded from the review-record commit.
- Content Directory remained fail-closed. No `content:runtimeMode` default or startup contract was changed. Spring Full content was rebuilt through the project menu with revision `20260924T141708718Z-8e6155e6a2a449128189a1c1a1d28d7e`; the prepared revision was applied through the existing Content Delivery validation flow.
- Pipeline `editor_status`, `runtime_status`, `list_open_scenes`, `console_status`, `eval_file`, and `simulate_key` worked with Pipeline `0.7.0-exp.1`. Title → Home reached the running game. No S-4c source implementation was edited.

## Direct runtime snapshots

- At the 14:53 UTC snapshot, Play was active and 25 Scenes were open. `Spring_Cell_5_2`, `Spring_Environment_5_2`, and `Spring_Lighting_5_2` were among them. Player position was `(1319.15, 4.48, 984.51)` (grid cell `(5,3)`).
- While `Spring_Cell_5_2` was loaded, runtime inspection reported `LightmapSettings.lightmaps.Length=2`, five MeshRenderers, and `Ground.lightmapIndex=0` with a non-zero scale/offset. This proves a baked lightmap was assigned while that Cell was resident; this snapshot predates the later user-reported unload of both target Cells.
- Runtime `RenderSettings` at that snapshot and again after both target Cells were absent: fog enabled, `ExponentialSquared`, color `(0.750, 0.820, 0.880, 1.000)`, density `0.0015`; ambient mode `Flat`, sky `(0.180, 0.220, 0.280, 1.000)`; `RenderSettings.sun=SeasonSun`, rotation `(15,330,0)`, color `(1.000,0.850,0.700,1.000)`, intensity `0.8`. These match the frozen Spring preset.
- The user reported: “今両方とも訪れてそのあとUnload状態にした” (`Spring_Cell_4_2` and `_5_2` were both visited and then unloaded). A later live snapshot showed 24 Scenes with `Spring_Lighting` still loaded and neither target Cell nor its Cell Lighting companion in the loaded Scene list, while the Spring sun/fog/ambient values above remained active.
- User confirmation of a post-unload return and baked-floor reload was not received. An attempted Pipeline `FlyController.Teleport` probe did not hold a stable position and is not treated as evidence. Temporary probe scripts are retained in this bundle for traceability; they were not project source edits.
- Therefore the post-unload re-entry requirement remains unverified even though pre-unload lightmap assignment and Spring environment persistence during the unloaded snapshot were observed. The previously human-verified seam and Cell Lighting close observations in HANDOFF §6 remain accepted and are not re-requested.

## Other results

- Full EditMode regression: `pwsh tools/run-tests.ps1`, Filter empty, Unity Editor closed, Unity `6000.6.0f1`; 909 passed, 0 failed, 0 skipped; Unity / runner exit 0. XML duration `682.2871346` seconds. Per-assembly totals are recorded in `all-editmode-results.xml`.
- `pwsh tools/contract-audit.ps1`: exit 0, no machine-checkable contract violations.
- `pwsh tools/docs-audit.ps1`: exit 0 before this record update; rerun after adding this record and preserve final output.
- Required SampleGame `RenderSettings` grep and Framework season-term grep: each returned no matches (ripgrep exit 1 for no matches); exact commands and outcomes are recorded in the companion grep files.
- Unity test import reserialized `PC_RPAsset.asset`; its generated diff was saved as `PC_RPAsset-test-side-effect.diff`, then the asset was restored to HEAD. The original `unity/UserSettings/OSMContentDelivery.json` was restored from the captured backup after Editor shutdown.
- No C' review was started.

## Decision

**Phase C: 保留（必須 runtime 証拠不足）。GO / NO-GO は確定しない。** The structural review, regression, audits, live Spring preset values, target Cell unload, and pre-unload baked-lightmap assignment are evidenced. The required return after unload with baked floor reloaded is still missing. No current-slice implementation defect was established by this follow-up; the remaining item is an observation gap. C' remains unstarted.
