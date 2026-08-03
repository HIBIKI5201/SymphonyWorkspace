# SaveDataRegistry から SaveStore への改名 — Round L1

## 目的

`SaveDataRegistry` を `SaveStore` へ改名する。Round I1 / I2 で内部を分割した結果、**この型はレジストリではなくストアになっている**。実際のレジストリは `SaveDataEntryRegistry`（Application）であり、名前が役割と食い違っている。

DesignPhilosophy の「クラス名をクラス設計に厳密に合わせる」に従う。

`[Obsolete]` の転送シムを残すため、本 Round は**後方互換**であり **2.19.0** とする。シムの削除は Phase 6（3.0.0）。

## Phase 4 の再定義

着手前の調査で、ArchitectureRevision の Phase 4 が計画どおりには後方互換にならないことが分かった。**Phase 4 の範囲を「シムを作れる改名だけ」へ狭める。**

| 対象 | シムの可否 | 扱い |
| --- | --- | --- |
| `SaveDataRegistry` → `SaveStore` | 可（`[Obsolete]` 転送 static class） | **本 Round** |
| `SaveDataLoader` → `SaveDataLoaderStrategy` | 可（旧を新基底の `[Obsolete]` 派生にする） | Round L2 |
| `SceneManagerConfig` → `SceneLoadConfig` | 可（`[MovedFrom]`） | Round L2 |
| 公開 enum 6型の `Enum` サフィックス化 | **不可** | **3.0.0 へ回す** |

### enum のシムが作れない理由

別の enum 型どうしに暗黙変換は存在せず、enum に演算子を定義することもできない。`SceneLoadState` → `SceneLoadStateEnum` にすると `SceneLoadInfo.State` の型が変わり、それを読む利用側コードが必ず壊れる。旧 enum を `[Obsolete]` で残しても、公開メンバーの型が変わる時点で破壊的である。

対象は6型で、ArchitectureRevision が言う「7つ」より1つ少ない。`AssetProtectionModeEnum` が既にサフィックスを持っているためである。

| enum | 場所 |
| --- | --- |
| `LocateType` | `Runtime/System/ServiceLocator/LocateType.cs` |
| `SceneLoadState` | `Runtime/System/SceneLoader/SceneLoadState.cs` |
| `SaveDataOperation` | `Runtime/System/SaveSystem/SaveDataOperation.cs` |
| `LogKind` | `Runtime/Debug/SymphonyDebugLogger.cs` |
| `InitializeType` | `Runtime/Utility/SymphonyVisualElement.cs` |
| `LoadType` | `Runtime/Utility/SymphonyVisualElement.cs` |

## 前提の確認

| 前提 | 確認結果 |
| --- | --- |
| `SaveDataRegistry` の参照件数 | 15ファイル・67箇所。最多は `SaveDataRegistryWindow.cs`（21箇所）と Sample（19箇所） |
| `SaveDataRegistry` はシリアライズされるか | **されない。** `static class` であり、`.asset` にも `.unity` にも型名は現れない。`[MovedFrom]` は不要 |
| 公開メンバー | `Exists<T>/Exists(Type)`、`Get<T>/Get(Type)`、`LoadAsync`×2、`SaveAsync`×2、`DeleteAsync`×2、`GetEntries()` |
| internal メンバー | `IsInitialized`、`CurrentViewModel`、`OnCurrentViewModelChanged`、`RefreshLoader`、`ConfigureLoaderResolver`、`ResetRuntimeState`、`GetCurrentLoader` |
| `SaveDataRegistryEntryInfo` の扱い | **本 Round では改名しない**（下記） |

### `SaveDataRegistryEntryInfo` を改名しない理由

`GetEntries()` は `IReadOnlyList<SaveDataRegistryEntryInfo>` を返す。要素型を `SaveDataEntryInfo` へ変えると、struct に暗黙変換を定義しても `IReadOnlyList<A>` と `IReadOnlyList<B>` は変換できないため、**戻り値の型が変わって破壊的になる**。

シムを作れないので 3.0.0 へ回す。`SaveDataRegistry` はシムとして残るため、名前が指す型が消えるわけではない。

同じ理由で `SaveDataRegistryWindow`（Editor）も本 Round では改名しない。UXML から型名で参照されており、改名すると `SymphonyWindow.uxml` の更新が要る。Editor 専用型なので 3.0.0 でまとめる。

## 公開API

### `SaveStore`（新規）

`Runtime/System/SaveSystem/SaveStore.cs`。`SaveDataRegistry` の**全メンバーをそのまま移す**。シグネチャ、例外の種類と条件、XMLドキュメントを変更しない。

```csharp
public static class SaveStore
{
    public static bool Exists<T>() where T : SaveDataContent, new();
    public static bool Exists(Type dataType);
    public static T Get<T>() where T : SaveDataContent, new();
    public static SaveDataContent Get(Type dataType);
    public static ValueTask<T> LoadAsync<T>(CancellationToken token = default) where T : SaveDataContent, new();
    public static ValueTask LoadAsync(Type dataType, CancellationToken token = default);
    public static ValueTask SaveAsync<T>(CancellationToken token = default) where T : SaveDataContent, new();
    public static ValueTask SaveAsync(Type dataType, CancellationToken token = default);
    public static ValueTask DeleteAsync<T>(CancellationToken token = default) where T : SaveDataContent, new();
    public static ValueTask DeleteAsync(Type dataType, CancellationToken token = default);
    public static IReadOnlyList<SaveDataRegistryEntryInfo> GetEntries();
}
```

internal メンバーも `SaveStore` へ移す。**状態（`_service` / `_query` / `_viewModel`）を持つのは `SaveStore` だけ**にする。

### `SaveDataRegistry`（`[Obsolete]` シム）

```csharp
[Obsolete("SaveStoreを使用してください。3.0.0で削除します。", error: false)]
public static class SaveDataRegistry
{
    // 公開メンバーのみ SaveStore へ転送する
}
```

- **転送するのは公開メンバーだけ。** internal メンバーは残さない。フレームワーク内部は `SaveStore` を直接使う
- 状態を持たない。すべて `SaveStore` へ委譲する
- **フレームワーク内に `SaveDataRegistry` への参照を1件も残さない。** 1件でも残ると非推奨警告が出て「警告0」の基準を満たせない

## 変更する箇所

### Runtime

| ファイル | 変更 |
| --- | --- |
| `SaveSystem/SaveStore.cs` | 新規。旧 `SaveDataRegistry.cs` の中身を移す |
| `SaveSystem/SaveDataRegistry.cs` | `[Obsolete]` 転送シムへ置き換える |
| `SaveSystem/Internal/SaveSystem.cs` | `SaveStore.ConfigureLoaderResolver` を呼ぶ |
| `Orchestrator/Internal/SymphonyOrchestrator.cs` | `SaveStore.ResetRuntimeState` を登録する |
| `SaveSystem/SaveDataOperationException.cs`、`SaveDataLoader.cs`、`Internal/Application/SaveDataService.cs`、`Internal/Adaptor/SaveDataQuery.cs` | XMLドキュメントの `<see cref>` を `SaveStore` へ |
| `SaveSystem/SaveDataRegistryEntryInfo.cs` | XMLドキュメントの参照のみ更新。型名は変えない |

### Editor

| ファイル | 変更 |
| --- | --- |
| `Administrator/UITK/CS/SaveDataRegistryWindow.cs` | 21箇所を `SaveStore` へ。クラス名は変えない |
| `SettingProvider/SaveSystemSettingProvider.cs` | `SaveStore.RefreshLoader()` |
| `Debug/SymphonyMcpTools.cs` | `SaveStore.IsInitialized` / `GetEntries()` |
| `PackageInitializer.cs` | `SaveStore.ConfigureLoaderResolver` |
| `Administrator/SymphonyAdministrator.cs` | 参照の更新 |

### Samples とドキュメント

`Samples/Runtime/SaveDataSystemSample/Scripts/SaveDataSystemSample_Controller.cs` の19箇所を `SaveStore` へ変更する。**Sample が非推奨APIを使っている状態にしない。**

`README.md`、`AGENTS.md`、`Documentation~/AgentUsage.md`、`Documentation~/Architecture.md` の記載を `SaveStore` へ更新し、旧名からの移行を CHANGELOG へ書く。

### テスト

`Tests/Editor/SaveDataQueryTests.cs` の3箇所は `SaveDataRegistryEntryInfo` への参照であり、型名を変えないため**変更不要**。

## 依存方向

変更しない。`SaveStore` が旧 `SaveDataRegistry` と同じ位置に立つ。

```text
SaveStore ──Command──> SaveDataService ──> SaveDataEntryRegistry ──> SaveDataEntryEntity
    │
    └──Query─────────> SaveDataQuery

SaveDataRegistry（[Obsolete] シム）──> SaveStore
```

## エラー処理

変更しない。未初期化は `SymphonyNotInitializedException`、引数不正は `ArgumentException` / `ArgumentNullException`、ローダー失敗は `SaveDataOperationException`。

シムは引数検証を行わず、そのまま `SaveStore` へ渡す。**検証を二重に書くと、片方だけ直したときに挙動が分岐する。**

## 影響範囲

- 公開APIのシグネチャ、例外の種類と条件、シリアライズ形式はいずれも変更しない
- `SaveDataRegistry` を使っている利用側コードは**そのまま動き、非推奨警告が出る**。移行は `SaveDataRegistry` を `SaveStore` へ置換するだけ
- `SaveDataRegistryEntryInfo` と `SaveDataRegistryWindow` の名前は据え置き。3.0.0 で扱う
- Sample を新APIへ更新するため、Sample を取り込み済みの利用側プロジェクトは再取り込みで差分が出る

## テストの置き場と種別

**新しいテストは追加しない。** 改名と転送だけであり、検証すべき新しい振る舞いが無い。

代わりに次で担保する。

- 既存の EditMode 222件・PlayMode 4件が全数成功すること。`SaveStore` 経由で同じ結果になることの確認になる
- **コンパイル警告0**。これが「フレームワーク内に旧APIへの参照が1件も残っていない」ことの機械的な証拠になる。`[Obsolete]` は参照があれば必ず警告を出す
- `rg -n "SaveDataRegistry\." Runtime Editor Samples -g '*.cs'` が0件であること（`SaveDataRegistry.cs` のシム定義自体を除く）

シムが正しく転送することを確かめる EditMode テストを1件だけ追加するかは検討したが、**転送先が同じ static メンバーであり、テストは「同じものを呼んでいる」以上のことを言えない**ため追加しない。誤って転送先を間違えればコンパイルが通らないか、既存テストが落ちる。

## 動作確認手順

1. Unity Scene 検証ガードに従い、親と submodule の dirty 状態を記録する
2. `uloop-clear-console` 後に `uloop-compile` を実行し、Error 0、**SymphonyFrameWork 由来の Warning 0** を確認する。非推奨警告が出たら内部参照が残っている
3. `uloop-clear-console` を挟んで EditMode と PlayMode の全テストを実行し、`Success` / `Passed` / `Failed` / `Skipped` を記録する。**同じ結果が2回続くことを確認する**
4. `rg -n "SaveDataRegistry\." Runtime Editor Samples -g '*.cs'` がシム定義以外0件であることを確認する
5. Save Data Sample を Play し、保存・再ロード・削除が従来どおり動くことを確認する
6. Symphony Administrator の Save Data パネルが従来どおり表示・操作できることを確認する
7. `SymphonyMcpTools.GetSaveDataJson()` が従来と同じ JSON を返すことを確認する
8. **利用側の移行を模擬する**: ホスト側（`Assets/Scripts/`）へ `SaveDataRegistry.Get<T>()` を書いた一時ファイルを置き、非推奨警告が出たうえでコンパイルが通ることを確認する。確認後に削除する
9. Play Mode の開始・終了を2回繰り返し、状態が残らないことを確認する
10. Console の Error / Exception が0件であることを確認する
11. この Round で追加・変更した `.cs` に UTF-8 BOM が付いていることを確認する

## バージョン判断

**マイナー（2.19.0）。** 後方互換な公開型 `SaveStore` を追加し、旧 `SaveDataRegistry` は `[Obsolete]` として動作を維持する。DesignPhilosophy の「既存APIを削除・変更する前に代替手段を用意し、可能な場合は `[Obsolete]` の移行期間を設ける」に従う。

## この Round で触るバージョン関連ファイル

| ファイル | 変更 |
| --- | --- |
| `Assets/SymphonyFrameWork/package.json` | `2.18.1` → `2.19.0` |
| `Assets/SymphonyFrameWork/CHANGELOG.md` | 2.19.0 へ Add（`SaveStore`）と Deprecated（`SaveDataRegistry`、移行方法）を記録 |
| `Assets/SymphonyFrameWork/README.md` | 現在のバージョンと Save Data の記載 |
| `Assets/SymphonyFrameWork/AGENTS.md` | API 早見表 |
| `Assets/SymphonyFrameWork/Documentation~/AgentUsage.md` | Save Data System の記載 |
| `Assets/SymphonyFrameWork/Documentation~/Architecture.md` | 構成図の型名 |

## ブランチ

`develop` から `feature/save-store-rename` を作成する。

## 後続 Round

- **Round L2（2.20.0）** — `SaveDataLoader` → `SaveDataLoaderStrategy`、`SceneManagerConfig` → `SceneLoadConfig`。**`[SerializeReference]` が `{class, ns, asm}` をアセットへ焼いているため `[MovedFrom]` が必須**。`MovedFromAttribute` がこの Unity バージョンで使えることは確認済み
- **3.0.0（Phase 6）** — 公開 enum 6型の `Enum` サフィックス化、`SaveDataRegistryEntryInfo` → `SaveDataEntryInfo`、`SaveDataRegistryWindow` の改名、全 `[Obsolete]` シムの削除
