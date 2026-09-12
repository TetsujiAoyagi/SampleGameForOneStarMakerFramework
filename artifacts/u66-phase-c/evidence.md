# U66 Phase C evidence bundle

- generated: 2026-09-13 JST
- implementation base: `3244f3635c6e18fffc1bbc40d3126e6d1eee009a`
- implementation head: `4263a33ee0d5e75b67e82e6260ff556f12fd1dce`
- complete diff SHA-256: `877e6a129750de88069a02fdf35bcc1123e1ff64e81760f533459fad4ebe5a6e`
- changed paths: 11（Phase A/HANDOFF 4、C# 1、Unity generated settings/assets 3、Packages 2、ProjectVersion 1）。Scene/Prefab/asmdefの変更なし。
- structure: 661 Scenes（SampleGame 659）、12 asmdef。base→headでScene path/GUID、Prefab、SceneResource、Addressable group membership、asmdef edgeの変更なし。
- package input hash: `unity/Packages/manifest.json` SHA-256 `920CA27F4B743D75945E59DED2EC8B12CC7AFCFA137F6AABD9D6E6971A71C1F8`
- package lock hash: `unity/Packages/packages-lock.json` SHA-256 `A748CA48847A2B830E818639D793C6E400A0FCCE26FB3DA04E631E7D139C2D83`
- Phase A snapshot hash: `artifacts/u66-phase-a/A1-snapshot.md` SHA-256 `194392B0046252C6A14EB239C1C97E1B36AFE93FB5716497066ABAD002C286FC`

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

## Not evidenced / manual gates

- no base-bound 6.5 raw baseline, so before/after behavior and timing comparison is unavailable.
- no live Full/Whitebox Play twice, Spring streaming traversal, input/camera/UI/telemetry/DebugSocket observation.
- no representative World Workspace open/close/cancel observation.
- no before/after screenshots, graphics test, mobile Player, Addressables packed content build, Windows Full/Whitebox Player build/run or restoration-failure injection.
- T8 build fixture/profile and Windows backend were not frozen in A3; the HANDOFF itself says to return to A rather than invent them.

This bundle supports compile/EditMode and structural compatibility only. It does not support a full U66 migration PASS.
