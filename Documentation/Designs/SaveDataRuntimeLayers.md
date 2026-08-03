# Save Data Runtime レイヤー分割 — Round I1

## 目的

`SaveDataRegistry`（420行）が、公開Facade・キャッシュ保持・ロード順序・例外変換・ローダー解決・スナップショット生成をすべて1つの静的クラスで担っている。Scene Load（G1）と Service Locate（H1）で行ったのと同じ形へ分割し、責務ごとに型を分ける。

分割後も**公開APIのシグネチャと挙動は一切変えない**。`SaveDataRegistry` は転送に徹する。

## Round分割

| Round | 内容 | 状態 |
| --- | --- | --- |
| **I1**（本書） | Domain / Application / Infrastructure の分離。公開APIは不変 | 本Round |
| I2 | `SaveDataQuery` / `SaveDataDto` / `SaveDataViewModel`、`SaveDataRegistryWindow` の脱リフレクション | 次Round |

## この Round のスコープ外

**明示的に含めない。** いずれも破壊的変更か、別Phaseの担当。

| 項目 | 理由 | 担当 |
| --- | --- | --- |
| 非同期型を `Awaitable` へ変更 | 公開APIの戻り値が変わる | Phase 5 |
| `SaveDataRegistry` → `SaveStore` 改名 | 公開型の改名 | Phase 4 |
| `SaveDataLoader` → `SaveDataLoaderStrategy` 改名 | 公開型の改名 | Phase 4 |
| `SaveDataRegistryEntryInfo` から `Data` を除く | 公開メンバーの削除 | Phase 4（新 `SaveDataEntryInfo` は I2 で追加） |
| Editor Window の脱リフレクション | 表示層の作業 | I2 |

## アクセス手段の検証

実装前に現状を確認した結果。

| 対象 | 現状 |
| --- | --- |
| 内部状態（`_cache` / `_loadedTypes` / `_loadingTasks` / `_entrySnapshot` / `_loaderResolver` / `_cachedLoader`） | すべて `private static`。外部から到達できない |
| 既存の `internal` アクセサ | `IsInitialized`、`LoadedTypes`、`GetCurrentLoader`、`RefreshLoader`、`ConfigureLoaderResolver`、`ResetRuntimeState` |
| テストからの到達 | `Runtime/AssemblyInfo.cs` が `SymphonyFrameWork.Tests.Editor` / `.Tests.Runtime` へ `InternalsVisibleTo` 済み。**内部型を直接テストできる** |
| `SaveDataRegistryEntryInfo.Data` | 可変の `SaveDataContent` を公開している。I2 で新 Info を足す際の論点 |
| `Internal/SaveSystem.cs` | Composition から `ConfigureLoaderResolver` を呼ぶだけの17行のシム |

**`_loadingTasks: Dictionary<Type, Task>` は進行中の `Task` を複数の呼び出し元で共有している。** `ArchitectureRevision.md` はこれを Awaitable 移行時に再設計対象としているが、**I1 では構造を保つ**。ここを触ると非同期の意味論が変わり、Phase 5 の判断を先取りすることになる。

## 内部設計

### 型と責務

| 型 | レイヤー | 責務 |
| --- | --- | --- |
| `SaveDataEntryEntity` | Domain | セーブデータ型ごとのエントリ。`DataType` を同一性とし、キャッシュ内容とロード済み状態を持つ。状態遷移メソッドからのみ変更する |
| `SaveDataEntryRegistry` | Application | 型をキーにEntityを登録・検索・除去する。全消去とスナップショット生成もここ |
| `SaveDataService` | Application | ロード・保存・削除の順序、重複ロードの排除、例外の `SaveDataOperationException` への変換 |
| `SaveDataLoader` | Infrastructure（既存） | 保存先へのI/O。**変更しない** |
| `SaveDataRegistry` | Adaptor（既存の公開Facade） | 引数検証と転送のみ。状態を持たない |

`Registry` が Entity の所有と検索、`Service` が処理順という分担は G1・H1 と同じ。

### 名前の衝突を避ける

内部Registryは **`SaveDataEntryRegistry`** とする。公開Facadeの `SaveDataRegistry` と1文字違いにならないよう、Entityの名前（`SaveDataEntryEntity`）に揃える。`ArchitectureRevision.md` の移行表もこの名前を指定している。

### スレッド安全性

現在 `_lock` で `_cache` / `_loadedTypes` / `_loadingTasks` を保護している。**ロックの所有を `SaveDataEntryRegistry` へ移す。** `SaveDataService` はロックを持たず、Registryの操作経由で整合を保つ。ロックの粒度と保護対象を現状から変えない。

## ファイル構成

### 新規

| パス | 名前空間 |
| --- | --- |
| `Runtime/System/SaveSystem/Internal/Domain/SaveDataEntryEntity.cs` | `SymphonyFrameWork.System.SaveSystem` |
| `Runtime/System/SaveSystem/Internal/Application/SaveDataEntryRegistry.cs` | 同上 |
| `Runtime/System/SaveSystem/Internal/Application/SaveDataService.cs` | 同上 |

`Internal` とレイヤー名は名前空間へ含めない。G1・H1 と同じ規則。

### 変更

| パス | 内容 |
| --- | --- |
| `Runtime/System/SaveSystem/SaveDataRegistry.cs` | 状態と処理を新型へ委譲。引数検証と転送だけ残す |
| `Runtime/System/SaveSystem/Internal/SaveSystem.cs` | Service の生成と注入へ変更 |
| `Runtime/Orchestrator/Internal/SymphonyOrchestrator.cs` | 必要なら初期化引数の追従 |

### 移動

`Internal/JsonUtilitySaveDataLoader.cs` と `Internal/NewtonsoftSaveDataLoader.cs` を `Internal/Infrastructure/` へ移す。G1・H1 でInfrastructureフォルダを作った構成に揃える。**`.meta` を必ず一緒に動かす。**

## 依存方向

```text
SaveDataRegistry（Adaptor / 公開Facade）
        │ 転送
        v
SaveDataService（Application）──> SaveDataEntryRegistry（Application）
        │                                  │ 所有
        │ 契約                             v
        v                          SaveDataEntryEntity（Domain）
SaveDataLoader（Infrastructure / 既存の拡張点）
```

- Domain は Unity API と外部I/Oを参照しない
- Application は `SaveDataLoader` の契約経由でのみ保存先へ触れる
- Composition（`SaveSystem`）が Service を生成し、ローダー解決を注入する

## エラー処理

現在の挙動を維持する。

- ローダーまたは保存先の失敗は `SaveDataOperationException` へ変換し、`Operation` / `DataType` / `LoaderType` と原因例外を保持する
- キャンセルは `SaveDataOperationException` へ変換せず `OperationCanceledException` のまま伝播する
- 破損データをデフォルト状態へ戻す復旧動作を変えない
- `ResetRuntimeState` は多重呼び出しで安全

## 影響範囲

**破壊的変更ではない。** 公開APIのシグネチャ、シリアライズ形式、例外の種類と条件はいずれも変わらない。

`internal` メンバー（`LoadedTypes`、`GetCurrentLoader` など）は `SymphonyMcpTools` と Editor Window が参照しているため、**同じ名前と意味で提供し続ける**。参照元が壊れないことを実装後に確認する。

## テストの置き場と種別

`Assets/SymphonyFrameWork/Tests/Editor/` に EditMode テストを追加する。`InternalsVisibleTo` により内部型を直接検証できる。

| 対象 | 検証内容 | 書き方 |
| --- | --- | --- |
| `SaveDataEntryEntity` | 同一性が `DataType` であること、状態遷移（未ロード→ロード済み）、内容の差し替え | 純粋C#。`new` して直接呼ぶ |
| `SaveDataEntryRegistry` | 型キーの登録・検索・除去、全消去、スナップショットの内容 | 純粋C#。Unity APIに依存しない |

`SaveDataService` はローダーのI/Oを伴うため、この Round ではテスト用のローダー差し替えを新設せず、既存の公開API経由の挙動確認に留める。**テスト専用の拡張点を製品コードへ作らない。**

## 動作確認手順

1. 親と submodule の dirty 状態を記録する
2. `uloop-clear-console` の後 `uloop-compile` を実行し、エラー0・警告0
3. EditMode と PlayMode の全テストが成功
4. Save Data Sample を Play し、保存・再ロード・削除・Registry 表示が従来どおり動く
5. Play Mode の開始・終了を2回繰り返し、`SymphonyMcpTools.GetSaveDataJson()` の `entries` が2回目の開始直後に空であること
6. `SymphonyMcpTools.GetSaveDataJson()` が `SaveDataContent` の中身を含まないこと（Round C の要件を維持）
7. Play Mode 停止後に package の `.unity` / `.prefab` 差分が無いこと

## バージョン判断

**マイナー（2.16.0）。** 公開APIの追加・変更・削除が無く、内部構造の分割のみ。

## この Round で触るバージョン関連ファイル

| ファイル | 触る箇所 |
| --- | --- |
| `Assets/SymphonyFrameWork/package.json` | `version` を `2.15.0` → `2.16.0` |
| `Assets/SymphonyFrameWork/CHANGELOG.md` | `## [2.16.0]` の見出しと本文 |
| `Assets/SymphonyFrameWork/README.md` | 「現在のバージョン」を `2.16.0` へ |

`AGENTS.md` は触らない（利用側から見えるAPIが変わらないため）。

## GitHub Issue

I1 と完全一致する既存 Issue は無い。`develop` から `feature/save-data-runtime-layers` を作成する。

## 実装の担当

**Codex CLI ワーカーの残枠が0%**（リセットまで約5日）のため、この Round は自分で実装する。`references/worker.md` の「ワーカーが使えないときは自分で実装する」に従い、設計書・検証・振り返りは省略しない。独立した目が入らない分は、上記「テストの置き場と種別」のテストと、動作確認手順の1項目ずつの照合で補う。
