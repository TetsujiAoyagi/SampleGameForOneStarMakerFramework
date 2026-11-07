# Content Directory 起動の facade（`ContentDirectoryStartup`）

## 0. メタデータ

このtracked文書の全Phase欄では、各成果物の取得案内を渡す既存の引渡し先・担当と状態（未生成／未保存／ローカル固定済み（未昇格）／引渡し未完了／確認済み）を、公開可能な範囲で記録する。取得に使うreference・期待packageSha256の実値、ローカルpath、内部catalog/receiptはこの文書へ転記しない。

- type: `slice`
- status: `A`（**A0 入力だけ。A1 未着手、実装キューではない**）。本書は [RUNTIME_REFACTOR_BOOTSTRAP.md](RUNTIME_REFACTOR_BOOTSTRAP.md) の A1 改訂（2026-11-07）で同書から切り出した S2 の設計素材を、program A0 [ASSET_BUILDSYSTEM_INDEPENDENCE.md](ASSET_BUILDSYSTEM_INDEPENDENCE.md) §4.2 の改訂を当てた形で預かる。素材の正本は本書だけで、BOOTSTRAP には残していない
- 切り出しの根拠: program A0 §6 D4（owner 判断、2026-11-07）。「S2（+ AC9 / AC10）を独立スライス `CONTENT_DIRECTORY_STARTUP` にする」
- 順序（D4）: (1) ASSET_CYCLE 改訂版 → (2) `BUILDSYSTEM_STANDALONE_ENTRY`（未作成） → **(3) 本スライス** → (4) asmdef スライス。(2) の Phase D より前に本書の A1 を始めない。BOOTSTRAP（S3〜S5）はこの列の外にあるが、同じ `AbstractApplicationInitializer.cs` を触るので、**本スライスと BOOTSTRAP の Phase B を同時に走らせない**
- branch: 未定（A1 の担当が決める）
- implementation base commit: 未固定。A1 の着手時に `develop` の head で測る。下の素材は base `d4861882` の実測で、(2) と BOOTSTRAP が先に入れば行番号と型の可視性が変わる
- implementation head commit: 未固定
- risk: `high`（起動順序、失敗経路、Player の receipt に現れる stage 名に触れる）
- owner: root（依頼者）
- created: 2026-11-07
- expires: 2026-12-31、または A1 の着手で本書が通常の A1 初稿に置き換わった時点。期限時は owner が更新・廃止を判断する
- harvest to: A1 で決める（候補は `unity/Assets/Docs/Architecture/04-app-startup.md` §4.2・§4.3。§4.7 の契約文は変えない）
- Phase A snapshot / Phase B result snapshot / evidence bundle / C' blind bundle の取得案内・状態: 未生成
- 引継ぎ票:
  - 次の Phase・役割: A1（初稿）。開始条件は上の順序。
  - 次の作業: §2 の「A1 の着手時にやり直すこと」を行ってから、§3 の素材で A1 初稿を書く。
  - 引き継ぐ commit: A1 着手時の `develop`。
  - 必要な権限: なし（文書のみ）。
  - 停止規則: `BUILDSYSTEM_STANDALONE_ENTRY` の Phase D より前に A1 を始めない。同じ session で Phase B に入らない。
  - 引き渡さないもの: なし。

## 1. このスライスが答える問い（A1 で確定する）

`AbstractApplicationInitializer` の起動・終了順序と派生クラス向け公開面を変えずに、Content Directory の起動（構成の解決、Addressables の封じ、session の登録・install・close、Play 停止時の完了）を `Runtime/BuildContent/` 側の public な 1 型に集め、Bootstrap から BuildContent の internal 型と `AssetManagement` の directory 用 internal メソッド 4 つへの直接参照を 0 にできるか。

位置付け（program A0 §2.2 X5、§4.3）: 越境を facade の 1 箇所に集めておけば、asmdef スライスはその 1 箇所だけを裁定すればよい。facade は BuildSystem 本体ではなく、連携部分（`ContentDirectorySession` 等）と OSM 固有の起動構成（`AppConfig` を読む `Resolve`）にまたがる。

## 2. A1 の着手時にやり直すこと

- **base の取り直し。** 下の行番号・型名・可視性は base `d4861882`。`BUILDSYSTEM_STANDALONE_ENTRY` は `ContentDirectoryIndex` や `InstalledRevisionVerifier` 等の public 入口を作る可能性があり（A0 §4.3 X6）、そうなれば facade が越える internal の一覧が変わる。BOOTSTRAP が先に入っていれば initializer の構成と行数も変わる。
- **不変条件の取り直し。** BOOTSTRAP §2.1 の I1〜I4（公開面、起動順序と stage 名、終了順序、失敗経路）と同じ形の不変条件を、着手時の base の実測で書く。BOOTSTRAP が完了して削除されていれば、git 履歴の同書と harvest 後の `04-app-startup.md` を参照する。
- **新しい A1 の規則。** 本スライスは新しく A1 を始めるので、[Phase と HANDOFF](../../.agents/skills/osm-workflow/references/phases-and-handoff.md#phase-a-の内部フロー) の利用者の入力の表、目的レビュー、分割の検討、不変条件と成立条件の主張と経路の箇条を適用する。
- **reflection で読まれる private field。** `SampleGame/Tests/Editor/Build/BuildContentDirectoryIntegrationTests.cs`（449 行目）が `_contentDirectorySession` を reflection で読む（BOOTSTRAP の A2 で判明。同書は I1 でこの名前を守る）。下の §3.3 の「フィールド 3 つを `_contentDirectory` 1 つにする」はこのテストを壊すので、A1 で、名前を保つか、SampleGame のテストの変更をスライスの範囲に含めるかを決める。BOOTSTRAP の Phase B は、Content Directory の状態に触れるメソッドの一覧を同書 §6 に残す。
- **P 検査の集合。** facade は BuildSystem 本体（U-B）の集合に含めない（A0 §4.2）。着手時に存在する P 検査（ASSET_CYCLE で凍結した方式、または常設化されたもの）の U-B 集合から除くファイルとして、`ContentDirectoryStartup.cs` を AC に名前で挙げる。

## 3. 設計素材（BOOTSTRAP A1 初稿から移したもの。A0 §4.2 適用済み）

### 3.1 成立条件と受け入れ条件の素材

- **facade（旧 BOOTSTRAP S2）:** public sealed class `ContentDirectoryStartup` が存在し、構成の解決（旧 `ReadContentDirectoryConfiguration` + `PlayerBakedContentConfiguration` 生成）、directory と決まった直後の Addressables の封じ（base 240〜249 行目。BeforeSceneLoad の同期区間に残す）、session の登録と install（旧 `InstallConfiguredContentDirectoryAsync`）、close、Play 停止時の完了（`CompleteContentDirectoryPlayStop` / `BeginSynchronousShutdown`）を所有する。Bootstrap はこの型の public メンバーだけを呼ぶ。置き場所は BuildSystem 本体の集合（U-B）に含めない場所で、program A0 §2.1 の連携部分と同じ folder（現状 `Runtime/BuildContent/`）でよい。U-A・U-B の P 検査の集合に含まれない。
- **判定表の単体テスト（旧 S6 / AC9）:** `ContentDirectoryStartup.Resolve` の判定表を、Unity Editor Play に依存しない EditMode テスト（`Tests/BuildContent/ContentDirectoryStartupTests.cs`）で 1 行 1 テストに固定する。各テストは `AppConfig` を辞書 provider で組み立て、Unity の `Application` に依存する値（Editor Play か、install root）は引数で渡す。

  | 入力 | 期待 |
  |---|---|
  | `content:runtimeMode` 未指定、installed pair なし | `ContentDirectoryException(InvalidConfiguration)`、メッセージは現行のまま |
  | `addressables` | `null`（Addressables 互換起動） |
  | `directory`、Editor Play でない、required Player でない | `InvalidConfiguration`（Editor Play 限定） |
  | `installedRevisionPath` と `manifestSha256` の片方だけ | `InvalidConfiguration` |
  | 未知の mode | `InvalidConfiguration` |
  | required Player、`runtimeMode` が baked と不一致 | `InvalidConfiguration`（protected key） |
  | required Player、pair の片方だけ | `InvalidConfiguration` |
  | `directory`、Editor Play、`directoryPath` / `buildIdentity` / `representation` のいずれかが空 | `InvalidConfiguration` |
  | `directory`、Editor Play、正規化できない path | `InvalidConfiguration`（`ContentRevisionGate.TryNormalize` 失敗） |
  | `directory`、Editor Play、存在しない directory | `InvalidConfiguration` |
  | `directory`、Editor Play、存在する temp directory、`content:firstScene` 未指定 | 非 null、`Path` が正規化済み、`FirstScene` が空、`IsInstalled` が false |
  | `directory`、Editor Play、存在する temp directory、`content:firstScene` に前後空白付きの名前 | `FirstScene` が trim 済みの名前（BS5b-2 の `ReadEditorFirstScene`） |

  installed pair の verify 成功経路と Player の `PlayerContentConfiguration.Resolve` 経路は既存の `PlayerContentConfigurationTests` と `BuildContentDirectoryIntegrationTests` が覆う（新規テストでは重複させない）。
- **install 失敗経路（旧 AC10）:** `ContentDirectoryStartup.InstallAsync` は、install が失敗したとき登録済み session を close してから元の例外を再送出する（現行 `InstallConfiguredContentDirectoryAsync` の catch と同じ）。EditMode テストで、既に directory を install 済みの `AssetManagement` に対して 2 回目の `InstallAsync` を呼ぶと `InvalidOperationException` が返り、session は close 済み（`ContentDirectorySession` の observable な状態、または偽 `IContentNativeDirectory` の呼び出し記録）であることを確認する。既存 `ContentDirectorySessionTests` の fixture（temp directory の index / manifest）を再利用する。
- **internal 越境 0（旧 I5 / AC5 の BuildContent 部分）:** 変更後の `Runtime/Bootstrap/**` に `ContentDirectorySession`、`UnityContentDirectoryBackend`、`PlayerContentConfiguration`、`PlayerBakedContentConfiguration`、`ContentDeliveryFiles`、`ContentInstallReceipt`、`InstalledRevisionVerifier`、`VerifiedInstalledRevision`、`ContentDeclarationMismatch`（BOOTSTRAP の A2 で一覧の漏れが見つかった）、`SealAddressables`、`InstallContentDirectory`、`CloseContentDirectoryAsync`、`CompleteContentDirectoryPlayStop` が現れない（grep）。BOOTSTRAP はこれらを initializer 1 ファイルに留める（同書 AC5）。
- **公開面（旧 I6）:** 新しい public 型は `ContentDirectoryStartup` と、U1 で決める入力の型だけ。
- **サイズ（旧 S1 の残り）:** BOOTSTRAP 完了後の initializer（同書 S1 で 1,080 行以下）から、Content Directory の分を除いて 900 行以下。数値は着手時の base で見直す。

### 3.2 責務マップの素材

| ファイル | 責務（一文） | 変更理由 | 所有者・寿命 | 許可する依存 | 公開面（A1 初稿の案） |
|---|---|---|---|---|---|
| `Runtime/BuildContent/ContentDirectoryStartup.cs`（新規、public sealed） | `AppConfig` から Content Directory の起動構成を解き、session の登録・install・close・Play 停止時の完了を所有する | Content Directory の起動規則（mode、pair、protected key、Editor Play 限定、verify）が変わるとき | initializer が BeforeSceneLoad で `Resolve` し、ReleaseAll で `CompletePlayStop` / `BeginSynchronousShutdown`。session の寿命は従来どおり | 同 folder の internal 型、`AssetManagement` の internal メソッド 4 つ、`ContentDirectoryException`（namespace は `OneStarMaker.Runtime.BuildContent` のまま。置き場所は共通契約 `Runtime/AssetBuildContracts/`） | public: `static ContentDirectoryStartup? Resolve(AppConfig, in ContentDirectoryStartupContext)`、`string Path` / `BuildIdentity` / `Representation` / `FirstScene`、`bool IsInstalled`、`UniTask InstallAsync(AssetManagement)`、`UniTask CloseAsync(AssetManagement)`、`void CompletePlayStop(AssetManagement)`、`void BeginSynchronousShutdown()`、封じの入口（U1）。internal: `VerifiedInstalledRevision? VerifiedRevision`。`ContentDirectoryStartupContext` は public readonly struct（U1）。約 250 行 |
| `Tests/BuildContent/ContentDirectoryStartupTests.cs`（新規） | 判定表と install 失敗経路 | — | — | — | 約 220 行 |

配置と依存の理由:

- **assembly の構成は未決。** 2026-11-07 の 2 assembly の決定は、owner の上位目的（AssetManagement 単独・BuildSystem 単独・併用。[ASSET_BUILDSYSTEM_INDEPENDENCE.md](ASSET_BUILDSYSTEM_INDEPENDENCE.md)）に置き換わった。`ContentDirectoryStartup` は BuildSystem 本体ではなく、連携部分と OSM 固有の起動構成（`AppConfig` を読む `Resolve`）にまたがる。本スライスの時点では全てが同じ `OneStarMaker.Runtime` assembly にあるので、facade は両方の internal を呼べ、「新しい public 型は 1 つ」は成り立つ。assembly を分けたときに facade が越える internal（AssetManagement 側の 4 メソッドと port、BuildSystem 本体側の 7 件。A0 §2.2 X4）の扱いは asmdef スライスで決める。
- **BuildSystem 本体の集合（U-B）に含めない場所に置く。** 連携部分と同じ folder（現状 `Runtime/BuildContent/`）でよいが、U-B 集合から除く対象として AC に名前で挙げる。
- **`AssetManagement` の internal メソッドも facade が呼ぶ。** Bootstrap がこれを直接呼ぶ現状は、`_assetManagement` を具象型へ確かめる処理を 5 箇所（base 240、419、456、603、977 行目）に散らしている。facade に寄せれば 1 箇所になる。

### 3.3 実装手順の素材

`ContentDirectoryStartup` と `ContentDirectoryStartupContext` を新設し、判定表と install 失敗のテストを先に書く。initializer の `ReadContentDirectoryConfiguration` / nested `ContentDirectoryConfiguration` / `InstallConfiguredContentDirectoryAsync` / `WarnOnDeclarationMismatch` / 失敗時 close / `ReleaseAll` の 2 分岐 / Player content smoke の close を facade 呼び出しに置き換える。フィールド 3 つ（`_contentDirectoryConfiguration`、`_contentDirectorySession`、`_bakedPlayerContent`）を `_contentDirectory`（`ContentDirectoryStartup?`）1 つにする。`_assetManagement` の名前と型は変えない（SampleGame.Tests の reflection）。

Phase A へ差し戻す条件の素材: `ContentDirectoryStartup` に A3 で凍結した以外の public メンバーが要る。判定表に Unity の `Application` を読まないと再現できない行がある。順序や例外の握り方を変えないと移せない箇所がある。

### 3.4 未決事項の素材

- **U1 `ContentDirectoryStartup` の入力の渡し方。** BOOTSTRAP A1 初稿の案は `Resolve(AppConfig config, in ContentDirectoryStartupContext context)` で、`context` は `Target`（`"StandaloneWindows64-Player"`、現行 const）、`IsEditorPlayMode`（`Application.isEditor && Application.isPlaying` を呼び出し側で評価）、`UseRequiredPlayerFileConfiguration`、`RequiredPlayerConfig`（required のとき `BuildConfig` が読んだ captured 値の `AppConfig`、それ以外は null）、`InstallRoot`（`Path.GetDirectoryName(Application.dataPath)`）を持つ public readonly struct。Unity に依存する値を全て引数にするのは判定表のテストのため。代替は `Resolve` が内部で `Application` を読む（テストが Editor Play に依存して判定表が成立しない）。あわせて Addressables の封じの入口を決める: (a) public `void Seal(AssetManagement)` を足す、(b) `Resolve` に `AssetManagement` を渡し、directory と決まったら封じまで行う。どちらでも封じは BeforeSceneLoad の同期区間で行う（`SealAddressables` は in-flight の Addressables 0 を assert するので、AfterSceneLoad の `InstallAsync` まで遅らせられない）。`Target` は `BUILDSYSTEM_STANDALONE_ENTRY` の X7（target の固定）の結論で扱いが変わりうる。
- **U2 `RequiredJsonFileConfigProvider`（Config の internal）の扱い。** BOOTSTRAP A1 初稿の案は `BuildConfig` に残す（Config は Bootstrap と同じ assembly に残す見込みなので越境ではない）。代替は required 構成の読取まで facade に移す（facade が Config の責務を持つので初稿は採らなかった）。BOOTSTRAP は `BuildConfig` を変えない。
