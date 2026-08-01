# VContainer 基本サンプル

VContainerによる依存性注入（DI）を、カウンター機能を通して確認するための最小サンプルです。`LifetimeScope`で登録した設定・サービス・EntryPointが、コンストラクタ経由でPresenterへ渡されます。

## 実行方法

1. `Scene/VContainerBasicSample.unity`を開きます。
2. Play Modeを開始します。
3. `Increment through injected service`で値を加算し、`Reset`で初期値へ戻します。
4. `Data/VContainerSampleSettings.asset`の値を変更すると、初期値や加算量を変更できます。

画面はサンプルを小さく保つため、`OnGUI`で描画しています。

## 登録と依存関係

`VContainerBasicSample.Configure()`では、次の依存関係を登録しています。

- `VContainerSampleSettings`：既存のScriptableObjectを登録
- `ICounterService` → `CounterService`：Singletonとして登録
- `VContainerSamplePresenter`：`IStartable`のEntryPointとして登録

`LifetimeScope`自身はVContainerによって自動登録されるため、`RegisterInstance(this)`を追加する必要はありません。追加すると実装型の登録が競合します。

## 主なファイル

- `Scripts/VContainerBasicSample.cs`：`LifetimeScope`と簡易View
- `Scripts/ICounterService.cs`：カウンターサービスの契約
- `Scripts/CounterService.cs`：サービス実装
- `Scripts/VContainerSamplePresenter.cs`：DIされた依存関係を接続するEntryPoint
- `Scripts/VContainerSampleSettings.cs`：サンプル設定用ScriptableObject
- `Data/VContainerSampleSettings.asset`：シーンから参照する設定データ
- `Scripts/LibraryResearch.VContainer.asmdef`：VContainerだけを参照する隔離アセンブリ

## 確認できる要素

- `LifetimeScope.Configure()`による登録
- インターフェースと実装の紐付け
- コンストラクタインジェクション
- `IStartable`による初期化
- `IDisposable`によるイベント購読解除

