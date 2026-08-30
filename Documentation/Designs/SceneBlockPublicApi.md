# Scene Block の公開API

## 目的

Issue [#110](https://github.com/HIBIKI5201/SymphonyFramework/issues/110) の Round 1 で、依存グラフを検証してトポロジカル層を算出する `SceneBlockGraphPlanner` を Domain へ追加した。**現状はすべて `internal` で、利用側から到達する経路が1つも無い。** アセットも公開エントリポイントも無いため、複数シーンをまとめてロードする単位という機能そのものが使えない。

Issue [#202](https://github.com/HIBIKI5201/SymphonyFramework/issues/202) が求めるのは次の2点である。

- **シーンブロックのロード状態を Symphony Administrator に表示する。**
- **ブロック単位のロードと単体シーンのロードを、ハイブリッドに実行できる設計にする。**

2つ目が設計の中心になる。`SceneLoader.LoadSceneAsync("Battle")` で単体ロードしたシーンと、`Battle` を含むブロックのロードが同じプロジェクトで併用される。**どちらか一方がもう一方のシーンを勝手にアンロードしてはならない。**

Issue タイトルの「永続化」は、ユーザー確認のうえ**ブロックを跨いだロードの維持**（セーブデータへの保存ではない）と解釈する。ブロックを切り替えても落としたくないシーンを、規則とフラグの両方で表現する。

## Round 分割

3 Round に分ける。**公開型を1つ追加するたびに `Tests/Editor/<型名>Tests.cs` が必要**（`PublicTypeTestCoverageTests`）なため、公開型の数が Round の大きさをそのまま決める。

| Round | 内容 | 公開型 | 版 |
| --- | --- | --- | --- |
| A | Authoring アセットと計画の公開 | `SceneBlockAsset`、`SceneBlockEntry`、`SceneBlockPlanException` | 6.6.0 |
| B | ロード実行の公開API | `SceneBlockLoader`、`SceneBlockInfo`、`SceneBlockLoadStateEnum` | 6.7.0 |
| C | Administrator への表示とInspector検証 | 無し（内部とEditorのみ） | 6.7.1 |

- **Round A だけでもリリースできる。** アセットを作ってシーン構成を書き始められ、依存グラフの検証結果を例外の形で受け取れる。
- **Round B が Issue #202 の本体。** 保持規則（ハイブリッド共存と永続化）はここで確定する。**Round A で中途半端なロードAPIを出して Round B で意味を変えることはしない。**
- **Round C は表示だけ。** 公開APIを増やさない。

ブランチは Round ごとに `develop` から切る。`feature/202-scene-block-asset`、`feature/202-scene-block-loader`、`feature/202-scene-block-window`。

---

## 公開API

### Round A: Authoring アセット

```csharp
namespace SymphonyFrameWork.System.SceneBlock
{
    [CreateAssetMenu(menuName = "Symphony Framework/Scene Block", fileName = "SceneBlock")]
    public sealed class SceneBlockAsset : ScriptableObject
    {
        public string BlockName { get; }                        // 既定はアセット名
        public IReadOnlyList<SceneBlockEntry> Entries { get; }
    }

    [Serializable]
    public sealed class SceneBlockEntry
    {
        public string SceneName { get; }
        public IReadOnlyList<string> DependsOn { get; }   // このシーンより先にロードするシーン名
        public int Priority { get; }                      // SceneLoadRequest.Priority と同じ意味
        public bool IsPersistent { get; }                 // ブロックのアンロードでも残す
    }

    public sealed class SceneBlockPlanException : Exception
    {
        public string BlockName { get; }
        public IReadOnlyList<string> Descriptions { get; }   // 検出した全異常の説明
    }
}
```

Inspector での書き味は次のとおり。

```text
Scene Block: TownBlock
├─ Entry[0]  Scene: Town_Base    DependsOn: []              Priority: 10  Persistent: ✔
├─ Entry[1]  Scene: Town_Props   DependsOn: [Town_Base]     Priority: 0
├─ Entry[2]  Scene: Town_NPC     DependsOn: [Town_Base]     Priority: 0
└─ Entry[3]  Scene: Town_UI      DependsOn: [Town_Props]    Priority: 0
```

**依存は `DependsOn`（先行を宣言）で書く。** Issue #110 の `A -> B, C` という表記は「Bのエントリに `DependsOn: [A]`、Cのエントリに `DependsOn: [A]`」へ対応する。エントリを追加するときに既存エントリを編集しなくて済み、npm や Gradle と同じ形になる。内部の `SceneBlockEdge(From = 先行, To = 後続)` へは `DependsOn` の各要素から `new SceneBlockEdge(dependency, entry.SceneName)` として変換する。

`SceneName` と `DependsOn` の各要素には `[SceneNameSelector]` を付け、Build Settings のシーン名から選ばせる。**配列要素へ PropertyDrawer が適用されるかは Unity 上で実測して確認する**（Round A の動作確認手順に含める）。適用されない場合は素の文字列入力にフォールバックし、Editor 側の検証で拾う。

`SceneBlockAsset` と `SceneBlockEntry` を `public` にする根拠は後述の「公開範囲の判断」にある。

### Round B: ロード実行

```csharp
namespace SymphonyFrameWork.System.SceneBlock
{
    public static class SceneBlockLoader
    {
        public static Awaitable<bool> LoadAsync(
            SceneBlockAsset block,
            IProgress<float> progress = null,
            CancellationToken token = default);

        public static Awaitable<bool> UnloadAsync(
            SceneBlockAsset block,
            IProgress<float> progress = null,
            CancellationToken token = default);

        public static bool IsLoaded(string blockName);
        public static bool TryGetBlockInfo(string blockName, out SceneBlockInfo blockInfo);
        public static IReadOnlyList<SceneBlockInfo> GetBlockInfos();
    }

    public enum SceneBlockLoadStateEnum
    {
        None = -1, Loading = 0, Complete = 1, Unloading = 2
    }

    public readonly struct SceneBlockInfo : IEquatable<SceneBlockInfo>
    {
        public string BlockName { get; }
        public SceneBlockLoadStateEnum State { get; }
        public float Progress { get; }
        public IReadOnlyList<string> HeldSceneNames { get; }   // このブロックが保持しているシーン
        public int LayerCount { get; }
    }
}
```

利用側の書き方は次のとおり。

```csharp
public sealed class BlockSwitcher : MonoBehaviour
{
    [SerializeField] private SceneBlockAsset _townBlock;
    [SerializeField] private SceneBlockAsset _dungeonBlock;

    private async Awaitable EnterDungeonAsync(CancellationToken token)
    {
        // 先に次をロードしてから前をアンロードすると、共通シーンの保持が途切れない。
        await SceneBlockLoader.LoadAsync(_dungeonBlock, token: token);
        await SceneBlockLoader.UnloadAsync(_townBlock, token: token);
    }
}
```

**`SwitchAsync` は作らない。** 上記の2行で同じ結果になり、順序の意味が呼び出し側のコードに残る。差分計算を隠した1つのメソッドにすると「なぜこのシーンが残ったのか」を説明できなくなる。

**進捗は `IProgress<float>`。** ブロック全体で `(完了済みシーン数 + 実行中の層の平均進捗) / 対象シーン総数` を通知する。層ごとではなくブロック全体で 0→1 に単調増加させる。

### 保持の規則（ハイブリッド共存と永続化）

Issue #202 の中心。**シーンごとに「どのブロックが保持しているか」を追跡し、保持が空になったときだけアンロードする。**

| 状況 | ロード時の扱い | アンロード時の扱い |
| --- | --- | --- |
| どのブロックも保持しておらず、SceneLoader も追跡していない | ロードし、そのブロックの保持へ加える | 保持が空になればアンロードする |
| 他のブロックが保持している | ロードせず、保持だけ加える | 保持が残るのでアンロードしない |
| ブロック外でロード済み（`SceneLoader` 直接呼び出し、初期シーン、`InitializeSceneList`） | ロードせず、**外部保持**として記録したうえで保持へ加える | 外部保持があるためアンロードしない |
| エントリが `IsPersistent = true` | 通常どおりロードする | **ブロックのアンロードでは決してアンロードしない** |

- **「ブロック外でロード済み」の判定は、ロードを開始する時点で `SceneLoader` が既に追跡しているかどうかで行う。** どのブロックも保持していないのに追跡されているなら、それはブロック以外の経路でロードされたシーンである。
- 外部保持を付けられたシーンは、その後どのブロックがアンロードしても残る。落としたい場合は `SceneLoader.UnloadSceneAsync` を利用側が明示的に呼ぶ。**フレームワークが利用側のロードを勝手に取り消さない**という一貫した規則にする。
- `IsPersistent` は「どのブロックにも属さないが常駐させたいシーン」（Manager シーンなど）を、ブロックの定義だけで表現するためにある。保持の追跡だけでは、そのシーンを含むブロックを全部アンロードしたときに落ちてしまう。

**同名ブロックの多重登録は例外にする。** Registry のキーは `BlockName` で、すでに同名で別アセットが読み込まれている場合は `InvalidOperationException` を投げる。同じアセットの再ロードは冪等に `true` を返す（`SceneLoader.LoadScene` がロード済みシーンへ行う扱いと揃える）。

### Round C: 表示

公開APIを追加しない。`SymphonyAdministrator` に `SceneBlockWindow` を足し、`SceneBlockViewModel` の `IReadOnlyReactiveProperty<IReadOnlyList<SceneBlockDto>>` を購読してブロック名・状態・進捗・保持シーン一覧を表示する。あわせて `SceneBlockAsset` の Inspector（`SceneBlockAssetEditor`）へ依存グラフの検証結果と算出した層を表示し、**Play Mode に入る前に循環依存や欠落参照へ気づけるようにする。**

### 対象外

- **`SceneBlockLoader` から Single モードのロードは提供しない。** ブロックは Additive の集合を前提にする。既存シーンを一掃したい場合は `SceneLoader` 側の既存APIを使う。
- **ブロックの入れ子（ブロックが他のブロックを依存に取る）は扱わない。** 依存グラフのノードはシーンに限定する。必要になったら独立した Round にする。
- **ロード状態のセーブデータ復元は行わない。** 「永続化」はブロックを跨いだロード維持の意味とする（ユーザー確認済み）。
- **Addressables 経由のシーンロードは扱わない。** 既存の `SceneLoader` が Build Settings のシーン名を前提にしているため、そこから外れる拡張は別Roundにする。

---

## 確定前に検証したアクセス手段

コードを読んで確認した。**1件、現状では届かない経路が見つかった。**

| 前提 | 確認結果 |
| --- | --- |
| `SceneBlockGraphPlanner.Plan` を Runtime の他の型から呼べる | ✔ `internal static`、名前空間 `SymphonyFrameWork.System.SceneBlock`、同一アセンブリ |
| `SceneLoadService.LoadScenes(SceneLoadRequest[], IProgress<float>, CancellationToken)` が使える | ✔ `internal async Task<bool>`。**引数は配列**なので、層のシーン一覧を配列へ変換して渡す |
| `SceneLoadService.UnloadScenes(string[], IProgress<float>, CancellationToken)` が使える | ✔ `internal async Task<bool>` |
| 「すでにロード済みか」を判定できる | ✔ `SceneLoadService.TryGetLoadedScene(string, out Scene)` が `internal` |
| Composition から `SceneLoadService` のインスタンスを取れる | ✘ **取れない。** `SceneLoader` は `private static SceneLoadService _service` を持つが、公開している内部アクセサは `CurrentViewModel` と `IsInitialized` だけである |
| テストから `internal` を触れる | ✔ `Runtime/AssemblyInfo.cs` が `SymphonyFrameWork.Tests.Editor` と `SymphonyFrameWork.Tests.Runtime` へ `InternalsVisibleTo` を与えている |
| Editor から `internal` を触れる | ✔ 同ファイルが `SymphonyFrameWork.Editor` へも与えている |

**Round B では `SceneLoader` へ `internal static SceneLoadService CurrentService => _service;` を追加する。** `CurrentViewModel` と同じ形の、Composition 専用の読み取り専用アクセサである。公開APIは増えない。これを設計書に書かずに着手すると、`SceneBlockService` へ `SceneLoadService` を注入できないことに実装中に気づくことになる。

`SceneLoadService.LoadScene` は、すでにロード済み・追跡済みのシーンに対して**再ロードせず `true` を返し、優先度に応じて Active Scene を切り替える**ことも確認した。したがってブロック側で「ロード済みなら呼ばない」判定を持たなくても壊れないが、**外部保持の記録には呼ぶ前の追跡状態が必要**なため、判定は保持規則のためにブロック側で行う。

---

## ファイル構成

パスはすべて `Assets/SymphonyFrameWork/` 起点。名前空間は Runtime 側がすべて `SymphonyFrameWork.System.SceneBlock`、Editor 側が `SymphonyFrameWork.Editor`。**`Internal/` 配下でも名前空間へ `Internal` を含めない。**

### Round A

- 新規 `Runtime/Service/SceneBlock/SceneBlockAsset.cs` — Authoring アセット
- 新規 `Runtime/Service/SceneBlock/SceneBlockEntry.cs` — エントリ1件
- 新規 `Runtime/Service/SceneBlock/SceneBlockPlanException.cs` — 依存グラフ異常
- 新規 `Runtime/Service/SceneBlock/Internal/Infrastructure/SceneBlockEntryReader.cs`
  - `internal static`。`IReadOnlyList<SceneBlockEntry>` から `nodeIds` / `SceneBlockEdge[]` / `SceneLoadRequest[]` を作る。**`ScriptableObject` を引数に取らない純粋関数にして、Unity アセット無しでテストする。**
  - `SceneBlockPlanErrorEnum` を人が読める説明文へ変換する処理もここへ置く（例外メッセージの正本）。
- 新規テスト `Tests/Editor/SceneBlockAssetTests.cs` / `SceneBlockEntryTests.cs` / `SceneBlockPlanExceptionTests.cs` / `SceneBlockEntryReaderTests.cs`
- 変更 `Documentation~/Modules/SceneBlock.md`（新規）、`README.md` の索引、`Documentation~/Html/`（再生成）
- 変更 `CHANGELOG.md`、`package.json`、`README.md`（版）、`Core/SymphonyConstant.cs` の `VERSION`

### Round B

- 新規 `Runtime/Service/SceneBlock/SceneBlockLoader.cs` — 公開エントリポイント
- 新規 `Runtime/Service/SceneBlock/SceneBlockInfo.cs`
- 新規 `Runtime/Service/SceneBlock/SceneBlockLoadStateEnum.cs`
- 新規 `Runtime/Service/SceneBlock/Internal/Domain/SceneBlockLoadEntity.cs`
  - ブロック1件の状態・層・保持シーン。**`SceneBlockEntry`（Authoring）と紛らわしくないよう `Entity` ではなく `LoadEntity` とする。**
- 新規 `Runtime/Service/SceneBlock/Internal/Application/SceneBlockRegistry.cs`
  - `BlockName -> SceneBlockLoadEntity` と、`SceneName -> 保持ブロック集合 + 外部保持フラグ` を持つ。
- 新規 `Runtime/Service/SceneBlock/Internal/Application/SceneBlockService.cs`
  - 層順のロード、保持規則の適用、`OnStateChanged` の発行。
- 新規 `Runtime/Service/SceneBlock/Internal/Application/IBlockSceneLoader.cs`
  - `SceneBlockService` が必要とする最小契約。**テスト境界のために作る**（[DesignPhilosophy `## 依存性逆転`](../DesignPhilosophy.md#依存性逆転)）。
- 新規 `Runtime/Service/SceneBlock/Internal/Infrastructure/SceneLoadServiceLoader.cs`
  - `IBlockSceneLoader` を `SceneLoadService` への委譲で実装する。**名前は要レビュー**（`ISceneLoader` / `UnitySceneLoader` の並びに合わせたが、`SceneLoadServiceLoader` は読みにくい）。
- 新規 `Runtime/Service/SceneBlock/Internal/Adaptor/SceneBlockQuery.cs`
- 変更 `Runtime/Service/SceneLoader/SceneLoader.cs` — `internal static SceneLoadService CurrentService` を追加
- 変更 `Runtime/Orchestrator/Internal/SymphonyOrchestrator.cs` — `SceneLoader.Initialize()` の**後**に `SceneBlockLoader.Initialize()`、`RecordInitializedSubsystem(SceneBlockLoader.ResetRuntimeState)`
- 新規テスト `Tests/Editor/SceneBlockLoaderTests.cs` / `SceneBlockInfoTests.cs` / `SceneBlockLoadStateEnumTests.cs` / `SceneBlockLoadEntityTests.cs` / `SceneBlockRegistryTests.cs` / `SceneBlockServiceTests.cs`
- 変更 `Documentation~/Modules/SceneBlock.md`、`Documentation~/Architecture.md`、`Documentation~/AgentUsage.md`、版一式

### Round C

- 新規 `Runtime/Service/SceneBlock/Internal/Adaptor/SceneBlockDto.cs`
- 新規 `Runtime/Service/SceneBlock/Internal/View/SceneBlockViewModel.cs`
- 新規 `Editor/Administrator/UITK/CS/SceneBlockWindow.cs` と `Editor/Administrator/UITK/UXML/SceneBlockWindow.uxml`
- 新規 `Editor/Configs/Drawer/SceneBlockAssetEditor.cs` — Inspector へ検証結果と層を表示
- 変更 `Editor/Documentation/SymphonyDocumentPageEnum.cs` へ `SceneBlock` を追加
- 変更 `Editor/Administrator/SymphonyAdministrator.cs` — ウィンドウの登録
- 新規テスト `Tests/Editor/SceneBlockDtoTests.cs` / `SceneBlockViewModelTests.cs` / `SceneBlockWindowTests.cs`
- 変更 `Documentation~/Modules/SceneBlock.md` の `## Editor機能`、`Documentation~/EditorTools.md` の `## 一覧`、版一式

新規 `.cs` の `.meta` は Unity に生成させる。

---

## 依存方向

```text
利用側 ──> SceneBlockLoader（Adaptor 公開エントリポイント）
                 │ Command
                 v
          SceneBlockService（Application）──> SceneBlockGraphPlanner / SceneBlockLoadEntity（Domain）
                 │                        └─> SceneBlockRegistry（Application）
                 │ 契約
                 v
          IBlockSceneLoader
                 ^
                 │ 実装
          SceneLoadServiceLoader（Infrastructure）──> SceneLoadService（SceneLoad サブシステム）
                 ^
                 │ 生成・注入
          SymphonyOrchestrator（Composition）
```

- **`Editor -> Runtime -> Core` の向きは変わらない。** Runtime 側は `UnityEditor` を参照しない。
- **`SceneBlockService` は `SceneLoader`（公開エントリポイント）を呼ばない。** [DesignPhilosophy `## 避ける設計`](../DesignPhilosophy.md#避ける設計) の「Applicationから公開エントリポイントを検索し、依存を隠す」に当たるため、Composition が注入する `IBlockSceneLoader` 越しに使う。
- **`SceneLoad` サブシステムは `SceneBlock` を知らない。** 依存は `SceneBlock -> SceneLoad` の一方向で、`SceneLoadService` に `IBlockSceneLoader` を実装させない（逆依存になる）。
- `SceneBlockEntryReader` と `SceneBlockGraphPlanner` は Unity API へ触れないため、EditMode テストから直接呼べる。

## 公開範囲の判断

[DesignPhilosophy `### 公開範囲`](../DesignPhilosophy.md#公開範囲) が列挙する「`public` にしてよい型」に、**利用側が作成する Authoring 用 `ScriptableObject` という項目が無い。** 現在の記述で最も近いのは「Unity上で利用側が配置または生成するAdaptor層のComponent」で、`ScriptableObject` は含まれない。同節は `Config` 用 `ScriptableObject` を `internal` にすると明記しているが、`SceneBlockAsset` はフレームワークの動作調整値ではなく**利用側のシーン構成そのもの**であり、Config とは性質が違う。

したがって次のように扱う。

- `SceneBlockAsset` と `SceneBlockEntry` は `public` にする。`[CreateAssetMenu]` でアセットを作り、Inspector で `SceneBlockLoader.LoadAsync` へ渡す以上、`internal` では成立しない。
- 値の書き込みは Inspector からだけにする。フィールドは `private` + `[SerializeField]`、外部へは読み取り専用プロパティだけを公開する（[CodeGuidelines `### シリアライズ`](../CodeGuidelines.md#シリアライズ)）。
- **`DesignPhilosophy.md` の `### 公開範囲` へ「利用側が作成するAuthoring用ScriptableObject」の行を追加することを提案する。** 提案にとどめ、この Round では書き換えない。承認されたら Round A と同じPRへ含める。

`SceneBlockPlanErrorEnum` と `SceneBlockPlanError` は `internal` のまま据え置く。Editor は `InternalsVisibleTo` で読めるため、Round C の Inspector 検証表示に不足は無い。公開APIには例外のメッセージと `Descriptions` という文字列一覧だけを出す。

---

## エラー処理

| 状況 | 扱い |
| --- | --- |
| `block` が `null` | `ArgumentNullException` |
| ブロックにエントリが1件も無い | `ArgumentException`。空のブロックをロードしても意味が無い |
| エントリのシーン名が `null` / 空 / 空白 | `SceneBlockPlanException`。アセットの記述誤りとして他の異常とまとめて報告する |
| 依存グラフに重複・自己依存・欠落参照・循環がある | `SceneBlockPlanException`。**検出した全異常を `Descriptions` へ入れる。** 1件目で止めない |
| 同名の別アセットがすでにロード済み | `InvalidOperationException` |
| 同じアセットの再ロード | 例外にせず `true` を返す。`SceneLoader.LoadScene` のロード済み扱いと揃える |
| シーンのロードが失敗した（`SceneLoader` が `false`） | `LoadAsync` が `false` を返す。**成立したシーンの保持は記録したまま残す** |
| `token` がキャンセルされた | `OperationCanceledException` が伝播する。**ロード済みのシーンは巻き戻さない** |
| 未初期化（Play Mode 外など） | `SymphonyNotInitializedException`。`SceneLoader` と同じ |

**依存グラフの異常を戻り値ではなく例外にする理由。** これはアセットの記述誤りであって、実行時に通常起こり得る失敗ではない。[DesignPhilosophy `## クラス設計`](../DesignPhilosophy.md#クラス設計) の「操作の失敗が通常起こり得る場合は `bool` またはTry pattern」「呼び出し側の実装ミスを示す場合は適切な例外」の区分で後者に当たる。`SceneInitializationException` と同じ位置づけである。

**キャンセルと失敗で巻き戻さない理由。** 巻き戻し（ロード済みシーンのアンロード）はそれ自体が非同期で、キャンセルできない後始末になる。キャンセルしたはずの処理がシーンを消し続ける状態を作るより、**成立した分を保持へ記録して残し、`UnloadAsync` で明示的に片付けられるようにする**ほうが説明できる。ブロックの状態は `Complete` にせず、保持シーン一覧に成立した分だけが載る。この挙動はモジュール文書の `## 実装時の注意` へ明記する。

**Domain Reload 無効への対応。** `SceneBlockLoader.ResetRuntimeState()` を `internal` で用意し、`SymphonyOrchestrator` の `RecordInitializedSubsystem` へ登録する。Play Mode を抜けたときに Registry の保持情報と ViewModel の購読を必ず捨てる。

---

## 影響範囲

- **公開APIは追加のみ。** 既存の `SceneLoader` の公開シグネチャと挙動、シリアライズ形式は変わらない。
- `SceneLoader` へ `internal` アクセサを1つ追加する（Round B）。利用側からは見えない。
- `SymphonyOrchestrator` の初期化順に1モジュール増える（Round B）。**`SceneLoader.Initialize()` より後**でなければ `CurrentService` が `null` になる。`Shutdown` は登録の逆順で走るため、`SceneBlockLoader` が先に片付く。
- `SceneBlockAsset` は利用側プロジェクトのアセットになる。**シリアライズ済みフィールド名を後から変える場合は `[FormerlySerializedAs]` が必要**になるため、Round A の時点でフィールド名を確定させる。
- Round C で `SymphonyDocumentPageEnum` に値を追加する。既存の値の数値は変えない。

---

## テストの置き場と種別

すべて EditMode（`Assets/SymphonyFrameWork/Tests/Editor/`）。**PlayMode テストは書かない。** 実際のシーンロードは `IBlockSceneLoader` の偽物で置き換えて検証し、Unity のシーンロードそのものは `SceneLoader` 側の既存テストとサンプルの担保に任せる。

メソッド名は既存の `対象_条件_期待` 形式に合わせる。

### Round A

`SceneBlockEntryReaderTests.cs`

| テスト | どう書くか |
| --- | --- |
| `ReadNodeIds_Entries_ReturnsSceneNamesInOrder` | エントリ3件を渡し、`nodeIds` が宣言順で返ることを配列比較する |
| `ReadEdges_DependsOn_CreatesEdgeFromDependencyToScene` | `DependsOn: [A]` のエントリBから `SceneBlockEdge(A, B)` が1件できることを確認する |
| `ReadEdges_MultipleDependsOn_CreatesEdgePerDependency` | `DependsOn: [A, C]` から辺が2件できることを確認する |
| `ReadEdges_EmptyDependsOn_CreatesNoEdge` | 依存なしのエントリから辺が0件であることを確認する |
| `ReadRequests_Entries_CarryPriority` | `Priority` が `SceneLoadRequest.Priority` へそのまま乗ることを確認する |
| `Describe_EachErrorKind_ContainsNodeIds` | `SceneBlockPlanErrorEnum` の4種すべてについて、説明文へ種別と対象ノード名が含まれることを確認する。**enum の値を1つずつ列挙し、新しい種別が増えたら落ちるようにする** |

`SceneBlockAssetTests.cs` / `SceneBlockEntryTests.cs` — `ScriptableObject.CreateInstance<SceneBlockAsset>()` と `SerializedObject` でフィールドへ値を入れ、読み取り専用プロパティが同じ値を返すこと、`BlockName` の既定がアセット名になることを確認する。**Unity アセットをファイルとして作らない。**

`SceneBlockPlanExceptionTests.cs` — 説明一覧とブロック名を渡して生成し、`Message` に全説明が含まれること、`Descriptions` が変更不能なコピーであること（渡した配列を後から書き換えても影響しないこと）を確認する。

### Round B

`SceneBlockServiceTests.cs` — 偽の `IBlockSceneLoader`（ロード要求を記録し、返す成否を差し替えられる `private sealed class`）を注入して検証する。

| テスト | どう書くか |
| --- | --- |
| `LoadAsync_Layers_LoadsInDependencyOrder` | 偽ローダーが受けた呼び出しの順序を記録し、層ごとにまとまって届くことを確認する |
| `LoadAsync_SameLayer_RequestsScenesTogether` | 同じ層の2シーンが1回の呼び出しでまとめて渡ることを確認する |
| `LoadAsync_AlreadyLoadedByAnotherBlock_DoesNotReload` | ブロック2つを順にロードし、共有シーンのロード要求が1回だけであることを確認する |
| `LoadAsync_LoadedOutsideBlock_MarksExternalHold` | 偽ローダーへ「すでにロード済み」と答えさせ、その後ブロックをアンロードしてもアンロード要求が出ないことを確認する |
| `UnloadAsync_LastHolder_UnloadsScene` | 保持していた唯一のブロックをアンロードし、アンロード要求が出ることを確認する |
| `UnloadAsync_OtherBlockStillHolds_KeepsScene` | 共有シーンを持つ2ブロックの片方をアンロードし、要求が出ないことを確認する |
| `UnloadAsync_PersistentEntry_KeepsScene` | `IsPersistent` のシーンがアンロード要求に含まれないことを確認する |
| `LoadAsync_CyclicGraph_ThrowsPlanException` | 循環するエントリで `SceneBlockPlanException` になり、ロード要求が1件も出ないことを確認する |
| `LoadAsync_SceneLoadFails_ReturnsFalseAndKeepsHeldScenes` | 途中の層を失敗させ、`false` と、成立済みシーンが保持に残ることを確認する |
| `LoadAsync_Canceled_DoesNotUnloadLoadedScenes` | 途中でキャンセルし、アンロード要求が出ないことを確認する |
| `LoadAsync_SameAssetTwice_ReturnsTrueWithoutReload` | 同じアセットを2回ロードし、2回目にロード要求が増えないことを確認する |
| `LoadAsync_DuplicateBlockName_ThrowsInvalidOperation` | 同名の別アセットで例外になることを確認する |
| `LoadAsync_Progress_ReportsMonotonicallyToOne` | 通知された進捗が単調非減少で、最後が 1 になることを確認する |

`SceneBlockRegistryTests.cs` — 保持集合の追加・除去、外部保持フラグ、`Clear` を単体で確認する。
`SceneBlockLoadEntityTests.cs` — 状態遷移（`Loading -> Complete`、`Complete -> Unloading`）と保持シーンの記録を確認する。
`SceneBlockLoaderTests.cs` — 未初期化での `SymphonyNotInitializedException`、`null` 引数の `ArgumentNullException`、`ResetRuntimeState` 後に状態が残らないことを確認する。
`SceneBlockInfoTests.cs` / `SceneBlockLoadStateEnumTests.cs` — 値の公開と等価性、enum の数値の固定。

### Round C

`SceneBlockViewModelTests.cs` — Query が返す Dto から `ReactiveProperty` が更新されること、同じ内容では通知されないこと（内容比較 Comparer）、`Dispose` で購読が切れることを確認する。
`SceneBlockDtoTests.cs` — 値の公開と等価性。
`SceneBlockWindowTests.cs` — `SceneLoadWindow` の既存テストに合わせ、UXML から要素を解決できることを確認する。

**`SceneBlockWindow` と `SceneBlockAssetEditor` の押下操作は自動で検証できない。** ボタン操作は人の確認へ回す（[implement スキルの design-doc 参照](../../.agents/skills/implement/references/design-doc.md)）。表示要素の実値の読み取りとスクリーンショットは自動で行う。

---

## 動作確認手順

自動で確認する範囲（各 Round 共通）:

1. `python scripts/verify_round.py` — コンパイル エラー0・警告0、EditMode / PlayMode 全数成功、Play Mode 2往復。
2. EditMode テストの件数が増えていること。

人が操作して確認する範囲:

**Round A**

3. `Assets > Create > Symphony Framework > Scene Block` でアセットを作れること。
4. **`SceneName` と `DependsOn` の要素が、`[SceneNameSelector]` のドロップダウンとして描画されること。** 配列要素へ PropertyDrawer が効かない場合はここで判明する。
5. 循環する依存を書いたアセットで `SceneBlockPlanException` のメッセージに全異常が並ぶこと（Round A では検証を呼ぶ検証用スクリプトから確認する）。

**Round B**

6. ブロックをロードし、依存の無いシーンが同時に、依存のあるシーンが後から読まれること。Profiler か `SceneLoadWindow` で層の境目を確認する。
7. **`SceneLoader.LoadSceneAsync` で単体ロードしたシーンを含むブロックをロードし、そのブロックをアンロードしても単体ロードしたシーンが残ること**（ハイブリッド共存の本命）。
8. 共通シーンを持つ2ブロックを「次をロード → 前をアンロード」の順で切り替え、共通シーンがアンロードされないこと。
9. `IsPersistent` のシーンが、そのシーンを含むブロックを全部アンロードしても残ること。
10. ロード中にキャンセルし、**ロード済みのシーンが消えないこと**と、その後 `UnloadAsync` で片付けられること。
11. Play Mode の開始・終了を2回繰り返し、Registry の保持情報が持ち越されないこと。

**Round C**

12. Symphony Administrator に Scene Block パネルが出て、ロード中の進捗と保持シーン一覧が更新されること。
13. **パネルを開いたままブロックをロード・アンロードし、表示が追従すること。** 開き直すと直る不具合をここで潰す。
14. `SceneBlockAsset` の Inspector が、循環依存のあるアセットで検証エラーと算出した層を表示すること。

---

## バージョン判断

| Round | 版 | 理由 |
| --- | --- | --- |
| A | 6.5.1 → **6.6.0** | 後方互換な公開API（アセットと例外）の追加。マイナー |
| B | 6.6.0 → **6.7.0** | 後方互換な公開API（エントリポイント、Info、Enum）の追加。マイナー |
| C | 6.7.0 → **6.7.1** | 公開APIを変えない。Editor 表示と内部型の追加のみ。パッチ |

## この Round で触るバージョン関連ファイル

各 Round で毎回同じ4か所を更新する。**`Core/SymphonyConstant.cs` の `VERSION` を落とさないこと**（`preflight` の `[version]` が検査する。CONTRIBUTING §6 は3か所しか挙げていない）。

| ファイル | 触る箇所 |
| --- | --- |
| `Assets/SymphonyFrameWork/package.json` | `version` |
| `Assets/SymphonyFrameWork/Core/SymphonyConstant.cs` | `VERSION` |
| `Assets/SymphonyFrameWork/CHANGELOG.md` | 見出しと `### Add` |
| `Assets/SymphonyFrameWork/README.md` | 「現在のバージョン」と機能索引（Round A で1行追加） |

3つの Round が同じファイルを触るため、**Round が終わるたびにコミットとマージまで進めてから次へ着手する。** 同じファイルへ複数 Round の変更が同時に載ると、コミットを意図ごとに分けられなくなる。
