# SOUND_VOLUME

公開文書のサウンド節は [07-09-services.md](../../unity/Assets/Docs/Architecture/07-09-services.md) にある。このスライスは当たり領域も VoiceGroup も実装しない。凍結時の計画は [SOUND_VOLUME_PHASE_A.md](SOUND_VOLUME_PHASE_A.md)。前段は [SOUND_SYSTEM.md](SOUND_SYSTEM.md)。

## 0. メタデータ

- type: slice
- status: C
- branch: cursor/sound-volume-159b
- implementation base commit: d90273a2a11c9c90fbf26be8f901a6a98efdfda6
- implementation head commit: c22ef6edf030239fb361af9af295c2394d88e9b2
- risk: normal
- owner: 実装担当
- created: 2026-10-04
- expires: 未設定
- harvest to: なし
- Phase A snapshot path / id: docs/handoff/SOUND_VOLUME_PHASE_A.md
- Phase A snapshot generated at: 2026-10-04
- Phase A snapshot hash: 5e830f219180a8ed346bf84662d2e91ef0feb3b2e3d5a816cf5a38c30f47edcb
- Phase B result snapshot path / id: 未実施
- Phase B result snapshot generated at: 未実施
- Phase B result snapshot hash: 未実施
- evidence bundle path / id: 未実施
- evidence bundle generated at: 未実施
- evidence bundle hash: 未実施
- C' blind bundle path / id: 未実施
- C' blind bundle generated at: 未実施
- C' blind bundle hash: 未実施

A2 と A3 は未実施。この節 7 は実装と同じセッションの発見メモであり、GO ではない。

## 1. 目的と対象外

- 目的: ミックスの領域を足し、その領域に音量、優先度、フェード、リバーブを載せられるようにする。Unity ではその領域を AudioMixerGroup へ流し、露出パラメータがあればリバーブと音量を送る。
- 対象外: リスナーが空間へ出入りして切り替える当たり領域、領域ごとの同時再生数、VoiceGroup と SoundHolder、フェードごとの CancellationTokenSource、Update への自動登録、CRI と Wwise の本体、ミキサーアセットの作成、鳴っているかの聴取、公開文書の書き換え。
- 現況: 前スライスは SoundHandle と音量だけで、空きが無ければ最も古いスロットを止めていた。そのリングは優先度の選択に置き換えた。

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

- `SoundVolumeId.cs` 73 行。`SoundVoiceId.cs` 60 行。識別子。
- `SoundReverb.cs` 60 行。`SoundVolumeSettings.cs` 41 行。領域に載せる数値。
- `SoundFade.cs` 52 行。1 歩の補間。
- `SoundMix.cs` 338 行。優先度、フェード、リバーブの状態。クリップもミキサーも知らない。
- `UnityMixerParameters.cs` 35 行。露出パラメータ名。
- `ISoundBackend.cs` 24 行。`SoundPlayer.cs` 56 行。共有口と転送。
- `UnitySoundBackend.cs` 391 行。AudioSource とミキサーグループへの結線。状態機械は SoundMix に置いたので、これ以上は分割していない。
- 試験はオフラインと EditMode。聴取はしない。

行数警報で分割する閾値には達していない。

## 4. 実装計画

- 変更対象: SoundSystem、EditMode 試験、オフライン試験、計画 snapshot、この HANDOFF。
- 順序: 識別子、状態機械、共有口、Unity の結線、試験。
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

- 実装: 領域、再生の世代、優先度による空き選択、Tick で進むフェード、リバーブの保持、Unity のミキサーグループ結線を追加した。最も古いスロットを無条件に止めるリングは外した。asmdef は変更していない。
- HANDOFF との差: なし。
- 未実行: Unity Editor のコンパイル確認と EditMode。ミキサーの SetFloat が実音に出るかは未確認。オフライン試験は UnitySoundBackend をコンパイルしない。
- implementation head commit: c22ef6edf030239fb361af9af295c2394d88e9b2
- Phase B 担当・モデル・ベンダー: この実装セッション。Grok。

## 7. Phase C

- 種別: 発見
- evidence bundle id / hash: 未実施
- 構造適合: 領域の数値は Runtime の SoundSystem にあり、ミキサー型は UnitySoundBackend と UnityMixerParameters に閉じている。SampleGame は参照していない。asmdef 参照は増えていない。
- 現在の問いを阻害する findings: なし。この発見は実装と同じセッションであり、独立レビューではない。
- 後続スライスへ移送する findings: 空間の当たり領域、領域ごとの同時再生上限、Tick を Update へ登録すること、ミキサーアセットを使った実音、CRI と Wwise、公開文書のトークン源によるフェード記述。
- 実行したテストコマンドと -Filter: `dotnet run --project tools/SoundSystemOfflineTests/SoundSystemOfflineTests.csproj -c Release`。Unity の filter は使っていない。優先度、フェード、割り当ては Unity 無しで観測できるため。
- テスト結果: オフライン 6 件実行、失敗 0、exit 0。名前は Handle_DefaultIsInvalid_AndRegisteredValuesAreDense、Mix_StealsOnlyTheLowerOrOlderVoice_AndFades、Mix_PlayAndTick_DoNotAllocate、Player_ForwardsHandleAndVolume_WithoutAllocating、Player_RejectsMissingBackend、Sources_KeepClipTypesOffTheSharedPlaySurface。contract-audit は errors=0 warnings=0。
- 判定必須のうち未実行: 全 EditMode 回帰。Unity の SoundSystemTests も未実行。
- 重い検証を発見段階で限定実行した場合の理由と範囲: Unity バッチは実行していない。
- 未確認事項: Unity でのコンパイル、AudioMixer.SetFloat の実音、リスナーが無いときの聞こえ。GO ではない。
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
