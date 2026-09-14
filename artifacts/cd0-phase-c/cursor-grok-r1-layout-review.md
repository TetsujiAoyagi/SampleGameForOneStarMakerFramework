# CD0 review of R1 HANDOFF layout contract

- Date: 2026-09-14
- Reviewer: Cursor Grok 4.6 (xAI). Same finding author. **独立性制約あり**.
- Docs commit: `4d1be33ed4291519a3f8dfcec64817e70fc3a7fc`
- Implementation head (unchanged): `bfc7c677e1c470d58797fb38a0128336f6490e79`

## Result

**R1 is closed as an E7 procedure contract.** The living HANDOFF now fixes:

- owner boundary: `artifacts/cd0/player-host/`
- Unity project root: `artifacts/cd0/player-host/unity/`
- host Content via `../artifacts/cd0/content` → `player-host/artifacts/cd0/content/`
- preflight: record project root, Content output, report, Player output; last three must be strict descendants of `player-host/`

That layout composes with the existing G2/G3 guards. `FindPlayerHostRoot(.../player-host/unity)` already matches the unit test. Implementation bytes were not changed, as claimed.

CD0 overall remains **HOLD / inconclusive** (native E0–E8 unrun).

## Residual (low, not reopening R1)

`FindPlayerHostRoot` still accepts a project whose root *is* `player-host` (no `unity/` child). That mistaken layout would again send Content output outside the owner boundary. E7 preflight of the four canonical paths is the backstop; the code does not encode the `unity/` leaf. A1 snapshot still has the pre-R1 one-line location; living HANDOFF §4/E7 is the operator contract.
