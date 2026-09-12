# S-4b Phase C evidence

- implementation base: `bfdc1f8a1f614ff17ae39a1a158181d6313ae879`
- implementation head: `73f4e4dc1f6b7967b25bb9afb7ab31f3d8db34c0`
- scope: P2 generated assets, startup wiring, and P3 generator removal. P1 is excluded.
- generated at: 2026-09-12 (Asia/Tokyo)

`full.diff`, `diff-stat.txt`, `name-status.txt`, and `commits.txt` fix the review target. `filesystem-inspect-current.txt` compares all 652 generated Scene paths with `expected-set.txt` and records the 440 colocated SceneResource assets and removal checks. The P2/P3 raw artifacts are copied without replacing their original files.

The two `unity-*.log` and `results-*.xml` pairs are the raw filtered and full EditMode runs. `contract-audit.log`, `diff-check.txt`, `diff-check-csharp.txt`, and `machine-exit-codes.txt` record the non-Unity checks. The implementation-wide diff check reports generated Unity `.asset` / `.meta` trailing whitespace; the C#-only check has no output and exit 0. `hashes.sha256` is generated after the bundle contents are final.

The current Editor was closed, so Graph / Map / Addressables / volume were not re-inspected in an Editor. The last Editor-side inspection is `validation.txt` / `last-editor-inspect.txt`; `generation.log` intentionally preserves the first stale-Library inspection failure before the later validation. Generate was not invoked during Phase C.
