# U66 live Editor / build result extract

- observed: 2026-09-13 JST
- Editor: Unity `6000.6.0f1 (f7f8ed4d1e24)`, already opened by the human; Pipeline port 7800, PID 4792
- active target: StandaloneWindows64; scripting backend: Mono; API compatibility: NET Standard 2.0
- scene: `Assets/Scenes/SampleScene.unity`

## Play / Workspace observations

- Full cycle 1: SampleScene -> SampleScene + UIScene; Console error entries 0; stop returned to SampleScene only.
- Full cycle 2: SampleScene -> SampleScene + UIScene; Console error entries 0; stop succeeded.
- Whitebox cycle 1: temporary `assets:sceneVariant=Whitebox`; SampleScene + UIScene; Console error entries 0; stop succeeded; original bytes restored.
- Workspace: `unity command menu --path "OneStarMaker/World Workspace/Open Window"`; result `Executed menu item`; subsequent Console error entries 0.
- repeated runtime message during Play: `There are 2 audio listeners in the scene. Please ensure there is always exactly one audio listener in the scene.` Post-load type query returned one active AudioListener at `/Main Camera`.

## Packed Addressables result

Production `AddressableAssetSettings.BuildPlayerContent` result:

`error=[VariantFilteringBuildScript] Build aborted due to whitelist validation errors.;duration=0;output=;locations=0`

Direct `VariantWhitelistBuilder.Build` diagnostics for both Production and WorldWhitebox:

`SceneResourceMap:HomeScene: no payload matched Variant whitelist.`

The same error was returned for `InGameScene`, `InGameSession`, `OutGameSession`, `Season_Autumn`, `Season_Spring`, `Season_Summer`, and `Season_Winter`.

## Windows Player result

Command: `OneStarMaker/Build/Build Player (Active Variant)` with Production active.

Editor log result:

`BuildFailedException: Failed to build Addressables content, content not included in Player Build. "[VariantFilteringBuildScript] Build aborted due to whitelist validation errors."`

`[VariantPlayerBuild] build failed: System.InvalidOperationException: Build result: Unknown`

No `Builds/ActiveVariant/SampleGame.exe` was produced. The overlay finally block reimported the original app-config. Final app-config SHA-256: `6A5BC86EFC8395A95EFCBFB46B1064228863E48518BA1AB7F0449C0DEBB2E338`.

## Cleanup

The build attempt's temporary active-profile serialization in `VariantFilteringBuildScript.asset` was restored. Unity-generated, untracked `ProfileDataSourceSettings.asset` and its meta were removed. No Player/build output existed to retain.
