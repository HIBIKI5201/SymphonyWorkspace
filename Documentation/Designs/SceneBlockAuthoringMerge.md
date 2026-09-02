# Scene Block Authoring の二重定義の統合

## 背景

`develop` に、同じ役割の Authoring 層が2系統入り、`SymphonyFrameWork.System.SceneBlock` 名前空間で
`SceneBlockAsset` と `SceneBlockAssetDrawer` が二重定義になった。**同一アセンブリの同名型であるため
`CS0101` でコンパイルできない。**

| | 系統A（Issue #202） | 系統B（`6ae1111`） |
| --- | --- | --- |
| 版 | 6.6.0 / 6.7.0 / 6.7.1 で公開済み | 版更新もCHANGELOGも無い |
| 公開範囲 | `public`。`CreateAssetMenu` あり | `internal`。`CreateAssetMenu` 無し |
| 依存の書き方 | エントリごとの `_dependsOn`（隣接リスト） | `_sceneIds` と `_edges`（辺リスト） |
| 付随する情報 | `_blockName` / `_priority` / `_isPersistent` | 無し |
| 参照元 | `SceneBlockLoader` / `SceneBlockService` / Administrator / サンプル / モジュール文書 | **自分自身とそのテストだけ** |
| 土台 | `SceneBlockGraphPlanner` | 同左 |

**表現力は等価で、系統Aが厳密に上位互換である。** 系統Bは利用者がアセットを作る手段すら持たず
（`CreateAssetMenu` が無い）、フレームワーク内のどこからも呼ばれていない。

## 決定

**系統Aを正とし、系統Bの型を取り除く。** そのうえで、系統Bだけが持っていた利点を系統Aへ取り込む。

### 系統Bから取り込むもの

**依存先の選択肢を、同じブロックに登録済みのシーンだけに絞る。**

系統Bの `SceneBlockAssetDrawer.BuildSceneIdOptions` は、辺の端点候補を「そのアセットに登録済みの
シーン識別子」に限っていた。系統Aの `_dependsOn` は `[SceneNameSelector]` で Build Settings の
全シーンを並べるため、**ブロックの外のシーンを選べてしまい、選んだ後に Inspector の検証が
`MissingReference` として弾く**形になっていた。誤りを作れてしまう設計であり、系統Bの方が正しい。

**Issue #199 で追加したセレクターのフィルター機能で実現する。** 専用Drawerを書かず、
`SceneNameSelectorAttribute(filterMethodName)` へフィルターを渡す。

- `SceneBlockAsset.CanDependOnScene(string)` を `internal` で追加する
- `SceneBlockEntry._dependsOn` を `[SceneNameSelector(nameof(SceneBlockAsset.CanDependOnScene))]` にする

`SceneNameSelectorDrawer` はフィルターの対象に `property.serializedObject.targetObject` を渡すため、
**エントリではなくアセットがフィルターを持てる。** エントリは兄弟エントリを知らないので、
ここがアセット側でなければ成立しない。着手前にこの経路を実際にコードで確認している。

#### 保存済みの依存を候補に残す理由

`SceneNameSelectorDrawer` は、保存済みの値が候補に無いとき **先頭の候補へ書き換える**。

```csharp
int index = Array.IndexOf(selectableScenes, property.stringValue);
if (index < 0) { index = 0; }
int selectedIndex = EditorGUI.Popup(position, label.text, index, selectableScenes);
property.stringValue = selectableScenes[selectedIndex];
```

候補をブロック内へ絞ると、**依存されている側のエントリを消したときに、依存する側の記述が
黙って別のシーンへ書き換わる。** これは絞り込みが持ち込む新しい事故であり、絞り込みの利点より重い。

そのため `CanDependOnScene` は「エントリに登録済みのシーン」に加えて
**「既にどこかの依存として保存済みのシーン」も候補に残す。** 新しく選べるのはブロック内のシーンだけで、
既存の値は失われない。外れた依存であること自体は、従来どおり `SceneBlockEntryReader` の検証が
Inspector へ表示する。

### 取り込まないもの

| 系統Bの要素 | 判断 |
| --- | --- |
| Build Settings のシーンを Popup で選ばせる | **系統Aが `[SceneNameSelector]` で既に実現している。** 属性ベースで再利用でき、フィルターにも対応する分こちらが上 |
| 層数とノード数のサマリ表示 | 系統Aは各層のシーン名まで出す。情報量で上回る |
| 「＋ 追加 / － 削除」ボタン | `DrawDefaultInspector()` のリスト操作で足りる |
| 入力途中の空欄を黙って除外する正規化 | **採らない。** 空欄のまま放置されたエントリはロード時に何も起こらず、気づかないまま壊れる。系統Aは異常として報告しており、そちらが安全 |
| `SceneBlockEdgeAuthoring` / `SceneBlockAssetPlanner` | 系統Bの `SceneBlockAsset` を消すと参照元が無くなる。死んだコードを残さない |

## 影響範囲

- 公開型の増減は無い。`SceneBlockAsset` へ `internal` メソッドが1つ増えるだけ
- シリアライズ形式は変わらない。既存の `SceneBlock` アセットはそのまま読める
- 版は 6.9.0（Fix と Change）

## テスト

`Tests/Editor/SceneBlockAssetTests.cs` へ `CanDependOnScene` の検証を追加する。
アセットの組み立ては既存のテストと同じく `SerializedObject` 経由にし、フィールド名の変更にも気づける形にする。

| 検証 | 期待 |
| --- | --- |
| 登録済みのシーン | 候補になる |
| ブロックの外のシーン | 候補にならない |
| 既に依存として保存済みのシーン | エントリから消えても候補に残る |
| `null` / 空文字 / 空白 | 候補にならない |
| エントリが1件も無いアセット | どのシーンも候補にならない |

## 実施レポート

実施日: 2026-09-02 / バージョン: 6.8.1 と 6.9.0 / PR: [#208](https://github.com/HIBIKI5201/SymphonyFramework/pull/208) と [#209](https://github.com/HIBIKI5201/SymphonyFramework/pull/209)

### 実装した内容

**Round 1（6.8.1 / PR #208）— 重複定義の解消。** 系統Bの6ファイルとテスト2ファイルを削除した。

- `Runtime/Service/SceneBlock/Internal/Infrastructure/SceneBlockAsset.cs`
- `Runtime/Service/SceneBlock/Internal/Infrastructure/SceneBlockAssetPlanner.cs`
- `Runtime/Service/SceneBlock/Internal/Infrastructure/SceneBlockEdgeAuthoring.cs`
- `Editor/SceneBlock/SceneBlockAssetDrawer.cs` と `Editor/SceneBlock.meta`
- `Tests/Editor/SceneBlockAssetPlannerTests.cs`、`Tests/Editor/SceneBlockEdgeAuthoringTests.cs`

**Round 2（6.9.0 / PR #209）— 依存先候補の絞り込み。**

- `SceneBlockAsset.CanDependOnScene(string)` を `internal` で追加
- `SceneBlockEntry._dependsOn` を `[SceneNameSelector(nameof(SceneBlockAsset.CanDependOnScene))]` へ
- `Tests/Editor/SceneBlockAssetTests.cs` へ5件追加
- `Documentation~/Modules/SceneBlock.md` の Depends On の説明と Editor機能の表を更新

### 設計から変えた点

**1つのRoundを2つに割った。** `release_round.py preflight` が「CHANGELOGの同一版で `Fix` と `Change` が同居している」を検出して止めた。修正は独立したパッチ版へ分ける規則があり、設計時にそこまで見ていなかった。**結果としてこちらが正しい。** 壊れている `develop` の復旧（6.8.1）が、機能追加（6.9.0）のレビューを待たずに先に入る。

それ以外の判断は設計どおりで、変更は無い。

### 検証結果

`python scripts/release_round.py preflight` — 2 Round とも全項目通過。

| | Round 1 | Round 2 |
| --- | --- | --- |
| branch | OK | OK |
| tests | ソース5件に対しテスト4件 | ソース3件に対しテスト1件 |
| version | 6.8.1 | 6.9.0 |
| changelog | 247件 | 248件 |
| bom / meta / layer / asmdef / playmode / docs | すべてOK | すべてOK |

加えて機械的に確認した項目。

- 削除後の同名型の重複は `IInjectable`（ジェネリック引数違いの多重定義）だけで、`CS0101` の対象は無い
- `SceneBlockEdgeAuthoring` / `SceneBlockAssetPlanner` への残存参照が0件
- サンプル `TownBlock.asset` / `DungeonBlock.asset` の依存はすべてブロック内で、絞り込みの影響を受けない

### 未実施の確認

**Unity Editor の無いリモート環境で実装したため、次はすべて未実施。** 依頼者の一括検証で確認する。

- コンパイル（エラー0・警告0）。**`CS0101` の解消がこの変更の目的であり、まずここを見る**
- EditMode テスト全数成功。`SceneBlockAssetTests` が 4 件から 9 件へ増え、`SceneBlockAssetPlannerTests` / `SceneBlockEdgeAuthoringTests` の計 8 件が消える
- `SceneBlockAsset` の Inspector で `Depends On` の Popup がブロック内のシーンだけになること
- エントリが1件も無いブロックで「条件に一致するシーンがありません。」と表示されること
- 依存されている側のエントリを消しても、依存する側の値が書き換わらないこと
- 削除した `.meta` に対応するアセットが Project ウィンドウから消え、警告が出ないこと
- サンプルの `TownBlock` / `DungeonBlock` を開いて、依存の表示が従来どおりであること

### 振り返り

リモート環境で新しく分かったことを `.agents/skills/implement/references/remote.md` へ反映した。

| 気づき | 反映先 |
| --- | --- |
| `release_round.py commit --pr` はコンテナに `gh` が無く必ず失敗する。コミットとpushは成功しているのでPRだけ作り直せばよい | remote.md §5 |
| 版の粒度（`Fix` と `Change` を同居させない）を設計時に見落とし、preflightで割り直した。**設計の段階でCHANGELOGの区分まで決めておけば、割り直しは起きない** | remote.md §1 と §6 |
| コンパイルできない状態が `develop` へ直接pushされていた。リモートではコンパイラが無いため、**マージ前に「同名型が増えていないか」を機械的に見る**のが唯一の防御になる | remote.md §2 |

見送ったもの: `TagSelectorDrawer` にも `SceneNameSelectorDrawer` と同じ「保存済みの値を先頭の候補へ書き換える」挙動がある。**今回の絞り込みが無ければ表に出ない問題であり、この Round の範囲を広げないため触っていない。** 別の Round で扱う。
