# DebugCommand による GameObject 検査・編集

- type: `program`
- status: **製品判断を作業指示から転記した計画。A2 は未実施。A3 は未実施。GO は未判定。**
- owner: 発注者（製品判断と後続の採否）。計画文の起票は 2026-10-06 の実装担当。
- created: 2026-10-06
- expires / harvest 期限: **2026-12-15、または本プログラムを置き換える revision。** コマンドが実装され、恒久契約を公開面へ移したら本ファイルと着手済みスライス HANDOFF を削除する。
- harvest to: 現在の計画状態は `docs/README.md`。実装後に残す契約は `unity/Assets/Docs/Architecture/12-telemetry.md` の「incoming control の任意 catalog」へ追記する。DebugSocket の hierarchy / inspector スキーマには移さない。
- PR base: `develop`。`main` へはpushしない。
- H1 / H2 / `external-current-v1`: 未適用。本プログラムに harness task は作らない。
- 関連スライス: [DEBUG_GO_LIST_SELECT.md](DEBUG_GO_LIST_SELECT.md) が最初の実装スライス。後続は本ファイルのスライス表が所有する。

本書は複数スライスの製品境界である。Phase C / C' の完了記録は各スライス HANDOFF に置く。ここにあるコマンド仕様は、発注者がこの作業で既に合意した範囲だけを実装してよい。合意に無い公開 API、バス、毎フレーム走査は足さない。

## 1. このプログラムが固定する製品判断

DebugCommand の目録を、GameObject の実行時検査と限定編集に広げる。最初の能力は次の6コマンドだけである。汎用の Component プロパティ編集はしない。

| コマンド名 | スライス | 効果 |
|---|---|---|
| `go.list` | S1 | 要求のたびに対象を列挙し、1ページを返す |
| `go.select` | S1 | `instanceId` で選択する。壊れていれば選択を消す |
| `go.set-active` | S2 | その GameObject の `SetActive` |
| `go.get-transform` | S3 | local position / euler / scale を読む |
| `go.set-transform` | S3 | 渡された local 成分だけを書く |
| `go.set-renderer-enabled` | S4 | その GameObject 自身の Renderer を1つ有効・無効にする |

名前は catalog の Ordinal 完全一致である。大文字小文字のゆれは未登録になる。組込の `ping` / `runtime-diagnostics` とは衝突しない。組込コマンドの分岐は増やさない。

### 同一性

- 主キーの JSON 名は `instanceId`。値は `EntityId.ToULong(gameObject.GetEntityId())` の十進数字だけからなる文字列である。
- JSON 数値では 53bit を超える値を壊す。数値の `instanceId` は拒否する。
- `GetInstanceID()` は Unity 6.5 でコンパイルエラーになるため使わない。
- hierarchy snapshot の `NodeId` はサービスが採番した別のトークンである。コマンドの `instanceId` と読み替えない。hierarchy registry をコマンドが読んだり書いたりしない。
- 階層パスは表示専用である。シーンのロードで変わり、同名で曖昧になる。パスでは検索も選択もしない。

### いつ走るか

- `go.list` は、呼び出し側がコマンドを送ったときだけ走る。毎フレームでは走らない。ランタイムから階層を push しない。
- Studio 側のポーリングは将来の対向実装であり、本プログラムのランタイムはスケジュールしない。
- ソケット経路の main thread 化は、既存の `MainThreadDebugCommandDispatcher` が dispatcher の前に行う。新しいキューは作らない。
- catalog の `TryExecute` は従来どおり呼び出し側スレッドで同期実行する。Unity 世界を触る実装は、生成したスレッド以外ではシーンを読まず、選択も消さない。

### ページ

- `offset` の既定は 0。0 未満は失敗。
- `limit` の既定は 64。有効範囲は 1 以上 128 以下。範囲外は失敗で、黙って切り詰めない。
- 応答は `count` と `hasMore` を持つ。ページに入った要素と、次が1件あるかだけを見る。全件の巨大な中間リストは作らない。LINQ は使わない。
- 表示文字列（`name`、`sceneName`、`displayPath`）は各 256 UTF-16 コード単位で打ち切る。超えた分は末尾を `...` にする。`instanceId` は打ち切らない。
- 走査順は、ロード済みシーンの index 昇順、各シーンの root 順、兄弟は sibling index 順の preorder。1回の呼び出しの中ではこの順。ロードをまたいだ安定は保証しない。

### 選択

- 選択状態は `ulong` の `instanceId` だけを持つ。プロセス全体の static にはしない。所有は App 寿命の登録側。
- コマンド登録の closure は、公開済み catalog 契約どおり、シーン所有オブジェクトを強参照で保持しない。走査用バッファは各呼び出しの終了時にクリアする。
- 選択中の id がスコープ内に見つからないとき、その選択は壊れたものとして消す。`go.select` は失敗を返す。`go.list` はページ自体は成功し、`selectionCleared` を true にする。
- 壊れた JSON、未知プロパティ、ページ範囲の失敗、スレッド不一致では選択を変えない。
- `instanceId` は破棄後に再利用され得る。強参照を残さないため、再利用された id は別オブジェクトに見える。この取り違えは本プログラムの残リスクとし、強参照アンカーを足して解消しない。

### シーンの範囲

- `SceneManager.sceneCount` のうち `IsValid` かつ `isLoaded` のシーンだけ。アクティブシーンだけに限定しない。
- DontDestroyOnLoad シーンは、Unity がそれをロード済みシーンとして出しているとき含む。シーン名は `scene.name` を表示する。
- 非アクティブな GameObject も含む。
- `HideFlags.HideAndDontSave` を持つオブジェクトは、その部分木ごと除外する。エディタ用の一時オブジェクトをページに混ぜない。
- prefab 資産や、ロードされていないシーンは対象外。

### `go.set-active`

- 対象は payload の `instanceId`。無いときは現在の選択。選択も無いときは失敗し、シーンは変えない。
- `active` は必須の JSON 真偽値。欠けるときは失敗し、`SetActive` を呼ばない。
- 呼ぶのはその GameObject の `SetActive` だけである。親の active は変えない。
- 親が非アクティブな子を active にしても、`activeSelf` は要求どおりになり、`activeInHierarchy` は false のままである。成功メッセージは `Set activeSelf. An inactive parent still keeps activeInHierarchy false.` とする。親が邪魔していないときは `Set GameObject active.`
- このコマンドは選択を変えない。対象が現在の選択で、かつ壊れているときだけ選択を消す。
- 応答の行には変更後の `activeSelf` と `activeInHierarchy` を入れる。

### Transform

- 読むのも書くのも local position、local euler angles、local scale だけである。world 座標と Rigidbody の移動 API は使わない。
- set は渡された成分だけを書く。各成分オブジェクトは `x` `y` `z` の有限数をすべて持つ。NaN と Infinity は拒否し、その呼び出しでは transform を変えない。
- euler を書いたあと、読み戻しは Unity が保持した値である。別の quaternion API は今スライス群では作らない。
- 選択は変えない。対象 id が壊れていて、それが現在の選択なら選択だけ消す。

### Renderer

- 対象 GameObject の `GetComponents<Renderer>()` だけを見る。子は見ない。
- `rendererIndex` が無いときは 0。これはコンポーネント順の先頭である。MeshRenderer に限定しない。
- 範囲外、または Renderer が0件のときは失敗し、どれも変えない。複数 Renderer を一括で切り替えない。
- 成功応答は `instanceId`、`rendererIndex`、`rendererCount`、`enabled`、具体型の `typeName`。
- 選択は変えない。

### 有効化

- コマンドは `DebugGameObjectCommands` の登録メソッドを呼んだ catalog にだけ現れる。
- `CreateDebugCommandDispatcher` の既定である `NullDebugCommandDispatcher` は変えない。
- `DebugSocketService` のコンストラクタ、UpdateSystem、sceneLoaded からは登録しない。
- DebugSocket が有効なだけで GameObject コマンドが有効になる、とはしない。App が catalog を作り、登録し、`CatalogDebugCommandDispatcher` を返すのが明示の有効化である。
- SampleGame の `AppInitializer` は本プログラムの初期スライスでは変えない。公開面の「SampleGame に catalog が配線されているという意味ではない」を維持する。対向アプリへの接続は後続スライス `DEBUG_GO_APP_OPT_IN` が所有する。

## 2. スライス

| ID | 所有する問い | 着手条件 |
|---|---|---|
| S1 `DEBUG_GO_LIST_SELECT.md` | 明示登録した catalog が、`go.list` と `go.select` を既存の TryExecute で完結できるか | 本書の製品判断。A2/A3 が無いことはスライス本文に記録する |
| S2 `DEBUG_GO_SET_ACTIVE.md` | 凍結した set-active が、親の非アクティブを壊さず選択契約を守るか | S1 の catalog 登録と行 DTO が存在する |
| S3 `DEBUG_GO_TRANSFORM.md` | local transform の読み書きが、有限数以外を拒否し選択を勝手に変えないか | S1 の対象解決 |
| S4 `DEBUG_GO_RENDERER.md` | 先頭または明示 index の1 Renderer だけを切り替えられるか | S1 の対象解決 |
| S5 `DEBUG_GO_APP_OPT_IN.md` | サンプルまたは明示の起動経路が、既定 dispatcher を置き換えずにコマンドを載せるか | S1 が catalog 上で動く。UI アクセシビリティ作業とはファイルを共有しない |

S2 以降の HANDOFF は、そのスライスの着手時に作る。本書の表に書いた挙動をスライス側で広げない。

## 3. 対象外

- 汎用 Component 編集、マテリアル、Animator、Collider、レイヤー、タグの変更。
- DebugSocket の hierarchy snapshot / delta、inspector query / detail、YAML プロトコルの変更。
- DebugStudio アプリの画面、ポーリング UI、新しいメッセージ種別。
- 毎フレームの走査、ランタイムからの push、UpdateSystem への登録。
- 第二のコマンドバス、asmdef 参照の追加、`SceneState` の変更。
- UI アクセシビリティ（ボタンの Narrator、UIToolkit のフォーカスと名前）。進行中の当該 PR のファイルは編集しない。
- script 実行基盤。
- 本番で常時払うコスト。未登録のとき、シーンを歩くオブジェクトを常駐させない。

## 4. 検証の分担

- S1 の中核は Unity 無しの offline 実行で、偽の世界を catalog に登録して JSON を見る。
- Unity 上のシーン走査、DontDestroyOnLoad、偽 null、Renderer の実コンポーネントは EditMode または Player の残りである。結果 XML を手で作らない。
- 本プログラム自体は文書であり、Unity テストの完了バーを持たない。GO は各スライスが、それぞれの HANDOFF に書いた判定必須を実行したときだけ主張できる。A2/A3 が未実施のあいだ、スライス担当は GO を記録しない。

## 5. 停止

次が必要になったら、そのスライスの実装を止めて計画へ戻す。

- 新しいコマンドバス、asmdef 参照、毎フレーム登録、プロトコル種別。
- catalog closure にシーンオブジェクトの強参照を残す実装。
- 本書の表に無いコマンド、またはパスを主キーにする実装。
- 既定 dispatcher を GameObject コマンドへすり替える実装。
