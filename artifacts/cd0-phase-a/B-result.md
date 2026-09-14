# CD0 Phase B result

- date: 2026-09-13 JST
- base: `0a11a4be58c7b75356b076f356078d8d001c2e5b`
- implementation head: `189bc21086aaf96d77f64c017b4ebf7ad4301876`
- phase owner/model: Codex / GPT-5 / OpenAI

Implemented the disposable CD0 fixture under `unity/Assets/CD0Spike/` without changing any existing OSM, SampleGame, Addressables, package, build profile, or app-config file.

The implementation contains three new assemblies (`CD0Spike.Runtime`, `CD0Spike.Editor`, `CD0Spike.Tests.Editor`), 18 C# files / 754 lines, Unity-generated meta files, two generated Scenes, one root asset, and one probe asset. Existing asmdefs have no new dependency edge.

Runtime responsibilities are split between the serialized root/types, run ledger, native content session, orchestrator, and event sink. Editor responsibilities are split between authoring, direct Content Directory build, isolated-host Player build, filesystem inventory, and inventory-validated cleanup. Tests cover stale generation, abandonment/drain, cleanup admission, cleanup failure, duplicate cleanup, path containment, prefix confusion, and traversal.

An already-open Unity `6000.6.0f1` Editor was reached only through `tools/unity-editor.cmd`. Compilation initially exposed a C# language-version mismatch at the `Loadable<T>` constructor call; the call was corrected without changing the API boundary. The final Editor recompile completed with `failed=false` and no errors.

`OneStarMaker/CD0/Generate Fixture` then generated the planned fixture through Editor APIs. It restored the original clean `Assets/Scenes/SampleScene.unity` setup. Console error count after successful generation was zero.

Fixture hashes:

- Root.asset: `A3D70B3236DBD86FEC5785356983252F822FDE72B228D7F0BEB8395921A8B890`
- Bootstrap.unity: `52EBE17BF558AB87E03FE68024829BAE34E187ECEB3DDB9205E9227316C5DF12`
- Payload.unity: `C2FC3201254D37AA01003D7B7633A79F622279D3A8C42EE4B8196432FF14000C`
- ProbeAsset.asset: `6E39224443F70CD8C4223C989AE1FEC25992AB03F73D710BB413355491FFD610`

`pwsh tools/contract-audit.ps1` passed. `pwsh tools/docs-audit.ps1` passed checks 1 and 2; it retained the pre-existing U66 harvest warning.

Per Phase B rules, Unity tests, Content Directory build, Player build/run, runtime registration/load/unload, failure/cancellation/retry cases, relocation, incremental rebuild, and HTTP were not executed. These remain Phase C work. The isolated Player host itself was not created because that is part of the C experiment setup.

The implementation was fixed in three commits ending at `189bc21086aaf96d77f64c017b4ebf7ad4301876`. The user's untracked `PRE_PHASE_A_BUILDSYSTEM_REBUILD_UNITY66_V3.md` remains untouched and unstaged.
