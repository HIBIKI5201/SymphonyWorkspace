# Symphony Framework コーディングガイドライン

このドキュメントは、Symphony Framework内のC#コードとUnityアセットを追加・変更する際の共通ルールです。新規コードには本ガイドラインを適用し、既存コードは機能変更の範囲内で段階的に合わせてください。

作業の進め方（`.meta` の扱い、ブランチとコミット規約、ドキュメントの同時更新、Unityでの検証手順）は [CONTRIBUTING.md](./CONTRIBUTING.md) にあります。Markdown文書の書き方は [DocumentationGuidelines.md](./DocumentationGuidelines.md) にあります。本ドキュメントは「コードをどう書くか」に限定しています。

## 基本方針

- `.cs`、`.md`、`.txt` の文字コードはUTF-8を使用する。
- Unity 6（6000.3.10f1）でコンパイルできるコードを書く。
- 可読性と保守性を、短さや技巧的な実装より優先する。
- 1つのクラス、メソッド、フィールドには1つの責務を持たせる。
- 公開APIは必要最小限にし、フレームワーク内だけで使う要素は `internal` または `private` にする。
- 継承を拡張点として設計していない具象クラスには `sealed` を付ける。
- 継承を許可するクラスは、原則として `abstract` にし、拡張可能なメンバーと前提条件をXMLドキュメントへ記載する。
- 既存の公開APIを変更・削除する場合は、利用側への影響と移行方法を確認する。
- 外部パッケージを追加・更新した場合は `package.json` とREADMEも更新する。
- 公開API（`public`/`protected` なクラス・メソッド・プロパティ・設定アセットの項目、名前空間、asmdef名など）を追加・変更・削除した場合は、同じ変更の中で利用者向け文書も更新する。更新対象と正本の分担は[CONTRIBUTING.md `## 6. 変更に応じて同時に更新するもの`](./CONTRIBUTING.md#6-変更に応じて同時に更新するもの)に従い、`AGENTS.md`へAPI説明やコード例を複製しない。挙動やシグネチャが変わったのに手順が古いままの文書は、それ自体をバグとして扱う。更新不要と判断した場合も、その理由（内部実装のみの変更である等）をPR説明かコミットメッセージに明記する。
- AIによる変更は、サマリー、コメント、タイポ修正を基本とする。ロジック、公開API、シリアライズデータを変更する場合は、明示された作業範囲内で行い、担当者が差分と実行結果を確認する。公開APIに影響する変更を行うAIエージェントは、上記の文書更新ルールを自動的に適用すること。

## ディレクトリとAssembly Definition

コードは責務に応じたディレクトリとasmdefへ配置します。

| ディレクトリ | 用途 | 主な制約 |
| --- | --- | --- |
| `Core/` | RuntimeとEditorで共有する最小限の基盤と内部ヘルパー | 上位機能へ依存させない。`ReactiveProperty<T>`などの`internal`な型は`Core/AssemblyInfo.cs`の`InternalsVisibleTo`でRuntime／Editorへ公開する |
| `Core/Editor/` | Editor APIを使う共有基盤（パス解決、Editor向け定数など） | `#if UNITY_EDITOR`で囲む。**Runtimeから参照しない**（Playerビルドで解決できなくなる）。参照してよいのはEditorアセンブリだけ |
| `Runtime/` | Playerビルドに含まれる機能 | `UnityEditor` を参照しない |
| `Runtime/System/<Subsystem>/` | サブシステムの公開エントリポイント、公開Component、公開Strategy、Info、Value Object、Enum | 利用側が参照する型だけを直下へ置く |
| `*/Internal/` | 各フォルダのDomain、Application、Adaptor、View、Infrastructure、Compositionの内部実装 | `internal`なEntity、Service、Registry、Query、Dto、ViewModel、Unity API実装を置く。名前空間には`Internal`を含めない（→ `## 名前空間`） |
| `Runtime/Obsolete/` | 代替APIへ移行済みの `[Obsolete]` シム | 移行期間のみ存在させ、メジャー更新で削除する |
| `Editor/` | Inspector、設定画面、Generatorなど | `SymphonyFrameWork.Editor` asmdefに含める |
| `Samples/` | 利用例 | 製品コードから依存しない |

- RuntimeコードからEditor APIを参照しない。Editor専用の`MenuItem`、設定、Inspector処理は`Editor/`側の型へ分離する。`#if UNITY_EDITOR`で囲んでもRuntimeファイルへ`UnityEditor`参照を置く例外は作らない。
- **`Core`はRuntimeとEditorの両方から使われる共有アセンブリであり、Editor APIを使うこと自体は許容する。** ただし置き場所と参照方向を守る。Editor APIを使う型は`Core/Editor/`へ置き、`#if UNITY_EDITOR`で囲む。その型をRuntimeから参照してはならない。Runtimeが必要とする値は、Editor側のCompositionからRuntimeへ注入する。
- Editor設定をRuntimeの挙動へ反映する場合は、Runtime側に`internal`な設定値の入れ物を置き、Editorの初期化処理が読み込んで注入する。Runtimeから設定ストア（`EditorPrefs`、`ScriptableSingleton`など）を直接読まない。
- asmdef間の参照は必要な方向にだけ追加し、循環参照を作らない。
- **公開エントリポイントが大きくなった場合は、型を分けずに `partial` でファイルだけを分割する。** 型を分けると利用側の呼び出しが変わり、破壊的変更になります。分割の単位は関心事ごとで、ファイル名は `<型名>.<関心事>.cs` とします（例: `SymphonyAwaitable.WhenAll.cs`）。**`partial` を使ってよいのはこの目的に限ります。** 内部実装（`*/Internal/`）は型ごと分割できるため、`partial` ではなく型の分割で対処します。
- パッケージ導入とAssets直置きの両方に対応するパスには、`EditorSymphonyConstant.FRAMEWORK_PATH` を使用する。
- `Assets/SymphonyFrameWork` や `Packages/symphonyframework` を機能コードへ直接埋め込まない。

## 名前空間

ルート名前空間は`SymphonyFrameWork`とし、サブシステムに合わせて次の名前空間を使用します。Adaptor、Application、View、Infrastructureなどの概念レイヤー名は名前空間へ含めず、型名と`Internal/`フォルダで責務と公開範囲を表します。

```text
SymphonyFrameWork
├─ Attribute
├─ Configs
├─ Core
├─ Debug
├─ Editor
├─ Exception
├─ Interface
├─ Orchestrator
├─ System
│  ├─ Audio
│  ├─ Pause
│  ├─ SaveData
│  ├─ SceneLoad
│  └─ ServiceLocate
└─ Utility
```

- 消費者向けの公開エントリポイントと公開Componentは、属するサブシステムの名前空間へ置く。例: `SaveStore`は`SymphonyFrameWork.System.SaveData`、`SceneLoader`は`SymphonyFrameWork.System.SceneLoad`、`ServiceLocator`と`ServiceInjector`は`SymphonyFrameWork.System.ServiceLocate`。
- Runtimeの`SymphonyOrchestrator`は`SymphonyFrameWork.Orchestrator`、Editorの`SymphonyEditorOrchestrator`は`SymphonyFrameWork.Editor`へ置く。Editor asmdefからRuntime asmdefへの一方向参照を維持する。
- 特定サブシステムの専用例外は、その公開エントリポイントと同じ名前空間へ置く。複数サブシステムで共通する例外は`SymphonyFrameWork.Exception`へ置く。
- 公開型と内部実装の区別は、名前空間ではなくフォルダで表す。Adaptorの公開エントリポイントとInfo、公開Strategyはサブシステム直下、Domain、Application、Adaptor Query／Dto、View、Infrastructureの内部実装は同じフォルダの`Internal/`配下へ置く。
- Sampleは`SymphonyFrameWork.Samples.<SampleName>`とする。
- ファイルの配置と名前空間を一致させる。
- 名前空間はディレクトリ構成を反映する。並び順を示す数字など、コード上の責務を表さないディレクトリ名は除外する。
- `internal`な型は、所属するフォルダ直下の`Internal/`へ置く。`Internal`と概念レイヤー名は名前空間には含めない。例: `Runtime/System/SceneLoad/Internal/Application/SceneLoadService.cs`の名前空間は`SymphonyFrameWork.System.SceneLoad`。
  - 横断的で最小限の内部ヘルパーは`Core/Internal/`へ置く。
  - Domain Entityは`Runtime/System/<Subsystem>/Internal/Domain/`へ置く。
  - ApplicationのServiceとRegistryは`Runtime/System/<Subsystem>/Internal/Application/`、AdaptorのQueryとDtoは`Internal/Adaptor/`へ置く。
  - 利用側が継承するpublic abstractなStrategyはサブシステム直下、内部Strategyは`Internal/Application/`へ置く。Infrastructureの具象Loaderは`Internal/Infrastructure/`へ置く。
  - ViewModelと内部表示Componentは`Runtime/System/<Subsystem>/Internal/View/`へ置く。`View`は責務を表すフォルダだが、名前空間には追加しない。
  - RuntimeのComposition Rootとライフタイム用Componentは`Runtime/Orchestrator/Internal/`、EditorのComposition Rootは`Editor/Orchestrator/Internal/`へ置く。
  - `internal`なConfig用`ScriptableObject`は`Runtime/Configs/Internal/`へ置く。
  - `Internal/`の外には、利用側が使う公開エントリポイント、公開Component、拡張契約、Info、不変な値だけを残す。
- `Runtime/Obsolete/`は、フォルダ構成と名前空間を一致させる唯一の例外とする。非推奨APIは移行先と同じ名前空間に置き、サブフォルダ名で対応する名前空間を示す。
- 1ファイルには1つの公開型だけを定義し、ファイル名を型名と一致させる。
- privateな入れ子型は、所有する型と密接に関係し、単独で再利用しない場合に限り同じファイルへ置ける。

## 書式

- インデントにはスペース4個を使用し、タブを使用しない。
- 波括弧は改行して配置する（Allman形式）。ただし本文が1文だけの制御構文は1行形式にする（→ `### 短縮形でも波括弧を省略しない`）。
- `using` ディレクティブはファイル先頭にまとめ、未使用のものを残さない。
- `using` は `System`、Symphony Framework、Unity、その他の順でグループ化し、各グループ内は名前順に並べる。
- アクセス修飾子は省略しない。
- 1行には原則として1つの文だけを書く。本文が1文だけの制御構文を1行形式で書く場合だけ例外とする。
- 長い引数リストや条件式は、意味のまとまりごとに改行する。
- マジックナンバーや重複する文字列は、名前付き定数または設定値へ置き換える。
- 不要になったコードをコメントアウトして残さず、バージョン管理から参照する。

### 短縮形でも波括弧を省略しない

**制御構文の本文は1文だけでも波括弧で囲みます。** ガード節や早期リターンも例外にしません。波括弧の省略は、後から1文追加したときにスコープを間違えるバグを生みます。

**本文が1文なら1行形式、2文以上ならAllman形式にします。** 1文のためだけに3行使うと、ガード節が並んだときに本題の処理が下へ押し出されます。

```csharp
// 誤: 波括弧の省略
if (token.IsCancellationRequested) return;

// 正: 本文が1文なら1行形式
if (token.IsCancellationRequested) { return; }

// 正: 本文が2文以上ならAllman形式
if (token.IsCancellationRequested)
{
    _source.Cancel();
    return;
}
```

`else if`、`foreach`、`using` 文、`lock` も同じです。式本体メンバー（`=>`）は、1つの式を返すプロパティとメソッドに限って使用できます。

### ローカル変数の型は明示する

**ローカル変数の型は型名で書き、`var` を使いません。** 変数の型が宣言行だけで分かる状態を優先します。代わりに、右辺が `new` の場合はターゲット型 `new()` で型名の重複を省きます。

```csharp
// 誤: 型が右辺にしかない
var go = new GameObject();
var scenes = new List<Scene>();

// 正: 左辺で型を示し、右辺を短縮する
GameObject go = new();
List<Scene> scenes = new();
Scene scene = SceneManager.GetSceneByName(sceneName);
```

- 型名を書けない匿名型（LINQの射影結果など）だけ `var` を使用できる。
- `foreach` の反復変数も型を明示する。
- 引数として直接渡す場合など、変数を経由しない `new` は従来どおり型名を書く（`Register(new SceneLoadInfo(...))`）。

## 命名規則

| 対象 | 規則 | 例 |
| --- | --- | --- |
| class、struct | PascalCase | `SceneLoadInfo` |
| enum | PascalCase + `Enum` | `SceneLoadStateEnum` |
| interface | `I` + PascalCase | `IInitializeAsync` |
| 公開操作クラス | 利用側が認識するサービス名をそのまま使う | `SceneLoader`、`SaveStore`、`AudioPlayer`、`PauseController` |
| ホストライフサイクルを統括するComposition Root | 対象 + `Orchestrator` | `SymphonyOrchestrator`、`SymphonyEditorOrchestrator` |
| Applicationの処理クラス | 対象 + `Service` | `SceneLoadService` |
| 利用側が継承するApplicationの抽象拡張点 | 対象 + `Strategy` | `SaveDataLoaderStrategy`、`PlayerPrefsSaveDataLoaderStrategy` |
| Registry／Entityを読み取り変換するAdaptorクラス | 対象 + `Query` | `SceneLoadQuery` |
| QueryからViewModelへ渡す更新データ | 対象 + `Dto` | `SceneLoadDto` |
| 状態保持・検索クラス | 対象 + `Registry` | `SceneLoadRegistry` |
| Registryが管理する同一性とライフサイクルを持つクラス | 対象 + `Entity` | `SceneLoadEntity` |
| 公開の管理状態Queryで返す不変スナップショット | 対象 + `Info` | `SceneLoadInfo` |
| View層の表示状態クラス | 対象 + `ViewModel` | `SceneLoadViewModel` |
| 値変更を通知するCoreクラス | 対象 + `ReactiveProperty` | `ReactiveProperty<T>` |
| Unityライフサイクルを受けるクラス | 対象 + `Component` | `ServiceLocateComponent` |
| Infrastructureの具象I/O・変換クラス | 対象 + `Loader` | `JsonUtilitySaveDataLoader` |
| 複雑な生成クラス | 対象 + `Factory` | `ModuleFactory` |
| 設定用ScriptableObject | 対象 + `Config` | `SceneLoadConfig` |
| Editor専用の診断・操作ツール群 | 対象 + `Tools` | `SymphonyMcpTools` |
| method | 動詞から始まるPascalCase | `LoadScene` |
| property | PascalCase | `InitializeSceneList` |
| bool property | `Is`、`Has`、`Can` などから始める | `IsResetAndLoadOnPlay` |
| parameter、local variable | camelCase | `sceneName` |
| private field | `_` + camelCase | `_initializeSceneList` |
| const | UPPER_SNAKE_CASE | `SYMPHONY_PACKAGE` |
| event | `On` + PascalCase | `OnPauseChanged` |
| event handler | 対象と動作 + `Handler` | `PauseChangedHandler` |
| async method | `Async` で終える | `LoadAsync` |
| Try pattern | `Try` で始め、成功可否を `bool` で返す | `TryGetInstance` |

- 略語だけを大文字にせず、通常の単語として扱う。例: `Json`, `Url`, `Id`。
- 単位を持つ値は名前に単位を含める。例: `durationSeconds`、`sizeBytes`。
- コレクションには複数形、または内容が分かる名前を付ける。例: `loadedScenes`。
- 公開エントリポイントには一律のサフィックスを付けず、利用側が認識するサービス名をそのまま使用する。`Loader`、`Locator`、`Injector`、`Player`、`Controller`などがサービス名の一部である場合は維持できる。
- 公開エントリポイント以外の操作や可変状態を持つclassには、実際の役割を表すサフィックスを付ける。`Manager`や`Data`だけで終わる新しい型名は禁止する。
- `Entity`はDomainの可変な登録単位、`Service`はApplicationのCommand処理、`Strategy`は利用側が継承・差し替えできるApplicationの抽象拡張点、`Registry`はEntityの保持と検索、`Query`はRegistry／EntityからInfoまたはDtoを生成するAdaptorの唯一の読み取り変換、`Info`は公開管理状態の不変スナップショット、`ViewModel`はViewの表示状態、`Component`はUnityライフサイクル境界に限定する。単なる改名で責務の混在を隠さない。
- 抽象拡張点はI/Oを扱っていても`Strategy`で終え、Infrastructureの利用側が継承しないsealed具象実装だけを`Loader`で終える。`PlayerPrefsSaveDataLoaderStrategy`はI/Oの一部を実装していても、利用側がJSON変換を継承実装するpublic abstractな中間拡張点なのでStrategyを優先する。
- enumはValue Objectへ含めず独立した型カテゴリとして扱い、入れ子型を含め必ず`Enum`で終える。自動生成される`SceneListEnum`、`TagsEnum`、`LayersEnum`、`AudioGroupTypeEnum`は既に適合している。
- abstractなApplicationクラスでも、利用側による差し替えを拡張点として設計していない場合は`Strategy`と呼ばない。
- `Factory`は複数依存や生成規則がある場合だけ使用し、単なる`new`の置き換えにしない。
- Entityを公開APIへ直接返さない。利用側へ状態を見せる場合は、可変参照を含まないInfoへ変換する。
- EntityはすべてDomainへ置き、Unity APIや外部I/Oを参照させない。
- Commandを実行するServiceだけが、観測可能な状態変更の確定後に標準C#の状態変更eventを論理更新1回につき1回発行する。失敗やキャンセルを状態へ反映した場合も通知し、状態が変わらなければ通知しない。RegistryとQueryは変更eventを持たない。
- ViewModelはServiceのeventを受けてAdaptorのQueryを呼び、DtoからReactivePropertyを更新する。ApplicationからQuery、ViewModel、ReactivePropertyを参照しない。
- class／structでは、型の種類が役割を表す`Exception`、`Attribute`、`Window`、`Drawer`、`State`、`Operation`、`Content`、`Utility`も適切なサフィックスとして認める。enumには適用せず、必ず`Enum`で終える。
- 型名と同じ意味を繰り返す曖昧な変数名を避ける。例: `data`、`info`、`manager`はスコープが広い場所では具体化する。
- UXMLとUSSの要素名、class名にはlower-kebab-caseを使用する。例: `save-data-panel`。

## メンバーの記述順とregion

クラス内のメンバーは、原則として次の順番で記述します。同じ分類では、関連するメンバーを近くに配置します。

1. コンストラクタ
2. publicイベント
3. publicプロパティ
4. interface実装プロパティ
5. public定数、public `static readonly`
6. publicメソッド
7. interface実装メソッド
8. publicなenum定義
9. publicなclass定義
10. publicなstruct定義
11. private／internal定数、`static readonly`
12. `[SerializeField]` フィールド
13. その他のprivateフィールド
14. Unityライフサイクルメソッド（`Awake`、`OnEnable`、`Start`、`Update`、`OnDisable`、`OnDestroy`）
15. イベントハンドラ
16. protectedメソッド、virtual／abstractメソッド
17. privateメソッド
18. internalヘルパーメソッド
19. privateなenum定義
20. privateなclass定義
21. privateなstruct定義
22. デバッグ機能

相互に強く関係するオーバーロードは分離せず、まとめて配置してください。

### regionで外部向けAPIと内部処理を分ける

**型の中身は `#region` で「利用側から呼べるもの」と「実装の都合」に二分します。** 上の記述順（1〜22）はregionの中でもそのまま維持し、regionは順序を上書きしません。

```csharp
public sealed class SceneLoadService
{
    #region 外部向けAPI

    /// <summary>
    ///     シーンをロードする。
    /// </summary>
    public Awaitable<bool> LoadAsync(string sceneName, CancellationToken token = default)
    {
    }

    #endregion

    #region 内部処理

    private readonly SceneLoadRegistry _registry;

    private void RemoveFinishedEntry(SceneLoadEntity entity)
    {
    }

    #endregion
}
```

**境界は上の記述順の番号と一致します。** どちらへ入れるか迷う必要はありません。

| region | 入れるもの |
| --- | --- |
| `#region 外部向けAPI` | 記述順の 1〜10。コンストラクタ、publicなevent・プロパティ・定数・メソッド、interface実装、公開する入れ子型 |
| `#region 内部処理` | 記述順の 11〜22。private／internal定数、`[SerializeField]`、privateフィールド、Unityライフサイクルメソッド、イベントハンドラ、protected／virtualメソッド、privateメソッド、internalヘルパー、privateな入れ子型、デバッグ機能 |

- **公開メンバーを持たない `internal` な実装型でも2つに分けます。** その型を呼ぶ側から見た入口を「外部向けAPI」へ置いてください。`internal` なメソッドが実装型の入口である場合は、記述順18ではなく6として扱います。
- 3つ目のregionを作って分類を細分化しません。まとまりが増えて分けたくなった場合は、regionではなく型の分割を検討します（`## ディレクトリとAssembly Definition`）。
- 片側が空になる型（定数だけのstatic classなど）では、その空のregionを書きません。
- `#region` を折りたたみの都合で使わないでください。分類の意味を持たないregionは、読み手が中身を推測できなくなるだけです。

## XMLドキュメントとコメント

- すべてのメソッドにXMLドキュメントを付ける。
- publicな型、プロパティ、イベントにもXMLドキュメントを付ける。
- コメントとXMLドキュメントは原則として日本語で記述し、文末を「。」で終える。
- 引数や戻り値の意味が自明でない場合は `<param>`、`<returns>`、`<typeparam>` を記述する。
- コードと一致しない古いコメントは、機能変更と同時に更新または削除する。
- TODOを残す場合は、未完了の内容と対応条件を具体的に書く。

### サマリーの形式

**サマリーは、対象によって1行形式と複数行形式を使い分けます。** 型とメソッドは複数行形式にして、コードを流し読みしたときに区切りとして目に留まるようにします。

| 対象 | 形式 |
| --- | --- |
| class、struct、interface、enum、メソッド、コンストラクタ | 複数行形式。`<summary>` の内側で改行し、スペース4個分インデントする |
| プロパティ、イベント、フィールド、enumのメンバー | 1行形式（`/// <summary> 説明。 </summary>`）。1行で書けない場合だけ複数行形式にする |

```csharp
/// <summary>
///     指定したシーンを非同期でロードする。
/// </summary>
/// <param name="sceneName"> Build Settingsに登録されたシーン名。 </param>
/// <param name="token"> 処理を中断するためのトークン。 </param>
/// <returns> ロードに成功した場合はtrue。 </returns>
public static Awaitable<bool> LoadSceneAsync(
    string sceneName,
    CancellationToken token = default)
{
    // 実装
}

/// <summary> 現在ロード中のシーン数。 </summary>
public static int LoadingCount { get; }
```

**サマリーの文字量は極力減らします。** 1文で言い切り、条件、副作用、前提は他のタグへ振り分けます。

- 1文で書き、修飾を重ねない。「〜する。」で終える。
- 引数と戻り値の説明はサマリーへ書かず、`<param>` と `<returns>` へ書く。
- 例外、前提条件、呼び出し順の制約は `<exception>` と `<remarks>` へ分ける。
- 複数文が必要な場合は文ごとに改行する。1行に詰め込まない。

```csharp
// 誤: 1文に条件と副作用を詰め込んでいる
/// <summary>
///     引数のシーン名が有効な場合にシーンをロードし、成功したらActive Sceneを切り替えて、失敗した場合はfalseを返す。
/// </summary>

// 正: サマリーは1文に絞り、残りをタグへ分ける
/// <summary>
///     シーンをロードし、Active Sceneへ設定する。
/// </summary>
/// <returns> ロードに成功した場合はtrue。 </returns>
/// <exception cref="ArgumentException"> sceneNameが空の場合。 </exception>
```

### 処理コメント

**メソッドの中には、処理のまとまりごとにコメントを残します。** コメントは実行時コストを持たないため、量を惜しむ理由がありません。書かないことで失われるのは、後から読む人（と、差分だけを見るAIエージェント）が意図を復元する手がかりです。

- 処理のまとまりごとに、そのまとまりが何をしているかを1行で置く。
- 分岐、早期リターン、ループの継続条件には、その条件が成立する状況を書く。
- 特に、理由、前提、制約、回避している問題は必ず書く。コードから読み取れないのはこれらだけです。
- 1行を機械的に言い換えるだけのコメント（`// カウントを1増やす`）は書かない。まとまりの単位で書きます。

```csharp
public static bool TryRegister(Type type, object instance)
{
    // 型と実体の対応が壊れた状態を登録させない。
    if (!type.IsInstanceOfType(instance)) { return false; }

    // 同じ型の二重登録は、後から登録した側が黙って捨てられるため失敗として扱う。
    if (_instances.ContainsKey(type)) { return false; }

    // 待機中の取得要求は、登録が完了してから解放する。
    _instances.Add(type, instance);
    ReleaseWaiters(type);

    return true;
}
```

## API設計

- 引数の不正は、可能な限り公開メソッドの入口で検証する。
- `null`、空文字、範囲外の値に対する挙動を明確にする。
- 操作の失敗が通常起こり得る場合は、`bool` またはTry patternを使用する。
- 呼び出し側の実装ミスを示す場合は、`ArgumentNullException`、`ArgumentException`、`InvalidOperationException` など適切な例外を使用する。
- 内部コレクションは直接公開せず、`IReadOnlyList<T>` など読み取り専用の型で返す。
- AdaptorのQueryだけがRegistryまたはDomain Entityを読み取り、公開API向けInfoまたはViewModel向けDtoへ変換する。InfoとDtoはQueryから渡された値だけを保持し、抽出ロジックを重複させない。
- 公開エントリポイントの管理状態照会メソッドは対応するQueryを呼んでInfoを返すだけにし、Registry／Entityを直接参照したり自分で変換したりしない。
- `ServiceLocator.GetInstance<T>`や`SaveStore.Get<T>`のように登録された公開payload自体の取得が契約である場合もQueryを経由する。payloadは返せるが、それを管理するEntityや内部コレクションは返さない。
- ViewModelは初期化時とServiceの状態変更event受信時にAdaptorのQueryを呼び、専用Dtoを受け取る。Queryは公開エントリポイントとViewModelを参照しない。
- 状態を変更するプロパティにpublic setterを設けない。`SetXxx`、`RecordXxx`、`ChangeXxx` など意図が分かるメソッドを公開する。
- generic APIと `Type` を受け取るAPIを併設する場合は、検証と本処理を共通化する。
- 新しいoverloadは既存の既定値と意味を変えないようにする。
- 安全に転送できる非推奨APIには`[Obsolete]`を付け、代替APIをメッセージに含める。名前、シグネチャ、意味が同時に変わり旧契約を安全に維持できないAPIは、メジャー更新で直接削除し、CHANGELOGのBreaking項目と移行ガイドへ明記する。
- 公開APIの追加・変更時はREADME、Sample、XMLドキュメントの更新要否を確認する。

## Unity固有のルール

### シリアライズ

- Inspectorへ公開するフィールドは、原則として `private` + `[SerializeField]` にする。
- `[SerializeField]` フィールドには、用途が自明でない限り `[Tooltip]` を付ける。
- 外部からの参照が必要な場合は、読み取り専用プロパティを公開する。
- データコンテナとして使用する `ScriptableObject` は、Inspectorだけから値を設定し、外部には読み取り専用プロパティを公開する。
- 既存のシリアライズ済みフィールド名を変更する場合は、データ移行のため `[FormerlySerializedAs]` を使用する。
- polymorphicな設定値には、必要に応じて `[SerializeReference]` と `[SubclassSelector]` を使用する。
- `ScriptableObject`の設定値は、CompositionまたはInfrastructureのinternalな`SymphonyConfigLocator`を通して取得し、利用側やApplicationからLocatorを直接呼ばない。Configアセットの生成はEditor Orchestratorから呼ばれる`SymphonyConfigInitializer`だけが行う。

```csharp
[SerializeField, Tooltip("再生開始時にロードするシーン名。")]
private string _initialSceneName;

public string InitialSceneName => _initialSceneName;
```

### ライフサイクルと状態

- `[RuntimeInitializeOnLoadMethod]`は`SymphonyOrchestrator`だけ、`[InitializeOnLoad]`と`[InitializeOnLoadMethod]`は`SymphonyEditorOrchestrator`だけに付ける。個別サブシステムやInitializerには付けない。
- 自動初期化属性を持つメソッドとOrchestratorのstatic constructorは、同じOrchestratorの明示的な初期化メソッドを呼ぶ入口に限定する。
- `MenuItem`、`SettingsProvider`、`CustomEditor`、`UxmlElement`などの発見用属性は使用できるが、そのcallbackからpackage-wideな初期化を開始しない。
- `AssetPostprocessor`はUnityが発見して任意のタイミングで呼ぶhost callbackとして使用でき、自動初期化属性の禁止対象には含めない。ただし初期化、Asset生成、`AssetDatabase.Refresh`、購読を所有せず、変更種別を`SymphonyEditorOrchestrator`へ通知してcoalesceするだけにする。
- `SymphonyConfigInitializer`などのEditor初期化モジュールは自動初期化属性を持たず、`SymphonyEditorOrchestrator`から明示的に開始される。`SymphonyConfigLocator`と`SymphonyEditorConfigLocator`はinternalな検索専用Infrastructureとし、生成や初期化を開始しない。
- Runtimeの`SymphonyDebugHUD`などに`MenuItem`や`UnityEditor`参照を置かない。メニュー入口はEditor専用型へ分離し、Runtimeの公開操作を呼ぶだけにする。
- サブシステムは副作用を持つstatic constructor、static field initializer、独自の遅延初期化を使わず、Orchestratorから呼ばれる`Initialize`と`Shutdown`または`Dispose`を提供する。
- Runtimeの各サブシステムから`SymphonyOrchestrator`を呼ばない。Orchestratorは汎用Factoryや`DontDestroyOnLoad` helperとして公開せず、必要なComponent、値、Infrastructure契約をBuild時に注入する。
- Editor WindowがRuntime ViewModelを取得するための`internal`な読み取り専用accessorだけは例外とし、そこから生成、初期化、終了を実行できない契約にする。
- `DefaultExecutionOrder`とScript Execution Orderをpackage-wideな初期化順に使わない。順序はOrchestratorのフェーズとモジュール順で表す。
- `Awake` は自身の初期化、`OnEnable` は購読・登録、`Start` は他オブジェクトへ依存する開始処理に使用する。
- `OnEnable` で登録したイベントやService Locatorは、対応する `OnDisable` で解除する。
- `OnDestroy` では、そのオブジェクトが所有するCancellationTokenSourceや一時リソースを解放する。
- staticなランタイム状態は、Enter Play Mode OptionsでDomain Reloadが無効でも正しく初期化できるようにする。
- staticイベント、キャッシュ、コレクションは `Initialize` またはリセット処理で明示的に初期化する。
- package-wideな`destroyCancellationToken`への終了callback登録は`SymphonyOrchestrator`が1回だけ行う。各Service、Registry、公開エントリポイントは個別登録せず、Orchestratorの`Shutdown`から構築順の逆順で終了する。
- `Shutdown`は多重呼び出しを無害にし、終了中または終了後の公開操作を拒否する。Domain Reloadなしで初期化が再実行された場合は、残存状態を先に終了する。
- Editor初期化中にAssetPostprocessorなどが再入した場合は、初期化を再帰実行せずdirty flagへcoalesceし、`Ready`後に1回処理する。
- package-wideな終了callbackから呼ぶ`Shutdown`は同期・非ブロッキングにする。非同期保存、`GetAwaiter().GetResult()`、長時間I/Oを行わず、進行中処理のキャンセルと即時解放だけを行う。
- 1つのモジュールの`Shutdown`が失敗しても残りを逆順で終了し、例外は最後にまとめてログへ記録する。
- RuntimeのViewModelはCompositionが所有し、Play Mode終了時またはランタイム状態のリセット時に破棄する。
- ViewModelとReactivePropertyの購読は所有者を明確にし、`OnDisable`、`Dispose`、またはランタイム状態のリセットで解除する。Editor WindowはViewModel自体を所有せず、自身の購読だけを所有する。
- Editor WindowがRuntimeのViewModelを必要とする場合は、Composition Rootの`internal`な読み取り専用accessorから取得する。Window側でViewModelを生成、キャッシュ継承、置換しない。
- UnityEngine.ObjectにはUnity独自のnull判定があるため、破棄済みObjectを通常のclassと同じように扱わない。
- 毎フレーム不要な検索、LINQ、文字列生成、アロケーションを `Update` や `OnGUI` に置かない。

### フレームワーク機能の利用

- シーン管理には、特別な理由がない限り`SceneManager`を直接呼ばず`SceneLoader`を使用する。
- 共有インスタンスの登録と取得には`ServiceLocator`を使用し、所有者と解除タイミングを明確にする。
- セーブ対象は`SaveDataContent`を継承し、`SaveStore`を通して操作する。
- ポーズに追従する待機やTweenには、`PauseController`または`SymphonyTween`の対応APIを使用する。
- フレームワークが生成する設定アセットやenumを手作業で複製しない。

## 非同期処理

- 非同期メソッド名は `Async` で終える。
- RuntimeとEditorの非同期処理は、原則としてUnity 6の`Awaitable`／`Awaitable<T>`を返す。公開APIも同じ契約にする。
- publicな非同期処理は、原則として`CancellationToken token = default`を受け取る。
- 受け取ったトークンは、下位の非同期処理とフレーム待機へ必ず渡す。
- 個別MonoBehaviourだけに紐づく処理には、そのMonoBehaviourの`destroyCancellationToken`を渡す。パッケージ全体の処理にはOrchestratorが所有するlifetime tokenを渡し、各サブシステムがtokenへ終了callbackを登録しない。
- `async void`はUnityイベントやUIコールバックなど、戻り値を受け取れない入口に限定する。その内部でキャンセルと例外を処理し、実処理はawait可能なメソッドへ委譲する。
- 完了済み値、複数処理の待機、timeout、条件待機には`SymphonyAwaitable`を使用する。
- `Awaitable`は原則1回だけawaitする。フィールドへ保存しない、再awaitしない、複数の呼び出し元へ同じインスタンスを返さない。
- 同じ進行中処理を複数の呼び出し元が待つ場合は、待機者ごとに`AwaitableCompletionSource`を作り、完了結果を個別に通知する。
- `AsyncOperation`は`Awaitable.FromAsyncOperation`で待つ。
- 外部ライブラリが`Task`を返す場合だけ境界で受け取り、`SymphonyAwaitable.FromTask`で直ちに変換する。
- 外部契約が`Task`を要求する場合だけ`SymphonyAwaitable.AsTask`を使う。フレームワークの公開契約へ`Task`／`ValueTask`を漏らさない。
- Unity APIはメインスレッドで呼び出す。バックグラウンドへ移動した処理は、Unity APIへ触れる前に`Awaitable.MainThreadAsync`で戻す。
- ポーリングには`GetAwaiter().GetResult()`などのブロッキング待機を使わず、`Awaitable.NextFrameAsync`または`SymphonyAwaitable.WaitWhile`を使用する。
- キャンセル、例外、正常な`false`を別の結果として扱う。

## エラー処理とログ

- 回復不能な不変条件違反は例外、通常起こり得る失敗は戻り値、設定不足は警告として扱う。
- 例外を握りつぶさない。失敗を戻り値へ変換する場合も、API仕様として意図が分かるようにする。
- キャンセルを一般的な失敗と混同しない。
- エラーメッセージには、対象の型名、シーン名、アセットパスなど調査に必要な文脈を含める。
- `Debug.LogError` と `Debug.LogWarning` は、`[{nameof(TypeName)}] メッセージ` の形式で出力する。
- MonoBehaviourからログを出す場合は、選択と追跡ができるよう第2引数へ `this` を渡す。
- 正常系で毎フレームログを出力しない。
- 複数の情報をまとめて出す場合は `SymphonyDebugLogger` または `StringBuilder` を使用する。
- Editor専用の詳細ログは `#if UNITY_EDITOR` で囲み、Playerビルドへ不要な処理を含めない。
- パスワード、トークン、個人情報、セーブデータの機密値をログへ出力しない。

**ログの重要度によって使うAPIを変えます。** 素の `Debug.Log` はPlayerビルドへ残り、利用側のコンソールを汚します。利用側から抑止する手段はありません。

| 重要度 | 使うもの | ビルドへ |
| --- | --- | --- |
| 利用側の誤用・異常系を伝える | 素の `Debug.LogWarning` / `Debug.LogError` | **残す。** 利用側が知るべき情報 |
| フレームワーク内部の動作追跡 | `#if UNITY_EDITOR` + Project Settingsの出力可否フラグ | 残さない |
| 一時的なデバッグ | `SymphonyDebugLogger.LogDirectForEditor`（`[Conditional("UNITY_EDITOR")]`付き） | 残さない |

- **情報レベル（`Debug.Log`）を `#if UNITY_EDITOR` の外へ置かない。** 文字列補間を伴う場合、出力の有無に関わらず文字列が構築されます。
- 出力可否をProject Settingsから切り替える場合は、Runtime側に `internal` な設定値の入れ物を置き、Editorの初期化処理が注入します（→ `## ディレクトリとAssembly Definition`）。実装例は `ServiceLocateLogOption` と `ServiceLocator` です。

## Editor拡張

- Editor専用コードは `Editor/` に配置する。
- `SymphonyEditorOrchestrator`がConfig確認、enum自動生成の購読、ログ出力、Editor用Loader、Play Mode遷移、assembly reload前、Editor終了を一元管理する。
- Editorモジュールは`[InitializeOnLoad]`、`[InitializeOnLoadMethod]`、副作用を持つstatic constructorを使用せず、`SymphonyEditorOrchestrator`から明示的に初期化・終了する。
- package-wideな`EditorApplication.update`、`delayCall`、TimerはEditor Orchestratorが所有するモジュールから登録し、named callbackまたは`IDisposable`でassembly reload前に解除できる形にする。解除できない匿名callbackを登録しない。
- Editor起動時の複数モジュールによるAsset変更はEditor Orchestratorが集約し、`AssetDatabase.Refresh`を最終段階で必要な場合だけ1回実行する。各初期化モジュールから個別にRefreshしない。
- `AssetPostprocessor`などUnityが直接呼ぶ必要のあるcallbackは残せるが、初期化と所有権を持たず、Editor Orchestratorが初期化したモジュールへ変更情報を中継するだけにする。
- Editor Window自身の表示期間だけ必要な`EditorApplication.update`はWindowが所有できるが、`OnEnable`と`OnDisable`で必ず対にする。Runtime状態の常時ポーリングには使用しない。
- Runtime状態の表示にはViewModelの`IReadOnlyReactiveProperty<T>`を購読し、RegistryやEntityの直接参照、`EditorApplication.update`による常時ポーリングを避ける。
- Runtime状態を変更する操作はViewModelへ委譲せず、Editor Windowから公開エントリポイントのCommandを呼ぶ。
- Edit ModeではRuntimeのViewModelを生成せず、Windowに未接続状態を表示する。Play Mode開始時にCompositionが生成したViewModelを取得し直す。
- Windowの有効化時またはViewModelの再取得時に現在値を受け取って表示を初期化し、無効化時またはPlay Mode終了時に購読の`IDisposable`を破棄する。
- アセット変更には、必要に応じて `Undo.RecordObject`、`EditorUtility.SetDirty`、`AssetDatabase.SaveAssets` を使用する。
- `AssetDatabase.Refresh` をループ内や頻繁に呼び出さない。
- ファイル生成前に出力先と内容を検証し、既存ファイルを上書きする条件を明確にする。
- `EditorPrefs` は個人設定、`ProjectSettings` はプロジェクト共有設定として使い分ける。
- UI Toolkitのイベントは、Windowの無効化時に解除または破棄できる構造にする。
- **Window自身が所有する `VisualElement` への購読は、匿名ラムダで書いてよい。** `Button.clicked += () => ...` のような購読は、Windowが閉じられれば `VisualElement` ごと破棄されるため、デリゲートがWindowより長生きしません。「解除できない匿名callbackを登録しない」の対象は、Windowより長生きする購読先（`EditorApplication.update`、static event、Runtime側のReactiveProperty）です。**購読先の寿命で判断します。**
- Package環境とAssets環境の双方で、UXML、USS、設定アセットのパスを確認する。

## パフォーマンスとコレクション

- `Update` 内での `GetComponent`、`Find`、`Resources.Load` は避け、初期化時にキャッシュする。
- 頻繁に参照する型付きデータには、目的に合ったDictionaryやHashSetを使用する。
- コレクションを公開する場合は、呼び出し側から内部状態を変更できない形にする。
- LINQは初期化やEditor処理では使用できるが、高頻度のRuntime処理では割り当てを確認する。
- ReactivePropertyは`IEqualityComparer<T>`を注入可能にし、指定がない場合だけ既定の比較を使用する。
- ReactivePropertyで配列や一覧を通知する場合は、内容比較を行うComparerと不変スナップショットを使用する。同値更新と不要なUI全再構築を避ける。
- 文字列をループで連結する場合は `StringBuilder` を使用する。
- ロック中にawait、Unity API、外部コールバックを実行しない。
- 最適化のために可読性を落とす場合は、Profilerによる根拠をコメントまたは変更説明へ残す。

## 変更時の確認

変更内容に応じて、次の項目を確認します。

- RuntimeとEditorの両asmdefがエラーなくコンパイルできる。
- Unity Editorの再起動後も設定アセットと自動生成コードが正しく読み込まれる。
- Play Modeの開始・終了を繰り返してもstatic状態やイベント購読が残らない。
- Domain Reloadを無効にしたPlay Modeでも初期化できる。
- Runtime終了時にpackage-wideな`destroyCancellationToken`からOrchestratorの`Shutdown`が1回だけ実行され、各モジュールが逆順で終了する。
- script reloadとEditor再起動で`SymphonyEditorOrchestrator`の購読が重複せず、assembly reload前に解除される。
- シーンのロード、アンロード、Single相当の遷移、キャンセルが正しく動作する。
- Service Locatorの登録、重複登録、解除、破棄、非同期待機が正しく動作する。
- Save Dataの初回生成、保存、再ロード、削除、ローダー変更が正しく動作する。
- Editor拡張がPackage導入とAssets直置きの両方で必要なアセットを見つけられる。
- Consoleに新しいErrorや意図しないWarningが出ていない。
- `git diff --check` で空白エラーがない。
- 公開挙動を変更した場合は `CHANGELOG.md` を更新した。

## レビュー用チェックリスト

- [ ] 変更の責務と配置先が一致している。
- [ ] 命名、メンバー順、書式が本ガイドラインに従っている。
- [ ] 制御構文の本文が1文でも波括弧で囲まれ、1文なら1行形式・2文以上ならAllman形式になっている。
- [ ] ローカル変数の型が明示され、`var` が匿名型以外に使われていない。
- [ ] 型の中身が `#region 外部向けAPI` と `#region 内部処理` に分かれている。
- [ ] 型とメソッドのサマリーが複数行形式で、1文に収まっている。
- [ ] メソッド内に、処理のまとまりごとのコメントがある。
- [ ] `InitializeOnLoad`系属性と`RuntimeInitializeOnLoadMethod`が対応するOrchestrator以外に存在しない。
- [ ] 副作用を持つstatic constructor／static field initializerと、package-wideな`DefaultExecutionOrder`が存在しない。
- [ ] package-wideなEditor callback、delayCall、Timerが解除可能で、Editor Orchestratorのライフサイクル下にある。
- [ ] Editor初期化中のcallback再入がcoalesceされ、起動処理から`AssetDatabase.Refresh`が重複実行されない。
- [ ] package-wideな`destroyCancellationToken`へ終了callbackを登録しているのがRuntime Orchestratorだけである。
- [ ] Orchestratorが初期化順を記録し、`Shutdown`を逆順かつ多重呼び出し可能に実行している。
- [ ] RuntimeのサブシステムからOrchestratorの生成・永続化helperを呼んでおらず、依存がCompositionから注入されている。
- [ ] `Shutdown`が同期・非ブロッキングで、途中の例外後も残りの解放を継続している。
- [ ] Registryの登録単位に同一性とライフサイクルがある場合は、DomainのEntityとして表現されている。
- [ ] AdaptorのQueryだけがRegistry／Domain Entityを読み取り、公開API向けInfoまたはViewModel向けDtoへ変換している。
- [ ] 公開エントリポイントの管理状態照会がQueryを呼ぶだけで、Entityや可変参照ではなく不変なInfoを返している。
- [ ] 登録payloadを返すQueryが、それを管理するEntityや内部コレクションまで公開していない。
- [ ] ApplicationがServiceとRegistryで構成され、Query、Dto、ViewModelへ依存していない。
- [ ] public abstractな拡張点が`Strategy`、internal sealedの具象I/O実装が`Loader`で終わっている。
- [ ] enum型が入れ子型を含め`Enum`で終わり、Value Objectと混同されていない。
- [ ] Commandを実行するServiceだけが、観測可能な論理更新の確定後に変更eventを過不足なく発行している。
- [ ] ViewModelがCommandを実行せず、Serviceのeventを受けてQueryを呼び、読み取り専用ReactivePropertyだけをViewへ公開している。
- [ ] RuntimeのViewModelをCompositionが所有し、Editor Windowは購読だけを所有している。
- [ ] ReactivePropertyの比較方法が値の性質に合い、コレクションは内容比較と不変スナップショットを使用している。
- [ ] ReactivePropertyとViewModelの購読解除がライフサイクルと対になっている。
- [ ] public／protected APIにXMLドキュメントがある。
- [ ] Runtimeコードが `UnityEditor` に依存していない。
- [ ] イベント購読、Service Locator登録、リソースの解除処理が対になっている。
- [ ] 非同期処理へCancellationTokenが伝播している。
- [ ] シリアライズ済みデータと公開APIの互換性を確認した。
- [ ] 失敗時の戻り値、例外、ログが適切である。
- [ ] 高頻度処理に不要な割り当てや検索がない。
- [ ] 必要なREADME、Sample、CHANGELOGを更新した。
- [ ] Unity上で関連機能を確認した。
