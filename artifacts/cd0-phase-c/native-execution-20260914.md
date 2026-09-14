# CD0 native execution — 2026-09-14

- Editor: Unity 6000.6.0f1, project D:/repositories/unity/SampleGameForOneStarMakerFramework/unity
- active target: StandaloneWindows64
- packages: com.unity.pipeline 0.4.0-exp.1, com.unity.addressables 2.11.2
- SVN inputs: none
- Production Addressables, BuildVariantProfile, VariantWhitelistBuilder, Player bootstrap, and seasonal Scenes were not changed.

## E1 — authoring and serialization

The first native regeneration exposed a fixture-authoring defect: after the generator created and saved the two Scenes, Root.asset serialized its probe LoadableObjectId as {fileID: 0, loadable: 1}. The committed valid reference was {fileID: 11400000, guid: 84c97ce2565d399499c438764d5bcfeb, type: 2}.

Live reflection against the exact Editor showed that both CreateLoadableObjectId(Object) and the explicit GUID overload produced 84c97ce2565d399499c438764d5bcfeb / 11400000 / SourceAsset while the asset instance was valid. The generator was fixed to reload the imported probe and create its ID before Scene creation, which unloads unused assets. A clean rerun then left Root.asset byte-identical to the committed valid reference. Scene and asset meta GUIDs stayed unchanged.

## E2 — direct Content Directory build

- menu: OneStarMaker/CD0/Build Content Directory (Isolated Run)
- output: artifacts/cd0/content/runs/20260914T140830456Z
- result: Succeeded
- platform: StandaloneWindows64
- total size reported by Unity: 644793
- files: one manifest JSON, five .cf, one .resS, and BuildManifestHash.txt
- the Pipeline request timed out while Unity remained in the build; the native build continued and emitted the successful final report
- warnings: existing nullable warnings, Pipeline runtime-manager absence, and unsupported fallback fonts; no build error

## E3/E4 — register, root, asset, Scene, cleanup

- directory completed with root Root
- probe asset accepted with value probe-v1
- additive payload Scene accepted as Payload
- payload marker enabled with scene-v1 and dependency probe-v1
- cleanup completed
- after leaving Play mode, GetContentDirectories valid handle count was 0

## E5 — failure and retry

Registration of artifacts/cd0/content/missing-native-case failed with a native FileNotFoundException for BuildManifestHash.txt. The run still emitted cleanupDone. Without restarting the Editor, retrying the valid directory completed the full root/asset/Scene/cleanup sequence. Native cancellation during an in-flight asset or Scene operation remains unexecuted.

## E6 — relocated local directory

The successful output was copied to artifacts/cd0/content/relocated path 日本語; the original run path was temporarily moved so it did not exist. Registration, root lookup, asset load, additive Scene load, and cleanup all succeeded from the relocated path. The valid handle count after Play mode was 0. The original run path was restored. This is Editor evidence; the fresh-Player relocation case remains part of E7.

## E8 — unchanged rebuild

Two builds used artifacts/cd0/content/local-v1 and build name cd0-local-v1. The second build completed in about 16 seconds. All eight relative files, lengths, and SHA-256 hashes were identical. Seven timestamps were unchanged. BuildManifestHash.txt was rewritten but retained identical bytes/hash: 82FE778B24398BD42BD735E874DB9D63FC7C722C731BC354659D2E87F71E76E0.

## Remaining

- E5 in-flight asset/Scene cancellation
- E7 isolated Player host, stripping, BuildReport comparison, and fresh-Player relocation
- E8 changed-input dependency/hash observation
- E9 HTTP remains optional and follows local completion

The native core path is no longer wholly inconclusive: E1–E4, missing-directory failure/retry, Editor relocation, and unchanged rebuild are supported. Overall CD0 remains HOLD until E7 and the remaining mandatory matrix are resolved.
