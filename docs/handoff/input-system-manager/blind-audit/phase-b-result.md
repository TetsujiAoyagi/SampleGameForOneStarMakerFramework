# Phase B result

- implementation base: 2c29c99806788406551affba6cc795e67e614748
- implementation head: 3ad67c81499530301c1329fc28e70830179dd49a
- phase A snapshot sha256: 03ffdabf2100511bf4ee486b792d69a473d561ff110fbf6a5d8bc85142467c3a
- phase A snapshot bytes are at implementation head path docs/handoff/input-system-manager/phase-a-snapshot.md and, after this result was written, also at docs/handoff/INPUT_SYSTEM_MANAGER_PHASE_A.md. The move is not part of implementation head.

## What the implementation contains

- `InputManager` in `OneStarMaker.Runtime.InputSystem`.
- The caller passes an `InputActionAsset`. The manager requires action maps named Player and UI. It does not load or destroy the asset. Dispose disables those two maps.
- Actions whose expectedControlType is Button or Vector2 become `InputActionValue` slots. Other control types increment `UnsupportedActionCount` and are not slots.
- `SetInteractionState` stores the `SceneState` the caller supplies. The manager does not subscribe to SceneDirector.
- While that state is Stable, Sample reads the device and publishes the active map. Slots on the other map are zero.
- While that state is not Stable, Sample does not call the device reader and rewrites every published slot to zero, keeping index, map, and kind.
- `TrySetMap` accepts Player and UI. `MapChanged` is an R3 observable and emits only when the map changes. Setting the same map does not emit and does not call Enable again.
- `TrySelectProfile` accepts only the profile id `default`.
- `TryRegister` registers one element on Update layer Input at order -100. The element samples in Update and does not change values in LateUpdate.
- `OneStarMaker.Tests` references `Unity.InputSystem`.
- SampleGame, FlyController, and AppInitializer are not in the implementation diff.
- Offline runner: `tools/InputSystemOfflineTests`.

## Commands and results

Command: `dotnet run -c Release --project tools/InputSystemOfflineTests`

Result: exit 0. 12 passed, 0 failed, 12 executed. Log: `offline-tests.txt`.

Command: `pwsh -NoProfile -File tools/contract-audit.ps1` (PowerShell 7.5.4)

Result: exit 0. `AUDIT_RESULT kind=contract files=577 errors=0 warnings=0 checks=1,2,3,4,5,6,7,8 applicable8=False`. Log: `contract-audit.txt`.

Command: `pwsh -NoProfile -File tools/docs-audit.ps1` (PowerShell 7.5.4)

Result: exit 0. `AUDIT_RESULT kind=docs files=109 errors=0 warnings=0 checks=1,2,3`. Log: `docs-audit.txt`.

## Not run

Unity Editor is not installed in this environment. `pwsh tools/run-tests.ps1` was not started. No Unity result XML exists. `InputActionAssetReader` was not compiled by the Unity editor.
