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

- **現状**: 距離政策・生成器・R-3 は identity と体積／候補フラグで動く。名前から座標を戻すのは factory、`CellScene`、子 identity 導出などに残る。
- **到着**: 不透明な identity 列と、各シーンが持つ体積と、ヒステリシスで切る。**どこにも名前文法が無い。**

---

## 対照

| 項目 | 現状 | 到着（§34） |
|---|---|---|
| desired のキー | 不透明な identity 文字列（**M-1 で到着**） | 不透明な identity 文字列 |
| 距離の入力 | `SceneResource` の AABB の中心（**M-1 で到着**） | データの体積（AABB / 球）の中心 |
| 候補 | Catalog の全件を Driver が候補集合へ組む | 呼び出し側が渡す集合 |
| 政策層が名前を | 組み立てない（**M-1 で到着**） | 組み立てない |
| 生成器の既存収集 / policy | identity 文字列（**M-2 で到着**） | identity 文字列 |
| R-3（`SwitchScene` 禁止） | `SceneResource.StreamByDistance`（**M-3 で到着**） | 距離政策の候補フラグ |
| 同じ体積に複数 identity | 政策層・生成器とも許す（**M-2 で到着**） | 候補集合の差し替えで排他 |
| 格子 | 生成器入力・スポーン・HUD（**M-1 で距離経路から外れた**） | 生成器が体積を焼く入力。HUD 用なら局所 |
| セル | SampleGame の型（**M-4 で到着**） | SampleGame の作業単位 |
| 季節 | FW が知らない（維持） | FW が知らない（維持） |
| LoadType | 親の引っ張り（維持） | 親の引っ張り。値を増やさない |
| ヒステリシス / maxInFlight / 距離順 | 維持 | 維持 |
| 政策 / メカニズム分離 | 維持 | 維持 |

太字の M-* は完了した移行スライスの番号である。個別 HANDOFF は harvest 後に削除済みで、公開面はその作業指示に依存しない。

---

## 部分解に見えるもの（到着点ではない）

次は現状を一層だけ一般化した記録である。未来形で書いてはいけない。

| 層 | 何をしたか | 残った主キー |
|---|---|---|
| 当初 | dense `N×N` | 格子座標 |
| S-3 | 矩形の集合へ展開 | 格子座標のまま。`Format` も残った |
| 退役した修飾パース | 名前文法を延長 | 名前から座標を戻すこと |

到着点は「格子の一般化」ではない。候補＋体積＋ヒステリシスである。

M-1 はこの列に入らない。走査範囲を広げたのではなく、**キーを座標から identity ＋ 体積へ替えた**からである。M-2 と M-3 により生成器と R-3 も名前文法から外れた。factory、`CellScene`、子 identity 導出、Driver の Format は S-4 に残る。

---

## 読み分け

```text
公開面
  §34          … 到着契約（未実装）
  §21          … 現状の設計記録（チケット・生成器契約・受入値）
  本書 / CURRENT_SPEC … 今動いている値と経路

作業台 docs/handoff/
  世界構図     … 四季の使い方。空間のキーは決めない
```

M-1〜M-4 と S-3 の記録は CURRENT_SPEC（公開面）。実装指示ではない。完了済み HANDOFF は復活させない。
