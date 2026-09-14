# CD0 follow-up review of Grok finding fixes

- Date: 2026-09-14
- Reviewer: Cursor Grok 4.6 (xAI), same session lineage as G1–G10. **独立性制約あり**（指摘の提出者と同一のため盲検ではない）。
- Implementation head: `bfc7c677e1c470d58797fb38a0128336f6490e79`
- Prior reviewed head: `189bc21086aaf96d77f64c017b4ebf7ad4301876`

## Adoption check

| ID | claimed | verified |
|---|---|---|
| G1 | exact `artifacts/cd0/player-host` name chain | Yes. `FindPlayerHostRoot` + test rejects `player-host-evil` and `other/cd0/player-host`. |
| G2 | Player output and previous report are host descendants | Yes. `RequireContainedPath` uses `IsContained` and rejects host-root equality. |
| G3 | Content output/quarantine under `artifacts/cd0/content` | Yes for the main-repo layout (`unity/` + `../artifacts/cd0/content`). Quarantine destination is checked separately. |
| G4 | rejected; A3 amendment 2 | Agree. `artifacts/cd0-phase-a/A3-frozen.md` item 2 says Tests.Editor references Runtime and Editor. B-result now records the reason. Living HANDOFF §3 still has A1 wording. |
| G5 | any valid registered CD blocks rebuild | Yes. Isolated and incremental share the same check. |
| cleanup traversal / host equality | extra audit fixes | Yes. `NormalizeFixtureAssetPath` canonicalizes, rejects `..`, rejects fixture root. |
| G6–G8 | remain HOLD | Agree. Native still unrun. |
| G9 | constraint, no code change | Agree. |
| G10 | test name only | Agree. Name still unchanged; no behavioral claim. |

Existing evidence XML: CD0 10/10, full EditMode 689/689 at this head. This session did not re-run Unity. Hard gates (nullable/record/Delay/Runtime UnityEditor) rechecked: 0.

## New residual

**R1 medium — E7 host layout vs G2/G3 composition.** Content menu paths are `../artifacts/cd0/content/...`, which is correct when ProjectRoot is the main `unity/` folder. `FindPlayerHostRoot` accepts any project *below* `artifacts/cd0/player-host`.

- Host = `artifacts/cd0/player-host` with Assets at the top (HANDOFF wording): content resolves to `artifacts/cd0/artifacts/cd0/content/...`, which is **outside** the host. G2 then refuses that report as `previousBuildReportDirectory`.
- Host = `artifacts/cd0/player-host/unity` (the unit test shape): content resolves inside the host, so G2 and G3 compose.

E7 says the isolated host itself does the Content build and then passes that report to Player. That only works if the host layout is the `unity/` child, or if host-side output paths are different from the main-repo relative paths. This is not a regression of the prefix bug; it is a new interaction of G2 with the planned host.

## Non-blocking

- Strict-descendant predicate is copied in Player / Build / Cleanup. Quarantine *source* still allows root equality via `IsContained` (destination check still fails closed for `content.failed-*`).
- G2/G3 strict descendant of content/player *output* is not unit-tested; only host-chain and fixture-path tests exist.
- CD0 overall remains **HOLD / inconclusive** until E0–E8 native evidence exists.

No implementation change in this review record.
