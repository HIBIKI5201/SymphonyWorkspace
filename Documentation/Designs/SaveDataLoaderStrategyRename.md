# SaveDataLoader から SaveDataLoaderStrategy への改名 — Round L2

## 目的

セーブデータの保存先を差し替える拡張点を `SaveDataLoaderStrategy` へ改名する。DesignPhilosophy の「拡張点には役割を表すサフィックスを付ける」と「クラス名をクラス設計に厳密に合わせる」に従う。

`[Obsolete]` シムを残すため後方互換であり、**2.20.0** とする。シムの削除は Phase 6（3.0.0）。

## 前提の確認

| 前提 | 確認結果 |
| --- | --- |
| 型の可視性 | `SaveDataLoader` = `public abstract`、`PlayerPrefsSaveDataLoader` = `public abstract`（Template）、`JsonUtilitySaveDataLoader` / `NewtonsoftSaveDataLoader` = `internal sealed` |
| シリアライズ形式 | `SaveSystemConfig._loader` は `[SerializeReference]`。**アセットへ `{class, ns, asm}` が焼かれる** |
| 実アセットの内容 | `Assets/Resources/SymphonyFrameWork/SaveSystemConfig.asset` に `type: {class: JsonUtilitySaveDataLoader, ns: SymphonyFrameWork.System.SaveSystem, asm: SymphonyFrameWork}` |
| `MovedFromAttribute` | この Unity バージョンでコンパイル・インスタンス化できることを確認済み（4引数版 `(bool, string, string, string)`） |
| `[MovedFrom]` が `[SerializeReference]` を救えるか | **未検証。本 Round の実装中に実アセットで実証する**（下記） |

### `SceneManagerConfig` を本 Round から外す

当初 L2 に含める計画だったが、**別種の危険があるため独立した Round（L3）へ回す。**

`SymphonyConfigLocator.GetConfig<T>()` は次のように型名で Resources を引く。

```csharp
Resources.Load<T>($"SymphonyFrameWork/{typeof(T).Name}");
```

`SceneManagerConfig` → `SceneLoadConfig` にすると `SceneLoadConfig.asset` を探しに行き、既存の `SceneManagerConfig.asset` が見つからない。`SymphonyConfigManager.FileCheck<T>()` が既定値で新しいアセットを作るため、**利用側のシーン設定が黙って失われる。**

`[MovedFrom]` は型の解決を救う属性であり、`Resources.Load` のパス解決には効かない。アセットファイル自体を改名する移行処理が要るため、`[SerializeReference]` の救済とは別の検証が必要になる。**1つの Round に2種類のデータ破壊リスクを混ぜない。**

## 公開API

### 新しい拡張点

| 新 | 旧 | 可視性 |
| --- | --- | --- |
| `SaveDataLoaderStrategy` | `SaveDataLoader` | `public abstract` |
| `PlayerPrefsSaveDataLoaderStrategy` | `PlayerPrefsSaveDataLoader` | `public abstract` |

中身（`protected abstract` メンバー、`internal` の呼び出し口）は変更しない。

### `[Obsolete]` シム

```csharp
[Obsolete("SaveDataLoaderStrategyを継承してください。3.0.0で削除します。", error: false)]
public abstract class SaveDataLoader : SaveDataLoaderStrategy { }

[Obsolete("PlayerPrefsSaveDataLoaderStrategyを継承してください。3.0.0で削除します。", error: false)]
public abstract class PlayerPrefsSaveDataLoader : PlayerPrefsSaveDataLoaderStrategy { }
```

**旧型を継承した利用側のローダーはそのまま動く。** 新しい基底の派生になるため、`SaveSystemConfig` の `[SerializeReference]` フィールドにも入る。

シムは**メンバーを1つも持たない**。転送すべき実装が無く、継承関係だけで互換性が成立する。

### 内部実装の改名

| 新 | 旧 | 可視性 |
| --- | --- | --- |
| `JsonUtilitySaveDataLoaderStrategy` | `JsonUtilitySaveDataLoader` | `internal sealed` |
| `NewtonsoftSaveDataLoaderStrategy` | `NewtonsoftSaveDataLoader` | `internal sealed` |

`internal` なので利用側のソースは壊れない。**しかし型名がアセットへ焼かれているため、`[MovedFrom]` が無いと既存の `SaveSystemConfig.asset` が壊れる。**

```csharp
[MovedFrom(true, null, null, "JsonUtilitySaveDataLoader")]
internal sealed class JsonUtilitySaveDataLoaderStrategy : PlayerPrefsSaveDataLoaderStrategy
```

名前空間とアセンブリは変わらないため `sourceNamespace` と `sourceAssembly` は `null` を渡す。

### `SaveSystemConfig` のフィールド型

```csharp
[SerializeReference, SubclassSelector]
private SaveDataLoaderStrategy _loader = new JsonUtilitySaveDataLoaderStrategy();
```

`SaveSystemConfig` は `internal` なので、フィールド型の変更は公開APIに影響しない。フィールド名 `_loader` は変えないため、シリアライズ済みの参照はそのまま解決される。

## 変更する箇所

| ファイル | 変更 |
| --- | --- |
| `Runtime/System/SaveSystem/SaveDataLoaderStrategy.cs` | 新規。旧 `SaveDataLoader.cs` の中身を移す（ファイルごと改名） |
| `Runtime/System/SaveSystem/SaveDataLoader.cs` | `[Obsolete]` の空シムへ置き換える |
| `Runtime/System/SaveSystem/Template/PlayerPrefsSaveDataLoaderStrategy.cs` | 同上 |
| `Runtime/System/SaveSystem/Template/PlayerPrefsSaveDataLoader.cs` | `[Obsolete]` の空シム |
| `Runtime/System/SaveSystem/Internal/Infrastructure/JsonUtilitySaveDataLoaderStrategy.cs` | 改名 + `[MovedFrom]` |
| `Runtime/System/SaveSystem/Internal/Infrastructure/NewtonsoftSaveDataLoaderStrategy.cs` | 改名 + `[MovedFrom]` |
| `Runtime/Configs/Internal/SaveSystemConfig.cs` | フィールド型と既定値 |
| `Runtime/System/SaveSystem/SaveStore.cs`、`Internal/Application/SaveDataService.cs`、`Internal/SaveSystem.cs` | `Func<SaveDataLoaderStrategy>` などの型参照 |
| `Editor/PackageInitializer.cs`、`Editor/Administrator/UITK/CS/SaveDataRegistryWindow.cs` | 型参照 |
| `Runtime/Orchestrator/Internal/SymphonyOrchestrator.cs` | `ResolveSaveDataLoader` の戻り値型 |
| `README.md` / `AGENTS.md` / `Documentation~/*` | 記載 |

**フレームワーク内に旧型への参照を1件も残さない。** 残るとコンパイル警告が出るため、警告0が機械的な証拠になる。

## 依存方向

変更しない。

```text
SaveDataLoaderStrategy（拡張点）
    ^                    ^
    │                    │
PlayerPrefsSaveDataLoaderStrategy    SaveDataLoader（[Obsolete] シム）
    ^                                     ^
    │                                     │
JsonUtility... / Newtonsoft...      PlayerPrefsSaveDataLoader（[Obsolete] シム）
```

## エラー処理

変更しない。

## 影響範囲

- 旧型を継承したローダーはそのまま動き、非推奨警告が出る。移行は基底型名の置換だけ
- **既存の `SaveSystemConfig.asset` が壊れないことが本 Round の成否を決める。** `[MovedFrom]` が効かない場合は実装を中断して報告する
- `SaveSystemConfig` の `_loader` フィールド名は変えないため、`[SerializeReference]` の参照構造は維持される

## テストの置き場と種別

**自動テストは追加しない。** 検証対象は「Unity のシリアライズ機構が旧い型名を新しい型へ解決できるか」であり、テストアセンブリからは再現できない。`[SerializeReference]` の解決はアセットの読み込み時に Unity が行う。

代わりに**実アセットで実証する**（下記の動作確認手順3〜5）。既存の EditMode 222件・PlayMode 4件が引き続き全数成功することも確認する。

## 動作確認手順

1. Unity Scene 検証ガードに従い、親と submodule の dirty 状態を記録する
2. **改名前に `Assets/Resources/SymphonyFrameWork/SaveSystemConfig.asset` をバックアップする。** 旧い型名を含む状態を保全するため
3. 改名と `[MovedFrom]` の実装後、**アセットを書き換えずに** Unity へ再読み込みさせ、`SymphonyConfigLocator.GetConfig<SaveSystemConfig>().Loader` が null でなく、型が `JsonUtilitySaveDataLoaderStrategy` であることを確認する
4. 確認できない場合は**実装を中断し、報告する**。勝手に代替手段（アセットの手動書き換え、`FormerlySerializedAs` への切り替えなど）へ移らない
5. 確認後、アセットが Unity によって新しい型名へ再シリアライズされることを確認する
6. `uloop-clear-console` 後に `uloop-compile` でエラー0・**SymphonyFrameWork 由来の警告0**
7. `uloop-clear-console` を挟んで EditMode と PlayMode の全テストを実行し、`Success` / `Passed` / `Failed` / `Skipped` を記録する。**同じ結果が2回続くことを確認する**
8. 利用側の移行を模擬する。ホスト側へ旧 `SaveDataLoader` を継承した一時クラスを置き、`CS0618` が出たうえでコンパイルが通ることを確認して削除する
9. Save Data Sample を Play し、保存・再ロード・削除が従来どおり動くことを確認する
10. Project Settings のローダー選択（`SubclassSelector`）に新しい型が並び、選択して保存できることを確認する
11. この Round で追加・変更した `.cs` に UTF-8 BOM が付いていることを確認する

## バージョン判断

**マイナー（2.20.0）。** 後方互換な公開型 `SaveDataLoaderStrategy` と `PlayerPrefsSaveDataLoaderStrategy` を追加し、旧型は `[Obsolete]` として動作を維持する。

## この Round で触るバージョン関連ファイル

| ファイル | 変更 |
| --- | --- |
| `Assets/SymphonyFrameWork/package.json` | `2.19.0` → `2.20.0` |
| `Assets/SymphonyFrameWork/CHANGELOG.md` | 2.20.0 へ Add と Deprecated（移行方法）を記録 |
| `Assets/SymphonyFrameWork/README.md` | 現在のバージョンと Save Data の拡張例 |
| `Assets/SymphonyFrameWork/Documentation~/AgentUsage.md` | 拡張点の型名 |
| `Assets/SymphonyFrameWork/Documentation~/Architecture.md` | 構成図の型名 |

## ブランチ

`develop` から `feature/save-data-loader-strategy` を作成する。

## 後続 Round

- ~~**Round L3** — `SceneManagerConfig` → `SceneLoadConfig`~~ → **3.0.0 へ延期した。** 自動移行を実装して検証したところ、利用側の設定値が失われることを実測したため中断している。経緯は [SceneLoadConfigRename.md](./SceneLoadConfigRename.md)
- **3.0.0（Phase 6）** — 公開 enum 6型の `Enum` サフィックス化、`SaveDataRegistryEntryInfo` → `SaveDataEntryInfo`、`SaveDataRegistryWindow` の改名、全 `[Obsolete]` シムの削除
