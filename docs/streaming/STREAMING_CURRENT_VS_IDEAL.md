# Streaming — 現状と到着契約

> 視覚整理。正本は二枚に分かれている。
>
> - 現状: [STREAMING_CURRENT_SPEC.md](STREAMING_CURRENT_SPEC.md) / [§21](../../unity/Assets/Docs/Architecture/21-scene-streaming.md)
> - 到着: [§34](../../unity/Assets/Docs/Architecture/34-ondemand-spatial-policy.md)
>
> UpdateSystem の [CURRENT_VS_IDEAL](../updater/UPDATE_SYSTEM_CURRENT_VS_IDEAL.md) に相当する。
> 同じ主張の新旧を並べているのではない。役割が違う。

---

## 一文で

- **現状**: 座標列を名前に Format し、格子定数で中心を組み立て、点距離で切る。
- **到着**: 不透明な identity 列と、各シーンが持つ体積と、ヒステリシスで切る。

---

## 対照

| 項目 | 現状 | 到着（§34） |
|---|---|---|
| desired のキー | `Vector2Int`（矩形を展開した座標列） | 不透明な identity 文字列 |
| 距離の入力 | 格子定数で組み立てるセル中心 | データの体積（AABB / 球）の中心 |
| 候補 | Catalog が展開した座標の全件 | 呼び出し側が渡す集合 |
| 政策層が名前を | `CellIdentity.Format` する | 組み立てない |
| R-3（`SwitchScene` 禁止） | `IsCellId`（名前文法） | 距離政策の候補フラグ |
| 同じ体積に複数 identity | 座標キーで潰れる | 候補集合の差し替えで排他 |
| 格子 | ランタイムの空間プロトコル | 生成器が体積を焼く入力。HUD 用なら局所 |
| セル | FW の型（`CellIdentity` / `CellScene`） | SampleGame の作業単位 |
| 季節 | FW が知らない（維持） | FW が知らない（維持） |
| LoadType | 親の引っ張り（維持） | 親の引っ張り。値を増やさない |
| ヒステリシス / maxInFlight / 距離順 | 維持 | 維持 |
| 政策 / メカニズム分離 | 維持 | 維持 |

---

## 部分解に見えるもの（到着点ではない）

次は現状を一層だけ一般化した記録である。未来形で書いてはいけない。

| 層 | 何をしたか | 残った主キー |
|---|---|---|
| 当初 | dense `N×N` | 格子座標 |
| S-3 | 矩形の集合へ展開 | 格子座標のまま。`Format` も残った |
| 退役した修飾パース | 名前文法を延長 | 名前から座標を戻すこと |

到着点は「格子の一般化」ではない。候補＋体積＋ヒステリシスである。

---

## 読み分け

```text
公開面
  §34          … 到着契約（未実装）
  §21          … 現状の設計記録（チケット・生成器契約・受入値）
  本書 / CURRENT_SPEC … 今動いている値と経路

作業台 docs/handoff/
  移行 HANDOFF … 格子キーを殺す順序。現行レイアウトで口を通す手順はここだけ
  世界構図     … 四季の使い方。空間のキーは決めない
```
