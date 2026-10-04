# SOUND_SYSTEM Phase A snapshot

type: slice

このファイルは凍結時点の計画である。実装結果とレビュー結果は載せない。

## 0. メタデータ

- type: slice
- status: A
- branch: cursor/sound-system-min-159b
- implementation base commit: 2c29c99806788406551affba6cc795e67e614748
- implementation head commit: 未実施
- risk: normal
- owner: 実装担当
- created: 2026-10-04
- expires: 未設定
- harvest to: なし
- Phase A snapshot path / id: docs/handoff/SOUND_SYSTEM_PHASE_A.md
- Phase A snapshot generated at: 2026-10-04
- Phase A snapshot hash: ライブの HANDOFF に記録する
- Phase B result snapshot path / id: 未実施
- Phase B result snapshot generated at: 未実施
- Phase B result snapshot hash: 未実施
- evidence bundle path / id: 未実施
- evidence bundle generated at: 未実施
- evidence bundle hash: 未実施
- C' blind bundle path / id: 未実施
- C' blind bundle generated at: 未実施
- C' blind bundle hash: 未実施

A2 の独立レビューと A3 の採否会議はこのセッションでは行っていない。凍結の根拠は、依頼で明示された最小範囲と再生引数の方針である。

## 1. 目的と対象外

- 目的: 再生 API を、文字列もゲーム固有の列挙型も受け取らず、割り当てなしで呼べるようにする。バックエンドは Unity ネイティブ、CRI、Wwise、その他へ差し替えられる形にし、実装は Unity ネイティブだけにする。
- 対象外: VoiceGroup、優先度による停止、フェード、SoundHolder、CRI と Wwise の本体、ゲーム側の列挙型、SampleGame への接続、3D、ピッチ、ループ、バス、文字列キーの辞書、公開文書の書き換え。
- 現況: 公開文書のサウンド節は MonoBehaviour の SoundService、VoiceGroup、SoundHolder、フェードを描いている。ランタイムにその実装は無い。入力と同様、ゲーム固有の列挙型はフレームワークが知らない。

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

- SoundHandle: 登録が返す識別子と、範囲外を例外にしない添字変換。所有者は値そのもの。公開面は構造体と比較。行数は小さい。
- SoundVoiceRing: 固定本数を古い順に回す。優先度は持たない。
- ISoundBackend: 具象バックエンドが実装する再生口。クリップ型を置かない。
- SoundPlayer: 再生箇所向けの転送。寿命は呼び出し側。バックエンドは破棄しない。
- UnitySoundBackend: AudioSource の固定プールとクリップ登録。入れ物の GameObject を所有し、Dispose で破棄する。実行中だけシーンをまたいで残す。
- SoundSystemTests: 転送、リング、Unity 登録の EditMode。聴取はしない。
- SoundSystemOfflineTests: Unity 無しで転送の割り当て、リング、共有面に AudioClip が無いことを見る。

## 4. 実装計画

- 変更対象: OneStarMaker.Runtime の SoundSystem、OneStarMaker.Tests、tools/SoundSystemOfflineTests、この計画とライブ HANDOFF。
- 順序: 識別子と共有口、転送、Unity 具象、オフライン試験、EditMode 試験。
- Phase B から Phase A へ差し戻す条件: 再生引数に文字列か列挙型が必要になったとき。asmdef 参照の追加が必要になったとき。割り当てなしの転送が成り立たないとき。
- 対象外を維持する方法: VoiceGroup、フェード、SoundHolder、CRI、Wwise、SampleGame のファイルを追加しない。

## 5. テストとレビュー計画

- 単体テスト: オフライン実行ファイルでハンドル、リング、転送の割り当て、共有面のソース検査。EditMode で同じ論理と Unity 登録。
- 差し戻し中の起点 -Filter: OneStarMaker.Tests.SoundSystem
- 判定必須テスト: 最終の全 EditMode 回帰。加えてオフライン実行ファイル。
- 全 EditMode 回帰の適用除外: なし
- 統合・Unity テスト: 鳴っているかの聴取は判定必須に入れない。
- 操作・実行時・目視条件の検証経路: なし
- 未知の操作経路の疎通結果: なし
- 人間の判断が必要な条件: なし
- 機械検査: pwsh tools/contract-audit.ps1 と pwsh tools/docs-audit.ps1
- A0/A1 主担当・モデル・ベンダー: この実装セッション。Grok。A2 は未実施。
- A2 独立レビューごとの観点・担当・モデル・ベンダー: 未実施
- A3 統合担当・モデル・採否: 未実施。依頼文の最小範囲を凍結本文にした。
- C' 用に予約した担当・モデル・ベンダー: 未予約
- 独立性の強化条件を満たせない場合の理由: A2 と C' をこのセッションでは開いていない。

## 6. Phase B 実装結果

- 実装: 未実施
- HANDOFF との差: 未実施
- 未実行: 未実施
- implementation head commit: 未実施
- Phase B 担当・モデル・ベンダー: 未実施

## 7. Phase C

- 種別: 未実施
- evidence bundle id / hash: 未実施
- 構造適合: 未実施
- 現在の問いを阻害する findings: 未実施
- 後続スライスへ移送する findings: 未実施
- 実行したテストコマンドと -Filter: 未実施
- テスト結果: 未実施
- 判定必須のうち未実行: 未実施
- 重い検証を発見段階で限定実行した場合の理由と範囲: 未実施
- 未確認事項: 未実施
- 担当・モデル: 未実施

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
