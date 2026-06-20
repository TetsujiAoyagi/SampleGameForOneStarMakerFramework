# Assembly 分割移行計画

> 作成日: 2026-03-07  
> ステータス: ✅ 完了 (2026-03-07)

---

## 1. 概要

`OneStarMakerCommon`（単一 Assembly）を 3 Assembly に分割し、フォルダを再構成する。

**Before:**
```
Assets/OneStarMakerCommon/          ← asmdef: OneStarMakerCommon (全部入り)
├── Scripts/
│   ├── AbstractApplicationInitializer.cs
│   ├── AssemblyInfo.cs
│   ├── AssetDescriptions/ (3 files)
│   ├── Config/            (5 files)
│   ├── Debug/             (3 files)
│   ├── Logging/           (4 files)
│   ├── Scene/             (17 files)
│   └── UI/                (2 files)
├── Editor/                (10 files)
└── Tests/                 (12 files)
```

**After:**
```
Assets/OneStarMaker/
├── Foundation/                     ← asmdef: OneStarMaker.Foundation
│   ├── Logging/           (4 files)
│   └── Config/            (5 files)
│
├── Runtime/                        ← asmdef: OneStarMaker.Runtime
│   ├── AbstractApplicationInitializer.cs
│   ├── AssemblyInfo.cs
│   ├── AssetDescriptions/ (3 files)
│   ├── Scene/             (17 files)
│   └── UI/                (2 files)
│
├── Debug/                          ← asmdef: OneStarMaker.Debug
│   └── Profiler/          (3 files)
│
├── Editor/                         ← asmdef: OneStarMaker.Editor
│   └── SceneGraph/        (10 files)
│
└── Tests/                          ← asmdef: OneStarMaker.Tests
    └── Scene/             (12 files)
```

---

## 2. Assembly 依存関係

```
OneStarMaker.Foundation  (leaf — 外部依存なし within framework)
       ▲
       │
OneStarMaker.Runtime ──► Foundation + UniTask + Addressables + LitMotion + VContainer
       ▲
       │
OneStarMaker.Debug ──► Foundation + Runtime + Unity.TextMeshPro

OneStarMaker.Editor ──► Runtime (Scene, AssetDescriptions)
OneStarMaker.Tests  ──► Runtime + Foundation
```

---

## 3. namespace マッピング（10 変換）

| # | Old namespace | New namespace | Assembly |
|---|---|---|---|
| N-1 | `OneStarMaker.Common.Logging` | `OneStarMaker.Foundation.Logging` | Foundation |
| N-2 | `OneStarMaker.Common.Config` | `OneStarMaker.Foundation.Config` | Foundation |
| N-3 | `OneStarMaker.Common` (root) | `OneStarMaker.Runtime` | Runtime |
| N-4 | `OneStarMaker.Common.AssetDescriptions` | `OneStarMaker.Runtime.AssetDescriptions` | Runtime |
| N-5 | `OneStarMaker.Common.SceneSystem` | `OneStarMaker.Runtime.SceneSystem` | Runtime |
| N-6 | `OneStarMaker.Common.UISystem` | `OneStarMaker.Runtime.UISystem` | Runtime |
| N-7 | `OneStarMaker.Common.Debug` | `OneStarMaker.Debug` | Debug |
| N-8 | `OneStarMaker.Common.Editor.SceneGraph` | `OneStarMaker.Editor.SceneGraph` | Editor |
| N-9 | `OneStarMaker.Common.Tests.SceneSystem` | `OneStarMaker.Tests.SceneSystem` | Tests |
| N-10 | `OneStarMaker.Common.Tests.SceneSystem.*` | `OneStarMaker.Tests.SceneSystem.*` | Tests |

---

## 4. asmdef 作成（3 新規 + 2 更新）

### 4.1 OneStarMaker.Foundation.asmdef (新規)
```json
{
    "name": "OneStarMaker.Foundation",
    "rootNamespace": "OneStarMaker.Foundation",
    "references": [],
    "includePlatforms": [],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": true,
    "precompiledReferences": [
        "ZLogger.dll",
        "Microsoft.Extensions.Logging.Abstractions.dll",
        "Microsoft.Extensions.Logging.dll"
    ],
    "autoReferenced": true,
    "defineConstraints": [],
    "versionDefines": [],
    "noEngineReferences": false
}
```

### 4.2 OneStarMaker.Runtime.asmdef (新規)
```json
{
    "name": "OneStarMaker.Runtime",
    "rootNamespace": "OneStarMaker.Runtime",
    "references": [
        "OneStarMaker.Foundation",
        "UniTask",
        "Unity.Addressables",
        "Unity.ResourceManager",
        "LitMotion",
        "Unity.InputSystem"
    ],
    "includePlatforms": [],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": true,
    "precompiledReferences": [
        "UniTask.dll",
        "VContainer.dll",
        "R3.dll",
        "ObservableCollections.dll"
    ],
    "autoReferenced": true,
    "defineConstraints": [],
    "versionDefines": [],
    "noEngineReferences": false
}
```

### 4.3 OneStarMaker.Debug.asmdef (新規)
```json
{
    "name": "OneStarMaker.Debug",
    "rootNamespace": "OneStarMaker.Debug",
    "references": [
        "OneStarMaker.Foundation",
        "OneStarMaker.Runtime",
        "UniTask",
        "Unity.TextMeshPro"
    ],
    "includePlatforms": [],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": true,
    "precompiledReferences": [
        "UniTask.dll"
    ],
    "autoReferenced": true,
    "defineConstraints": [],
    "versionDefines": [],
    "noEngineReferences": false
}
```

### 4.4 OneStarMaker.Editor.asmdef (更新)
- references: `"OneStarMakerCommon"` → `"OneStarMaker.Runtime"`

### 4.5 OneStarMaker.Tests.asmdef (更新)
- references: `"OneStarMakerCommon"` → `["OneStarMaker.Foundation", "OneStarMaker.Runtime"]`

---

## 5. Game 層 asmdef 更新（4 ファイル）

| asmdef | Old reference | New references |
|---|---|---|
| SampleGame.Common | `"OneStarMakerCommon"` | `"OneStarMaker.Foundation", "OneStarMaker.Runtime"` |
| SampleGame.DependOnAll | `"OneStarMakerCommon"` | `"OneStarMaker.Foundation", "OneStarMaker.Runtime", "OneStarMaker.Debug"` |
| SampleGame.InGame | `"OneStarMakerCommon"` | `"OneStarMaker.Foundation", "OneStarMaker.Runtime"` |
| SampleGame.OutGame | `"OneStarMakerCommon"` | `"OneStarMaker.Foundation", "OneStarMaker.Runtime"` |

---

## 6. ファイル移動一覧（62 .cs + 5 .asmdef）

### 6.1 Foundation (9 files)
| From | To |
|---|---|
| `Scripts/Logging/IAppLogger.cs` | `Foundation/Logging/IAppLogger.cs` |
| `Scripts/Logging/AppLogger.cs` | `Foundation/Logging/AppLogger.cs` |
| `Scripts/Logging/AppLoggerFactory.cs` | `Foundation/Logging/AppLoggerFactory.cs` |
| `Scripts/Logging/NullAppLogger.cs` | `Foundation/Logging/NullAppLogger.cs` |
| `Scripts/Config/AppConfig.cs` | `Foundation/Config/AppConfig.cs` |
| `Scripts/Config/IConfigProvider.cs` | `Foundation/Config/IConfigProvider.cs` |
| `Scripts/Config/JsonFileConfigProvider.cs` | `Foundation/Config/JsonFileConfigProvider.cs` |
| `Scripts/Config/EnvironmentVariableConfigProvider.cs` | `Foundation/Config/EnvironmentVariableConfigProvider.cs` |
| `Scripts/Config/CommandLineConfigProvider.cs` | `Foundation/Config/CommandLineConfigProvider.cs` |

### 6.2 Runtime (22 files)
| From | To |
|---|---|
| `Scripts/AbstractApplicationInitializer.cs` | `Runtime/AbstractApplicationInitializer.cs` |
| `Scripts/AssemblyInfo.cs` | `Runtime/AssemblyInfo.cs` |
| `Scripts/AssetDescriptions/*.cs` (3) | `Runtime/AssetDescriptions/*.cs` |
| `Scripts/Scene/*.cs` (17) | `Runtime/Scene/*.cs` |
| `Scripts/UI/*.cs` (2) | `Runtime/UI/*.cs` |

### 6.3 Debug (3 files)
| From | To |
|---|---|
| `Scripts/Debug/DebugProfilerView.cs` | `Debug/Profiler/DebugProfilerView.cs` |
| `Scripts/Debug/FrameTimeGraphRenderer.cs` | `Debug/Profiler/FrameTimeGraphRenderer.cs` |
| `Scripts/Debug/FrameTimeSampler.cs` | `Debug/Profiler/FrameTimeSampler.cs` |

### 6.4 Editor (10 files) — パスは変わらない（フォルダは OneStarMaker/ 直下に維持）
### 6.5 Tests (12 files) — パスは変わらない（フォルダは OneStarMaker/ 直下に維持）

---

## 7. using 文の変更一覧（28 箇所）

### Framework 内部 (8 files)
| File | Old using | New using |
|---|---|---|
| AbstractApplicationInitializer.cs | `OneStarMaker.Common.Config` | `OneStarMaker.Foundation.Config` |
| AbstractApplicationInitializer.cs | `OneStarMaker.Common.SceneSystem` | `OneStarMaker.Runtime.SceneSystem` |
| AbstractApplicationInitializer.cs | `OneStarMaker.Common.UISystem` | `OneStarMaker.Runtime.UISystem` |
| DebugProfilerView.cs | `OneStarMaker.Common.Logging` | `OneStarMaker.Foundation.Logging` |
| DebugProfilerView.cs | `OneStarMaker.Common.UISystem` | `OneStarMaker.Runtime.UISystem` |
| SceneBase.cs | `OneStarMaker.Common.UISystem` | `OneStarMaker.Runtime.UISystem` |
| SceneDirector.cs | `OneStarMaker.Common.AssetDescriptions` | `OneStarMaker.Runtime.AssetDescriptions` |
| SceneDirector.cs | `OneStarMaker.Common.UISystem` | `OneStarMaker.Runtime.UISystem` |
| SceneDirector.Loading.cs | `OneStarMaker.Common.AssetDescriptions` | `OneStarMaker.Runtime.AssetDescriptions` |
| SceneResource.cs | `OneStarMaker.Common.AssetDescriptions` | `OneStarMaker.Runtime.AssetDescriptions` |

### Editor (4 files)
| File | Old using | New using |
|---|---|---|
| SceneResourceGenerator.cs | `OneStarMaker.Common.AssetDescriptions` | `OneStarMaker.Runtime.AssetDescriptions` |
| SceneResourceGenerator.cs | `OneStarMaker.Common.SceneSystem` | `OneStarMaker.Runtime.SceneSystem` |
| SceneNodeData.cs | `OneStarMaker.Common.AssetDescriptions` | `OneStarMaker.Runtime.AssetDescriptions` |
| SceneGraphInspectorPanel.cs | `OneStarMaker.Common.AssetDescriptions` | `OneStarMaker.Runtime.AssetDescriptions` |
| SceneGraphViewModel.cs | `OneStarMaker.Common.AssetDescriptions` | `OneStarMaker.Runtime.AssetDescriptions` |
| SceneGraphViewModel.cs | `OneStarMaker.Common.SceneSystem` | `OneStarMaker.Runtime.SceneSystem` |

### Tests (12 files) — 全て `OneStarMaker.Common.*` → 対応する新 namespace

### Game 層 (4 files)
| File | Old using | New using |
|---|---|---|
| AppInitializer.cs | `OneStarMaker.Common` | `OneStarMaker.Runtime` |
| AppInitializer.cs | `OneStarMaker.Common.SceneSystem` | `OneStarMaker.Runtime.SceneSystem` |
| GameSceneFactory.cs | `OneStarMaker.Common.SceneSystem` | `OneStarMaker.Runtime.SceneSystem` |
| NullLoadingDisplay.cs | `OneStarMaker.Common.SceneSystem` | `OneStarMaker.Runtime.SceneSystem` |
| TitleScene.cs | `OneStarMaker.Common.SceneSystem` | `OneStarMaker.Runtime.SceneSystem` |

---

## 8. InternalsVisibleTo 更新

`AssemblyInfo.cs` の InternalsVisibleTo を更新:
- `"OneStarMakerCommon.Tests"` → `"OneStarMaker.Tests"`
- `"OneStarMakerCommon.Editor"` → `"OneStarMaker.Editor"`

---

## 9. 実行手順（中断安全な順序）

| Step | 操作 | ロールバック |
|---|---|---|
| **1** | 新フォルダ構造を作成 | フォルダ削除 |
| **2** | .cs ファイルを新フォルダにコピー（旧フォルダは残す） | コピー先削除 |
| **3** | 新 asmdef 3つを作成 | asmdef 削除 |
| **4** | 新ファイルの namespace を一括変更 | Git revert |
| **5** | 新ファイルの using を一括変更 | Git revert |
| **6** | Editor/Tests の asmdef を更新 | Git revert |
| **7** | Game 層の asmdef を更新 | Git revert |
| **8** | Game 層の using を更新 | Git revert |
| **9** | コンパイル確認 | — |
| **10** | 旧 OneStarMakerCommon フォルダを削除 | Git revert |
| **11** | Docs（ARCHITECTURE.md, README.md）を更新 | Git revert |

**中断ポイント:** Step 2 まで完了していれば、旧フォルダが残っているため安全にロールバック可能。

---

## 10. リスク

| リスク | 対策 |
|---|---|
| Unity .meta GUID 変更でアセット参照破壊 | ファイル移動ではなくコピー＋旧削除で .meta を維持（Unity 外で mv） |
| Editor の SceneGraph ノードデータ破壊 | ScriptableObject の GUID は .meta ファイルに紐づくため、.meta ごと移動すれば安全 |
| 途中でセッション切れ | Step 2 まで完了なら旧フォルダで動作継続可能。本ドキュメントで残タスク把握可能 |
