# Save Data Query / ViewModel — Round I2

## 目的

Round I1 で分離した `SaveDataEntryEntity` / `SaveDataEntryRegistry` / `SaveDataService` の**読み取り経路**を Adaptor の `SaveDataQuery` へ集約し、Editor Window の毎フレーム polling を状態変更 event の購読へ置き換える。Service Locate の Round H2 と同じ形へ揃える。

I1 完了時点では次が残っている。

- 一覧の生成（`GetEntrySnapshot`）が Application の `SaveDataEntryRegistry` にあり、Query が無い
- `SaveDataRegistryWindow` が `SymphonyAdministrator.Update()` から毎 Editor フレーム駆動され、表示署名の比較で差分を検出している
- 読み込み済みかどうかが公開情報に無く、Window は `Data != null`（＝キャッシュの有無）を「Loaded」と誤って表示している
- MCP 診断が `internal` アクセサ `SaveDataRegistry.LoadedTypes` に依存している

本 Round は I1 を含む `develop` から開始し、単独で検証・リリース可能な **2.17.0** とする。Phase 4 の改名（`SaveDataRegistry` → `SaveStore`、`SaveDataLoader` → `SaveDataLoaderStrategy`）は含めない。

## 前提の確認

設計を確定する前に実コードで確認した事実を記録する。

| 前提 | 確認結果 |
| --- | --- |
| Editor から internal 型へ到達できるか | `Runtime/AssemblyInfo.cs` に `InternalsVisibleTo("SymphonyFrameWork.Editor")` がある。新しい asmdef 参照は不要 |
| テストから internal 型へ到達できるか | 同ファイルに `SymphonyFrameWork.Tests.Editor` / `.Runtime` がある |
| `SaveDataRegistry` の状態 | `_service` は `private static`。既存の internal アクセサは `IsInitialized` / `LoadedTypes` / `RefreshLoader` / `ConfigureLoaderResolver` / `ResetRuntimeState` / `GetCurrentLoader` |
| Save Data は Play Mode 専用か | **違う。** `Editor/PackageInitializer.cs:29` が Edit Mode でも `ConfigureLoaderResolver` を呼ぶ。Window は Play Mode 外でも動作する |
| `_service` はいつ差し替わるか | `ConfigureLoaderResolver` のみ。Edit Mode 起動時（`PackageInitializer`）と Play Mode 開始時（`SymphonyOrchestrator.cs:46` の `SaveSystem.Initialize`）の 2 回 |
| `ResetRuntimeState` は `_service` を破棄するか | しない。`_service.Reset()` を呼ぶだけなので、Play Mode 終了で ViewModel は生き残る |
| Window にリフレクションは残っているか | **残っていない。** `SaveDataRegistryWindow` のリフレクションは AppDomain の型走査（`GetTypesSafe` / `IsSupportedSaveDataType`）で、これは機能そのものであり除去対象ではない。internal 状態を読むリフレクションは `PauseWindow.cs:29` の `typeof(PauseManager).GetField("_pause", ...)` だけで、**Round J の対象**とする |

Service Locate との最大の違いは「Edit Mode でも初期化される」点である。`ServiceLocatorWindow` は `EditorApplication.playModeStateChanged` だけで再接続できたが、Save Data では Edit Mode 側の接続が必要になる。Play Mode 遷移ではなく **ViewModel の差し替え自体を event で通知する**方式を採る。

## 公開API

### `SaveDataRegistryEntryInfo` へ `IsLoaded` を追加する

```csharp
public readonly struct SaveDataRegistryEntryInfo
{
    public Type DataType { get; }
    public SaveDataContent Data { get; }
    public string SaveDate { get; }
    public bool IsLoaded { get; }   // 追加
}
```

`Data` は「キャッシュされているインスタンス」であって「永続化データを読み込み済み」ではない。両者は別の状態であり、現在の Window はこれを取り違えている。`IsLoaded` を公開することで、Window と MCP が `internal` アクセサ無しで正しい状態を得られる。

生成は Adaptor（`SaveDataQuery`）の責務とし、constructor は `internal` のまま `(Type, SaveDataContent, bool)` へ変更する。

### 削除する `internal` アクセサ

`SaveDataRegistry.LoadedTypes` を削除する。唯一の利用者だった `SymphonyMcpTools.GetSaveDataJson()` が `IsLoaded` を使うため不要になる。H2 で `RegisteredInstances` / `SingletonRoot` を削除したのと同じ整理である。

既存の公開 API のシグネチャ、例外の種類と条件、シリアライズ形式はいずれも変更しない。

### `GetEntries()` の並び順

`SaveDataRegistry.GetEntries()` の戻り値を **`DataType.FullName` の ordinal 昇順**へ揃える。従来は `Dictionary` の列挙順（挿入順に依存する未定義の順序）だった。順序を契約として定めることで、Window と MCP が独自に並べ替える必要がなくなる。

## 内部型

### `SaveDataQuery`

`Runtime/System/SaveSystem/Internal/Adaptor/SaveDataQuery.cs` へ追加する。

```csharp
internal sealed class SaveDataQuery
{
    internal SaveDataQuery(SaveDataEntryRegistry registry);
    internal IReadOnlyList<SaveDataRegistryEntryInfo> GetInfos();
    internal IReadOnlyList<SaveDataDto> GetDtos();
}
```

- Query だけが `SaveDataEntryRegistry` と `SaveDataEntryEntity` を読む
- 一覧は `DataType.FullName ?? DataType.Name` の ordinal 昇順、変更不能なスナップショット
- Command、ローダー、例外変換、ログを扱わない

**永続化データの存在確認（`Exists`）は Query に含めない。** ローダーへの I/O であり、状態変更のたびに全型分の I/O が走ることになる。「保存済みかどうか」は従来どおり Editor Window が `SaveDataRegistry.Exists` を必要な行に対してだけ呼ぶ。

### スナップショットのキャッシュを廃止する

`SaveDataEntryRegistry` の `GetEntrySnapshot()` / `_entrySnapshot` / `_entrySnapshotDirty` を削除し、Query が毎回新しい一覧を構築する。

理由は 2 つある。

1. **既存のキャッシュは不正確だった。** `_entrySnapshotDirty` はエントリの新規作成と全消去でしか立たず、読み込み済み状態の変化では立たない。`IsLoaded` を公開する以上そのままでは使えない
2. **保存で `SaveDate` が変わってもレジストリの構造は変わらない。** version 番号ベースのキャッシュにしても保存を検出できない

polling を廃止するので、一覧の構築は「状態が変わったとき」だけになる。毎フレームの再構築は起きない。

代わりに `SaveDataEntryRegistry` へ次を追加する。

```csharp
internal IReadOnlyList<SaveDataEntryEntity> GetEntities();  // lock 内で複製して返す
internal int Version { get; }                                // 実際に状態が変わったときだけ増える
```

`GetEntities()` はロック内で複製する。`_entries` を辞書のまま公開すると、バックグラウンドで進行中のロードと列挙が競合する。

`Version` は「エントリが新規作成された」「読み込み済み状態が変化した」「全消去された」ときにだけ増やす。Service が「実際に何か変わったか」を判定するために使う。

### `SaveDataDto`

`Runtime/System/SaveSystem/Internal/Adaptor/SaveDataDto.cs` へ追加する不変値とする。

```csharp
internal readonly struct SaveDataDto : IEquatable<SaveDataDto>
{
    internal Type DataType { get; }
    internal string DataTypeName { get; }
    internal string SaveDate { get; }
    internal bool IsLoaded { get; }
}
```

- `DataType` を含めるのは、Window が選択行に対して Load / Save / Delete を実行するために型そのものを必要とするため。`SaveDataRegistryEntryInfo.DataType` として既に公開されている情報であり、内部状態の露出にはあたらない
- `SaveDataContent` の実体は含めない。Window は選択中の 1 件だけを `SaveDataRegistry.Get` で取得する
- constructor は `internal`。**生成するのは Query だけ**とする

Window の行は Dto そのものではない。行には「永続化データが存在するか」（`Exists` の結果）が要るが、それは Query に含めない値である。Window 側に private な行の型を置き、Dto と `Exists` の結果を突き合わせて組み立てる。Dto を Editor が生成できるようにすると、Adaptor 以外が Dto を作れることになるため採らない。

### `SaveDataViewModel`

`Runtime/System/SaveSystem/Internal/View/SaveDataViewModel.cs` へ追加する。

- constructor で `SaveDataQuery` と `SaveDataService` を受け取る
- 初期値を `query.GetDtos()` から生成する
- `service.OnStateChanged` を購読し、変更時だけ最新 Dto 一覧を取得する
- `IReadOnlyReactiveProperty<IReadOnlyList<SaveDataDto>> Entries` を公開する
- 一覧用 comparer は件数、順序、各 Dto の値を比較し、内容同値なら通知しない
- `Dispose` で購読と `ReactiveProperty` を冪等に解放する

## 状態変更 event

`SaveDataService` へ `internal event Action OnStateChanged` を追加する。

発行するのは、**表示内容が変わりうる操作が完了したとき**に限る。

| 操作 | 発行 | 理由 |
| --- | --- | --- |
| `LoadAsync` | 完了時に必ず（失敗時も） | 読み込み済み状態と内容が変わる。失敗しても新しいエントリが増えている |
| `SaveAsync` | 完了時に必ず | `SaveDate` が変わる。レジストリの構造は変わらないので version では検出できない |
| `DeleteAsync` | 完了時に必ず | 内容が既定値へ戻る |
| `Reset` | `Version` が変わったときだけ | 空の状態を繰り返し消去しても通知しない |
| `Get` | **発行しない** | 新しいエントリを作る場合は必ず未読み込みであり、内部で `LoadAsync` が走ってそちらが発行する。既存の読み込み済みエントリを返す場合は何も変わらない |
| `Exists` | 発行しない | 読み取りのみ |

`Get` が毎フレーム呼ばれても一覧の再構築が起きないことが重要である。

### 発行時のスレッドと例外

発行はその操作を完了させたスレッドで行う。Unity の同期コンテキストにより、メインスレッドから始まった `await` はメインスレッドへ戻るため、同梱ローダーと通常の利用ではメインスレッドで発行される。ただし `SaveDataLoader` は利用側が差し替えられる拡張点であり、`ConfigureAwait(false)` を使う実装やバックグラウンドスレッドからの呼び出しでは、メインスレッド外で完了しうる。

`ReactiveProperty.SetValue` はメインスレッド外で `InvalidOperationException` を投げる。これがセーブ・ロードの呼び出し側へ伝播すると、**デバッグ表示の都合でセーブが失敗する**ことになる。

したがって発行側で購読者の例外を隔離する。

```csharp
private void RaiseStateChanged()
{
    try
    {
        OnStateChanged?.Invoke();
    }
    catch (Exception exception)
    {
        Debug.LogException(exception);
    }
}
```

握り潰さずログへ出す。購読者は現時点で `SaveDataViewModel`（デバッグ表示専用）のみであり、その失敗を保存処理の失敗にしない。

## 公開Facade と Composition

```text
SaveDataRegistry
  Command ──> SaveDataService
  Query   ──> SaveDataQuery
  View    ──> SaveDataViewModel（internal accessor のみ）
```

`SaveDataRegistry` へ次を追加する。

```csharp
internal static SaveDataViewModel CurrentViewModel { get; }
internal static event Action OnCurrentViewModelChanged;
```

- `ConfigureLoaderResolver` が Registry / Service / Query / ViewModel を結合し、**古い ViewModel を Dispose してから** `OnCurrentViewModelChanged` を発行する
- `ResetRuntimeState` は `_service.Reset()` のままとし、ViewModel を差し替えない。Reset は `OnStateChanged` として ViewModel に届き、一覧が空になる
- `GetEntries()` は Query へ転送する。未初期化時は従来どおり例外ではなく空一覧を返す
- `IsInitialized` の意味は変えない

Orchestrator の初期化順と `SaveSystem.Initialize` の入口は変更しない。

## Editor Window

`SaveDataRegistryWindow` から polling を除去する。

- `Update()` を削除する。`SymphonyAdministrator.Update()` から `_saveDataRegistryWindow?.Update()` を削除する（残るのは `_pauseWindow?.Update()` のみ）
- `Initialize_S` で `SaveDataRegistry.OnCurrentViewModelChanged` を購読し、現在の ViewModel へ接続する
- `Entries.Subscribe(ApplyEntries)` で変更時だけ一覧を更新する
- `ApplyEntries` が、Editor が走査した対応型一覧と Dto 一覧を突き合わせて private な行の型（`SaveDataEntryRow`）を作る。キャッシュに無い型は `SaveDataRegistry.Exists` が true の場合だけ行にする（従来と同じ）
- 未選択のまま新しいエントリが現れた場合の自動選択も `ApplyEntries` の中で行う
- `_lastViewSignature` と `BuildViewSignature` を削除する。変更検出は `ReactiveProperty` の comparer が行う
- 行の表示は Dto の `IsLoaded` を使う。`Loaded` / `Saved` / `Empty` の 3 状態という表示自体は変えない
- `Dispose` で ViewModel 購読と `OnCurrentViewModelChanged` の購読を解除する
- Load / Save / Delete ボタンの処理は変更しない。実行後は Service の event 経由で一覧が更新される

**既知の制限（従来と同じ）**: Unity の外部でセーブファイルが増減しても一覧には反映されない。反映するには全対象型に対する `Exists` の定期実行が必要で、I/O を毎フレーム走らせることになるため行わない。Window を開き直すか、いずれかの操作を行えば更新される。

## MCP診断

`SymphonyMcpTools.GetSaveDataJson()` は `SaveDataRegistry.GetEntries()` の `IsLoaded` を使う。`LoadedTypes` は参照しない。

JSON のフィールド名と意味は維持する。

```json
{
  "initialized": true,
  "entries": [
    { "typeName": "Namespace.PlayerData", "saveDate": "2026-08-03 12:00:00", "loaded": true }
  ]
}
```

`SaveDataContent` の中身を含めない規則は維持する。

## ファイル構成

### 新規

| パス | レイヤー | 公開範囲 |
| --- | --- | --- |
| `Runtime/System/SaveSystem/Internal/Adaptor/SaveDataQuery.cs` | Adaptor Query | internal sealed |
| `Runtime/System/SaveSystem/Internal/Adaptor/SaveDataDto.cs` | Adaptor Dto | internal readonly struct |
| `Runtime/System/SaveSystem/Internal/View/SaveDataViewModel.cs` | View | internal sealed |
| `Tests/Editor/SaveDataQueryTests.cs` | EditMode test | test assembly |
| `Tests/Editor/SaveDataViewModelTests.cs` | EditMode test | test assembly |

`Internal/Adaptor.meta` と `Internal/View.meta` は Unity に生成させる。

### 変更

- `Runtime/System/SaveSystem/SaveDataRegistryEntryInfo.cs`
- `Runtime/System/SaveSystem/SaveDataRegistry.cs`
- `Runtime/System/SaveSystem/Internal/Application/SaveDataEntryRegistry.cs`
- `Runtime/System/SaveSystem/Internal/Application/SaveDataService.cs`
- `Editor/Administrator/UITK/CS/SaveDataRegistryWindow.cs`
- `Editor/Administrator/SymphonyAdministrator.cs`
- `Editor/Debug/SymphonyMcpTools.cs`
- `Tests/Editor/SaveDataEntryRegistryTests.cs`
- `package.json` / `CHANGELOG.md` / `README.md`
- `Documentation~/Architecture.md` / `Documentation~/AgentUsage.md`

## 依存方向

```text
SaveDataRegistry ──Command──> SaveDataService ──> SaveDataEntryRegistry ──> SaveDataEntryEntity
       │                            │
       └──Query───────────────────> SaveDataQuery ──────────┘
                                          │
                          ┌───────────────┴───────────────┐
                          v                               v
              SaveDataRegistryEntryInfo             SaveDataDto
                                                          │
SaveDataService.OnStateChanged ──> SaveDataViewModel ──────┘
                                          │
                            IReadOnlyReactiveProperty
                                          │
                                          v
                              SaveDataRegistryWindow
```

Application は Query、Info、Dto、ViewModel へ依存しない。Editor は ViewModel / Dto と公開 Facade だけを参照する。

## エラー処理

- 未初期化時の `GetEntries()` は従来どおり空一覧
- 購読者の例外は発行側で捕捉してログへ出し、保存・読み込みへ伝播させない
- ViewModel 未接続の Window は例外を出さず空表示にする
- MCP は既存どおり例外を JSON の `error` へ変換する

## テストの置き場と種別

すべて `Tests/Editor/` の EditMode テストとして追加する。`InternalsVisibleTo` により internal 型へ直接アクセスする。

### `SaveDataQueryTests`

`new SaveDataEntryRegistry()` を直接生成し、`GetOrCreate` / `MarkLoaded` で状態を作ってから `new SaveDataQuery(registry)` を検証する。

- 型を逆順で登録し、`GetInfos()` と `GetDtos()` が `FullName` の ordinal 昇順になること
- `MarkLoaded` した型だけ `IsLoaded` が true になること
- 取得後に `registry.GetOrCreate` で型を増やすと、次の取得で件数が増えること（キャッシュしていないこと）
- 返却一覧が変更不能であること

### `SaveDataViewModelTests`

`SaveDataService` は `Func<SaveDataLoader>` を遅延評価するため、ローダーに触れない操作（`Reset`）はローダー無しで検証できる。ロード・保存の通知は `SaveDataLoader` を継承したテスト用ローダー（`public abstract class` なのでテストアセンブリから継承できる）を注入して検証する。

- constructor 直後の初期値が Query の一覧と一致すること
- `service.LoadAsync` 完了で `Entries` が更新されること
- `service.SaveAsync` で `SaveDate` の変化が通知されること（version が変わらない経路の回帰テスト）
- 内容が変わらない再取得では通知されないこと（購読回数で確認）
- `Dispose` 後は Service の変更を反映せず、多重 `Dispose` が無害であること
- 購読者が例外を投げても `SaveAsync` が失敗しないこと

### `SaveDataEntryRegistryTests`（既存の更新）

- `Registry_GetEntrySnapshot_IsCachedUntilChanged` を削除する（キャッシュを廃止するため）
- `GetEntities()` が複製を返すこと、`Version` が新規作成・読み込み状態変化・全消去でだけ増えることを追加する

既存の EditMode / PlayMode テストも全件実行する。

## 動作確認手順

1. Unity Scene 検証ガードに従い、親と submodule の dirty 状態を記録する
2. `uloop-clear-console` 後に `uloop-compile` を実行し、Error 0、意図しない Warning 0 を確認する
3. EditMode と PlayMode の全テストを実行する
4. **Edit Mode で** Symphony Administrator を開き、Save Data パネルに対応型が表示され、Load / Save / Delete が従来どおり動作することを確認する
5. Save Data Sample を Play し、保存・再ロード・削除で一覧の State と Date が更新されることを確認する
6. `SymphonyAdministrator.Update()` が Save Data を polling していないこと（コード上で 1 行削除されていること）を確認する
7. `SymphonyMcpTools.GetSaveDataJson()` が `loaded` を正しく返すことを確認する
8. Play Mode の開始・終了を 2 回繰り返し、2 回目に前回の ViewModel 購読が残らないことを確認する
9. Console の Error / Exception が 0 件であることを確認する
10. Play Mode 停止後に package の `.unity` / `.prefab` 差分が無いことを確認する
11. `rg -n "LoadedTypes|GetEntrySnapshot" Runtime Editor -g '*.cs'` が 0 件であることを確認する
12. `rg -n "UnityEditor|EditorPrefs" Runtime Core -g '*.cs'` で新規違反が 0 件であることを確認する

## バージョン判断

**マイナー（2.17.0）。** 後方互換な `SaveDataRegistryEntryInfo.IsLoaded` を追加するため。`GetEntries()` の並び順を型名昇順として定めるのは、従来が未定義順だったための明確化であり、シグネチャと例外条件は変わらない。internal の読み取り経路と Editor の更新方式の置換は利用側へ影響しない。

## このRoundで触るバージョン関連ファイル

| ファイル | 変更 |
| --- | --- |
| `Assets/SymphonyFrameWork/package.json` | `2.16.0` → `2.17.0` |
| `Assets/SymphonyFrameWork/CHANGELOG.md` | 2.17.0 へ Query / Dto / ViewModel / Editor / MCP の変更とテストを記録 |
| `Assets/SymphonyFrameWork/README.md` | 現在のバージョン |
| `Assets/SymphonyFrameWork/Documentation~/Architecture.md` | Query / Dto / ViewModel の関係を追加 |
| `Assets/SymphonyFrameWork/Documentation~/AgentUsage.md` | `IsLoaded` を含む一覧照会を追加 |

## ブランチ

`develop` から `feature/save-data-query-viewmodel` を作成する。
