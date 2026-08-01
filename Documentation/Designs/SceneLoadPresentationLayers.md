# Scene Load Presentation Layers

## 目的

Round G2として、Scene Loadの読み取り経路をAdaptorの`SceneLoadQuery`へ集約し、公開状態を`SceneLoadInfo`、View向け更新値を`SceneLoadDto`として返す。Runtimeの`SceneLoadViewModel`は`SceneLoadService.OnStateChanged`を購読し、Editorの`SceneLoaderWindow`はViewModelの読み取り専用ReactivePropertyだけを購読する。

これにより、G1終了時点で暫定的に残した次の依存を解消する。

- `SceneLoader`が`SceneLoadRegistry`／`SceneLoadEntity`を直接読んでいる
- `SceneLoaderWindow`が`SceneLoader._registry`と`SceneLoadRegistry._entities`をリフレクションで取得している
- `SceneLoaderWindow`がEditor更新ごとにDictionaryを再取得し、ListViewを再構築している
- `SymphonyMcpTools`が内部Entity一覧を直接列挙している

本Roundは[SceneLoadRuntimeLayers.md](./SceneLoadRuntimeLayers.md)のG2であり、G1のcommit `b60b452`を取り込んだPR #116のマージ後の`develop`を前提とする。

## 対象範囲

| 含める | 含めない |
| --- | --- |
| `SceneLoadQuery`、`SceneLoadInfo`、`SceneLoadDto`、`SceneLoadViewModel` | `SceneLoadState` → `SceneLoadStateEnum` |
| `SceneLoader`の状態照会をQuery経由へ変更 | `SceneManagerConfig` → `SceneLoadConfig` |
| 公開の一覧／点検索スナップショットAPI | `SceneLoaderWindow` → `SceneLoadWindow` |
| `SceneLoaderWindow`の脱リフレクション・購読化 | 公開非同期APIの`Awaitable`化 |
| `SymphonyMcpTools`の公開Info利用 | Scene Blockの依存グラフと並列スケジューラ |
| Query／Info／ViewModelのEditModeテスト | 他サブシステムのQuery／ViewModel化 |

公開型の改名とシリアライズ済みConfigの移行はPhase 4（3.0.0）へ据え置く。

## 公開API

### `SceneLoadInfo`

Queryが追跡中Entityから生成する、取得時点の不変スナップショットを追加する。

```csharp
public readonly struct SceneLoadInfo : IEquatable<SceneLoadInfo>
{
    public string SceneName { get; }
    public SceneLoadState State { get; }
    public int Priority { get; }
    public float Progress { get; }
    public bool IsActive { get; }

    internal SceneLoadInfo(
        string sceneName,
        SceneLoadState state,
        int priority,
        float progress,
        bool isActive);
}
```

- public setterと状態変更メソッドを持たない
- EntityやRegistryを受け取らず、Queryが抽出した値だけを受け取る
- constructorは`internal`とし、利用側による実状態と無関係なInfo生成を許さない
- 値比較を可能にし、既定値同士を含む等値性を定義する
- `Progress`はEntityで正規化済みの0〜1を保持する

`SceneLoadRequest`はシーンロードの**入力**としてシーン名と希望優先度をまとめ、`SceneLoadInfo`はScene Loaderが管理する**現在の結果**として状態、実優先度、進捗、Active Scene判定をまとめる。両者は用途を混ぜない。

### `SceneLoader`の追加API

```csharp
public static IReadOnlyList<SceneLoadInfo> GetSceneInfos();

public static bool TryGetSceneInfo(
    string sceneName,
    out SceneLoadInfo sceneInfo);
```

- `GetSceneInfos`はScene名のordinal昇順で、変更不能なスナップショットを返す
- `TryGetSceneInfo`はnull、空、空白、未追跡のScene名に対してfalseと`default`を返す
- どちらも未初期化時は既存照会APIと同じ`SymphonyNotInitializedException`を送出する
- 既存`IsExist`と`TryGetState`は残し、実装だけを同じQueryへ転送する
- 既存の戻り値、例外、既定値、ロード／アンロード挙動は変更しない

公開操作は引き続き`SceneLoader`だけが持つ。`SceneLoadQuery`、`SceneLoadDto`、`SceneLoadViewModel`、ReactivePropertyは`internal`とする。

## 内部設計

### `SceneLoadQuery`

`Runtime/System/SceneLoader/Internal/Adaptor/SceneLoadQuery.cs`へ置く。constructorで`SceneLoadRegistry`を受け取り、次の副作用のない変換だけを行う。

```csharp
internal sealed class SceneLoadQuery
{
    internal bool TryGetInfo(string sceneName, out SceneLoadInfo sceneInfo);
    internal IReadOnlyList<SceneLoadInfo> GetInfos();
    internal IReadOnlyList<SceneLoadDto> GetDtos();
}
```

- Registry／Entityの変更、Unity API、I/O、event発行を行わない
- Active Scene名と各Entityを1回の同期処理でスナップショット化する
- 公開用InfoとView用Dtoの派生判定をここへ集約する
- `SceneLoader`と`SceneLoadViewModel`はRegistry／Entityを直接参照しない
- 一覧はScene名のordinal昇順に統一し、Dictionaryの列挙順へ依存しない

### `SceneLoadDto`

`Runtime/System/SceneLoader/Internal/Adaptor/SceneLoadDto.cs`へ置く内部`readonly struct`とする。ViewModelが一覧表示に必要とする`SceneName`、`State`、`Priority`、`Progress`、`IsActive`だけを保持し、値等値性を実装する。

InfoをDtoとして再利用しない。Infoは利用側へ公開するAdaptor契約、DtoはAdaptorからViewへ渡す内部更新値であり、ViewModelはInfoを参照しない。

### `SceneLoadViewModel`

`Runtime/System/SceneLoader/Internal/View/SceneLoadViewModel.cs`へ置き、`SceneLoadQuery`と`SceneLoadService`をconstructorで受け取る。

```csharp
internal sealed class SceneLoadViewModel : IDisposable
{
    internal IReadOnlyReactiveProperty<IReadOnlyList<SceneLoadDto>> Scenes { get; }
}
```

- 生成時に`SceneLoadService.OnStateChanged`を購読し、初期Dtoスナップショットを反映する
- 状態変更通知を受けるたびに同じQueryを呼び直す
- `ReactiveProperty<IReadOnlyList<SceneLoadDto>>`の変更可能な実体はViewModelだけが所有する
- 内容比較用comparerを使用し、同じDto列なら通知しない
- `Dispose`でService eventを購読解除し、ReactivePropertyも破棄する
- Command、Unity API、Editor API、Registry、Entity、Infoを参照しない

### Compositionとライフタイム

G1と同じく、このサブシステム内の暫定Composition Rootは`SceneLoader.Initialize`とする。

```text
SceneLoader.Initialize
  ├─ SceneLoadRegistry
  ├─ UnitySceneLoader
  ├─ SceneLoadService
  ├─ SceneLoadQuery
  └─ SceneLoadViewModel
```

`SceneLoader`はEditorが現在のViewModelを取得するための`internal static`な読み取り専用accessorを持つ。`ResetRuntimeState`ではViewModelを最初にDisposeしてService eventを解除し、その後RegistryをClearして全参照をnullへ戻す。Domain Reload無効で再初期化されても、古いViewModel、ReactiveProperty、購読、Dto一覧を残さない。

G1の診断用`TrackedScenes`と`ActiveSceneName` accessorは削除する。`IsInitialized`は`SymphonyMcpTools`の未初期化判定に必要なため残す。

## Editor Window

`SceneLoaderWindow`は`SceneLoader.CurrentViewModel`からViewModelを取得し、`Scenes.Subscribe(...)`の購読だけを所有する。

- `System.Reflection`、`FieldInfo`、`BindingFlags`を削除する
- `Dictionary<string, SceneLoadEntity>`とEntity参照を削除する
- Dtoの不変一覧を`ListView.itemsSource`へ設定し、通知時だけ`Rebuild`する
- 初期化時とPlay Mode遷移時に現在のViewModelへbindする
- `EnteredPlayMode`ではRuntime初期化完了後の`EditorApplication.delayCall`でもう一度bindし、callback順へ依存しない
- `ExitingPlayMode`／`EnteredEditMode`でReactivePropertyの購読を解除する
- `IDisposable`を実装し、`SymphonyAdministrator.OnDisable`から購読とEditor callbackを必ず解除する
- `SymphonyAdministrator.Update`から`SceneLoaderWindow.Update`の毎フレーム呼び出しを削除する。他の未移行Windowの更新は維持する

WindowはViewModelを生成・置換せず、Scene LoadのCommandも実行しない。

## MCP診断

`SymphonyMcpTools.GetSceneLoaderJson`は初期化済みの場合に`SceneLoader.GetSceneInfos()`を呼び、公開InfoだけからJSONを生成する。既存フィールド`name`、`state`、`priority`、`activeSceneName`は維持し、後方互換な診断値として`progress`と`isActive`を追加する。

`SceneLoadEntity`、`SceneLoadRegistry`、ViewModelのいずれも直接参照しない。未初期化時と例外時に必ず有効なJSONを返す既存契約も維持する。

## 依存方向

```text
利用側 ──> SceneLoader ──> SceneLoadQuery ──> SceneLoadRegistry ──> SceneLoadEntity
                 │                 │
                 │ Command         └──> SceneLoadInfo（公開スナップショット）
                 v
          SceneLoadService ──event──> SceneLoadViewModel
                                         ^
                                         │ SceneLoadDto / ReactiveProperty
                       SceneLoaderWindow ─┘

SymphonyMcpTools ──> SceneLoader.GetSceneInfos() ──> SceneLoadInfo
```

- ApplicationはQuery、Dto、ViewModel、ReactiveProperty、Editorを参照しない
- Queryは公開エントリポイントとViewModelを参照しない
- RuntimeのViewModelは`UnityEditor`を参照しない
- Editorは`InternalsVisibleTo`経由でViewModelとDtoを読むが、Domain EntityとRegistryを読まない

## ファイル構成

| パス | 変更 |
| --- | --- |
| `Runtime/System/SceneLoader/SceneLoadInfo.cs`（新規） | 公開の不変状態スナップショット |
| `Runtime/System/SceneLoader/SceneLoader.cs` | Query／ViewModelの生成・破棄、公開Info API、既存照会のQuery転送 |
| `Runtime/System/SceneLoader/Internal/Adaptor/SceneLoadQuery.cs`（新規） | Registry／EntityからInfo／Dtoを生成 |
| `Runtime/System/SceneLoader/Internal/Adaptor/SceneLoadDto.cs`（新規） | View向け不変更新値 |
| `Runtime/System/SceneLoader/Internal/View/SceneLoadViewModel.cs`（新規） | Service eventをReactivePropertyへ変換 |
| `Editor/Administrator/UITK/CS/SceneLoaderWindow.cs` | Reflection／Entity／ポーリングを除去しViewModel購読へ変更 |
| `Editor/Administrator/SymphonyAdministrator.cs` | Scene Windowの毎フレーム更新を削除しDisposeを追加 |
| `Editor/Debug/SymphonyMcpTools.cs` | 公開InfoからScene JSONを生成 |
| `Samples/Runtime/SceneLoaderSample/Scripts/SceneLoaderSample_Controller.cs` | 公開Infoによる状態、優先度、進捗、Active表示 |
| `Tests/Editor/SceneLoadInfoTests.cs`（新規） | 公開Infoの値、等値性、既定値 |
| `Tests/Editor/SceneLoadQueryTests.cs`（新規） | 点検索、一覧、順序、Active判定、スナップショット性、Dto |
| `Tests/Editor/SceneLoadViewModelTests.cs`（新規） | 初期値、event更新、内容同値時の非通知、Dispose |
| `Tests/Editor/SymphonyMcpToolsTests.cs` | Scene Loader JSONの追加フィールドと有効性を追従 |

新しい`.cs`とフォルダの`.meta`は手書きせずUnity Editorへ生成させる。既存公開型、MonoBehaviour、ScriptableObject、UXMLのGUIDは変更しない。

## エラー処理

- Queryはnull、空、空白のScene名を未検出として扱い、例外やログを発生させない
- Queryが返す一覧は空でもnullにしない
- ViewModelはQueryの同期結果だけを反映し、状態変更eventの例外を握りつぶさない
- WindowはPlay Mode外またはViewModel未生成時に空一覧を表示し、例外を投げない
- Windowを複数回bind／Disposeしても購読解除が冪等になるようにする
- 公開APIの未初期化例外は既存`SceneLoader`照会と統一する

## 影響範囲

後方互換な公開型と公開メソッドの追加であり、既存の公開シグネチャ、enum値、Config型／フィールド、Resourcesアセット、ロード／アンロード順序は変えない。

`SceneLoaderWindow`の更新タイミングは毎Editorフレームから状態変更時へ変わるが、表示する値の意味は維持し、進捗とActive判定を追加する。`SymphonyMcpTools`の既存JSONフィールドは維持する。

## テストと動作確認

### EditMode

- `SceneLoadInfo`: 全プロパティ、値等値、非等値、`default`
- `SceneLoadQuery`: 未登録／登録済み点検索、InfoとDtoの全値、Scene名順、Active Scene、取得済みスナップショットがEntity更新後に変わらないこと
- `SceneLoadViewModel`: 初期Dto、Service event後の通知、同内容で通知しないこと、Dispose後にService eventとの接続が切れること
- `SymphonyMcpTools`: 未初期化時の有効JSON契約を維持すること

### Unity確認

1. `uloop-compile`でError 0／Warning 0
2. EditModeとPlayModeの全テストが成功
3. Scene Loader Sampleで`SceneLoadRequest`からScene Aをロードし、AdministratorへScene名、状態、優先度、進捗、Active状態が反映される
4. Scene Aをアンロードすると一覧から消える
5. `GetSceneLoaderJson()`が既存フィールドに加えて`progress`と`isActive`を返す
6. Play Modeの開始・終了を2回繰り返し、2回目に前回の一覧や重複通知が残らない
7. `rg -n "System.Reflection|BindingFlags|FieldInfo|SceneLoadEntity|SceneLoadRegistry" Editor/Administrator/UITK/CS/SceneLoaderWindow.cs`が0件
8. `rg -n "TrackedScenes|ActiveSceneName" Editor/Debug/SymphonyMcpTools.cs Runtime/System/SceneLoader/SceneLoader.cs`で暫定accessor利用が0件

## バージョン判断

**マイナー（2.13.0）。** 既存契約を壊さず、公開`SceneLoadInfo`と`GetSceneInfos`／`TryGetSceneInfo`を追加するため。

更新対象:

- `package.json`: `2.12.0` → `2.13.0`
- `CHANGELOG.md`: 公開Info／Query／ViewModel、Editor脱リフレクション、テスト追加を記録
- `README.md`: Scene Loader節へ状態一覧／点検索の最小例を追加
- `Samples/Runtime/SceneLoaderSample/Scripts/SceneLoaderSample_Controller.cs`: 状態表示を`TryGetSceneInfo`の利用例へ変更
- `Documentation~/Architecture.md`: `SceneLoadInfo`と読み取り関係を追加し、G1/G2後の内部レイヤーを反映
- `Documentation~/AgentUsage.md`: 既知のScene名なしで状態を列挙する場合は`GetSceneInfos`を使う旨を追加

`AGENTS.md`は導線と常時ルールが変わらないため更新しない。

## GitHub Issue

2026-08-02時点のopen Issueを確認した結果、G2の完了条件と一致する独立Issueはない。

- Issue #104はQuery／ViewModelテストの追加により前進するが、「全機能」のテスト完備には達しないためcloseしない
- Issue #110はG1の`SceneLoadRequest`を入力単位として利用できるが、G2では依存グラフ、ScriptableObject、並列スケジューラを実装しないためcloseしない
- Issue #112はG1のPR #116で解決対象となっており、G2へ重複実装しない
- Issue #105、#106は公開名前空間／ディレクトリ移動を伴うため、非破壊のG2へ混ぜない

## ブランチとレビュー単位

PR #116は2026-08-02に`develop`へマージ済みである。`feature/scene-load-query-viewmodel`を最新の`origin/develop`から作り、G2だけを1commit・1PRとして`develop`へ提出する。

これにより、G1とG2を別commit・別PRとしてレビュー可能に保ち、G2の差分へG1の変更を重複表示しない。
