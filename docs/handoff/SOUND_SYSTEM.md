# SOUND_SYSTEM

公開文書のサウンド節は [07-09-services.md](../../unity/Assets/Docs/Architecture/07-09-services.md) にある。このスライスはその全文を実装しない。凍結時の計画は [SOUND_SYSTEM_PHASE_A.md](SOUND_SYSTEM_PHASE_A.md)。

## 0. メタデータ

- type: slice
- status: C
- branch: cursor/sound-system-min-159b
- implementation base commit: 2c29c99806788406551affba6cc795e67e614748
- implementation head commit: 03505391e2c02a4f642e583ee6a43bdaea8a4e96
- risk: normal
- owner: 実装担当
- created: 2026-10-04
- expires: 未設定
- harvest to: なし
- Phase A snapshot path / id: docs/handoff/SOUND_SYSTEM_PHASE_A.md
- Phase A snapshot generated at: 2026-10-04
- Phase A snapshot hash: 8b18c6bfe1f7dbe0d36f903b82d829fddab80cf219dcaee9b4e9dc1dd3346945
- Phase B result snapshot path / id: 未実施
- Phase B result snapshot generated at: 未実施
- Phase B result snapshot hash: 未実施
- evidence bundle path / id: 未実施
- evidence bundle generated at: 未実施
- evidence bundle hash: 未実施
- C' blind bundle path / id: 未実施
- C' blind bundle generated at: 未実施
- C' blind bundle hash: 未実施

A2 の独立レビューと A3 の採否会議は行っていない。計画は依頼の最小範囲をそのまま凍結した。この節 7 は同じセッションの発見メモであり、独立した判定 C ではない。GO とは書かない。

## 1. 目的と対象外

- 目的: 再生 API を、文字列もゲーム固有の列挙型も受け取らず、割り当てなしで呼べるようにする。バックエンドは Unity ネイティブ、CRI、Wwise、その他へ差し替えられる形にし、実装は Unity ネイティブだけにする。
- 対象外: VoiceGroup、優先度による停止、フェード、SoundHolder、CRI と Wwise の本体、ゲーム側の列挙型、SampleGame への接続、3D、ピッチ、ループ、バス、文字列キーの辞書、公開文書の書き換え。
- 現況: 公開文書は MonoBehaviour の SoundService、VoiceGroup、SoundHolder、フェードを描いている。このスライスはその形を作らない。共有の再生口と Unity の固定プールだけを置いた。

## 2. 意思決定と受け入れ境界

- このスライスが答える問い: フレームワークの再生引数を文字列にせず、Unity ネイティブだけを実装した差し替え口で、割り当てなしに鳴らせるか。
- 進める最低条件: 共有の再生口が SoundHandle と音量だけを受け、その転送が測定区間で割り当てない。AudioClip は Unity 具象型の登録にだけ現れる。ゲームの列挙型と SampleGame へ依存しない。CRI と Wwise の型は追加しない。
- 受け入れ条件:
  - Play の引数は SoundHandle と float である。
  - SoundHandle の既定値は無効で、有効値は登録順の 1 始まりである。
  - SoundPlayer は無効ハンドルも転送する。2000 回の転送で追加割り当てが 0 である。
  - ISoundBackend は AudioClip も string も持たない。
  - UnitySoundBackend は登録時だけ配列を伸ばし、再生は固定数の AudioSource を古い順に再利用する。無効ハンドルと破棄後の Play は無音で戻る。
  - 空のプール、null クリップ、破棄後の登録は例外にする。
- ここでは答えない問いと所有する後続スライス: 優先度、フェード、SoundHolder、CRI、Wwise、ゲーム側の列挙型とクリップの対応表、公開文書の SoundService 像をこの最小形へ更新するか。
- 判定定義: 進める最低条件を満たし、Unity 未実行を未確認のまま GO と書かない。GO は判定 C の全 EditMode 回帰の後だけ。
- 停止規則: 進める最低条件を満たし、現在の問いに致命的な反証がなければ GO で終了する。最低条件未達のまま終了しない。
- A3 後の例外承認: なし
- 本文へ転記した実装制約: 再生で文字列も列挙型も受け取らない。列挙型を使うならゲームが SoundHandle を表に保持する。フレームワークはその表を知らない。Unity オブジェクトの null 判定は == null。record は使わない。新規ファイルの先頭は #nullable enable。asmdef 参照は増やさない。
- 未決事項: なし

## 3. 責務マップ

- `SoundHandle.cs` 76 行。識別子と範囲外を例外にしない添字変換。公開面は構造体。整数は internal。
- `SoundVoiceRing.cs` 19 行。固定本数を古い順に回す。優先度は持たない。
- `ISoundBackend.cs` 14 行。具象が実装する再生口。クリップ型を置かない。
- `SoundPlayer.cs` 30 行。再生箇所向けの転送。バックエンドは破棄しない。
- `UnitySoundBackend.cs` 138 行。AudioSource の固定プールとクリップ登録。GameObject を所有し Dispose で破棄する。実行中だけシーンをまたいで残す。
- `SoundSystemTests.cs` 137 行。転送、リング、Unity 登録の EditMode。聴取はしない。
- `tools/SoundSystemOfflineTests` は Unity 無しの転送、リング、共有面の検査。

行数警報の対象になるファイルは無い。

## 4. 実装計画

- 変更対象: OneStarMaker.Runtime の SoundSystem、OneStarMaker.Tests、tools/SoundSystemOfflineTests、計画 snapshot、この HANDOFF。
- 順序: 識別子と共有口、転送、Unity 具象、オフライン試験、EditMode 試験。
- Phase B から Phase A へ差し戻す条件: 再生引数に文字列か列挙型が必要になったとき。asmdef 参照の追加が必要になったとき。割り当てなしの転送が成り立たないとき。
- 対象外を維持する方法: VoiceGroup、フェード、SoundHolder、CRI、Wwise、SampleGame のファイルを追加しない。

## 5. テストとレビュー計画

- 単体テスト: オフライン実行ファイル。EditMode は同じ論理と Unity 登録。
- 差し戻し中の起点 -Filter: OneStarMaker.Tests.SoundSystem
- 判定必須テスト: 最終の全 EditMode 回帰。加えてオフライン実行ファイル。
- 全 EditMode 回帰の適用除外: なし
- 統合・Unity テスト: 鳴っているかの聴取は判定必須に入れない。
- 操作・実行時・目視条件の検証経路: なし
- 未知の操作経路の疎通結果: なし
- 人間の判断が必要な条件: なし
- 機械検査: pwsh tools/contract-audit.ps1 と pwsh tools/docs-audit.ps1
- A0/A1 主担当・モデル・ベンダー: この実装セッション。Grok。
- A2 独立レビューごとの観点・担当・モデル・ベンダー: 未実施
- A3 統合担当・モデル・採否: 未実施。依頼文の最小範囲を凍結本文にした。
- C' 用に予約した担当・モデル・ベンダー: 未予約
- 独立性の強化条件を満たせない場合の理由: A2 と C' をこのセッションでは開いていない。

## 6. Phase B 実装結果

- 実装: SoundHandle、SoundVoiceRing、ISoundBackend、SoundPlayer、UnitySoundBackend、EditMode 試験、オフライン試験を追加した。asmdef は変更していない。
- HANDOFF との差: なし。
- 未実行: Unity Editor のコンパイル確認と EditMode。オフライン試験は UnitySoundBackend をコンパイルしない。
- implementation head commit: 03505391e2c02a4f642e583ee6a43bdaea8a4e96
- Phase B 担当・モデル・ベンダー: この実装セッション。Grok。

## 7. Phase C

- 種別: 発見
- evidence bundle id / hash: 未実施
- 構造適合: 再生口は Runtime の SoundSystem にあり、SampleGame を参照しない。AudioClip は UnitySoundBackend の登録に閉じている。asmdef 参照は増えていない。
- 現在の問いを阻害する findings: なし。ただしこの発見は実装と同じセッションであり、独立レビューではない。
- 後続スライスへ移送する findings: 優先度付きの同時再生、フェード、SoundHolder、CRI、Wwise、ゲーム側の対応表、公開文書の更新、実際に鳴っているかの確認。
- 実行したテストコマンドと -Filter: `dotnet run --project tools/SoundSystemOfflineTests/SoundSystemOfflineTests.csproj -c Release`。Unity の filter は使っていない。割り当てなしの転送は Unity 無しで観測できるため。
- テスト結果: オフライン 5 件実行、失敗 0、exit 0。名前は Handle_DefaultIsInvalid_AndRegisteredValuesAreDense、Ring_ReusesTheOldestSlot、Player_ForwardsHandleAndVolume_WithoutAllocating、Player_RejectsMissingBackend、Sources_KeepClipTypesOffTheSharedPlaySurface。contract-audit は errors=0 warnings=0。
- 判定必須のうち未実行: 全 EditMode 回帰。Unity の SoundSystemTests も未実行。
- 重い検証を発見段階で限定実行した場合の理由と範囲: Unity バッチは実行していない。
- 未確認事項: Unity でのコンパイル、AudioSource の実再生、リスナーが無いときの聞こえ。GO ではない。
- 担当・モデル: この実装セッション。Grok。

## 8. Phase C'

- 担当方式: 未実施
- blind audit bundle id / hash: 未実施
- 確認範囲・方法: 未実施
- 判定: 未実施
- 現在の問いを阻害する findings: 未実施
- 後続スライスへ移送する findings: 未実施
- 残存リスク: 未実施
- 監査できなかった範囲: 未実施
- 独立性: 未実施
- 発見 C / 判定 C 結論の事前閲覧・設計実装への関与: 未実施
- 担当・モデル: 未実施

## 9. Phase D

- C / C' の突合: 未実施
- マージ判断: 未実施
- harvest: 未実施
- 削除確認: 未実施
