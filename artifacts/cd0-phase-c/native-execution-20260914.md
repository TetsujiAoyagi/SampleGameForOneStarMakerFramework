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

## E7 — isolated Player, stripping, BuildReport, relocation

The disposable project root was artifacts/cd0/player-host/unity. Runtime, Editor, and Fixture sources and meta files matched the main checkout by SHA-256; OSM, SampleGame, Addressables settings, and their build callbacks were absent. Content output, copied reports, and Player outputs were strict descendants of artifacts/cd0/player-host.

- backend: Mono; API compatibility: .NET Standard 2.0; managed stripping: High
- first Player build: Succeeded, 0 errors, 1 warning, 73,439,257 bytes, 53.296 seconds
- the Player registered the host Content output and loaded Root, probe-v1, the additive Payload Scene, and the content-only Cd0SceneMarker before cleanup
- a fresh Player loaded a complete copy from relocated content 日本語 while the original path was absent; the first launch intentionally documented that an unquoted Windows argument truncates at the first space, and the correctly quoted single argument succeeded
- passing a Player BuildHistory directory as previousBuildReportDirectories produced Unity's Failed to locate or parse ScriptsOnlyCache.yaml warning
- passing the Content Directory BuildHistory directory containing ScriptsOnlyCache.yaml removed that warning; the High-stripping Player build succeeded with 0 errors and 1 warning in 12.340 seconds, and its runtime load completed
- the remaining warning was the expected absence of a RuntimePipelineManager in the isolated bootstrap Scene

This establishes that previousBuildReportDirectories consumes the Content build report for content-only code retention; an arbitrary Player report directory is not interchangeable.

## E8 — changed input

In the isolated host, changing only the probe value from probe-v1 to probe-v2 and rebuilding changed three roles: the manifest JSON, one 856-byte .cf payload, and BuildManifestHash.txt. The other five files retained identical SHA-256 hashes. The already-built High-stripping Player then loaded probe-v2 from the rebuilt Content Directory and completed the Scene/cleanup sequence.

## Remaining

- E5 in-flight asset/Scene cancellation
- E9 HTTP remains optional and follows local completion

The native core path is supported through E1–E8. In-flight native cancellation was not made timing-dependent and remains inconclusive as allowed by E5. The result is CONDITIONAL because Player evidence is isolated-host-only and production integration remains untested.
