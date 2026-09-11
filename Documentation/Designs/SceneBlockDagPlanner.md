# Scene Block DAG Planner（Round 110-1）

Issue: [#110](https://github.com/HIBIKI5201/SymphonyFramework/issues/110)

ロードマップ: [Documentation/IssueImplementationRoadmap.md](../IssueImplementationRoadmap.md) Phase 4 / Round 110-1

## Round分割の前提（優先順位の変更）

ロードマップの推奨順序は `#113 → #129 → #109 → #168 → #110 → #115` だが、`#129` Round 129-1 は
Generator DLLのビルドに`dotnet build`が必要という、AGENTS.md §7の禁止事項に触れる可能性がある判断を
含み、ユーザー確認待ちで止まっている（[RoslynSourceGeneratorPrototype.md](RoslynSourceGeneratorPrototype.md)参照）。
ロードマップ本文が明示的に許容している並べ替え（「#110は#129〜#168とコード上の直接依存がないため、
優先度を上げたい場合は#113の次へ移動できる」）に従い、本Roundは`#110`の最初のRoundへ着手する。
`#129`系の判断が確定するまで、`#110`を先行して進める。

## 目的

`#110` Scene Blockは、複数シーンの依存関係を検証してから並行ロードする機能である。既存の
`SceneLoadService.LoadScenes`（[SceneLoadService.cs](../../Assets/SymphonyFrameWork/Runtime/Service/SceneLoader/Internal/Application/SceneLoadService.cs)）は
複数`SceneLoadRequest`を同時に開始できるが、依存グラフの検証や層ごとの実行順序は持たない。

Round 110-1は、Scene Blockの土台となる**Unity APIに触れない純粋なDomainモデル**を用意する。
シーン識別子と依存辺（どのシーンがどのシーンより先にロードされる必要があるか）を表す不変な値と、
そこから以下を検出する`SceneBlockGraphPlanner`を実装する。

1. 重複するノード識別子
2. 自己依存（自分自身への依存辺）
3. 循環依存
4. 存在しないノードを参照する依存辺（欠落参照）

異常が無ければ、依存順を守った**トポロジカル層**（同じ層内は並行ロード可能なノードの集合）を返す。

この Round では次を扱わない。

- `SceneBlock` ScriptableObjectとEditor UI（Round 110-2）
- `SceneLoadService`との統合、並行ロード、進捗、キャンセル（Round 110-3）
- 公開API、Info/Dto、Administrator、MCP診断（Round 110-4）

Round 110-1の成果物はすべて`internal`であり、利用側から見える変更は無い。

## 公開API

追加・変更する`public`APIは無い。本Roundの型はすべて`internal`にする
（[DesignPhilosophy.md](../DesignPhilosophy.md)「公開範囲」: フレームワーク内部だけが使う値と算出ロジックは`internal`にする）。

## ファイル構成

新しいサブシステム`SceneBlock`を`System`名前空間へ追加する。既存の`SceneLoader`と並ぶ独立したサブシステムとして扱う理由は、Scene Blockが依存グラフの検証という別責務を持ち、Round 110-3で初めて`SceneLoadService`と統合されるため。

| パス | 種別 | 内容 |
| --- | --- | --- |
| `Assets/SymphonyFrameWork/Runtime/Service/SceneBlock/Internal/Domain/SceneBlockEdge.cs` | Value Object | 依存辺 `{ string From; string To }`（`To`は`From`のロード完了後にロード可能）。`IEquatable<SceneBlockEdge>`実装 |
| `Assets/SymphonyFrameWork/Runtime/Service/SceneBlock/Internal/Domain/SceneBlockPlanErrorEnum.cs` | Enum | `DuplicateNode` / `SelfDependency` / `MissingReference` / `CyclicDependency` |
| `Assets/SymphonyFrameWork/Runtime/Service/SceneBlock/Internal/Domain/SceneBlockPlanError.cs` | Value Object | `{ SceneBlockPlanErrorEnum Kind; IReadOnlyList<string> NodeIds }`。`NodeIds`は種別ごとに意味が変わる（下記エラー処理を参照） |
| `Assets/SymphonyFrameWork/Runtime/Service/SceneBlock/Internal/Domain/SceneBlockPlanResult.cs` | Value Object | `{ bool IsSuccess; IReadOnlyList<IReadOnlyList<string>> Layers; IReadOnlyList<SceneBlockPlanError> Errors }`。生成は`internal static`の`Success(layers)`/`Failure(errors)`ファクトリに限定し、コンストラクタは`private` |
| `Assets/SymphonyFrameWork/Runtime/Service/SceneBlock/Internal/Domain/SceneBlockGraphPlanner.cs` | Domain（純粋アルゴリズム） | `internal static SceneBlockPlanResult Plan(IReadOnlyList<string> nodeIds, IReadOnlyList<SceneBlockEdge> edges)` |
| `Assets/SymphonyFrameWork/Tests/Editor/SceneBlockEdgeTests.cs` | EditModeテスト | `SceneBlockEdge`の等価性 |
| `Assets/SymphonyFrameWork/Tests/Editor/SceneBlockPlanResultTests.cs` | EditModeテスト | `Success`/`Failure`ファクトリの契約 |
| `Assets/SymphonyFrameWork/Tests/Editor/SceneBlockGraphPlannerTests.cs` | EditModeテスト | 正常系のトポロジカル層と全異常系 |

新しいasmdefは作らない。既存の`SymphonyFrameWork.asmdef`（Runtime）へ追加する。`SceneLoader`ディレクトリの構成（`Internal/Domain/` `Internal/Application/`...）に揃え、本Roundでは`Internal/Domain/`だけを作る。

### 名前空間ドキュメントの更新

[Documentation/CodeGuidelines.md](../CodeGuidelines.md)の「名前空間」節にある`System`配下のツリー図（`Audio` `Pause` `SaveData` `SceneLoad` `ServiceLocate`）に`SceneBlock`を追加する。名前空間は`SymphonyFrameWork.System.SceneBlock`。

## 依存方向

Domain層のみ。`SceneBlockEdge` / `SceneBlockPlanErrorEnum` / `SceneBlockPlanError` / `SceneBlockPlanResult` / `SceneBlockGraphPlanner`はいずれもピュアC#で、`UnityEngine` / `UnityEditor`を参照しない。他サブシステム（`SceneLoad`など）への参照も持たない。Round 110-3で`SceneLoadService`（Application層）から呼び出される想定だが、本Roundでは呼び出し元を作らない。

## エラー処理

**不変条件違反（例外）と通常の失敗（Try pattern相当の結果値）を区別する。**

- **引数契約違反は例外にする。** `nodeIds`が`null`、`edges`が`null`、`nodeIds`の要素に`null`または空白文字列が含まれる場合は`ArgumentException`（または`ArgumentNullException`）を投げる。これはCallerのプログラミング誤りであり、Round 110-2以降でアセット側のバリデーションが別途担当する「データとして間違っている」場合とは区別する。
- **グラフの内容が不正な場合は例外にせず`SceneBlockPlanResult.Failure`で返す。** 依存グラフはRound 110-2で`SceneBlock`アセットの著者が作成するデータであり、誤りが起こり得る通常の失敗として扱う。

`SceneBlockPlanError.NodeIds`の意味:

| `Kind` | `NodeIds`の内容 |
| --- | --- |
| `DuplicateNode` | 重複していたノード識別子1件（1要素） |
| `SelfDependency` | 自己依存していたノード識別子1件（1要素、`edge.From == edge.To`） |
| `MissingReference` | 辺が参照しているが`nodeIds`に存在しない識別子1件（1要素）。同じ識別子が複数の辺から参照されていても、識別子ごとに1件へ集約する |
| `CyclicDependency` | 循環に関与している残りのノード識別子（Kahnのアルゴリズムで入次数が0にならなかったノード集合。並び順はアルファベット順）。厳密な単一サイクルの経路までは求めない |

**検出は排他的ではなく、見つかった異常をすべて集めて返す。** 1回の`Plan`呼び出しで複数種別のエラーが同時に見つかった場合、`Errors`へすべて含める。ただし循環検出は、自己依存や欠落参照を含む辺を対象から除外してから行う（既に個別のエラーとして報告済みの辺が、無関係な循環検出の巻き添えにならないようにする）。

### アルゴリズム

1. `nodeIds`の重複を検出する（`DuplicateNode`）。以降の処理は重複を1つにまとめた集合を「有効なノード集合」として扱う。
2. 各`edge`について、`From == To`なら`SelfDependency`を記録し、その辺を以降の循環検出対象から除外する。
3. 各`edge`について、`From`または`To`が有効なノード集合に存在しなければ`MissingReference`を記録し（識別子ごとに重複しないよう集約）、その辺を以降の循環検出対象から除外する。
4. 残った辺と有効なノード集合でKahnのアルゴリズム（入次数0のノードを1層ずつ取り除く）を実行する。
   - 同じ層に複数ノードが含まれる場合は識別子の`StringComparer.Ordinal`昇順で安定した順序にする（テストの期待値を決定的にするため。ロード優先度そのものはRound 110-3以降の対象で、本Roundでは並び順の意味を持たせない）。
   - 全ノードを層へ割り当てられた場合は`Layers`を確定する。
   - 割り当てられずに残ったノードがあれば`CyclicDependency`を1件記録し、`NodeIds`へ残ったノード識別子（同じくOrdinal昇順）を入れる。
5. `Errors`が1件でもあれば`SceneBlockPlanResult.Failure(errors)`、無ければ`SceneBlockPlanResult.Success(layers)`を返す。

## 影響範囲

新規追加のみで、既存の公開API、シリアライズ形式、`SceneLoadService`を含む既存Runtimeコードへの影響は無い。

## テストの置き場と種別

`Assets/SymphonyFrameWork/Tests/Editor/`へEditModeテストを追加する（Unity APIに触れないため、PlayModeテストは不要）。**テストの実装は必須。**

| ファイル | テストメソッド | 検証内容 | 書き方 |
| --- | --- | --- | --- |
| `SceneBlockEdgeTests.cs` | `Equals_SameFromAndTo_ReturnsTrue` | `From`/`To`が同じ2つの`SceneBlockEdge`が等価 | `new SceneBlockEdge("A","B")`を2つ生成し`Assert.That(a, Is.EqualTo(b))` |
| | `Equals_DifferentTo_ReturnsFalse` | `To`が違えば非等価 | 上と同様に`Is.Not.EqualTo` |
| `SceneBlockPlanResultTests.cs` | `Success_WithLayers_IsSuccessAndHasNoErrors` | `Success`が`IsSuccess=true`、渡した`Layers`を保持し`Errors`が空 | `SceneBlockPlanResult.Success(layers)`を生成し3プロパティを`Assert.That`で確認 |
| | `Failure_WithErrors_IsNotSuccessAndHasNoLayers` | `Failure`が`IsSuccess=false`、渡した`Errors`を保持し`Layers`が空 | 同様 |
| `SceneBlockGraphPlannerTests.cs` | `Plan_LinearChain_ReturnsSequentialLayers` | `A→B→C`が`[[A],[B],[C]]`になる | ノード`["A","B","C"]`、辺`[(A,B),(B,C)]`で`Plan`を呼び`Layers`を`Is.EqualTo`で比較 |
| | `Plan_DiamondDependency_GroupsIndependentNodesInSameLayer` | ロードマップ例`A→B,C` `B→D,E`が`[[A],[B,C],[D,E]]`相当になる（`D`が`B`のみに依存し`E`が`C`のみに依存する場合、両方とも2層目で入次数0になるため実際は`[[A],[B,C],[D,E]]`) | 辺`[(A,B),(A,C),(B,D),(C,E)]`で確認。層内の順序はOrdinal昇順を期待値にする |
| | `Plan_NoNodesNoEdges_ReturnsSuccessWithNoLayers` | 空グラフは成功かつ`Layers`が空 | `Plan(Array.Empty<string>(), Array.Empty<SceneBlockEdge>())` |
| | `Plan_IsolatedNode_ReturnsSingleLayer` | 辺の無い単独ノードは1層1件になる | ノード`["A"]`、辺なし |
| | `Plan_DuplicateNodeId_ReturnsDuplicateNodeError` | 重複ノードが`DuplicateNode`エラーになる | ノード`["A","A"]`、辺なしで`Errors`に`Kind==DuplicateNode && NodeIds[0]=="A"`が1件 |
| | `Plan_SelfDependency_ReturnsSelfDependencyError` | `A→A`が`SelfDependency`エラーになる | 辺`[(A,A)]` |
| | `Plan_EdgeReferencesMissingNode_ReturnsMissingReferenceError` | 存在しないノードへの辺が`MissingReference`エラーになる | ノード`["A"]`、辺`[(A,"Ghost")]`で`NodeIds[0]=="Ghost"`を確認 |
| | `Plan_EdgeReferencedByMultipleEdges_ReturnsSingleMissingReferenceError` | 同じ欠落識別子が複数辺から参照されても1件に集約される | ノード`["A","B"]`、辺`[(A,"Ghost"),(B,"Ghost")]`で`Errors.Count(e => e.Kind == MissingReference) == 1` |
| | `Plan_TwoNodeCycle_ReturnsCyclicDependencyError` | `A→B→A`が`CyclicDependency`エラーになり、両ノードが`NodeIds`に含まれる | 辺`[(A,B),(B,A)]` |
| | `Plan_ThreeNodeCycle_ReturnsCyclicDependencyErrorWithAllInvolvedNodes` | `A→B→C→A`で3ノードすべてが`NodeIds`に含まれる | 辺`[(A,B),(B,C),(C,A)]` |
| | `Plan_CycleWithUnrelatedValidNode_DoesNotAffectIndependentNode` | 循環に無関係な独立ノードは`CyclicDependency`の`NodeIds`へ含まれない | ノード`["A","B","X"]`、辺`[(A,B),(B,A)]`（`X`は無関係） |
| | `Plan_SelfDependencyEdge_ExcludedFromCycleDetection` | 自己依存の辺があっても、それだけでは無関係な残りのグラフに循環と誤検出されない | ノード`["A","B"]`、辺`[(A,A),(A,B)]`で`Errors`に`CyclicDependency`が含まれないことを確認（`SelfDependency`のみ） |
| | `Plan_NullNodeIds_ThrowsArgumentException` | `nodeIds`が`null`なら例外 | `Assert.Throws<ArgumentNullException>` |
| | `Plan_NodeIdIsNullOrWhitespace_ThrowsArgumentException` | 空白のノード識別子は契約違反として例外 | `Assert.Throws<ArgumentException>` |

`InternalsVisibleTo`により、テストアセンブリから`internal`な`SceneBlockGraphPlanner`等を直接呼び出す。既存の`SceneLoadEntityTests.cs`と同じ`namespace SymphonyFrameWork.Tests`、`public sealed class`の形式に揃える。

## 動作確認手順

### 自動確認

1. `python scripts/verify_round.py`でUnityコンパイル、Console、EditModeテストを確認する（PlayModeへの影響は無いが、既存スイートの回帰が無いことは2往復の確認に含める）。
2. コンパイルがエラー0・警告0、EditModeが全数成功することを確認する。

### 人が操作して確認する項目

無し。本Roundは純粋なDomainロジックのみで、Editor UIの追加・変更を含まない。

## バージョン判断

**パッチ更新。** 公開API・シリアライズ形式の変更は無く、内部実装の追加のみ（6.2.1の内部カタログ追加と同様の扱い）。Round 110-2以降で公開APIが確定した時点で、その時点の変更内容に応じてマイナー更新を検討する。

## この Round で触るバージョン関連ファイル

- `Assets/SymphonyFrameWork/package.json`の`version`（6.4.0 → 6.4.1）
- `Assets/SymphonyFrameWork/CHANGELOG.md`に`## [6.4.1]`の見出しを追加
- `README.md`の「現在のバージョン」表記（存在する場合。無ければ対象外と明記して良い）

`Documentation~/Modules/`と`Documentation~/EditorTools.md`は対象外（公開APIが無いため、利用者向けドキュメントに追記する内容が無い）。Round 110-4で公開APIが確定した時点でモジュール文書を新設する。

## Round分割の確認（rounds.md の検索結果）

削除・改名するメンバーは無い（新規追加のみ）。ワークスペース側`Documentation/`の記述更新は次の検索で確認した。

```text
rg -n "README\.md|Documentation~/|EditorTools\.md|AgentUsage\.md" Documentation/ AGENTS.md
```

本Roundで触れるのは`Documentation/CodeGuidelines.md`の名前空間ツリーへの`SceneBlock`追加のみ。`AGENTS.md`・`CONTRIBUTING.md`側の記述更新は無い（Editor機能・公開APIがまだ無いため）。

## 実施レポート

実施日: 2026-08-30 / バージョン: 6.4.1 / PR: 未作成（submodule feature/110-scene-block-dag-planner → develop）

### 実装した内容

設計書どおり、`Assets/SymphonyFrameWork/Runtime/Service/SceneBlock/Internal/Domain/`配下へ`SceneBlockEdge`・`SceneBlockPlanError`・`SceneBlockPlanErrorEnum`・`SceneBlockPlanResult`・`SceneBlockGraphPlanner`の5ファイルを実装した（前回セッションでCodex CLIワーカーが実装済みのものを、今回のセッションで検証・コミットした）。`SceneBlockGraphPlanner.Plan`はKahnのアルゴリズムでトポロジカル層を算出し、重複ノード・自己依存・欠落参照・循環依存を`SceneBlockPlanResult`のエラー一覧として返す。`Tests/Editor/`配下へ`SceneBlockEdgeTests.cs`・`SceneBlockPlanResultTests.cs`・`SceneBlockGraphPlannerTests.cs`（テストメソッド計20件）を追加した。設計書のテスト一覧と実装済みテストメソッド名を突き合わせ、全項目が対応することを確認した。

### 設計から変えた点

無し。実装はワーカーへ委譲したセッションで完了しており、今回のセッションはUnity検証とコミットのみを行った。

### 検証結果

`python scripts/verify_round.py --skip-playmode --json`（本Roundは`SceneLoadService`統合を含まないためPlayMode不要と判断）:

- `compile`: `errors: 0`, `warnings: 0`（2回問い合わせの確定値）
- `tests` (EditMode): `total: 499`, `passed: 499`, `failed: 0`, `skipped: 0`（うち`SceneBlock`関連は`--filter-value ".*SceneBlock.*"`で個別実行し20件全数成功したことを別途確認）
- `enterPlayModeOptions`: `"OK"`（Domain/Scene Reload両方無効のまま）
- `release_round.py preflight`: 全項目OK（`[tests] OK: ソース1件に対しテスト6件を変更`、`[meta] OK: 8件`、`[docs] OK: 生成物は正本と同期しています`ほか）

### 未実施の確認

- PRの作成（`gh pr create`）と`release_round.py finalize`（`develop`へのマージ、親リポジトリのgitlink更新・push）は未実施。本自律実行タスクの指示「pushは行わない」に従い、共有状態を変更するマージ・gitlink push操作は人の確認を待つ。submoduleのfeatureブランチへのpushは`release_round.py commit`の不可分な一部として発生済み。
- 「人が操作して確認する項目」は設計書どおり「無し」（Editor UIの追加・変更を含まないRoundのため）。

### 振り返り

- 前回まで4回連続で再現していた「uloop MCPサーバーがUnity起動後も応答しない」環境障害は、今回は再現しなかった。原因は前回セッションの調査（`.uloop/outputs/VibeLogs/`が存在しない）ではなく、`VibeLogger`の全ログメソッドが`[Conditional(ULOOPMCP_DEBUG系シンボル)]`で通常ビルドでは呼び出し自体が除去される仕様だったため、そのディレクトリの不在は障害の証拠になっていなかった。今回は`uloop-cli launch`の180秒待ちがサーバー起動完了より早くタイムアウトしただけで、数十秒待って`get-logs`を再試行すると接続できた。**次回同様の症状が出た場合は、`launch`のタイムアウトで即座に「環境障害」と断定せず、まず時間を空けて`get-logs`を再試行することを優先する。** ロードマップやこのスキルへの反映は提案にとどめ、今回は反映していない（ユーザー確認後に検討）。
- CHANGELOG.mdの`### Add`詳細節は`release_round.py bump`では自動生成されず（見出しと要約のみ）、手で追記が必要だった。`bump`のヘルプにもその旨の明記が無く、初回は見落としかけた。頻出するなら`bump`のヘルプメッセージへ「`### Add`は手で追記する」の一文を足す提案の余地があるが、今回は提案にとどめる。
