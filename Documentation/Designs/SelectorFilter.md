# セレクター属性のフィルター

## 目的

`[SceneNameSelector]`、`[TagSelector]`、`[SubclassSelector]` は、Inspectorのドロップダウンへ**プロジェクトに存在する候補をすべて**並べる。利用側が候補を絞る手段が無いため、次のような場面で目的外の値を選べてしまう。

- 「`UI_` で始まるシーンだけを選ばせたい」フィールドに、全シーンが並ぶ。
- 「敵のタグだけを選ばせたい」フィールドに、`MainCamera` や `Untagged` が並ぶ。
- `[SerializeReference]` の派生型のうち、その場面で使えない実装まで並ぶ。

既存の回避策は、利用側が独自のPropertyDrawerを書き直すことしかない。属性の描画そのものは同じで、候補の絞り込みだけが違うため、**Drawerごと複製させるのは過剰**である。

Issue [#199](https://github.com/HIBIKI5201/SymphonyFramework/issues/199)。属性の引数へフィルター関数を指定できるようにする。

## Round 分割

1 Round で完了する。3つのセレクター属性へフィルター引数を追加し、共通の解決処理を1つのEditor内部型へ置き、EditModeテストと版・CHANGELOG・モジュール文書を同時に更新する。

`develop` から `feature/199-selector-filter` を切る。

## 公開API

C#の属性引数はコンパイル時定数に限られるため、**デリゲートそのものは渡せない**。フィルターは**メソッド名の文字列**で指定し、Drawerが実行時に解決する。`nameof` を使えば綴りの誤りはコンパイル時に気づける。

```csharp
public sealed class SceneNameSelectorAttribute : PropertyAttribute
{
    public SceneNameSelectorAttribute(string filterMethodName = null);
    public string FilterMethodName { get; }
}

public sealed class TagSelectorAttribute : PropertyAttribute
{
    public TagSelectorAttribute(string filterMethodName = null);
    public string FilterMethodName { get; }
}

public sealed class SubclassSelectorAttribute : PropertyAttribute
{
    public SubclassSelectorAttribute(bool includeMono = false, string filterMethodName = null);
    public bool IsIncludeMono();
    public string FilterMethodName { get; }
}
```

利用側の書き方は次のとおり。

```csharp
public sealed class EnemySpawner : MonoBehaviour
{
    [SerializeField, TagSelector(nameof(IsEnemyTag))]
    private string _targetTag;

    [SerializeField, SceneNameSelector(nameof(IsBattleScene))]
    private string _battleScene;

    private static bool IsEnemyTag(string tag) => tag.StartsWith("Enemy");

    private bool IsBattleScene(string sceneName) => sceneName.StartsWith(_scenePrefix);
}
```

**フィルターメソッドの契約**は次のとおり。

| 項目 | 内容 |
| --- | --- |
| 宣言場所 | `SerializedObject.targetObject` の型、またはその基底型 |
| シグネチャ | `bool <名前>(string)`（`SceneNameSelector` / `TagSelector`）、`bool <名前>(Type)`（`SubclassSelector`） |
| アクセス修飾子 | 問わない。`private` でよい |
| static / instance | どちらでもよい。instanceの場合は `targetObject` を対象に呼ぶ |
| 戻り値 | `true` を返した候補だけをドロップダウンへ残す |

`FilterMethodName` を `public` にする根拠は、[DesignPhilosophy.md `### 公開範囲`](../DesignPhilosophy.md#公開範囲) の「利用側が自身のフィールドや型へ付けるInspector属性」である。属性は利用側が書く型であり、引数はその一部になる。解決と適用の処理は利用側が触らないため `internal` に留める。

**既存の引数無しの使い方はそのまま動く。** 引数は既定値付きの追加であり、`[SubclassSelector(true)]` のような既存の位置指定も意味が変わらない。

### 対象外

- **フィルターの型（`typeof(MyFilter)`）による指定は行わない。** Issueが求めているのは関数であり、フィルターごとに型を1つ作らせるのは、フィールドの隣にメソッドを1つ書くより重い。
- **入れ子の `[Serializable]` クラス側へフィルターメソッドを置く経路は用意しない。** 宣言場所を「`targetObject` の型とその基底型」の1つに固定する。入れ子クラスのインスタンスを `SerializedProperty` のパスから復元する処理が必要になり、規則も「どちらに書けばよいか」が読み手から見て曖昧になる。入れ子クラスのフィールドでも、フィルターはホスト側のMonoBehaviour／ScriptableObjectへ書く。

## ファイル構成

- 変更: `Assets/SymphonyFrameWork/Runtime/Attribute/SceneNameSelectorAttribute.cs`
- 変更: `Assets/SymphonyFrameWork/Runtime/Attribute/TagSelectorAttribute.cs`
- 変更: `Assets/SymphonyFrameWork/Runtime/Attribute/SubclassSelectorAttribute.cs`
  - いずれも名前空間 `SymphonyFrameWork.Attribute`。既定値付き引数と読み取り専用プロパティを追加する。
- 新規: `Assets/SymphonyFrameWork/Editor/AttributeDrawer/Internal/SelectorFilterUtility.cs`
  - 名前空間 `SymphonyFrameWork.Editor`。`internal static`。フィルターメソッドの解決、候補への適用、表示メッセージの生成を持つ。**`Internal/` 配下だが名前空間へ `Internal` は含めない**（[CodeGuidelines.md `## 名前空間`](../CodeGuidelines.md#名前空間)）。
- 変更: `Assets/SymphonyFrameWork/Editor/AttributeDrawer/SceneNameSelectorDrawer.cs`
- 変更: `Assets/SymphonyFrameWork/Editor/AttributeDrawer/TagSelectorDrawer.cs`
- 変更: `Assets/SymphonyFrameWork/Editor/Configs/Drawer/SubclassSelectorDrawer.cs`
- 新規: `Assets/SymphonyFrameWork/Tests/Editor/SelectorFilterUtilityTests.cs`
- 新規: `Assets/SymphonyFrameWork/Tests/Editor/SceneNameSelectorAttributeTests.cs`
- 新規: `Assets/SymphonyFrameWork/Tests/Editor/TagSelectorAttributeTests.cs`
- 新規: `Assets/SymphonyFrameWork/Tests/Editor/SubclassSelectorAttributeTests.cs`
- 変更: `Assets/SymphonyFrameWork/Tests/Editor/PublicTypeTestCoverageTests.cs`
  - テストを書いた3つの属性型を `UntestedPublicTypes` から消す。Drawerの3型は残す（`OnGUI` を自動で叩けないため）。
- 変更: `Assets/SymphonyFrameWork/Documentation~/Modules/InspectorAttributes.md`、`Documentation~/Html/`（再生成）
- 変更: `Assets/SymphonyFrameWork/CHANGELOG.md`、`package.json`、`README.md`（版）
- 設計記録: `Documentation/Designs/SelectorFilter.md`

新規 `.cs` の `.meta` は Unity に生成させる。

## 依存方向

`Runtime -> Core`、`Editor -> Runtime` の向きは変わらない。

- 属性は従来どおり `Runtime/Attribute/` に残り、`UnityEditor` を参照しない。追加するのは `string` のプロパティだけで、フィルターの解決と実行はEditor側だけが行う。
- `SelectorFilterUtility` は `System.Reflection` だけを使い、`UnityEditor` にも `UnityEngine` にも触れない。**Unity APIへ触れないため、EditModeテストからそのまま単体で検証できる。**
- `Editor/AssemblyInfo.cs` が `SymphonyFrameWork.Tests.Editor` へ `InternalsVisibleTo` を与えていること、テストの asmdef が `SymphonyFrameWork.Editor` を参照していることを確認済み。`internal` のままテストできる。

## エラー処理

フィルターの指定ミスは、**利用側のコードの誤り**であり、黙って全候補を表示すると気づけない。かといって `OnGUI` は毎フレーム走るため、ログを出すとConsoleが埋まる。**フィールドの位置へメッセージを表示する**（既存の「`SceneNameSelector`はstring型にのみ使用できます。」と同じ形）。

| 状況 | 扱い |
| --- | --- |
| `FilterMethodName` が `null` または空 | フィルター無し。全候補を表示する |
| 契約に合うメソッドが見つからない | 期待するシグネチャを含むメッセージを表示し、ドロップダウンを描かない |
| フィルターが例外を投げた | 例外メッセージを表示し、ドロップダウンを描かない |
| `targetObject` が取得できない | 対象を取得できない旨を表示し、ドロップダウンを描かない |
| フィルターが全候補を落とした | 「条件に一致する候補がありません。」を表示し、ドロップダウンを描かない |

**候補が空のときに値を書き換えない。** 既存Drawerは `property.stringValue = SceneList[selectedIndex]` を無条件に実行するため、候補が空の状態で描くと例外になる。既存の空判定（`SceneList.Length == 0`）と同じ位置でフィルター後の空も弾く。`TagSelector` には現在この判定が無いため追加する。

`null` の候補（`SubclassSelector` の先頭にある未設定の選択肢）は**フィルターへ渡さず常に残す**。利用側のフィルターが `null` を受け取って落ちるのを避けるためと、未設定へ戻す手段を消さないためである。

## 影響範囲

- 公開APIの**追加のみ**。既存のコンストラクタ呼び出し、シリアライズ形式、Inspectorの表示はフィルターを指定しない限り変わらない。
- `SubclassSelectorDrawer` の型候補キャッシュ（`s_TypeCache`）は**キーも中身も変えない**。フィルターはキャッシュから取り出した3つの並列配列へ、添字を揃えたマスクとして後段で適用する。instanceメソッドのフィルターが `targetObject` の状態に依存しても、キャッシュが古い結果を返すことはない。
- `TagSelectorDrawer` に候補が空のときの分岐が増える。フィルター未指定では `InternalEditorUtility.tags` が常に `Untagged` を含むため、到達しない。

## テストの置き場と種別

EditMode（`Assets/SymphonyFrameWork/Tests/Editor/`）へ置く。`SelectorFilterUtility` はUnity APIへ触れないため、**テスト用のプレーンなクラスをtargetの代わりに渡して直接呼ぶ**。Drawerの `OnGUI` は自動で叩けないため、Drawerからはロジックをこの型へ寄せ、Drawer自体は表示の組み立てだけを残す。

`SelectorFilterUtilityTests.cs`（テスト内に `private sealed class` のフィルター定義用スタブを置く）:

| テスト | どう書くか |
| --- | --- |
| `TryCreateMask_NoFilterName_KeepsAllCandidates` | `filterMethodName` に `null` と `""` を渡し、マスクが全 `true` で `true` が返ることを確認する |
| `TryCreateMask_StaticFilter_KeepsMatchingCandidates` | スタブの `private static bool` を名前で渡し、マスクの内容を配列比較する |
| `TryCreateMask_InstanceFilter_UsesTargetState` | スタブのインスタンスフィールドで判定を変え、targetの状態が反映されることを確認する |
| `TryCreateMask_BaseTypeFilter_IsFound` | 派生スタブをtargetにして、基底型のprivateメソッドが解決できることを確認する |
| `TryCreateMask_NullCandidate_IsKeptWithoutInvoking` | `null` を含む `Type[]` を渡し、`null` の位置が `true` かつフィルターの呼び出し回数が候補数-1であることを確認する |
| `TryCreateMask_TypeCandidates_MatchesTypeParameter` | `bool(Type)` のスタブで `Type[]` を絞れることを確認する |
| `TryCreateMask_MissingMethod_ReturnsFalseWithMessage` | 存在しない名前を渡し、`false` とメソッド名を含むメッセージを確認する |
| `TryCreateMask_WrongSignature_ReturnsFalseWithMessage` | 戻り値が `void` のメソッド名を渡し、`false` になることを確認する |
| `TryCreateMask_NullTarget_ReturnsFalseWithMessage` | targetに `null` を渡し、`false` とメッセージを確認する |
| `TryCreateMask_FilterThrows_ReturnsFalseWithMessage` | 例外を投げるスタブを渡し、`false` と例外メッセージを含むことを確認する。**例外はutilityが捕まえてメッセージへ変換するため、`LogAssert` は使わない** |
| `ApplyMask_KeepsOnlyMaskedElements` | 配列とマスクを渡し、残る要素と順序を確認する |
| `ApplyMask_LengthMismatch_ReturnsSource` | マスク長が違う場合に元の配列を返すことを確認する |

`SceneNameSelectorAttributeTests.cs` / `TagSelectorAttributeTests.cs`:

| テスト | どう書くか |
| --- | --- |
| `Constructor_WithoutArgument_HasNoFilter` | 引数無しで生成し、`FilterMethodName` が `null` であることを確認する |
| `Constructor_WithFilterName_ExposesFilterName` | 名前を渡して生成し、同じ値が読めることを確認する |
| `AttributeUsage_TargetsFieldWithoutMultiple` | `AttributeUsageAttribute` を反射で読み、`ValidOn` が `Field`、`AllowMultiple` が `false` であることを確認する |

`SubclassSelectorAttributeTests.cs` は上記に加えて、`IsIncludeMono()` の既定値が `false` であること、`includeMono` と `filterMethodName` を同時に渡せることを確認する。

Drawer 3型（`SceneNameSelectorDrawer` / `TagSelectorDrawer` / `SubclassSelectorDrawer`）は `PublicTypeTestCoverageTests.UntestedPublicTypes` に残す。`OnGUI` の描画結果を自動で読む手段が無いため。

## 動作確認手順

自動で確認する範囲:

1. `python scripts/verify_round.py` — コンパイル（エラー0・警告0）、EditModeとPlayModeの全数成功、Play Mode 2往復。
2. 新規EditModeテストの件数が増えていること。

人が操作して確認する範囲（Inspectorの表示のためスクリーンショットまでしか自動化できない）:

3. 検証用のMonoBehaviourへ、フィルター付きの `[TagSelector]` と `[SceneNameSelector]` を1つずつ書き、Inspectorのドロップダウンが**条件に一致する候補だけ**になること。
4. フィルターを指定しない同じ属性のフィールドが、従来どおり全候補を表示すること。
5. 存在しないメソッド名を渡したフィールドが、ドロップダウンではなく期待シグネチャのメッセージを表示すること。
6. 全候補を落とすフィルターで「条件に一致する候補がありません。」が表示され、**シリアライズ済みの値が書き換わらない**こと。
7. `[SerializeReference]` + `[SubclassSelector(filterMethodName: nameof(...))]` のフィールドで、候補が絞られたうえで先頭の `<null>` が残ること。既に保存済みの値が、フィルターで落ちても表示中に消えないこと。
8. **同じ属性を持つ2つのコンポーネントを同時にInspectorへ出し**、instanceフィルターがそれぞれのオブジェクトの状態で判定されること（`SubclassSelectorDrawer` のstaticキャッシュがフィルター結果を持ち越さないこと）。

## バージョン判断

**マイナー更新（6.4.0 → 6.5.0）。** 公開属性へ後方互換な引数とプロパティを追加する。既存の呼び出し、シリアライズ形式、既定の表示は変わらない。

## この Round で触るバージョン関連ファイル

| ファイル | 触る箇所 |
| --- | --- |
| `Assets/SymphonyFrameWork/package.json` | `version` を `6.5.0` へ |
| `Assets/SymphonyFrameWork/CHANGELOG.md` | `## [6.5.0]` の見出しと `### Add` |
| `Assets/SymphonyFrameWork/README.md` | 「現在のバージョン」の行 |
| `Assets/SymphonyFrameWork/Documentation~/Modules/InspectorAttributes.md` | 「Editor機能」の表とフィルターの節 |

他のRoundと同じファイルを触らない。

## 実施レポート

実施日: 2026-08-30 / バージョン: 6.5.0 / PR: 未作成（submodule の `feature/199-selector-filter` へ push 済み）

### 実装した内容

| 設計 | 実装 |
| --- | --- |
| 属性へフィルター名の引数を追加 | `Runtime/Attribute/` の3属性へ既定値付き引数と `FilterMethodName` を追加 |
| 解決と適用の共通処理 | `Editor/AttributeDrawer/Internal/SelectorFilterUtility.cs`（`internal static`）の `TryCreateMask<T>` と `ApplyMask<T>` |
| 文字列セレクターへの適用 | `SceneNameSelectorDrawer` / `TagSelectorDrawer` がマスクで候補を絞り、空になったら値を書き換えずメッセージを出す |
| 派生型セレクターへの適用 | `SubclassSelectorDrawer` が `s_TypeCache` から取り出した3配列へ同じマスクを適用する。キャッシュのキーと中身は変えていない |
| テスト | `SelectorFilterUtilityTests`（12件）、`SceneNameSelectorAttributeTests` / `TagSelectorAttributeTests`（各3件）、`SubclassSelectorAttributeTests`（5件） |
| 網羅一覧の更新 | `PublicTypeTestCoverageTests.UntestedPublicTypes` から属性3型を削除。Drawer 3型は残した |

### 設計から変えた点

- **`SubclassSelectorDrawer` の `EditorGUI.PrefixLabel` を、フィルター判定の後ろへ移した。** 設計時は位置を決めていなかったが、フィルターのエラーメッセージを `LabelField` で出すとラベルが二重に描かれるため。フォールバック経路（宣言型を復元できない場合）でも `PrefixLabel` と `PropertyField` の重ね描きが無くなる。
- **参照先フィールドの描画を `DrawManagedReferenceFields` へ切り出した。** フィルターのエラー時も従来どおり中身を描くため、同じ処理を2か所から呼ぶ必要が生じた。
- **`TryCreateMask<T>` へ `where T : class` を付けた。** 候補は `string` と `Type` だけであり、未設定を表す `null` の判定を型引数の制約で明確にするため。
- **`Core/SymphonyConstant.cs` の `VERSION` も更新した。** 設計書の「この Round で触るバージョン関連ファイル」に載せ忘れていたものを、`preflight` の `[version]` が検出した。

### 検証結果

| 項目 | 結果 |
| --- | --- |
| `release_round.py preflight` | すべての検証を通過（tests: ソース7件に対しテスト9件、bom 13件、meta 5件、docs 同期） |
| `build_module_docs.py --check` | OK: 19件の生成物が正本と同期 |
| `verify_round.py` | **実行できず。** 実行環境に Unity Editor が無く、uLoopMCP が `Unity project not found` を返す |

**コンパイル、EditMode / PlayMode テスト、Play Mode 2往復、Inspector の表示確認は未実施である。** 「動作確認手順」の1〜8はすべて Unity 上で行う必要がある。

新規4ファイルの `.meta` は、Unity Editor が無い環境のため**スクリプトで生成した**（`fileFormatVersion: 2` と新規GUID、既存の `.cs.meta` / フォルダ `.meta` と同じ形式）。既存アセットのGUIDは変更していない。Unity で開いたときに再生成や差分が出ないことを確認すること。
