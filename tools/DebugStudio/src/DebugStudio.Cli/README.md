# DebugStudio CLI

The .NET 8 CLI sends live control-plane commands and compares explicitly selected telemetry captures locally.

## Live commands

```sh
debugstudio-cli send --command debugsocket.ping --control-uri ws://127.0.0.1:5012/cli-control/ --payload "{}" --timeout-seconds 15
```

The control URI, `{}` payload and 15-second timeout are optional defaults. `--uri` remains an alias for `--control-uri`. Existing send output and exit behavior are unchanged.

## Offline comparison

```sh
debugstudio-cli compare --input capture.ndjson --baseline-session baseline-id --candidate-session candidate-id

debugstudio-cli compare --input rolling-001.ndjson --input rolling-002.ndjson --baseline-session baseline-id --candidate-session candidate-id --format json
```

Use `--help` for the global synopsis, or `compare --help` for the comparison synopsis. Subcommand dispatch is case-insensitive. Options, formats, session IDs, stream/kind/name values and grouping keys use exact, case-sensitive matching. IDs are preserved without trimming and must be nonempty and distinct.

`--input` is the only repeatable option. Every input must name an explicit local file. Directories, URLs, stdin, implicit discovery, repeated singleton options and positional extras are rejected. The default format is `text`; `json` is also supported. Comparison opens no network connection and never changes source files.

### Capture admission

Inputs are normalized v3 telemetry NDJSON from the existing DebugStudio Export writer, one JSON object per nonblank UTF-8 line. LF, CRLF and an initial UTF-8 BOM are accepted; blank lines are counted separately. Invalid UTF-8 is rejected without replacement decoding. Elastic bulk action lines and Kibana exports are not telemetry captures.

Every nonblank row needs a string `@timestamp`, integer `timestampUnixTimeMilliseconds` and recognized `stream` (`telemetry` or `serviceStatus`). Telemetry also needs `schemaVersion: 3`, a nonempty `name` and `kind` equal to `span`, `sample` or `event`. Unknown extension fields are allowed, but malformed JSON, duplicate object properties and wrong typed fields are errors, including in rows that would otherwise be skipped.

Valid service-status rows are skipped and counted. Telemetry without a session ID is unassigned, skipped and warned about; telemetry for other sessions is skipped and counted. Each selected session must have at least one valid telemetry row.

### Observed duration metrics

- Only selected `kind: "span"`, `name: "AppStartup"` rows with a nonempty `payload.stage` and finite, nonnegative `elapsedMs` enter startup metrics.
- AppStartup values are whole-span durations grouped by their stage label, not individual step durations. BeforeSceneLoad/AfterSceneLoad label the whole span; a failure stage identifies the last attempted step.
- Only selected `kind: "span"`, `name: "SceneLoad"` rows with a nonempty `payload.targetIdentity` and finite, nonnegative `elapsedMs` enter scene metrics. No flat-field fallback is used.
- SceneLoad describes observed spans/calls, including already-active short-circuit calls. It does not establish completed cold-load durations.
- All valid observed durations are included regardless of `isSuccess`. Missing keys/durations are excluded from their metric, counted and warned about. A present negative or nonfinite selected duration is an error.

Each union key shows both runs' count, median and maximum milliseconds, followed by candidate-minus-baseline deltas. Keys sort ascending with ordinal comparison, independently for startup and scene groups. Median is the middle observation or the overflow-safe arithmetic midpoint of the two middle observations. Count is essential context for sparse captures.

An unobserved side has count 0 and unavailable median/max/duration deltas, represented as `n/a` in text or `null` in JSON. It is never represented as zero milliseconds. If neither run has observations, the metric has no groups.

### Bottleneck and coverage

Bottleneck is the observed telemetry-row count, including samples/events. A row counts once if its tags contain exact `Bottleneck` or its tag bits contain the existing Bottleneck bit. When both sources exist and disagree, their union is counted and a warning is emitted. If both are absent, tag coverage is unknown; a count of 0 does not prove that no bottlenecks occurred. The report shows a count delta, not a frame rate or unobserved-frame estimate.

A positive selected `(sessionId, producerSequence)` identifies a row across overlapping or repeated files. Exact typed duplicates are retained once and counted with both locations. JSON property order/whitespace and unknown extensions do not matter. Present tag arrays compare as ordinal sets; null tags differ from an empty array. Same sequence numbers in different sessions are independent.

Conflicting typed fields for the same identity are fatal; no capture wins automatically. Typed equality includes session attributes: a rolling capture and later manual export can conflict if attributes were enriched later. Select nonconflicting inputs rather than expecting enrichment to be merged.

Missing/null sequence is allowed, counted and warned about. Such rows are retained and cannot be proven unique; no identity is invented from timestamps, names or span IDs. Present nonpositive selected sequences are errors. Sequence gaps are not checked because producer order crosses logs and telemetry, and gaps do not prove telemetry loss.

Successful nonblank-row accounting is disjoint:

`nonblank = selected retained after deduplication + exact duplicates + skipped serviceStatus + unassigned telemetry + other-session telemetry`

Blank rows are separate. Missing sequence/tag coverage and metric exclusions are subsets of selected retained rows, not additional skipped-input categories.

### Output and exits

Summary goes to stdout and diagnostics to stderr. JSON has `reportVersion: 1`, selected IDs, input accounting, per-run coverage, startup/scene groups, Bottleneck counts, descriptions and diagnostics with input locations. Text presents the same information with warnings on stderr. Numeric formatting is invariant-culture; text escapes control characters in IDs, keys and paths.

- `0`: observed comparison completed; warnings can remain
- `2`: usage, invalid input, ambiguous duplicate identity or missing selected run
- `1`: input file-open/read failure

Every input is admitted before any report is published. Fatal failures publish only one fatal diagnostic, with no partial summary or accumulated warnings. JSON failures identify file/1-based line; encoding failures identify the accurate physical line. Duplicate conflicts identify both locations. File-open/usage/missing-session errors do not invent line numbers. Malformed full input lines are never echoed.

The result describes supplied observations. It makes no automatic regression verdict, statistical significance, capture-completeness or environment-equivalence claim.
