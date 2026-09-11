# Scene Block Asset Infrastructure（Round 110-2）

Issue: [#110](https://github.com/HIBIKI5201/SymphonyFramework/issues/110)

ロードマップ: [Documentation/IssueImplementationRoadmap.md](../IssueImplementationRoadmap.md) Phase 4 / Round 110-2

前提となる設計書: [SceneBlockDagPlanner.md](SceneBlockDagPlanner.md)（Round 110-1、Domainモデルと`SceneBlockGraphPlanner`）

## Round分割の前提（継続）

`#129`系（Roslyn自動生成）はAGENTS.md §7解釈のユーザー確認待ちで止まったまま（[RoslynSourceGeneratorPrototype.md](RoslynSourceGeneratorPrototype.md)参照）。Round 110-1と同じ判断で、`#110`を先行して進める。Round 110-1はsubmodule `feature/110-scene-block-dag-planner`ブランチへコミット・push済みだが、`develop`へのPRマージ（`release_round.py finalize`）は未実施（人の確認待ち）。本Roundは、その未マージのfeatureブランチの上に積み重ねる。**develop以降への公開操作（PR作成・finalize）は本Roundでも行わない。**

## 目的

Round 110-1で実装した純粋なDomainモデル（`SceneBlockEdge` / `SceneBlockPlanResult` / `SceneBlockGraphPlanner`）は、Unity Authoringデータから独立している。Round 110-2では、それを実際にプロジェクトへ配置できる`ScriptableObject`アセットと、アセットの内容をDomainモデルへ変換して検証結果を得るInfrastructure、そしてアセットの妥当性をInspector上で確認できるCustom Editorを追加する。

ロードマップの完了条件は「Assetの並び順に依存せず同じ実行計画になり、循環などをロード開始前にInspector/ログで確認できる」。本Roundはこれを満たす最小構成とする。

この Round では次を扱わない。

- `SceneLoadService`との統合、並行ロード、進捗、キャンセル（Round 110-3）
- 公開API、Info/Dto、Administrator、MCP診断、Sample、モジュール文書の新設（Round 110-4）
- `Assets > Create`メニューからのアセット生成（`[CreateAssetMenu]`）。本Roundのアセットは`internal`型であり、利用側コードから型を参照して生成する手段がまだ無い。既存の`SceneLoadConfig`等と同様、生成経路（Factory、公開APIからの自動生成、または`CreateAssetMenu`)は公開APIが確定するRound 110-4で決める。本Roundの検証は`uloop-execute-dynamic-code`による`ScriptableObject.CreateInstance`+`AssetDatabase.CreateAsset`で行う

## 公開API

追加・変更する`public`APIは無い。本Roundの型はすべて`internal`にする
（[DesignPhilosophy.md](../DesignPhilosophy.md)「公開範囲」: `Config`、`Infrastructure`の具象実装は`internal`にする。「Asset」もInfrastructure層に属する）。

`SceneBlockAssetDrawer`（`UnityEditor.Editor`派生）だけは、既存の`SceneLoadConfigDrawer`（`Editor/Configs/Drawer/SceneLoadConfigDrawer.cs:15`）と同じ理由で`public sealed class`にする。Unity の `CustomEditor` 機構自体はアクセス修飾子を要求しないが、既存コードの形式に揃える。

**この`public`指定により、`Tests/Editor/PublicTypeTestCoverageTests.cs`の網羅検査（[PublicTypeTestCoverageTests.cs:128-151](../../Assets/SymphonyFrameWork/Tests/Editor/PublicTypeTestCoverageTests.cs)）が`SceneBlockAssetDrawer`に対応する`Tests/Editor/SceneBlockAssetDrawerTests.cs`を要求する。** 既存の`SceneLoadConfigDrawer`は同検査の`UntestedPublicTypes`（未消化の残作業一覧）に載っているだけで、新規追加の型を無条件に免除する前例ではない（同ファイルの`UntestedBacklog_TypesWithTests_AreRemovedFromList`が示すとおり、この一覧は減らす方向にしか動かせない）。本Roundはテストを新規に書く側で対応し、一覧へは追加しない。

## ファイル構成

| パス | 種別 | 内容 |
| --- | --- | --- |
| `Assets/SymphonyFrameWork/Runtime/Service/SceneBlock/Internal/Infrastructure/SceneBlockEdgeAuthoring.cs` | Authoring値（`[Serializable] internal struct`） | `{ [SerializeField] string _from; [SerializeField] string _to; }`。`internal string From => _from;` `internal string To => _to;`。テストと将来のAuthoring側コードから生成しやすいよう`internal SceneBlockEdgeAuthoring(string from, string to)`を持つ（構造体の暗黙のパラメータ無しコンストラクタとは共存する） |
| `Assets/SymphonyFrameWork/Runtime/Service/SceneBlock/Internal/Infrastructure/SceneBlockAssetPlanner.cs` | Infrastructure（純粋な変換+検証ロジック） | `internal static class SceneBlockAssetPlanner { internal static SceneBlockPlanResult Plan(IReadOnlyList<string> sceneIds, IReadOnlyList<SceneBlockEdgeAuthoring> edges) }`。Authoringデータを正規化してから`SceneBlockGraphPlanner.Plan`（Round 110-1）へ委譲する。**Unity APIに触れない**（`UnityEngine.SerializeField`はAuthoring構造体側にのみ現れ、このクラス自体は`System.Collections.Generic`だけを参照する） |
| `Assets/SymphonyFrameWork/Runtime/Service/SceneBlock/Internal/Infrastructure/SceneBlockAsset.cs` | Infrastructure（`ScriptableObject`） | `internal sealed class SceneBlockAsset : ScriptableObject`。`[SerializeField] private string[] _sceneIds`、`[SerializeField] private SceneBlockEdgeAuthoring[] _edges`（既定値は両方`Array.Empty<T>()`）。`internal IReadOnlyList<string> SceneIds => _sceneIds;`、`internal IReadOnlyList<SceneBlockEdgeAuthoring> Edges => _edges;`、`internal SceneBlockPlanResult Plan() => SceneBlockAssetPlanner.Plan(_sceneIds, _edges);`（1行の委譲のみで独自ロジックを持たない） |
| `Assets/SymphonyFrameWork/Editor/SceneBlock/SceneBlockAssetDrawer.cs` | Editor（Custom Inspector） | `[CustomEditor(typeof(SceneBlockAsset))] public sealed class SceneBlockAssetDrawer : UnityEditor.Editor`。`SceneLoadConfigDrawer.cs`と同じ`serializedObject.Update()` → 描画 → `ApplyModifiedProperties()`の構造。`OnEnable`でBuild Settingsのシーン名一覧をキャッシュ（`SceneLoadConfigDrawer.cs:74-80`と同じ手順）。`_sceneIds`の各要素をBuild Settingsのシーン名からのPopupで編集（同ファイルの`initializeSceneList`ループを踏襲）。`_edges`の各要素は`From`/`To`をそれぞれ**現在の`_sceneIds`の値一覧**からのPopupで編集する（Build Settings全体ではなく、このアセットが宣言したノードに限定し、フリーテキストによる誤字を防ぐ）。`_sceneIds`が空の間は`EditorGUILayout.HelpBox("先にシーンを追加してください", MessageType.Info)`を表示し、辺の追加ボタンを無効化する。描画・`ApplyModifiedProperties()`の後に`((SceneBlockAsset)target).Plan()`を呼び、`SceneBlockPlanResult.Errors`があれば`EditorGUILayout.HelpBox`（`MessageType.Error`、`SaveDataSettingProvider.cs:119`・`SymphonySettingProvider.cs:181-193`と同じ idiom）で種別ごとに1件ずつ表示し、無ければ層数と各層のノード数を`MessageType.Info`のHelpBoxで表示する |
| `Assets/SymphonyFrameWork/Tests/Editor/SceneBlockEdgeAuthoringTests.cs` | EditModeテスト | コンストラクタと公開プロパティの契約 |
| `Assets/SymphonyFrameWork/Tests/Editor/SceneBlockAssetPlannerTests.cs` | EditModeテスト | Authoringデータの正規化とDomain委譲の契約 |
| `Assets/SymphonyFrameWork/Tests/Editor/SceneBlockAssetDrawerTests.cs` | EditModeテスト | `PublicTypeTestCoverageTests`（下記参照）が要求する`SceneBlockAssetDrawer`のテスト。`OnInspectorGUI`の描画自体は自動検証できないため、`[CustomEditor]`の配線だけを確認する |

新しいasmdefは作らない。`SceneBlockAsset`等は既存の`SymphonyFrameWork.asmdef`（Runtime）、`SceneBlockAssetDrawer`は既存の`SymphonyFrameWork.Editor.asmdef`へ追加する。`Runtime/Service/SceneLoader/`の`Internal/Infrastructure/UnitySceneLoader.cs`と同じフォルダ命名（`Internal/Infrastructure/`）に揃える。

### アクセス手段の検証（実施済み）

- `Runtime/AssemblyInfo.cs:4`に`[assembly: InternalsVisibleTo("SymphonyFrameWork.Editor")]`が既にあることを確認した。`SceneBlockAsset`が`internal`でも、Editorアセンブリの`SceneBlockAssetDrawer`から`[CustomEditor(typeof(SceneBlockAsset))]`と`_sceneIds`等へのアクセス（`SerializedProperty`経由、フィールド自体への直接アクセスは無し）が届く。
- Round 110-1の`SceneBlockEdge`のコンストラクタと`From`/`To`は`internal`（[SceneBlockEdge.cs:17,25,28](../../Assets/SymphonyFrameWork/Runtime/Service/SceneBlock/Internal/Domain/SceneBlockEdge.cs)）。同じ`SymphonyFrameWork`アセンブリに置く`SceneBlockAssetPlanner`から届く。
- `SceneBlockPlanResult.Success`/`Failure`は`internal static`（[SceneBlockPlanResult.cs:28,43](../../Assets/SymphonyFrameWork/Runtime/Service/SceneBlock/Internal/Domain/SceneBlockPlanResult.cs)）。`SceneBlockGraphPlanner.Plan`経由でのみ生成されるため、`SceneBlockAssetPlanner`は`SceneBlockGraphPlanner.Plan`を呼ぶだけで直接は使わない。

## 依存方向

`SceneBlockEdgeAuthoring` / `SceneBlockAssetPlanner` / `SceneBlockAsset`はInfrastructure層で、Round 110-1のDomain型（`SceneBlockEdge` / `SceneBlockPlanResult` / `SceneBlockGraphPlanner`）へ依存する。`SceneBlockAssetPlanner`はUnity APIに触れない（`SceneBlockGraphPlanner`と同じ制約）。`SceneBlockAsset`だけが`UnityEngine.ScriptableObject`/`SerializeField`に依存する。`SceneBlockAssetDrawer`（Editor）はRuntimeの`SceneBlockAsset`へ一方向に依存し、`Core → Runtime → Editor`の向きを保つ。他サブシステム（`SceneLoad`など）への参照は持たない。

## エラー処理

**`SceneBlockAssetPlanner.Plan`は、Round 110-1の`SceneBlockGraphPlanner.Plan`より寛容な契約にする。** 理由は、Authoringデータ（Inspectorで編集中のアセット）は構造上「入力途中の空欄」を含みうるためである。生成直後の`SceneBlockAsset`は`_sceneIds`/`_edges`が空配列であり、辺を1件追加した直後は`From`/`To`のどちらかが未選択（空文字列）になりうる。これらを`SceneBlockGraphPlanner`の厳格な契約（`null`や空白は`ArgumentException`）にそのまま渡すと、Inspectorを開いただけで例外や誤ったエラー表示が出る。

`SceneBlockAssetPlanner.Plan(IReadOnlyList<string> sceneIds, IReadOnlyList<SceneBlockEdgeAuthoring> edges)`の契約:

1. **`sceneIds`が`null`の場合は空一覧として扱う。** 例外を投げない。
2. **`edges`が`null`の場合は空一覧として扱う。** 例外を投げない。
3. `sceneIds`の要素のうち`null`または空白文字列は**除外**する（ノードとして数えない）。「まだ入力していない行」として扱い、エラーにしない。
4. `edges`の要素のうち`From`または`To`が`null`または空白文字列のものは**除外**する（辺として数えない）。同様に「未入力の行」として扱う。
5. 上記で除外しなかった`sceneIds`と`edges`を`SceneBlockEdge`へ変換し、`SceneBlockGraphPlanner.Plan(...)`へそのまま渡す。**それ以外の検証（重複、自己依存、循環、"空白ではないが存在しない識別子への参照"）はRound 110-1の契約をそのまま使う。** 空白除外は「未入力」と「誤字による欠落参照」を区別するためのものであり、後者（例: `"Ghost"`という実在しないシーンIDへの参照）は引き続き`MissingReference`として報告される。

**`SceneBlockAsset.Plan()`はこの関数への1行の委譲のみ。** 独自の分岐を持たないため、`SceneBlockAsset`自体は単体テスト対象にしない（[design-doc.md](../../.agents/skills/implement/references/design-doc.md)の「ロジックはUnity APIへ触れない純粋な型へ切り出し、そちらをテスト対象にする」に従い、ロジックは全て`SceneBlockAssetPlanner`にある）。

## 影響範囲

新規追加のみで、既存の公開API、シリアライズ形式、Round 110-1のDomain型、既存Runtimeコードへの影響は無い。

## テストの置き場と種別

`Assets/SymphonyFrameWork/Tests/Editor/`へEditModeテストを追加する（`SceneBlockAssetPlanner`と`SceneBlockEdgeAuthoring`はUnity APIに実質触れないため、PlayModeテストは不要）。**テストの実装は必須。**`InternalsVisibleTo`により`internal`な型を直接テストする。既存の`SceneBlockGraphPlannerTests.cs`と同じ`namespace SymphonyFrameWork.Tests`、`public sealed class`の形式に揃える。

| ファイル | テストメソッド | 検証内容 | 書き方 |
| --- | --- | --- | --- |
| `SceneBlockEdgeAuthoringTests.cs` | `Constructor_ExposesFromAndTo` | コンストラクタに渡した値が`From`/`To`へ反映される | `new SceneBlockEdgeAuthoring("A", "B")`を生成し`Assert.That(edge.From, Is.EqualTo("A"))`・`Assert.That(edge.To, Is.EqualTo("B"))` |
| `SceneBlockAssetPlannerTests.cs` | `Plan_ValidLinearChain_ReturnsSequentialLayers` | 正常な`A→B→C`が`[[A],[B],[C]]`になる（Round 110-1と同じ入力を委譲経由で確認） | `sceneIds: ["A","B","C"]`、`edges: [new("A","B"), new("B","C")]`で`Plan`を呼び`result.Layers`を`Is.EqualTo`で比較 |
| | `Plan_NullSceneIds_TreatedAsEmpty` | `sceneIds`が`null`でも例外を投げず成功する | `Plan(null, Array.Empty<SceneBlockEdgeAuthoring>())`を呼び`Assert.DoesNotThrow`相当（呼び出し自体をtry無しで実行し例外が飛ばないことを確認）、`result.IsSuccess`が`true`かつ`result.Layers`が空 |
| | `Plan_NullEdges_TreatedAsEmpty` | `edges`が`null`でも例外を投げず、ノードだけの1層になる | `Plan(new[]{"A"}, null)`を呼び`result.IsSuccess`が`true`、`result.Layers`が`[["A"]]` |
| | `Plan_BlankSceneIdEntries_ExcludedFromGraph` | 空文字列・空白・`null`のシーンIDは検証対象から除外され、有効な`"A"`だけの1層になる | `sceneIds: ["A", "", "  ", null]`、`edges`無しで`result.Layers`が`[["A"]]`かつ`result.Errors`が空 |
| | `Plan_EdgeWithBlankEndpoint_ExcludedFromGraph` | `From`または`To`が空白の辺は無視され、独立した2ノードとして扱われる | `sceneIds: ["A","B"]`、`edges: [new("A",""), new(null,"B")]`で`result.IsSuccess`が`true`、`result.Layers`が`[["A","B"]]`（辺が無いため同一層） |
| | `Plan_EdgeReferencingMissingRealSceneId_ReturnsMissingReferenceError` | 空白ではない実在しない識別子への参照は、空白除外の影響を受けず`MissingReference`として報告される | `sceneIds: ["A"]`、`edges: [new("A","Ghost")]`で`result.Errors`に`Kind==MissingReference && NodeIds[0]=="Ghost"`が1件 |
| | `Plan_DuplicateSceneIds_ReturnsDuplicateNodeError` | 重複ノードの検出はRound 110-1の契約のまま機能する | `sceneIds: ["A","A"]`、`edges`無しで`result.Errors`に`Kind==DuplicateNode`が1件 |
| `SceneBlockAssetDrawerTests.cs` | `CreateEditor_ForSceneBlockAsset_ResolvesToSceneBlockAssetDrawer` | `[CustomEditor(typeof(SceneBlockAsset))]`の配線が機能し、`SceneBlockAsset`に対して`SceneBlockAssetDrawer`が解決される | `ScriptableObject.CreateInstance<SceneBlockAsset>()`でアセットを生成し、`UnityEditor.Editor.CreateEditor(asset)`の戻り値を`Assert.That(editor, Is.TypeOf<SceneBlockAssetDrawer>())`で検証する。`[TearDown]`で`Object.DestroyImmediate(editor)`・`Object.DestroyImmediate(asset)`を呼び後始末する |

## 動作確認手順

### 自動確認

1. `python scripts/verify_round.py`でUnityコンパイル、Console、EditModeテストを確認する。
2. コンパイルがエラー0・警告0、EditModeが全数成功すること（既存499件 + 本Round分の増加）。

### 人が操作して確認する項目

Editor Inspectorの表示・追従はGUIの押下を自動化できないため（[design-doc.md](../../.agents/skills/implement/references/design-doc.md)参照）、次はスクリーンショットと表示要素の実値読み取りで確認する。

1. `uloop-execute-dynamic-code`で`SceneBlockAsset`のインスタンスを`ScriptableObject.CreateInstance<SceneBlockAsset>()`→`AssetDatabase.CreateAsset(...)`により一時アセットとして作成し、Projectウィンドウで選択してInspectorを開く。スクリーンショットで、シーンID一覧・辺一覧の編集UIと追加/削除ボタンが表示されることを確認する。
2. シーンIDのPopupが、Build Settingsに登録済みのシーン名（`NewScene` / `Scene2` / `Scene3`）だけを選択肢に持つことを、表示要素の実値読み取りで確認する。
3. シーンIDを2件追加後、辺を`A→B`→`B→A`の順に追加して循環を作り、`CyclicDependency`のHelpBoxが両方のシーンID名を含めて表示されることをスクリーンショットで確認する。
4. **Inspectorを開いたまま**（閉じて開き直さず）、循環を作る片方の辺を削除し、HelpBoxが消えて層数を示す成功表示に切り替わることを確認する（開いたままのウィンドウが追従するかの確認）。
5. 辺を1件追加した直後（`From`だけ選択し`To`が未選択のまま）、エラー表示が出ないことを確認する（空白除外の視覚的確認）。
6. 一時アセットは確認後に`AssetDatabase.DeleteAsset`で削除する（プロジェクトへ残さない）。

## バージョン判断

**パッチ更新。** 公開API・シリアライズ形式の変更は無く、`internal`な実装の追加のみ（Round 110-1と同じ扱い）。

## この Round で触るバージョン関連ファイル

- `Assets/SymphonyFrameWork/package.json`の`version`（6.4.1 → 6.4.2）
- `Assets/SymphonyFrameWork/CHANGELOG.md`に`## [6.4.2]`の見出しを追加

`README.md`の「現在のバージョン」表記は、存在する場合のみRound 110-1と同様に確認する。`Documentation~/Modules/`と`Documentation~/EditorTools.md`は対象外とする。理由: 本Roundは`[CreateAssetMenu]`を持たず、利用側が到達できるEditorメニュー項目を追加しないため（上記「この Round では次を扱わない」参照）。ユーザーが到達可能な生成経路が確定するRound 110-4で、その時点の到達経路に応じて`EditorTools.md`とモジュール文書を新設する。

## Round分割の確認（rounds.md の検索結果）

削除・改名するメンバーは無い（新規追加のみ）。ワークスペース側`Documentation/`の記述更新の要否を次の検索で確認した。

```text
rg -n "README\.md|Documentation~/|EditorTools\.md|AgentUsage\.md" Documentation/ AGENTS.md
```

本Roundはワークスペース側`Documentation/`のいずれのファイルも変更しない（`Documentation/CodeGuidelines.md`の名前空間ツリーはRound 110-1で`SceneBlock`を追加済みで、本Roundは同じ名前空間内の追加のみのため変更不要）。

## 実施レポート

（Round完了後に追記する）
