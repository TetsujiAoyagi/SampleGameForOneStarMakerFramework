# SOUND_VOLUME Phase A snapshot

type: slice

このファイルは凍結時点の計画である。実装結果とレビュー結果は載せない。

## 0. メタデータ

- type: slice
- status: A
- branch: cursor/sound-volume-159b
- implementation base commit: d90273a2a11c9c90fbf26be8f901a6a98efdfda6
- implementation head commit: 未実施
- risk: normal
- owner: 実装担当
- created: 2026-10-04
- expires: 未設定
- harvest to: なし
- Phase A snapshot path / id: docs/handoff/SOUND_VOLUME_PHASE_A.md
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

A2 の独立レビューと A3 の採否会議はこのセッションでは行っていない。凍結の根拠は、前スライスの再生口に優先度、フェード、ミキサーのエフェクトを載せる場所が無いことと、依頼でそれらを領域にまとめたいと示されたことである。

## 1. 目的と対象外

- 目的: ミックスの領域を足し、その領域に音量、優先度、フェード、リバーブを載せられるようにする。Unity ではその領域を AudioMixerGroup へ流し、露出パラメータがあればリバーブと音量を送る。
- 対象外: リスナーが空間へ出入りして切り替える当たり領域、領域ごとの同時再生数、VoiceGroup と SoundHolder、フェードごとの CancellationTokenSource、Update への自動登録、CRI と Wwise の本体、ミキサーアセットの作成、鳴っているかの聴取、公開文書の書き換え。
- 現況: 再生は SoundHandle と音量だけで、空きが無ければ最も古いスロットを止めていた。領域もフェードもリバーブも無い。

## 2. 意思決定と受け入れ境界

- このスライスが答える問い: 優先度、フェード、リバーブを、クリップ再生の文字列引数にせず、ミックスの領域へ載せられるか。
- 進める最低条件: 領域の識別子で再生先を指定でき、優先度の低い再生から止まり、フェードは注入した時間で進み、リバーブの数値は領域に残り、共有の再生口はミキサー型を持たない。
- 受け入れ条件:
  - 空きが無いとき、新しい音より高い優先度だけが鳴っていれば新しい音は鳴らない。それ以外は最も低い優先度を止め、同点は最も古いものを止める。
  - 領域のフェードは再生スロットを空けない。再生のフェードが 0 に着いたらそのスロットを空ける。古い世代の再生へフェードしても復活しない。
  - 2000 回の再生と Tick で追加割り当てが 0 である。
  - ISoundBackend は AudioClip も AudioMixer も string も持たない。
  - Unity の領域登録はミキサーグループと露出パラメータ名を受け、パラメータが無いときの音量フェードはソース音量に掛ける。パラメータが有るときはミキサーへ送り、ソース音量には掛けない。
- ここでは答えない問いと所有する後続スライス: 空間の当たり領域、領域ごとの同時再生上限、Tick の Update 登録、ミキサーアセットでの実音確認、CRI と Wwise への同じ領域の対応、公開文書のフェード記述をトークン源から状態更新へ改めるか。
- 判定定義: Unity 未実行のまま GO と書かない。GO は判定 C の全 EditMode 回帰の後だけ。
- 停止規則: 進める最低条件を満たし、現在の問いに致命的な反証がなければ GO で終了する。最低条件未達のまま終了しない。
- A3 後の例外承認: なし
- 本文へ転記した実装制約: 領域はミックス上のバスであり、ワールドの当たり判定ではない。フェードの時間は Tick の引数で進む。フェードごとにトークン源は作らない。Dispose は入れ物を破棄し、遅れた再生は無効値で戻る。Unity オブジェクトの null 判定は == null。record は使わない。asmdef 参照は増やさない。
- 未決事項: なし

## 3. 責務マップ

- SoundVolumeId / SoundVoiceId: 領域と、世代つきの再生。公開面は構造体。
- SoundReverb / SoundVolumeSettings: 領域に載せる数値。ミキサー型を持たない。
- SoundFade: 1 歩の補間。
- SoundMix: 優先度の選択、フェード、リバーブの保持。クリップもミキサーも知らない。
- UnityMixerParameters: 露出パラメータ名。登録時だけ使う。
- ISoundBackend / SoundPlayer: 共有の再生口と転送。Tick は呼び出し側が進める。
- UnitySoundBackend: AudioSource のプール、ミキサーグループへの出力、パラメータの送信。古い順のリングはここから外す。
- 試験: オフラインで優先度、フェード、割り当て。EditMode で領域の登録とフェード。

## 4. 実装計画

- 変更対象: SoundSystem、その EditMode 試験、オフライン試験、この計画とライブ HANDOFF。
- 順序: 領域と再生の識別子、状態機械、共有口、Unity の結線、試験。
- Phase B から Phase A へ差し戻す条件: 共有口にミキサー型や文字列の再生引数が必要になったとき。空間の当たり領域がこのスライスの最低条件になったとき。
- 対象外を維持する方法: 当たり領域、VoiceGroup、SoundHolder、CRI、Wwise、Update 登録を追加しない。

## 5. テストとレビュー計画

- 単体テスト: オフライン実行ファイル。EditMode は同じ論理と Unity の領域登録。
- 差し戻し中の起点 -Filter: OneStarMaker.Tests.SoundSystem
- 判定必須テスト: 最終の全 EditMode 回帰。加えてオフライン実行ファイル。
- 全 EditMode 回帰の適用除外: なし
- 統合・Unity テスト: ミキサーを通した実音の聴取は判定必須に入れない。
- 操作・実行時・目視条件の検証経路: なし
- 未知の操作経路の疎通結果: なし
- 人間の判断が必要な条件: なし
- 機械検査: pwsh tools/contract-audit.ps1 と pwsh tools/docs-audit.ps1
- A0/A1 主担当・モデル・ベンダー: この実装セッション。Grok。
- A2 独立レビューごとの観点・担当・モデル・ベンダー: 未実施
- A3 統合担当・モデル・採否: 未実施。依頼の次の一手を、ミックスの領域として凍結した。
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
