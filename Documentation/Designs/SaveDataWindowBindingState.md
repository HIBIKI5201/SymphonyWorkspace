# SaveDataWindowBindingState

GitHub Issue: [#166 セーブデータウィンドウのDirtyフラグ](https://github.com/HIBIKI5201/SymphonyFramework/issues/166)

ブランチ: `feature/166-savedata-window-binding-state`（submodule 側、`develop` から作成済み）

## 目的

Symphony Administrator の Save Data パネルには、いま3つの欠落がある。

1. **選択中の型がランタイムでロードされても、ウィンドウが古い表示のまま残る。** `SaveDataWindow.ApplyEntries` は行一覧を作り直すだけで、Inspector のバインド先（`SaveDataDebugState._data`）を評価し直さない。バインドは `SelectType` と各ボタン操作の中でしか起きないため、「型を選んだまま Play Mode へ入り、ゲームがその型をロードした」場合に、パネルは未ロードのままの見た目を出し続ける。
2. **未ロードの型では何も編集できない。** `BindCurrentSelection` は `SaveStore.IsLoaded` が false のとき `RebindDebugState(null)` で参照を切り、HelpBox だけを出す。さらに `BuildRows` は、キャッシュ済みでも永続化済みでもない型を行に含めないため、そういう型は一覧にすら出ない。
3. **いま Inspector に映っているデータが「ゲームが実際に使っているインスタンス」なのか「ウィンドウが用意したインスタンス」なのかを、画面から区別できない。** 前者への編集はランタイムへ即座に効き、後者への編集は誰にも効かない。効き方が正反対なのに見分けが付かない。Issue が「Dirtyフラグ」と呼んでいるのはこの区別のことである。

本 Round は、Inspector のバインド元を3状態として明示し、色ランプで区別し、状態が変わったら自動で追従させる。あわせて、**非接続で編集した内容を Play Mode へ持ち越す経路**を用意する。

### 状態の定義

**インスタンスの所有者で分ける。** Play Mode かどうかでは分けない。

| 状態 | enum | ランプ色 | 成立条件 | Inspector が指すもの |
| --- | --- | --- | --- | --- |
| 接続中 | `Registry` | 緑 | `SaveDataViewStore` が「ロード済み」と答える型 | レジストリ正本 |
| ロード済み | `Loaded` | 黄 | 上記が不成立で、ウィンドウ専用インスタンスへ永続化データをロード済み | ウィンドウ専用インスタンス |
| 新規 | `Instance` | 赤 | 上記が不成立。ロード中もここに含む | ウィンドウ専用インスタンス |
| 未選択 | `None` | 消灯 | 型が未選択、または対応型が0件 | なし |

`Registry` が他より優先する。ランタイムがロードした瞬間に、ウィンドウ専用インスタンスの有無に関係なく接続中へ移る。

**ロード中は `Instance`（赤）のまま**で、ランプの隣に進行表示を出す。ロードが完了して初めて `Loaded`（黄）へ移る。

### この分け方を選ぶ根拠

**Edit Mode でレジストリへロードしても、その結果は Play Mode へ持ち越されない。** `SymphonyOrchestrator.GameBeforeSceneLoaded` が `SaveDataInitializer.Initialize` を呼び、`SaveStore.ConfigureLoaderResolver` が `SaveDataEntryRegistry` ごと作り直すためである（`SaveStore.cs:202-220`、`SymphonyOrchestrator.cs:48`）。したがって Edit Mode でレジストリを触る操作には持続的な価値が無く、**非接続時の Load／Save／Delete はレジストリを一切経由しない**方が、目的にも安全側にも合う。持ち越しは、後述する保存先へのフラッシュで行う。

## 公開API

追加は次の1件だけ。**後方互換な追加であり、既存シグネチャの変更・削除は無い。**

```csharp
// SymphonyFrameWork.Editor.SymphonyUserSettingConfig（既存の public ScriptableSingleton）
/// <summary> 非接続で編集したセーブデータを、Play Mode 突入時に保存先へ書き出すか。 </summary>
public bool IsSaveDataPlayModeCarryOverEnabled { get; set; }
```

既存の `IsServiceLocatorSetInstanceLogEnabled` などと同じ形（`private` フィールド + setter で `Save()`）。保存先は `UserSettings/SymphonyFrameWork/SymphonyUserSettingConfig.asset`。`DesignPhilosophy.md:500` の「個人設定は `UserSettings/SymphonyFrameWork/`、`EditorPrefs` は使用しない」に従う。既定値は `false`（明示的に有効化しない限り、Play Mode 突入が保存先へ書き込むことはない）。

それ以外の追加はすべて `internal` で、`InternalsVisibleTo` により `SymphonyFrameWork.Editor` と `SymphonyFrameWork.Tests.Editor` にだけ届く（`Runtime/AssemblyInfo.cs:4,8`、`Editor/AssemblyInfo.cs:7` で確認済み）。

### View専用エントリポイントを操作の窓口にする

**`SaveDataWindow` から `SaveStore` の操作APIを直接呼ぶのをやめ、View層に新設する `SaveDataViewStore` を経由させる。**

`SaveDataViewModel` へ命令を持たせる案は採らない。`DesignPhilosophy.md` の `### ViewModel` が「Commandを実行しない」と定めており、`## やってはいけないこと` にも「ViewModelからCommandを実行する」が挙がっているためである。同文書へ `### View専用エントリポイント` を新設し、この形を正式な選択肢として明文化した上で採用する。

```csharp
// SymphonyFrameWork.System.SaveSystem.SaveDataViewStore（internal、Runtime/System/SaveSystem/Internal/View/）
internal sealed class SaveDataViewStore
{
    internal SaveDataViewStore(SaveDataQuery query, SaveDataService service);

    // 問い合わせ
    internal string CurrentLoaderName { get; }
    internal bool IsLoaded(Type dataType);
    internal bool Exists(Type dataType);
    internal SaveDataContent GetLoadedContent(Type dataType);

    // 命令（レジストリ経由）
    internal Task LoadAsync(Type dataType, CancellationToken token = default);
    internal Task SaveAsync(Type dataType, CancellationToken token = default);
    internal Task DeleteAsync(Type dataType, CancellationToken token = default);

    // 命令（レジストリを経由しない）
    internal Task LoadDetachedAsync(Type dataType, SaveDataContent target, CancellationToken token = default);
    internal Task SaveDetachedAsync(Type dataType, SaveDataContent source, CancellationToken token = default);
    internal Task DeleteDetachedAsync(Type dataType, CancellationToken token = default);
}
```

- **`SaveDataViewModel` は変更しない。** 表示状態（`Entries`）の購読だけを担い続ける。ウィンドウは「表示は ViewModel、操作と問い合わせは `SaveDataViewStore`」の2経路になる。
- **表示状態と `ReactiveProperty` を `SaveDataViewStore` へ持たせない。** 保持するのは `SaveDataQuery` と `SaveDataService` への参照だけで、すべて委譲する。
- `GetLoadedContent` は**未ロードなら例外ではなく `null` を返す**。`SaveStore.Get` は `InvalidOperationException` を投げるため、ウィンドウ側が `IsLoaded` と `Get` を2回に分けて呼ぶ必要があり、その間に状態が変わりうる。1回の問い合わせで済ませる。
- `CurrentLoaderName` は `_service.GetCurrentLoader().GetType().Name`。ウィンドウから `SaveStore.GetCurrentLoader()` を消すため。
- 命令はすべて `Task` を返す。ウィンドウ側で `Task.IsCompleted` を同期的に見る必要がある箇所（後述のフラッシュ）があるため、`Awaitable` へは変換しない。
- **Editor 向けの公開APIは今回作らない。** `SaveDataViewStore` は `InternalsVisibleTo` で Editor から届き、現時点で利用側の要求も無い。必要になった時点で Editor アセンブリ側に公開APIを設け、そこから委譲する（`DesignPhilosophy.md` の `### View専用エントリポイント` に規定済み）。

### Composition と公開

`SaveStore.ConfigureLoaderResolver` が、既存の `SaveDataQuery` / `SaveDataViewModel` と同じ寿命で `SaveDataViewStore` を生成・所有する。

```csharp
// SaveStore（internal メンバーの追加）
internal static SaveDataViewStore CurrentViewStore { get; }   // 未初期化ならnull
```

- 既存の `CurrentViewModel` / `OnCurrentViewModelChanged` はそのまま。`OnCurrentViewModelChanged` は ViewModel と ViewStore の両方が差し替わったことを示す通知として使う（発行位置は現状のまま、両方を作り終えた後）。
- `SaveStore` の**操作API（`LoadAsync` / `SaveAsync` / `DeleteAsync` など）は変更しない。** 追加するのは読み取り専用accessorの1つだけで、Composition が所有物を公開する既存の形（`CurrentViewModel`）に揃える。
- ウィンドウは `SaveDataViewStore` を長期保持せず、操作のたびに `SaveStore.CurrentViewStore` を読む（現在 `CurrentViewModel` に対して行っているのと同じ扱い）。`null` なら未初期化として扱う。

### Service へ追加する internal API

レジストリを経由しない I/O。**エラー文脈の付与（`SaveDataOperationException` への変換）とローダー解決を上位層へ複製しない**ために Service へ載せる。

```csharp
// SymphonyFrameWork.System.SaveSystem.SaveDataService
internal Task LoadDetachedAsync(Type dataType, SaveDataContent target, CancellationToken token = default);
internal Task SaveDetachedAsync(Type dataType, SaveDataContent source, CancellationToken token = default);
internal Task DeleteDetachedAsync(Type dataType, CancellationToken token = default);
```

3つとも `GetLoader()` でローダーを固定し、既存の `ExecuteLoaderOperationAsync` を通して例外へ操作文脈を付ける。**`_registry` を読み書きせず、`OnStateChanged` も発行しない。** これがレジストリ経由の `LoadAsync` / `SaveAsync` / `DeleteAsync` との唯一の違いである。

### Editor へ追加する internal 型

```csharp
// SymphonyFrameWork.Editor
internal enum SaveDataBindingSourceEnum { None, Instance, Loaded, Registry }

internal static class SaveDataBindingState
{
    internal static SaveDataBindingSourceEnum Resolve(
        bool hasSelection, bool isRegistryLoaded, bool isLocalContentLoaded);
    internal static string GetLampUssClassName(SaveDataBindingSourceEnum source);
    internal static string GetDisplayText(SaveDataBindingSourceEnum source);
    internal static string BuildStatusSuffix(bool isLoading, bool isDirty);
    internal static IReadOnlyList<string> AllLampUssClassNames { get; }
}
```

`Resolve` と `BuildStatusSuffix` は Unity API へ触れない純粋関数として切り出す。**Editor ウィンドウの GUI 操作は自動検証できない**（`.agents/skills/implement/references/design-doc.md` の記載どおり、`EditorWindow.SendEvent`・`uloop simulate-mouse-*`・`execute-dynamic-code` のいずれでも叩けない）ため、判定規則とクラス名の対応だけを EditMode テストで押さえる。`AllLampUssClassNames` は、状態を切り替えるときに前の修飾クラスを取り外すために使う。

enum のサフィックスは `CodeGuidelines.md:146,182` に従う。

## ファイル構成

設計書だけワークスペース側。それ以外は submodule（`Assets/SymphonyFrameWork/`）配下。

| パス | 区分 | 変更内容 |
| --- | --- | --- |
| `Runtime/System/SaveSystem/Internal/Application/SaveDataService.cs` | 変更 | detached 3メソッドを追加 |
| `Runtime/System/SaveSystem/Internal/View/SaveDataViewStore.cs` | **新規** | View専用エントリポイント |
| `Runtime/System/SaveSystem/SaveStore.cs` | 変更 | `CurrentViewStore` の生成・所有・公開 |
| `Editor/Configs/ConfigData/SymphonyUserSettingConfig.cs` | 変更 | 持ち越しフラグを追加 |
| `Editor/Administrator/UITK/CS/SaveDataBindingSourceEnum.cs` | **新規** | バインド元の列挙 |
| `Editor/Administrator/UITK/CS/SaveDataBindingState.cs` | **新規** | 判定と表示値の対応 |
| `Editor/Administrator/UITK/CS/SaveDataWindow.cs` | 変更 | 再バインド、専用インスタンス、自動ロード、色ランプ、持ち越し |
| `Editor/Administrator/UITK/UXML/SaveDataWindow.uxml` | 変更 | ランプ・状態ラベル・チェックボックスの追加、一覧見出しの改名 |
| `Editor/Administrator/UITK/SymphonyWIndow.uss` | 変更 | ランプと状態別クラスの追加 |
| `Tests/Editor/SaveDataBindingStateTests.cs` | **新規** | 判定規則の検証 |
| `Tests/Editor/SaveDataServiceDetachedTests.cs` | **新規** | detached I/O がレジストリを触らないことの検証 |
| `Tests/Editor/SaveDataViewStoreTests.cs` | **新規** | View専用エントリポイントの委譲の検証 |
| `Documentation~/Modules/SaveDataSystem.md` | 変更 | 「Save Data パネル」節と内部構造図を書き直す |
| `Documentation~/EditorTools.md` | 変更 | 一覧の `Save Data` 行を更新 |
| `Documentation~/Html/` | **自動生成** | `python scripts/build_module_docs.py` の出力を同じコミットへ含める |
| `CHANGELOG.md` / `package.json` | 変更 | 3.10.0 |
| `Documentation/Designs/SaveDataWindowBindingState.md` | **新規**（ワークスペース側） | この文書 |

新規 `.cs` は6件。`.meta` は Unity Editor にフォーカスを当てて生成させ、`.cs` と対で揃っていることをコミット前に確認する。

### 他の4パネルは変更しない

`SceneLoadWindow` / `ServiceLocateWindow` / `PauseWindow` / `AutoEnumGeneratorWindow` には**追随すべき違反が無い。** 一度は「旧規約のまま残る」として `// TODO:` を入れたが、実際の呼び出しを数えたところ根拠が無く、全件を取り消した。

| パネル | Facade 呼び出しの実体 | 判定 |
| --- | --- | --- |
| `SceneLoadWindow:135,141` | `IsInitialized` と `CurrentViewModel` のみ。**命令を1つも出していない** | 対象なし |
| `ServiceLocateWindow:138,144` | 同上 | 対象なし |
| `PauseWindow:197` | `PauseManager.Pause = isPaused`（命令） | 新規約でも合法 |
| `AutoEnumGeneratorWindow:48-60` | `AutoEnumGenerator.*EnumGenerate()`。Editor専用の生成器で ViewModel も Query も Service も持たない | 概念が適用できない |

**今回の改訂は規約の置き換えではなく緩和である。** 「操作は公開エントリポイント**か**View専用エントリポイントを呼ぶ」としたため、Facade を直接呼ぶ既存パネルは今も規約に適合している。View専用エントリポイントが要るのは、`SaveDataWindow` のように**公開APIへ広げたくない操作（detached I/O）を View が必要とする場合**だけである。

一方、ワークスペース側の `.agents/skills/audit/references/perspectives.md` へは**コード内 `TODO` の検出**を監査観点 B9 として追加する。TODO は書いた時点では追随の意思表示だが、検出手段が無ければ棚卸しされずに残り続ける。今回のように**根拠を失った TODO を検出して消すこと**も、この観点の役割に含める。

## 依存方向

`SymphonyFrameWork.Editor` → `SymphonyFrameWork`（Runtime）→ `SymphonyFrameWork.Core` の向きは変えない。

- `SaveDataViewStore` は Runtime の `Internal/View/` に閉じ、`UnityEditor` へ触れない。読み取りは `SaveDataQuery`、状態変更は `SaveDataService` へ委譲するだけである。
- `SaveDataViewModel` は変更しない。表示状態の保持と `ReactiveProperty` の公開だけを担い続ける。
- `SaveDataBindingState.Resolve` は `bool` 3つを受ける純粋関数で、`SaveStore` にも `UnityEditor` にも依存しない。状態の問い合わせは呼び出し側（`SaveDataWindow`）が `SaveDataViewStore` へ行う。
- 追加する Editor 型は `SymphonyFrameWork.Editor` に閉じる。Runtime も Core も Editor を参照しない。
- **Play Mode 突入の検知（`EditorApplication.playModeStateChanged`）は Editor 側にとどめる。** Runtime へ Editor ライフサイクルの知識を持ち込まない。

## 振る舞いの詳細

### 1. ランタイムのロードへ追従する

`ApplyEntries` の末尾（`RefreshView()` の前）で、バインド元を評価し直す。

**通知が届くことは経路を追って確認済み:**

- ランタイムが `SaveStore.LoadAsync` を呼ぶ → `SaveDataService.LoadInternalAsync` の `finally` が必ず `RaiseStateChanged()` を実行（`SaveDataService.cs:243-247`）→ `SaveDataViewModel.StateChangedHandler` が `_entries.SetValue(_query.GetDtos())` → `SaveDataDto.IsLoaded` が false→true で変わるため `SaveDataDtoListComparer` が差分ありと判定 → `ApplyEntries` が走る。
- Play Mode 突入 → `ConfigureLoaderResolver` が `OnCurrentViewModelChanged` を発行 → `ViewModelChangedHandler` → `BindViewModel` → `Entries.Subscribe` は `notifyCurrent: true` が既定（`ReactiveProperty.cs:96,110`）なので、購読と同時に現在値で `ApplyEntries` が走る。
- Play Mode 終了 → `SymphonyOrchestrator.Shutdown` → `SaveStore.ResetRuntimeState` → `_service.Reset()` → `_registry.Clear()` で版番号が進むため `RaiseStateChanged`（`SaveDataService.cs:204-207`）。接続中から非接続へも追従する。

**再バインドの条件:** 解決したバインド元が現在と違うとき、または `Registry` のまま正本のインスタンス参照が変わったときだけ `RebindDebugState` する。毎回の通知で無条件に作り直すと、編集途中の Inspector が更新のたびに巻き戻る。

### 2. ウィンドウ専用インスタンスと自動ロード

`SaveDataWindow` が、**選択中の型ひとつ分だけ**保持する。保持するのは、対象型・`SaveDataContent` の実体・ロード済みフラグ・編集済み（Dirty）フラグ・進行中のロード要求ID。

型を選択して非接続だと分かった時点で、次の順で進む。

1. `Activator.CreateInstance(型)` で専用インスタンスを生成し、Inspector へバインドする。**赤**。
2. `viewStore.Exists(型)` が true なら、`viewStore.LoadDetachedAsync(型, 専用インスタンス)` を開始する。**赤のまま**、ランプの隣に `Loading…` を出す。
3. 完了したら**黄**へ移り、進行表示を消す。false だった場合（保存データが無い）は 2 を行わず、赤のままにする。

- **ロード中は Inspector を `EditorGUI.DisabledScope` で編集不可にする。** ロード完了時の上書きで編集内容が黙って消えるのを防ぐ。
- **古い完了を捨てる。** ロード開始時に採番した要求IDと選択型を completion 時に照合し、選択が変わっていたら結果を捨てる。破棄済みウィンドウ（`_disposed`）でも同様に捨てる。
- 失敗した場合は赤のまま、ステータスに例外メッセージを出す。`ExecuteActionAsync` と同じ扱いで `Debug.LogException` も残す。
- 破棄: 選択型が変わったとき、および `SaveDataWindow.Dispose` のとき。`SaveDataContent` は `IDisposable` なので必ず `Dispose()` を呼ぶ。
- **接続中（緑）へ移っても破棄しない。** Play Mode を抜けて非接続へ戻ったとき、同じ内容が戻る。

### 3. Dirty フラグと Play Mode への持ち越し

**Dirty の立ち方:** `DrawEditorInspector` の `_debugSerializedObject.ApplyModifiedProperties()` が `true` を返し、かつバインド元が `Loaded` または `Instance` のとき、専用インスタンスを編集済みにする。接続中（緑）の編集はレジストリ正本へ直接入るため Dirty にしない。

**持ち越し:** `EditorApplication.playModeStateChanged` の `ExitingEditMode` で、次がすべて成立するときだけ保存先へ書き出す。

- `SymphonyUserSettingConfig.instance.IsSaveDataPlayModeCarryOverEnabled` が true
- 専用インスタンスが Dirty
- バインド元が `Loaded` または `Instance`（接続中なら持ち越す必要が無い）

`viewStore.SaveDetachedAsync(型, 専用インスタンス)` を呼び、返った `Task` を見る。

- `IsCompleted` なら、そのまま Play Mode へ進む。Dirty を下ろす。`IsFaulted` なら `Debug.LogException` してステータスへ出し、Play Mode 自体は止めない。
- **完了していなければ Play Mode 突入を取り消す。** `EditorApplication.isPlaying = false` で戻し、書き込み完了後に `EditorApplication.EnterPlaymode()` で入り直す。理由を `Debug.Log` に残す。

**同梱ローダーではこの分岐に入らない。** `PlayerPrefsSaveDataLoaderStrategy` は `PlayerPrefs.SetString` + `Save()` を同期実行して `SymphonyAwaitable.Completed()` を返す（`PlayerPrefsSaveDataLoaderStrategy.cs:37-43`）ため、`SaveDetachedAsync` は一度も待機せずに完了済み `Task` を返す。取り消しと入り直しは、**利用側が本当に非同期なローダーを実装している場合の保険**である。この経路は同梱構成では踏めないため、動作確認手順に「入り込めない経路である」ことを明記する。

書き出した後、ゲームが `SaveStore.LoadAsync<T>()` を呼べば、その内容が読まれる。Play Mode 突入で `ConfigureLoaderResolver` がレジストリを作り直すため、保存先を経由するこの経路だけが持ち越しとして成立する。

**チェックボックス:** パネル上に `Play Mode へ持ち越す` を置く。`SymphonyUserSettingConfig` を読み書きし、Editor を再起動しても値が残る。既定は off。

### 4. ボタンの意味

| ボタン | 接続中（緑） | 非接続（黄・赤） |
| --- | --- | --- |
| Load | `viewStore.LoadAsync`。レジストリ正本を保存先から読み直す。緑のまま | `viewStore.LoadDetachedAsync` で専用インスタンスへ読み込む。**黄へ移る**。Dirty を下ろす |
| Save | 編集内容を正本へ同期してから `viewStore.SaveAsync` | `viewStore.SaveDetachedAsync` で専用インスタンスを保存先へ書く。**レジストリへは載せない**。状態は変わらず、Dirty が下りる |
| Delete | 確認ダイアログの後 `viewStore.DeleteAsync`。正本を既定値へ戻す | 確認ダイアログの後 `viewStore.DeleteDetachedAsync`。専用インスタンスを `Dispose` して作り直す。**赤へ移る** |

非接続の Save / Delete は `OnStateChanged` を発行しないため、`RefreshView()` をウィンドウ側で明示的に呼ぶ。行の `IsSaved` は `BuildRows` が `viewStore.Exists` を引き直すので追従する。

### 5. 一覧にすべての対応型を出す

`BuildRows` は現在、キャッシュ済みでも永続化済みでもない型を行に含めない（`SaveDataWindow.cs:649`）。この状態では赤に到達できる型が一覧に出ないため、**`_saveDataTypes` の全件を行にする**。行の `State` 表示（`Loaded` / `Saved` / `Empty`）は既存のままで足りる。

一覧を出すだけではレジストリ生成も走らない。**自動選択（`ResolveAutoSelectType`）は変更しない。** キャッシュ済みエントリが1件も無ければ自動選択せず、利用者の明示選択を待つ。パネルを開いただけで保存先へ I/O が走らない（＝勝手に自動ロードが始まらない）ようにするためである。

一覧の見出しは実態に合わせて `Registry Cache` → `Save Data Types` へ変える。同じ文字列を出しているステータス文言も合わせて直す。

### 6. 表示

`save-loaded-entries` の下に、横並びの1行を置く。

```text
[■] Connected to runtime instance    Loading…  /  未保存の変更あり
[ ] Play Mode へ持ち越す
```

- **色ランプ** `save-binding-lamp`: 12×12 の `VisualElement`。状態別の修飾クラスで背景色を切り替える。`None` では消灯色。
- **状態ラベル** `save-binding-state`: `SaveDataBindingState.GetDisplayText` の戻り値。
- **進行・Dirty 表示** `save-binding-suffix`: `BuildStatusSuffix(isLoading, isDirty)` の戻り値。**ランプの隣**に出す。両方成立することはない（ロード中は編集不可のため）。
- **チェックボックス** `save-carry-over`: `Toggle`。ラベルは `Play Mode へ持ち越す`。
- パネル外枠（`class="base"`）にも同じ状態の修飾クラスを付け、枠線色を変える。`SymphonyWIndow.uss` は全パネル共通のため、クラス名は `save-binding--registry` / `--loaded` / `--instance` と接頭辞を付けて衝突を避ける。修飾ルールは `.base` より**後ろ**に置く（どちらも単一クラスセレクタで詳細度が同じため、記述順で決まる）。
- 色: 緑 `rgb(80, 170, 100)` / 黄 `rgb(205, 175, 60)` / 赤 `rgb(200, 85, 85)` / 消灯 `rgb(90, 90, 90)`。

## エラー処理

- **通常起こり得る失敗**（保存先が無い、JSON が壊れている）は既存の経路のまま。`SaveDataLoaderStrategy.LoadAsync` が既定値へ戻して警告ログを出す。detached 経路も同じ基底実装を通るため挙動は揃う。
- **I/O 失敗** は `SaveDataOperationException`（操作種別・データ型・ローダー型付き）へ変換する。detached 3メソッドも既存の `ExecuteLoaderOperationAsync` を通すので、レジストリ経由と同じ例外になる。
- **キャンセル** は `OperationCanceledException` のまま伝播させる。既存の `catch (OperationCanceledException) { throw; }` の並びを崩さない。
- **不変条件違反** は例外。`dataType` が `SaveDataContent` の具象型でなければ `ArgumentException`、`target` / `source` が `null` なら `ArgumentNullException`。
- **`SaveDataViewStore` の問い合わせは例外を返さない設計にする。** `GetLoadedContent` は未ロードで `null`、`IsLoaded` は false。`Exists` と `CurrentLoaderName` はローダーへ触れるため例外がありうるので、既存の変換規則に従う。
- **UI 境界** は既存の `ExecuteActionAsync` が捕捉し、`Debug.LogException` とステータス表示の両方へ出す。自動ロードと Play Mode フラッシュも同じ変換を通す。フラッシュだけは `playModeStateChanged` の同期コールバック内なので `async void` にせず、`Task` の状態を見て分岐する。
- **`SaveStore` 未初期化**（`CurrentViewModel == null`）のとき、ウィンドウは非接続として扱い、専用インスタンスの生成までは行うが I/O は行わない。`Exists` を呼べないため自動ロードも走らない。

## 影響範囲

- **公開APIは1プロパティの追加のみ。** シリアライズ形式（セーブデータの JSON）への影響は無い。`SymphonyUserSettingConfig.asset` にフィールドが1つ増えるが、既存アセットは既定値 `false` で読み込まれる。
- **View層に `SaveDataViewStore` が加わる。** `internal` かつ利用側からは見えないため、外部影響は無い。`SaveDataViewModel` は変更しないので、表示側の契約も変わらない。`Documentation~/Modules/SaveDataSystem.md` の内部構造図へ、`Window` から `SaveDataViewStore` を経て `Service` / `Query` へ向かう操作経路を書き足す。
- **`Documentation/DesignPhilosophy.md` を同じ Round で改訂する。** `### View専用エントリポイント` の新設、`### ViewModel` の「操作は公開エントリポイントを直接呼ぶ」の緩和、`## やってはいけないこと` とチェックリストへの追記。ワークスペース側の変更なので、submodule とは別コミットにする。
- **Save Data パネルの見た目と操作が変わる。** 一覧に全対応型が出る、ランプと状態ラベルが付く、型を選ぶと保存データを自動でロードする、非接続でも編集・保存・削除ができる。
- **型を選択すると保存先への読み取り I/O が走るようになる。** これまでパネル操作で I/O が走るのは明示的なボタン操作だけだった。`Exists` は既に `BuildRows` が全型分呼んでいるため、読み取り回数の桁は変わらない。
- **Play Mode 突入が保存先へ書き込む場合がある。** 持ち越しチェックが on で、かつ非接続の編集が未保存のときに限る。既定 off。
- **非接続時の Save が保存先を上書きできるようになる。** 以前は未ロードの型を保存する操作自体が到達不能だった。**Save には確認ダイアログを置かない**。Inspector で編集した内容を明示的に書き出す操作であり、Delete（不可逆かつ既存データを消す）とは性質が違う。
- **既存テストへの影響は無い見込み。** `SymphonyAdministratorUxmlTests` は `container.Q<SaveDataWindow>()` が非 null であることだけを見る。`AdministratorUxmlStyleSrcTests` は `<Style src>` のパスと GUID の一致を見るもので、今回 `src` は変えない。どちらも実装後に再実行して確認する。

## テストの置き場と種別

`Assets/SymphonyFrameWork/Tests/Editor/`（EditMode）。既存の命名（英語の `対象_条件_期待`）に揃える。

### `SaveDataBindingStateTests.cs`（新規）

`SaveDataBindingState` は `bool` を受ける純粋な静的クラスなので、Unity API も `SaveStore` も要らず、そのまま呼んで戻り値を `Assert.That` で比較する。

| テスト | 検証内容 |
| --- | --- |
| `Resolve_NoSelection_ReturnsNone` | `hasSelection: false` なら他の引数に関係なく `None` |
| `Resolve_RegistryLoaded_ReturnsRegistry` | レジストリがロード済みなら `Registry` |
| `Resolve_LocalContentLoaded_ReturnsLoaded` | 非接続で、専用インスタンスがロード済みなら `Loaded` |
| `Resolve_LocalContentNotLoaded_ReturnsInstance` | 非接続で、ロードしていなければ `Instance` |
| `Resolve_RegistryLoaded_TakesPrecedenceOverLocalContent` | 両方 true でも `Registry` |
| `GetLampUssClassName_EverySource_ReturnsDistinctName` | 全 enum 値を `Enum.GetValues` で回し、互いに異なる文字列を返す |
| `AllLampUssClassNames_ContainsEveryClassName` | 全 enum 値の `GetLampUssClassName` が `AllLampUssClassNames` に含まれる（付け替え時の取り外し漏れを防ぐ） |
| `GetDisplayText_EverySource_ReturnsNonEmpty` | 全 enum 値が空でない文言を返す |
| `BuildStatusSuffix_Loading_ReportsLoading` | ロード中の文言を返す |
| `BuildStatusSuffix_Dirty_ReportsUnsavedChange` | 未保存の変更を示す文言を返す |
| `BuildStatusSuffix_Idle_ReturnsEmpty` | どちらでもなければ空文字列 |

### `SaveDataServiceDetachedTests.cs`（新規）

既存の `SaveDataViewModelTests` と同じ形で、`Dictionary` を保存先にした `MemorySaveDataLoader`（`SaveDataLoaderStrategy` 派生）を用意し、`new SaveDataService(new SaveDataEntryRegistry(), () => loader)` を直接組む。グローバルな `SaveStore` は触らない。**準備段階でもレジストリを直接操作せず、`SaveAsync` など Service の経路で保存済み状態を作る。**

| テスト | 検証内容 | 書き方 |
| --- | --- | --- |
| `LoadDetachedAsync_DoesNotRegisterEntry` | detached ロードはレジストリへエントリを作らない | 準備直後の `registry.GetEntities().Count` を記録し、操作後に**同じ値**であることを確認する（`Is.Zero` のような絶対値では書かない） |
| `LoadDetachedAsync_RestoresPersistedValues` | 保存済み JSON が渡したインスタンスへ入る | 別インスタンスを `SaveAsync` で保存 → `Reset()` → 新しいインスタンスへ `LoadDetachedAsync` → フィールド値を比較 |
| `LoadDetachedAsync_NoPersistedData_ResetsToDefault` | 保存値が無ければ既定値へ戻る | 基底が `Debug.Log` を出すため `LogAssert.Expect(LogType.Log, new Regex(...))` を併記する |
| `LoadDetachedAsync_DoesNotRaiseStateChanged` | `OnStateChanged` を発行しない | 準備完了後に購読して発火回数を数え、操作後も**準備直後からの増分が0**であることを確認する |
| `SaveDetachedAsync_PersistsWithoutRegistering` | 保存はされるがレジストリには載らない | 操作後に `Exists` が true、エントリ件数は準備直後と同じ |
| `SaveDetachedAsync_UpdatesSaveDate` | `SaveDate` が入る | 保存前に `null` を確認してから保存し、非 null になることを見る |
| `SaveDetachedAsync_CompletesSynchronously` | 同期完了するローダーでは待機を挟まない | 返った `Task` が `IsCompleted` であること。**Play Mode 突入時のフラッシュが同梱構成で成立する前提そのもの** |
| `DeleteDetachedAsync_RemovesPersistedDataOnly` | 保存先から消えるがレジストリは変わらない | `Exists` が false、エントリ件数は準備直後と同じ |
| `SaveDetachedAsync_NullSource_Throws` | `ArgumentNullException` | |
| `LoadDetachedAsync_MismatchedType_Throws` | `dataType` と実体が食い違えば `ArgumentException`（基底 `Validate` の契約） | |

### `SaveDataViewStoreTests.cs`（新規）

既存の `SaveDataViewModelTests` の `MemorySaveDataLoader` と同じ組み立てを使い、`new SaveDataViewStore(query, service)` を直接組む。`SaveDataViewModel` も同じ `service` から作り、通知の有無を観測する。

| テスト | 検証内容 |
| --- | --- |
| `Constructor_NullQuery_Throws` | `ArgumentNullException` |
| `Constructor_NullService_Throws` | `ArgumentNullException` |
| `IsLoaded_BeforeLoad_ReturnsFalse` | 未ロードで false |
| `GetLoadedContent_NotLoaded_ReturnsNull` | **例外を投げず** `null` を返す（`SaveStore.Get` との差） |
| `GetLoadedContent_AfterLoad_ReturnsRegistryInstance` | `LoadAsync` 後、レジストリ正本と同一参照（`Assert.That(..., Is.SameAs(...))`） |
| `Exists_AfterSave_ReturnsTrue` | 保存後に true |
| `CurrentLoaderName_ReturnsLoaderTypeName` | 注入したローダーの型名を返す |
| `LoadAsync_RaisesViewModelUpdate` | レジストリ経由の命令は ViewModel の `Entries` を更新する（準備直後の通知回数からの増分で比較） |
| `LoadDetachedAsync_DoesNotRaiseViewModelUpdate` | detached 命令は `Entries` を更新しない（同じく増分で比較） |

`LogAssert.ignoreFailingMessages` は使わない。

### 自動で検証しないもの

**ウィンドウの GUI 操作、ランプの色、進行表示、チェックボックス、Play Mode 突入時のフラッシュは自動検証しない。** ボタンやトグルを押す手段が無く、色は描画結果でしか確認できず、`playModeStateChanged` は EditMode テストの中で起こせない。次節の手順で人が確認する。

## 動作確認手順

`uloop-clear-console` → `uloop-compile`（**エラー0・警告0**）→ `uloop-run-tests --test-mode EditMode` と `--test-mode PlayMode`（全数成功）を先に通す。その後、`Window > SymphonyFrameWork > Symphony Administrator` を開いたまま以下を行う。

**自動で確認する項目:** コンパイル、EditMode／PlayMode テスト、`.meta` の対応。

**検証に使うシーンと型:** `Assets/SymphonyFrameWork/Samples/Runtime/SaveDataSystemSample/SaveDataSystemSample.unity`。`SaveDataSystemSample_Controller.Start()` が `SaveStore.LoadAsync<SaveDataSystemSample_PlayerDataA>()` と `<...PlayerDataB>()` を呼ぶため、「ゲーム側が対象型をロードする」状況をそのまま作れる。`Samples~` ではなく `Samples/` 配下でコンパイル対象のため、`PlayerDataA` / `PlayerDataB` はパネルの型一覧にも出る。**ホスト側（`Assets/Scripts/`）へ検証用スクリプトを追加する必要は無い。**

**人が操作して確認する項目:**

| # | 操作 | 期待 |
| --- | --- | --- |
| 1 | Edit Mode でパネルを開き、保存データが無い型を選ぶ | 一覧に出る。**赤**のまま。`Loading…` は出ない（`Exists` が false のため I/O を始めない）。Inspector に既定値が出て編集できる |
| 2 | 値を変えて Save | 赤のまま。ランプの隣の `未保存の変更あり` が消える。行の `State` が `Saved` になる |
| 3 | 別の型へ切り替えて戻る | 一瞬**赤 + `Loading…`** を経て**黄**になり、2 で保存した値が出る。ロード中は Inspector が編集不可 |
| 4 | 黄のまま値を変える | ランプの隣に `未保存の変更あり` が出る。黄のまま |
| 5 | 持ち越しチェックを **off** のまま Play Mode に入り、ゲーム側で対象型を `SaveStore.LoadAsync` する | **緑**へ変わるが、4 の編集内容は入っていない（保存先へ書かれていない）。ウィンドウを開き直さずに緑になること |
| 6 | Play Mode を抜け、持ち越しチェックを **on** にして 4 をやり直し、Play Mode に入る | 突入時に保存先へ書かれ、`未保存の変更あり` が消える。ゲーム側の `SaveStore.LoadAsync` が 4 の編集内容を読む |
| 7 | 6 の状態から Play Mode を抜ける | ウィンドウを開き直さずに**黄**へ戻る。編集内容が残っている |
| 8 | 黄で Delete → 確認で Delete | **赤**へ戻る。行の `State` が `Empty` になる |
| 9 | Play Mode の開始・終了を**2回**繰り返す | Domain Reload 無効でも、ゴースト参照や重複購読による例外・二重更新が出ない。`playModeStateChanged` の購読が二重にならない |
| 10 | Symphony Administrator を閉じて開き直す | 例外が出ない。`Dispose` で専用インスタンスと `playModeStateChanged` の購読が解放されている |
| 11 | Editor を再起動して持ち越しチェックを見る | on のまま残っている（`UserSettings/SymphonyFrameWork/SymphonyUserSettingConfig.asset`） |

**この手順で踏めない経路:** 「Play Mode 突入時のフラッシュが同期完了しなかった場合の取り消しと入り直し」。同梱ローダーが同期完了するため到達しない。**コードレビューと `SaveDetachedAsync_CompletesSynchronously` テストで担保し、実機確認の対象から外す。**

`uloop-screenshot` で、赤・黄・緑それぞれのランプと状態ラベルを1枚ずつ残す。

## バージョン判断

**マイナー（3.9.6 → 3.10.0）。**

`SymphonyUserSettingConfig` へ `public` プロパティを1つ追加するため、`DesignPhilosophy.md:547` の「後方互換な公開API追加はマイナー」に該当する。既存の `public` / `protected` メンバー、シグネチャ、既定値の意味、セーブデータのシリアライズ形式はいずれも変えないので、メジャーではない。

Round は1つで完結する。差分は新規4ファイルを含めて17ファイル程度で、全部を読める規模に収まる。

## この Round で触るバージョン関連ファイル

| ファイル | 変更内容 |
| --- | --- |
| `Assets/SymphonyFrameWork/package.json` | `"version": "3.9.6"` → `"3.10.0"` |
| `Assets/SymphonyFrameWork/CHANGELOG.md` | `## [3.10.0] - 2026-08-14` を先頭へ追加。`### Add`（状態の色ランプ、自動ロード、非接続時の編集、Play Mode への持ち越し）と `### Fix`（ランタイムのロードへ追従しない不具合、対応型が一覧に出ない不具合） |

`README.md` の「現在のバージョン」と `AGENTS.md` のAPI早見表は、利用側から見た公開APIの入口が変わらないため触らない。実装時に `rg -n "README\.md|Documentation~/|EditorTools\.md|AgentUsage\.md" Documentation/ AGENTS.md` を実行し、今回の変更で古くなるワークスペース側の記述が無いことを再確認する。

---

## 実施レポート

実施日: 2026-08-14 / バージョン: 3.9.7（Fix）・3.10.0（Add） / PR: [#169](https://github.com/HIBIKI5201/SymphonyFramework/pull/169)（マージ済み） / Issue: #166（クローズ済み）

### 実装した内容

| 設計項目 | 実現したファイル |
| --- | --- |
| detached I/O（レジストリと状態変更eventに触れない） | `Runtime/System/SaveSystem/Internal/Application/SaveDataService.cs` |
| View専用エントリポイント | `Runtime/System/SaveSystem/Internal/View/SaveDataViewStore.cs`（新規） |
| Composition と公開 | `Runtime/System/SaveSystem/SaveStore.cs` の `CurrentViewStore` |
| 3状態の判定と表示値 | `Editor/.../CS/SaveDataBindingSourceEnum.cs`・`SaveDataBindingState.cs`（新規） |
| 再バインド、専用インスタンス、自動ロード、Dirty、持ち越し | `Editor/.../CS/SaveDataWindow.cs` |
| ランプ・状態ラベル・チェックボックス | `Editor/.../UXML/SaveDataWindow.uxml`・`SymphonyWIndow.uss` |
| 持ち越しフラグ | `Editor/Configs/ConfigData/SymphonyUserSettingConfig.cs` |
| テスト | `Tests/Editor/` に3ファイル（新規） |

`SaveDataViewModel` は設計どおり無変更。`SaveDataWindow` から `SaveStore` への参照は `OnCurrentViewModelChanged` / `CurrentViewModel` / `CurrentViewStore` の3つだけになった。

### 設計から変えた点

- **命令の置き場を、着手後に `SaveDataViewModel` から `SaveDataViewStore` へ変えた。** 最初の設計書は ViewModel へ命令を集約すると書いたが、`DesignPhilosophy.md` の `### ViewModel`（「Commandを実行しない」）と `## やってはいけないこと`（「ViewModelからCommandを実行する」）に抵触していた。**設計を書く時点でこの2箇所を読んでいなかったのが原因**で、ワーカーが1度実装を終えた後の差し替えになった。同文書へ `### View専用エントリポイント` を新設して正式な選択肢とした上で採用している。
- **4パネルへ入れた `// TODO:` を全件取り消した。** 「旧規約のまま残る4パネル」という設計書の記述が誤りだった。今回の改訂は規約の**置き換えではなく緩和**であり、Facade を直接呼ぶ既存パネルは今も規約に適合している。実際の呼び出しを数えると、`SceneLoadWindow` と `ServiceLocateWindow` は命令を1つも出しておらず、`AutoEnumGeneratorWindow` は ViewModel も Query も持たない Editor 専用の生成器だった。
- **Editor 向けの公開APIは作らなかった。** 作ってよいという判断はもらっていたが、`InternalsVisibleTo` で Editor から届き、現時点で利用側の要求が無いため見送った。

### 検証結果

`verify_round.py` と `release_round.py preflight` を自分で実行した値。

| 項目 | 結果 |
| --- | --- |
| コンパイル | エラー0・警告0（**確定前は「警告12件」を返した。取り直した値を採用**） |
| EditMode | 334/334 成功 |
| PlayMode | 21/21 成功 ×2往復 |
| Console（テスト実行前） | エラー0件 |
| 新規 `.cs` 6件の `.meta` | 6/6 生成済み |
| `preflight` | 9項目すべて通過 |

実機の表示は `execute-dynamic-code` で要素の状態を直接読み、次まで確認した。

```
state=Window instance | lamp=save-binding-lamp save-binding--instance
rows=8 | toggle=False
```

赤状態の修飾クラスが実際に付くこと、一覧に全8型が出ること（従来はキャッシュ済み／保存済みのみ）、持ち越しチェックボックスが既定 off であること。

### 未実施の確認

「動作確認手順」の11項目のうち、**#3〜#11 が未実施**。黄・緑への遷移、`Loading…`、`未保存の変更あり`、Play Mode 持ち越し、Editor 再起動後の設定保持が該当する。ボタンとトグルを押す手段が無いため自動化できない。`Samples/Runtime/SaveDataSystemSample/SaveDataSystemSample.unity` を再生すると #5〜#7 を踏める。

**「Play Mode 突入時のフラッシュが同期完了しなかった場合の取り消しと入り直し」は、同梱ローダーが同期完了するため原理的に踏めない。** コードレビューと `SaveDetachedAsync_CompletesSynchronously` で担保し、実機確認の対象から外した。

### 振り返り

適用したもの:

- `implement/references/design-doc.md` へ「DesignPhilosophy のレイヤー節と `## やってはいけないこと` 全項目を読む」を追加
- `implement/references/release.md` を `finalize` の実装（PRをマージする）に合わせて修正し、マージ可否を問い合わせて止めないことを明記
- `implement/SKILL.md` へステップ7（実施レポート）と `verify_round.py` の呼び出しを追加
- `audit/references/perspectives.md` へ観点 B9「コード内 TODO の棚卸し」を追加
- `scripts/release_round.py preflight` へ Enter Play Mode Options の検査を追加
- `scripts/verify_round.py` を新設。Unity 側の検証を1コマンドへ集約
- `uloop-execute-dynamic-code` skill へ「複数行 `--code` の失敗は `Success: true` で黙って返る」を追記

見送ったもの:

- **型検証の3重化**（`SaveStore` / `SaveDataService` / `SaveDataLoaderStrategy`）。Service 側の追加は例外契約のために必要だが、統合すると `SaveStore` の公開例外メッセージが変わる。別 Round で単独に扱う。
