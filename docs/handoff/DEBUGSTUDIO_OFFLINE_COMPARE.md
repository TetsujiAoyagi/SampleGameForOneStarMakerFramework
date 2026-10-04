# DebugStudio offline comparison — A0/A1 r2 candidate

## 0. Metadata

- type: `slice`; status: `A2 integrated — pending tooling proof and human A3`; risk: `normal`; owner: dot coordination.
- branch: `codex/debugstudio-offline-compare`; base: `develop` at `2c29c99806788406551affba6cc795e67e614748` (remote rechecked 2026-10-04 UTC); implementation head: not created.
- created: 2026-10-04; expires: 2026-11-04; harvest: `tools/DebugStudio/src/DebugStudio.Cli/README.md`, then remove this HANDOFF at D.
- Ordinary tracked HANDOFF; no Artifacts/Harness migration. Freeze A snapshot path/time/hash at A3. B/evidence/blind-bundle metadata and phase identities are pending, never inferred complete.

## 1. A0: question and boundaries

User requested DebugStudio offline comparison. CLI currently only sends live commands. Export owns normalized NDJSON models/serialization; v3 exposes sessionId, optional producerSequence, kind/name, elapsedMs, payload stage/target and tags. Existing queries preserve startup stages/scene targets and show sample counts. Sequence crosses logs and telemetry; its gaps do not prove telemetry loss.

**Question/minimum to advance:** Can two explicitly selected captured runs be compared locally with correct observed summaries, honest input limitations, and unchanged send behavior? GO requires those properties, the scoped final tests/audits and C/C' evidence on one fixed head. Stop when this question is answered; unmet required conditions are NO-GO.

**Out of scope:** Unity, ScriptSystem/mainline, producer/protocol/Export-model/serializer, WPF/App, Server, Elastic, CI/workflow changes; networking on compare; automatic regression verdicts/thresholds, frame p95, significance, completeness/loss estimates, environment equivalence, run auto-selection, database/GUI. Later capture completeness, environment matching and extra metrics belong to prospective `DebugStudio offline analysis follow-ons`; do not create or implement that program here. No human manual testing is implicitly assigned.

## 2. A1: acceptance contract

### Invocation and results

`debugstudio-cli compare --input <file.ndjson> [--input <other.ndjson> ...] --baseline-session <id> --candidate-session <id> [--format text|json]`

- Explicit files, two nonempty distinct ordinal/case-sensitive IDs; no trimming IDs, directories, implicit file discovery, stdin or URLs. Only input is repeatable. Reject repeated singleton/unknown options, missing values and positional extras. Default format is text; compare help exits 0.
- Program routes compare separately; preserve the existing public send parser/result contract, defaults, aliases, output, networking and exit behavior. Global help adds the synopsis only.
- Summary stdout, diagnostics stderr. Exit 0 means completed observed comparison (warnings allowed), 2 means usage or invalid/ambiguous input, 1 means file-open/read failure. Fatal failures publish no partial summary. Read all input first; never mutate source files.
- Text and JSON present the same report: selected IDs, input/selected/skipped/duplicate/coverage counts, startup and scene groups, Bottleneck counts and diagnostics. JSON has `reportVersion: 1`, finite numeric values or null, no NaN/Infinity; text uses `n/a` for unavailable values. Output is invariant-culture, stable ordinal group order; text escapes control characters in IDs/keys/paths. Do not echo malformed full lines. No reporting framework/new packages.
- Successful-report accounting is disjoint: nonblank JSON input rows = selected unique rows after deduplication + exact duplicate rows + skipped serviceStatus + unassigned telemetry + other-session telemetry. Blank rows are separate. Selected unique is baseline plus candidate; absent-sequence rows remain retained and cannot be proven unique. Metric/coverage exclusions are subsets of selected unique, not additional skipped-input categories. A mixed fixture must assert the sum.

### Reader and observed metrics

- One normalized JSON object per nonblank UTF-8 line; accept LF/CRLF and an initial BOM, count ignored blanks. Reject invalid UTF-8 bytes strictly, never replacement-decode them. Encoding errors identify the file and an accurate 1-based line or byte offset; buffered read-ahead must not produce a guessed line number. Deserialize the existing Export models with normalized camelCase fields. Unknown extension fields are allowed. Required envelope fields are `@timestamp` string, `timestampUnixTimeMilliseconds` integer and nonempty `stream`; do not reinterpret bulk/Kibana NDJSON. Recognized serviceStatus rows are counted/skipped.
- Telemetry requires schemaVersion 3, nonempty name and recognized kind (span/sample/event). Missing sessionId is unassigned with warning; other sessions are counted/skipped. Validate JSON/envelope before skipping. Each selected run must have at least one valid telemetry row, otherwise input error naming the missing ID.
- Selected AppStartup spans group by exact nonempty payload.stage; SceneLoad spans by exact nonempty payload.targetIdentity. No invented expected stage or flat-field fallback. Missing key/elapsedMs: exclude from that metric, count and warn. Include all valid observed durations regardless of isSuccess, explicitly label all outcomes included.
- Report/docs must identify AppStartup values as whole-span durations grouped by their stage label, not individual step durations: BeforeSceneLoad/AfterSceneLoad label the whole span and failure stage is the last attempted step. SceneLoad summarizes observed spans/calls, including already-active short-circuit calls; it does not establish completed cold-load durations.
- Each union key shows each run's count, median and max ms, and candidate-minus-baseline count/median/max deltas. Median is the exact middle value or arithmetic midpoint (overflow-safe). Missing side: count 0, null median/max and duration deltas; never zero ms. If both runs lack a metric, say no observations.
- Bottleneck is observed telemetry-row count per selected run, including samples/events. Count once if tags contains exact Bottleneck OR tagBits has the existing Contracts Bottleneck bit. If both exist and disagree, warn/use union. Missing both means unknown tag coverage, not proof of no bottleneck. Show count delta; no rate or inference about unobserved frames.

### Invalid/duplicate policy

- Fatal input: invalid UTF-8, malformed/truncated/nonobject JSON, duplicate JSON object property names, missing/wrong-type required fields, unknown stream, unsupported telemetry schema/kind, selected negative/nonfinite duration or present nonpositive producerSequence. Identify file, 1-based line and concise reason (encoding failures may use an accurate byte offset as above). Optional null grouping/duration/sequence follows the missing-data rules, not silent zero defaults.
- Identity is positive `(sessionId, producerSequence)` for selected telemetry only. Exact duplicates count once and expose skipped-duplicate count/locations. Exact means the same typed Export-model fields, treating tags as an ordinal set; JSON property order/whitespace and unknown extensions do not matter. Conflicting same identity is fatal and identifies both locations; never choose a winner. This applies across overlapping/repeated files. Same sequence in different sessions is not duplicate.
- Strict typed equality deliberately includes session attributes: a rolling capture and later manual export of the same identity may conflict if attributes were enriched later. Document this limitation; do not silently merge enriched records or choose which capture wins.
- Absent producerSequence is allowed, counted and warned as incomplete deduplication coverage; do not invent an identity from timestamp/name/spanId. No gap checks. Missing metrics/tags/sequence are warnings; missing selected runs are errors.

## 3. Responsibility/ownership map

Only these paths may change. All new analysis types are internal in `DebugStudio.Cli.Offline`; CLI project grants test internals access and adds CLI → Export reference. No other dependency/API changes. State/streams belong to one invocation; reader disposes streams; metric policy is pure and testable without Unity. Offline separates local-file analysis from live control.

| Path under tools/DebugStudio (except HANDOFF) | Responsibility and boundary | Current → expected lines |
|---|---|---|
| src/DebugStudio.Cli/Program.cs | Dispatch only | 62 → 80 |
| src/DebugStudio.Cli/CliArgumentParser.cs | Global usage addition only, preserve send | 164 → 175 |
| src/DebugStudio.Cli/DebugStudio.Cli.csproj | Export reference/test visibility | 16 → 25 |
| src/DebugStudio.Cli/Offline/CompareArguments.cs | Arguments/options; no I/O | 0 → 120 |
| src/DebugStudio.Cli/Offline/TelemetryNdjsonReader.cs | File/JSON admission, provenance, identity/coverage diagnostics; Export + System.Text.Json | 0 → 280 |
| src/DebugStudio.Cli/Offline/RunComparison.cs | Pure grouping/statistics and report data; no I/O | 0 → 180 |
| src/DebugStudio.Cli/Offline/ComparisonReportWriter.cs | Text/JSON presentation via TextWriter | 0 → 130 |
| src/DebugStudio.Cli/Offline/CompareCommand.cs | Parse/read/compare/write orchestration and exit policy | 0 → 80 |
| tests/DebugStudio.Cli.Tests/OfflineCompareTests.cs | Parser/metrics/output unit tests | 0 → 240 |
| tests/DebugStudio.Cli.Tests/TelemetryNdjsonReaderTests.cs | Temp-file reader and command integration | 0 → 280 |
| tests/DebugStudio.Cli.Tests/CliArgumentParserTests.cs | Legacy send/help regression checks | 69 → 120 |
| src/DebugStudio.Cli/README.md | Current usage/contracts/examples | 0 → 100 |
| docs/handoff/DEBUGSTUDIO_OFFLINE_COMPARE.md (repo root) | Bounded phase/evidence ledger | this packet |

New files naturally exceed 50% growth; each has one change reason/invocation lifetime/test boundary. Reader keeps admission/provenance/deduplication together because they determine one row's acceptance. No 500-line file or generic Helper/Manager is planned. A pure report-model file may split from RunComparison without new responsibility; other scope/responsibility/API/owner/rejection-contract changes return to A. No extra tests project/solution change.

## 4. Implementation and verification

A2 independently reviews this same packet, including architecture; human A3 approves integrated findings/scope before any implementation. Fresh B session implements reader/pure metrics, presentation/dispatch, then tests/docs. Existing outside-scope files and other worktrees remain read-only. B adaptations within the frozen contract need no redesign; new contracts require A revision. User approved small visible push checkpoints; parent owns plan-only draft PR publication now and later authorized checkpoints. Implementation still waits for human A3; merge is not authorized.

- Discovery/fix filter: CLI test project, Release, `FullyQualifiedName~OfflineCompare|FullyQualifiedName~TelemetryNdjsonReader|FullyQualifiedName~CliArgumentParser`.
- Final candidate: all tests in existing DebugStudio.Cli.Tests and DebugStudio.Export.Tests projects, Release/no filter, TRX + raw logs; `dotnet build tools/DebugStudio/DebugStudio.Linux.slnf -c Release`; `pwsh tools/contract-audit.ps1 -BaseRef 2c29c99806788406551affba6cc795e67e614748`; `pwsh tools/docs-audit.ps1`; `git diff --check`. Record exact head/commands/test names/counts; zero executed is not pass.
- Cover explicit/mixed/missing/same IDs and disjoint count sum, unrelated sessions, whole-startup-span/scene-call labels, target isolation, sparse/odd/even/zero/large finite samples, one-sided/null deltas, all-outcome inclusion; malformed/envelope/schema/kind/property-duplicate failures; exact/conflicting identity duplicates across files including enriched session attributes; sequence absent/gapped/cross-session; missing key/duration; negative/nonfinite duration; tag union/conflict/coverage; BOM/blanks/CRLF and invalid UTF-8 with accurate location, I/O failure, culture/order/escaping/JSON nulls, fatal-after-valid no partial summary and source preservation. No Task.Delay/Thread.Sleep.
- Use existing NdjsonTelemetryExportWriter for valid fixtures, proving writer → reader compatibility. Built-CLI smoke on temporary fixtures checks stdout/stderr/exits without Unity/Server/Elastic. Unit tests directly test the pure logic; command tests cover orchestration.
- **Explicit full EditMode/WPF exclusion:** .NET-only consumer addition changes no Unity code/assets/protocol/generation. Substitute CLI+Export suites/Linux build/audits. Unity Editor/PlayMode/Addressables/Player and WPF visual/runtime tests are not acceptance conditions. Existing Windows CI, if publication is approved, is additional coverage; do not modify CI.
- Tooling route: isolated cloud Linux checkout exists. Official .NET 8 x64 download HEAD returned 200 (216939894 bytes), NuGet responds; coordinator verified Microsoft workspace install-dir and official PowerShell archive routes. User approved workspace-only official .NET 8/PowerShell setup; a separate worker is installing and checking baseline CLI/audits. Execution route is NOT YET PROVED. A3 may freeze only after version/restore/build/audit proof arrives, or the human explicitly approves a first verification checkpoint with an honest unconfirmed route. If blocked, report NOT RUN/NO-GO and resolve the environment, never switch to the unauthorized user computer or invent a human test obligation.
- Evidence outside Git: immutable A snapshot, B result, complete fixed base..head diff, raw checks/TRX; manifest contains paths/IDs/time/SHA-256. C/C' verify readable hash-matched copies. Only locator ledger goes in HANDOFF. Blind bundle excludes C findings; no big raw payload in product history. Parent arranges publication/evidence retention.

## 5. Review/freeze ledger and pending phases

- A0/A1: planning agent, OpenAI inherited model (coordinator records exact identity). Independent A2 on r1: `gpt-6-astra` architecture and `gpt-6-sol` contract, both approve architecture. Their concise findings are integrated below; human A3 approval remains pending. Workspace tool setup and parent-owned draft checkpoints are user-approved; implementation/merge are not. A3 exceptions: none.
- A2 adoption ledger: adopt whole-span/observed-call semantics to avoid unsupported duration claims; adopt disjoint row accounting to avoid double-counted totals; adopt strict UTF-8/accurate locations to avoid repaired or mislocated input; retain strict typed-duplicate conflict policy and document later attribute enrichment so ambiguity is deliberate. Tooling route stays unproved until the setup worker's evidence or explicit human first-checkpoint decision. No new responsibilities or other acceptance expansion; no findings rejected or deferred.
- B: fresh session required; implementation/result/head/deviations/unexecuted checks pending.
- C: fresh session/model different from B; discovery and judgment evidence/structure/findings/results pending.
- C': fresh blind session/model different from B and C, or explicit human audit; reserved by coordinator. Actual independence, scope, residual risk and verdict pending. Do not label an unperformed audit complete.
- D: human reconciliation/merge judgment, CLI README harvest and HANDOFF deletion pending.
