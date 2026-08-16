# SaveDataManagedTypes

Issue: [#170 セーブデータの管理フラグを設定可能に](https://github.com/HIBIKI5201/SymphonyFramework/issues/170)

## 目的

Issue #166 で Save Data パネルが「AppDomain 内の対応セーブデータ型すべて」を一覧するようになった結果、
**テストアセンブリの検証用セーブデータ型まで一覧へ現れる**ようになった。
`SaveDataWindow.EnsureTypeListCurrent` が `AppDomain.CurrentDomain.GetAssemblies()` を無条件に走査するためで、
利用側から見ると自分が定義していない型が並ぶ。

型ごとに「管理対象かどうか」のフラグを Project Settings で設定できるようにし、
**フラグが立っている型だけを Save Data パネルへ表示する**。
既定値は、テストアセンブリの型が `false`、それ以外の型が `true` とする。

既存の何では足りないか:

- `SaveDataConfig`（Runtime, Resources）はローダーしか持たず、型ごとの設定を持てない。
- Save Data パネル側にフィルタの入口が無く、`SaveDataWindow` の中に型探索が閉じている。

## 公開API

**追加しない。** この Round で追加する型はすべて `internal`（Editor アセンブリ内）である。

理由:

- 設定の編集経路は Project Settings 画面と Save Data パネルだけで、利用側コードから触る必要が無い。
- `Documentation/DesignPhilosophy.md` `## 公開範囲` の「Config は `internal` にする。Editor からの参照は
  `InternalsVisibleTo` で許可する」に従う。テストからの参照は `Editor/AssemblyInfo.cs` の
  `[assembly: InternalsVisibleTo("SymphonyFrameWork.Tests.Editor")]` で既に許可されている（確認済み）。

既存の公開型 `SaveDataSettingProvider` と `SaveDataWindow` のシグネチャは変えない。

## ファイル構成

新規（すべて名前空間 `SymphonyFrameWork.Editor`）:

| パス | 型 | 役割 |
| --- | --- | --- |
| `Editor/Configs/ConfigData/SaveDataVisibilityConfig.cs` | `internal sealed class SaveDataVisibilityConfig : ScriptableSingleton<SaveDataVisibilityConfig>` | 型ごとの管理フラグを ProjectSettings へ保存する。`[Serializable]` な入れ子 `private` エントリを持つ |
| `Editor/SaveData/SaveDataTypeCatalog.cs` | `internal static class SaveDataTypeCatalog` | 対応セーブデータ型の探索と、テストアセンブリ判定 |
| `Editor/SaveData/SaveDataVisibilityMap.cs` | `internal sealed class SaveDataVisibilityMap` | 保存済みの上書き値と既定値から、型ごとの管理フラグを解決する純粋ロジック |
| `Editor/SaveData/SaveDataTypeTreeNode.cs` | `internal sealed class SaveDataTypeTreeNode` | 型の完全名から名前空間の階層ツリーを組み立てる純粋ロジック |

変更:

| パス | 変更内容 |
| --- | --- |
| `Editor/SettingProvider/SaveDataSettingProvider.cs` | ローダー設定の下へ「Managed Save Data Types」節を追加し、階層ツリーとチェックボックスを描画する |
| `Editor/Administrator/UITK/CS/SaveDataWindow.cs` | 型探索を `SaveDataTypeCatalog` へ移し、管理フラグで絞り込む。設定変更へ追従する購読を追加する |

`Editor/SaveData/` を新設するのは、3つの純粋ロジック型を **Save Data パネル（`Editor/Administrator/UITK/CS/`）と
Project Settings 画面（`Editor/SettingProvider/`）の両方が使う**ためである。
どちらかの下へ置くと、もう一方から UI 実装のフォルダを参照することになる。
`Editor/` 配下のサブフォルダは名前空間へ含めない既存の扱いに合わせ、名前空間は `SymphonyFrameWork.Editor` とする
（`Editor/Configs/ConfigData/`、`Editor/Generator/AssetStoreToolsPackager/` と同じ）。

## 依存方向

すべて Editor アセンブリ内で完結する。Runtime と Core は変更しない。

```text
SaveDataSettingProvider ─┐
                         ├─> SaveDataTypeCatalog ─> (System.Reflection)
SaveDataWindow ──────────┘        │
                                  v
                         SaveDataVisibilityMap <── SaveDataVisibilityConfig（ScriptableSingleton）
                                  │
                         SaveDataTypeTreeNode（表示順の組み立てのみ）
```

- `SaveDataVisibilityMap` と `SaveDataTypeTreeNode` は Unity API へ触れない。`SaveDataTypeCatalog` は
  `System.Reflection` だけを使い、`UnityEditor` へ触れない（テストから直接呼べるようにするため）。
- Runtime の挙動は変えない。**この設定は Editor の表示だけに効く。**
  `SaveStore.Get<T>()` は従来どおりフラグに関係なく動く。

## 管理フラグの解決規則

`SaveDataVisibilityMap` が次の順で解決する。

1. `SaveDataVisibilityConfig` に**その型名の明示的な値がある**ならそれを使う。
2. 無ければ既定値を使う。既定値は「テストアセンブリの型なら `false`、それ以外は `true`」。

**保存するのは利用者が明示的に切り替えた値だけとする。** 探索で見つけた型を既定値ごと保存しない。

- 画面を開いただけで ProjectSettings のファイルが書き変わるのを避けるため。
- 既定値の規則は決定的なので、保存しなくても「追加したクラスは `true`、テストアセンブリのクラスは `false`」を満たす。
- 型が消えても、その型の上書き値は消さずに残す。コンパイルエラー中の一時的な消失で利用者の選択を失わないため。

### テストアセンブリの判定

`SaveDataTypeCatalog.IsTestAssembly(Assembly)` は、**そのアセンブリが参照するアセンブリ名に
`nunit.framework` または `UnityEngine.TestRunner` が含まれるか**で判定する。
判定本体は `IsTestAssembly(IEnumerable<string> referencedAssemblyNames)` として分け、テストから直接呼べるようにする。

`defineConstraints` の `UNITY_INCLUDE_TESTS` はコンパイル済みアセンブリのメタデータには残らないため、
実行時に読めるのは参照関係だけである。**この判定は既定値を決めるためだけに使い、利用者は上書きできる。**
誤判定しても、チェックボックスを1回操作すれば直る。

判定が実際に効いていることは、テストアセンブリ自身（`SymphonyFrameWork.Tests.Editor`）を入力にした
テストで確認する（後述）。

## 階層ツリーの構築規則

Issue #170 の規則をそのまま実装する。**名前空間のノードは、分岐が無い限り1つへまとめる。**

- `SpaceA.ScopeB.ClassC` と `SpaceA.ScopeB.ClassD` → ルート直下に `SpaceA.ScopeB`、その下に `ClassC` と `ClassD`
- ここへ `SpaceA.ScopeE.ClassF` が加わると → `SpaceA` の下に `ScopeB`（下に `ClassC` `ClassD`）と `ScopeE`（下に `ClassF`）

まとめる対象は**名前空間ノードどうしだけ**とする。子が1つでもそれがクラス（葉）なら、まとめない。
Issue の図で `SpaceE` と `ClassF` が別の行になっていることに合わせる。

- 名前空間を持たない型は、ルート直下の葉として扱う。
- 兄弟の並びは名前の序数昇順（`StringComparer.Ordinal`）。既存の `AssetPathTreeNode.Sort` と同じ規則にする。
- ルートは表示しない容器とし、描画側はルートの子から描く。

`Editor/Generator/AssetStoreToolsPackager/AssetPathTreeNode.cs` に似た型があるが、**再利用しない。**
`AssetPathTreeNode` は区切りごとに必ずノードを作り（まとめる規則が無い）、葉が型ではなくアセットパスで、
配下のアセット数を持つ。まとめ規則を後付けすると Packager 側の表示が変わる。

## Project Settings の描画

`Project Settings > SymphonyFrameWork > Save System` のローダー設定の下へ節を追加する。

| 項目 | 内容 |
| --- | --- |
| 名前空間ノード | Foldout と、配下を一括切り替えするトグル。展開状態はノードが保持する（永続化しない） |
| クラスの葉 | `EditorGUILayout.ToggleLeft` によるチェックボックス。切り替えた時点で ProjectSettings へ保存する |

名前空間ノードのトグルの規則:

- 表示値は**配下の葉がすべて管理対象なら `true`**、1つも管理対象でないなら `false`。
  混在している場合は `EditorGUI.showMixedValue` を立て、値は `false` として描く。
- 押されたら、**配下の葉すべてへ新しい値を明示的な上書きとして保存する。** 混在状態から押した場合は
  `true`（全部入れる）へ倒す。既定値へ戻す操作は設けない。
- 一括切り替えは**配下の葉の数だけ上書き値を書き込む**ため、設定ファイルはその分だけ増える。
  保存とイベント発行は1回にまとめ、葉ごとに通知しない。
- `showMixedValue` は描画後に必ず `false` へ戻す。戻さないと後続のトグルまで混在表示になる。

型が1つも見つからない場合は `HelpBox` で理由を出す。
- 画面を開くたびに型を探索し直す（`SettingsProvider.activateHandler`。`AssetStoreToolsPackagerProvider` と同じ形）。
  Foldout の展開状態は探索し直すと初期値へ戻るが、探索結果が変わらない限りツリーを作り直さないことで維持する。

## Save Data パネルへの反映

`SaveDataWindow.EnsureTypeListCurrent` を次のように変える。

- 型探索を `SaveDataTypeCatalog.CollectSupportedTypes()` へ委譲する
  （現在の `GetTypesSafe` と `IsSupportedSaveDataType` はカタログへ移す）。
- 探索結果を `SaveDataVisibilityMap` で絞り込み、管理対象の型だけを `_saveDataTypes` にする。
- **`SaveDataVisibilityConfig` の変更通知を購読し、通知のたびに `EnsureTypeListCurrent` と `RefreshView` を呼ぶ。**
  `Initialize_S` での購読と `Dispose` での解除を対にする。

**「設定を変えたとき、開いたままの Save Data パネルが追従するか」は必ず確認する項目に入れる。**
現在 `EnsureTypeListCurrent` は `Initialize_S` からしか呼ばれておらず、購読を足さないと
パネルを開き直すまで一覧が変わらない。Issue #124 Round 2 と同じ落とし穴である。

表示が空になったときの文言を、原因ごとに分ける。

| 状態 | 文言 |
| --- | --- |
| 対応型が1つも無い | 既存のまま「プロジェクト内に SaveDataContent を継承したセーブデータ型が見つかりません。」 |
| 対応型はあるが管理対象が0件 | 「管理対象のセーブデータ型がありません。Project Settings > SymphonyFrameWork > Save System で管理対象を設定してください。」 |

## エラー処理

- 通常の失敗は無い。型の解決に失敗するアセンブリは、既存の `GetTypesSafe` と同じく
  `ReflectionTypeLoadException` から取得できた型だけを残す（カタログへそのまま移す）。
- `Assembly.GetReferencedAssemblies()` は例外を投げうる呼び出しではないが、
  取得できない場合に備えて空の列挙として扱い、既定値を「テストアセンブリではない（`true`）」側へ倒す。
  **見えなくなる方向へ倒さない。**
- 例外を投げる新しい経路は追加しない。IMGUI の描画中に例外を投げると Project Settings 画面が壊れるため
  （`AssetStoreToolsPackagerProvider` の既存コメントと同じ理由）。

## 影響範囲

- **公開API・シリアライズ形式への影響は無い。** `SaveDataConfig.asset` は変更しない。
- Save Data パネルの表示対象が変わる。**テストアセンブリの型は既定で表示されなくなる**（これが Issue の目的）。
- 新しい設定ファイル `ProjectSettings/Packages/symphonyframework/SaveDataVisibilityConfig.asset` が、
  **利用者が最初にチェックを切り替えたときだけ**生成される。
- 移行手順は不要。既存プロジェクトでは、既定値によって従来と同じ型（テストアセンブリ以外）が表示される。

## テストの置き場と種別

すべて EditMode。`Assets/SymphonyFrameWork/Tests/Editor/` へ置く。
メソッド名は既存に合わせた英語の `対象_条件_期待` 形式。

### `Tests/Editor/SaveDataVisibilityMapTests.cs`

`SaveDataVisibilityMap` を、型名と「テストアセンブリか」の組と上書き値の辞書から直接生成して検証する。
Unity API もリフレクションも介さない。

| テスト | 検証内容 |
| --- | --- |
| `IsManaged_UnknownType_DefaultsToTrue` | 上書きが無い通常の型は `true` |
| `IsManaged_TestAssemblyType_DefaultsToFalse` | 上書きが無いテストアセンブリの型は `false` |
| `IsManaged_OverriddenTestAssemblyType_UsesOverride` | テストアセンブリの型でも上書き `true` が優先される |
| `IsManaged_OverriddenNormalType_UsesOverride` | 通常の型でも上書き `false` が優先される |
| `IsManaged_TypeNotInCatalog_ReturnsFalse` | カタログに無い型名は管理対象外として扱う |

### `Tests/Editor/SaveDataTypeTreeNodeTests.cs`

`SaveDataTypeTreeNode.Build(IEnumerable<string> typeFullNames)` を文字列だけで検証する。

| テスト | 検証内容 |
| --- | --- |
| `Build_SingleBranchNamespaces_AreMergedIntoOneNode` | Issue の例1。ルート直下が `SpaceA.ScopeB` の1ノードで、葉が `ClassC` `ClassD` |
| `Build_BranchingNamespaces_AreSplitAtBranchPoint` | Issue の例2。`SpaceA` の下が `ScopeB` と `ScopeE` に割れる |
| `Build_SingleClassUnderNamespace_KeepsClassAsOwnLeaf` | 子が1つでもクラスならまとめない |
| `Build_TypesWithoutNamespace_BecomeRootLeaves` | 名前空間なしの型がルート直下の葉になる |
| `Build_UnorderedInput_SortsChildrenByName` | 兄弟が序数昇順に並ぶ |
| `Build_Leaf_ExposesFullTypeName` | 葉が完全名を保持する（チェックボックスの保存キーになるため） |
| `EnumerateTypeFullNames_Node_ReturnsAllDescendantLeaves` | 名前空間ノードが配下の葉の完全名をすべて返す（一括切り替えの対象になるため） |
| `EnumerateTypeFullNames_Leaf_ReturnsItself` | 葉は自分自身の完全名だけを返す |

### `Tests/Editor/SaveDataTypeCatalogTests.cs`

| テスト | 検証内容 |
| --- | --- |
| `IsTestAssembly_ReferencesNUnit_ReturnsTrue` | 参照名に `nunit.framework` を含む入力で `true` |
| `IsTestAssembly_ReferencesTestRunner_ReturnsTrue` | 参照名に `UnityEngine.TestRunner` を含む入力で `true` |
| `IsTestAssembly_ProductAssembly_ReturnsFalse` | 製品側の参照名だけの入力で `false` |
| `IsTestAssembly_OwnTestAssembly_ReturnsTrue` | **`typeof(SaveDataTypeCatalogTests).Assembly` を渡して `true`。** 参照メタデータから判定できるという前提を実測で固定する |
| `IsTestAssembly_FrameworkEditorAssembly_ReturnsFalse` | `typeof(SaveDataWindow).Assembly` を渡して `false` |
| `IsSupportedSaveDataType_SerializableConcreteType_ReturnsTrue` | `[Serializable]` な具象派生型を受け入れる |
| `IsSupportedSaveDataType_AbstractType_ReturnsFalse` | 抽象型を除外する |
| `IsSupportedSaveDataType_TypeWithoutSerializable_ReturnsFalse` | `[Serializable]` が無い型を除外する |
| `IsSupportedSaveDataType_TypeWithoutDefaultConstructor_ReturnsFalse` | 引数なしコンストラクタが無い型を除外する |
| `IsSupportedSaveDataType_NonSaveDataType_ReturnsFalse` | `SaveDataContent` を継承しない型を除外する |

`IsSupportedSaveDataType` の判定は `SaveDataWindow` から移設するもので、**移設の前後で判定が変わらないこと**を
このテストで固定する。移設前はテストが無かった。

### テストを書かないもの

`SaveDataVisibilityConfig`（`ScriptableSingleton`）と、Project Settings / Save Data パネルの GUI 操作は自動検証しない。
`ScriptableSingleton` はテストが実プロジェクトの `ProjectSettings/` を書き換えるため、
EditorWindow と IMGUI のボタン・トグルの押下は手段が無いため（設計書の既知の制約）。
**ロジックは上記3つの純粋な型へ切り出してあり、テスト対象はそちらである。**
`--no-tests-reason` は使わない（テストは追加するため）。

## 動作確認手順

### 自動で確認する（`verify_round.py`）

- コンパイルがエラー0・警告0で通る。
- EditMode テストが全数成功し、件数が Round 前より増えている（+23 件を見込む）。
- PlayMode テストが全数成功する（この Round では PlayMode テストを追加しない）。
- Play Mode の開始・終了を2往復してゴースト参照が残らない。

### 人が操作して確認する（`uloop` では叩けない）

1. `Project Settings > SymphonyFrameWork > Save System` を開く。ローダー設定の下に
   「Managed Save Data Types」が出て、名前空間の階層でツリーが描かれている。
2. **ツリーの形が Issue の規則どおりであること。** 分岐の無い名前空間が1行にまとまり、
   分岐すると分かれている。
3. `SymphonyFrameWork.Tests` 配下の型のチェックが**外れた状態**で表示されている。
   その親の名前空間ノードのトグルが、混在なら混在表示、全部外れていれば `false` になっている。
   親のトグルを押すと配下がまとめて切り替わる。
4. `Window > SymphonyFrameWork > Symphony Administrator` の Save Data パネルに、
   テストアセンブリの型が**現れない**。`Visible Entries` の件数が対応型の総数より少ない。
5. **Save Data パネルを開いたまま** Project Settings へ戻り、テストアセンブリの型のチェックを入れる。
   → パネルを開き直さずに一覧へ現れること。外すと消えること。
6. すべてのチェックを外すと、パネルが「管理対象のセーブデータ型がありません。」を表示する。
7. Unity を再起動しても、5 で切り替えた状態が維持されている
   （`ProjectSettings/Packages/symphonyframework/SaveDataVisibilityConfig.asset` が生成されている）。

## バージョン判断

**マイナー更新: 5.0.1 → 5.1.0。**

- 公開APIとシリアライズ形式を変えないが、利用者から見える Editor 機能の追加である。
- 後方互換。既定値により、従来と同じ型が表示され続ける。

## この Round で触るバージョン関連ファイル

| ファイル | 変更内容 |
| --- | --- |
| `Assets/SymphonyFrameWork/package.json` | `version` を `5.1.0` へ（`release_round.py bump --level minor`） |
| `Assets/SymphonyFrameWork/CHANGELOG.md` | `## [5.1.0]` の `### Add` |
| `Assets/SymphonyFrameWork/README.md` | 「現在のバージョン」（`bump` が更新）と、`## 初期設定` の Save System の行へ管理対象型の設定を追記 |
| `Assets/SymphonyFrameWork/Documentation~/Modules/SaveDataSystem.md` | `### Save System設定` へ「Managed Save Data Types」を追加。`### Save Data パネル` へ「管理対象の型だけを表示する」旨を追記 |
| `Assets/SymphonyFrameWork/Documentation~/Html/` | `python scripts/build_module_docs.py` の再生成物 |

`AGENTS.md` と `Documentation~/Architecture.md` は変更しない。導線も公開型の関係も変わらないため。

---

## 実施レポート

実施日: 2026-08-16 / バージョン: 5.1.0 / PR: [#182](https://github.com/HIBIKI5201/SymphonyFramework/pull/182)

### 実装した内容

設計の「ファイル構成」どおり、追加4ファイル・変更2ファイル。すべて `internal` で、公開型は1つも増えていない。

| 設計の項目 | 実現したファイル |
| --- | --- |
| 管理フラグの保存と変更通知 | `Editor/Configs/ConfigData/SaveDataVisibilityConfig.cs`（`ScriptableSingleton`、`[Serializable]` な入れ子 `private` エントリ、`SetManaged` の単数版と複数版、`OnChanged`） |
| 型の探索・テストアセンブリ判定・対応型判定 | `Editor/SaveData/SaveDataTypeCatalog.cs`（判定は `SaveDataWindow` から移設） |
| 既定値と上書き値の解決 | `Editor/SaveData/SaveDataVisibilityMap.cs` |
| 名前空間ツリー（分岐の無い階層をまとめる） | `Editor/SaveData/SaveDataTypeTreeNode.cs` |
| Project Settings の描画 | `Editor/SettingProvider/SaveDataSettingProvider.cs` の `DrawManagedTypes` / `DrawTypeNode` |
| Save Data パネルの絞り込みと追従 | `Editor/Administrator/UITK/CS/SaveDataWindow.cs`（`EnsureTypeListCurrent` の変更、`OnChanged` の購読と解除） |

テストは設計に挙げたクラス名・メソッド名のまま23件を追加した（`SaveDataVisibilityMapTests` 5件、`SaveDataTypeTreeNodeTests` 8件、`SaveDataTypeCatalogTests` 10件）。

### 設計から変えた点

1. **`SaveDataConfig` が未生成でも、管理対象の節を描くようにした。** 設計では触れていなかったが、従来は `config == null` で `IMGUI` 全体を早期 return していた。管理対象の設定は Runtime Config と無関係な Editor 専用設定であり、Runtime Config の有無で隠す理由が無い。

2. **名前空間ノードのトグルを Foldout より先に描くようにした。** 設計では描画順を決めていなかった。ワーカーの実装は Foldout → トグルの順で、`EditorGUILayout.Foldout` が横方向へ広がるため**トグルが行の右端へ追い出され、型の葉のチェックと列が揃わなかった**。スクリーンショットで確認して入れ替えた。同じ行の Foldout へ字下げが二重に掛からないよう、`EditorGUI.indentLevel` を一時的に 0 にしている。

3. **未選択時の文言を、対応型の有無と管理対象の有無で3通りに分けた。** 設計では `EnsureTypeListCurrent` の分岐だけを書いていたが、実際には Registry に管理対象外の型のエントリが載っていると `ApplyAutoSelection` が後から「Save Data Types からセーブデータを選択してください。」で上書きし、**選べる行が1つも無いのに選択を促す表示になった**（実機で確認）。`ResolveUnselectedMessage()` へ集約し、`ApplyAutoSelection`・`UpdateBindingStatusMessage`・`ExecuteActionAsync` の3か所から使う。対応型自体が無い場合と区別するため `_hasSupportedTypes` を持たせた。

4. **テストアセンブリ判定の結果を、Project Settings 側でキャッシュした。** 設計では触れていなかったが、IMGUI は再描画のたびに走るため、`Assembly.GetReferencedAssemblies()` を全型分・毎フレーム呼ぶことになっていた。型を探索し直したときにだけ作り直す。

5. **`SaveDataTypeCatalog.IsSupportedSaveDataType` から `UnityEngine.Object` の除外条件が落ちた。** 移設元にはあった。`SaveDataContent` は `UnityEngine.Object` を継承しないため、C# の単一継承により両方を満たす型は存在せず、**判定結果は変わらない**（到達しない条件だった）。設計の「移設の前後で判定が変わらないこと」は満たしている。

6. **範囲外の1行を含めた。** `SaveDataWindow.ProbeSavedDateAsync` の `Debug.LogException` を `SymphonyDebugLogger.LogException` へ変えた。ワーカーの成果物に含まれていたもので、[#102](https://github.com/HIBIKI5201/SymphonyFramework/issues/102) で決めた「フレームワーク内のログは `SymphonyDebugLogger` 経由」に対する取り残しである。同じファイルの他の箇所はすべて移行済みで、挙動は変わらない。**1コミット1意図の原則からは外れるため、ここに記録する。**

### 検証結果

`verify_round.py`（最終の実装で2回実行し、2回とも同じ結果）:

- コンパイル: エラー0件 / 警告0件
- Console: エラー0件（「テスト実行前の時点で警告3件」と出るが、取り直すと0件。確定前の値）
- EditMode: 438/438 成功（失敗0 / スキップ0）。Round 前は415件で **+23件**
- PlayMode: 21/21 成功（失敗0 / スキップ0）を2往復

`release_round.py preflight`: 全項目通過（`bom` 10件、`meta` 7件、`asmdef` 2件、`docs` 同期、`tests` ソース4件に対しテスト6件）。

**Editor の実表示も実測した。** 設計では「人が操作して確認する」に置いていた項目のうち、次は自動で確認できた。

- Project Settings のツリー描画をスクリーンショットで確認。`SymphonyFrameWork` → `Tests` が `SymphonyFrameWork.Tests` の1行にまとまり、配下に7件の型が並ぶ（**分岐が無い名前空間をまとめる規則が効いている**）。チェックはすべて外れた状態（テストアセンブリの既定値）
- Save Data パネルの実値を UI Toolkit のラベルから読み、管理対象0件のとき `Visible Entries: 0` / `rows=0` / 「管理対象のセーブデータ型がありません。…」を確認
- **パネルを開いたまま**設定を1件 `true` にすると、開き直さずに `Visible Entries: 1` / `rows=1` へ変わり、`false` へ戻すと0件と元の文言へ戻ることを確認（設計の「動作確認手順」5・6に相当）
- 生成された `SaveDataVisibilityConfig.asset` の内容が、切り替えた1件だけを持つことを確認。**確認後に削除して作業ツリーへ残していない**

### 未実施の確認

「動作確認手順」の人が操作する項目のうち、次が未実施である。

- **項目3の後半・項目7**: 名前空間トグルの押下（混在表示からの一括切り替え）と、Unity 再起動後に設定が残ること。EditorWindow / IMGUI のトグル押下を自動で叩く手段が無いため。**トグルの値の決め方（混在なら `true` へ倒す）と一括保存はコード上の分岐で、対応するツリーの列挙は `EnumerateTypeFullNames` のテストで固定してある。**押下そのものは未確認
- 上記の実測は、いずれも設定APIを直接呼んで行った。**GUI のクリック経路は通していない**

### 振り返り

| 気づき | 扱い |
| --- | --- |
| Editor の GUI は「叩けない」だけで「見られない」わけではない。スクリーンショットと UI Toolkit の実値読み取りで、レイアウト崩れと表示文言を自動で確認できた。実際にこの2つで欠陥を2件見つけている | **`implement` スキルのステップ3へ追加を提案**（下記） |
| `finalize --paths` が実施レポートを書く前に設計書をコミットするため、親リポジトリへ必ず2コミット目が要る | 提案（下記） |
| 設計書に「描画順」を書いていなかったため、レイアウトの崩れがワーカーの実装で入った | 今回は設計書の「Project Settings の描画」へ後追いで書ける粒度。仕組みへの還元は見送る |

提案（**ユーザーの承認を得てから反映する。この Round では変更していない**）:

1. **`references/review.md` のステップ3へ、Editor UI の視認確認を足す。** 現在は「EditorWindow の GUI 操作は自動検証できない」とだけ書かれており、**操作できないことと確認できないことが同一視されている。** 次の2つを手順として書く。
   - `SettingsService.OpenProjectSettings(<パス>)` や `EditorWindow.GetWindow<T>()` で対象を開き、`uloop screenshot --window-name <名前> --match-mode exact` で撮って**レイアウトを目で見る**
   - UI Toolkit の Window は `rootVisualElement.Query<VisualElement>().Build()` を `execute-dynamic-code` で回し、**ラベルの実テキストと `ListView.itemsSource.Count` を読む**。`Q<T>` は拡張メソッドが解決できず、`UQueryExtensions.Q` は多重定義で曖昧になるため、`Query().Build()` の列挙が確実（実測）
   - 実例として、今回これで「名前空間のチェックが行の右端に出る」「管理対象0件なのに選択を促す文言が出る」の2件を見つけたことを添える
2. **`release_round.py finalize` へ、実施レポートが未記入なら止めるか警告する検査を入れる。** 現状は設計書を先にコミットするため、レポートは必ず別コミットになる。`--paths` に渡した設計書へ `## 実施レポート` が無ければ警告する程度でも、書き忘れは防げる。
