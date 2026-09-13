# CD0 Phase A revision 1 — frozen implementation input

- implementation base: `0a11a4be58c7b75356b076f356078d8d001c2e5b`
- frozen: 2026-09-13 JST
- A1 input: `artifacts/cd0-phase-a/A1-snapshot.md`
- human authorization: 2026-09-13「実装に入って」

The A1 snapshot remains the complete planning packet. The following A2 amendments are authoritative where they differ from A1.

1. Split orchestration, native session ownership, event output, filesystem inventory, and inventory-validated cleanup. Existing OSM assemblies gain no reference to the fixture assemblies.
2. Add exactly three fixture assemblies: Runtime, Editor, Tests.Editor. Runtime uses Unity APIs but no OSM or third-party package. Editor references Runtime. Tests.Editor references Runtime and Editor. All use `autoReferenced=false`; existing asmdefs remain unchanged.
3. A run-owned content session exclusively owns the directory handle, Loadable, loaded Scene, and pending native operations. Borrowed roots are not dereferenced after unregister. Asset and Scene abandonment use separate ledger cases, and retry begins only after terminal observation and cleanup.
4. Player stripping is executed only in a separate disposable project without OSM/Addressables. P0/P1 use identical content bytes and record the first divergent stage plus linker evidence. An isolated-host result does not prove OSM Player integration.
5. Relocation passes only in a fresh Player process with the original path absent and a byte-identical, non-link destination. Incremental experiments include restore, dependency removal, and dependency restoration cases R5-R7.
6. Evidence cases share case-id/run-id/generation, preconditions, expected terminal, allowed diagnostics, cleanup state, and pass/constrained/fail/inconclusive. Runtime-required outputs are separated from reports/logs/unknown files.
7. `ContentLoadManager.GetContentDirectories()` is available in the exact Editor and may be used together with the run's own handle ledger.
8. HTTP, SVN integration, BuildSystem design, existing Addressables repair, production bootstrap, and low-level ContentLoadInterface remain out of scope.
9. The Player stripping control type is `Cd0SceneMarker`, which the bootstrap and runner must not reference statically. `Cd0ProbeAsset` is the dependency-load subject and is not a valid content-only negative control because `Cd0Root` names its generic type.
10. A partial-acquisition failure must be attempted in C. Cleanup failure is a distinct terminal ledger state: unresolved cleanup forbids unregister and retry, and remains quarantined for evidence/recovery.
11. E6 is provisional until the E7 P1 fresh Player loads the relocated bytes with the original path unavailable. Native API observations and run-ledger observations are labeled separately.

Stop and reopen Phase A if the direct APIs differ at compile/runtime, an existing production setting/asset/asmdef must change, a third Scene is needed, a new public OSM API or lifetime is required, or native resources cannot be drained and accounted for.
