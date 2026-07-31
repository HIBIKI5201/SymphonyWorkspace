# Architecture Revision — レイヤー再定義・役割命名・Awaitable全面移行

## 目的

既存コードと設計語彙の乖離を解消し、各型の責務、Unity境界、公開面を型名と配置から判断できる構造へ移行します。同時に、フレームワーク内外の非同期契約をUnity 6の`Awaitable`／`Awaitable<T>`へ統一します。

この変更は公開型名と公開メソッドの戻り値を変更するため、**3.0.0の破壊的変更**として実施します。本書は実装順、互換性、移行手順を固定する設計書です。レイヤーの判断基準は [`DesignPhilosophy.md`](../DesignPhilosophy.md)、配置と命名は [`CodeGuidelines.md`](../CodeGuidelines.md) に従います。

## 対象範囲

- Runtimeの主要サブシステムを、Adaptor、Application、View、Infrastructure、Compositionの責務へ分割する。
- 操作を公開する型には利用側が認識するサービス名をそのまま使用し、内部の固定処理を`Service`、差し替え可能なApplication処理を`Strategy`、状態保持を`Registry`として命名する。
- Domainに、Registryが管理する同一性とライフサイクルを持つ登録単位`Entity`を置く。
- Adaptorに`Query`、公開API向け`Info`、ViewModel向け`Dto`を置き、RegistryとDomain Entityの読み取り変換をQueryへ一本化する。
- View層に`ViewModel`を置き、Coreの`ReactiveProperty<T>`を通してRuntimeとEditorへ表示状態の変更を通知する。
- Runtime／Editorの自動初期化属性、package-wideな終了通知、Play Mode遷移をOrchestratorへ集約する。
- Unityライフサイクルを受ける型を`Component`、Infrastructureのsealed具象I/O実装を`Loader`、利用側が継承する抽象拡張点を`Strategy`、複雑な生成を担う型を`Factory`として命名する。
- 曖昧な`Manager`と`Data`を新しい型名へ使用しない。
- RuntimeとEditorの非同期APIを`Awaitable`へ移行し、合成処理を`SymphonyAwaitable`へ集約する。
- 公開APIのうち旧契約を安全に転送できるものには段階的に`[Obsolete]`シムを用意し、3.0.0で旧シムを削除する。`SaveDataRegistry`のように意味まで変わる型はシムを置かず直接削除する。
- README、AGENTS.md、Samples、CHANGELOG、`package.json`はコード移行と同じ変更で更新する。

本書を作る段階ではコードを変更しません。実装時はsubmodule側で変更し、Unityによるコンパイルとサンプル確認を行います。

## 設計上の決定

### レイヤー

| レイヤー | 責務 | 代表型 |
| --- | --- | --- |
| Domain | Entity、Value Object、状態、状態遷移、Enum | `SceneLoadEntity`、`SceneLoadStateEnum`、`LocateTypeEnum` |
| Application | Command処理、Entityの保持と検索 | `SceneLoadService`、`SceneLoadRegistry` |
| Adaptor | 公開操作面、読み取り変換、Info、Dto | `SceneLoader`、`ServiceLocateComponent`、`SceneLoadQuery` |
| View | ViewModel、表示、診断、Editor UI、表示用GameObject | `SceneLoadViewModel`、`SymphonyDebugHUD`、`ServiceHostComponent` |
| Infrastructure | Unity API、外部I/O、Config | `JsonUtilitySaveDataLoader`、`SceneLoadConfig` |
| Composition | 生成、注入、初期化、終了 | `SymphonyOrchestrator`、`SymphonyEditorOrchestrator`、`SymphonyLifetimeComponent` |

Adaptorを公開面とし、Viewを表示・診断へ限定します。個別GameObjectのライフサイクルはAdaptorまたはView、フレームワーク全体の起動・終了はCompositionのOrchestratorが受け、サブシステムの処理はApplicationへ委譲します。

### 起動と終了の一元化

フレームワーク全体のホストライフサイクルはOrchestratorだけが所有します。RuntimeとEditorはasmdefと利用可能APIが異なるため、Runtimeの`SymphonyOrchestrator`とEditorの`SymphonyEditorOrchestrator`へ分けますが、各サブシステムはどちらの場合もOrchestratorから明示的に実行されます。

```text
[RuntimeInitializeOnLoadMethod]
          │
          v
SymphonyOrchestrator ──> Init ──> Build ──> Ready
          ^                                  │
          │ package-wide destroy token      │
          └──────────── Shutdown <───────────┘
                         逆順

[InitializeOnLoad / InitializeOnLoadMethod]
          │
          v
SymphonyEditorOrchestrator ──> Editor modules
          ^                         │
          └──── reload / quit ──────┘
                    Shutdown（逆順）
```

- `[RuntimeInitializeOnLoadMethod]`は`SymphonyOrchestrator`だけに置く。
- `[InitializeOnLoad]`と`[InitializeOnLoadMethod]`は`SymphonyEditorOrchestrator`だけに置く。
- `MenuItem`、`SettingsProvider`、`CustomEditor`、`UxmlElement`などの発見用属性は対象外とする。ただし、そのcallbackからpackage-wideな初期化を開始しない。
- `PackageInitializer`、`AutoEnumGenerator`、ログWriterなどは自動初期化属性と副作用を持つstatic constructorを失い、明示的な初期化・終了メソッドを持つEditorモジュールへ変更する。
- `AssetPostprocessor`は`MenuItem`などと同じUnity発見型なので禁止対象には含めない。ただし任意のタイミングで再入するhost callbackとして扱い、`OnPostprocessAllAssets`では`SymphonyEditorOrchestrator`へ変更種別を通知してcoalesceするだけにする。初期化、Asset生成、Refresh、購読の所有は行わない。
- `SymphonyLifetimeComponent`はpackage-wideな`destroyCancellationToken`を提供するだけとし、`SymphonyOrchestrator`がtokenへ1回だけ終了callbackを登録する。
- Runtimeの各サブシステムからOrchestratorを呼ばない。Orchestratorの`CreateSystemObject`や`PreserveObject`を汎用helperとして残さず、Build時の生成・永続化または注入したInfrastructure契約へ置き換える。
- `ServiceLocator`、`SceneLoader`、Save Dataなどはtokenへ`ResetRuntimeState`を個別登録せず、Orchestratorの`Shutdown`から逆順に`Shutdown`／`Dispose`される。
- `Shutdown`は状態遷移で多重実行を防ぎ、初期化途中の失敗、Play Mode終了、GameObject破棄のどの経路でも同じ終了手順を使う。
- Orchestratorは`Uninitialized`、`Initializing`、`Ready`、`ShuttingDown`を明示し、初期化失敗時は成功済みモジュールだけを逆順でrollbackする。
- Editor初期化中にAssetPostprocessorが再入した場合は再帰初期化せず、変更種別をcoalesceして`Ready`後に1回処理する。各初期化モジュールの`AssetDatabase.Refresh`は削除し、必要なRefreshをEditor Orchestratorの最終段階へ集約する。
- package-wideな終了callbackから呼ぶ`Shutdown`は同期・非ブロッキングとする。1モジュールの例外で中断せず残りを解放し、例外は最後にまとめて記録する。
- Domain Reloadなしで次のPlay Modeが始まった場合は、Orchestratorが残存状態を先に終了してから新しいRuntimeを構築する。
- `Application.quitting`、`AssemblyReloadEvents.beforeAssemblyReload`、`EditorApplication.playModeStateChanged`のpackage-wideな購読は対応するOrchestratorへ集約する。Editor WindowはOrchestratorが公開する内部の接続状態を購読する。

| 現在の分散入口 | 移行後 |
| --- | --- |
| `PackageInitializer` | 削除。`SymphonyEditorOrchestrator`が`SymphonyPackageInitializer`と`SymphonyConfigInitializer`を順に実行 |
| `AutoEnumGenerator` | 自動初期化属性を外したinternal Generatorとして残し、購読は`AutoEnumGenerationInitializer`が所有してEditor Orchestratorから開始・解除 |
| `TagsAndLayersPostProcessor` | internalなhost callbackとして残す。`InitializeOnLoadMethod`を削除し、asset変更をEditor Orchestratorへ通知。scene list購読は`AutoEnumGenerationInitializer`が所有 |
| `SymphonyDebugLogFileWriter` | 同名のinternal Editorモジュールとして残す。static初期化を削除し、Editor Orchestratorから`Initialize`／`Shutdown`を実行 |
| `SymphonyAssetProtector` | `SymphonyAssetProtectionPostProcessor` + `SymphonyAssetProtectionInitializer`。前者はhost callback、後者はnamed `delayCall`とメニュー状態を所有 |
| `SymphonyConfigManager` | `SymphonyConfigInitializer`。internalなEditor初期化モジュールとして3つのRuntime ConfigとEditor Configの存在を保証 |
| `SymphonyConfigLocator` | 同名のinternal Runtime Infrastructureとして維持。既存Configの検索だけを行い、生成と自動初期化は行わない |
| `SymphonyEditorConfigLocator` | 同名のinternal Editor Infrastructureとして維持。`ScriptableSingleton`の検索だけを行う |
| Runtime内の`SymphonyDebugHUD`の`MenuItem` | 属性と`UnityEditor`参照を削除し、Editor専用internal型`SymphonyDebugHudMenu`から`Show`／`Hide`を呼ぶ |
| `ServiceLocateData`の`[RuntimeInitializeOnLoadMethod]`と`Application.quitting` | Runtime Orchestratorの初期化・終了状態へ統合 |
| `ServiceLocator`、`SceneLoader`、`SaveSystem`のdestroy token登録 | Runtime Orchestratorの1つのtoken登録と逆順`Shutdown`へ統合 |
| `SymphonyLocate`と`SymphonyHUDDrawer`の`DefaultExecutionOrder` | 属性を削除し、公開Componentのローカルな登録は自身のライフサイクル、package-wideな生成順はRuntime Orchestratorで保証 |
| `SymphonyAdministrator`の`EditorApplication.update` | Runtime状態のポーリングを廃止してViewModel購読へ移行。Window固有の更新が残る場合だけOnEnable／OnDisableで所有 |
| `AudioManager`、`SymphonyDebugHUD`、`ServiceLocateData`からの`SymphonyOrchestrator`呼び出し | OrchestratorがBuild時にUnity Componentを生成・注入し、サブシステムからCompositionへの逆依存を削除 |

### `Manager`の分割

`Manager`は役割を示さないため、新規型名へ使用しません。既存の`Manager`が持つ責務は次の規則で分割します。

| 現在の責務 | 移行先 |
| --- | --- |
| 利用側へ操作を公開する | Adaptor / サービス名をそのまま使う公開エントリポイント |
| 処理順、不変条件、失敗時の復旧 | Application / `Service` |
| 継承またはConfig選択で差し替える処理 | Application / `Strategy` |
| 登録、検索、キャッシュ | Application / `Registry` |
| 同一性を持つ個別要素の状態とライフサイクル | Domain / `Entity` |
| 利用側へ返す現在状態のスナップショット | Adaptor契約 / `Info` |
| 公開InfoとViewModel向けDtoへの読み取り変換 | Adaptor / `Query`・`Info`・`Dto` |
| RuntimeとEditorへ公開する表示状態 | View / `ViewModel` |
| Scene、GameObject、PlayerPrefs、Addressablesの呼び出し | Infrastructure / `Loader`または技術名を持つ実装 |
| 個別GameObjectの所有、ローカルなUnityコールバック | AdaptorまたはView / `Component` |
| package-wideな起動・終了、Editorホストイベント | Composition / `Orchestrator` |
| 複数依存を伴う生成 | Composition / `Factory`またはInitializer |

### 役割サフィックス

公開エントリポイントだけは一律の役割サフィックスを付けず、利用側が認識するサービス名をそのまま使用します。それ以外の操作や状態を持つclassには役割サフィックスを必須とし、中心となるサフィックスは`Orchestrator`、`Service`、`Strategy`、`Query`、`Registry`、`Entity`、`Info`、`Dto`、`ViewModel`、`Component`、`Loader`、`Factory`です。型の種類自体が役割を表すclass／structには`Config`、`Exception`、`Attribute`、`Window`、`Drawer`、`State`、`Operation`、`Content`、`Utility`、`Tools`も既定サフィックスとして認めます。enumにはこれらを終端サフィックスとして使わず、例外なく`Enum`で終えます。

`Loader`、`Locator`、`Injector`、`Player`、`Controller`が公開サービス名の一部である場合は維持できます。既存の型名を機械的に置換せず、実際の責務を分割してから命名します。特に`Manager`を`Service`へ、`Data`を`Registry`へ単純置換してUnity APIやGameObject所有を残すことは禁止します。

### EntityとInfo

すべてのEntityをDomainへ置きます。Registryは生の可変値をDictionaryへ直接格納せず、同一性とライフサイクルを持つDomain Entityを管理します。ServiceはEntityの明示的な状態遷移を呼び、Registryは検索と所有に集中します。

| 内部Entity | 公開Info | 同一性 | 主な状態 |
| --- | --- | --- | --- |
| `SceneLoadEntity` | `SceneLoadInfo` | シーン識別子 | ロード状態、進捗、失敗情報 |
| `ServiceRegistrationEntity` | `ServiceRegistrationInfo` | 登録されたサービス型 | 登録方式、所有状態、登録状態 |
| `SaveDataEntryEntity` | `SaveDataEntryInfo` | セーブデータ型 | ロード状態、保存日時、処理状態 |

- EntityはDomainの`internal sealed class`を基本とし、Registryが生成、登録、検索、除去を所有する。
- EntityはUnity APIや外部I/Oを呼ばず、`AsyncOperation`、`Scene`、GameObjectを保持しない。
- `ServiceRegistrationEntity`は登録対象を不透明な`object`として保持できるが、Unityオブジェクトとしての操作と所有は`ServiceHostComponent`へ委譲する。
- InfoはAdaptorの`public readonly struct`を基本とし、AdaptorのQueryがRegistryまたはEntityから生成する。
- Registry／Entityから必要な値を抽出して派生値へ整形するロジックはQueryだけが持ち、InfoはQueryから渡された値を保持する。
- InfoはEntity、内部コレクション、変更可能な`SaveDataContent`への参照を公開しない。
- Infoのコンストラクタまたはfactory methodは`internal`にし、EntityではなくQueryが抽出した値だけを受け取る。

### ViewModelとReactiveProperty

ViewModelはView層に置き、AdaptorのQueryが生成したDtoをEditorやRuntime表示向けの状態へ変換します。Editor WindowはRegistryをポーリングせず、ViewModelが公開する読み取り専用ReactivePropertyを購読します。

```text
公開エントリポイント.Command ──> Application Service ──> Registry / Domain Entity
                                              │ 観測可能な状態変更確定後のevent
                                              v
                                          ViewModel
                                              │ View → Adaptor（Query呼び出し）
                                              v
Registry / Domain Entity ──読取──> Adaptor Query ──Dto──> ViewModel
                                      │
                                      └─Info──> 公開エントリポイント.管理状態照会

公開エントリポイント.管理状態照会 ──呼び出し──> Adaptor Query
```

| Query | Dto | ViewModel | 主なReactiveProperty |
| --- | --- | --- | --- |
| `SceneLoadQuery` | `SceneLoadDto` | `SceneLoadViewModel` | Scene一覧、状態、進捗、選択可能状態 |
| `ServiceLocateQuery` | `ServiceLocateDto` | `ServiceLocateViewModel` | 登録一覧、登録・解除状態 |
| `SaveDataQuery` | `SaveDataDto` | `SaveDataViewModel` | Entry一覧、ロード・保存・削除状態 |
| `PauseQuery` | `PauseDto` | `PauseViewModel` | ポーズ状態、操作可能状態 |

4つのQueryはいずれもAdaptorに属し、RegistryとEntityの読み取り結果をInfoまたはDtoへ変換します。公開エントリポイントとViewModelは同じQueryを共有し、独自にRegistryを読みません。

Coreには次の最小契約を追加します。

```csharp
internal interface IReadOnlyReactiveProperty<out T>
{
    T Value { get; }
    IDisposable Subscribe(Action<T> observer, bool notifyCurrent = true);
}

internal sealed class ReactiveProperty<T> : IReadOnlyReactiveProperty<T>, IDisposable
{
    public ReactiveProperty(T initialValue, IEqualityComparer<T> comparer = null);
    public T Value { get; }
    public bool SetValue(T value);
    public IDisposable Subscribe(Action<T> observer, bool notifyCurrent = true);
    public void Dispose();
}
```

- `ReactiveProperty<T>`と`IReadOnlyReactiveProperty<T>`は`Core/Internal/`へ置き、`InternalsVisibleTo`でRuntimeとEditorから利用する。
- Commandを実行するServiceだけが標準C#の状態変更eventを持ち、観測可能な状態変更の確定後に論理更新1回につき1回通知する。開始、進捗、完了、失敗、キャンセルをEntityの状態へ反映した場合も通知し、状態が変わらなければ通知しない。RegistryとQueryは変更eventを持たない。
- ViewModelはServiceのeventを受けてAdaptorのQueryを呼び、返されたDtoからReactivePropertyを更新する。ApplicationからQuery、ViewModel、ReactivePropertyを参照しない。
- ViewModelはCommandを実行しない。Runtime ViewとEditor Windowの操作は公開エントリポイントを直接呼ぶ。
- ViewModelだけが`SetValue`を呼び、ViewとEditorには`IReadOnlyReactiveProperty<T>`を返す。
- `IEqualityComparer<T>`をコンストラクタで任意に受け取り、未指定時だけ`EqualityComparer<T>.Default`を使用する。
- 配列やListには内容比較用comparerを指定し、通知後に変更されないスナップショットを値として渡す。
- 通知はメインスレッドで行い、バックグラウンド処理から更新する前に`Awaitable.MainThreadAsync`で戻る。
- `Subscribe`は解除用`IDisposable`を返す。Editor Windowの無効化、ViewModelの終了、Play Mode終了で必ず破棄する。
- CompositionがViewModelを生成・所有する。ViewModelは`IDisposable`を実装し、Service eventの購読と所有するReactivePropertyを解放する。
- Composition Rootは現在のViewModelを取得する`internal`な読み取り専用accessorを提供し、Editor WindowはPlay Modeごとにそこから取得する。
- Editor WindowはViewModelを所有せず、ReactivePropertyの購読だけを所有する。
- Edit ModeでRuntime Compositionが存在しない場合は未接続状態を表示し、Play Mode切り替え時にViewModelを再取得する。
- Domain Reloadなしの再生開始でも古いViewModelと購読を再利用しない。
- 高頻度の進捗更新では、値の同値判定と通知頻度を確認し、不要な配列コピーやUI再構築を避ける。

## 型名と責務の移行表

### 公開Runtime API

| 現在 | 移行後 | レイヤー・扱い |
| --- | --- | --- |
| `ServiceLocator` | `ServiceLocator` | Adaptor / 公開エントリポイント。名称維持 |
| `ServiceInjector` | `ServiceInjector` | Adaptor / 注入専用の補助エントリポイント。名称維持 |
| `SceneLoader` | `SceneLoader` | Adaptor / 公開エントリポイント。名称維持 |
| `SaveDataRegistry` | 削除（移行先は`SaveStore`） | public static旧APIを3.0.0で直接削除。`[Obsolete]`シムは置かない |
| `SaveDataLoader` | `SaveDataLoaderStrategy` | Application向けのpublic abstract拡張契約。非同期契約もAwaitableへ変更 |
| `PlayerPrefsSaveDataLoader` | `PlayerPrefsSaveDataLoaderStrategy` | public abstractな中間拡張点なのでStrategyを優先 |
| `AudioManager` | `AudioPlayer` | Adaptor / 公開エントリポイント |
| `PauseManager` | `PauseController` | Adaptor / 公開エントリポイント |
| `SymphonyLocate` | `ServiceLocateComponent` | Adaptor / Inspector設定と登録・解除の同期 |
| `SymphonyLocateObject<T>` | 削除 | キャッシュを持つ別経路を廃止し、`ServiceLocator`の取得APIへ統一 |
| `SymphonyTask` | `SymphonyAwaitable` | Utility / Awaitableの生成・合成・変換 |
| `SymphonyConfigLocator` | 同名・`internal`化 | Runtime Infrastructure / 既存Config検索専用。利用側からの直接利用を廃止 |

`SaveDataRegistry`はAPI名、非同期シグネチャ、`Get<T>()`の意味が同時に変わるため、安全な転送シムを構成できません。旧型は3.0.0で削除し、CHANGELOGのBreaking項目と移行ガイドに`SaveStore`への手順を記載します。

### Scene Load

| 現在 | 移行後 | 責務 |
| --- | --- | --- |
| `SceneLoadManager` | `SceneLoadService` | 処理順、入力検証、キャンセル、結果集約 |
| `SceneLoadManager`のUnity API部分 | `UnitySceneLoader` | `SceneManager`、`AsyncOperation`、Sceneルート取得 |
| `SceneLoadData` | `SceneLoadRegistry` + `SceneLoadEntity` | Registryは検索、Entityはシーン単位の状態とライフサイクルを担当 |
| `SceneLoadData.SceneInfo` | `SceneLoadInfo` | Adaptor QueryがEntityから生成する不変な公開読み取り値 |
| 新規 | `SceneLoadQuery` + `SceneLoadDto` | Adaptor / InfoとViewModel向けDtoを生成する唯一の読み取り変換 |
| `SceneResetter` | `SceneLoadService`へ統合 | Single相当の遷移をServiceの処理として統合 |
| `SceneManagerConfig` | `SceneLoadConfig` | Infrastructure / Scene設定 |
| `SceneManagerConfigDrawer` | `SceneLoadConfigDrawer` | Editor / Config表示 |
| `SceneLoaderWindow` | `SceneLoadWindow` | View / Editor UI |

`UnitySceneLoader`はApplicationが定義する最小契約を実装します。進捗集計は`SceneLoadRegistry`または副作用のない専用値へ分離し、`ValueTask<bool>[]`の状態ポーリングを再現しません。

### Service Locate

| 現在 | 移行後 | 責務 |
| --- | --- | --- |
| `ServiceLocateManager` | `ServiceLocateService` | 登録、解除、破棄の処理順と検証 |
| `ServiceLocateData`の辞書部分 | `ServiceLocateRegistry` + `ServiceRegistrationEntity` | Registryは型検索、Entityは登録単位の状態とライフサイクルを担当 |
| `ServiceLocateData`のGameObject部分 | `ServiceHostComponent` | Singleton登録したComponentの所有とTransform操作 |
| 新規 | `ServiceRegistrationInfo` | Adaptor Queryが生成する登録型、登録方式、登録状態の不変な公開スナップショット |
| 新規 | `ServiceLocateQuery` + `ServiceLocateDto` | Adaptor / InfoとViewModel向けDtoを生成する唯一の読み取り変換 |
| `ServiceLocatorWindow` | `ServiceLocateWindow` | View / Editor UI |

`ServiceLocateRegistry`は`UnityEngine.Object`を破棄せず、GameObjectも生成しません。`ServiceHostComponent`への親子付け、破棄済みObjectの判定、終了検知はUnity境界側へ移します。

### Save Data

| 現在 | 移行後 | 責務 |
| --- | --- | --- |
| `SaveDataRegistry`の公開static API | 削除。`SaveStore`へ移行 | 3.0.0でシムなし削除。公開操作面を新APIへ置換 |
| `SaveDataRegistry`の処理部分 | `SaveDataService` | ロード、保存、削除の順序と例外変換 |
| `SaveDataRegistry`のキャッシュ部分 | `SaveDataEntryRegistry` + `SaveDataEntryEntity` | Application Registryによる型別検索 + Domain Entityによるエントリ状態管理。旧公開名を再利用しない |
| `SaveDataRegistryEntryInfo` | `SaveDataEntryInfo` | Adaptor Queryが生成し、可変な`SaveDataContent`参照を除いた不変スナップショット |
| 新規 | `SaveDataQuery` + `SaveDataDto` | Adaptor / InfoとViewModel向けDtoを生成する唯一の読み取り変換 |
| `SaveDataLoader` | `SaveDataLoaderStrategy` | public abstractな拡張契約 |
| `PlayerPrefsSaveDataLoader` | `PlayerPrefsSaveDataLoaderStrategy` | public abstractなInfrastructure特化の中間Strategy |
| `JsonUtilitySaveDataLoader` | 同名維持 | Infrastructure / internal sealed具象Loader |
| `NewtonsoftSaveDataLoader` | 同名維持 | Infrastructure / internal sealed具象Loader |
| `SaveSystem` | `SaveDataInitializer` | Composition / Strategy解決と終了時リセット |
| `SaveSystemConfig` | `SaveDataConfig` | Infrastructure / Loader選択設定 |
| `SaveSystemSettingProvider` | `SaveDataSettingProvider` | Editor / Project Settings入口 |
| `SaveDataRegistryWindow` | `SaveDataWindow` | View / Editor UI |

内部の`SaveDataEntryRegistry`とDomainの`SaveDataEntryEntity`は`internal`にし、旧public型名`SaveDataRegistry`を内部へ再利用しません。`SaveDataQuery`がEntityから`SaveDataEntryInfo`と`SaveDataDto`を生成し、`DataType`、`SaveDate`、ロード状態などの値だけをコピーします。現在の`SaveDataRegistryEntryInfo.Data`のような変更可能な参照は公開しません。同期取得APIの扱いは[同期取得の廃止](#同期取得の廃止)で定めます。

### Audio、Pause、Composition、Config

| 現在 | 移行後 | 責務 |
| --- | --- | --- |
| `AudioManager`の処理・状態 | `AudioService` + `AudioRegistry` | Application / 再生判断と再生状態 |
| `AudioManager`のUnity API部分 | `UnityAudioComponent` | Infrastructure境界 / AudioSource操作 |
| `AudioManagerConfig` | `AudioConfig` | Infrastructure / Audio設定 |
| `AudioManagerConfig.AudioGroupSettings` | `AudioGroupConfig` | Infrastructure / グループ別設定値 |
| `AudioManagerConfigDrawer` | `AudioConfigDrawer` | Editor / Config表示 |
| `PauseManager`の処理・状態 | `PauseService` + `PauseRegistry` | Application / ポーズ変更と状態保持 |
| 新規 | `PauseQuery` + `PauseDto` | Adaptor / ViewModel向けDtoを生成する読み取り変換 |
| `SymphonyOrchestratorObject` | `SymphonyLifetimeComponent` | Composition / Unity終了トークンの供給 |

AudioSourceの生成方法は既存挙動を維持しつつ、Applicationから直接Unity APIへ触れない境界を作ります。Pauseの待機APIは`Awaitable`へ変更し、ポーズ状態の保持とイベント通知を分離します。

### 維持する型

次の型は名称が役割を表しており、この改訂を理由には変更しません。

- `SaveDataContent`。`SaveDataRegistryEntryInfo`はInfo規則へ合わせて`SaveDataEntryInfo`へ変更する。
- `JsonUtilitySaveDataLoader`、`NewtonsoftSaveDataLoader`など、利用側が継承しないinternal sealedの具象Loader。
- `SceneInitializationException`、`ServiceNotRegisteredException`、`SaveDataOperationException`、`SymphonyNotInitializedException`。
- Inspector用の各`Attribute`とEditor側の各`Drawer`。Config名に追従するDrawerだけは前表のとおり改名する。
- `SymphonyStringUtil`など、この改訂で責務が変わらない汎用型。将来の変更時には`Utility`サフィックスへの統一を別途判断する。

### Enumの改名

enumはValue Objectから分離し、型名を例外なく`Enum`で終えます。次の現行型は3.0.0で一括改名します。

| 現在 | 移行後 |
| --- | --- |
| `LocateType` | `LocateTypeEnum` |
| `SceneLoadState` | `SceneLoadStateEnum` |
| `SaveDataOperation` | `SaveDataOperationEnum` |
| `SymphonyDebugLogger.LogKind` | `SymphonyDebugLogger.LogKindEnum` |
| `SymphonyVisualElement.InitializeType` | `SymphonyVisualElement.InitializeTypeEnum` |
| `SymphonyVisualElement.LoadType` | `SymphonyVisualElement.LoadTypeEnum` |
| `AssetStoreToolsPackager.PackageMode` | `AssetStoreToolsPackager.PackageModeEnum` |

自動生成型の`SceneListEnum`、`TagsEnum`、`LayersEnum`、`AudioGroupTypeEnum`は既に規則へ適合しているため変更しません。

## 名前空間と配置

概念レイヤー名を名前空間へ追加せず、サブシステム単位の名前空間を維持します。公開範囲と責務はフォルダおよび型サフィックスで表します。

```text
Core/
└─ Internal/
   ├─ IReadOnlyReactiveProperty.cs
   └─ ReactiveProperty.cs
Runtime/
├─ System/
│  ├─ Audio/
│  │  ├─ AudioPlayer.cs
│  │  └─ Internal/
│  │     ├─ Application/
│  │     │  ├─ AudioService.cs
│  │     │  └─ AudioRegistry.cs
│  │     └─ Infrastructure/
│  │        └─ UnityAudioComponent.cs
│  ├─ Pause/
│  │  ├─ PauseController.cs
│  │  └─ Internal/
│  │     ├─ Application/
│  │     │  ├─ PauseService.cs
│  │     │  └─ PauseRegistry.cs
│  │     ├─ Adaptor/
│  │     │  ├─ PauseQuery.cs
│  │     │  └─ PauseDto.cs
│  │     └─ View/
│  │        └─ PauseViewModel.cs
│  ├─ SaveData/
│  │  ├─ SaveStore.cs
│  │  ├─ SaveDataLoaderStrategy.cs
│  │  ├─ PlayerPrefsSaveDataLoaderStrategy.cs
│  │  ├─ SaveDataEntryInfo.cs
│  │  └─ Internal/
│  │     ├─ Application/
│  │     │  ├─ SaveDataService.cs
│  │     │  └─ SaveDataEntryRegistry.cs
│  │     ├─ Adaptor/
│  │     │  ├─ SaveDataQuery.cs
│  │     │  └─ SaveDataDto.cs
│  │     ├─ Domain/
│  │     │  └─ SaveDataEntryEntity.cs
│  │     ├─ Infrastructure/
│  │     │  ├─ JsonUtilitySaveDataLoader.cs
│  │     │  └─ NewtonsoftSaveDataLoader.cs
│  │     ├─ Composition/
│  │     │  └─ SaveDataInitializer.cs
│  │     └─ View/
│  │        └─ SaveDataViewModel.cs
│  ├─ SceneLoad/
│  │  ├─ SceneLoader.cs
│  │  ├─ SceneLoadInfo.cs
│  │  └─ Internal/
│  │     ├─ Application/
│  │     │  ├─ SceneLoadService.cs
│  │     │  └─ SceneLoadRegistry.cs
│  │     ├─ Adaptor/
│  │     │  ├─ SceneLoadQuery.cs
│  │     │  └─ SceneLoadDto.cs
│  │     ├─ Domain/
│  │     │  └─ SceneLoadEntity.cs
│  │     ├─ Infrastructure/
│  │     │  └─ UnitySceneLoader.cs
│  │     └─ View/
│  │        └─ SceneLoadViewModel.cs
│  └─ ServiceLocate/
│     ├─ ServiceLocator.cs
│     ├─ ServiceInjector.cs
│     ├─ ServiceLocateComponent.cs
│     ├─ ServiceRegistrationInfo.cs
│     └─ Internal/
│        ├─ Application/
│        │  ├─ ServiceLocateService.cs
│        │  └─ ServiceLocateRegistry.cs
│        ├─ Adaptor/
│        │  ├─ ServiceLocateQuery.cs
│        │  └─ ServiceLocateDto.cs
│        ├─ Domain/
│        │  └─ ServiceRegistrationEntity.cs
│        └─ View/
│           ├─ ServiceHostComponent.cs
│           └─ ServiceLocateViewModel.cs
├─ Configs/Internal/
│  ├─ AudioConfig.cs
│  ├─ SaveDataConfig.cs
│  ├─ SceneLoadConfig.cs
│  └─ SymphonyConfigLocator.cs
├─ Orchestrator/Internal/
│  ├─ SymphonyOrchestrator.cs
│  └─ SymphonyLifetimeComponent.cs
└─ Utility/
   └─ SymphonyAwaitable.cs
Editor/
├─ Orchestrator/Internal/
│  └─ SymphonyEditorOrchestrator.cs
├─ Configs/Internal/
│  ├─ SymphonyConfigInitializer.cs
│  └─ SymphonyEditorConfigLocator.cs
├─ Generator/Internal/
│  ├─ AutoEnumGenerator.cs
│  └─ AutoEnumGenerationInitializer.cs
├─ AssetProtection/Internal/
│  ├─ SymphonyAssetProtectionPostProcessor.cs
│  └─ SymphonyAssetProtectionInitializer.cs
└─ Debug/Internal/
   ├─ SymphonyDebugLogFileWriter.cs
   └─ SymphonyDebugHudMenu.cs
```

`Internal/`は可視性だけを表すため、名前空間へ`Internal`を含めません。フォルダ移動時は`.meta`を対にして`git mv`し、GUIDを維持します。

## Awaitable全面移行

### 基本契約

- RuntimeとEditorの新しい非同期メソッドは`Awaitable`／`Awaitable<T>`を返す。
- 中断可能なpublic APIは`CancellationToken token = default`を受け取る。
- `Awaitable`をフィールドや共有Dictionaryへ保存しない。
- 同じ処理を複数待機者へ公開する場合は、各待機者に専用の完了ソースを作る。
- `Task`は外部ライブラリ境界でのみ受け取り、`SymphonyAwaitable`で変換する。
- 外部契約上`Task`を返す必要がある箇所だけ、Awaitable→Taskブリッジを使用する。

### `SymphonyAwaitable`のAPI

最低限、次のAPIを用意します。実装は完了ソースとCancellationToken登録の所有権を明確にし、完了・例外・キャンセルのいずれでも登録を解放します。

```csharp
public static Awaitable Completed();
public static Awaitable<T> FromResult<T>(T result);
public static Awaitable WhenAll(params Awaitable[] awaitables);
public static Awaitable<T[]> WhenAll<T>(params Awaitable<T>[] awaitables);
public static Awaitable WaitWhile(Func<bool> predicate, CancellationToken token = default);
public static Awaitable WithTimeout(
    Func<CancellationToken, Awaitable> operationFactory,
    TimeSpan timeout,
    CancellationToken token = default);
public static Awaitable<T> WithTimeout<T>(
    Func<CancellationToken, Awaitable<T>> operationFactory,
    TimeSpan timeout,
    CancellationToken token = default);
public static Awaitable<T> FromTask<T>(Task<T> task, CancellationToken token = default);
public static Awaitable FromTask(Task task, CancellationToken token = default);
public static Task AsTask(Awaitable awaitable, CancellationToken token = default);
public static Task<T> AsTask<T>(Awaitable<T> awaitable, CancellationToken token = default);
```

`Completed`と`FromResult`は呼び出しごとに新しいAwaitableを返します。`WhenAll`へ渡した各Awaitableを呼び出し側が別途awaitしてはいけません。`WhenAll`が唯一のconsumerになります。空配列、null要素、同期完了、処理途中の例外、キャンセル時の契約をXMLドキュメントへ明記します。

`WhenAny`は、未完了側のAwaitableと例外を安全に消費する一般契約を定められないため、具体的な利用要件が生じるまで実装しません。timeoutは開始済みAwaitableを受け取らず、linked tokenを渡せるoperation factoryを受け取ります。factoryは受け取ったtokenを下位処理へ伝播しなければならず、無視する処理を強制停止できない協調的timeoutとします。Taskブリッジに渡すtokenは待機だけをキャンセルし、元のTask自体をキャンセルしません。待機キャンセル後もブリッジが元のTaskの完了と例外を観測し、未観測例外を作らないようにします。

### `IInitializeAsync`

現在のinterfaceは`Task`をプロパティへ保存して再awaitし、完了後に`Task.CompletedTask`へ差し替えます。Awaitableは1回だけawaitするプール型であるため、この設計を移植しません。

```csharp
public interface IInitializeAsync
{
    Awaitable InitializeAsync(CancellationToken token = default);
}
```

- `InitializeTask`、`IsDone`、default実装の`DoInitialize()`を削除する。
- 初期化済み・初期化中の状態は呼び出し側の`SceneLoadService`が管理する。
- 同じ対象への重複初期化はServiceが拒否または待機者へ結果を配る。
- 利用側の実装は、渡されたtokenを下位処理へ伝播する。

### Service Locateの非同期待機

現在の`ServiceLocator.GetInstanceAsync<T>`は`TaskCompletionSource<T>`を使います。移行後は`ServiceLocateRegistry`が`ServiceRegistrationEntity`と待機者ごとの`AwaitableCompletionSource<T>`を所有します。

- すでに登録済みなら`SymphonyAwaitable.FromResult`で返す。
- 未登録なら待機者を型ごとのリストへ追加する。
- 登録時は各待機者を一度だけ完了し、リストから除去する。
- timeoutまたはcancel時は該当待機者だけを除去する。
- 1つのAwaitableを複数呼び出し元へ返さない。

### Save Dataの重複ロード

現在の`Dictionary<Type, Task>`は1つの進行中Taskを複数呼び出し元へ共有します。Awaitableで同じ構造を再現せず、型ごとのロード状態と待機者一覧へ分けます。

```text
最初のLoadAsync
  ├─ SaveDataEntryEntityをLoadingへ変更
  └─ ServiceがLoaderを1回だけ実行

後続のLoadAsync
  └─ 呼び出しごとの完了ソースを待機者一覧へ追加

完了
  ├─ SaveDataEntryEntityをLoadedまたはFailedへ変更
  └─ 全待機者へ同じ結果または例外を個別に通知
```

共有ロード本体は`SaveDataService`のライフタイムtokenで所有します。各呼び出し側のtokenは、その呼び出しに対応する待機者だけを一覧から除去してキャンセルし、ロード本体へ渡しません。全待機者がキャンセルされてもロード本体は継続し、完了結果を`SaveDataEntryEntity`へ反映します。ロード本体をキャンセルするのは`SaveDataService`のShutdownだけとします。これにより、最初の呼び出しや最後の待機者の都合が他の呼び出しとキャッシュ状態へ影響しません。

### 同期取得の廃止

現在の`SaveDataRegistry.Get<T>()`は未ロード時に`LoadAsync(...).GetAwaiter().GetResult()`を呼びます。PlayerLoopで進むAwaitableを同期ブロックすると完了不能になるため、暗黙の同期ロードを廃止します。

- `SaveStore.Get<T>()`は**ロード済みの値だけ**を返すQueryに変更する。未ロード時は`InvalidOperationException`を送出する。
- 通常の初回取得は`await SaveStore.LoadAsync<T>(token)`を使用し、ロードした値を戻り値で受け取る。
- 同期I/Oが必要なLoaderには、非同期処理を同期ブロックするのではなく、明示的な同期契約を別途定義する。ただし3.0.0の既定APIには追加しない。
- `SaveDataWindow`は3箇所の同期ブロックを削除し、`SaveDataViewModel`の処理状態を購読して関連ボタンを無効化する。Editor上でAwaitableを進めるために`EditorApplication.update`が必要な場合も、表示状態のポーリングには使用しない。

### `SaveDataLoaderStrategy`

現行のpublic abstract `SaveDataLoader`は`SaveDataLoaderStrategy`へ、public abstractな中間拡張点`PlayerPrefsSaveDataLoader`は`PlayerPrefsSaveDataLoaderStrategy`へ改名します。どちらも利用側が継承するApplicationの抽象拡張点であるため、I/Oを扱っていてもStrategy規則を優先します。Infrastructureのinternal sealed具象実装`JsonUtilitySaveDataLoader`と`NewtonsoftSaveDataLoader`だけがLoader名を維持します。

同時に`protected abstract ValueTask`を`protected abstract Awaitable`へ変更します。同期完了するPlayerPrefs向けStrategy実装は`SymphonyAwaitable.Completed()`または`FromResult(...)`を返します。

```csharp
protected abstract Awaitable<string> LoadJsonAsync(Type dataType, CancellationToken token);
protected abstract Awaitable SaveJsonAsync(Type dataType, string json, CancellationToken token);
protected abstract Awaitable DeleteCoreAsync(Type dataType, CancellationToken token);
```

これは利用側の派生Strategyをコンパイルエラーにする破壊的変更です。CHANGELOGとREADMEへ、基底型名、overrideの戻り値、同期完了ヘルパー、token伝播の変更例を最優先で掲載します。

### Scene Load

現在の実装には`Task.WhenAll`、`ValueTask<bool>[]`の`IsCompleted`／`Result`ポーリング、`AsyncOperation`の間接待機があります。

- `IInitializeAsync`の並列初期化は`SymphonyAwaitable.WhenAll`で待つ。
- Sceneのロードとアンロードは`Awaitable.FromAsyncOperation`で直接待つ。
- `UnitySceneLoader`は`AsyncOperation.progress`をApplicationの契約を通して`SceneLoadService`へ報告し、Serviceが`SceneLoadEntity`を更新して状態変更eventを発行する。進捗はQueryまたは`SceneLoadInfo`として読み取り、完了判定とUnity APIのポーリングを分離する。
- 結果配列の`IsCompleted`／`Result`は使用しない。各処理が結果を返し、合成メソッドが集約する。
- Unity API呼び出しは`UnitySceneLoader`へ置き、`SceneLoadService`は契約だけを参照する。

### `SymphonyVisualElement`

現在はコンストラクタで初期化を開始し、`InitializeTask`へ保存して後からawaitします。移行後は生成と非同期初期化を分けます。

- コンストラクタでは参照の設定だけを行う。
- `Awaitable InitializeAsync(CancellationToken token = default)`を明示的に呼ぶ。
- `InitializeTask`を削除する。
- Addressablesの`AsyncOperationHandle<T>.Task`は`SymphonyAwaitable.FromTask`で境界変換する。
- handleのrelease所有者をVisualElementまたはWindowのどちらか一方に固定する。

### Editor Package処理

`SymphonyPackageLoader`の`Task.WhenAll`と`Task.Yield`は、`SymphonyAwaitable.WhenAll`と`Awaitable.NextFrameAsync`へ移行します。Unity Package ManagerのRequestは完了フラグを次フレームごとに確認し、timeoutとエラーを明示的に処理します。

Editor APIが`Task`を要求する箇所だけ`AsTask`ブリッジを使用し、Runtimeの公開契約へ逆流させません。

### バックグラウンド処理

現在の`SymphonyTask.BackGroundThreadAction`と`BackGroundThreadActionAsync`は、処理後にメインスレッドへ戻りません。移行後の`SymphonyAwaitable`では次の順序を保証します。

```text
BackgroundThreadAsync
  → action実行
  → MainThreadAsync
  → Unityログまたは後続処理
```

戻り値を受け取れない`async void`版は削除し、呼び出し側がawaitできるAPIだけを公開します。

### その他の非同期シグネチャ

| 現在の箇所 | 移行方針 |
| --- | --- |
| `SceneLoader`の`ValueTask<bool>` | `SceneLoader`の`Awaitable<bool>`へ変更 |
| `SaveDataRegistry`の`ValueTask` | `SaveStore`／`SaveDataService`の`Awaitable`へ変更 |
| `PauseManager`の`Task` | `PauseController`の`Awaitable`へ変更 |
| `SymphonyTween`の`Task` | 全publicメソッドを`Awaitable`へ変更し、tokenをすべてのフレーム待機へ渡す |
| `SymphonyDebugHUD.AddText`の`ValueTask` | `Awaitable`へ変更 |
| `SymphonyLocateObject<T>.GetInstanceAsync`の`ValueTask<T>` | 型自体を削除し、`ServiceLocator.GetInstanceAsync<T>`の`Awaitable<T>`へ統一 |
| Obsolete配下の旧Save Loaderの`ValueTask` | 3.0.0で旧APIとともに削除 |
| `EnumGenerator`の`Task.Delay` | `EditorApplication.update`を使うEditor時間待ちへ変更し、メインスレッドを維持 |

変換後は`System.Threading.Tasks`の参照を全文検索し、外部契約とのブリッジに必要なファイルだけへ限定します。

## 公開API互換性

### `[Obsolete]`シム

型名変更は、可能なものから旧型を`Runtime/Obsolete/`へ移して新しい公開エントリポイントへ転送します。

- 旧公開型のメソッドは新しい公開エントリポイントへ転送し、旧状態を別に持たない。
- メッセージに新しい型名と最小の置換例を含める。
- 型名だけが変わりシグネチャが同じ段階では`error: false`にする。
- 戻り値が`Task`／`ValueTask`から`Awaitable`へ変わるメソッドは、旧シグネチャをブリッジで維持できる場合だけシムを用意する。
- single-awaitやキャンセルの意味を安全に維持できないAPIは、3.0.0のBreaking項目として直接削除する。
- 旧シムは新しい内部状態へ必ず転送し、二重キャッシュや二重イベントを作らない。

`SaveDataRegistry`はこの方針の例外です。API名と非同期シグネチャに加え、`Get<T>()`が「未ロードなら暗黙ロード」から「ロード済み値だけを返す」へ変わるため、旧契約を装う安全な転送ができません。3.0.0でシムなしに削除し、`SaveStore`への明示的な移行手順をCHANGELOGのBreaking項目と利用側向け移行ガイドへ記載します。

### シリアライズ互換性

- MonoBehaviourとScriptableObjectのファイル移動・改名では`.meta`を維持する。
- class名変更で既存SceneやPrefabの参照が切れないことをUnity上で確認する。
- シリアライズ済みフィールド名を変更する場合は`[FormerlySerializedAs]`を付ける。
- Config型の改名後も既存アセットが読み込まれ、Project Settingsから編集できることを確認する。

## 実装フェーズ

### Phase 1 — 文書の確定

- `DesignPhilosophy.md`を新レイヤーへ更新する。
- `CodeGuidelines.md`へ配置、役割サフィックス、Awaitable規則を反映する。
- 本設計書の移行表とブロッカーを現行コードに照合する。
- Adaptor Queryへの一本化、`SaveDataRegistry`の直接削除、StrategyとEnumの改名一覧をBreaking変更として確定する。

### Phase 2 — `SymphonyAwaitable`

- 完了済み値、`WhenAll`、operation factory方式のtimeout、条件待機、Taskブリッジを実装する。`WhenAny`は実装しない。
- utility単体で確認できるEditModeテストを追加する。
- 既存`SymphonyTask`の呼び出しを段階的に移す。

### Phase 3 — 内部型の分割

- Runtime／Editorの自動初期化属性を各Orchestratorへ集約し、既存モジュールのstatic初期化を明示的な`Initialize`／`Shutdown`へ変更する。
- `PackageInitializer`、`AutoEnumGenerator`、`SymphonyDebugLogFileWriter`、`TagsAndLayersPostProcessor`、`SymphonyAssetProtector`、`SymphonyConfigManager`を上記移行表のEditorモジュール／host callbackへ変更し、`SymphonyEditorOrchestrator`から開始・終了する。
- Runtimeの`SymphonyDebugHUD`から`UnityEditor`参照と`MenuItem`を除き、Editor専用`SymphonyDebugHudMenu`へ移す。
- `SymphonyAssetProtector`のstatic constructor／匿名delay callbackと、package-wideな`DefaultExecutionOrder`を除去する。
- `PackageInitializer`、Config生成、enum／asmdef生成に分散した起動時`AssetDatabase.Refresh`を集約し、Editor callbackの再入を防ぐ。
- `CreateSystemObject`／`PreserveObject`のサブシステム利用を除去し、Unity Component生成をCompositionまたは注入したInfrastructureへ移す。
- package-wideなdestroy token登録を`SymphonyOrchestrator`の1件へ集約し、各サブシステムの個別登録を削除する。
- Scene Load、Service Locate、Save Dataの順でDomain Entity、ApplicationのService／Registry、AdaptorのQuery／Info／Dto、Unity境界へ分割する。
- Audio、Pauseを同じ原則へ揃える。
- Coreへcomparerを注入できる`ReactiveProperty<T>`と読み取り専用契約を追加し、Composition所有のViewModelを構築する。
- 既存Editor Windowの状態取得をViewModel購読へ変更し、Window無効化時の購読解除を実装する。
- Compositionが新しい具象型を生成・注入するよう変更する。
- この段階では既存の公開型から新内部実装へ転送し、利用側APIを可能な限り維持する。

### Phase 4 — 公開型の改名

- 新しい公開エントリポイントと公開Componentを追加する。
- 安全に転送できる旧型へ`[Obsolete]`シムを付ける。
- `SaveDataLoaderStrategy`と`PlayerPrefsSaveDataLoaderStrategy`を追加し、利用側の継承例を新しい基底型へ変更する。
- 7つの現行enumを`Enum`サフィックスへ一括改名し、シリアライズ参照と利用側コードを更新する。自動生成済みの4型は変更しない。
- `SaveDataRegistry`はシムを作らず削除し、内部Registryには`SaveDataEntryRegistry`を使用する。
- README、AGENTS.md、Samplesを新APIへ変更する。
- ConfigとComponentの`.meta`、`FormerlySerializedAs`、既存Scene参照を確認する。

### Phase 5 — Awaitableシグネチャ

- `IInitializeAsync`、`SaveDataLoaderStrategy`、全公開エントリポイント、Tween、Pause、Debug HUD、Editor UIを移行する。
- 同期ブロック、保存された非同期値、共有された非同期値を全文検索で除去する。
- 利用側向けの移行例をREADMEとCHANGELOGへ記載する。

### Phase 6 — 3.0.0

- 移行期間を終えた旧シムを削除する。
- 旧`SaveDataRegistry`が存在せず、`SaveStore`への移行手順がREADMEとCHANGELOGのBreaking項目にあることを確認する。
- `SaveDataLoaderStrategy`系と7つのenum改名をCHANGELOGのBreaking項目へ列挙する。
- `package.json`を3.0.0へ更新する。
- CHANGELOGへBreaking項目と移行手順を記載する。
- submoduleをcommit・pushしてから親リポジトリのgitlinkを更新する。

## 利用側向け移行ガイド素案

| 2.x | 3.0.0 |
| --- | --- |
| `ServiceLocator.GetInstance<T>()` | 名称を維持。Awaitable移行箇所だけ書き換え |
| `ServiceInjector.Inject(target)` | 名称を維持 |
| `SceneLoader.LoadScene(...)` | `await SceneLoader.LoadSceneAsync(...)` |
| `SaveDataRegistry` | 3.0.0でシムなし削除。すべての呼び出しを`SaveStore`へ移す |
| `SaveDataRegistry.LoadAsync<T>()` | `await SaveStore.LoadAsync<T>()`。戻り値を初回取得に使用 |
| `SaveDataRegistry.Get<T>()`による暗黙ロード | 先に`await SaveStore.LoadAsync<T>()`し、`SaveStore.Get<T>()`はロード済み値の取得だけに使用 |
| `SaveDataRegistryEntryInfo` | `SaveDataEntryInfo`。可変な`Data`参照は削除し、必要な値をコピーしたスナップショットへ変更 |
| `SaveDataLoader`派生型 | `SaveDataLoaderStrategy`を継承し、overrideを`Awaitable`へ変更 |
| `PlayerPrefsSaveDataLoader`派生型 | `PlayerPrefsSaveDataLoaderStrategy`を継承し、JSON変換overrideを`Awaitable`へ変更 |
| `LocateType` | `LocateTypeEnum` |
| `SceneLoadState` | `SceneLoadStateEnum` |
| `SaveDataOperation` | `SaveDataOperationEnum` |
| `SymphonyDebugLogger.LogKind` | `SymphonyDebugLogger.LogKindEnum` |
| `SymphonyVisualElement.InitializeType` | `SymphonyVisualElement.InitializeTypeEnum` |
| `SymphonyVisualElement.LoadType` | `SymphonyVisualElement.LoadTypeEnum` |
| `AssetStoreToolsPackager.PackageMode` | `AssetStoreToolsPackager.PackageModeEnum` |
| `AudioManager` | `AudioPlayer` |
| `PauseManager` | `PauseController` |
| `SymphonyLocate` | `ServiceLocateComponent` |
| `Task`／`ValueTask`を返す独自Loader | `Awaitable`を返し、同期完了は`SymphonyAwaitable.Completed()`を使用 |
| `IInitializeAsync.InitializeTask` | 呼び出し側で状態を管理し、`InitializeAsync(token)`だけを実装 |

移行ガイドには、単なる検索置換で済まない`SaveDataRegistry`の削除、`SaveDataLoaderStrategy`派生型、`IInitializeAsync`実装を最初に載せます。`Awaitable`は同じ戻り値を2回awaitできないため、利用側が非同期値をフィールド保存している場合の書き換え例も示します。enumは上表の一括置換一覧と、自動生成4型は変更不要であることを併記します。

## 検証

### 文書段階

- `DesignPhilosophy.md`と`CodeGuidelines.md`のレイヤー、語彙、役割サフィックスが一致する。
- Entity、Info、Query、Dto、ViewModelの所属レイヤーと受け渡し方向が両文書で一致している。
- Markdownの見出しリンクと相対リンクが実在する。
- 移行表の「現在」欄にある型が、現行コードまたは移行前履歴に実在する。
- `.md`がUTF-8 BOMなし、LFである。
- `git diff --check`で空白エラーがない。

### 実装段階

- uLoopでRuntimeとEditorをコンパイルし、Errorと意図しないWarningがない。
- Scene Load、Service Locate、Save Data、Audio、Pauseの該当Sampleを確認する。
- Play Modeの開始・終了を2回繰り返し、Domain Reloadなしでもstatic状態と購読が残らない。
- `rg`で`InitializeOnLoad`系属性と`RuntimeInitializeOnLoadMethod`が対応するOrchestrator以外に存在しないことを確認する。
- 副作用を持つstatic constructor／static field initializer、package-wideな`DefaultExecutionOrder`、解除不能な匿名Editor callbackが残っていないことを確認する。
- Runtimeの各サブシステムから`SymphonyOrchestrator`への呼び出しが残っていないことを確認する。Editor Windowの読み取り専用ViewModel accessorだけを記録済み例外とする。
- package-wideなdestroy tokenのcallback登録がRuntime Orchestratorの1件だけで、終了時に各モジュールが構築順の逆順で1回ずつ終了することを確認する。
- 1モジュールの`Shutdown`を意図的に失敗させても残りが解放され、終了callbackが非同期処理やブロッキング待機を開始しないことを確認する。
- Editor初期化がAssetPostprocessorから再入せず、複数のAsset変更後もRefreshと再生成が必要回数にcoalesceされることを確認する。
- script reload前とEditor終了時にEditorモジュールが逆順で終了し、再ロード後に購読とTimerが重複しないことを確認する。
- 並行ロード、個別キャンセル、timeout、例外、同期完了の各経路を確認する。
- Editor Windowの処理中にUIが固まらず、同期ブロックがない。
- ViewModelの値変更がEditor Windowへ反映され、同値更新では再通知されない。
- コレクション用comparerにより、同一内容の別インスタンスでは再通知されず、内容変更時には通知される。
- Window無効化とPlay Mode終了後にReactivePropertyの購読が残らない。
- Edit Modeの未接続表示とPlay Mode切り替え後のViewModel再取得が正しく動作する。
- Config、Scene、Prefabの既存参照が維持される。
- `rg`で`GetAwaiter().GetResult()`、公開`Task`／`ValueTask`、`TaskCompletionSource`、`Task.WhenAll`、保存された`Awaitable`が残っていないことを確認する。外部契約の例外箇所は理由を記録する。

## 完了条件

- 利用側の操作入口がAdaptorの公開エントリポイントまたは公開Componentに限定されている。
- ApplicationのServiceがUnity APIとGameObject所有から分離されている。
- すべてのEntityがDomainにあり、Registryが検索と所有を担っている。
- AdaptorのQueryだけがDomain EntityまたはRegistryを読み取り、公開エントリポイント向けInfoまたはViewModel向けDtoへ変換し、登録payloadを返す場合も管理用Entityを公開していない。
- Commandを実行するServiceだけが、観測可能な論理更新の確定後に状態変更eventを過不足なく発行している。
- ViewModelがAdaptorのQueryから専用Dtoを受け取り、DtoをReactivePropertyへ反映している。
- ViewModelがCommandを実行せず、ViewとEditorへ読み取り専用ReactivePropertyだけを公開している。
- Domain Reloadなしの再初期化でもViewModelとReactivePropertyの購読が重複しない。
- Runtime／Editorの自動初期化属性が対応するOrchestratorだけにあり、サブシステムの初期化と終了がOrchestratorから実行されている。
- package-wideなstatic初期化、`DefaultExecutionOrder`、解除不能なEditor callbackが残っていない。
- Runtimeの各レイヤーからOrchestratorへの逆依存がなく、Unityオブジェクトの生成と永続化がCompositionまたはInfrastructureに限定されている。
- package-wideなdestroy tokenをRuntime Orchestratorだけが購読し、全サブシステムの`Shutdown`を逆順かつ1回だけ実行している。
- 新しい操作型に曖昧な`Manager`または`Data`名がない。
- 公開非同期APIが`Awaitable`へ統一され、single-await制約を破る共有がない。
- 3.0.0のREADME、AGENTS.md、Samples、CHANGELOG、`package.json`が実装と一致している。
- submoduleの変更がpush済みで、親リポジトリのgitlinkが到達可能なcommitを指している。
