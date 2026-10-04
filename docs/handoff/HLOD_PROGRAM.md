# HLOD / Proxy の必要性を判断する program

- type: `program`
- status: **PR #91 の文書修正 A3 を凍結。HLOD は未実装。観測・比較・pilot の実行は未承認。**
- owner: 統合担当 root（計画の維持・証拠保持）、発注者（後続 A3 / D 判断）
- created: 2026-10-04
- expires / harvest 期限: **2026-11-05 または本方針を置き換える revision**。root が継続・終了・移管を判断して更新する。
- harvest to: 現在の計画状態は `docs/README.md`。将来、実証・実装された表示契約だけを `unity/Assets/Docs/Architecture/24-rendering-system.md` へ、実際の streaming 変更があれば `docs/streaming/STREAMING_CURRENT_SPEC.md` へ反映する。
- PR base: `develop`。PR #91 は本書、`HLOD_RESIDENT_PILOT.md`、`docs/README.md` の文書修正だけ。
- conditional candidate: [resident pilot](HLOD_RESIDENT_PILOT.md)。Gate 2 の設計種であり、最初の実装指示ではない。

## 1. 今回の問いと現況

**複数の detail 群を一つの遠景表現で覆う必要性は、どの観測で示せるか。その前に、既存のより小さな方法で問題を解決できるか。** HLOD を作ること自体を完了条件にしない。問題が再現しない、または最小の代替策で解決すれば、pilot を実施せず program を閉じられる。

現在の `LoadRadius = 375` / `UnloadRadius = 550` と Player camera の far clip `2000` は遠景欠落を調べるきっかけであり、実画像で欠落を観測した証拠ではない。`docs/streaming/STREAMING_CURRENT_SPEC.md` の T-07〜T-09（Play 実証・telemetry・受入判定）は未了である。Architecture §21 / §24 の HLOD / Proxy / RenderWorld の将来予約を、現在の実装や pilot 採用判断として読まない。

確認済みの責務境界:

- `SceneResource` / Scene graph の parent は Scene の寿命・依存関係。HLOD の表示 coverage を集約する tree として流用しない。
- Content Directory の台帳は Scene identity 単位。同一 identity の別 representation 同時ロードと deferred activation は拒否される。near / proxy を既存 Variant の単純切替えとみなさない。
- Cell の地面 Renderer と Collider は同居する。Scene / GameObject の非表示・解放は物理・gameplay も巻き込む。companion は構造 Cell のロードに追随し、既存 companion の一括移行は前提にしない。
- Rendering の最小 `RenderEnvironment` lease / sink は実装済み。RenderWorld / BRG と HLOD は未実装の構想である。

根拠: `SceneSystem/SceneResource.cs`、`AssetManagement/AssetManagement.ContentDirectory.cs`（`unity/Assets/OneStarMaker/Scripts/Runtime/`）、`unity/Assets/SampleGame/InGame/InGameSession/World/CellScenes/CellScene.cs`、`unity/Assets/SampleGame/InGame/InGameSession/PlayerScene/PlayerScene.cs`、Architecture §21 / §24、Streaming 現状仕様。

## 2. Gate 0 — 問題を観測する

後続の担当が一つの Season / Cell 経路、camera の移動経路・速度、target device / graphics 条件、対象版を選び、反復可能な観測を残す。対象は、遠景の穴や pop、draw / triangles / frame cost、memory / streaming budget のいずれか一つの実際の問題とする。

問題と選んだ判定条件が再現できなければ、pilot なしで終了する。今回の文書 PR に、新しいゲーム demo、Unity 起動、または CPU / GPU / memory 全項目の benchmark を要求しない。実装へ進むための観測条件と、この計画文書をマージする条件を分ける。

## 3. Gate 1 — 同じ問題に最小の代替策を比較する

Gate 0 と同じ経路・条件で、問題に対応する候補を比較する。

- streaming 設定: ロード半径や退避条件など、現在の仕組みの調整。
- Unity `LODGroup` / per-object Mesh LOD と culling: 常駐している物体の細部と描画負荷を調整する方法。
- 単純な独立遠景 visual: Cell が不在になった後の遠景 coverage が問題なら、必要な遠景だけを独立して保持する方法。

選んだ条件を満たす最小の方法を採る。これで問題が解決すれば、aggregate 制御 pilot を実施せず終了する。object LOD は、それだけで複数 detail 群が unload された後の一つの代表表示を保証する責務ではない。この責務の違いを、native LOD が失敗した証拠に読み替えない。

## 4. Gate 2 — 必要性が残った場合だけ aggregate pilot を設計する

Gate 1 後も観測した問題が残り、その解決に複数群の coverage 対応関係が必要な場合だけ、**新しい A1 / 独立 A2 / 人間 A3** を行う。現時点の [resident pilot 候補](HLOD_RESIDENT_PILOT.md) は未承認で、B の自動開始条件ではない。

候補は全常駐の2 detail 群と1手製 aggregate 表現を使う。明示 membership / bounds / 閾値の検証、coverage 全体での hysteresis、Renderer のみの切替え、Collider / gameplay 継続、取得した表示 ownership、終了時の authored enabled 状態の正確な復元、競合 writer の拒否、main-camera 描画境界の観測を設計対象とする。具体的 owner、camera、frame / capture 経路は後続 A3 で選び直す。

小 fixture の成功が示すのは制御契約だけ。製品の look / performance / memory 改善、実コンテンツへの採用、次の機能の着手承認は示さない。

## 5. 後続候補はそれぞれ独立した Gate を持つ

次の問いは必要性が観測されたときだけ個別 A1 / A2 / A3 を切る。resident fixture の GO から続く実装キューではない。

- proxy 非同期ロード: ready まで旧表示を保持し、失敗・キャンセル・古い完了・owner 終了を扱う必要があるか。
- detail visual と Collider / gameplay の寿命分離: 描画資産だけを解放する必要があるか。本番 companion 全体の移行は別判断。
- detail load / release: coverage 全体が ready になるまで旧表示を保ち、swap 後に旧 visual を解放する必要があるか。
- 階層化: 対象の規模・分布で複数親階層が必要か。Scene parent / 地理配置 / 描画 tree を同一視しない。
- baker: 手製 manifest / proxy の制作コストが自動化を必要とするか。保護領域・再生成・Material / lighting・品質予算を別に決める。

将来の資産取得は `IAssetManagement` と明示 `AssetOwner`（App / Manual / Scene(id) / Bind(go)）を使う。表示 owner と asset owner は別であり、Renderer membership は GameObject / Scene / Collider の停止・解放権限を与えない。`SceneState` は SceneLifecycleManager が所有し、描画 LOD の状態にはしない。

## 6. 後続の検証経路と証拠の意味

ローカル Unity Editor を必要な範囲で起動・操作できる環境である。ただし対象アプリの起動・camera 操作・render capture・証拠受渡しの成立は**未確認**。後続の操作条件を凍結する前に小さな feasibility 確認を行う。workstation の存在だけで疎通成功とせず、人間の手動移動・撮影を暗黙に依頼しない。

将来の pilot A3 は Unity 6000.6.0f1 の executor / 担当、初期 app / Camera 状態、操作、main-camera render 後の capture / readback、対象版・保存先・hash・保持と C / C' への受渡し、不成立時の基盤不備 / 観測不足 / 実装欠陥の分類を決める。

pure policy test、各 frame の Renderer / Collider / gameplay 状態、実際の描画画像は別の証拠である。state log は画像 coverage の証明ではなく、画像は lifetime / unload 完了の証明ではない。fixture が全常駐なら memory 削減を主張しない。実コンテンツ採用時の視覚品質・性能予算はさらに別の Phase A を所有する。

## 7. この文書修正の終了と program の寿命

PR #91 の終了条件は、3文書が Gate 0 / 1 / 条件付き Gate 2 と上記境界を矛盾なく説明し、固定 head の差分・`git diff --check`・docs / contract audit と文書 C / C' が揃うこと。HLOD の実装・pilot 実行・画像証明は本 PR の成果ではない。今回の3文書だけの差分は Unity / 全 EditMode の対象外であり、将来 pilot の検証免除にはならない。

文書 PR の Phase D でも本 program と未承認 candidate を**進行中の作業台として保持**する。恒久文書へ未実装の動作・品質・性能を harvest しない。Gate 0 / 1 で不要と判断した場合、後続が完了した場合、または新 revision に置き換えた場合に、root が真である現況だけを harvest し、解決・置換された作業台を削除する。

従来の pilot A1 r1〜r3 / A2 は candidate の履歴であり、今回の文書修正 A3 とは承認対象が異なる。文書修正の凍結は将来の表示 owner・frame boundary・asset lease / load / release の実装判断を承認しない。
