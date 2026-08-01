# Scene Load Runtime Layers — Round G1

## 目的

`ArchitectureRevision.md` の Phase 3 にある Scene Load 分割を、単独で検証・リリースできる2 Roundへ分けて進める。本書はその前半である **Round G1** を扱う。

現行の Scene Load は、次の責務が3型へ集中している。

| 型 | 現在の責務 |
| --- | --- |
| `SceneLoader` | 公開API、入力検証、Unity Scene照会、初期化、内部状態の所有 |
| `SceneLoadManager` | 処理順、追跡状態、`SceneManager`／`AsyncOperation`、ルートGameObject初期化、複数処理の集約 |
| `SceneLoadData` | シーン単位の状態、Dictionary検索、Active Scene、ロード完了callback |

このままでは、シーン単位の状態遷移と優先度判断をUnity APIから切り離して検証できない。Round G1ではRuntime内部をDomain、Application、Infrastructureへ分割し、公開`SceneLoader`を既存シグネチャの転送入口にする。

## Round分割

| Round | 含めるもの | 含めないもの | 互換性 |
| --- | --- | --- | --- |
| **G1（本書）** | `SceneLoadRequest`、`IProgress<float>`による進捗経路、`SceneLoadEntity`、`SceneLoadRegistry`、`SceneLoadService`、Application定義のScene操作契約、`UnitySceneLoader`、`SceneResetter`統合、既存Editor診断経路の追従、純粋ロジックのテスト | 公開Info、Adaptor Query／Dto、ViewModel、Editor Windowの購読化、公開型名変更、Awaitableシグネチャ移行 | 後方互換な追加 |
| **G2** | `SceneLoadQuery`、`SceneLoadInfo`、`SceneLoadDto`、`SceneLoadViewModel`、`SceneLoaderWindow`と`SymphonyMcpTools`のQuery／ViewModel利用、リフレクションとEntity直接参照の除去 | enum／Config／Windowの公開名変更、公開非同期シグネチャ変更 | 後方互換な追加 |
| **Phase 4以降** | `SceneLoadState` → `SceneLoadStateEnum`、`SceneManagerConfig` → `SceneLoadConfig`、`SceneLoaderWindow` → `SceneLoadWindow`、公開非同期APIの`Awaitable`化 | G1/G2の内部構造変更 | 3.0.0の破壊的変更 |

G1とG2はそれぞれの終端でコンパイルとテストが通り、後続Roundを実施しなくても既存の公開APIとシリアライズ済みConfigを利用できる状態にする。

## 公開API

### `SceneLoadRequest`

シーン名とActive Scene選択用の優先度を一体として扱う、Adaptor境界の公開Value Objectを追加する。

```csharp
public readonly struct SceneLoadRequest : IEquatable<SceneLoadRequest>
{
    public SceneLoadRequest(string sceneName, int priority = 0);

    public string SceneName { get; }
    public int Priority { get; }
}
```

`public`にする根拠は、利用側がロード対象を構築して公開`SceneLoader`へ渡す入力値であり、DesignPhilosophyの「公開エントリポイントの引数・戻り値として境界を越えるValue Object」に該当するためである。

`LoadSceneMode`はRequestへ含めない。Scene名と優先度は対象Sceneに付随する値だが、Additive／Singleは「そのSceneをどう扱うか」ではなく、他のSceneを残すかという遷移全体の実行方針である。複数RequestをロードするAPIでも各要素へSingleを持たせると、同一batch内で互いをアンロードする矛盾が起きるため、既存どおり単一ロードCommandの引数へ残す。

constructorはnull、空、空白のScene名を`ArgumentException`で拒否する。構造体の`default`は避けられないため、公開メソッド入口でも`SceneName`を再検証する。等値性は`SceneName`と`Priority`の両方で判定する。

### 新しい推奨overload

```csharp
public static ValueTask<bool> LoadScene(
    SceneLoadRequest request,
    IProgress<float> progress = null,
    LoadSceneMode mode = LoadSceneMode.Additive,
    CancellationToken token = default);

public static ValueTask<bool> LoadScenes(
    SceneLoadRequest[] requests,
    IProgress<float> progress = null,
    CancellationToken token = default);
```

複数Sceneのoverloadでは各Requestの優先度を保持する。これにより、将来のIssue #110 Scene BlockがScene名と優先度の平行配列を持たずに済む。ただし依存グラフ、ScriptableObject、並列スケジューリングはIssue #110固有の設計が必要なので、本Roundでは実装もIssueのcloseも行わない。

進捗はC#標準の`IProgress<float>`を新しい推奨契約とする。これによりIssue #112「シーンロードなどの進捗通知にIProgressを利用する」を本Roundの解決対象へ含める。

### 既存APIの互換維持

次の既存シグネチャは変更・削除せず、そのまま維持する。

```csharp
public static bool GetExistScene(string sceneName, out Scene scene);
public static bool IsExist(string sceneName);
public static bool TryGetState(string sceneName, out SceneLoadState state);
public static bool SetActiveScene(string sceneName);
public static bool RegisterLoadedScene(string sceneName, int priority);
public static ValueTask<bool> LoadScene(
    string sceneName,
    Action<float> loadingAction = null,
    LoadSceneMode mode = LoadSceneMode.Additive,
    int priority = 0,
    CancellationToken token = default);
public static ValueTask<bool> LoadScenes(
    string[] sceneNames,
    Action<float> loadingAction = null,
    CancellationToken token = default);
public static ValueTask<bool> UnloadScene(
    string sceneName,
    Action<float> loadingAction = null,
    CancellationToken token = default);
public static ValueTask<bool> UnloadScenes(
    string[] sceneNames,
    Action<float> loadingAction = null,
    CancellationToken token = default);
public static void RegisterAfterSceneLoad(string sceneName, Action action);
public static ValueTask WaitForLoadSceneAsync(
    string sceneName,
    CancellationToken token = default);
```

既存の`Action<float>` overloadは`SceneLoadRequest`と`IProgress<float>`へ変換して新overloadへ転送する互換入口とする。公開overloadの同じ引数位置へ`Action<float>`と`IProgress<float>`を併設すると、`null`を明示する既存コードがoverload ambiguityになる。そのため`IProgress<float>`は`SceneLoadRequest` overloadだけへ追加し、既存string overloadのソース互換性を守る。

`SceneLoadState`は上記`TryGetState`の`out`型であり、改名は破壊的変更になるため本Roundでは触らない。`SceneManagerConfig`も既存のResourcesアセットがその型とGUIDを参照しているため触らない。

## 内部設計

### `SceneLoadEntity`（Domain）

シーン名を同一性として、次の状態だけを保持する`internal sealed class`とする。

- `Name`
- `State`（既存`SceneLoadState`を使用）
- `Priority`
- `Progress`

ロード開始、進捗更新、ロード完了、アンロード開始、優先度更新をメソッドで表し、プロパティにsetterを公開しない。Unityの`Scene`、`AsyncOperation`、`GameObject`は保持しない。進捗は0〜1へ正規化し、ロード完了時は1とする。

### `SceneLoadRegistry`（Application）

`Dictionary<string, SceneLoadEntity>`を所有し、次を担当する。

- Entityの登録、検索、削除、全消去
- 現在Unityにロードされているシーン名との同期。既存Entityの優先度は維持する
- Active Scene名と優先度の保持
- ロード完了済みEntityのうち最高優先度のものを検索する
- `RegisterAfterSceneLoad`用の一回限りcallbackをシーン名ごとに保持し、完了時に取り出して削除する

RegistryはUnity APIを呼ばず、Info／Dtoを生成しない。G1中の`SceneLoader`、`SceneLoaderWindow`、`SymphonyMcpTools`は内部Entityを直接読むが、これはG2でQueryへ置き換えるまでの暫定経路とする。

### Scene操作契約（Application）

Application側に`internal interface ISceneLoader`を置き、`SceneLoadService`が必要とする最小操作だけを定義する。

```csharp
internal interface ISceneLoader
{
    string ActiveSceneName { get; }
    IReadOnlyList<string> GetLoadedSceneNames();
    bool TryGetLoadedScene(string sceneName, out Scene scene);
    bool TrySetActiveScene(string sceneName);
    ValueTask<bool> LoadSceneAsync(
        string sceneName,
        IProgress<float> progress,
        CancellationToken token);
    ValueTask<bool> UnloadSceneAsync(
        string sceneName,
        IProgress<float> progress,
        CancellationToken token);
    ValueTask InitializeRootObjectsAsync(string sceneName);
}
```

`GetExistScene(..., out Scene)`という既存公開APIを変えないため、契約の1メソッドだけはUnityの`Scene`値を受け渡す。ただし`Scene`の有効性判定、`SceneManager`呼び出し、ルートGameObject取得はすべてInfrastructure実装へ閉じ、ServiceとEntityは`Scene`の状態を直接検査・保持しない。

### `UnitySceneLoader`（Infrastructure）

`ISceneLoader`の`internal sealed`実装として次を担当する。

- `SceneManager`によるロード済みScene一覧、Active Scene、Scene検索と切替
- `LoadSceneAsync`／`UnloadSceneAsync`と`AsyncOperation.progress`の通知
- ロード済みSceneのルートGameObject列挙
- `ServiceInjector.TryAutoInject`と`IInitializeAsync.DoInitialize()`の実行
- ルート初期化失敗を既存`SceneInitializationException`へ変換する

公開APIの非同期型はRound G1では`ValueTask`のまま維持する。`IInitializeAsync`と複数処理の`Awaitable`全面移行はPhase 5で行い、本Roundに混ぜない。

### `SceneLoadService`（Application）

コンストラクタで`SceneLoadRegistry`と`ISceneLoader`を受け取り、現行`SceneLoadManager`と`SceneResetter`の処理を統合する。

- 入力検証後のロード、アンロード、Single相当の遷移、複数処理の結果集約
- `SceneLoadRequest`からScene名と優先度を受け取り、複数Requestでも対応を崩さず処理する
- Entityのロード／アンロード状態と進捗の更新
- 優先度に基づくActive Sceneの選択
- 既にUnityへロード済みだが未追跡のSceneをRegistryへ同期する
- ロード完了callbackの登録と実行
- 起動時のScene一覧同期、除外Sceneを残すリセット、初期Sceneロード
- 観測可能な状態が変化したときの`internal event Action OnStateChanged`

`SceneResetter`は削除し、その処理をServiceへ統合する。Serviceは`SceneManager`、`AsyncOperation`、`GameObject`を直接呼ばない。

### `SceneLoader`（Adaptor）

static状態を`SceneLoadService`と`SceneLoadRegistry`へ置き換える。`Initialize`でRegistry、`UnitySceneLoader`、Serviceを生成し、`ResetRuntimeState`でServiceの進行状態とRegistryを破棄する。

公開メソッドは入力検証と転送だけを行う。CommandはServiceへ、G2までの単純な状態照会はRegistryへ転送する。公開用Infoへの変換はまだ行わない。既存`Action<float>`は、`Report`が同じ呼び出しスタックでdelegateを実行する内部adapterへ包み、`Progress<T>`がSynchronizationContextへpostすることによる通知タイミング変更を避ける。

`AfterSceneLoad(SceneManagerConfig)`はConfigの値をprimitive／配列へ取り出してServiceへ渡す。ApplicationからConfigアセットを参照しない。

## アクセス手段の検証

設計確定前に次を現行コードで確認した。

- `Runtime/AssemblyInfo.cs`は`SymphonyFrameWork.Editor`、`SymphonyFrameWork.Tests.Editor`、`SymphonyFrameWork.Tests.Runtime`へ`InternalsVisibleTo`を付与済み。Editorとテストから新しい`internal`型へ到達できる
- `Tests/Editor/SymphonyFrameWork.Tests.Editor.asmdef`は`SymphonyFrameWork`を参照し、`UNITY_INCLUDE_TESTS`を`defineConstraints`へ指定済み。Entity、Registry、ServiceのEditMode単体テストを追加できる
- `SymphonyMcpTools.GetSceneLoaderJson()`は`SceneLoader.TrackedScenes`を通して`SceneLoadData.SceneInfo`へコンパイル時依存している。G1ではアクセサの戻り値をEntity一覧へ追従させ、JSONのフィールド名と意味を維持する
- `SceneLoaderWindow`は`SceneLoadData.SceneInfo`をフィールド型に使い、さらに`SceneLoader._data`と`SceneLoadData._sceneDict`をリフレクションで取得している。`SceneLoadData`削除後もコンパイルするため、G1では型とリフレクション対象だけを`_registry`／Entity辞書へ追従させる。リフレクションの削除と購読化はG2で行う
- `SceneManagerConfig`は`internal`だが読み取りプロパティがあり、`SymphonyOrchestrator`から`SceneLoader.AfterSceneLoad`へ既に渡されている。新しい公開アクセサや可視性変更は不要

## ファイル構成

名前空間はすべて既存の`SymphonyFrameWork.System.SceneLoad`を維持し、概念レイヤー名を含めない。

| パス | 変更 |
| --- | --- |
| `Runtime/System/SceneLoader/SceneLoadRequest.cs`（新規） | Scene名と優先度を持つ公開Value Object |
| `Runtime/System/SceneLoader/SceneLoader.cs` | static所有先をService／Registryへ変更し、公開メソッドを転送化 |
| `Runtime/System/SceneLoader/Internal/Domain/SceneLoadEntity.cs`（新規） | シーン単位の状態と遷移 |
| `Runtime/System/SceneLoader/Internal/Application/SceneLoadRegistry.cs`（新規） | Entity、Active Scene、完了callbackの所有と検索 |
| `Runtime/System/SceneLoader/Internal/Application/ISceneLoader.cs`（新規） | Applicationが定義するScene操作契約 |
| `Runtime/System/SceneLoader/Internal/Application/SceneLoadService.cs`（新規） | Command処理、優先度判断、起動時リセット |
| `Runtime/System/SceneLoader/Internal/Infrastructure/UnitySceneLoader.cs`（新規） | Unity Scene APIとルートObject初期化 |
| `Runtime/System/SceneLoader/Internal/SceneLoadManager.cs` | 削除。ServiceとInfrastructureへ分割 |
| `Runtime/System/SceneLoader/Internal/SceneLoadData.cs` | 削除。RegistryとEntityへ分割 |
| `Runtime/System/SceneLoader/Internal/SceneResetter.cs` | 削除。Serviceへ統合 |
| `Editor/Debug/SymphonyMcpTools.cs` | ソース変更不要。`SceneLoader.TrackedScenes`の戻り値変更によりEntity一覧へ型追従 |
| `Editor/Administrator/UITK/CS/SceneLoaderWindow.cs` | G1のコンパイル互換のため型とリフレクション対象だけを追従。購読化はしない |
| `Tests/Editor/SceneLoadEntityTests.cs`（新規） | Entityの状態遷移と進捗 |
| `Tests/Editor/SceneLoadRegistryTests.cs`（新規） | 登録、検索、優先度、callback、同期 |
| `Tests/Editor/SceneLoadServiceTests.cs`（新規） | fake `ISceneLoader`によるActive Scene優先度判断と転送 |
| `Tests/Editor/SceneLoadRequestTests.cs`（新規） | 公開Value Objectの検証と等値性 |
| `Samples/Runtime/SceneLoaderSample/Scripts/SceneLoaderSample_Controller.cs` | 新しいRequest／`IProgress<float>` overloadの利用例へ1経路を更新 |

新しい`.cs`と新規フォルダの`.meta`は手書きせず、Unity Editorへ生成させる。既存3型の削除は対応する`.meta`と対で行う。移動ではなく責務分割による置換であり、公開MonoBehaviour／ScriptableObjectのGUIDは変更しない。

## 依存方向

```text
SceneLoader（Adaptor）
        │ Command                         │ 暫定Query（G2まで）
        v                                 v
SceneLoadService（Application） ───> SceneLoadRegistry（Application）
        │                                      │
        │ ISceneLoader                         v
        v                              SceneLoadEntity（Domain）
UnitySceneLoader（Infrastructure）
        │
        └──> SceneManager / AsyncOperation / GameObject / ServiceInjector
```

- EntityとRegistryからInfrastructure、Adaptor、Editorを参照しない
- Serviceは`SceneManager`等のScene操作APIを直接呼ばず、Application側の契約を介して実行する
- Infrastructureは契約を実装し、Composition相当の`SceneLoader.Initialize`が具象型を結合する。サブシステム全体の生成をOrchestratorへ移す作業は後続の全サブシステム統合時に判断する
- RuntimeからEditorを参照しない
- `IProgress<float>`はAdaptor、Application、Infrastructureを同じ標準契約で通過し、Entityの進捗更新と利用側通知をServiceが同じ報告値から行う

## エラー処理

- 公開入口のnull、空文字、空配列の扱いと例外型は現行どおり維持する
- `SceneLoadRequest` constructorとRequest overloadはnull、空、空白のScene名を`ArgumentException`で拒否する。`default(SceneLoadRequest)`も入口検証で拒否する
- SceneがBuild Settingsに無い、またはUnityのロード／アンロード開始に失敗した場合は`false`を返し、追跡開始前または失敗後のEntityを残さない
- 依存注入／`IInitializeAsync`の失敗は既存どおり`SceneInitializationException`として文脈付きで送出する
- `CancellationToken`によるキャンセルは`OperationCanceledException`として伝播し、一般的な`false`と混同しない。Unityの`AsyncOperation`自体を強制停止できない点は既存契約と同じ
- 未初期化時は既存どおり`SymphonyNotInitializedException`を送出する
- `OnStateChanged`はEntityの開始、進捗、完了、削除、Active Scene変更が実際に確定した場合だけ通知する

## 影響範囲

既存公開API、enumの数値、Config型／フィールド、Resourcesアセットは変わらない。後方互換な公開Value Objectとoverloadを追加する。`SceneLoaderSample`の1経路を新APIの例へ更新し、残りの既存呼び出しを互換確認として残す。`ServiceLocatorSample`は既存APIの互換確認に使用する。

内部では`SceneLoadData.SceneInfo`が削除されるため、`SceneLoaderWindow`の型とリフレクション対象を追従させる。`SymphonyMcpTools`は`SceneLoader.TrackedScenes`を介していたためソース変更なしでEntity一覧へ追従する。G1終了時点のEditor Windowは引き続きリフレクションと更新時ポーリングを使うため、レイヤー分割の完成ではない。この負債はG2の対象として残す。

## テストの置き場と種別

`Assets/SymphonyFrameWork/Tests/Editor/`へEditMode単体テストを追加する。`InternalsVisibleTo`で内部型へ直接到達し、Unity Sceneの実操作を必要としないfake `ISceneLoader`をテスト内に定義する。

- `SceneLoadEntityTests`: ロード開始、進捗の0〜1正規化、完了、アンロード開始、優先度更新をメソッド呼び出しとプロパティ検証で確認する
- `SceneLoadRegistryTests`: 登録／検索／削除、Unity側一覧との同期時の優先度維持、最高優先度検索、ロード完了callbackの即時実行と一回実行を確認する
- `SceneLoadServiceTests`: fakeへロード済みScene名とActive Scene名を与え、同優先度以上のロードでActive Sceneが切り替わること、低い優先度では切り替わらないこと、Active Sceneアンロード後に残存する最高優先度Sceneを選ぶことを確認する
- `SceneLoadRequestTests`: constructorで値が保持されること、無効なScene名を拒否すること、Scene名と優先度による等値性を通常のNUnit比較で確認する
- `SceneLoadServiceTests`内でfake `ISceneLoader`へ渡された`IProgress<float>`を呼び、Entityの進捗と外部observerへ同じ値が届くことも確認する

既存EditMode／PlayModeテストも全数実行する。PlayModeテスト内でPlay Modeを抜けて再入することはできないため、Domain Reloadなしの2往復は下記の手動操作として行う。

## 動作確認手順

1. `uloop-clear-console`後に`uloop-compile`を実行し、Error 0件・意図しないWarning 0件であること
2. EditModeとPlayModeの全テストが成功すること。新規4テストクラスが実行対象に含まれること
3. `SceneLoaderSample`をPlayし、`SceneLoadRequest`と`IProgress<float>`を使う経路を含め、Scene A/Bの追加ロード、優先度によるActive Scene切替、待機、アンロードが従来どおり動作すること
4. `LoadSceneMode.Single`で対象Scene以外がアンロードされ、Orchestratorの永続GameObjectが残ること
5. Play Modeの開始・終了を2回繰り返し、`SymphonyMcpTools.GetSceneLoaderJson()`が2回目にも有効なJSONを返し、前回だけの追跡Sceneを含まないこと
6. `SceneLoaderWindow`がPlay Mode中のScene名、状態、優先度を引き続き表示できること（G1では更新方式は変更しない）
7. `rg`で`SceneLoadManager`、`SceneLoadData`、`SceneResetter`への参照が0件であること
8. `rg -n "UnityEditor|EditorPrefs" Runtime Core -g '*.cs'`で新しいRuntime→Editor参照が無いこと

## バージョン判断

**マイナー（2.12.0）。** 既存APIとシリアライズ形式は維持し、後方互換な`SceneLoadRequest`と`IProgress<float>` overloadを追加するため。あわせて後続のQuery／ViewModel追加を可能にする内部アーキテクチャを分割する。

## このRoundで触るバージョン関連ファイル

| ファイル | 触る箇所 |
| --- | --- |
| `Assets/SymphonyFrameWork/package.json` | `version`を`2.11.0` → `2.12.0` |
| `Assets/SymphonyFrameWork/CHANGELOG.md` | `## [2.12.0] - 2026-08-02`と、Request／IProgress追加、内部レイヤー分割、Unity境界分離、テスト追加、既存API互換維持の説明 |
| `Assets/SymphonyFrameWork/README.md` | 「現在のバージョン」を`2.12.0`へ更新し、Scene Loader節へ`SceneLoadRequest`と`IProgress<float>`の最小例を追加 |
| `Assets/SymphonyFrameWork/Samples/Runtime/SceneLoaderSample/Scripts/SceneLoaderSample_Controller.cs` | 1つのロード経路を新APIの例へ変更し、他の既存overload利用は互換確認として残す |
| `Assets/SymphonyFrameWork/Documentation~/Architecture.md` | Scene Loaderの公開型関係へ`SceneLoadRequest`を追加 |

`AGENTS.md`と`Documentation~/AgentUsage.md`は導線やAI向け常時制約が変わらないため更新しない。`Documentation~/Architecture.md`は新しい公開Value Objectとの関係だけを反映し、内部レイヤー全体はG2完了後にまとめて反映する。

## GitHub Issue

- **Issue #112**を本Roundで解決する。Runtime内部の全進捗経路を`IProgress<float>`へ統一し、新しいRequest overloadから標準契約を公開する。既存`Action<float>` overloadはソース互換のadapterとして残す
- Issue #104はScene Loadのテスト追加によって一部前進するが、「全機能」の完了条件を満たさないためcloseしない
- Issue #105は`IInitializeAsync`の所属変更以外にService Locate側の移動も含み、公開名前空間の扱いもPhase 4の判断が必要なため本Roundへ混ぜない
- Issue #110は`SceneLoadRequest`が将来のScene Blockの入力単位になり得るが、依存グラフと並列スケジューラを実装しないためcloseしない

submoduleブランチは`feature/112-scene-load-runtime-layers`を使用し、PR本文へ`Issue: #112`を記載する。PRのbaseは`develop`でありGitHubリポジトリの既定ブランチ`main`ではないため、自動close用の`Closes #112`は使わない。
