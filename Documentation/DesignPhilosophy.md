# Symphony Framework 設計思想

このドキュメントは、Symphony Frameworkの設計判断に共通する価値観、依存方向、責務分割、初期化とライフサイクルの考え方を定義します。具体的な書式や命名は [`CodeGuidelines.md`](./CodeGuidelines.md) を参照してください。

Symphony Frameworkは、利用側プロジェクトが依存する**配布用のUnityパッケージ**です。同じClean Architecture／DDDの語彙を使う場合でも、業務ロジックを持つゲーム本体とは前提が異なります。本ドキュメントはその前提でレイヤーと語彙を再定義します。

主要サブシステム（`ServiceLocator`、`SceneLoader`、`SaveSystem` など）は、新しい設計思想に合わせて優先的に一括改修します。ただし公開APIとシリアライズ済みデータの破壊的変更は、[公開APIとバージョニング](#公開apiとバージョニング)の規約に従って行います。

## 目標

Symphony Frameworkは、次の性質を持つUnity向け基盤を目指します。

- プロジェクト規模が大きくなっても機能を分離して拡張できる。
- ゲーム仕様、保存先、入力元、表示方法などの変更へ柔軟に対応できる。
- 複数人が並行して変更しても、責務と影響範囲を判断しやすい。
- Unityの利便性を活かしながら、Unityライフサイクルへの過度な依存を避ける。
- 利用側プロジェクトへ特定のゲーム設計やデータ構造を強制しない。
- 公開APIとシリアライズ済みデータの互換性を、意図的な変更以外で壊さない。
- フレームワーク内部の実装詳細（Unity API呼び出し、保存形式、内部Manager）を、利用側から隠蔽する。

## 採用する原則

設計判断では、次の原則を目的に応じて使用します。原則を適用すること自体を目的にはしません。

- Clean Architecture: 重要なルールを外部の詳細から守る。フレームワークでは「重要なルール」の多くが、利用側プロジェクトを知らない公開契約（interface、DTO、Config）にあたる。
- Domain-Driven Design: 意味のある値と操作を型や語彙として表現する。
- SOLID: 変更理由を分け、抽象を介して依存方向を制御する。
- GRASP: 責務を適切な情報と役割を持つ型へ割り当てる。
- KISS: 将来予測だけを根拠に複雑な抽象を増やさない。
- Command-Query Separation: 状態変更と情報取得を区別する。
- MVVM: UIの状態とUnity上の表示を分離する必要がある場合に使用する。
- Semantic Versioning: 公開APIとシリアライズ済みデータへの影響度に応じてバージョンを判断する。

設計が競合した場合は、正しさ、理解しやすさ、変更容易性、計測済みの性能要件の順で判断します。ただし公開APIの破壊的変更は、この優先順位より先に[公開APIとバージョニング](#公開apiとバージョニング)の合意を必要とします。

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
- Symphony Frameworkから利用側プロジェクトの具象型を参照しない。矢印は常に一方向であり、利用側の型やnamespaceをフレームワーク側でif分岐・reflectionによって特別扱いしない。
- 外部パッケージ固有の処理は境界へ寄せ、可能な場合はinterfaceの背後へ隠す。
- asmdefで依存方向を表現し、循環参照を許可しない。

## 概念レイヤー

大きな機能を設計する場合は、次の概念レイヤーで責務を分けます。これは必ず同名のディレクトリを作る規則ではありません。機能の規模が小さい場合は、責務が混ざらない範囲で型数を抑えます。

フレームワークでは「ドメインルール」がゲーム仕様ではなく、**サブシステム自身が守る不変条件**（例: 同じ型を二重登録しない、ロード中のシーンを二重解放しない）を指します。各層の説明には、既存サブシステムでの対応例を併記します。

### レイヤー間の参照ルール

依存は常に外側から内側へのみ許可します。ただしSymphony Frameworkでは、外側の層が隣接する層だけを参照する厳密なOnion（KillChordで採用しているような、必ず1つ内側の層を経由する形）ではなく、**外側の層はそれより内側の任意の層を直接参照できる**、緩やかなClean Architectureを採用します。

```text
利用側プロジェクト
        │ 参照
        v
   View（静的Facade、Component）
        │ 参照
        ├────────────> Adaptor ──参照──> Application ──参照──> Domain
        │                  │                  ^
        │                  │ SubclassSelectorで │
        │                  │ 具象実装を選択      │
        │                  v                  │
        └──────────────────────────参照(Query／ValueObject)───┘

Composition ──生成・注入──> View, Adaptor, Application
Infrastructure（Config含む）──実装──> Domain／Applicationが定義するinterface
Composition ──Configを注入──> View
```

- Viewは、Domainの型安全なValueObjectやApplicationのQueryを直接参照してよい。DTOへ変換する必要性がない単純な参照まで、常にAdaptorを経由させない。
- Adaptorは、Applicationの実装を複数用意して切り替える必要が実際に生じた時点で導入する。それまではViewがApplicationを直接呼び出し、将来の切り替えに備えて先にAdaptorを用意しない（[避ける設計](#避ける設計)参照）。
- 切り替えが必要になった場合、Adaptorは `[SerializeReference, SubclassSelector]` などにより、利用するApplication実装を多態的に選択する置き場所になる。
- 内側の層（Domain、Application）は外側の層（Adaptor、View、Infrastructure、Composition）を参照しない。この方向だけは緩めない。
- 「参照してよい」は「常に参照すべき」ではない。単純な機能では、間の層を素通りさせず省略してよい（[KISS](#採用する原則)）。

### Domain

機能の値、状態、不変条件、状態遷移を表します。

- 可能な限りピュアC#で実装する。
- `Vector2` などUnityの値型は利用できるが、`MonoBehaviour` の継承やScene上の存在を前提にしない。
- 外部I/O、Service Locator、Resources、Addressablesを参照しない。
- 不正な状態を生成できないコンストラクタと操作を設計する。
- 例: `SceneLoadData`、`SaveDataRegistryEntryInfo`、`LocateType` が持つ状態と不変条件。

### Application

Domainを組み合わせ、利用者が実行したいユースケースを表します。1つのユースケースに対して複数の実装（例: 差し替え可能なロード戦略）を実際に用意する必要が生じた場合だけ、interfaceまたは抽象基底型として定義し、具象実装の選択をAdaptorへ委ねる。実装が1つの間はViewから直接呼び出す。

- DomainとApplication自身が定義する契約へ依存する。
- 保存、ロード、表示、Unityオブジェクト操作の具象実装へ直接依存しない。
- 入力を検証し、処理結果を戻り値として明確に返す。
- 例: `SceneLoadManager`、`ServiceLocateManager`、`SaveDataRegistry` の内部処理が表すユースケース。

### Adaptor

Viewと、Application・Domainの形式を相互変換し、**利用するApplication実装の選択**を担当します。この層は、実装を複数用意して切り替える必要が実際に生じるまで作りません。単一実装のサブシステムはAdaptorを持たず、ViewがApplicationを直接呼び出します。

- ControllerはCommandを受け、Applicationの処理を呼び出す。
- PresenterはQuery結果をView向けの形式へ変換する。
- 複数の実装を切り替え可能にしたいApplicationは、`[SerializeReference, SubclassSelector]` などによりAdaptorが多態的に選択・保持する。
- DTO、Output Port、View Modelのinterfaceなど、境界を越える契約を定義する。
- フレームワークの機能ルールを重複実装しない。
- 例: Editor拡張（Symphony Administrator）がRuntimeの状態をUITK向けに変換する部分。

### View

利用側プロジェクトおよびUnity（GameObject、UI Toolkit、UGUI、AudioSourceなど）と接する境界です。View層には性質が異なる2種類の型が属するため、参照ルールを分けます。

- **Facade**（静的公開クラス。例: `SceneLoader`、`AudioManager`、`PauseManager`、`ServiceLocator`、`SaveDataRegistry`）は、**利用側コードに公開する唯一の入口**です。DomainのValueObjectやApplicationのQueryなど、内側の任意の層を直接参照してよい（[レイヤー間の参照ルール](#レイヤー間の参照ルール)）。状態を持たない参照透過な実行を保ち、内部Managerの有無という状態以外は持ちません（[Facadeと内部Manager](#facadeと内部manager)参照）。
- **Component**（GameObject、UI Toolkit、UGUIなど、外部入力を受けて表示状態を保持するUnity上の型）は、Facadeより厳格な規則に従います。外部入力（ユーザー操作、他システムからのイベント）はAdaptorのControllerが受け取り、表示状態はAdaptorが所有するView Modelから読み取ります。ComponentはApplicationやDomainを直接参照せず、必ずView Model／Controller／Signalを経由します（[View Model、Signal、Spawner](#view-modelsignalspawner)参照）。
- Unityライフサイクルは表示と入力の開始・停止に限定する。
- 例（Facade）: `SceneLoader`、`ServiceLocator`。例（Component）: `SymphonyLocate`、`SymphonyDebugHUD` など、Inspectorから設定してシーンに置くComponent。

### Infrastructure

永続化、外部パッケージ、データベース、Resources、Addressables、**Config（設定用ScriptableObject）**などの技術的詳細を担当します。フレームワークにとっての「外部」は、`SceneManager`、`PlayerPrefs`、Addressables、Newtonsoft.Jsonなど**Unity・外部ライブラリのAPIそのもの**であることが多い。

- Applicationなど内側のレイヤーが定義したRepositoryやLoaderのinterfaceを実装する。
- Configアセットは、利用側プロジェクトごとのカスタマイズ値を保持する技術的詳細として扱い、Compositionが読み取ってView（Facade）やAdaptorへ注入する。
- Unityアセットを、内側のレイヤーが理解できる型へ変換する。
- ロードしたハンドル、ストリーム、購読などの所有者と解放方法を明確にする。
- 例: `JsonUtilitySaveDataLoader`、`NewtonsoftSaveDataLoader`、`PlayerPrefsSaveDataLoader` が実装する `SaveDataLoader`、および `SceneManagerConfig`、`AudioManagerConfig` などのConfigアセット。

### Composition

具象型を生成し、依存性注入、初期化順、公開、終了処理を担当します。

- Domain、Application、Adaptor、View、Infrastructureの具象型を結合する。
- `SymphonyOrchestrator` はパッケージ全体のComposition Rootとして扱う。
- 他レイヤーに具象型の組み立てやService Locator登録を分散させない。
- 初期化に失敗した場合は、部分的に構築された状態を残さない。

## 依存性逆転

内側のルールから外側の詳細を利用したい場合は、内側にinterfaceを定義し、外側が実装します。Compositionが具象実装を注入します。

```text
Application ──定義──> ISaveDataLoader
                           ^
                           │ 実装
Infrastructure ────────────┘
                           ^
                           │ 注入
Composition ───────────────┘
```

- interfaceは、実装を交換する必要、テスト境界、依存方向の逆転がある場所に作る。
- 実装が1つという理由だけでinterfaceを禁止しないが、将来使うかもしれないという理由だけでも追加しない。
- interfaceのメソッドは利用側が必要とする最小単位にする。
- 具象型の詳細をinterfaceの引数や戻り値へ漏らさない。
- 利用側プロジェクトが実装を差し替える拡張点（`SaveDataLoader` の継承など）は、[利用側への非侵襲性](#利用側への非侵襲性)の対象として明示する。
- ConfigはInfrastructureに属するため、View（Facade）が必要とするConfig由来の値は、Compositionが `Build` フェーズで注入する。ViewやAdaptorが直接Configアセットを検索しない。

## クラス設計

### Facadeと内部Manager

Facadeは、View層に属する、1つのサブシステムが公開する唯一の入口です。Application層のユースケースをまとめ、複数実装の切り替えが必要になった時点でAdaptorが選択した実装へ処理を委譲します。利用側（消費者）から見ると、これらFacadeクラスがSymphonyFrameWorkの「API」そのものです。Facadeは自身が属するサブシステムのフォルダ直下および同じサブシステムの名前空間に置き、内部Manager等の実装詳細は同じフォルダの`Internal/`配下へ分離します。名前空間ではなくフォルダで公開範囲を表すことで、Facadeとその引数・戻り値のValue Objectが利用側から1つの`using`で揃い、かつ公開されているAPIが一目で分かるようにします。

- 公開APIは原則としてstaticなFacadeクラス1つに集約する（例: `SceneLoader`、`AudioManager`、`PauseManager`、`ServiceLocator`、`SaveDataRegistry`）。View層に属するため、DomainのValueObjectやApplicationのQueryを直接受け渡ししてよい。サブシステムの機能はすべてこのFacadeを経由して呼び出し、他の公開型から同じ機能へ到達できる経路を作らない（[公開範囲](#公開範囲)）。
- 1つのFacadeへ統合すると責務が曖昧になる場合は、同じサブシステムに用途を限定した複数のFacadeを設けてよい。`ServiceInjector` は注入操作だけを提供する補助Facadeとして、`ServiceLocator` から分離したまま公開する。`SceneLoader` は、ロードしたシーンのルートオブジェクトが `IInjectable` を実装している場合、`ServiceInjector` を介して自動的に注入する。手動での `ServiceInjector.Inject(...)` 呼び出しは、シーンロードを経由しない生成（実行時Instantiateなど）向けに引き続き公開する。
- Facadeのメソッドは参照透過な実行にする。同じ入力に対して常に同じ内部Managerの対応する処理へ転送し、Facade自身が分岐ロジックや累積するビジネス状態を持つことは避ける。
- ただし、Compositionが `Build` フェーズで注入する協力者（内部Manager、サービスインスタンス）を保持しているかどうかという状態は、Facadeが持つことを許可する。未初期化判定や `TryXxx` はこの状態に基づく。
- 実処理と業務状態は非staticな内部Manager（例: `SceneLoadManager`、`ServiceLocateManager`）に置く。内部Managerが複数実装を持つ場合だけAdaptorを介して選択し、単一実装の間はFacadeが内部Managerを直接呼び出す。
- Facadeのメソッドは、内部Managerの例外や状態を利用者向けの形（戻り値、Try pattern、明確な例外）へ変換する。
- 内部Managerは[公開範囲](#公開範囲)に従い、原則`internal`にする。

### Entity

Entityは、同一性とライフサイクルを持つ可変の参照型です。

- `class` で実装する。
- 状態は読み取り専用プロパティとして公開する。
- public setterを設けず、`ChangeXxx`、`RecordXxx` など意図を表すメソッドで変更する。
- 変更メソッド内で不変条件を守る。

```csharp
public sealed class LoadOperationEntity
{
    public LoadOperationEntity(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException("IDを指定してください。", nameof(id));
        }

        Id = id;
    }

    public string Id { get; }
    public float Progress { get; private set; }

    public void ReportProgress(float progress)
    {
        Progress = Mathf.Clamp01(progress);
    }
}
```

### Value Object

Value Objectは、値そのものに意味があり、不変である型です。

- 原則として `readonly struct` で実装する。
- コンストラクタで範囲、null、有限値などを検証する。
- 値として比較する必要がある場合は `IEquatable<T>` を実装する。
- 順序に意味がある場合だけ `IComparable<T>` と比較演算子を実装する。
- 単位や制約が異なる値を、同じprimitive型のまま受け渡さない。

### DTO

DTOは、境界を越えて更新データを渡す不変の値です。

- 原則として `readonly struct` を使用する。
- 同期呼び出しの一時データで、ヒープ保持や非同期境界を越えないことが保証される場合は `readonly ref struct` を使用できる。
- DTOへロジックや外部参照を持たせない。
- Viewへ渡す場合は、必要な表示データだけを含める。

### RepositoryとLoader

- Repositoryは、呼び出し側にとってのデータ集合へのアクセスを表す。
- Loaderは、特定データの読み書きや変換手順を表す。フレームワークでは、利用側が差し替える拡張点になることが多い（例: `SaveDataLoader` 継承）。
- 契約は利用する内側のレイヤー、具象実装はInfrastructureへ置く。
- キャッシュする場合は、生成、更新、無効化、解放の条件をAPIとして明確にする。

### Use Case、Service、Factory

- Use Caseは、利用者が達成したい1つの操作をApplication上で表す。Facadeの各publicメソッドは、原則として1つのUse Caseに対応する。
- Serviceは、1つのEntityへ自然に属さないDomainまたはApplicationの処理を担当する。
- Factoryは、生成手順や不変条件が複雑なDomain／Applicationオブジェクトを構築する。
- Factoryを単なる `new` の置き換えにせず、生成ルール、実装選択、依存解決がある場合に使用する。
- これらの型からViewやInfrastructureの具象型を生成しない。

### ConfigとAsset

- ConfigはInfrastructure層に属する。表示や技術的機能だけで完結する設定に使用し、フレームワークでは**利用側プロジェクトごとの唯一のカスタマイズ入口**になることが多い。
- ConfigはDomainやApplicationから直接検索しない。Compositionが読み取り、必要な値だけをView（Facade）またはAdaptorへ注入する。
- Configの動的な再読み込みが必要な場合も、FacadeがConfigを直接検索しない。CompositionがResolverを注入し、Facadeはキャッシュ無効化後にResolverを再実行する。
- Assetは、UnityのAuthoringデータをDomainやApplicationの型へ変換する入口に使用する。
- `ScriptableObject` のシリアライズ値は外部から変更させず、読み取り専用プロパティを公開する。
- 種類が増えるデータには抽象基底型とfactory methodを用意し、`[SerializeReference, SubclassSelector]` による選択を検討する。
- Assetから生成したRuntimeオブジェクトへ、変更可能なシリアライズ状態を共有しない。
- Configのフィールドを追加・変更する場合は、既存アセットの読み込み互換性を確認する（[公開APIとバージョニング](#公開apiとバージョニング)参照）。

### PresenterとController

- Presenterは状態を問い合わせ、View Model向けのデータへ変換する。原則として状態を変更しない。
- **Controllerは、Component（View）から転送された外部入力（ユーザー操作、他システムからのイベント）を受け取る唯一の窓口です。** 受け取った入力をCommandとしてApplicationへ渡し、状態変更を開始する。
- FacadeはDomainのValueObjectやApplicationのQueryを直接受け取ってよいが（[レイヤー間の参照ルール](#レイヤー間の参照ルール)）、Component（View）はこの緩和ルールの対象外とし、必ずView Model（読み取り）とController／Signal（入力）を介する（[View](#view)参照）。

### View Model、Signal、Spawner

- **View Modelの実体はAdaptorが所有します。** UIに表示する状態を保持し、PresenterからDTOを受け取って更新する。ComponentはView Modelを読み取るだけで、自ら状態を持たない。
- DTOを同期的に参照するだけの場合は、コピーを避ける必要性を確認した上で `in` 引数を使用できる。
- SignalはComponent側の入力や表示イベントを境界の外（Controller）へ通知する。グローバルなEvent Busとして使用しない。
- SpawnerはViewオブジェクトの生成手順を担当し、FactoryとしてPrefab、親Transform、初期表示を組み立てる。
- View ModelとSignalの購読は、Componentの有効期間と対になる解除処理を持つ。

### RegistryとContainer

- Registryは、型やIDに対応するオブジェクトを検索する責務だけを持つ（例: `ServiceLocator` 内部の登録テーブル、`SaveDataRegistry` のキャッシュ）。
- Containerは、1つの機能モジュールが外部へ公開するサービス群をまとめる。
- Containerのプロパティは読み取り専用にし、構築後に依存を差し替えない。
- 巨大なService Locator代替としてContainerへ無関係なサービスを集めない。

### InitializerとDebugger

- InitializerはCompositionに置き、生成、依存性注入、登録、購読を順序立てて行う。
- Debuggerは製品ロジックから分離し、EditorまたはDevelopビルドでだけ診断情報と操作を提供する。
- Debuggerを通して製品状態を変更する場合も、通常のCommandや公開APIを経由し、不変条件を迂回しない。

## CommandとQuery

- Queryは状態を返し、観測可能な状態変更を行わない。
- Commandは状態を変更し、必要な場合だけ成功可否や結果を返す。
- property getterでロード、保存、登録、ログ出力などの副作用を起こさない。
- `GetXxx` と `SetXxx` を機械的に作らず、利用者の意図を表す操作名を選ぶ。
- `TryXxx` は失敗が通常の分岐である場合に使い、例外の代替として乱用しない。

## 初期化ライフサイクル

複数の依存を持つ新しいシステムでは、初期化を次のフェーズへ分けます。`SymphonyOrchestrator` はこのフェーズを、全サブシステムに対するComposition Rootとして統括します。

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

- モジュールは名前と実行順を持ち、同じフェーズを実行順に処理する。
- いずれかのフェーズが失敗した場合は後続フェーズを実行しない。
- `Shutdown` は構築順の逆順で実行する。
- 各フェーズは繰り返し呼び出されても二重登録や二重解放を起こさないよう設計する。
- 単純な機能へ形式的な5フェーズを強制しない。状態と依存が複雑になった時点で導入する。
- 新しいサブシステムを `SymphonyOrchestrator` へ組み込む場合、既存サブシステムの初期化順・失敗時挙動を変えないことを確認する。

## 依存性注入とService Locator

- ピュアC#クラスのモジュール内依存には、コンストラクタ注入を第一候補とする。
- Inspector参照はViewやCompositionなどUnity境界に限定する。
- `ServiceLocator` はCompositionでのサービス公開と、独立モジュール間の接続に使用する。
- DomainやApplicationの処理途中でService Locatorを直接検索しない。
- 取得には可能な限り `TryGetInstance<T>` を使用し、初期化失敗を明示的に処理する。
- 登録した型と所有者を記録し、`Shutdown` または対応するUnityライフサイクルで登録解除する。
- 複数サービスを公開するモジュールでは、機能単位のContainerにまとめる。

## Unityとの境界

UnityEngineの型を使うことと、Unityライフサイクルへ依存することを区別します。フレームワークのInfrastructure層がUnity APIを直接呼び出すのは、それがそのサブシステムの目的（Unity APIのラップ）である限り正しい設計です。ここでの回避対象は、Domain／Applicationが**目的外にUnityライフサイクルへ依存する**ことです。

- DomainやApplicationで `Vector2`、`Color` などの値型を使うことは許容する。
- `MonoBehaviour`、Scene、GameObjectの存在を前提にする処理はViewまたはCompositionへ置く。
- Unityコールバックは処理の入口とし、重要なロジックを通常のC#メソッドへ委譲する。
- `Awake` や `Start` の暗黙の実行順に依存せず、Compositionが明示的に初期化する。
- 実行順が必要な場合は数値を散在させず、定数またはモジュールの `Order` で表現する。
- Domain Reloadが無効でも、static状態を明示的にリセットできるようにする。

## 非同期処理の型

非同期型はレイヤーと用途に応じて選択します。

- UnityフレームやUnityライフサイクルに密接なComposition処理は `Awaitable`／`Awaitable<T>` を使用する。
- Application、Adaptor、公開ライブラリAPIは `Task<T>`／`ValueTask<T>` を使用する。
- 同期完了が多く、呼び出し側が1回だけawaitする軽量APIには `ValueTask<T>` を検討する。
- 複数回の参照、`Task.WhenAll`、外部ライブラリとの連携が必要な場合は `Task<T>` を使用する。
- `Awaitable` とTask系の橋渡しはCompositionやInfrastructureなどの境界に限定する。
- すべての中断可能な処理へ `CancellationToken` を伝播する。
- キャンセル、失敗、正常なfalseを混同しない。

## 状態とリソースの所有権

すべての可変状態と解放が必要なリソースには、所有者を1つ決めます。

- 作成した側が、破棄または所有権移譲の責任を持つ。
- イベントを購読した側が購読解除する。
- Addressablesのhandleを保持した側がreleaseする。
- CancellationTokenSourceを生成した側がdisposeする。
- Service Locatorへ登録したCompositionが登録解除する。
- Registryやcacheは、無効化とリセットのAPIを持つ。
- 所有権を移す場合は、型名、引数名、XMLドキュメントのいずれかで明示する。

## 公開APIとバージョニング

Symphony Frameworkは他プロジェクトが依存するパッケージであるため、公開APIとシリアライズ済みデータの変更は利用側の破壊につながります。変更前に影響範囲を判断します。

### 公開範囲

**サブシステムの機能は、すべてFacade経由で呼び出します。** 利用側がサブシステムに何かを「させる」経路はFacadeのメソッドだけであり、それ以外の公開型から機能を起動できてはいけません。副作用を持つ操作、内部状態を変える操作、永続化・シーン遷移・登録解除などを引き起こす操作は、Facadeにだけ`public`メンバーとして置きます。

したがって、Facade以外に`public`にしてよい型は、**機能ではなくデータと契約を表すもの**に限ります。具体的には、enum、利用側が実装・継承する抽象基底クラスとinterface、Value Object、例外、Inspector属性です。これらの型が`public`なのは、利用側がFacadeへ値を渡し、Facadeから値を受け取り、Facadeが駆動する実装を差し込むためであって、利用側がそれらを直接操作するためではありません。

この原則に沿って、`public`にする型は次に限定します。それ以外は`internal`にします。

- Facade（View層の静的公開クラス。例: `SceneLoader`、`ServiceLocator`、`SaveDataRegistry`）。これらFacadeクラスは、それが属するサブシステムの名前空間（`SymphonyFrameWork.System.SceneLoad` など）のAPIとして利用側から参照される。サブシステムの機能を持つ`public`メンバーを宣言できるのはFacadeだけである。
- 利用側プロジェクトが拡張・実装する前提の契約（Applicationの抽象基底クラスやinterface、またはViewの購読契約。例: `SaveDataLoader`、`PlayerPrefsSaveDataLoader`、`SaveDataContent`、`IInitializeAsync`、`IPausable`）。`Template/`配下にあっても、利用側が継承して保存先や変換方法を差し替えるための抽象基底クラスはこれに含める。継承させる型であることと、その型を利用側が直接呼べることは別問題であり、**利用側が実装するメンバーは`protected abstract`、Facadeや内部Managerが駆動するメンバーは`internal`にする**。契約型に`public`メソッドを置いてよいのは、利用側が自身のインスタンスへ対して行う操作（`Dispose`など）に限られる。
- Facadeの引数・戻り値として利用側へ渡るDomainのValue Object（例: `LocateType`、`SceneLoadState`、`SaveDataRegistryEntryInfo`）。フレームワーク側が生成して返すだけのValue Objectは、型は`public`のままコンストラクタを`internal`にする。
- Facadeが回復方法の異なる失敗を通知するための専用例外。特定サブシステムの例外はFacadeと同じ名前空間へ置き、原因例外と診断に必要な文脈を保持する。
- 利用側が自身のフィールド／クラスへ直接付与するInspector属性（`PropertyAttribute`の派生。例: `ReadOnlyAttribute`、`SubclassSelectorAttribute`）。
- 特定のサブシステムに紐づかない、汎用の再利用可能なユーティリティ（例: `SymphonyTask`、`SymphonyStringUtil`、`SymphonyConfigLocator`）。特定サブシステム専用のFacadeとは区別し、汎用性がある場合に限定する。
- Composition Rootは`internal`にする。`SymphonyOrchestrator`とその`PreserveObject`を含む全メンバーは内部実装であり、利用側へ公開しない。
- Config（ScriptableObject設定資産）はInfrastructureに属するため`internal`にする。Editorの設定画面・Drawerからは`InternalsVisibleTo`経由でアクセスする。
- 上記に該当しない内部Manager、Adaptor、Infrastructureの具象実装（Template実装の具象クラス、Config、内部専用Component）、Entity、DTO、Registry内部実装は`internal`にする。
- Editor拡張やCompositionのためだけに存在するメンバーは、Facade上にあっても`public`にしない。ローダーやManagerなど内部実装を取り出すアクセサ、状態のリセット、初期化・注入のフックは`internal`にし、`[assembly: InternalsVisibleTo("SymphonyFrameWork.Editor")]` など明示的なアセンブリ間許可で参照する。Editor拡張がRuntimeのinternal型を必要とする場合も、無条件にpublicへ広げない。
- 1つのサブシステムに複数の`public`な入口を作らない。同じ機能へ別経路で到達できるFacadeを追加すると、キャッシュ・Config解決・Editorからの可視性が経路ごとに分岐し、[Facadeと内部Manager](#facadeと内部manager)の「唯一の入口」が成立しなくなる。用途を限定した補助Facade（`ServiceInjector`）は、既存Facadeと機能が重複しない場合に限り認める。
- 既存のpublicな内部Manager等をinternalへ変更する場合も、破壊的変更として次の規約に従う。

### バージョニング

- 公開API（public／protectedのメンバー、シグネチャ、既定値の意味）の破壊的変更はSemantic Versioningのメジャー、後方互換な追加はマイナー、実装のみの修正はパッチとして扱う。
- 既存の公開APIを削除・変更する前に、代替手段を用意し `[Obsolete("代替APIの案内", error: false)]` を付けた移行期間を設ける。`error: true` への切り替えとメンバー削除はメジャーバージョンで行う。
- `ScriptableObject` や `[Serializable]` データのフィールド名・型を変更する場合は `[FormerlySerializedAs]` を使用し、既存アセットが読み込めることを確認する。
- 保存データ形式（`SaveDataContent` 継承クラスのフィールド）の変更は、利用側の既存セーブデータが読み込めなくなる可能性があるため、フィールド追加時の既定値、フィールド削除時の移行手順を明示する。
- 公開APIの追加・変更・非推奨化は、README、Sample、XMLドキュメント、CHANGELOGを同一の変更内で更新する。
- 内部実装（`internal`／`private`、名前空間内だけで完結する型）の変更にバージョニング上の制約は課さない。積極的にリファクタリングしてよい。

## 利用側への非侵襲性

フレームワークは、利用側プロジェクトのゲーム設計・データ構造・アーキテクチャを前提にしません。

- Domain、Application、Adaptorの型は、利用側の具象クラス・enum・namespaceを知らない。型引数、interface、DTOを介して利用側の型を受け取る。
- 利用側が拡張する前提の型（`SaveDataLoader` の継承、`IInitializeAsync` の実装など）は、抽象基底クラスまたはinterfaceとして公開し、実装すべき最小のメンバーだけを要求する。公開するのは「実装させるため」であり、その型を利用側が直接呼び出す前提は置かない（[公開範囲](#公開範囲)）。
- Configはフレームワークの動作を調整するためだけに使い、利用側のゲームデータ（アイテム、ステージ、キャラクターなど）を表現する場所として設計しない。
- Sampleは「動く例」であり、利用側が変更せず本番で使うことを前提にしない。`Template/` 配下は、利用側が継承して差分だけを実装するための抽象基底クラス（`PlayerPrefsSaveDataLoader` など）を置く場所であり、そのまま使う具象実装は`Internal/`へ置いて`internal`にする。
- フレームワークのログ、例外メッセージ、Editorウィンドウは、利用側のプロジェクト固有語彙（ゲームタイトル、独自ルール名）に依存しない。
- 利用側プロジェクトの都合（特定ジャンル、特定入力デバイスなど）に合わせた分岐を、Domain／Applicationへ追加しない。必要な場合はConfigまたは拡張点として切り出す。

## 開発コードの分離

- Runtimeにはマスタービルドで必要なコードだけを含める。
- Editor拡張はEditor専用asmdefへ置く。
- デバッグ入力や検証用ComponentはDevelop用asmdefへ分離し、Releaseビルドへ含めない。
- 技術検証や先行研究のデモは製品Runtimeから分離し、製品コードから参照しない。
- Sampleは公開APIの利用例として保ち、内部APIへ依存させない。

## 避ける設計

- すべての処理を1つのManagerやMonoBehaviourへ集める。
- DomainやApplicationからScene、GameObject、PlayerPrefs、Addressablesを直接操作する（Infrastructure以外での直接操作）。
- Service Locatorを任意の場所から参照し、依存を隠す。
- public setterで不変条件を迂回できる状態を公開する。
- property getterからロードやキャッシュ生成などの重い副作用を起こす。
- 将来必要になるかもしれないという理由だけでinterface、factory、genericを追加する。
- 初期化順を `Awake`、`Start`、Script Execution Orderの偶然に依存させる。
- 登録、購読、ロードだけを実装し、解除、解放、キャンセルを実装しない。
- Editor、Develop、SampleのコードをRuntimeへ混在させる。
- バージョニング上の合意なく公開API・シリアライズ形式を破壊的に変更する。
- 利用側プロジェクトの具体的なゲーム設計・データ構造をDomain／Applicationへ埋め込む。
- Facade、拡張用Application型、Value Object以外の型を根拠なく`public`にする。
- 実際に複数実装を切り替える予定がないのに、先回りしてAdaptorや`SubclassSelector`による選択機構を用意する。

## 設計判断のチェックリスト

新しい型や機能を追加する前に確認します。

- [ ] この型が変更される理由は1つか。
- [ ] この責務は現在のレイヤーとディレクトリに属するか。
- [ ] 依存方向は内側のルールを外側の詳細から守っているか。
- [ ] interfaceは実際の境界または交換理由を表しているか。
- [ ] 状態変更とQueryが分離されているか。
- [ ] 不正な状態を型またはコンストラクタで防げるか。
- [ ] 初期化、利用開始、終了の順序が明示されているか。
- [ ] 登録、購読、handle、tokenの所有者と解放方法が決まっているか。
- [ ] Unityライフサイクルへ依存しなくてもテスト可能な部分を分離したか。
- [ ] 小さな問題に対して過剰な抽象化を導入していないか。
- [ ] 公開APIまたはシリアライズ形式を変更する場合、バージョニングと移行手段を決めたか。
- [ ] 利用側プロジェクトの具体的な設計・データ構造を前提にしていないか。
- [ ] Facadeからの直接参照は単純なQuery／ValueObjectに留まり、実装の切り替えが必要な処理はAdaptorを経由しているか。
- [ ] Component（View）が、Application／Domainを直接参照せずView Model／Controller／Signalだけを介しているか。
- [ ] Adaptorを新設する場合、実際に複数実装を切り替える具体的な必要が今あるか（先回りではないか）。
- [ ] 公開にした型がFacade／拡張用Application型／ValueObjectのいずれかに該当するか。それ以外は`internal`か。
