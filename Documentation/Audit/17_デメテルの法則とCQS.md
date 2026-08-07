# 17. デメテルの法則とCQS

**指摘なし。**

## 確認したこと

| 項目 | 結果 |
| --- | --- |
| 3段以上のメンバーチェーン | **0件** |
| 引数が5個を超えるメソッド | **0件** |
| `ref` と `out` の併用 | **0件** |

### 3段チェーンが0件

`a.B.C.D` の形（型名・名前空間を除く）を機械的に探したが、Runtime / Core に該当が無かった。

`Docs/RuntimeAudit` はゲーム側で3段チェーンを11箇所指摘していた。
当フレームワークは `Internal/` による層分離が効いており、
**そもそも他の型の内部構造へ到達できる経路が少ない**。

唯一チェーンらしく見えるのは次の形だが、これは静的クラスのメンバーアクセスであり
デメテル違反にはあたらない。

```csharp
TagsAndLayersPostProcessor.SceneList.OnSettingChanged += SceneListChangedHandler;
```

`TagsAndLayersPostProcessor` は Editor 側の `AssetPostprocessor` で、
`SceneList` は公開された監視対象データである。ここを縮めると
「どの設定の変更を購読しているか」が読めなくなるため、現状のほうが良い。

### 引数の多いメソッドが無い

最多は `SymphonyVisualElement` のコンストラクタ経路で3引数（`path` / `initializeType` / `loadType`）。
`Docs/RuntimeAudit` が指摘した「引数15個の `Initialize`」に相当するものは無い。

`SceneLoadService.WaitForAll` は4引数
（`tasks` / `progresses` / `progress` / `token`）だが、
うち1つは `CancellationToken` であり実質3引数である。

### CQS違反が無い

- **`ref` と `out` を同時に使うメソッドは0件**
- `bool TryGet(string, out T)` 形式は .NET の標準的な慣用であり、
  CQS違反として扱わない
- **状態を変えつつ値を返すメソッド**として唯一該当しうるのが
  [SceneLoadRegistry.TakeLoadedAction](../../Assets/SymphonyFrameWork/Runtime/System/SceneLoader/Internal/Application/SceneLoadRegistry.cs)
  （取り出して削除する）だが、**`Take` という名前が「取り出して消す」ことを明示している**。
  意図が名前に出ているため問題ない

  同様に [AutoEnumGenerator.ConsumeAssetChanges](../../Assets/SymphonyFrameWork/Editor/Generator/EnumGenerate/AutoEnumGenerator.cs)
  も `Consume` という名前で消費を明示している。**この2つは命名で解決している好例である。**

## この観点の限界

3段チェーンの検出は正規表現によるもので、
**変数へ一度代入してからアクセスする形（`var x = a.B; x.C.D;`）は拾えない**。
今回は該当が0件だったが、「チェーンが無い」ことの証明にはならない。
