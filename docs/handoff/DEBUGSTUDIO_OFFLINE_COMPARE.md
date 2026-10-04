# DebugStudio offline comparison — A3 frozen r3

## 0. Metadata

- type: `slice`; status: `Phase B candidate; final C/C' pending`; risk: `normal`; owner: dot coordination.
- branch: `codex/debugstudio-offline-compare`; base: `develop` at `2c29c99806788406551affba6cc795e67e614748` (remote rechecked 2026-10-04 UTC); implementation head: the fixed candidate commit identified by the current evidence manifest; this mutable ledger does not self-identify its enclosing commit.
- created: 2026-10-04; expires: 2026-11-04; harvest: `tools/DebugStudio/src/DebugStudio.Cli/README.md`, then remove this HANDOFF at D.
- Ordinary tracked HANDOFF; no Artifacts/Harness migration. The frozen A3 source is the Git blob named below. Candidate execution evidence is stored outside Git and fixed before final C/C'.

## 1. A0: question and boundaries

User requested DebugStudio offline comparison. CLI currently only sends live commands. Export owns normalized NDJSON models/serialization; v3 exposes sessionId, optional producerSequence, kind/name, elapsedMs, payload stage/target and tags. Existing queries preserve startup stages/scene targets and show sample counts. Sequence crosses logs and telemetry; its gaps do not prove telemetry loss.

**Question/minimum to advance:** Can two explicitly selected captured runs be compared locally with correct observed summaries, honest input limitations, and unchanged send behavior? GO requires those properties, the scoped final tests/audits and C/C' evidence on one fixed head. Stop when this question is answered; unmet required conditions are NO-GO.

**Out of scope:** Unity, ScriptSystem/mainline, producer/protocol/Export-model/serializer, WPF/App, Server, Elastic, CI/workflow changes; networking on compare; automatic regression verdicts/thresholds, frame p95, significance, completeness/loss estimates, environment equivalence, run auto-selection, database/GUI. Later capture completeness, environment matching and extra metrics belong to prospective `DebugStudio offline analysis follow-ons`; do not create or implement that program here. No human manual testing is implicitly assigned.

## 2. A1: acceptance contract

### Invocation and results

`debugstudio-cli compare --input <file.ndjson> [--input <other.ndjson> ...] --baseline-session <id> --candidate-session <id> [--format text|json]`

- Explicit files, two nonempty distinct ordinal/case-sensitive IDs; no trimming IDs, directories, implicit file discovery, stdin or URLs. Only input is repeatable. Reject repeated singleton/unknown options, missing values and positional extras. Default format is text; compare help exits 0.
- Program recognizes compare with OrdinalIgnoreCase, matching send dispatch, and routes it separately; preserve the existing public send parser/result contract, defaults, aliases, output, networking and exit behavior. Global help adds the synopsis only.
- Summary stdout, diagnostics stderr. Exit 0 means completed observed comparison (warnings allowed), 2 means usage or invalid/ambiguous input, 1 means file-open/read failure. Fatal failures publish only the fatal diagnostic, with no partial summary or accumulated warnings. Usage/missing-session/open errors need no invented line; JSON errors retain exact file/line, encoding errors the accurate file/line or byte offset, and duplicate conflicts both locations. Read all input first; never mutate source files.
- Text and JSON present the same report: selected IDs, input/selected/skipped/duplicate/coverage counts, startup and scene groups, Bottleneck counts and diagnostics. JSON has `reportVersion: 1`, finite numeric values or null, no NaN/Infinity; text uses `n/a` for unavailable values. Output is invariant-culture, StringComparer.Ordinal ascending union-key order, independently for startup and scene groups; text escapes control characters in IDs/keys/paths. Do not echo malformed full lines. No reporting framework/new packages.
- Successful-report accounting is disjoint: nonblank JSON input rows = selected unique rows after deduplication + exact duplicate rows + skipped serviceStatus + unassigned telemetry + other-session telemetry. Blank rows are separate. Selected unique is baseline plus candidate; absent-sequence rows remain retained and cannot be proven unique. Metric/coverage exclusions are subsets of selected unique, not additional skipped-input categories. A mixed fixture must assert the sum.

### Reader and observed metrics

- One normalized JSON object per nonblank UTF-8 line; accept LF/CRLF and an initial BOM, count ignored blanks. Reject invalid UTF-8 bytes strictly, never replacement-decode them. Encoding errors identify the file and an accurate 1-based line or byte offset; buffered read-ahead must not produce a guessed line number. Deserialize the existing Export models with normalized camelCase fields. Unknown extension fields are allowed. Required envelope fields are `@timestamp` string, `timestampUnixTimeMilliseconds` integer and nonempty `stream`; do not reinterpret bulk/Kibana NDJSON. Recognized serviceStatus rows are counted/skipped.
- Stream, kind and telemetry-name classification uses exact ordinal matches. Telemetry requires schemaVersion 3, nonempty name and recognized kind (span/sample/event). Missing sessionId is unassigned with warning; other sessions are counted/skipped. Validate JSON/envelope before skipping. Each selected run must have at least one valid telemetry row, otherwise input error naming the missing ID.
- Selected AppStartup spans group by exact nonempty payload.stage; SceneLoad spans by exact nonempty payload.targetIdentity. No invented expected stage or flat-field fallback. Missing key/elapsedMs: exclude from that metric, count and warn. Include all valid observed durations regardless of isSuccess, explicitly label all outcomes included.
- Report/docs must identify AppStartup values as whole-span durations grouped by their stage label, not individual step durations: BeforeSceneLoad/AfterSceneLoad label the whole span and failure stage is the last attempted step. SceneLoad summarizes observed spans/calls, including already-active short-circuit calls; it does not establish completed cold-load durations.
- The population for count/median/max consists only of selected spans with the relevant nonempty key and finite nonnegative elapsedMs; missing observations are counted separately, and the union contains only keys present in that population. Each union key shows each run's count, median and max ms, and candidate-minus-baseline count/median/max deltas. Median is the exact middle value or arithmetic midpoint (overflow-safe). Missing side: count 0, null median/max and duration deltas; never zero ms. If both runs lack a metric, say no observations.
- Bottleneck interpretation is pure policy in RunComparison. It is observed telemetry-row count per selected run, including samples/events. Count once if tags contains exact Bottleneck OR tagBits has the existing Contracts Bottleneck bit. If both exist and disagree, warn/use union. Missing both means unknown tag coverage, not proof of no bottleneck. Show count delta; no rate or inference about unobserved frames.

### Invalid/duplicate policy

- Fatal input: invalid UTF-8, malformed/truncated/nonobject JSON, duplicate JSON object property names, missing/wrong-type required fields, unknown stream, unsupported telemetry schema/kind, selected negative/nonfinite duration or present nonpositive producerSequence. Identify file, 1-based line and concise reason (encoding failures may use an accurate byte offset as above). Optional null grouping/duration/sequence follows the missing-data rules, not silent zero defaults.
- Identity is positive `(sessionId, producerSequence)` for selected telemetry only. Exact duplicates count once and expose skipped-duplicate count/locations. Exact means the same typed Export-model fields; null tags is distinct from an empty array, and present tag arrays are ordinal sets; JSON property order/whitespace and unknown extensions do not matter. Conflicting same identity is fatal and identifies both locations; never choose a winner. This applies across overlapping/repeated files. Same sequence in different sessions is not duplicate.
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
| src/DebugStudio.Cli/Offline/RunComparison.cs | Pure grouping/statistics/Bottleneck policy and report data; no I/O | 0 → 180 |
| src/DebugStudio.Cli/Offline/ComparisonReportWriter.cs | Text/JSON presentation via TextWriter | 0 → 130 |
| src/DebugStudio.Cli/Offline/CompareCommand.cs | Parse/read/compare/write orchestration and exit policy | 0 → 80 |
| tests/DebugStudio.Cli.Tests/OfflineCompareTests.cs | Parser/metrics/output unit tests | 0 → 240 |
| tests/DebugStudio.Cli.Tests/TelemetryNdjsonReaderTests.cs | Temp-file reader and command integration | 0 → 280 |
| tests/DebugStudio.Cli.Tests/CliArgumentParserTests.cs | Legacy send/help regression checks | 69 → 120 |
| src/DebugStudio.Cli/README.md | Current usage/contracts/examples | 0 → 100 |
| docs/handoff/DEBUGSTUDIO_OFFLINE_COMPARE.md (repo root) | Bounded phase/evidence ledger | this packet |

New files naturally exceed 50% growth; each has one change reason/invocation lifetime/test boundary. Reader keeps admission/provenance/deduplication together because they determine one row's acceptance. No 500-line file or generic Helper/Manager is planned. A pure report-model file may split from RunComparison without new responsibility; other scope/responsibility/API/owner/rejection-contract changes return to A. No extra tests project/solution change.

## 4. Implementation and verification

Independent A2 architecture/contract reviews and the external review clarifications below were integrated; human A3 approved proceeding on 2026-10-04 at 01:25:25 UTC. The fresh B session has implemented reader/pure metrics, presentation/dispatch, tests/docs and the local-file admission boundary. Existing outside-scope files and other worktrees remain read-only. B adaptations within the frozen contract need no redesign; new contracts require A revision. User approved small visible push checkpoints; parent owns plan-only draft PR publication now and later authorized checkpoints. Implementation remains within frozen r3. The current user request on 2026-10-04 authorizes local C/C', necessary repairs and sequential merging after successful review, including premise evaluation.

- Discovery/fix filter: CLI test project, Release, `FullyQualifiedName~OfflineCompare|FullyQualifiedName~TelemetryNdjsonReader|FullyQualifiedName~CliArgumentParser`.
- Final candidate: all tests in existing DebugStudio.Cli.Tests and DebugStudio.Export.Tests projects, Release/no filter, TRX + raw logs; `dotnet build tools/DebugStudio/DebugStudio.Linux.slnf -c Release`; `pwsh tools/contract-audit.ps1 -BaseRef 2c29c99806788406551affba6cc795e67e614748`; `pwsh tools/docs-audit.ps1`; `git diff --check`. Record exact head/commands/test names/counts; zero executed is not pass.
- Cover explicit/mixed/missing/same IDs and disjoint count sum, unrelated sessions, whole-startup-span/scene-call labels, target isolation, sparse/odd/even/zero/large finite samples, one-sided/null deltas, all-outcome inclusion; malformed/envelope/schema/kind/property-duplicate failures; exact/conflicting identity duplicates across files including enriched session attributes; sequence absent/gapped/cross-session; missing key/duration; negative/nonfinite duration; tag union/conflict/coverage; BOM/blanks/CRLF and invalid UTF-8 with accurate location, I/O failure, culture/order/escaping/JSON nulls, fatal-after-valid no partial summary and source preservation. No Task.Delay/Thread.Sleep.
- Use existing NdjsonTelemetryExportWriter for valid fixtures, proving writer → reader compatibility. Built-CLI smoke on temporary fixtures checks stdout/stderr/exits without Unity/Server/Elastic. Unit tests directly test the pure logic; command tests cover orchestration. The existing public serializer remains unchanged. A direct test → Export reference is not required: .NET 8 transitive project references were proved in the separate three-project probe.
- **Explicit full EditMode/WPF exclusion:** .NET-only consumer addition changes no Unity code/assets/protocol/generation. Substitute CLI+Export suites/Linux build/audits. Unity Editor/PlayMode/Addressables/Player and WPF visual/runtime tests are not acceptance conditions. Existing Windows CI, if publication is approved, is additional coverage; do not modify CI.
- Tooling route PROVED: official workspace-only .NET SDK 8.0.425 and PowerShell 7.6.6 archives/checksums verified; baseline CLI restore/Release build/help, exactly 4/4 parser tests and contract audit (561 files, 0 errors/warnings) passed. Setup RESULT and manifest locator are below. These are route proofs on unchanged baseline implementation, not comparison acceptance. Source `debugstudio-tools/env.sh`; use `-m:1 -nodeReuse:false`, and for builds/tests `-p:UseSharedCompilation=false --disable-build-servers`, with fresh external artifact/results directories. Single-node execution avoids sandbox MSBuild IPC failure without escalation. Existing MessagePack 3.1.4 NuGet advisories (3 high, 9 moderate) are recorded by setup and unchanged/out of scope. The original cloud execution setup is historical. The current user request authorizes local Windows execution; report the actual SDK/runtime and retain TRX/raw output. Do not claim Windows results establish Linux runtime success or invent human test duties.
- Evidence outside Git: immutable A snapshot, B result, complete fixed base..head diff, raw checks/TRX; manifest contains paths/IDs/time/SHA-256. C/C' verify readable hash-matched copies. Only locator ledger goes in HANDOFF. Blind bundle excludes C findings; no big raw payload in product history. Parent arranges publication/evidence retention.

## 5. Review/freeze ledger and pending phases

- A0/A1: planning agent, OpenAI; exact inherited model identity was not exposed and is not invented. Independent A2 on r1: `gpt-6-astra` architecture and `gpt-6-sol` contract, both approve architecture. Human A3: user approved the communicated review adoption/rejection and implementation at 2026-10-04T01:25:25Z, message `Sentinel_7ee258fdb06c8191ab40ccc7de3be53d` replying to `Sentinel_6dc03bbb66a08191bc1e4947d6c1d0d7`. Original workspace setup and draft checkpoints were approved. The current 2026-10-04 user request separately authorizes local verification and sequential merging once C/C' and premise evaluation are complete. A3 exceptions: none.
- A2 adopted: whole-span/observed-call semantics, disjoint row accounting, strict UTF-8/accurate location, and deliberate strict typed-duplicate conflict with enrichment caveat. Tooling proof is complete.
- External review adopted: count/median/max population requires key plus valid duration; union keys sort ascending Ordinal; exact stream/kind/name classification; case-insensitive compare dispatch; fatal-only diagnostics with accurate-location exceptions; null tags distinct from empty arrays; pure Bottleneck policy remains in RunComparison. Rejected mandatory direct test → Export reference: the .NET 8 three-project probe compiled/ran through a transitive reference. Do not change the public serializer. No other scope expansion.
- B: fresh `gpt-6.1-sol` session; candidate implements the frozen CLI comparison scope. Final implementation head and execution results are fixed in the external manifest.
- C: fresh `gpt-6-astra` session, different from B; discovery/judgment evidence/structure/findings/results pending.
- C': fresh blind `gpt-5.6-sol` session reserved, different from B/C. Actual independence, scope, residual risk and verdict pending; do not label an unperformed audit complete.
- D: human reconciliation/merge judgment, CLI README harvest and HANDOFF deletion pending.

## 6. Candidate implementation result

- New internal CLI components cover arguments, file admission, typed duplicate identity, pure observed metrics, text/JSON formatting and execution/exit orchestration. Existing send behavior remains a separate dispatch path.
- Every present telemetry producerSequence must be positive before session selection. Sequence identity/deduplication and duration validity remain scoped as specified; serviceStatus does not become a telemetry record.
- Explicit UNC and Windows device namespace input forms are rejected before file I/O. Normal filesystem paths remain subject to operating-system path/mount semantics; this CLI is not an OS filesystem or network sandbox.
- Unit tests cover these input forms and sequence admission alongside the existing observed-comparison contract. No new public API, project dependency, Unity behavior or report metric is introduced.
- Full candidate verification and independent C/C' are pending until the fixed candidate manifest is generated. Earlier heads' successful tests are not acceptance evidence for this candidate.

## 7. Evidence acquisition

- Frozen A3 source: `9e25ba0b01e68b3cd5c2490a5d089944536e288a:docs/handoff/DEBUGSTUDIO_OFFLINE_COMPARE.md`, Git blob `75514071354cd7f975152271a009d1c717da5add`. Obtain with `git show`; the local bundle verifies the recovered bytes against this blob. The former cloud-only standalone snapshot and its original SHA-256 are not claimed recovered.
- Local evidence root: `D:/repositories/unity/SampleGameForOneStarMakerFramework/TestResults/pr87-integration/`. Each candidate has a separate immutable directory/archive and manifest with base/head, commands, raw logs, full TRX, complete diff and SHA-256 for all payloads. Copies are extracted and hash-verified before reviewer access. Retain through 2026-11-04; preserve the locator in the PR when harvesting this HANDOFF.
- The final C and blind C' inputs are the same fixed candidate package, generated before either judgment. Reviewer reports stay outside it. Independent premise evaluation is separate from specification-conformance judgment.
- Harvest target remains the CLI README, which documents only observed comparisons and does not assert performance causality or completeness. Remove this HANDOFF during final D after the required reviews succeed.
