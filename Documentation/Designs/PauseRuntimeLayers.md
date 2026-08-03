# Pause のレイヤー分割と脱リフレクション — Round J1

## 目的

`PauseManager`（281行）が、公開Facade・ポーズ状態の保持・`IPausable` の購読管理・待機ユーティリティをすべて担っている。Scene Load / Service Locate / Save Data と同じ形へ揃える。

あわせて、Phase 3 で残っている最後の2つを解消する。

- **`PauseWindow` のリフレクション**（`typeof(PauseManager).GetField("_pause", BindingFlags.Static | BindingFlags.NonPublic)`）— パッケージ内で internal 状態をリフレクションで読む唯一の箇所
- **`SymphonyAdministrator.Update()` の polling** — 残る呼び出しは `_pauseWindow?.Update()` の1件のみ。これを event 購読へ置き換えると、`Update()` と `EditorApplication.update` の購読自体を削除できる

Audio は同じ Round に含めない。**Audio には Editor Window も MCP 診断も無く、Query と ViewModel を作っても利用者がいない。** DesignPhilosophy の「使用されていないクラス設計は作らない」に反する。Audio は Domain / Application の分割だけを Round J2 で扱う。

本 Round は 2.17.1 を含む `develop` から開始し、単独で検証・リリース可能な **2.18.0** とする。`IPausable` の移動と改名（Phase 4）は含めない。

## 前提の確認

設計を確定する前に実コードで確認した事実を記録する。

| 前提 | 確認結果 |
| --- | --- |
| Pause は Play Mode 専用か | **専用である。** `PauseManager.Initialize()` の呼び出し元は `SymphonyOrchestrator.cs:49` だけで、Editor 側の初期化入口は無い。Save Data と違い、`ServiceLocatorWindow` と同じ `playModeStateChanged` 方式が使える |
| `PauseWindow` の現在の状態取得 | `_pauseInfo.GetValue(null)` によるリフレクション。**`internal static bool IsPaused` が既にあるにもかかわらず使っていない。** Round C で MCP 用に追加したが Window は追従していない |
| `PauseWindow` は破棄されるか | **されない。** `IDisposable` を実装しておらず、`SymphonyAdministrator.OnDisable()` も呼んでいない。本 Round で追加する |
| `SymphonyAdministrator.Update()` の残り | `_pauseWindow?.Update()` の1件のみ（Save Data 分は Round I2 で削除済み） |
| `internal` アクセサの利用者 | `IsInitialized` / `IsPaused` / `PausableSubscriberCount` はいずれも `SymphonyMcpTools.cs` からのみ |
| Editor から internal 型へ到達できるか | `Runtime/AssemblyInfo.cs` に `InternalsVisibleTo("SymphonyFrameWork.Editor")` がある。新しい asmdef 参照は不要 |
| 既存テスト | Pause のテストは0件 |

## 公開API

### 変更しないもの

`Pause`、`OnPauseChanged`、`PausableNextFrameAsync`、`PausableWaitForSecond`、`PausableWaitForSecondAsync`、`PausableWaitUntil`、`PausableDestroy`、`PausableInvoke`、`IPausable`（`PauseManager` の入れ子のまま）のシグネチャ、例外の種類と条件をいずれも変更しない。

`IPausable` を入れ子から出すのは利用側の `using` を壊すため Phase 4 で行う。

### 追加するもの

```csharp
public readonly struct PauseInfo : IEquatable<PauseInfo>
{
    public bool IsPaused { get; }
    public int PausableSubscriberCount { get; }
}

public static PauseInfo GetPauseInfo();
```

公開する根拠は、Service Locate の `ServiceRegistrationInfo` および Scene Load の `SceneLoadInfo` と同じで、**利用側と診断が管理状態を取得時点の不変値として照会できるようにするため**である。購読件数はデバッグ時に「解除し忘れが積み上がっていないか」を見る値として役に立つ。

`PauseInfo` は Query 取得時点の値を保持し、その後の状態変更で値自体は変化しない。

### 削除するもの

`internal static bool IsPaused` と `internal static int PausableSubscriberCount` を削除する。`SymphonyMcpTools` は `GetPauseInfo()` を使う。Round H2 で `RegisteredInstances` / `SingletonRoot` を、Round I2 で `LoadedTypes` を削除したのと同じ整理である。

`internal static bool IsInitialized` は残す。MCP が「Play Mode かつ初期化済みか」を判定するために、例外を出さずに問い合わせる必要があるため。

## 内部型

### `PauseStateEntity`

`Runtime/System/Pause/Internal/Domain/PauseStateEntity.cs`（`internal sealed`）。

```csharp
internal sealed class PauseStateEntity
{
    public bool IsPaused { get; private set; }
    public bool SetPaused(bool isPaused);   // 変化したときだけ true
}
```

状態が1つの bool だけなので Entity としては小さいが、**「変化したときだけ true を返す」という判定をここへ閉じ込める**ことに意味がある。現在の `Pause` setter は同じ値を代入しても毎回 `_onPauseChanged` を発火しており、`IPausable` の `Pause()` が連続で呼ばれうる。

これは挙動の変更にあたるため、**CHANGELOG に Fix として明記する**（下記「影響範囲」）。

### `PausableRegistry`

`Runtime/System/Pause/Internal/Application/PausableRegistry.cs`（`internal sealed`）。

`IPausable` と、それに対応する `Action<bool>` の対応表を所有する。現在 `PauseManager` の `_pauseEventDictionary` が担っている責務。

- `bool TryRegister(IPausable pausable, out Action<bool> pauseEvent)` — 既に登録済みなら false
- `bool TryUnregister(IPausable pausable, out Action<bool> pauseEvent)`
- `int Count`
- `void Clear()`

### `PauseService`

`Runtime/System/Pause/Internal/Application/PauseService.cs`（`internal sealed`）。

- `PauseStateEntity` と `PausableRegistry` を保持する
- `bool IsPaused` / `void SetPaused(bool)` — 変化したときだけ `OnPauseChanged` と `OnStateChanged` を発行する
- `event Action<bool> OnPauseChanged` — 利用側向け。Facade がそのまま中継する
- `event Action OnStateChanged` — 表示向け。ポーズ状態の変化と購読件数の変化で発行する
- `void Register(IPausable)` / `void Unregister(IPausable)`
- `void Reset()`

**購読者の例外は発行側で止めない。** Save Data（Round I2）では表示専用の ViewModel だけが購読者だったため発行側で握ったが、`OnPauseChanged` は**利用側のゲームロジックが購読する公開 event** であり、その例外を握り潰すと不具合が見えなくなる。現在の実装も握っていない。

`OnStateChanged`（表示専用）だけは ViewModel の失敗をゲームへ波及させないため、発行側で捕捉してログへ出す。

### `PauseQuery`

`Runtime/System/Pause/Internal/Adaptor/PauseQuery.cs`（`internal sealed`）。

- `PauseInfo GetInfo()` — 公開Info
- `PauseDto GetDto()` — 表示用Dto

Query だけが Entity と Registry を読む。

### `PauseDto`

`Runtime/System/Pause/Internal/Adaptor/PauseDto.cs`（`internal readonly struct`）。

```csharp
internal readonly struct PauseDto : IEquatable<PauseDto>
{
    internal bool IsPaused { get; }
    internal int PausableSubscriberCount { get; }
}
```

Editor Window は現在ポーズ状態しか表示していないが、**購読件数も表示する**。解除し忘れの検出はポーズ機構で最も起きやすい不具合であり、管理パネルに出す価値がある。

### `PauseViewModel`

`Runtime/System/Pause/Internal/View/PauseViewModel.cs`（`internal sealed`）。

- constructor で `PauseQuery` と `PauseService` を受け取る
- `service.OnStateChanged` を購読し、`IReadOnlyReactiveProperty<PauseDto> State` を公開する
- `Dispose` で購読と `ReactiveProperty` を冪等に解放する

## 公開Facade と Composition

```text
PauseManager
  Command ──> PauseService
  Query   ──> PauseQuery
  View    ──> PauseViewModel（internal accessor のみ）
```

- `Initialize()` が Entity / Registry / Service / Query / ViewModel を結合する
- `ResetRuntimeState()` は最初に ViewModel を Dispose し、Service を Reset して全参照を null へ戻す。多重呼び出しで安全にする
- `internal static PauseViewModel CurrentViewModel` を追加する
- 待機ユーティリティ（`PausableWaitForSecondAsync` など）は Facade に残し、ポーズ状態の読み取りだけ Query へ委譲する。これらは状態を持たない待機処理であり、レイヤーを増やす利得が無い

Orchestrator の初期化順と `PauseManager.Initialize()` の入口は変更しない。

### ファイル移動

`Runtime/System/PauseManager.cs` を `Runtime/System/Pause/PauseManager.cs` へ移す。名前空間 `SymphonyFrameWork.System` は**変更しない**（公開型のため、変更は Phase 4）。

CodeGuidelines の「名前空間とフォルダの対応」に対して、`Runtime/System/Pause/` に `SymphonyFrameWork.System` を置くのは他のサブシステムと同じ扱い（`Runtime/System/SaveSystem/` が `SymphonyFrameWork.System.SaveSystem`、`Runtime/System/SceneLoader/` が `SymphonyFrameWork.System.SceneLoad`）とは異なる。**`PauseManager` は名前空間を変えずフォルダだけ移す**ため、この Round では不一致が残る。Phase 4 の改名で `SymphonyFrameWork.System.Pause` へ揃える。

`.meta` を `.cs` と一緒に移動し、GUID を維持する。

## Editor Window

`PauseWindow` を `ServiceLocatorWindow` と同じ形にする。

- `IDisposable` を実装する
- **リフレクション（`FieldInfo` / `BindingFlags`）を完全に除去する。** `using System.Reflection;` も消える
- `EditorApplication.playModeStateChanged` を購読し、EnteredPlayMode 後の `delayCall` で ViewModel へ接続する
- `State.Subscribe(ApplyState)` で変更時だけ表示を更新する
- ExitingPlayMode / EnteredEditMode で購読を解除し、非ポーズ表示へ戻す
- `Update()` を削除する
- 購読件数を表示する Label を UXML へ追加する
- ポーズ / 再開ボタンは、Play Mode 外では `SymphonyNotInitializedException` になるため、未初期化時は無効化する（現在は例外がそのまま Console へ出る）

`SymphonyAdministrator` から次を削除する。

- `private void Update()` そのもの
- `EditorApplication.update += Update` / `-= Update`
- `OnDisable()` へ `_pauseWindow?.Dispose()` と `_pauseWindow = null` を追加する

**これで Phase 3 の「Editor は polling をやめて event 購読へ移行する」が完了する。**

## MCP診断

`SymphonyMcpTools.GetPauseJson()` は `PauseManager.GetPauseInfo()` を使う。JSON のフィールド名と意味は維持する。

```json
{ "initialized": true, "paused": false, "pausableSubscriberCount": 0 }
```

## ファイル構成

### 新規

| パス | レイヤー | 公開範囲 |
| --- | --- | --- |
| `Runtime/System/Pause/PauseInfo.cs` | Adaptor公開Info | public readonly struct |
| `Runtime/System/Pause/Internal/Domain/PauseStateEntity.cs` | Domain | internal sealed |
| `Runtime/System/Pause/Internal/Application/PausableRegistry.cs` | Application | internal sealed |
| `Runtime/System/Pause/Internal/Application/PauseService.cs` | Application | internal sealed |
| `Runtime/System/Pause/Internal/Adaptor/PauseQuery.cs` | Adaptor Query | internal sealed |
| `Runtime/System/Pause/Internal/Adaptor/PauseDto.cs` | Adaptor Dto | internal readonly struct |
| `Runtime/System/Pause/Internal/View/PauseViewModel.cs` | View | internal sealed |
| `Tests/Editor/PauseStateEntityTests.cs` | EditMode test | test assembly |
| `Tests/Editor/PausableRegistryTests.cs` | EditMode test | test assembly |
| `Tests/Editor/PauseServiceTests.cs` | EditMode test | test assembly |
| `Tests/Editor/PauseViewModelTests.cs` | EditMode test | test assembly |
| `Tests/Editor/PauseInfoTests.cs` | EditMode test | test assembly |

### 移動

- `Runtime/System/PauseManager.cs` → `Runtime/System/Pause/PauseManager.cs`（`.meta` を同時に移動）

### 変更

- `Editor/Administrator/UITK/CS/PauseWindow.cs`
- `Editor/Administrator/UITK/UXML/PauseWindow.uxml`（購読件数の Label を追加）
- `Editor/Administrator/SymphonyAdministrator.cs`
- `Editor/Debug/SymphonyMcpTools.cs`
- `package.json` / `CHANGELOG.md` / `README.md`
- `Documentation~/Architecture.md` / `Documentation~/AgentUsage.md`

## 依存方向

```text
PauseManager ──Command──> PauseService ──> PauseStateEntity
     │                        │       └──> PausableRegistry
     └──Query──────────────> PauseQuery ──┘
                                  │
                    ┌─────────────┴─────────────┐
                    v                           v
                PauseInfo                   PauseDto
                                                │
PauseService.OnStateChanged ──> PauseViewModel ─┘
                                                │
                                  IReadOnlyReactiveProperty
                                                │
                                                v
                                          PauseWindow
```

Domain と Application は Unity API に依存しない（待機ユーティリティは Facade に残るため）。Editor は ViewModel / Dto と公開 Facade だけを参照する。

## エラー処理

- 未初期化での公開API呼び出しは従来どおり `SymphonyNotInitializedException`
- `GetPauseInfo()` も未初期化では `SymphonyNotInitializedException`。`IsInitialized` で事前に判定できる
- `Register` / `Unregister` の null は `ArgumentNullException`（従来どおり）
- `OnPauseChanged`（公開event）の購読者例外は**握らない**。従来どおり呼び出し元へ伝播する
- `OnStateChanged`（表示専用）の購読者例外は発行側で捕捉してログへ出す
- MCP は既存どおり例外を JSON の `error` へ変換する

## 影響範囲

- 公開APIのシグネチャと例外条件は変更しない
- `PauseInfo` と `GetPauseInfo()` の追加は後方互換
- **挙動が1つ変わる**: `PauseManager.Pause` に現在と同じ値を代入したとき、`OnPauseChanged` が発行されなくなる。従来は `Pause = true` を2回続けると `IPausable.Pause()` が2回呼ばれていた。**CHANGELOG へ Fix として記載し、利用側への影響を明記する。** 同じ値での再通知に依存した実装は壊れるが、それは状態変更通知としては不正な依存である
- `internal` アクセサ2つの削除は利用側への破壊的変更ではない
- `PauseManager.cs` の移動は名前空間を変えないため、利用側の `using` に影響しない

## テストの置き場と種別

すべて `Tests/Editor/` の EditMode テストとして追加する。Pause の Domain / Application は Unity API に依存しないため EditMode で完結する。`InternalsVisibleTo` により internal 型へ直接アクセスする。

### `PauseStateEntityTests`

`new PauseStateEntity()` を直接生成し、`SetPaused` の戻り値と `IsPaused` を検証する。

- 初期状態が非ポーズであること
- 異なる値の設定で true が返り、状態が変わること
- **同じ値の再設定で false が返り、状態が変わらないこと**（再通知抑止の根拠）

### `PausableRegistryTests`

`IPausable` を実装したテスト用クラスを定義し、`new PausableRegistry()` を直接検証する。

- 初回登録が成功し `Count` が増えること
- 重複登録が false を返し `Count` が増えないこと
- 未登録の解除が false を返すこと
- `Clear` で `Count` が 0 になること
- null 引数が `ArgumentNullException` になること

### `PauseServiceTests`

`new PauseService(new PauseStateEntity(), new PausableRegistry())` を直接生成して検証する。Unity のライフサイクルに依存しない。

- `SetPaused` で `OnPauseChanged` が新しい値とともに1回発行されること
- **同じ値の再設定では発行されないこと**（購読回数で確認）
- 登録済み `IPausable` の `Pause()` / `Resume()` が状態に応じて呼ばれること
- 解除後は呼ばれないこと
- 登録・解除で `OnStateChanged` が発行されること（購読件数が表示に出るため）
- `OnStateChanged` の購読者が例外を投げても `SetPaused` が失敗しないこと（`LogAssert.Expect` で消費する）
- **`OnPauseChanged` の購読者例外は伝播すること**（握っていないことの回帰テスト）
- `Reset` で状態と登録が消えること

### `PauseViewModelTests`

- constructor 直後の初期値が Query の現在値と一致すること
- `SetPaused` と `Register` / `Unregister` で `State` が更新されること
- 内容が変わらない場合は通知されないこと（購読回数で確認）
- `Dispose` 後は更新されず、多重 `Dispose` が無害であること

### `PauseInfoTests`

- constructor の値、等値・非等値、operator、hash を検証する

Editor Window の表示は自動テストにしない。UXML と `EditorApplication` のコールバックに依存するため、手動確認へ回す。

## 動作確認手順

1. Unity Scene 検証ガードに従い、親と submodule の dirty 状態を記録する
2. `uloop-clear-console` 後に `uloop-compile` を実行し、Error 0、意図しない Warning 0 を確認する
3. EditMode と PlayMode の全テストを実行する
4. `rg -n "BindingFlags|GetField\(" Editor -g '*.cs'` の結果が `SubclassSelectorDrawer.cs`（`SerializedProperty` → `FieldInfo` の解決であり除去対象ではない）だけになることを確認する
5. `rg -n "EditorApplication.update" Editor -g '*.cs'` が 0 件であることを確認する
6. Pause Manager Sample を Play し、管理パネルのポーズ表示が緑／赤で切り替わり、購読件数が表示されることを確認する
7. Play Mode 外で管理パネルのポーズ／再開ボタンが無効化され、Console へ例外が出ないことを確認する
8. Play Mode の開始・終了を2回繰り返し、2回目に前回の ViewModel 購読が残らないことを確認する
9. `SymphonyMcpTools.GetPauseJson()` が既存フィールドを返すことを確認する
10. Console の Error / Exception が 0 件であることを確認する
11. Play Mode 停止後に package の `.unity` / `.prefab` 差分が無いことを確認する
12. `git status` で `PauseManager.cs` の移動が rename として記録され、`.meta` が対で動いていることを確認する
13. この Round で追加・変更した `.cs` に UTF-8 BOM が付いていることを確認する

## バージョン判断

**マイナー（2.18.0）。** 後方互換な `PauseInfo` と `GetPauseInfo()` を追加するため。同じ値での再通知を止めるのは不具合修正であり、シグネチャと例外条件は変わらない。

## この Round で触るバージョン関連ファイル

| ファイル | 変更 |
| --- | --- |
| `Assets/SymphonyFrameWork/package.json` | `2.17.1` → `2.18.0` |
| `Assets/SymphonyFrameWork/CHANGELOG.md` | 2.18.0 へ Info／レイヤー分割／脱リフレクション／再通知の修正を記録 |
| `Assets/SymphonyFrameWork/README.md` | 現在のバージョン |
| `Assets/SymphonyFrameWork/Documentation~/Architecture.md` | Pause の内部構成図を追加 |
| `Assets/SymphonyFrameWork/Documentation~/AgentUsage.md` | `GetPauseInfo()` と同値再通知の仕様を追加 |

## ブランチ

`develop` から `feature/pause-runtime-layers` を作成する。

## Round J2（本 Round の対象外）

Audio の Domain / Application 分割。`AudioSettingData` → `AudioGroupEntity`、`AudioGroupRegistry`、`AudioService`。**Query と ViewModel は作らない**（Editor Window も MCP 診断も無く、利用者がいないため）。着手時に「Audio を表示する経路が本当に無いか」を再確認する。

## 実装後の記録

実装時に設計書と食い違った判断、および実装中に判明した事実を記録する。

### `PauseService` の event 公開方法

設計書では `event Action<bool> OnPauseChanged` をそのまま持つと書いたが、Facade の `add` / `remove` アクセサから購読者を出し入れするため、`AddPauseChangedHandler` / `RemovePauseChangedHandler` を明示的に用意した。C# の event はクラス外から `+=` できないため、Facade からの中継にはメソッドが要る。

### `PauseWindow` の未接続時の表示

設計書には「非ポーズ表示へ戻す」とだけ書いたが、非ポーズ（赤）と未接続の区別が付かないため、未接続時は背景色を既定のグレーへ戻し、テキストを `-` にした。ポーズ／再開ボタンも `SetEnabled(false)` にする。

### テスト実行の不安定さ（本 Round の変更とは無関係）

`LogAssert` を使う「意図的に例外ログを出すテスト」のログが、**リコンパイル直後の初回テスト実行に限り**、後続の async テストの未処理ログとして計上されることがある。

- `ReactivePropertyTests` が `LogAssert.ignoreFailingMessages` を使っていたのを `LogAssert.Expect` へ変更した。`ignoreFailingMessages` はログを無視するだけで消費しないため、遅れて届いたログがテスト間へ漏れる
- それでも**リコンパイル直後の初回実行では再現する**。`uloop-clear-console` を挟み、コンパイルが落ち着いてから実行すると安定する
- 3回連続実行で、1回目のみ失敗し2回目・3回目は 208/208 で成功することを実測した
