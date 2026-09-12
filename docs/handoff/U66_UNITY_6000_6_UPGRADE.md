# U66 — Unity 6000.6 migration

> type: slice  
> status: **A1 初稿。未凍結。A2 独立レビューと A3 人間統合の前に実装へ渡さない。**  
> branch: `cursor/u66-unity-6000-6-handoff-6e0b`  
> implementation base commit: `3244f3635c6e18fffc1bbc40d3126e6d1eee009a`  
> implementation head commit: （未到達）  
> risk: high  
> owner: Phase A A1 = Cursor Cloud / Grok。Phase B 以降は A3 凍結後のローカル担当  
> created: 2026-09-12  
> expires: 本スライスの Phase D harvest 完了時  
> harvest to: `AGENTS.md`、`README.md`、`unity/Assets/README.md`、`unity/ProjectSettings/ProjectVersion.txt`、必要なら Architecture の現況版表記  
> Phase A snapshot path / id: （A3 凍結時に固定）  
> Phase A snapshot generated at:  
> Phase A snapshot hash:  
> Phase B result snapshot path / id:  
> evidence bundle path / id:  
> C' blind bundle path / id:

入力正本は Pre-Phase A 計画書 `PRE_PHASE_A_BUILDSYSTEM_REBUILD_UNITY66_V3.md` だが、**本書を Phase B の正本とする。** 計画書の package 表・Cinemachine / Timeline の読み・Addressables 3.x 前提は、2026-09-12 の `develop` と Unity 6.6 公式情報で補正した。補正内容は §1.4。

関連 program 計画（BuildSystem rebuild / CD0 / BS1 以降）は U66 の実装範囲ではない。U66 完了後に別 HANDOFF を切る。

---

## 1. 目的と対象外

### 目的

BuildSystem を再設計せず、repository を **Unity 6000.6.0f1** で cleanly open / compile / test / representative play できる状態へ移す。

成功条件は「Content Directories が使えること」ではない。**6.6 上で現行 repository の baseline が回復していること**。その結果として、次の CD0 spike を 6.6 上で開始できる状態になったことだけを確認する。

### 対象外

- Content Directories API の本実装、PoC 以外の実験コード、Addressables group の Content Directory schema 変換
- `BuildVariantProfile` / `VariantWhitelistBuilder` / Addressables build pipeline の再設計
- `VariantPlayerBuild` の JSON overlay 廃止、`firstSceneIdentify` 読者の実装
- ContentTag / BuildTag / `IBuildTagProvider` / BuildPlan
- unused package の削除（ads / analytics / purchasing / visualscripting 等）
- NuGet パッケージの bump
- git package（LitMotion / UniTask / CsprojModifier / NuGetForUnity）の hash bump。compile 修復に必要な場合だけ例外
- 661 Scene の `ForceReserializeAssets`
- `DEVELOPMENT_BUILD` / `UNITY_64` の一括置換（リポジトリ C# に使用なし）
- Dictionary 自前 wrapper の Unity 6.6 標準 Dictionary シリアライズへの移行
- Android GLES 3.1 / Adaptive icon 作業。compile が要求しない限り触らない
- package upgrade だけを根拠にした大規模リファクタ
- 「どうせ後で消す」を理由にした既存回帰の省略

### 現況（A0）

implementation base: `develop@3244f3635c6e18fffc1bbc40d3126e6d1eee009a`（2026-09-12 fetch。`origin/develop` と一致）。

| 項目 | 現在値 |
|---|---|
| Editor | `6000.5.0f1` (`88b47c5e7076`) |
| Unity Scene | 661 |
| SampleGame Scene | 659 |
| Season 配下 | 652（Full Cell 216 / Whitebox 216 / Environment 216 / Season Lighting 4） |
| 非 Season Scene | 9（`SampleScene`、OutGame 4、`PlayerScene`、`InGameUI`、`Result`、`UIScene`） |
| EditorBuildSettings Scene 0 | `Assets/SampleGame/OutGame/Title/Title.unity` のみ |
| Player build Scene | `VariantPlayerBuild` が `Assets/Scenes/SampleScene.unity` を hard-code |
| Addressables | 2.9.1。Default Local Group 1 つ。`VariantFilteringBuildScript` が packed build 経路 |
| URP | 17.5.0。`Assets/Settings/PC_RPAsset.asset` / `PC_Renderer.asset` / Mobile 対 |
| Input System | 1.19.0。`Assets/InputSystem_Actions.inputactions` |
| Test Framework | 1.7.0（builtin） |
| Cinemachine | 3.1.7。`CinemachineCameraBackend` が `Unity.Cinemachine` を参照 |
| Timeline | 1.8.12 |
| AI Navigation | 2.0.13 |
| SBP | transitive 2.6.1 |
| collections | transitive 6.5.0（LitMotion → Burst 経由） |
| test-framework.performance | transitive 3.5.0 |
| ugui | 2.5.0 |
| pipeline | `com.unity.pipeline` `0.4.0-exp.1`（Editor command 面） |
| 信頼できる全件テスト証拠 | リポジトリ内に無し。`TestResults/` は未チェックイン |

git packages（manifest は URL のみ。lock hash が実 pin）:

| package | lock hash |
|---|---|
| LitMotion | `2053ef5c23f2ae755dd85d5865b698c542c351b6` |
| CsprojModifier | `3c9be1a827ce7a2e0d9518e34905fc2fc7a6d5df` |
| UniTask | `e5acc106ee196bc5a32fb14cdf2987b0f96d11e0` |
| NuGetForUnity | `c2af83c9d4f8cdaada9d4a0e94de2f195d8e1d01` |

NuGet（`unity/Assets/packages.config`）は現状 pin を維持する。手動導入は MessagePack 3.1.7、ObservableCollections 3.3.4、R3 1.3.1、VContainer 1.0.2、ZLogger 2.5.10、ZString 2.6.0、ZStringFormatExtension 0.0.6。

既に 6.5 側で済んでいる移行:

- `GetInstanceID` は使っていない。`GetEntityId` + `EntityId.ToULong`。`contract-audit` 検査6 が残存を弾く。
- `UIDocument` は `PanelRenderer` へ移行済み。
- リポジトリ C# に `DEVELOPMENT_BUILD` / `UNITY_64` / `UxmlFactory` / `SetRuntimeInputBackend` / legacy Rendering Debugger `DebugState` は無い。

常時契約（本文転記）:

- 依存は Game → Framework の一方向。asmdef 参照の追加は設計判断なので勝手に行わない。
- アセットは `IAssetManagement` 経由。`AssetOwner` で寿命を宣言する。
- `SceneState` の既存14値は減らさず、並べ替えない。
- 公開 API のログ抽象は `ILogger<T>`。
- Update は `UpdateSystemRuntime` に登録する。
- Editor コードを Runtime アセンブリに置かない。
- Unity 側 C# で `record` を使わない。新規または編集する Unity 側 `.cs` は `#nullable enable`。
- 破棄されうる `UnityEngine.Object` は `== null` / `!= null`。
- Phase B は Unity.exe を起動しない。`pwsh tools/run-tests.ps1` と Addressables ビルドも実行しない。
- Scene / Prefab / Addressables / AuthoredRoot を変える場合、人間が既に開いている Editor へ `unity status` / `unity command` / `unity eval` で接続する。Cloud では Unity CLI を叩かない。
- `packages-lock.json` は人間が Editor を開いたときに UPM が書く。手で lock を捏造しない。
- `develop` / `main` へ直接コミットしない。PR base は `develop`。

### 1.4 Pre-Phase A 計画書への補正

計画書を現 `develop` と Unity 6.6 公式情報で照合した結果。**古い・誤った・過剰な前提**は次のとおり。Phase B は計画書ではなく本書を使う。

1. **採用 Editor は 6000.6.0f1 で固定する。** 公式リリースは 2026-08-31、changeset `f7f8ed4d1e24`。Issue Tracker に `6000.6.1f1` が出るが、`https://unity.com/releases/editor/whats-new/6000.6.1f1` は 404。公開 stable としては未確認。Phase B 開始時に人間が Hub / archive で newer `6000.6.x` f-release を一度だけ確認する。floating target は使わない。
2. **計画書の package 表は不完全。** 欠落した 6.6 line は Test Framework 1.8.0、ugui 2.6.0、collections 6.6.0、Performance Testing の core 化、Cinemachine / Timeline の core package 化。表だけ見て「書いてない package は触らない」と読むと、first-open 後の lock を誤って戻す。
3. **Cinemachine 6.6.0 は Cinemachine 6 ではない。** 公式 Upgrade Guide は「Cinemachine 3 is a core package」と書く。本リポジトリは既に 3.1.7。6.6.0 は Editor 同梱の version lock であり、CM2→CM3 の破壊的移行ではない。3.1.7 を無理に残そうとして core package と衝突させない。API / namespace が壊れたら停止条件。
4. **Timeline 6.6.0 も core package 化。** 1.8.12 を「major jump だから拒否」しない。first-open が 6.6.0 に寄せたら受け入れる。
5. **Addressables の Content Directory schema は 3.x/4.x 専用ではない。** 公式 What's New 6.6 は、同梱 Addressables（2.11.2）に Content Directory schema と「Default Build Script が AssetBundle / content directory の両方を扱う」と書く。U66 では schema 変換も Default Build Script への乗換もしない。`VariantFilteringBuildScript` を残す。
6. **purchasing を 4.15.0 へ下げない。** 現在 5.4.2。6.6 の template 比較表に出る 4.x は別系統。
7. **analytics は deprecated。** U66 で新 Analytics へ移行しない。削除もしない。
8. **`com.unity.pipeline` 0.4.0-exp.1 が計画書に無い。** osm-unity-editor の command 面。壊れたら停止。勝手に bump しない。
9. **661 Scene の一括 reserialize は推奨しない。** Upgrade Guide は YAML word wrap 廃止と `ForceReserializeAssets` を書く。本リポジトリでは大量 Scene YAML を U66 に混ぜるとレビュー不能になる。first-open が数百 Scene を書き換えたら停止して人間判断。
10. **Content Directories の仕様推測を U66 契約に書かない。** 公式は local only、独自 distribution は明示許可。これは CD0 / DIST の入力であり、U66 の受け入れ条件ではない。
11. **`VariantPlayerBuild.BootstrapScenePath` という定数は無い。** 実装は `UnityPlayerBuildBackend` 内の文字列 `"Assets/Scenes/SampleScene.unity"`。Architecture §20 の「`firstSceneIdentify` 読者は未実装」は現況のまま。U66 で直さない。
12. **公開面の版表記は stale。** `AGENTS.md` / `README.md` / `unity/Assets/README.md` はまだ 6000.5.0f1。U66 の harvest 対象。計画書は触れていない。

---

## 2. 受け入れ条件と制約

### 受け入れ条件

1. `unity/ProjectSettings/ProjectVersion.txt` が `6000.6.0f1`（Phase B 開始時に newer 6000.6.x f-release を選んだ場合は、その pin を HANDOFF に追記してから進める）。
2. `Packages/manifest.json` と `packages-lock.json` が 6.6 で resolve され、§3 の migration table に従う。lock は Editor first-open の UPM 出力を正とする。
3. 許可した互換修復以外の公開 API、asmdef 参照追加、BuildSystem 再設計が無い。
4. `pwsh tools/contract-audit.ps1` が exit 0。
5. Phase C で Editor を閉じ、`pwsh tools/run-tests.ps1` が 1 件以上実行かつ failed 0。0 件は失敗。
6. World Workspace の Editor テスト（`OneStarMaker.Tests.Editor` の WorldAuthoring）が落ちない。
7. 人間 smoke: Spring 起動、距離 streaming、Full / Whitebox 切替、World Workspace の代表 open、DebugSocket / telemetry 起動、Console に migration 由来の新しい Error / Exception が無い。
8. 可能なら Windows Player を既存 `OneStarMaker/Build/Build Player (Active Variant)` で 1 件。失敗しても「経路が 6.6 で起動不能」以外は、未実装の `firstSceneIdentify` 読者を直して通さない。
9. URP の代表画面に明らかな magenta / missing material が無い。
10. Content Directory build / register は未実施。Addressables group の CD schema 変換も未実施。
11. 公開面の現況版表記（`AGENTS.md`、`README.md`、`unity/Assets/README.md`）が 6.6 pin に更新されている。
12. 次の CD0 を 6.6 Editor 上で開始できること。判定は「6.6 で project が開き、公式 CD API が Editor に存在する」。CD を呼んだことや spike コードを残したことは条件にしない。

### 本文へ転記した実装制約

- Phase B は Unity.exe を起動しない。初回 open / package migration / serialized asset upgrade は人間が Hub で 6000.6.0f1 を入れ、専用 worktree を開いて行う。
- 人間の first-open session に無関係な Scene 編集を混ぜない。
- Editor が開いた後、agent は `unity status` が `ready` のときだけ `unity command` / `unity eval` で接続してよい。
- 許可する source change は、6.6 API / package 互換修復、compiler error / obsolete-as-error、namespace / asmdef 名の不可避な追随、避けられない serialized setting、公開面の版表記。
- asmdef 参照を「6.6 になったから」で足さない。core package 化で assembly 名が変わって compile が死ぬ場合だけ、最小差分で直し、HANDOFF に記録する。
- Addressables 2.11.2 は legacy build path を当面動かすための migration dependency。retirement しない。
- SBP 3.0.3 は Addressables の transitive。直接 pin を増やさない。
- unused package を整理しない。
- テストに `Task.Delay` / `Thread.Sleep` を足さない。

### 未決事項

- Phase B 開始日に Hub 上の最新 6000.6.x f-release が 6000.6.0f1 以外かどうか。**人間が一度だけ決める。**
- first-open が実際にどの package を core / builtin へ寄せるか。表は予想。lock が正。
- 661 Scene の YAML が first-open で何件書き換わるか。大量なら人間が「含める / 捨てて Library だけ残す / Phase A 再開」を決める。
- Addressables 2.11.2 + SBP 3.0.3 で `VariantFilteringBuildScript` が compile するか。壊れて修復コストが高い場合は、削除せず Phase A を reopen して人間判断。
- `com.unity.pipeline` 0.4.0-exp.1 が 6.6 で resolve するか。
- Managed Code Variant の Player 設定を U66 で触るか。リポジトリは `DEVELOPMENT_BUILD` を使っていない。既定 Release のままにし、URP debug overlay が Development Player で消えることだけ smoke で見る。設定変更が必要なら Phase A 再開。
- 信頼できる 6.5 baseline の全件テスト結果が手元に無い。Phase C は 6.6 結果を「前回比」ではなく絶対ゲート（実行 > 0 かつ failed 0）で見る。

---

## 3. 責務マップ

U66 は機能クラスを新設しない。変更理由は「6.6 で現行契約を再び成立させる」ただ一つ。新しい Policy / orchestration / 公開 API を置かない。

| ファイル | 責務 | 変更理由 | 所有者 / 寿命 | 依存 | 公開面 | テスト境界 | 行数 / 増分 | 分割判断 |
|---|---|---|---|---|---|---|---|---|
| `unity/ProjectSettings/ProjectVersion.txt` | Editor pin | 6.6 へ更新 | 人間 first-open | なし | 現況の正本 | 目視 | 2 / 0 | 非分割。単一設定 |
| `unity/Packages/manifest.json` | 直接依存宣言 | 6.6 line と明示 pin の分離 | 人間 + UPM | registry / git | 依存契約 | resolve 成否 | 63 / 小さい | 非分割 |
| `unity/Packages/packages-lock.json` | 解決結果 | Editor が書く | UPM | manifest | なし | 手捏造しない | 670 / 変動大 | 非分割妥当。生成物 |
| `unity/ProjectSettings/*.asset` のうち version / package / graphics 関連 | Editor 設定 | first-open が書いた unavoidable 分だけ | Editor | Unity 設定 | なし | smoke | 最大 950 | 非分割。機能追加ではない。無関係な設定書き換えは戻す |
| `unity/Assets/Settings/*RP*.asset` | URP 資産 | 17.6 automatic upgrade が入った場合だけ | App 設定資産 | URP | なし | 描画 smoke | 52〜449 | 非分割。upgrade 差分の隔離確認 |
| `unity/Assets/AddressableAssetsData/*` | Addressables 設定 | Editor が機械的に書いた場合だけ | Editor 資産 | Addressables 2.x | なし | compile + 既存 build 経路 | 118 ほか | 非分割。group 再設計禁止 |
| Unity 側 `.cs`（発生時のみ） | 互換修復 | compile / obsolete-as-error | 既存所有者のまま | 既存依存のまま | 公開 API を増やさない | 既存テスト | 予想 < 50% | 新クラスを作らず、壊れた呼び出しだけ直す。500 行警報が出たら Phase A 再開 |
| `*.asmdef`（発生時のみ） | 参照名の追随 | core package の assembly 名変更時だけ | 既存境界 | Game → Framework を維持 | 参照追加は最小 | contract-audit 検査5 | 12 ファイル | 参照追加は停止条件に近い。名前変更だけ可 |
| `AGENTS.md` / `README.md` / `unity/Assets/README.md` | 現況版表記 | harvest | 公開面 | なし | 今この瞬間に真 | docs-audit | 48 / 117 / 179 | 非分割。版番号だけ |
| `docs/README.md` | 作業台一覧 | 本 HANDOFF を現況に載せる | 公開面 | handoff | 今この瞬間に真 | docs-audit | 65 / +数行 | 非分割 |

行数警報: `packages-lock.json` と大量 YAML は生成・設定差であり、責務増加ではない。機能クラスが 50% 増えるなら配置が誤っているので止める。

### package migration table

方針列の意味:

- **accept-editor**: 6.6 builtin / core に寄せる。first-open の lock を正とする
- **upgrade-same-major**: 明示 pin を 6.6 recommended へ上げる
- **pin**: 現在値を維持。compile が要求しない限り触らない
- **do-not-downgrade**: 6.6 template より新しい。下げない
- **do-not-remove**: unused でも U66 で消さない

| package | 現在 | 6.6 で予想される値 | 方針 | 根拠 / リスク |
|---|---:|---:|---|---|
| Editor | 6000.5.0f1 | **6000.6.0f1** | pin 一度だけ | 公式 2026-08-31。6.6.1f1 は公開ページ無し |
| `com.unity.addressables` | 2.9.1 | 2.11.2 | upgrade-same-major | 6.6 bundled。CD schema が載っても変換しない。SBP 3.0.3 を引き込む |
| `com.unity.scriptablebuildpipeline` | 2.6.1 | 3.0.3 | accept-editor（transitive） | **major**。Addressables packed build の最大リスク |
| `com.unity.render-pipelines.universal` | 17.5.0 | 17.6.0 | accept-editor | 描画回帰必須。`FIRST_BIT_LOW` obsolete |
| `com.unity.render-pipelines.core` | 17.5.0 | 17.6.0 | accept-editor | Terrain 依存削除。本プロジェクトは `modules.terrain` を既に直接持つ |
| `com.unity.shadergraph` | 17.5.0 | 17.6.0 | accept-editor | URP と同梱 |
| `com.unity.inputsystem` | 1.19.0 | 1.20.0 | upgrade-same-major | compile + `FlyController` / Input System UI smoke |
| `com.unity.ai.navigation` | 2.0.13 | 2.0.14 | upgrade-same-major | compile / 代表 Scene |
| `com.unity.test-framework` | 1.7.0 | 1.8.0 | accept-editor | **計画書欠落。最高リスク。** `run-tests.ps1` の XML / 起動 |
| `com.unity.ugui` | 2.5.0 | 2.6.0 | accept-editor | UI テストあり。計画書欠落 |
| `com.unity.collections` | 6.5.0 | 6.6.0 | accept-editor | LitMotion の transitive。計画書欠落 |
| `com.unity.test-framework.performance` | 3.5.0 | 6.6.0 / core | accept-editor | 公式 Upgrade Guide: core package。二重インストールに注意 |
| `com.unity.cinemachine` | 3.1.7 | 6.6.0 core | accept-editor | CM3 の Editor 同梱。CM2 移行ではない。`Unity.Cinemachine` 参照維持 |
| `com.unity.timeline` | 1.8.12 | 6.6.0 core | accept-editor | core package 化。機能再設計しない |
| `com.unity.collab-proxy` | 2.12.4 | 2.13.6 | accept-editor（Editor が要求したら） | ツール。拒否しない |
| `com.unity.ide.rider` | 3.0.39 | Editor 次第 | pin / accept-editor | ツール |
| `com.unity.ide.visualstudio` | 2.0.26 | Editor 次第 | pin / accept-editor | ツール |
| `com.unity.ads` | 4.16.4 | 4.19.0 | pin | unused。上げない。消さない |
| `com.unity.analytics` | 3.8.2 | deprecated | pin | 移行しない。消さない |
| `com.unity.purchasing` | 5.4.2 | template は 4.15.0 | do-not-downgrade | 5.x を維持 |
| `com.unity.visualscripting` | 1.9.11 | 1.9.12 | pin | unused |
| `com.unity.xr.legacyinputhelpers` | 3.0.1 | 不明 | pin | unused |
| `com.unity.multiplayer.center` | 1.0.1 | builtin | pin | unused |
| `com.unity.pipeline` | 0.4.0-exp.1 | 不明 | pin | **計画書欠落。** Editor command 面。壊れたら停止 |
| `com.unity.project-auditor-rules` | 1.0.3 | 不明 | pin | Auditor 設定。触らない |
| LitMotion / UniTask / CsprojModifier / NuGetForUnity | lock hash 上記 | 同じ hash | pin | git URL を動かさない |
| NuGet 一式 | packages.config | 同じ | pin | bump しない |

6.6 Upgrade Guide で U66 が意識する非 package 項目:

| 項目 | 扱い |
|---|---|
| YAML word wrap 廃止 | 大量 Scene 差分は停止。`ForceReserializeAssets` しない |
| Dynamic batching obsolete | 設定が警告になるだけなら触らない。描画が壊れたら記録 |
| Rendering Debugger legacy API | 使用無し。出てきたら互換修復のみ |
| `DEVELOPMENT_BUILD` / `UNITY_64` | 使用無し。一括置換しない。warnings-as-errors で初めて直す |
| Managed Code Variant 既定 Release | Player smoke で URP debug の欠落を見る。設定は原則触らない |
| Dictionary 標準シリアライズ | 既存 wrapper を移行しない |
| UXML Factory/Traits 削除 | 使用無し |
| `DefaultEventSystem.LegacyInputProcessor` 削除 | 使用無し |
| Android GLES 3.1 / Adaptive icon | 対象外 |

---

## 4. 実装計画

### 変更対象

- `ProjectVersion.txt`
- `Packages/manifest.json` / `packages-lock.json`
- first-open が書いた unavoidable な `ProjectSettings` / URP / Addressables 設定
- compile を戻す最小 C# / asmdef
- 公開面の版表記
- 本 HANDOFF の Phase B 欄（実装後）

### 順序

1. A2 独立レビュー → A3 凍結。**凍結前に Phase B を始めない。**
2. 専用 branch / worktree。base は凍結時点の `develop`。
3. 人間が Unity Hub で 6000.6.0f1 を install。newer 6000.6.x f-release があれば本書の pin を更新してから進める。
4. 人間がその worktree を初回 open し、Unity の project / package migration を実行する。
5. 生成された tracked 差分を分類する。  
   - 採用: version / package / 不可避設定  
   - 差し戻し: 無関係な Scene 編集、BuildSystem 資産の意味変更  
   - 停止: 数百 Scene の YAML、package resolver 不能、third-party 非互換
6. compile error だけ直す。Editor が開いているなら `unity status` 後に接続してよい。
7. Phase B は `pwsh tools/contract-audit.ps1` を実行し、テスト未実行を明記する。
8. Phase C が Editor を閉じて `pwsh tools/run-tests.ps1` と人間 smoke を行う。

### Phase B の停止条件

次が出たら BuildSystem rewrite へ逃げず、実装を止めて Phase A に返す。

- third-party git / NuGet が 6.6 incompatible
- asmdef 参照を足さないと Game → Framework または Runtime / Editor を維持できない
- Scene serialization の大量書き換え、または破損
- URP upgrade で代表描画が壊れる
- `run-tests.ps1` / Test Framework 1.8.0 でテスト基盤が起動不能
- package resolver が incompatible dependency を要求
- `com.unity.pipeline` が壊れ、既存 Editor command 契約が使えない
- Addressables 2.11.2 / SBP 3.0.3 で既存 packed build 経路が compile 不能、または修復が再設計になる
- Cinemachine / Timeline の core 化で API / namespace が壊れ、互換修復では足りない
- HANDOFF に無い状態、依存、所有者、寿命、公開 API が必要
- Content Directory schema を有効化しないと Addressables が動かないと Editor が要求する

Addressables だけが壊れ、修復コストが高い場合でも、Phase B が独断で削除しない。Phase A を reopen し、「legacy Addressables を U66 で一時維持するか / CD0 を前倒しするか」を人間が決める。

### 対象外を維持する方法

- PR 説明と diff 点検で、`Editor/Build/Variants/` の意味変更、CD API 新規参照、NuGet / git hash、unused package 削除を落とす。
- Content Directory の型名（`BuildPipeline.BuildContentDirectory`、`ContentLoadManager`、`LoadableSceneId`）が本番コードに増えていないことを Phase C で機械確認する。テストやコメントの言及は可。呼び出しは不可。
- 661 `.unity` の変更件数を `git diff --name-only` で数える。数十を超えたら停止 cond。

### rollback / failure handling

- 専用 branch のみ。`develop` へ直接 commit しない。
- first-open が失敗したら worktree を捨て、`Library/` を残したまま branch を捨ててよい。`unity/Library/` と `unity/Temp/UnityLockfile` はブランチを捨てても残る。
- 部分適用した manifest を `develop` に戻す作業は、失敗した branch 上で revert し、新しい revision の Phase A を開く。
- 人間のマシンに 6000.5.0f1 を残す。rollback は「6.6 branch を使わない」であり、6.5 の上で BuildSystem 再設計を始めない。

---

## 5. テストとレビュー計画

### 単体テスト

新しい中核ロジックは無い。互換修復が入った既存テストだけを再実行する。テストを足すのは、6.6 で壊れた既存契約を回帰固定する場合に限る。

### 統合・Unity テスト

Phase C:

1. Editor を閉じる。
2. `pwsh tools/run-tests.ps1`（全 EditMode。`-Filter` は失敗切り分けだけ）。
3. 関連 Editor テスト。特に `WorldWorkspace*` / `WorldCompanion*`、Build 系（意味変更なしの compile 回帰）、Camera / Cinemachine、AssetManagement、Streaming、SceneDirector。
4. 人間 smoke（Play）:
   - Spring 起動（スポーン Spring `(0,4)`）
   - 距離 streaming の候補出し / load / unload
   - Full / Whitebox runtime 選択
   - World Workspace の代表 Cell を Full と Whitebox で open
   - DebugSocket 接続または少なくとも起動時の compile / runtime 例外が無いこと
5. 可能なら Windows Player 1 件（既存 Active Variant 経路）。
6. URP 代表画面。magenta / missing shader なし。
7. Console の新しい Error / Exception なし。

Content Directory build は Phase C の受け入れに入れない。

### 機械検査

- `pwsh tools/contract-audit.ps1`
- `pwsh tools/docs-audit.ps1`（版表記と handoff 一覧を触ったため）
- Phase C で `BuildPipeline.BuildContentDirectory` / `ContentLoadManager` / `LoadableSceneId` の本番呼び出しが差分に無いこと

### レビュー担当

- A0/A1 主担当: Cursor Cloud / Grok。入力は `develop@3244f36`、計画書 v3、公式 6000.6.0f1 / Upgrade Guide / What's New / Content Directories manual。
- A2 独立レビュー（必須。high risk）:
  1. アーキテクチャゲート。責務混在、Game → Framework、Editor I/O と package policy の分離、661 Scene YAML の扱い。初稿を見せてよい。
  2. package / Editor migration。表の accept-editor vs pin、SBP 3、Test Framework 1.8、core package 化。同じ入力版。互いの指摘を渡さない。
  3. 可能なら A0 だけから代替構成（例: 6.6.0f1 ではなく次の patch を待つ、または Addressables を先に上げない）。初稿を見せない。
- A3: 人間。採否を記録して凍結。
- Phase C: Phase B と異なるモデル。
- C' 予約: Phase A / B / C に使っていない系列。本 A1 は Grok 系を使った。C' は Claude または GPT 系、あるいは人間。A2 で全系列を使い切らない。

独立性の強化条件を満たせない場合は、`独立性制約あり` として理由を残し、未実施にはしない。

---

## 6. Phase B 実装結果

- 実装: 未到達
- HANDOFF との差:
- 未実行:
- implementation head commit:
- Phase B 担当・モデル・ベンダー:

---

## 7. Phase C

- evidence bundle id / hash:
- 構造適合:
- findings:
- テスト結果:
- 未確認事項:
- 担当・モデル:

---

## 8. Phase C'

- 担当方式:
- blind audit bundle id / hash:
- 確認範囲・方法:
- 判定: 未実施
- findings:
- 残存リスク:
- 監査できなかった範囲:
- 独立性:
- Phase C 結論の事前閲覧・設計実装への関与:
- 担当・モデル:

---

## 9. Phase D

- C / C' の突合:
- マージ判断:
- harvest: `ProjectVersion.txt`、package 現況、`AGENTS.md` / README の版表記。恒久化した制約だけ Architecture へ。本 HANDOFF は harvest 後に削除。
- 削除確認:

---

## 10. 既知のリスク

| リスク | 深刻度 | 扱い |
|---|---|---|
| Test Framework 1.7.0 → 1.8.0 で `run-tests.ps1` が壊れる | high | 停止条件。テスト基盤を U66 の外で作り直さない |
| SBP 2.6.1 → 3.0.3 が Addressables packed build を壊す | high | 互換修復まで。再設計なら Phase A reopen |
| 661 Scene の YAML ノイズ | high | 大量差分は停止。一括 reserialize しない |
| Cinemachine / Timeline core 化 | normal | CM3 済みなので通常は影響なし。壊れたら停止 |
| URP 17.6 描画差 | normal | smoke。shader macro obsolete |
| `com.unity.pipeline` exp | high | Editor 作業契約。壊れたら停止 |
| Addressables 2.11.2 が CD schema を見せる | normal | 使わない。変換 wizard を踏まない |
| Managed Code Variant 既定 Release | low | Player smoke で観測。設定変更は Phase A |
| git / NuGet 非互換 | high | bump せず停止 |
| 6.5 全件テスト証拠が無い | normal | 絶対ゲートで見る |
| 6.6.1f1 が直後に出る | low | Phase B 開始時に一度だけ確認 |

---

## 11. CD0 への受け渡し（仕様推測はしない）

U66 が CD0 に渡してよい事実は次だけ。

- 6.6 Editor で本 repository が開く。
- 公式 manual / Scripting API 上、`BuildPipeline.BuildContentDirectory` と `ContentLoadManager.RegisterContentDirectory` が 6.6 に存在する。
- Content Directories は local only。remote は独自 distribution か AssetBundle。
- 同梱 Addressables は 2.11.2。CD schema が UI にあっても、U66 は使っていない。

CD0 が自分で実測すべきこと（U66 が答えを書かない）:

- generated `LoadableSceneId` の authoring
- root ScriptableObject と AssetDatabase mutation
- BuildReport / `previousBuildReportDirectories`
- register / unregister と Loadable lifetime
- copy / download した directory の path portability

---

## 12. 外部根拠（A1 調査時点）

- Unity 6000.6.0f1: https://unity.com/releases/editor/whats-new/6000.6.0f1
- Upgrade to Unity 6.6: https://docs.unity3d.com/6000.6/Documentation/Manual/UpgradeGuideUnity66.html
- New in Unity 6.6: https://docs.unity3d.com/6000.6/Documentation/Manual/WhatsNewUnity66.html
- Content Directories: https://docs.unity3d.com/6000.6/Documentation/Manual/content-directories.html
- `BuildPipeline.BuildContentDirectory`: https://docs.unity3d.com/6000.6/Documentation/ScriptReference/BuildPipeline.BuildContentDirectory.html
- `6000.6.1f1` whats-new: 2026-09-12 時点 404
