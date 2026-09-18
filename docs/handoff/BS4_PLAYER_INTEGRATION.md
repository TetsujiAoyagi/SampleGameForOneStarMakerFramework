# BS4 — Player integration HANDOFF

- type: slice
- status: A0（設計入力の固定中）
- branch: `codex/bs4-player-integration`
- implementation base commit: `ea7acf7` (`develop`, 2026-09-19)
- implementation head commit: 未作成
- risk: high（起動、Player packaging、code stripping と AOT）
- owner: BS4 主担当
- created: 2026-09-19
- expires: BS4 Phase D。2026-10-19 までに継続条件を再確認する。
- harvest to: `unity/Assets/Docs/Architecture/04-app-startup.md`、`13-resource-system.md`、`18-asset-description.md`
- Phase A snapshot / Phase B result / evidence bundle / C' blind bundle: Phase ごとに固定し path・時刻・hash を追記する。

## A0 — 同一入力 packet

### この slice が答える問い

対応する成功 Content BuildReport から組み立てた固定 target の Player が、選択 content を本体へ二重同梱せず、独立した local directory を登録して論理初回 Scene と代表非 Scene content を利用・解放して終了できるか。

### 現況と根拠

- `develop` は `ea7acf7`、PR #63 の BS3 実装と #64 のレビュー artifacts 削除を含む。開始時 tracked / untracked 差分はない。
- BS2b の `BuildContentResult` は identity、成功 content path、preflight/outcome report、manifest/metadata path を返す。成功 directory は `StandaloneWindows64-Player` 用。現在の SampleGame content menu は `BuildContentResult` をログに出すだけで Player build に渡さない。
- BS3 `ContentDirectorySession` は単一 root の登録・index・native load の寿命を持ち、`IAssetManagement` の型付き load と owner/cache が利用権を保持する。同一 process revision 削除は `ContentRevisionGate` の lease を使う。この契約を変更しない。
- `AbstractApplicationInitializer` の directory mode は Editor Play 限定。config、UICommon、SceneResourceMap は先に Addressables から取得される。`RegisterAlreadyLoadedScenes` は既にロードされた Unity Scene を map の名前で照合するだけ。Player の論理初回 Scene を content から新規に開始する動作はない。
- 旧 `VariantPlayerBuild` は active target、`SampleScene.unity` と共有 `app-config.json` の一時書換を使う。BS4 はこの新経路を再利用しない。
- 公開 Architecture §4 は従来の Scene 0 と Addressables bootstrap を説明し、§13・§18 は BS3 の Editor directory 経路までを実装済みとして記す。Player 成立済みとは記さない。
- Unity の公式 stripping 説明では content-only build の型は Player から除去され得る。Addressables の自動 link.xml と CD の保持を同一視せず、直接参照・明示的な preserve と実 Player 検証で因果を確認する。

### 要求、制約、最低条件の入力

- 固定 target は Windows Standalone 64 bit。bootstrap Scene だけを Player Scene 一覧へ入れ、選択された content Scene を直接同梱しない。
- build 要求から専用 runtime config を生成し、local directory、build identity、logical first scene、起動時 representation を渡す。共有 `app-config.json` の mutation は不要にする。
- 対応する成功 Content BuildReport と Player の関係を診断可能に固定し、mismatch / 欠損 / 部分失敗では起動・build を失敗させる。BS3 の登録・利用寿命・取消後 drain 契約を保つ。
- 必要 code を High stripping で保持し、IL2CPP/AOT の固定 target Player を実行して bootstrap→登録→論理初回 Scene→代表非 Scene fixture の load/release→終了を確認する。単なる report 成功や Editor Play は GO の代替にしない。
- 最終全 EditMode 回帰、必要な PlayMode、content build と Player 実行を判定証拠とする。必須 Player 検証が環境不足でできなければ NO GO。
- Unity C# は `#nullable enable`、`record` 不使用、偽 null 判定遵守。Runtime→Editor 依存、新 asmdef 参照、公開 API の無断追加を避ける。Scene / Prefab / Addressables 等は Editor で操作し、Phase B は対象限定・コンパイルまで、テストと Build は Phase C に置く。

### 既知の未決事項（A1 で調査して閉じる）

1. Addressables bootstrap の必要最小限を Player へどう残し、SceneResource graph metadata の source 不在利用をどう成立させるか。UICommon 等の既存経路との境界。
2. runtime config の配置、読み取り順、identity / target / path 検証、local directory の移設と Player 配布形態。
3. Content BuildReport と Player の照合点、build 順序、失敗時の staging と既存成功 Player 保護。
4. Content Scene の二重同梱の機械的検査と、stripping / IL2CPP / AOT の明示保持方法。
5. 固定 target 実機での自動成功信号、代表非 Scene fixture、終了と native resource release の確認方法。
6. 起動時表現固定の範囲と同一 session 内の別表現要求の扱い。

### ここでは答えない問い

- HTTP/LAN 配信、install・hash 検証、別 process 排他、物理削除は DIST。
- 旧 Addressables build/workflow、旧 Player overlay の廃止は RET。BS4 は新 Player 経路の成立を確認する。
- 通常 Editor Play 停止時の完全 drain は RET。Scene 以外の本番 Description 型と Mesh サブアセットは実需要時の非 Scene slice。
- 同一 session 中の任意の表現切替は後続の専用 slice。BS4 の Player は起動時一回固定を守り、別表現要求の失敗境界を確認する。

## A1 — 設計初稿（A2 入力）

### 進める最低条件と判定

1. **対応付けと単一同梱:** 同じ実行で得た成功 `BuildContentResult` の identity / target / outcome / manifest / metadata を検証し、`BuildPlayerOptions.previousBuildReportDirectories` へ対応 metadata を渡した Windows x64 Player を作る。Player Scene 一覧は既存の `UIScene` 一件。selected content Scene の GUID が Player の `BuildReport.packedAssets` に無く、Player 側の Addressables content と `StreamingAssets` に第二の copy が無いことを証拠化する。content は Player 出力の sibling directory に一回だけ配置する。
2. **起動と寿命:** 専用生成 config から relative local directory、identity、target、representation、論理初回 Scene を読み、source asset / AssetDatabase 無しで graph metadata を得る。登録して SceneDirector から初回 Scene を明示 load し、代表 Prefab `GameObject` を型付きで load / release（可能なら instantiate / destroy）して、終了時に BS3 session の利用権を正しく解放する。欠損・不一致・別表現要求を黙って Addressables に切り替えない。
3. **code と実機:** IL2CPP、High managed stripping、`StandaloneWindows64` Player を実 build・実行し、上記の順序と終了を Player の生ログ・検証 receipt・process exit で確認する。対応する content 側の code type 情報を Player build に渡すこと、必要な実型・generic 呼出しの保持を実挙動で示す。必須 Player 実行が不能なら NO GO。

受け入れ詳細: config に schema version / build identity / target / relative path / first scene / representation を固定する。path は install root に対して canonical 化し、root 外・絶対パス・欠損を拒否する。`ContentDirectorySession.Register` が root identity / target を再照合する。Player receipt は content report path と identity、Player build GUID、backend、stripping、scene 一覧、排除した GUID の検査件数、smoke 各段階を残す。build 失敗時は成功済み Player を上書きせず staging に失敗情報を残し、部分出力を成功として公開しない。

GO は上記三条件、判定必須テストと C/C' の blocker 無しを全て満たした場合のみ。CONDITIONAL ACCEPT は用いない。最低条件達成後、DIST / RET / 非 Scene 本番拡張の問いを追加せず終了する。

### 技術判断と比較

- **bootstrap:** `UIScene.unity` は既に `UICommon` と EventSystem を持つ。これを唯一の Player Scene 0 とし、directory Player では `LoadUICommonAsync` がロード済み component を使うことを必須にする。未発見時に Addressables load へ逃げない。新たな本番 Scene 作成や既存 Scene の広範再生成を要しない。
- **graph metadata:** build 前に production `SceneResourceMap` を BS4 所有の一時 `Resources/BS4/SceneResourceMap` asset へ Editor の `AssetDatabase.CopyAsset` で複写する。map と参照する `SceneResource` 定義は Player に保持し、Unity Scene payload は Player Scene 一覧に含めない。`AssetReference` は GUID locator であって直接 Unity Scene 参照ではないという仮説を build report の GUID 検査で証明する。もし選択 Scene が混入するなら build を失敗させ、Phase A revision で薄い graph catalog へ切り替える。`SceneDirector` の graph / lifecycle 契約は変えない。実行時に source path や AssetDatabase は要求しない。
- **設定:** 共有 `Assets/SampleGame/Config/app-config.json` は読み書きしない。`OSM_BS4_PLAYER` を `BuildPlayerOptions.extraScriptingDefines` に付け、Game composition root から `StreamingAssets/bs4-runtime.json` を必須の plain-file provider として先頭に加える。環境変数・コマンドラインが上書き可能な既存順序は維持する。ただし identity、target、path、first scene、representation の検証は上書き後の値に対して行い、不正なら起動失敗。Player 専用 JSON は Editor build の一時 staging から生成し、build 後に生成元のみ消す。`runtimeMode=directory` を必須にし、Player 以外の既定は Addressables のまま。
- **build の対応:** SampleGame Editor に新 Player coordinator を置き、呼出側は `BuildContentResult` をそのまま渡す。preflight と outcome を読み、schema / identity / target / success path / manifest の一致を確認し、Report のみ・directory のみの任意入力を受けない。`previousBuildReportDirectories` は Unity が content 型保持の情報を Player 側へ渡す正規口。成功後に Player と content を一つの publish directory に収め、receipt を書いて staging→final の同一 volume rename。旧 `VariantPlayerBuild` と一時 overlay を使わない。
- **二重同梱:** Player Scene 配列一件を検査し、`BuildReport.packedAssets[].contents[].sourceAssetGUID` で preflight の selected scene GUID と交差しないことを検査する。既存 `StreamingAssets` の未知ファイルや Addressables の Player build 自動生成は preflight で拒否または限定抑止し、復元する。判定 C は publish directory のファイル一覧と GUID 検査結果を固定する。metadata だけに頼らず Player build report を見る。
- **stripping/AOT:** BuildProfile は IL2CPP / High / x64 を明示して build 後に Editor 設定を復元する。`previousBuildReportDirectories` で Unity の content 使用型を linker へ渡す。静的な generic `LoadContentAssetAsync<GameObject>` と Prefab instantiate を smoke 経路に置く。実 Player で欠ける型だけ `link.xml` または明示 code root を追加し、その理由をコメントと HANDOFF に記録する。CD0 Mono 成功、BuildReport 成功、`link.xml` の存在単独では成立としない。
- **失敗:** Player 前の検証失敗は Player build を開始しない。Unity build 失敗、dedup 検査失敗、copy / receipt / publish 失敗は失敗 outcome に identity と stage を残す。以前の成功ディレクトリは不変。起動時 config / directory / graph / first scene の失敗は stage 付き error と非ゼロ smoke exit を残し、Addressables へ fallback しない。session 登録後の失敗は BS3 の close / drain に従う。
- **起動時表現:** config の一値を SceneDirector と smoke object request に固定する。同一 session で異なる表現を要求する通常 UI は作らない。BS3 の `EntryAmbiguous` になるケースを隠さず失敗として確認し、任意の切替は後続専用 slice とする。

### 責務マップと規模

- `AbstractApplicationInitializer.cs`（938 行、+約 70）: Framework bootstrap orchestration。plain-file config provider の注入、Player directory 許可、graph hook、論理初回 Scene の明示 load、失敗 cleanup。App が所有し `Application.quitting` まで。既存 500 行警報は起動順序の同一所有者による部分変更であり、Parser / I/O を同ファイルへ増やさず別クラスに置く。新 protected hook は限定的に宣言し、Editor/Addressables 既定を保つ。
- 新 `Runtime/Config/RequiredJsonFileConfigProvider.cs`（約 90 行）: file I/O と JSON flatten。起動一回の短寿命、`IConfigProvider` 実装。Unity Editor 依存なし。欠損・不正 JSON を明示失敗させる。pure path/field validation は別にテスト可能にする。
- 新 `Runtime/BuildContent/PlayerContentConfiguration.cs`（約 130 行）: schema / target / relative path / identity / first scene / representation の純粋な検証・resolve。外部公開は最小、`App` 寿命の immutable 値。Unity API と Editor API を使わない単体検査境界。
- `AppInitializer.cs`（310 行、+約 55）: SampleGame の path と graph resource を選ぶ composition root、Player smoke hook。App 寿命。Framework→Game の依存は追加しない。
- `SampleGame/DependOnAll/Editor/Build/SampleGameContentBuild.cs`（94 行、+約 15）: content build result を返す選択入口を増やし、Player への接続を提供。project の季節 policy を維持する。
- 新 `SampleGame/DependOnAll/Editor/Build/PlayerBuildInputValidator.cs`（約 140 行）: report JSON と request の純粋照合。Editor I/O なしで path / metadata の整合をテスト。SampleGame Editor 内部。
- 新 `.../SampleGamePlayerBuild.cs`（約 230 行）: Unity Editor adapter と build lifecycle の単一 owner。`BuildPipeline.BuildPlayer`、一時 generated Resources/config、Addressables 自動 build 抑止、PlayerSettings 復元、stage/publish。I/O を含むため fake port で失敗経路をテスト。予想 3 責務になるなら `PlayerBuildPublisher` へ分割し、Phase A revision で責務配置を再確認する。
- 新 `.../PlayerBuildReportVerifier.cs`（約 100 行）: build report の Scene GUID 交差と file list、receipt 生成。Editor API はこの adapter 側に閉じ、policy は pure set 比較へ分離。
- Unity Editor tests（新 200〜350 行）: config/path/report 失敗と fake publish、GUID dedup。Player 実 build / run の harness は Phase C 用であり、fixture 生成以外を全 EditMode 本体へ埋め込まない。

新 asmdef 参照は予定しない。新規生成 asset は BS4 所有 path と marker を持ち、既存 production scene/prefab/Addressables 設定の永続変更をしない。想定実装差分約 900〜1200 行の高リスク slice として独立レビューを行う。

### 実装順序と Phase B 停止規則

1. 純粋 config / report validator と failure tests。2. Runtime bootstrap hooks と SampleGame 配線。3. Player adapter / staging / dedup / receipt。4. 実 fixture harness。5. Editor compile と contract audit。Phase B はテスト・Build を実行しない。

map copy の直接依存が content Scene を Player へ取り込む、`previousBuildReportDirectories` が現行 Unity API で型保持できない、新 asmdef edge・公開 API・owner / 寿命の追加が必要、既存 SceneDirector に graph 形式の変更が必要と判明したら Phase A revision へ戻す。失敗を Player 成功へ読み替えない。

### テスト計画

- 差し戻し中の起点 filter: `PlayerContentConfiguration` / `PlayerBuildInputValidator` / `PlayerBuildReportVerifier` の EditMode、BS3 の `ContentDirectorySessionTests` と `ContentDirectoryIndexTests`。必要に応じて違反根拠を記録して拡大する。
- 判定必須: `pwsh tools/contract-audit.ps1`、`pwsh tools/docs-audit.ps1`、最終 head の全 EditMode 回帰（空 filter）、BS3 の directory PlayMode 経路、固定 Windows x64 IL2CPP / High Player build と実行。Unity バッチテストは Editor を閉じ、初回から sandbox 外の承認済み `pwsh tools/run-tests.ps1` を使う。XML の件数・対象名を確認し、0 件を失敗とする。
- Player harness は production graph に合う初回 Scene と、同じ directory の代表 Prefab fixture を用意する。選択結果・report・Player receipt と固定 implementation head を対にし、成功 signal の前に load / release / shutdown stage を検査する。source fixture を Player 実行時に参照しない。欠損 directory、identity / target mismatch、別表現要求は非ゼロ終了で確認する。
- A2 は同一 A1 版から architecture と runtime/build failure の独立レビューを別モデルで行う。A0 だけの代替構成も独立に取得済み。A3 で採否を記録し、C' 用に A/B/C 未関与モデルを残す。

## A2 — 独立レビュー

同じ入力版の A1 を複数モデルへ渡し、うち一件をアーキテクチャゲートに指定する。A0 のみからの代替案も別担当で検討する。C' 用の未関与モデルを予約する。

## A3 — 統合・凍結

レビュー所見の採否、凍結 snapshot と hash を記入する。program の委任に基づく技術判断は主担当が理由を記録する。

## Phase B / C / C' / D

各 Phase の実績、固定 commit と証拠、未実行事項、判定を順次記入する。Phase D のマージ判断はユーザーへ渡す。
