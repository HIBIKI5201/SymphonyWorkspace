# Symphony Framework 設計思想

このドキュメントは、Symphony Frameworkの設計判断に共通する価値観、依存方向、責務分割、初期化とライフサイクルの考え方を定義します。具体的な書式、配置、命名は [`CodeGuidelines.md`](./CodeGuidelines.md) を参照してください。

Symphony Frameworkは、利用側プロジェクトが依存する**配布用のUnityパッケージ**です。ゲーム本体の業務設計を持ち込まず、各サブシステムが守る規則とUnity固有の実装詳細を分離します。3.0.0へ向けた既存コードの移行順と互換性方針は [`Designs/ArchitectureRevision.md`](./Designs/ArchitectureRevision.md) に記録します。

## 目標

Symphony Frameworkは、次の性質を持つUnity向け基盤を目指します。

- プロジェクト規模が大きくなっても機能を分離して拡張できる。
- ゲーム仕様、保存先、入力元、表示方法などの変更へ柔軟に対応できる。
- 複数人が並行して変更しても、責務と影響範囲を判断しやすい。
- Unityの利便性を活かしながら、Unityライフサイクルへの過度な依存を避ける。
- 利用側プロジェクトへ特定のゲーム設計やデータ構造を強制しない。
- 公開APIとシリアライズ済みデータの互換性を、意図的な変更以外で壊さない。
- Unity API呼び出し、保存形式、内部状態などの実装詳細を利用側から隠蔽する。

## 採用する原則

設計判断では、次の原則を目的に応じて使用します。原則を適用すること自体を目的にはしません。

- Clean Architecture: 重要な規則をUnityや外部ライブラリの詳細から守る。
- Domain-Driven Design: 意味のある値と操作を型や共通語彙として表現する。
- SOLID: 変更理由を分け、抽象を介して依存方向を制御する。
- GRASP: 責務を、その情報と役割を最も自然に持つ型へ割り当てる。
- KISS: 将来予測だけを根拠に抽象や型を増やさない。
- Command-Query Separation: 状態変更と情報取得を区別する。
- Semantic Versioning: 公開APIとシリアライズ済みデータへの影響度に応じてバージョンを判断する。

設計が競合した場合は、正しさ、理解しやすさ、変更容易性、計測済みの性能要件の順で判断します。ただし公開APIの破壊的変更は、この優先順位より先に[バージョニング](#バージョニング)の規約へ従います。

## パッケージの依存方向

パッケージ全体では、依存を次の方向に限定します。

```text
Samples ───────┐
               v
Editor ────> Runtime ────> Core

利用側プロジェクト ────> Symphony Framework
```

- `Core` はRuntime、Editor、Samplesへ依存しない。
- `Runtime` はCoreへ依存できるが、Editorへ依存しない。
- `Editor` はCoreとRuntimeへ依存できる。
- `Samples` は公開されたRuntime APIだけを使用し、製品コードから参照されない。
- Symphony Frameworkから利用側プロジェクトの具象型を参照しない。
- 外部パッケージ固有の処理は境界へ寄せ、可能な場合は契約の背後へ隠す。
- asmdefで依存方向を表現し、循環参照を許可しない。

## 概念レイヤー

大きな機能を設計する場合は、次の概念レイヤーで責務を分けます。これは必ず同名のディレクトリを作る規則ではありません。機能が小さい場合は、責務が混ざらない範囲で型数を抑えます。

フレームワークで守る中心的な規則は、ゲーム固有の仕様ではなく、**サブシステム自身の不変条件**です。たとえば、同じサービスを二重登録しないこと、ロード中のシーンを二重解放しないこと、同一セーブ型のロードを競合させないことが該当します。

### レイヤー間の参照ルール

Domainを最も内側とし、Applicationをその外側に置きます。AdaptorとViewはUnityまたは利用側から処理を受け取る外側の入口です。Infrastructureは内側が定義する契約を実装し、Compositionが具象型を結合します。

```text
利用側コード ───────────────> Adaptor（公開エントリポイント・公開Component）
Unityライフサイクル ──> Adaptor / View
                              │
                              v
                         Application ──> Domain
                              ^
                              │ 契約を実装
Infrastructure（Unity API・I/O・Config）

Composition ──生成・注入・終了処理──> 各レイヤー
View（表示・診断）──────────────> Application / Domainの読み取り契約
```

- 内側のDomainとApplicationは、Adaptor、View、Infrastructure、Compositionの具象型を参照しない。
- Adaptorは利用側へ公開する操作面であり、処理をApplicationへ委譲する。
- Viewは表示と診断に限定し、状態変更はApplicationまたは公開エントリポイントへ委譲する。
- InfrastructureはUnity APIや外部I/Oを呼び、Applicationが定義する契約を実装する。
- Compositionだけが具象型の選択、生成、注入順を知る。
- 「参照してよい」は「必ず型を分ける」という意味ではない。小さな機能では、責務を保てる範囲で層に対応する型を省略してよい。

### Domain

機能の値、Entity、状態、不変条件、状態遷移を表します。

- 可能な限りピュアC#で実装する。
- `Vector2` などUnityの値型は利用できるが、`MonoBehaviour` の継承やScene上の存在を前提にしない。
- 外部I/O、サービス検索、Resources、Addressablesを参照しない。
- 不正な状態を作る経路を限定し、状態遷移の前後条件を明確にする。
- EntityはすべてDomainに置き、Unity APIや外部I/Oを参照させない。
- 例: `SceneLoadEntity`、`ServiceRegistrationEntity`、`SaveDataEntryEntity`、`SceneLoadState`、`LocateType`。

### Application

Domainの値と規則を組み合わせ、サブシステムが提供する処理を実行します。処理を担う`Service`と、状態を保持・検索する`Registry`はこの層に属します。

- `SceneLoadService`、`ServiceLocateService`のようなServiceは、入力検証、処理順、失敗時の復旧を担当する。
- `SceneLoadRegistry`、`ServiceLocateRegistry`のようなRegistryは、Domain Entityの登録、検索、更新、無効化を担当する。
- Unity API、ファイル、PlayerPrefs、Addressablesを直接呼ばない。必要な操作は契約として定義し、Infrastructureから注入する。
- 表示、GameObjectの所有、Unityコールバックを担当しない。
- 戻り値、例外、キャンセルの意味を公開契約と一致させる。

### Adaptor

利用側プロジェクトへ公開する唯一の操作面です。サービス名をそのまま型名にした静的な**公開エントリポイント**と、InspectorやUnityライフサイクルを公開契約として受ける`Component`が属します。

- `SceneLoader`、`AudioPlayer`、`PauseController`、`ServiceLocator`、`SaveStore`は、利用側がサブシステムを操作する入口になる。
- `ServiceInjector`のような補助エントリポイントは、主エントリポイントと機能が重複せず、責務を分ける理由が明確な場合だけ設ける。
- `ServiceLocateComponent`のような公開Componentは、Unityコールバックを受けて登録・解除などの処理をApplicationへ委譲する。
- CommandメソッドはApplicationのServiceを呼び、利用側向けの戻り値、例外、Try形式へ変換する。
- RegistryまたはEntityの管理状態を照会するQueryメソッドは、AdaptorのInfoへ変換して返す。Infoの抽出ロジックはInfo自身へ置ける。
- `ServiceLocator.GetInstance<T>`や`SaveStore.Get<T>`のように、登録された公開payload自体の取得が契約であるQueryはpayloadを返せる。ただし、その登録を管理するEntityは返さない。
- 公開エントリポイントは累積する業務状態を保持しない。
- 公開ComponentはUnityオブジェクトへの参照を保持できるが、サブシステムの規則を重複実装しない。
- Domainの不変な値やApplicationの読み取り結果を公開APIで直接受け渡してよい。

### View

表示、可視化、デバッグ、Editor UIなど、利用者または開発者へ状態を見せる表層機能を担当します。

- UGUI、UI Toolkit、デバッグHUD、EditorWindow、表示用のGameObjectを扱う。
- ViewModelはApplicationのQueryが生成したDTOを表示状態へ変換し、`IReadOnlyReactiveProperty<T>`としてViewとEditor UIへ公開する。
- Editor UIはViewModelのReactivePropertyを購読し、値の変更をポーリングせずに反映する。
- Unityライフサイクルは表示の開始・停止、購読・解除、描画更新の同期に使う。
- 状態変更を独自に実装せず、Applicationまたは公開エントリポイントへ委譲する。
- 表示のために状態を整形できるが、Domainの不変条件を持たない。
- `ServiceHostComponent`は、サービスの登録表ではなくGameObjectの所有と破棄を担うため、この層に置く。

### Infrastructure

永続化、外部パッケージ、Resources、Addressables、Unity API、**Config用ScriptableObject**などの技術的詳細を担当します。

- Scene操作は`SceneManager`を直接呼ぶ実装へ分離し、`SceneLoadService`へ注入する。
- `SaveDataLoader`の具象実装は保存形式や保存先へのI/Oを担当する。
- Configは利用側プロジェクトごとの設定値を保持し、Compositionが読み取る。
- Unityアセットや外部ライブラリの値を、内側が理解できる値へ変換する。
- ロードしたハンドル、ストリーム、購読などの所有者と解放方法を明確にする。
- 例: `JsonUtilitySaveDataLoader`、`NewtonsoftSaveDataLoader`、`PlayerPrefsSaveDataLoader`、`SceneLoadConfig`、`AudioConfig`。

### Composition

具象型を生成し、依存性注入、初期化順、公開、終了処理を担当します。

- Domain、Application、Adaptor、View、Infrastructureの具象型を結合する。
- 依存方向はOrchestratorから各レイヤーへの一方向にする。Runtimeの各レイヤーからOrchestratorを検索または呼び出してGameObject生成、永続化、初期化を依頼しない。
- Runtimeの`SymphonyOrchestrator`とEditorの`SymphonyEditorOrchestrator`を、それぞれのホストライフサイクルに対するComposition Rootとして扱う。
- `[RuntimeInitializeOnLoadMethod]`は`SymphonyOrchestrator`、`[InitializeOnLoad]`と`[InitializeOnLoadMethod]`は`SymphonyEditorOrchestrator`だけが所有する。サブシステムやInitializerへUnityの自動初期化属性を付けない。
- `SymphonyLifetimeComponent`はUnityライフサイクルとpackage-wideな`destroyCancellationToken`をRuntime Compositionへ提供するだけにする。
- `SymphonyOrchestrator`だけがpackage-wideな`destroyCancellationToken`へ1回登録し、キャンセル時に全サブシステムの`Shutdown`を逆順で実行する。各Service、Registry、公開エントリポイントは同じtokenへ終了処理を個別登録しない。
- Runtime用ViewModelを生成・所有し、Editor Windowへ内部参照を提供する。Play Mode終了時にViewModelを破棄する。
- Edit ModeでRuntime Compositionが存在しない場合、Editor Windowは未接続状態を表示し、Play Mode切り替え時にViewModelを再取得する。
- 他レイヤーへ具象型の組み立てやサービス登録を分散させない。
- 初期化に失敗した場合は、部分的に構築された状態を残さない。

## 依存性逆転

内側の規則から外側の詳細を利用したい場合は、内側に最小の契約を定義し、外側が実装します。Compositionが具象実装を注入します。

```text
Application ──定義──> ISaveDataLoader
                            ^
                            │ 実装
Infrastructure ─────────────┘
                            ^
                            │ 注入
Composition ────────────────┘
```

- interfaceは、実装交換、テスト境界、依存方向の逆転が実際に必要な場所へ作る。
- 将来使うかもしれないという理由だけで契約を追加しない。
- interfaceのメソッドは呼び出し側が必要とする最小単位にする。
- 具象型の詳細を引数や戻り値へ漏らさない。
- 利用側が実装を差し替える拡張点は、[利用側への非侵襲性](#利用側への非侵襲性)の対象として明示する。
- Config由来の値はCompositionが読み取り、必要な値だけを各層へ注入する。公開エントリポイントやServiceがConfigアセットを検索しない。

## クラス設計

### Orchestrator

OrchestratorはComposition層に属し、UnityまたはEditorのホストライフサイクルをフレームワークの明示的な初期化・終了フェーズへ変換します。

- Runtimeの`SymphonyOrchestrator`だけが`[RuntimeInitializeOnLoadMethod]`を持ち、BeforeSceneLoadとAfterSceneLoadの入口からRuntime初期化を進める。
- Editorの`SymphonyEditorOrchestrator`だけが`[InitializeOnLoad]`または`[InitializeOnLoadMethod]`を持ち、Config確認、enum生成監視、ログ、Editor用LoaderなどのEditorモジュールを初期化する。
- Unity属性を持つメソッドとstatic constructorは、対応するOrchestratorの通常メソッドを呼ぶだけの入口にする。
- `MenuItem`、`SettingsProvider`、`CustomEditor`、`UxmlElement`などUnityが型やfactoryを発見するための属性は自動初期化属性と区別し、そのcallback内でpackage-wideな初期化を行わない。
- package-wideな`destroyCancellationToken`、Editor終了、assembly reload、Play Mode遷移を集約し、同じ終了処理を複数経路から呼ばれても1回だけ実行する。
- `Uninitialized`、`Initializing`、`Ready`、`ShuttingDown`の状態を明示し、`Ready`以外では通常の公開操作を許可しない。
- Editor初期化中にAssetPostprocessorなどのhost callbackが再入した場合は、その場で再初期化せず変更をcoalesceして`Ready`後に1回処理する。
- 初期化したモジュールと`IDisposable`を順序付きで記録し、`Shutdown`では逆順に解放する。
- サブシステムのInitializer、Service、Registry、ViewModelには明示的な`Initialize`／`Shutdown`／`Dispose`を実装させ、Orchestratorからだけ実行する。
- Orchestratorを汎用FactoryやUnity helperとして公開しない。動的なUnityオブジェクト生成が必要なら、Compositionが生成済みComponentまたはInfrastructure契約を注入する。
- package-wideな終了callbackから呼ぶ`Shutdown`は同期的かつ非ブロッキングにし、非同期保存や完了待ちを開始しない。進行中処理のキャンセルと所有リソースの即時解放だけを行う。
- 1つのモジュールの終了処理が例外を出しても残りを逆順で解放し、最後にまとめて記録する。終了処理から例外を外部へ再送出しない。
- Runtime Orchestratorから`UnityEditor`を参照しない。Editor専用の属性とAPIは`SymphonyEditorOrchestrator`へ隔離する。

### 公開エントリポイント

公開エントリポイントはAdaptor層に属し、サブシステムの公開操作を集約します。型名には設計パターンの一律なサフィックスを付けず、利用側が認識するサービス名をそのまま使用します。

- 公開APIは原則としてサブシステムごとのstaticな公開エントリポイントへ集約する。
- Commandメソッドは同じ入力を対応するServiceへ転送し、Serviceの結果を利用側向けの戻り値、Try形式、明確な例外へ変換する。
- RegistryまたはEntityの管理状態を照会するQueryメソッドは、Infoの生成処理を呼び出す。
- 登録された公開payload自体を取得するQueryは、そのpayloadを返せるが、管理用Entityや内部コレクションを公開しない。
- Compositionから注入されたService、Registryを保持しているかという初期化状態は持ってよい。
- 累積状態とDomainの判断を持たない。
- 例: `SceneLoader`、`ServiceLocator`、`SaveStore`、`AudioPlayer`、`PauseController`。

### Service

ServiceはApplication層に属し、固定された処理順、入力検証、失敗時の復旧を担当します。

- 非staticにし、必要なRegistry、Strategy、Infrastructure契約をコンストラクタで受け取る。
- Entityの状態遷移を呼び出し、必要な副作用をInfrastructureへ依頼する。
- Commandによる観測可能な状態変更が確定するたびに、状態変更を表す標準C# eventを論理更新1回につき1回だけ発行する。開始、進捗、完了、失敗、キャンセルがEntityの状態へ反映された場合も通知対象とする。
- ViewModelやQueryを参照せず、eventの購読者を知らない。
- Unity API、表示状態、GameObjectの所有を持たない。
- 状態の保存と検索をRegistryへ委譲する。
- 具象型は原則として`internal`にする。
- 例: `SceneLoadService`、`ServiceLocateService`、`SaveDataService`。

### Strategy

StrategyはApplication層に属し、継承またはConfig選択によって差し替え可能な処理を表します。

- 利用側による継承、または複数の具象実装からの選択が実際に必要な場合だけ作る。
- 抽象基底型またはinterfaceとして、差し替えるアルゴリズムの最小契約を定義する。
- 状態の所有、Unity API呼び出し、具象依存の生成を行わない。
- ServiceがStrategyを実行し、Compositionが具象Strategyを選択して注入する。
- abstractであるだけの内部補助型へ`Strategy`サフィックスを付けない。
- 例: `SceneLoadStrategy`。

### Query

QueryはApplication層に属し、RegistryとDomain Entityを読み取ってViewModel向けDTOを生成します。

- 状態を変更せず、I/O、ログ出力、遅延初期化などの副作用を起こさない。
- ViewModelは初期化時と、Commandを実行したServiceの状態変更eventを受けた時にQueryを呼ぶ。
- QueryはDTOを戻り値として返し、ViewModelを直接参照しない。
- 公開API向けInfoは生成しない。Infoへの変換はAdaptorの公開エントリポイントが担当する。
- DTOが複数のViewModelで共通にならない限り、対象ViewModelに対応する最小の形にする。
- 例: `SceneLoadQuery`、`ServiceLocateQuery`、`SaveDataQuery`。

### Registry

RegistryはApplication層に属し、キーに対応するEntityの所有と検索を担当します。

- 型、ID、シーン識別子などのキーからEntityを登録、検索、除去する。
- Entityの生成、無効化、全消去の条件を明確にする。
- Entityの状態遷移や処理順を重複実装しない。
- I/Oを行わず、ロードや保存はServiceからLoaderへ依頼する。
- Registry自身はEntityやInfoを公開APIへ返さない。利用側向けの管理状態照会は、公開エントリポイントがRegistryまたはEntityからInfoを生成して返す。
- staticな公開Registryを作らない。利用側の入口は公開エントリポイントにする。
- 例: `SceneLoadRegistry`、`ServiceLocateRegistry`、`SaveDataRegistry`。

### Entity

EntityはDomain層に属し、識別子によってRegistryから検索され、ライフサイクルの中で状態が変化する参照型です。Entityはフレームワーク内部の登録単位として扱い、公開APIへ直接返しません。

- `class`で実装し、継承を拡張点にしない場合は`sealed`にする。
- 型、シーン識別子、登録IDなど、ライフサイクルを通して安定する同一性を持つ。
- 可変状態はprivate setterまたはprivate fieldへ保持し、状態遷移を表すメソッドからだけ変更する。
- RegistryがEntityの生成、登録、検索、除去を所有し、ServiceがEntityの操作を呼び出す。
- Unity API、外部I/O、公開エントリポイントを直接呼ばない。必要な副作用はServiceがInfrastructureへ依頼する。
- `AsyncOperation`、`Scene`、GameObjectの所有など、Unity固有の実装詳細をEntityへ保持しない。
- `ServiceRegistrationEntity`は登録対象を`object`として参照できるが、その実体がUnityオブジェクトでも検査、親子付け、破棄を行わない。
- 例: `SceneLoadEntity`、`ServiceRegistrationEntity`、`SaveDataEntryEntity`。

### Info

InfoはAdaptor層に属し、公開エントリポイントの管理状態を照会するQueryメソッドがEntityまたはRegistryの現在状態から生成して返す不変なスナップショットです。取得後に内部状態が変化しても、既に返したInfoの内容は変化しません。

- 原則として`readonly struct`で実装する。参照共有が必要な大きな値では、不変な`sealed class`を選択できる。
- Entityの識別子と、利用側が観測できる状態だけを含める。
- public setter、状態変更メソッド、Unity API、外部I/Oを持たない。
- Entityや内部の可変コレクション、変更可能な`SaveDataContent`などをプロパティから直接公開しない。
- Entityから必要な値を抽出し、公開用の派生値へ整形するロジックを持てる。ただしDomainの判断、状態変更、I/Oは行わない。
- 生成は公開エントリポイントが担当し、Entityを受け取るコンストラクタまたはfactory methodは`internal`にする。
- 例: `SceneLoadInfo`、`ServiceRegistrationInfo`、`SaveDataEntryInfo`。

### ViewModel

ViewModelはView層に属し、RuntimeまたはEditorの表示に必要な状態を保持します。ApplicationのQueryから受け取ったDTOを表示用の形へ変換し、変更を`ReactiveProperty<T>`として公開します。

- Entity、Registry、Infoを参照せず、ApplicationのQueryが返すDTOから表示状態を構築する。
- Commandを実行したServiceの状態変更eventだけを購読し、通知時にQueryを呼び直してReactivePropertyを更新する。
- 状態の更新権限はViewModel自身だけが持ち、ViewとEditor UIには`IReadOnlyReactiveProperty<T>`を公開する。
- Commandを実行しない。Runtime ViewやEditor Windowの操作は公開エントリポイントを直接呼ぶ。
- Runtime側に置くViewModelから`UnityEditor`を参照しない。EditorはRuntimeのViewModelを購読する。
- CompositionがViewModelを生成・所有し、Editor Windowは現在のViewModelへの購読だけを所有する。
- Composition Rootは現在のViewModelを取得する`internal`な読み取り専用accessorを提供する。Editor WindowはこのaccessorからPlay Modeごとに取得し、ViewModelを生成・置換しない。
- 購読とApplicationへの接続を所有し、`Dispose`で必ず解除する。
- Domain Reloadが無効でも、再初期化時に古い購読と値を残さない。
- 例: `SceneLoadViewModel`、`ServiceLocateViewModel`、`SaveDataViewModel`。

### ReactiveProperty

`ReactiveProperty<T>`は、値の変更を購読者へ通知するCoreの内部基盤です。ViewModelが変更可能な実体を所有し、ViewやEditor UIには読み取り専用契約だけを渡します。

- `ReactiveProperty<T>`と`IReadOnlyReactiveProperty<T>`を`Core/Internal/`へ置き、RuntimeとEditorから利用する。
- コンストラクタで`IEqualityComparer<T>`を任意に受け取る。未指定時だけ`EqualityComparer<T>.Default`を使用し、同値なら通知しない。
- 配列やListなど参照比較になる型には内容比較用comparerを指定し、通知後に内容が変化しないスナップショットを値にする。
- 購読は`IDisposable`を返し、購読した側がライフサイクル終了時に破棄する。
- 購読開始時に現在値を通知するかどうかをAPI引数で明示する。
- 通知と値の更新はメインスレッドに限定する。バックグラウンド処理はメインスレッドへ戻ってから更新する。
- `Dispose`後は購読をすべて解除し、更新と新規購読を拒否する。
- Unityのシリアライズ、永続化、グローバルなEvent Busとして使用しない。

### Component

ComponentはUnityライフサイクルを受ける`MonoBehaviour`または表示要素です。

- Inspector設定や利用側からの登録を受ける公開ComponentはAdaptorに置く。
- 表示、デバッグ、Editor連携のためのComponentはViewに置く。
- `Awake`、`OnEnable`、`OnDisable`、`OnDestroy`を同期点とし、処理をService、ViewModel、公開エントリポイントへ委譲する。
- 購読、登録、CancellationTokenSourceの生成と解除を対にする。
- UnityコールバックへApplicationの規則を書かない。
- 例: `ServiceLocateComponent`、`ServiceHostComponent`。

### Value Object

Value Objectは、境界を越えて受け渡す不変の値です。`readonly struct`だけでなく、不変なenumも含みます。

- 値の意味と制約が型名から読み取れるようにする。
- 生成時の検証が必要な値は、コンストラクタまたはFactoryに集約する。
- 値として比較する必要がある場合に限り`IEquatable<T>`を実装する。
- 順序に意味がある場合だけ`IComparable<T>`と比較演算子を実装する。
- 単位や制約が異なる値を、同じprimitive型のまま受け渡さない。
- 例: `SceneLoadState`、`LocateType`。

### DTO

DTOはApplicationのQueryが生成し、ViewModelへ渡す不変の更新データです。この用途に限定し、公開APIやInfrastructureとの汎用転送形式には使用しません。

- 原則として`readonly struct`を使用する。
- 同期呼び出しだけで使い、保持や非同期境界越えがない場合は`readonly ref struct`を使用できる。
- 状態変更ロジックや外部参照を持たせない。
- 対応するViewModelが必要とするデータだけを含める。
- 型名は対象 + `Dto`とし、`Data`だけで終わる曖昧な名前を付けない。
- Infoが利用側へ公開するAdaptor契約であるのに対し、DTOはApplicationからViewへの内部通知データとする。
- 例: `SceneLoadDto`、`ServiceLocateDto`、`SaveDataDto`。

### Loader

LoaderはInfrastructure層に属し、特定データの読み書き、変換、Unityリソース取得の手順を担当します。

- 契約は利用する内側の層、具象実装はInfrastructureへ置く。
- 外部I/Oの失敗、キャンセル、リソース解放を明確にする。
- 読み込んだ値をApplicationが理解できる形式へ変換する。
- 例: `SaveDataLoader`、`UnitySceneLoader`。

### Factory

Factoryは、依存解決や複数段階の生成規則を伴うオブジェクト構築を担当します。

- 単なる`new`の置き換えには作らない。
- 公開エントリポイントを検索せず、必要な依存を引数で受け取る。
- 生成後に必要な不変条件と所有権を保証する。
- 生成する型のレイヤーに合わせて配置する。

### Config

ConfigはInfrastructure層に属し、利用側プロジェクトごとのカスタマイズ値を保持します。

- DomainやApplicationから直接検索しない。Compositionが読み取り、必要な値だけを注入する。
- 動的な再読み込みが必要な場合は、Compositionが更新と再注入を統括する。
- `ScriptableObject`のシリアライズ値は外部から変更させず、読み取り専用プロパティを公開する。
- シリアライズ済みフィールドを変更する場合は、[バージョニング](#バージョニング)の規約に従う。

### Asset

AssetはInfrastructure層に属し、UnityのAuthoringデータをDomainやApplicationが理解できる値へ変換する入口です。

- Runtimeの可変状態をAssetへ書き戻さない。
- Assetから生成したRuntimeオブジェクトへ変更可能なシリアライズ状態を共有しない。
- 読み込みと変換に失敗した場合の扱いをLoaderまたはFactoryの契約として明示する。

### Initializer

InitializerはCompositionに属し、生成、依存性注入、登録、購読を順序立てて行います。

- Unityの自動初期化属性や副作用を持つstatic constructorを持たず、Orchestratorから明示的に呼ばれる。
- static field initializerからI/O、Unity API、イベント購読、Timer開始などの副作用を起こさない。
- 初期化フェーズと失敗時の巻き戻しを明確にする。
- 再初期化で二重登録や二重購読を起こさない。
- 終了処理を構築順の逆順で実行できるよう、生成した依存を記録する。

### Debugger

DebuggerはViewに属し、EditorまたはDevelopmentビルドで診断情報と操作を提供します。

- ViewModelの読み取り専用ReactivePropertyを購読し、DomainのEntityやApplicationのRegistryを直接監視しない。
- 製品状態を変更する場合は通常の公開エントリポイントを経由し、不変条件を迂回しない。
- Releaseビルドへ不要な状態収集や操作を含めない。

## CommandとQuery

- CommandはServiceが実行し、状態を変更して必要な場合だけ成功可否や結果を返す。
- Commandを実行したServiceは、観測可能な状態変更が確定するたびに状態変更eventを論理更新1回につき1回だけ発行する。複数の変更を1つの原子的更新として確定する場合は最後に1回だけ発行する。
- 失敗またはキャンセルをEntityの状態として確定した場合はeventを発行し、状態が何も変わらなかった場合は発行しない。
- ApplicationのQueryは状態を変更せず、RegistryとEntityからViewModel向けDTOを生成する。
- ViewModelは初期化時にQueryを1回実行し、その後はCommand側のeventを受けるたびにQueryを再実行する。
- 公開エントリポイントの管理状態QueryはRegistryまたはEntityからAdaptorのInfoを生成する。登録された公開payload自体を返す既存の取得契約は維持できるが、EntityとDTOを公開APIへ返さない。
- property getterでロード、保存、登録、ログ出力などの副作用を起こさない。
- `GetXxx`と`SetXxx`を機械的に作らず、利用者の意図を表す操作名を選ぶ。
- `TryXxx`は失敗が通常の分岐である場合に使い、例外の代替として乱用しない。

## 初期化ライフサイクル

複数の依存を持つ新しいシステムでは、初期化を次のフェーズへ分けます。Runtimeでは`SymphonyOrchestrator`、Editorでは`SymphonyEditorOrchestrator`がこのフェーズを全サブシステムに対するComposition Rootとして統括します。

```text
Init → ResourceLoadAsync → Build → Ready

Shutdown ← 登録と購読を逆順に解除
```

| フェーズ | 責務 |
| --- | --- |
| `Init` | 単体で完結する初期値と内部状態の準備 |
| `ResourceLoadAsync` | ファイル、Resources、Addressablesなどの非同期ロード |
| `Build` | 具象型の生成、依存性注入、サービス登録 |
| `Ready` | 他モジュールとの接続、イベント購読、利用開始 |
| `Shutdown` | 購読解除、登録解除、ハンドル解放、破棄 |

| ホストからの入口 | 所有者 | 実行内容 |
| --- | --- | --- |
| `RuntimeInitializeLoadType.BeforeSceneLoad` | `SymphonyOrchestrator` | stale stateの終了、Lifetime Component生成、`Init`から`Build` |
| `RuntimeInitializeLoadType.AfterSceneLoad` | `SymphonyOrchestrator` | 初期Sceneとの接続、`Ready` |
| `Application.quitting`またはpackage-wideな`destroyCancellationToken`のキャンセル | `SymphonyOrchestrator` | 同じRuntime `Shutdown`を多重呼び出し安全に実行 |
| Editor domain load／script reload後 | `SymphonyEditorOrchestrator` | Editorモジュールの`Init`、`Build`、`Ready` |
| assembly reload前／Editor終了 | `SymphonyEditorOrchestrator` | Editorモジュールの`Shutdown` |
| Play Mode状態変更 | `SymphonyEditorOrchestrator` | Editor Windowへ接続状態を通知し、Runtime ViewModelを再取得または切断 |

- モジュールは名前と実行順を持ち、同じフェーズを実行順に処理する。
- いずれかのフェーズが失敗した場合は後続フェーズを実行しない。
- `Shutdown`は構築順の逆順で実行する。
- 初期化途中で失敗した場合も、成功済みモジュールだけを逆順で終了してから失敗状態を公開する。
- Editorの初期化モジュールによるAsset変更はOrchestratorが集約し、`AssetDatabase.Refresh`を初期化の最終段階で必要な場合だけ1回実行する。Refresh後の同一domainで処理が続くことを前提にしない。
- Orchestratorは`Shutdown`の多重呼び出しを無害にし、終了開始後に新しい初期化や公開操作を受け付けない。
- 公開エントリポイントは未初期化時に遅延初期化せず、Try形式または`SymphonyNotInitializedException`で明示する。
- 各フェーズは繰り返し呼び出されても二重登録や二重解放を起こさないよう設計する。
- 単純な機能へ形式的な全フェーズを強制しない。状態と依存が複雑になった時点で導入する。
- Domain Reloadが無効でも、再初期化と終了処理が成立するようにする。

## 依存性注入とService Locator

- ピュアC#クラスのモジュール内依存には、コンストラクタ注入を第一候補とする。
- Inspector参照はAdaptor、View、CompositionなどUnity境界に限定する。
- `ServiceLocator`はCompositionで公開済みのサービスを利用側が取得する入口にする。
- DomainやApplicationの処理途中で公開エントリポイントを検索しない。
- 取得には可能な限り`TryGetInstance<T>`を使用し、初期化失敗を明示的に処理する。
- 登録した型と所有者を記録し、`Shutdown`または対応するUnityライフサイクルで登録解除する。
- 複数の依存は明示的な型やコンストラクタ引数としてまとめ、グローバルな検索対象を増やさない。

## Unityとの境界

UnityEngineの値型を使うことと、UnityライフサイクルやUnity API呼び出しへ依存することを区別します。

- DomainやApplicationで`Vector2`、`Color`などの値型を使うことは許容する。
- Unityライフサイクルの同期はAdaptorまたはViewのComponentが受け、処理をApplicationへ委譲する。
- フレームワーク全体の起動・終了を表すUnityコールバックとEditorコールバックはOrchestratorだけが受ける。個別Componentのローカルな表示・登録ライフサイクルとは区別する。
- `DefaultExecutionOrder`やScript Execution Orderをpackage-wideな初期化順の代替にしない。Orchestratorのフェーズとモジュール順で決定する。
- Scene、GameObject、PlayerPrefs、Addressablesなどの具象API呼び出しはInfrastructureへ置く。
- Unityコールバックにはサブシステムの規則を書かず、通常のC#メソッドを呼ぶ入口にする。
- `Awake`や`Start`の暗黙の実行順へ依存せず、Compositionが明示的に初期化する。
- 実行順が必要な場合は数値を散在させず、定数またはモジュールの`Order`で表現する。
- Domain Reloadが無効でも、static状態を明示的にリセットできるようにする。

## 非同期処理の型

Unity 6の`Awaitable`／`Awaitable<T>`を、内部・公開APIを問わずフレームワークの既定の非同期型とします。生成、合成、変換は`SymphonyAwaitable`へ集約します。

- 公開非同期APIも`Awaitable`／`Awaitable<T>`を返す。
- 完了済み値、複数処理の待機、タイムアウト、条件待機には`SymphonyAwaitable`を使う。
- `Awaitable`は原則1回だけawaitし、フィールド保存、再await、複数呼び出し元での共有をしない。
- 同じ進行中処理を複数の呼び出し元が待つ場合は、待機者ごとの`AwaitableCompletionSource`へ結果を配る。
- `AsyncOperation`は`Awaitable.FromAsyncOperation`で待つ。
- 外部ライブラリが`Task`を返す場合だけ境界で保持し、`SymphonyAwaitable`のブリッジで直ちに変換する。
- フレームワークの公開契約へ`Task`／`ValueTask`を漏らさない。例外は外部契約の実装に型が固定される場合だけとする。
- すべての中断可能な処理へ`CancellationToken`を伝播する。
- バックグラウンド処理後にUnity APIへ触れる前は、`Awaitable.MainThreadAsync`でメインスレッドへ戻る。
- キャンセル、失敗、正常な`false`を混同しない。

## 状態とリソースの所有権

すべての可変状態と解放が必要なリソースには、所有者を1つ決めます。

- 作成した側が、破棄または所有権移譲の責任を持つ。
- イベントを購読した側が購読解除する。
- Addressablesのhandleを保持した側がreleaseする。
- `CancellationTokenSource`を生成した側がdisposeする。
- サービスを登録したCompositionまたは公開Componentが登録解除する。
- Registryやcacheは、Entityの無効化とリセットのAPIを持つ。
- 所有権を移す場合は、型名、引数名、XMLドキュメントのいずれかで明示する。

## 公開APIとバージョニング

Symphony Frameworkは他プロジェクトが依存するパッケージであるため、公開APIとシリアライズ済みデータの変更は利用側の破壊につながります。変更前に影響範囲を判断します。

### 公開範囲

**サブシステムの操作はAdaptor層の公開エントリポイントまたは公開Componentから開始します。** 副作用を持つ操作、内部状態を変える操作、永続化、シーン遷移、登録解除などを、その他の公開型から直接起動できるようにしません。

`public`にしてよい型は次に限定します。それ以外は`internal`にします。

- Adaptor層の公開エントリポイント。例: `SceneLoader`、`ServiceLocator`、`SaveStore`、`AudioPlayer`、`PauseController`。
- Unity上で利用側が配置または生成するAdaptor層のComponent。例: `ServiceLocateComponent`。
- 利用側が実装・継承する契約。例: ApplicationのStrategy、`SaveDataLoader`、`SaveDataContent`、`IInitializeAsync`、`IPausable`。
- 公開エントリポイントの引数・戻り値として境界を越えるInfo、Value Object、enum。
- 回復方法の異なる失敗を通知する専用例外。
- 利用側が自身のフィールドや型へ付けるInspector属性。
- サブシステムに依存しない汎用ユーティリティ。例: `SymphonyAwaitable`、`SymphonyStringUtil`。

さらに、次の制約を適用します。

- 利用側が実装するメンバーは`protected abstract`、公開エントリポイントやServiceだけが駆動するメンバーは`internal`を基本とする。
- フレームワーク側だけが生成する不変値は、型を`public`、コンストラクタを`internal`にできる。
- Composition Root、Config、Domain Entity、Query、DTO、ViewModel、ReactiveProperty、内部Service、Registry、Infrastructureの具象実装は`internal`にする。Editorからの参照は`InternalsVisibleTo`で許可する。
- Editor専用の初期化、リセット、内部アクセサを公開APIへ広げず、`InternalsVisibleTo`で必要なアセンブリだけに許可する。
- 補助エントリポイントは主エントリポイントと操作が重複しない場合だけ認める。

### バージョニング

- `public`／`protected`メンバー、シグネチャ、既定値の意味、シリアライズ済み形式の破壊的変更はメジャー更新とする。
- 後方互換な公開API追加はマイナー、公開契約を変えない修正はパッチとして扱う。
- 既存APIを削除・変更する前に代替手段を用意し、可能な場合は`[Obsolete("代替APIの案内", error: false)]`の移行期間を設ける。
- `ScriptableObject`や`[Serializable]`データのフィールド名を変更する場合は`[FormerlySerializedAs]`を使用する。
- 保存形式を変更する場合は、既存データの既定値と移行手順を明示する。
- 公開APIの追加・変更・非推奨化は、README、Sample、XMLドキュメント、AGENTS.md、CHANGELOG、`package.json`を同じ変更内で更新する。
- `internal`／`private`だけの変更にバージョニング上の制約は課さない。

## 利用側への非侵襲性

フレームワークは、利用側プロジェクトのゲーム設計、データ構造、アーキテクチャを前提にしません。

- Domain、Application、Infrastructureの型は、利用側の具象クラス、enum、namespaceを知らない。
- 利用側が拡張する型は、抽象基底クラスまたはinterfaceとして公開し、実装すべき最小のメンバーだけを要求する。
- Configはフレームワークの動作調整だけに使い、利用側のゲームデータを表現する場所にしない。
- Sampleは公開APIで書ける動作例とし、内部APIへ依存させない。
- ログ、例外、Editorウィンドウを利用側固有のゲーム語彙へ依存させない。
- 特定ジャンルや入力デバイス向けの分岐はApplicationへ埋め込まず、設定または拡張点として切り出す。

## 開発コードの分離

- RuntimeにはPlayerビルドで必要なコードだけを含める。
- Editor拡張はEditor専用asmdefへ置く。
- デバッグ入力や検証用ComponentはDevelopment用asmdefへ分離し、Releaseビルドへ含めない。
- 技術検証や先行研究のデモは製品Runtimeから分離し、製品コードから参照しない。
- Sampleは公開APIの利用例として保ち、内部APIへ依存させない。

## 避ける設計

- 異なるレイヤーの責務を1つの型や`MonoBehaviour`へ集める。
- DomainやApplicationからScene、GameObject、PlayerPrefs、Addressablesを直接操作する。
- Applicationから公開エントリポイントを検索し、依存を隠す。
- RuntimeのAdaptor、View、InfrastructureからOrchestratorの生成helperを呼び、Compositionへ逆依存する。
- public setterで不変条件を迂回できる状態を公開する。
- property getterからロードやキャッシュ生成などの重い副作用を起こす。
- 将来必要になるかもしれないという理由だけでinterface、Factory、genericを追加する。
- 初期化順を`Awake`、`Start`、Script Execution Orderの偶然に依存させる。
- Orchestrator以外へ`InitializeOnLoad`系属性または`RuntimeInitializeOnLoadMethod`を置き、サブシステムを独自起動する。
- static constructorまたはstatic field initializerからI/O、Unity API、イベント購読、Timer、`delayCall`を開始してOrchestratorを迂回する。
- package-wideな`EditorApplication.update`、`delayCall`、Timerへ解除不能な匿名callbackを登録する。
- `DefaultExecutionOrder`やScript Execution Orderでフレームワーク全体の初期化順を作る。
- package-wideな`destroyCancellationToken`へ複数のServiceやRegistryが個別に終了処理を登録する。
- 終了callback内で非同期保存、ブロッキング待機、長時間I/Oを開始する。
- 登録、購読、ロードだけを実装し、解除、解放、キャンセルを実装しない。
- Editor、Development、SampleのコードをRuntimeへ混在させる。
- 合意なく公開APIやシリアライズ形式を破壊的に変更する。
- 利用側プロジェクトの具体的なゲーム設計やデータ構造をDomain／Applicationへ埋め込む。
- 役割を表さない`Manager`や`Data`を新しい型名へ使う。
- EntityをDomain以外へ置く、DTOをQueryからViewModelへの受け渡し以外に流用する。
- ViewModelからCommandを実行する、またはInfoをViewModelの更新データとして使う。
- `Awaitable`を保存、再await、複数の待機者で共有する。

## 設計判断のチェックリスト

新しい型や機能を追加する前に確認します。

- [ ] この型が変更される理由は1つか。
- [ ] 公開エントリポイントはサービス名をそのまま使用し、それ以外のclassはサフィックスで役割を表しているか。
- [ ] この責務は現在のレイヤーとディレクトリに属するか。
- [ ] Adaptorは利用側の公開面、Viewは表示・診断に限定されているか。
- [ ] UnityライフサイクルはAdaptor／Viewが受け、処理をApplicationへ委譲しているか。
- [ ] Unity API呼び出しをInfrastructureへ分離したか。
- [ ] Serviceの状態保持をRegistryへ分離したか。
- [ ] Registryが管理する個別要素に同一性とライフサイクルがある場合、Entityとして表現したか。
- [ ] EntityがDomainに置かれ、Unity APIや外部I/Oへ依存していないか。
- [ ] 公開エントリポイントの管理状態QueryがEntityや可変オブジェクトを返さず、不変なInfoを返しているか。
- [ ] 登録payloadを返すQueryが、payloadを管理するEntityや内部コレクションまで公開していないか。
- [ ] ApplicationのQueryがViewModel専用DTOを生成し、DTOを公開APIへ漏らしていないか。
- [ ] Commandを実行するServiceだけが状態変更eventを所有し、観測可能な論理更新の確定後に過不足なく発行しているか。
- [ ] CompositionがViewModelを所有し、Editor Windowは購読だけを所有しているか。
- [ ] コレクションを持つReactivePropertyへ内容比較用comparerを指定したか。
- [ ] 継承によって拡張可能なApplication型はStrategyとして、固定処理のServiceと区別されているか。
- [ ] 依存方向は内側の規則を外側の詳細から守っているか。
- [ ] Runtimeの各レイヤーがOrchestratorを呼ばず、必要なComponentやFactoryをCompositionから注入されているか。
- [ ] interfaceは実際の境界または交換理由を表しているか。
- [ ] 状態変更とQueryが分離されているか。
- [ ] 初期化、利用開始、終了の順序が明示されているか。
- [ ] Unity／Editorの自動初期化属性が対応するOrchestratorだけにあり、各モジュールをOrchestratorから明示的に実行しているか。
- [ ] static constructor、static field initializer、`DefaultExecutionOrder`、解除不能なEditor callbackがOrchestratorを迂回していないか。
- [ ] package-wideな`destroyCancellationToken`の登録先がRuntime Orchestratorだけになり、`Shutdown`が逆順かつ多重呼び出し可能になっているか。
- [ ] `Shutdown`が同期・非ブロッキングで、1モジュールの失敗後も残りの解放を継続するか。
- [ ] 登録、購読、handle、tokenの所有者と解放方法が決まっているか。
- [ ] `Awaitable`を保存、再await、共有していないか。
- [ ] 小さな問題に対して過剰な抽象化を導入していないか。
- [ ] 公開APIまたはシリアライズ形式を変更する場合、バージョニングと移行手段を決めたか。
- [ ] 公開にした型が[公開範囲](#公開範囲)のいずれかに該当するか。
