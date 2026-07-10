# 14. アーキテクチャレビュー — ギャップ分析 & フレームワーク比較

> レビュー日: 2026-03-07  
> レビュー対象: OneStarMaker Framework (Phase 1 完了時点)  
> 視点: ソフトウェアアーキテクトによるセルフレビュー

> **注記 (2026-07-06): 本書は 2026-03-07 時点のスナップショットであり、歴史的記録として原文のまま保存する。**
> その後の実装で解消済みの指摘: F13 リソースシステム（AssetManagement + AssetResidentCache 実装済み、[13](13-resource-system.md)/[19](19-asset-resident-cache-tickets.md)）、
> Addressables 生呼び（`IAssetManagement` 経由に移行）、テストカバレッジ（UpdateSystem / AssetManagement / Build のテスト追加）。
> また推奨アクション 2「VContainer LifetimeScope 統合」は取り止め、**手動 DI を正式採用**した（[03-di.md](03-di.md) 参照）。

---

## 目次

1. [目標 vs 現状ギャップ](#1-目標-vs-現状ギャップ)
2. [既存 Unity フレームワークとの比較: 優位性](#2-既存-unity-フレームワークとの比較-優位性)
3. [既存 Unity フレームワークとの比較: 弱点・リスク](#3-既存-unity-フレームワークとの比較-弱点リスク)
4. [総合評価](#4-総合評価)
5. [推奨アクション](#5-推奨アクション)

---

## 1. 目標 vs 現状ギャップ

| # | 目標機能 | 設計 | 実装 | ギャップ | リスク |
|---|---|:---:|:---:|---|---|
| **F1** | 3-Assembly 分割 (Foundation/Runtime/Debug) | ✅ | ✅ | — | — |
| **F2** | Config (3ソースマージ) | ✅ | ✅ | — | — |
| **F3** | Logging (ZLogger + `ILogger<T>` / `ILoggerFactory`) | ✅ | ✅ | — | — |
| **F4** | Telemetry (Activity Span + JSONL Sink) | ✅ | ✅ | — | — |
| **F5** | ZString ホットパス最適化 | ✅ | ✅ | TMP `SetTextFormat` 不可（NuGet版制約） | UPM 版切替が必要になる可能性 |
| **F6** | Scene 管理 (14状態 + partial×4) | ✅ | ✅ | — | 状態数の認知負荷は高い |
| **F7** | UI 管理 (6レイヤー + Blocker + SiblingIndex) | ✅ | ✅ | — | — |
| **F8** | Scene Graph Editor | ✅ | ✅ | — | GraphView は Experimental API |
| **F9** | App 起動 3フェーズ | ✅ | ✅ | — | — |
| **F10** | HostedService パターン | ✅ | ❌ | **設計のみ。Phase 2 未着手** | サービスライフサイクル管理が手動 |
| **F11** | SoundService | ✅ | ❌ | **設計のみ。Phase 2 未着手** | 音がない＝動作確認困難 |
| **F12** | InputManager + R3 配信 | ✅ | ❌ | **設計のみ。Phase 2 未着手** | Game 層が入力を受けられない |
| **F13** | リソースシステム + メモリバジェット | ✅ | ❌ | **設計のみ。T9-T15 未着手** | アセット管理が Addressables 生呼び |
| **F14** | 手動 DI の配線拡張性 | ✅ | ✅ | `DependOnAll` をコンポジションルートとする手動 DI を正式採用 | サービス増加で Factory 配線が読みづらくなる可能性。具体的な配線コストを計測して再評価する |
| **F15** | Game 実装 (Title→InGame 遷移) | — | ⚠️ | TitleScene のみ。InGame/Player/Grid 未着手 | フレームワークの検証手段がない |
| **F16** | UI Toolkit 段階移行 | 構想 | ❌ | Phase 4。設計ドキュメントもなし | 移行パスが不透明 |
| **F17** | テスト | ✅ | ⚠️ | SceneDirector テストのみ存在 | カバレッジが極めて低い |

### サマリ

- **設計完成度:** 17 中 15 が設計済み（88%）
- **実装完成度:** 17 中 9 が実装済み（53%） — Phase 1 インフラのみ
- **最大ギャップ:** Phase 2 サービス群（F10–F12）が全て未着手。Game 層が動くための「最後の一マイル」が欠けている

---

## 2. 既存 Unity フレームワークとの比較: 優位性

| 観点 | OneStarMaker | 一般的な Unity プロジェクト / 既存 FW | 優位理由 |
|---|---|---|---|
| **依存方向の強制** | Assembly 分割 + 禁止ルール明文化。Game→FW のみ、逆参照不可。手動 DI の配線は DependOnAll のコンポジションルートへ集約 | Zenject/Extenject は全層から参照可能。`[Inject]` が散在しがち | コンパイル時に違反を検出。「知らないうちにフレームワークがゲーム固有型を参照」が原理的に不可能 |
| **シーンライフサイクル** | 14状態 + キャンセル窓 + PoNR の明示的モデル。SceneLifecycleManager がオーナーシップを独占 | Unity 標準は Load/Unload のみ。多くのプロジェクトで状態管理が暗黙的 | 二重ロード/二重アンロード/キャンセル競合を構造的に排除。旧プロジェクトで実際に踏んだバグが再発不可能 |
| **ログ** | `AppLoggerFactory` が `ILoggerFactory` を構成。Game 層は `ILogger<T>` + ZLogger 拡張を直接参照 | `Debug.Log` 直接呼び、Release ビルドで残留するパターンが多い | rolling file / DebugSocket / カテゴリフィルタが Game 層ログにも適用される。Service Locator なしで依存が型シグネチャから追跡可能 |
| **テレメトリ設計** | OTel 互換 TraceId/SpanId。JSONL ローカル → 将来 Elastic。IL2CPP 安全 | 多くの Unity プロジェクトにはテレメトリ基盤がない。あっても Analytics SDK 依存 | シーン遷移・起動・FPS を統一フォーマットで因果関係付きで記録。OTEL SDK なしで軽量実装 |
| **Cysharp 系ライブラリの採用** | UniTask + R3 + LitMotion + ZLogger + ZString を用途ごとに採用。依存配線はコンテナを使わず DependOnAll の手動 DI で行う | Zenject + DOTween + UniRx + 自前ログ = API 思想がバラバラ | async / Observable / Tween / 構造化ログの主要 API を統一しつつ、依存の出所をコンストラクタとコンポジションルートから追跡できる |
| **暗黙知の文書化** | IK-1〜IK-10 レベルで「なぜそうなのか」をテーブル化 | README + コード内コメントが散在。設計意図が消失 | 新メンバーが「なぜ UIView は1シーン1つなのか」を文書から即座に理解可能 |
| **トレードオフ記録** | 全設計セクションに「採用/却下理由」テーブル | 設計理由が Slack/議事録に埋没 | ADR (Architecture Decision Records) 相当。後から「なぜ sortingOrder を使わないのか」が追跡可能 |
| **Scene Graph Editor** | ノードベース MVVM + 3層ファイル分離 (ノード/エッジ/レイアウト) | 多くのプロジェクトは ScriptableObject 手編集か Inspector ベース | 親子関係をビジュアル編集でき、Git マージ衝突を構造的に回避。SceneResource 生成を自動化 |
| **GC 最適化戦略** | ZString をホットパス限定で適用。判断根拠を文書化 | 「全部 ZString にする」か「何もしない」の二択になりがち | コスト/ベネフィットの判断基準が明確。導入範囲を自覚的に管理 |

---

## 3. 既存 Unity フレームワークとの比較: 弱点・リスク

| 観点 | OneStarMaker の現状 | 既存 FW / ベストプラクティス | 問題点・改善案 |
|---|---|---|---|
| **Over-Engineering リスク** | 14状態のシーンライフサイクル、6レイヤー UI、3層ファイル分割 Editor — これらは **横スクロール STG** のために構築されている | GameFramework / QFramework 等は必要十分な粒度。小規模ゲームには軽量 FW が適する | STG に14状態は過剰設計の疑い。**実際にこの複雑さを使い切るゲーム規模かの検証が未完了**。Title→InGame を一通り動かして初めて妥当性が判断できる |
| **実装の空洞化** | 設計ドキュメント 17本 vs 実際に動く Game シーン 1本 (TitleScene) | 「動くもの優先」が Unity 開発の定石。Unity 公式テンプレートは最小動作から拡張 | **設計文書は充実しているが、ゲームが動かない**。Phase 2 サービス (Sound/Input/HostedService) が全部揃わないと Game ループが成立しない |
| **テストカバレッジ** | SceneDirector テストのみ。Foundation (Config/Logging/Telemetry) のテストなし | 成熟した FW は Foundation 層こそテスト密度が高い | `AppLoggerFactory` の AdditionalFormatter、`JsonFileTelemetrySink` のローリング/フラッシュ、`AppConfig` の3ソースマージ — **最もバグが出やすい箇所にテストがない** |
| **static テレメトリ** | `AppTelemetry` は static class。Sink リストは static フィールド + lock | VContainer / DI ベースなら `IAppTelemetry` interface で注入可能 | テスト時にグローバル状態がリークする。並列テスト不可。DI で解決可能だが Foundation が DI を知らない制約と衝突 |
| **手動 DI の配線コスト** | `ISceneFactory` と `DependOnAll` を経由して依存を明示的に配線する | DI コンテナでスコープを自動管理する設計もある | サービス増加で Factory の引数や配線が読みにくくなる可能性がある。実際の重複・変更頻度・テスト容易性を観測し、閾値を超えた場合だけ方針を再評価する |
| **MonoBehaviour 依存** | UICommon / UIView / DebugProfilerView が MonoBehaviour | Pure C# + interface で抽象化し、MonoBehaviour は最外殻のみにする設計もある | UICommon が MonoBehaviour のため、ユニットテストが困難。`FindRootComponent<T>()` がランタイム依存する |
| **Addressables 生呼び** | SceneDirector が `Addressables.LoadSceneAsync` / `UnloadSceneAsync` を直接呼ぶ | リソースシステム (F13) のインターフェースを通してロードするのが理想 | リソースシステム設計は完了しているが未実装。**現在の SceneDirector はリファクタリングが必要になる** |
| **エラーリカバリ** | シーンロード失敗時の回復パスが薄い。`catch` でログ + 再throw が主 | Resilience パターン (リトライ/フォールバック/Circuit Breaker) | STG では「ロード失敗→タイトルに戻す」程度で十分だが、その具体実装がない |
| **パフォーマンス実測なし** | ZString 導入判断はあるが、Profiler による Before/After 計測データがない | Unity Profiler / Memory Profiler で GC Alloc を実測し判断するのが定石 | 「ZString で GC を減らした」が **定量的に検証されていない**。Deep Profile による実測が必要 |
| **NuGet vs UPM の混在** | ZString/ZLogger は NuGet (DLL)。VContainer/R3/LitMotion は UPM | Cysharp 系は全て UPM で統一するのが推奨。NuGet → IL2CPP の code stripping 問題のリスク | `link.xml` や `preserve` 属性の管理が必要になる可能性。`ZStringFormatExtension` の TMP 機能が使えなかった問題もこれが原因 |
| **ドキュメントの維持コスト** | 設計ドキュメント 13 本以上。暗黙知テーブル + 施行ルールテーブル + トレードオフテーブル | 多くのプロジェクトはドキュメントが腐る | ドキュメントの品質は現時点で高いが、**コードと乖離し始めるタイミングが必ず来る**。CI でのドキュメント検証手段がない |
| **チーム前提の設計 / 1人開発の現実** | 施行ルール・コーディング規約・コードレビュー前提のルールが多数ある | 1人開発なら規約は内面化されている。ルールの enforcement は self-discipline のみ | 「コードレビュー」が施行手段として書かれているが、レビュワーがいなければ機能しない。Roslyn Analyzer や EditorConfig で自動化すべき |

---

## 4. 総合評価

| 評価軸 | スコア | コメント |
|---|---|---|
| **設計の一貫性** | ★★★★★ | Cysharp 統一、一方向依存、暗黙知の文書化 — 設計思想の筋が通っている |
| **設計の網羅性** | ★★★★☆ | 17 機能中 15 が設計済み。エラーリカバリとパフォーマンス検証の設計が弱い |
| **実装の完成度** | ★★☆☆☆ | Phase 1 完了のみ。**Game ループが動かない**。ここが最大の課題 |
| **テスタビリティ** | ★★★☆☆ | DI 設計は良いが、static テレメトリ + MonoBehaviour 依存 + テスト不足 |
| **保守性** | ★★★★☆ | Doc 完備、3-Assembly 分離。ただしドキュメント腐敗リスク |
| **スケーラビリティ** | ★★★★☆ | 汎用 FW として再利用可能な設計。ただし STG 以外での検証ゼロ |
| **開発速度への貢献** | ★★☆☆☆ | 現時点では **フレームワークが開発速度のボトルネック**。動くゲームより先に FW を磨いている |

### 評価総括

設計品質は高水準。Cysharp エコシステム統一 + 一方向依存の厳格な Assembly 分割 + ADR レベルのトレードオフ記録は、企業規模プロジェクトでも通用する。

しかし **「設計は完璧だがゲームが動かない」** 状態は危険信号。フレームワークの妥当性は動くゲームでしか検証できない。14状態のシーンライフサイクルが STG に必要かは、Title→InGame→Result を実際に遷移させて初めて判断できる。

---

## 5. 推奨アクション（優先順）

| 順位 | アクション | 理由 | 見積り |
|---|---|---|---|
| **1** | **Phase 2 サービス最小実装 → Title→InGame 動線を貫通させる** | フレームワークの妥当性は動くゲームでしか検証できない。Sound/Input は stub でもよいから動線をつなぐ | Phase 2 (中) |
| **2** | **Phase 2 の Factory 配線コストを観測する** | 手動 DI を正式方針として維持し、サービス追加時の重複・変更範囲・テスト容易性を記録する。具体的な痛みが確認できた場合だけ DI 方針を再評価する | Phase 2 を通じて |
| **3** | **Foundation 層のユニットテスト追加** | AppConfig マージ、JsonFileTelemetrySink ローリング、TelemetryRecord — 最もバグが出やすい箇所 | 随時 |
| **4** | **Unity Profiler で GC Alloc 実測** | ZString 投資の ROI を数値で確認。DeepProfile でフレーム毎 alloc を計測 | 短期 |
| **5** | **NuGet → UPM 統一検討** | ZString/ZLogger の UPM 化で `SetTextFormat` 問題を解消し、IL2CPP stripping リスクも排除 | 短期 |
| **6** | **Roslyn Analyzer / EditorConfig 導入** | 1人開発で「コードレビュー」が施行手段として機能しないため、静的解析で自動化 | 短期 |
| **7** | **エラーリカバリの具体実装** | 「ロード失敗→タイトルに戻す」フォールバックパスを SceneDirector に組み込む | Phase 2–3 |

---

## 付録: 本レビューの前提

- 旧プロジェクト (NewGradious) で実際に踏んだ問題が §14「前プロジェクトからの教訓」として ARCHITECTURE.md に記録されており、本フレームワークの設計判断の多くはそこからの学び
- レビュー対象は Phase 1 完了 + テレメトリ (T1-T8) + ZString ホットパス最適化完了時点のスナップショット
- 比較対象として挙げた「既存 Unity FW」は GameFramework, QFramework, Zenject ベースの一般的な Unity アーキテクチャパターンを指す
