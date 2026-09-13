# U66 Phase C evidence bundle

- generated: 2026-09-13 JST
- implementation base: `3244f3635c6e18fffc1bbc40d3126e6d1eee009a`
- implementation head: `4263a33ee0d5e75b67e82e6260ff556f12fd1dce`
- complete diff SHA-256: `877e6a129750de88069a02fdf35bcc1123e1ff64e81760f533459fad4ebe5a6e`
- changed paths: 11（Phase A/HANDOFF 4、C# 1、Unity generated settings/assets 3、Packages 2、ProjectVersion 1）。Scene/Prefab/asmdefの変更なし。
- structure: 661 Scenes（SampleGame 659）、12 asmdef。base→headでScene path/GUID、Prefab、SceneResource、Addressable group membership、asmdef edgeの変更なし。
- package input hash: `unity/Packages/manifest.json` SHA-256 `920CA27F4B743D75945E59DED2EC8B12CC7AFCFA137F6AABD9D6E6971A71C1F8`
- package lock hash: `unity/Packages/packages-lock.json` SHA-256 `A748CA48847A2B830E818639D793C6E400A0FCCE26FB3DA04E631E7D139C2D83`
- frozen Phase A revision: `artifacts/u66-phase-a/A3-frozen.md` SHA-256 `2B5A136B0F6EAEDEF5CE3C776950739AC5F2424E7691645EC3CB481DBA4C6D56`

## Machine checks before tests

- `pwsh tools/contract-audit.ps1`: PASS; one changed Unity C# file, no mechanically detectable contract violation.
- `pwsh tools/docs-audit.ps1`: PASS.
- manifest/lock `ConvertFrom-Json`: PASS.
- `rg enableWordWrapping unity/Assets -g *.cs`: no match.
- `git diff --check` and cached diff check: PASS.

## Unity test evidence

- command: `pwsh tools/run-tests.ps1`（filterなし、WithGraphicsなし）
- Editor: `6000.6.0f1 (f7f8ed4d1e24)`、Windows Standalone support detected
- started after confirming no `Unity` process and no Pipeline-connected instance.
- result: exit 0; total 679, passed 679, failed 0, skipped 0; duration 4.7 minutes on the final fixed head.
- raw XML: `TestResults/results-all-20260913-084215.xml`; SHA-256 `39226C87BCD83D15A8BDB72DCE5C55F0CC9CA08B6A8EF39F9D5E64C8A27D0DE0`
- raw log: `TestResults/unity-all-20260913-084215.log`; SHA-256 `D8C4EBED3C7D67E3506E927A35D19F2A73B1573F6496C2D6FE57AE976D9E5E18`
- compile errors in final raw log: none; log records `Test run completed. Exiting with code 0`.
- required-name coverage counts: AssetManagement 39、UpdateSystem 61、Cinemachine 19、SceneVariantResolver 7、InvalidCompanion 1、SeasonCandidateSelection 5、SessionSeasonController 8、PlayerWorldReadySequence 3、Variant 31、WorldAuthoring 72、Workspace 22、Transaction 20、Recovery 33。
- stabilization rerun: the first run exposed the required Addressables serialization field. After committing it as the new implementation head, the final run above left the tracked worktree clean.

## Live Editor gates (Unity 6000.6.0f1)

- connected to the already-open Editor through Pipeline; project path and exact Editor revision matched the frozen inputs. The agent did not launch Unity.
- Full Play from `Assets/Scenes/SampleScene.unity`: two enter/stop cycles completed. `UIScene` loaded additively, Console error count was 0, and stop returned to the single SampleScene without a residual loaded UIScene.
- Whitebox Play: temporarily overlaid `assets:sceneVariant=Whitebox`, observed one enter/stop cycle with SampleScene + UIScene and 0 Console errors, then restored `app-config.json` byte-for-byte. Final SHA-256: `6A5BC86EFC8395A95EFCBFB46B1064228863E48518BA1AB7F0449C0DEBB2E338`.
- representative World Workspace: `OneStarMaker/World Workspace/Open Window` executed successfully; Console errors after opening: 0. No generation/save operation was performed.
- Full Play performance snapshot: 30 draw calls / 30 SetPass / 1,709 triangles / 5,127 vertices; CPU frame 11.6922 ms, GPU 0.096 ms, main thread 4.0856 ms. There is no 6.5 raw baseline, so this is observational only.
- runtime warning: Play emitted repeated `There are 2 audio listeners in the scene` messages during loading, although a post-load query found one active `AudioListener` at `/Main Camera`. This is retained as an open runtime finding, not attributed to the 6.6 delta.

## Build gates

- fixed command/result extract: `artifacts/u66-phase-c/live-editor-build-log.md` (hash in manifest). Frozen profiles: Production SHA-256 `41C4E50FCE25E59D12E68EC13E86195382A05DD34004D016095F1EB979B10B0C`; WorldWhitebox SHA-256 `EAF8990D65524B55500A01C33F2D08A091BCD5EFB3E7B6C1B38880AAC6724350`.
- Production packed Addressables build: **FAIL**. `VariantFilteringBuildScript` returned `Build aborted due to whitelist validation errors` before content output.
- Direct whitelist diagnostics showed the same missing-payload set for Production and WorldWhitebox: `HomeScene`, `InGameScene`, `InGameSession`, `OutGameSession`, and `Season_Autumn/Spring/Summer/Winter` each had no payload matching the profile whitelist.
- Production Windows Player build through `OneStarMaker/Build/Build Player (Active Variant)`: **FAIL** in `AddressablesPlayerBuildProcessor.PrepareForBuild` for the same whitelist errors; no executable was produced. `VariantPlayerBuild` restored `app-config.json` in `finally`.
- WorldWhitebox packed content and Player build were not redundantly invoked after the direct diagnostic proved the identical blocking set. No Player run was possible because neither profile can produce a Player.
- The build attempt temporarily selected the tracked filtering builder/profile and generated `ProfileDataSourceSettings.asset`; both changes were removed/restored after evidence capture. The final tracked worktree was clean before documentation edits.

## Deferred / unevidenced

- no base-bound 6.5 raw baseline, so before/after behavior and timing comparison is unavailable.
- SampleScene does not reach Spring streaming in the current runtime path. Per the human scope decision on 2026-09-13, Season scenes and Season cells are deferred to later work and are not repaired or claimed compatible by U66.
- direct input/camera/UI interaction, telemetry/DebugSocket payload inspection, before/after screenshot comparison, mobile Player, restoration-failure injection, and a Player runtime launch remain unevidenced.
- Whiteboxの2回目の完全なenter/stop周期、Workspaceのclose/dirty-cancel、domain reload無効設定の個別証拠化も未実施。Phase CはFAILを確定できた時点で追加gateを打ち切っており、全gate実施済みとは扱わない。

## Phase C result

**FAIL (review completed; remaining gates stopped after blocker):** compile/EditMode, structural compatibility, basic Full/Whitebox Play entry, and Workspace opening passed, but the frozen Production/WorldWhitebox content contract cannot build Addressables and therefore cannot build or run a Windows Player. The blocker appears to be pre-existing project content/profile completeness; U66 does not repair it. The migration PR must not claim Player/Season compatibility or claim that every frozen gate ran.
