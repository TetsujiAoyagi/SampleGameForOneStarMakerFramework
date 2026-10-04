# 7〜9. サウンド・入力・バックグラウンドサービス

> [ARCHITECTURE.md](../../ARCHITECTURE.md) に戻る

---

## 7. サウンド

### 7.1 任意の再生口と寿命

`OneStarMaker.Runtime.SoundSystem` は `SoundHandle`、`SoundPlayer`、`ISoundBackend` と、固定数の voice を持つ任意の `UnitySoundBackend` を提供する。`SoundPlayer` は backend を借用して転送するだけで、所有・Dispose しない。ミドルウェアへの差し替え成立や SampleGame の音楽配線は含まない。

backend の作成者は host を所有し、登録した全 `AudioClip` を backend の寿命全体にわたって保持する。クリップは `IAssetManagement` と `AssetOwner.Manual` で取得し、`backend.Dispose()` の後で `IAssetHandle<AudioClip>` を解放する。`AssetOwner.App` は Dispose を `ReleaseAll` より先に行う終了順序が明示されている場合だけ使用できる。`Scene(id)` / `Bind(go)` の自動失効する所有者は使えない。backend はクリップをロード・解放・破棄せず、個別登録解除も行わない。

`SoundHandle` は backend 内だけの登録順 1 始まりの値で、0 は無効。`IsValid` は正の値という構造だけを示す。別 backend の値は別クリップと一致し得るため渡してはいけない。登録は append-only で、破棄済み・null クリップ、Dispose 後の登録、整数上限の登録は拒否する。

### 7.2 論理ミックス経路と native 設定

`SoundVolumeId` は空間の領域ではなく backend 内だけの論理ミックス経路を指す。経路は Dispose まで残り、`DefaultVolume` は初期 gain 1・priority 0・reverb Off の ungrouped 経路。`RegisterVolume(settings)` は明示的に ungrouped、`RegisterVolume(group, settings)` は呼び出し側の `AudioMixerGroup` を backend の寿命まで借用する。null・破棄済みの configured group は登録できない。

各 voice は host 配下の専用 GameObject 上に `AudioSource` と `AudioReverbFilter` を1つずつ持つ。source は 2D・非 loop で、group の有無にかかわらず `source.volume = voiceGain * routeGain` を適用する。有限の gain は `[0,1]` に制限する。group は出力先だけを指定し、共有 mixer の exposed parameter は書き換えない。呼び出し側が mixer に設定した減衰・効果は論理 gain の保証外である。`TryGetAudibleGain` が返すのは現在の論理経路 gain で、可聴音量の計測値ではない。

`SoundReverb` は voice ごとの Unity native filter 設定であり、共有バスの残響 tail や正規化した wet mix ではない。レベルは millibel `[-10000,2000]`、減衰は秒 `[0.1,20]`、拡散は percent `[0,100]`。コンストラクタは disabled でも非有限値を拒否し、有限値を範囲へ制限する。`default` / `Off` は disabled として有効である。

filter は User preset を数値より先に選び、選択3値と固定値 `dryLevel/room/roomHF/roomLF=0`、`decayHFRatio=0.5`、`reflectionsLevel=-10000`、`reflectionsDelay=0`、`reverbDelay=0.04`、`hfReference=5000`、`lfReference=250`、`density=100` を設定する。Off または解放時は disabled にし、レベル `-10000`、減衰 `1`、拡散 `100` と固定値へリセットする。効果は各 source の group に入る前に作用する。

### 7.3 選択・フェード・停止

純粋な `SoundMix` が優先度と時間注入フェードを管理する。空き slot を先に使い、満杯なら最も低い priority を選ぶ。incoming より高い priority しかない場合は拒否し、同点なら古い voice を置き換える。`SoundVoiceId` の世代が stale voice 操作を防ぐ。ハンドル・経路・voice ID は backend 間で共有しない。

経路フェードは voice を解放しない。voice フェードが 0 に到達すると停止・解放する。0秒は戻る前に native 値と cleanup を適用する。負または非有限の時間・非有限 target は状態を変えず、有限 target を `[0,1]` に制限する。Play の非有限 gain、無効・未登録値、破棄済みクリップ・host・選択 source/filter、優先度拒否、Dispose 後は Invalid を返す。

生成・登録・再生・フェード・Tick・Dispose は Unity メインスレッド限定。backend は自動 Update loop を作らない。単一の呼び出し側所有者が `UpdateSystemRuntime` に Tick を登録し、そのフレーム順序を守る。負・非有限 delta は何も変更せず、0 delta はフェードを進めず cleanup できる。次の有効 Tick で `!AudioSource.isPlaying` の active voice を解放する。Play 前には回収しないため、同一フレームの容量・優先度は純粋 policy に従う。headless で再生が開始されなければ次 Tick で解放され得る。native Pause/Stop は backend が所有し、外からの操作は対応しない。

configured group が外部で破棄された場合、次の有効 Tick またはその経路への Play で該当 voice を同期停止・clear し、その経路への以降の Play を拒否する。default 出力へ自動転送しない。置換・解放・Dispose は source を停止して clip/group を clear し、filter を disabled/reset にする。Dispose は先に disposed を記録し、全 native 参照と借用登録表を消してから host を破棄する。PlayMode の遅延 Destroy にアセット解放順序を依存させない。繰り返し Dispose と遅い Play は無作用である。

native テストの component state は設定・参照 cleanup を検証する。可聴品質、実時間の自然終了、空間の listener 入退場、共有バスの残響、middleware の成立を保証しない。

---

## 8. 入力

### 8.1 構成

```
InputManager (OneStarMaker.Runtime)
  └── InputObserver … ActionMap の切り替え、イベント配信

NewStgCommonInput : InputManager (Game.Common)
  └── ゲーム固有の Action を enum で型安全に公開
      R3 の Observable でイベントを配信
```

### 8.2 ルール

- `InputActionAsset` は Unity の Input System で管理する。
- ActionMap の切り替え（Player ↔ UI）は `InputObserver.ChangeMode()` で行う。
- ゲーム固有の Action 定義は Game.Common 層の enum で管理し、OneStarMaker.Runtime は enum を知らない。
- イベント配信には R3 の `Observable` を使用する（旧プロジェクトの UniRx `IObservable` から移行）。

---

## 9. バックグラウンドサービス（HostedService）

### 9.1 設計

ASP.NET Core の `IHostedService` パターンの薄い移植（DI コンテナには依存しない。手動 DI 正式採用については [03-di.md](03-di.md) 参照）。

```
IHostBuilder
  └── Build() → IHostedServiceExecutor
                    ├── StartServicesAsync()  Starting → Start → Started
                    └── StopServicesAsync()   Stopping → Stop  → Stopped

IHostedService           … Start/Stop のみ
IHostedLifecycleService  … Starting/Started/Stopping/Stopped フック付き
BackgroundService        … 長時間実行タスクの基底クラス（UniTask ベース）
```

### 9.2 起動処理との統合

- `HostedServiceExecutor` は `AbstractApplicationInitializer` の起動フェーズ内で `StartServicesAsync` を呼ぶ。
- アプリ終了時（`Application.quitting`）に `StopServicesAsync` を呼ぶ。
- `IHostedService` / `IHostedLifecycleService` のインターフェースは特定の DI コンテナ・フレームワークに依存しない。

### 9.3 ルール

- サービスの登録は `IHostBuilder.Services.Add()` で起動前に行う。
- サービスの取得は `IHostedServiceExecutor.GetService<T>()` で行う。
- `BackgroundService` を継承して `ExecuteAsync` を実装すれば、バックグラウンドタスクを簡単に追加できる。
