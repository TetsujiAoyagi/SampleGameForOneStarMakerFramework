# RET — 旧 Addressables BuildSystem の廃止

## 0. メタデータ

- type: `slice`
- status: `B`
- branch: `codex/ret-retire-addressables-build`
- implementation base commit: `356767ff082ae7ddf5f8c9ce0c992b7e2185178f`
- implementation head commit: `a77cbaf4a2cf29e86468d57fad28d4018221d336`
- risk: `high`（Addressables、公開 API、serialized 参照、所有者・寿命）
- owner: RET 主担当 / Cursor Grok 4.6
- created: 2026-09-20
- expires: RET Phase D マージ。2026-10-20 に未マージなら再確認
- harvest to: Architecture `04-app-startup.md`、`13-resource-system.md`、`18-asset-description.md`、`20-variant-checkout-workflow.md`。program 現在地は RET Phase D で harvest して本書を削除する
- Phase A snapshot path: `artifacts/bs2b/ret-evidence/phase-a/snapshot.md`（ignored。PR に入れない）
- Phase A snapshot generated at: `2026-09-20T13:18:50.8198390Z`
- Phase A snapshot hash: `1fda4f266d561c57f968c6aeab43e4e2655fa1d1aad63652575be33eb75d76a9`
- Phase B result snapshot path: `artifacts/bs2b/ret-evidence/phase-b/result.md`（ignored。PR に入れない）
- Phase B result snapshot generated at: `2026-09-20T13:50:00Z`
- Phase B result snapshot hash: `889b1e6ce422e724c5181608f4b989cc02ca4db426019b7015719cf71a908c5c`
- Phase C evidence / C' blind bundle: 未到達

本文へ転記した常時制約: Game → Framework の一方向。asmdef 参照の無断追加禁止。アセットは `IAssetManagement` と `AssetOwner`。SceneState の既存14値は減らさず並べ替えない。公開ログは `ILogger<T>`。Update は `UpdateSystemRuntime`。1システムの例外で他を止めない。Editor コードを Runtime アセンブリに置かない。Unity 側 C# は先頭 `#nullable enable`、`record` 禁止、破棄可能 Unity Object は `== null` / `!= null`。テストに `Task.Delay` / `Thread.Sleep` 禁止。`unity test` / `unity run` 禁止。テストは `pwsh tools/run-tests.ps1`（Windows は sandbox 外）。参照 0 を削除理由にしない。PR base は develop。Phase D merge はユーザー明示まで禁止。cursor-agent は Grok 系のみ。ASTRA は使わない。DIST の install / known-good / OS lease / source-free Player を作り直さない。

## 1. 目的と対象外

- 目的: 新経路の利用者を維持したまま、置換済みの旧 Addressables build・開発 workflow を取り残しなく廃止する。
- 対象外: 配信運用拡張（signing、latest channel、CDN、delta/resume、他 OS、互換 Player の revision 横断更新）。Mesh 等の非 Scene 実需要。DIST 判定後続メモの自動拡張（process-probe 追加 exclusive-read `IOException`、第2 revision が coordinator 新規 publish ではないこと、Editor Play 中拒否の実 Play / Domain Reload 未実測）。WorldCompanion / Addressables full-suite flake 修正。Addressables package の即全廃。完了済み HANDOFF の復活と PRE の追跡追加。本セッションでの判定 C / C' / merge。実装 B は凍結後の別セッション。
- 現況: `develop` `356767f`（PR #68 DIST マージ済み）。DIST 実装 head `3a95c03`。install / known-good / OS lease と BS4 Player identity は成立。通常 Editor Play は Addressables 既定で source の UICommon / SceneResourceMap に依存。通常 Play 停止は `ReleaseAll` + `BeginSynchronousShutdown` で、明示 `CloseAsync` と同等の完全 drain を待たない。旧 Variants / Hybrid / Remote / Checkout / Player overlay は未置換。package `com.unity.addressables` 2.11.2 は `AddressableBackend`、`AssetReference`、World オーサリング、Player の `DoNotBuildWithPlayer` が使う。

## 2. 意思決定と受け入れ境界

- このスライスが答える問い: 新経路の利用者を維持したまま、置換済みの旧 build・開発 workflow を取り残しなく廃止できるか。
- 進める最低条件:
  - M1. Content Directory build（代表 Spring Full）と DIST install と known-good と OS lease 競合拒否が退行しない。transport v1 / install レイアウト / lease 意味を変えない。
  - M2. 通常 Editor Play が verified installed revision から source なしで UICommon ready → directory 登録 → 論理初回 Scene stable まで進む。sourceFiles Missing でも起動する。未選択・不正 pair・bootstrap entry 欠損は Addressables に戻らず失敗する。
  - M3. 通常 Windows x64 IL2CPP High Player（BS4 identity）が bootstrap → 登録 → 初回 Scene → 代表 Prefab 往復 → 終了できる。`VariantPlayerBuild` を使わない。
  - M4. 通常 Editor Play 停止と通常 Player quit で directory の完全 drain が完了し、同一 revision の delete exclusive lease が取得できる。Play 停止で Addressables `UnloadSceneAsync` を呼ばない。
  - M5. 旧 BuildSystem の通常経路が到達不能。メニュー/CLI は置換案内のみ。隠れた InitializeOnLoad catalog 注入と Player overlay が通常 Play/build を駆動しない。置換済み / 意図的廃止 / 残存の表が公開文書と一致する。
  - M6. Addressables package は残す。残存 owner を文書化する。package 削除を成功条件にしない。
  - M7. Architecture §4 / §13 / §18 / §20 が現況。§20 の旧手順を通常手順として書かない。`docs-audit` と `contract-audit` が通る。
- 受け入れ条件（最低条件の観測可能な詳細）:
  - `content:runtimeMode` 未指定の Editor Play は Delivery「Use For Next Play」相当の verified pair が無ければ BeforeSceneLoad で失敗する。pair があれば directory。明示 `addressables` だけが互換口。未知 mode は失敗。known-good の自動適用はしない。
  - SampleGame は季節 policy を変えず、既存 `compose` フックで bootstrap Scene（`Assets/OneStarMaker/Scenes/UISystem/UIScene.unity`）と `SceneResourceMap` を共通必須 content として合成する。Spring Full の index に loadable entry がある。Unity Content Directory へ DIST 用の sibling ファイルを混ぜない。
  - 通常 Editor directory Play は `GetUICommonPrefabAddress` / Addressables Map ロードを使わない。既存 `LoadContentSceneAsync` / `LoadContentAssetAsync` だけを使う。logical key は SampleGame 固定（`samplegame:bootstrap:uicommon` と `samplegame:bootstrap:scene-resource-map`）。
  - Play 停止の入口は既存 Runtime `Application.quitting` / `ReleaseAll` のみ。`playModeStateChanged` も新しい公開 `IAssetManagement` も足さない。`AssetManagement.ReleaseAll` のあと、internal の play-stop 完了待ちが `StopAndDrain`・shutdown scene terminal・cache 退避・unregister・OS read lease 解放まで終わる。Scene は既存 `ReleaseSceneAfterUnityShutdown` を使い `UnloadSceneAsync` しない。
  - 廃止メニュー 4 つと `tools/rebuild-remote.ps1` / `tools/serve-addressables.ps1` は案内を出し、group snapshot / app-config overlay / Remote プロファイル生成 / catalog 追加ロードを実行しない。
  - `SceneVariantEditorInjector` と明示 addressables mode の SceneVariant テストは残す。directory の representation は `content:representation` のみ。profile SceneVariant を directory へ暗黙入力しない。
  - WorldCompanion の Addressables 登録は残存。flake 修正をしない。
  - 新 asmdef なし。
- ここでは答えない問いと所有する後続:
  - 配信運用拡張: program が DIST v1 に増やさない範囲。未作成の運用 slice
  - 非 Scene Description / Mesh: program「非Scene拡張」
  - 同一 session の実行中表現切替: Architecture §18
  - DIST 判定メモ 3 件: 後続観測。RET 完了バーにしない
  - WorldCompanion flake: 本スライスの置換対象でなければ所有しない
  - Addressables package 全廃と `AssetReference` 置換: RET Phase D で program 残課題へ。本スライスでは不要化未確認
  - 部分 Checkout 開発の再燃: RET Phase D の残課題
- 判定定義: M1〜M7 充足で GO。必須経路未実行は NO-GO。CONDITIONAL ACCEPT は使わない。新しい public API / owner / state / 依存 / fail-closed が必要なら A revision。
- 停止規則: 進める最低条件を満たし、現在の問いに致命的な反証がなければ GO で終了する。最低条件未達のまま終了しない。
- A3 後の例外承認: なし
- 未決事項: Play 停止待ちが quit 中に死鎖することが実装で判明した場合は A 再開（寿命契約）。それ以外の新事実は phases-and-handoff の分類に従う。

### A3 で閉じた設計

U1 既定起動: 未指定を fail-closed。明示 `addressables` が互換・rollback。app-config の未指定を directory に書き換えない。Editor の通常接続は既存 PlayBridge の verified pair。known-good 自動適用はしない。

U2 Editor bootstrap: DIST 契約は不変。content subtree の Scene/Object entry として bootstrap を載せる。transport `files` 規則と install レイアウトを変えない。

U3 SceneVariant: profile の SceneVariant は互換 addressables 用に残す。directory は `content:representation`。

U4 Addressables 入口: `IAssetManagement` の既存 Addressables 署名と `AddressableBackend` を残す。通常起動は使わない。

U5 旧メニュー: 案内付き no-op。本体 mutation は呼ばない。即ファイル削除だけにしない。

U6 drain: Runtime `ReleaseAll` が待つ。Editor 専用 teardown クラスを作らない。

U7 AddressableAssetsData: tracked 設定は残す。OSM 通常経路から VariantFiltering を駆動しない。active builder を Unity 既定へ戻す Editor 設定 mutation は許可。

A2 代替構成（Session Binding）のうち、PlayBridge を通常 Editor 接続の権威にすることは採用。未指定＝addressables を serialized 正本のまま残す案、known-good 自動 Binding、Editor 専用 config provider、Bootstrap Descriptor JSON、`EditorPlayTeardownCoordinator` は不採用。

## 3. 責務マップ

行数は着手時概数。500 行 / 3 責務 / 50% 増は分割判断の警報。

### 起動と寿命（new-path）

| ファイル | 現在行 | 予想 | 責務 | 所有者・寿命 | 依存 | 公開面 | テスト | 分割 |
|---|---|---|---|---|---|---|---|---|
| `AbstractApplicationInitializer.cs` | ~1128 | +20〜40 | 未指定 mode の fail-closed。directory 時に Content 入口で UICommon/Map を読む。`ReleaseAll` から AM の play-stop 完了を待つ | App | AM / session フィールド | protected override 追加は既存で足りる前提 | fail-closed の既存 Bootstrap テスト拡張 | 既存 500 超。orchestration の分岐のみ。drain 本体は AM。非分割妥当 |
| `AppInitializer.cs` | 既存 | +20 | SampleGame の bootstrap logical key と directory 分岐 | Game App | Framework 公開 Content 入口 | 新公開 API なし | 起動テスト | 非分割 |
| `AssetManagement.cs` | 398 | +10〜20 | `ReleaseAll` は現行どおり Scene を `ReleaseSceneAfterUnityShutdown`。続けて play-stop 完了を所有 | AM / AssetOwner | session port | 新 public メソッドなし。internal 完了待ち | Unity なしで台帳と port fake | 非分割 |
| `AssetManagement.ContentDirectory.cs` | 147 | +40〜70 | play-stop: pending drain、shutdown scene terminal 待ち、cache 退避、`CloseAsync` 相当の unregister。明示 close の `UnloadSceneAsync` 経路は維持 | AM orchestration | `IContentDirectoryBackend` | internal | fake backend で Unload スキップと lease | drain を Initializer や session 単独に移さない。非分割妥当 |
| `IContentDirectoryBackend.cs` | 33 | +0〜8 | shutdown drain 完了待ちが既存メソッドで足りなければ internal 追加 | session port | なし | internal のみ | fake | 公開 IAssetManagement に出さない |
| `ContentDirectorySession.cs` | 既存 | +10〜30 | native terminal、unregister、OS lease。完全 drain の owner 台帳列挙は持たない | session / OS lease | Unity CD / gate | 既存 | 既存 session テスト拡張 | native 終端は非分割 |
| `ContentDeliveryPlayBridge.cs` | 98 | +10〜20 | verified pair を次 Play へ。未選択を通常既定にしない | Editor process env | DIST public consumer | internal | EditMode | 非分割 |
| `ContentDeliveryWindow.cs` | 168 | +10 | source 必須案内を「編集には source、通常 Play には install」へ直す | Editor UI | PlayBridge | メニュー既存 | 文言と Apply/Reset | 非分割 |
| `PlayerBuildProjectMutation.cs` | 148 | 0 | Addressables を Player に載せない | build transaction | Addressables Settings | なし | 既存 | 非分割 |

### bootstrap content 合成（A2 B1 採用）

| ファイル | 現在行 | 予想 | 責務 | 所有者 | 依存 | テスト | 分割 |
|---|---|---|---|---|---|---|---|
| `SeasonSceneSelectionPolicy.cs` | 243 | 0 | 季節と表現の選択。bootstrap を知らない | 呼び出しごとの純関数 | Selection core のみ | 既存 | **変更しない** |
| `SampleGameBootstrapContentComposer.cs`（新） | 0→80〜120 | Bs4FixtureComposer と同型。plan/snapshot へ UICommon Scene と Map を ExactlyOne で合成し衝突を拒否 | 一 build 要求 | Selection / Materialization の純データ | fake snapshot。AssetDatabase なし | Bs4 合成と別ファイル。季節 policy に混ぜない |
| `SampleGameContentBuild.cs` | 106 | +20〜40 | 既知 path から GUID/閉包を取り、季節選択のあと composer を `compose` に渡す | Editor orchestration | AssetDatabase / materializer gateway | 統合は既存 build テスト。純合成は composer 側 | I/O はここ。policy に落とさない |
| bootstrap 2 件の閉包 | 既存 gateway | 既存 `AssetDatabaseGateway` / dependency snapshot を流用 | Editor I/O | 新汎用 Helper 禁止 | materializer テストの流用 | 新 gateway クラスを作らない |

### 残す Runtime / serialized

| ファイル | 現在行 | 予想 | 分類 | 判断 |
|---|---|---|---|---|
| `AddressableBackend.cs` | 176 | 0。`TryLoadRemoteCatalogAsync` は Initializer から削除し、backend へ移さない | 残存 | catalog 追加ロードは意図的廃止。backend は通常 asset I/O のみ。非分割 |
| `AssetPayload.cs` / `SceneAssetDescription.cs` | 既存 | 0 | 残存 serialized AssetReference | フィールド削除は package 全廃 slice |
| `BuildVariantProfile.cs` | 115 | 0〜10 | SceneVariant 残す。whitelist/remote は通常から読まない | フィールドは残す |
| `DeveloperVariantSettings.cs` / `Provider` | 94 / 159 | 0〜15 | SceneVariant 選択 UI は残す。RemoteCatalog 表示は互換説明 | 非分割 |
| `SceneVariantEditorInjector.cs` | 23 | 0 | 互換 addressables 用 | 非分割 |
| `RemoteCatalogRuntimeBridge.cs` | 52 | 削除または空 | 意図的廃止 | injector と一緒に通常から切断。参照 0 だけを理由にしない。案内コミットで役割を書く |
| WorldCompanion Addressables 登録 | 既存 | 0 | 残存 | flake 修正しない |

### 旧 BuildSystem（各ファイル。案内 no-op。mutation 削除）

いずれも Editor。通常経路から切断。テストは「呼ばれても settings / app-config / groups を変えない」。

| ファイル | 現在行 | 置換 | 分類 |
|---|---|---|---|
| `VariantWhitelistBuilder.cs` | 172 | `SeasonSceneSelectionPolicy` + bootstrap composer | 意図的廃止（通常） |
| `AddressablesGroupSnapshot.cs` | 307 | なし（一時 mutation 不要） | 意図的廃止 |
| `AddressablesGroupSyncFilter.cs` | 68 | 同上 | 意図的廃止 |
| `VariantFilteringBuildScript.cs` | 258 | `SampleGameContentBuild` | 意図的廃止 |
| `VariantHybridPlayModeScript.cs` | 261 | Delivery + directory Play | 意図的廃止 |
| `VariantHybridPlayModeRegistrar.cs` | 88 | メニュー案内 → Delivery | 意図的廃止 |
| `VariantRemoteBuildSetup.cs` | 479 | Content publish + Delivery | 意図的廃止。ファイル単位で案内化 |
| `VariantRemoteBuildBatch.cs` | 180 | 同上 | 意図的廃止 |
| `VariantPlayerBuild.cs` | 192 | BS4 Player coordinator | 意図的廃止 |
| `VariantCheckoutReportWindow.cs` | 354 | `ContentSourceAdvisor` / Delivery | 意図的廃止 |
| `AssetDependencyClosure.cs` | 150 | 新経路の dependency snapshot | 意図的廃止。流用が必要なら A 再開（新公開 API） |
| `RemoteCatalogEditorInjector.cs` | 37 | なし | 意図的廃止 |
| `LocalRevisionEditorInjector.cs` | 84 | DIST 鮮度は expected revision 一致 | 意図的廃止 |
| `tools/rebuild-remote.ps1` | — | Content build + publish | 意図的廃止。失敗案内 |
| `tools/serve-addressables.ps1` | — | Delivery HTTP | 意図的廃止。失敗案内 |

`EditorDirectoryPlayBootstrap.cs` は **作らない**（A2 B4）。pair 検証は PlayBridge と Runtime BeforeSceneLoad。

### 作らないもの

- 新しい公開 `IAssetManagement` メソッド
- 新しい asmdef
- 汎用 Helpers / Managers
- DIST publisher / installer / lease のプロトコル変更
- Addressables package 削除

## 4. 実装計画

- 変更対象: 上記マップ。本番 Scene/Prefab の広範な再保存はしない。bootstrap 2 件を content に含める build は既存 Editor 入口。
- 順序:
  1. bootstrap composer と ContentBuild 接続。Spring Full に entry があることを EditMode で固定
  2. Initializer / AppInitializer の未指定 fail-closed と directory bootstrap の Content 入口
  3. AM play-stop 完了待ち。Initializer は session へ `BeginSynchronousShutdown` だけを呼ばない
  4. 旧メニュー/CLI/InitializeOnLoad を案内 + no-op
  5. 公開文書。残存 owner 表
  6. `contract-audit`。コンパイル確認は B。テストは C
- Phase B から Phase A へ差し戻す条件: DIST 契約変更が必要。新 public API / asmdef / AssetOwner / SceneState / fail-closed 理由が必要。play-stop 待ちが quit 上で完了できず所有者が AM 以外になる。bootstrap を CD entry にできず別チャネルが必要。中核判定を Unity I/O なしでテストできない。package を消さないと旧経路を切れない。
- 対象外を維持する方法: Delivery UI に運用拡張を足さない。WorldCompanion flake を触らない。DIST 後続メモを判定必須にしない。Mesh Description を足さない。参照 0 だけでファイルを消さない。

## 5. テストとレビュー計画

- 単体: runtimeMode 未指定 / 片側 pair / 未知 mode。composer の衝突と ExactlyOne。play-stop: Unload スキップ、pending 待ち、lease 解放。旧メニュー no-op（mutation port fake）。
- 差し戻し中の起点 `-Filter`: `Variant|ContentDirectory|ContentDelivery|PlayerContent|PlayerBuild|Bootstrap|SceneVariant|SeasonScene|BuildContent`
- 判定必須:
  1. 全 EditMode 回帰（空 filter）。`pwsh tools/run-tests.ps1`。Windows は sandbox 外
  2. Editor Play: source なし directory 通常 Play → 初回 Scene stable → Stop → 同一 revision の delete lease 取得
  3. 配信回帰: local または HTTP install → known-good → 使用中削除拒否
  4. Player 回帰: Windows x64 IL2CPP High、対応 identity、source-free 起動、代表 Prefab 往復、通常終了後に lease 解放
- 全 EditMode 適用除外: なし
- WorldCompanion full-suite を判定必須に足さない。空 filter に含まれる既存件は落とさない
- 機械検査: `pwsh tools/contract-audit.ps1`、`pwsh tools/docs-audit.ps1`
- A0/A1 主担当: Cursor Grok 4.6（xAI / SpaceXAI）
- A2 architecture gate: gpt-5.6-sol（OpenAI）。同一 A0+A1。指摘を他レビューへ渡していない
- A2 代替構成: Composer 2.5。A0 のみ。初稿未読
- A3 統合: 主担当 Grok 4.6。program 委任（2026-09-17）に従い採否
- C' 予約: ユーザー指示により GPT 系。Claude は利用不可。ASTRA は使わない。
- 強化条件: A は Grok + OpenAI + Composer。C' に未関与ベンダーを残せていない（独立性制約あり）。

### A2 ledger（採否）

| ID | 出典 | 判定 | 理由 |
|---|---|---|---|
| B1 季節 policy に bootstrap を混ぜない | architecture gate | 採用 | 変更理由が違う。`compose` + 新 composer。policy は変更しない |
| B2 完全 drain は AM がorchestration | architecture gate | 採用 | 現行 `CloseContentDirectoryAsync` と同じ所有。session は native/lease |
| B3 Play 停止入口を一つに固定 | architecture gate | 採用 | Runtime `ReleaseAll` のみ。`playModeStateChanged` 不採用 |
| B4 `EditorDirectoryPlayBootstrap` を作らない | architecture gate | 採用 | 検証の二重化と内部 API 公開を避ける |
| B5 old-build をファイル別に書く | architecture gate | 採用 | 本节の表 |
| 欠落ファイル AM.ContentDirectory / IContentDirectoryBackend / ContentBuild / Settings | architecture gate | 採用 | マップへ追加 |
| AddressableBackend へ catalog を移さない | architecture gate | 採用 | catalog ロードは廃止。backend 非分割 |
| fail-closed 既定 / package 残 / bootstrap を CD へ | architecture gate | 採用 | 配置だけ修正 |
| Session Binding を Play 権威にする | 代替構成 | 採用（既存 PlayBridge） | app-config 既定書き換えより、明示 pair を通常接続にする |
| 未指定＝addressables を正本のまま | 代替構成 | 不採用 | 通常が新経路という A0 最低条件に反する |
| known-good 自動 Binding | 代替構成 | 不採用 | 別 revision 誤 Play。U1 |
| Editor 専用 config provider / Descriptor JSON | 代替構成 | 不採用 | 新チャネル。既存 Content 入口と PlayBridge で足りる |
| `EditorPlayTeardownCoordinator` | 代替構成 | 不採用 | Editor→Runtime 公開 API と B3 に反する |
| signing/CDN/Mesh/package 全廃/flake | 両方 | 後続 | 対象外 |

C' 実績予定: GPT 系（ユーザー指示）。A2 architecture gate が同一ベンダーのため `独立性制約あり`。ASTRA 不使用。

## 6. Phase B 実装結果

実装した。テストは未実行（Phase C）。

- bootstrap composer を季節選択のあと `compose` で接続。logical key は `DirectoryBootstrapKeys`。
- 未指定 `content:runtimeMode` は verified pair が無ければ BeforeSceneLoad で失敗。directory Editor Play は Content 入口で UICommon / Map を読む。
- Play 停止は `ReleaseAll` のあと `CompleteContentDirectoryPlayStopAsync` を待つ。明示 close の `UnloadSceneAsync` は使わない。
- 旧メニュー 4 つ、Hybrid/Filtering、remote batch、catalog/revision injector、`rebuild-remote.ps1` / `serve-addressables.ps1` は案内 no-op。
- Architecture §4 / §13 / §18 / §20 を現況へ更新。`contract-audit` / `docs-audit` は通過。Editor Pipeline `ready`。
- B 適応: Framework は SampleGame key を知らない（protected virtual）。新 public API なし。`VariantRemoteBuildBatch` も CLI 入口のため案内終了。

未実行: 全 EditMode、Editor Play、DIST install 回帰、BS4 Player。

## 7. Phase C

未実施

## 8. Phase C'

未実施

## 9. Phase D

未実施
)
