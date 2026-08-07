# 16. SOLID原則（特にSRP）

300行超のファイルと60行超のメソッドを列挙し、責務の観点で読解した。
**行数は責務過多の兆候であって、それ自体は欠陥ではない。**

| 原則 | 評価 | 主な指摘 |
| --- | --- | --- |
| **S** 単一責任 | 一部違反 | `SymphonyAwaitable` 1,030行、`SceneLoadService.LoadScene()` 91行 |
| **O** 開放閉鎖 | 良好 | enum switch は表示文字列の解決のみ |
| **L** リスコフ置換 | 良好 | 既定で `true` を返す基底クラスは無し |
| **I** インターフェース分離 | **良好** | 全インターフェースが5メンバー以下 |
| **D** 依存性逆転 | **良好** | asmdef と `Internal/` で徹底。例外1件（[08](08_アセンブリ境界とレイヤー違反.md)） |

## 調査サマリ

| 分類 | 件数 |
| --- | --- |
| 300行超のファイル | 10（Runtime 4 / Editor 6） |
| 60行超のメソッド | 8（Runtime 3 / Editor 5） |
| 最大ファイル | `SymphonyAwaitable.cs` 1,030行 |
| 最長メソッド | `SceneLoadService.LoadScene()` 91行 |

比較として `Docs/RuntimeAudit` の対象（ゲーム側）は400行超が21ファイル・60行超が17メソッドだった。
**規模あたりの密度は当フレームワークのほうがかなり低い。**

---

## 【設計指摘】`SymphonyAwaitable.cs` が1,030行

**場所**: [SymphonyAwaitable.cs](../../Assets/SymphonyFrameWork/Runtime/Utility/SymphonyAwaitable.cs)

単一の `public static class` に、次の5つの独立した関心事が同居している。

| 関心事 | 代表API |
| --- | --- |
| 完了済み値の生成 | `Completed()` |
| 合成 | `WhenAll(...)` |
| 条件待機 | （待機系API群） |
| タイムアウト | （タイムアウト系API群） |
| `Task` ブリッジ | `AwaitBridgeAsync`、`ObserveAwaitableAsync` |

クラスのXMLドキュメント（[同ファイル:10-14](../../Assets/SymphonyFrameWork/Runtime/Utility/SymphonyAwaitable.cs)）に
この5分類がそのまま列挙されている。**作者は責務の境界を把握している。**

### なぜ分割が難しいか

`SymphonyAwaitable` は**利用側が `using SymphonyFrameWork.Utility;` して呼ぶ静的APIの入口**であり、
型を分割すると呼び出し側のコードが変わる。**破壊的変更になる。**

### 修正方針

型を分けずに `partial class` でファイルだけ分割する。

```text
Runtime/Utility/SymphonyAwaitable.cs            （クラスのXMLドキュメントと共通部）
Runtime/Utility/SymphonyAwaitable.Completed.cs
Runtime/Utility/SymphonyAwaitable.WhenAll.cs
Runtime/Utility/SymphonyAwaitable.Timeout.cs
Runtime/Utility/SymphonyAwaitable.TaskBridge.cs
```

**利用側から見たAPIは1文字も変わらない。** `.meta` の新規生成が必要になる点だけ注意する。

**`Documentation/CodeGuidelines.md` の「ディレクトリとAssembly Definition」節へ、
`partial` を使ってよい条件を条文として追加した。** 要点は次のとおり。

- **公開エントリポイントが大きくなった場合に限り** `partial` でファイルだけを分割する
- 分割の単位は関心事ごと。ファイル名は `<型名>.<関心事>.cs`
- **内部実装（`*/Internal/`）は型ごと分割できるため、`partial` を使わない**

条文追加前はこの判断基準が無く、毎回割れる状態だった。

---

## 【設計指摘】`SceneLoadService.LoadScene()` が91行

**場所**: [SceneLoadService.cs:136-226](../../Assets/SymphonyFrameWork/Runtime/System/SceneLoader/Internal/Application/SceneLoadService.cs)

同ファイルの `UnloadScene()` も68行ある。両者はロード／アンロードという対称な処理で、
それぞれが次を抱えている。

- 引数と状態の検証
- レジストリへの登録・更新
- Unity APIの呼び出し（`UnitySceneLoader` への委譲）
- 進捗の集約と `IProgress<float>` への報告
- 完了callbackの取り出しと実行（`TakeLoadedAction`）
- 例外時のロールバック

**このうち「完了callbackの取り出しと実行」は3箇所（[154](../../Assets/SymphonyFrameWork/Runtime/System/SceneLoader/Internal/Application/SceneLoadService.cs)、
[170](../../Assets/SymphonyFrameWork/Runtime/System/SceneLoader/Internal/Application/SceneLoadService.cs)、
[228](../../Assets/SymphonyFrameWork/Runtime/System/SceneLoader/Internal/Application/SceneLoadService.cs)）に
同じ形で現れる。**

```csharp
_registry.TakeLoadedAction(request.SceneName)?.Invoke();
```

**修正方針**: この1行を `InvokeLoadedActions(request)` として切り出す。
3箇所の重複が消え、`LoadScene()` の行数も減る。
`internal` なメソッドなので破壊的変更にはならない。

91行という数字そのものより、**分岐のたびに同じ後始末を書き忘れる余地があること**が問題である。

---

## 検証したが問題が無かった項目

- **既定で `true` を返す基底クラスは0件**（LSP違反の典型パターン）
- **すべてのインターフェースが5メンバー以下。**
  最大は [IGameObject](../../Assets/SymphonyFrameWork/Runtime/Interface/IGameObject.cs) の3メンバー
- **`enum` に対する `switch` は表示文字列の解決（[ServiceLocator.cs:597](../../Assets/SymphonyFrameWork/Runtime/System/ServiceLocator/ServiceLocator.cs)）
  とログ種別の分岐（[SymphonyDebugLogger.cs:40](../../Assets/SymphonyFrameWork/Runtime/Debug/SymphonyDebugLogger.cs)）のみ。**
  振る舞いの分岐ではないため、OCP違反にはあたらない
- **Editor側の300行超6ファイルはいずれもEditorWindowかSettingProviderで、
  UI描画メソッドが行数を占めている。** 責務の混在ではない

---

## 付録A: 300行超のファイル（全10件）

| 行数 | 場所 | 判定 |
| --- | --- | --- |
| 1030 | `Assets/SymphonyFrameWork/Runtime/Utility/SymphonyAwaitable.cs` | **指摘** |
| 670 | `Assets/SymphonyFrameWork/Runtime/System/SceneLoader/Internal/Application/SceneLoadService.cs` | **指摘**（メソッド長） |
| 640 | `Assets/SymphonyFrameWork/Editor/Administrator/UITK/CS/SaveDataWindow.cs` | 正当（EditorWindow） |
| 622 | `Assets/SymphonyFrameWork/Runtime/System/ServiceLocator/ServiceLocator.cs` | 正当（公開Facade） |
| 439 | `Assets/SymphonyFrameWork/Runtime/System/SceneLoader/SceneLoader.cs` | 正当（公開Facade） |
| 435 | `Assets/SymphonyFrameWork/Editor/Generator/AssetStoreToolsPackager/AssetStoreToolsPackageWindow.cs` | 正当（EditorWindow） |
| 371 | `Assets/SymphonyFrameWork/Editor/Configs/Drawer/SubclassSelectorDrawer.cs` | 正当（PropertyDrawer） |
| 333 | `Assets/SymphonyFrameWork/Editor/SettingProvider/AssetStoreToolsPackagerProvider.cs` | 正当（SettingProvider） |
| 316 | `Assets/SymphonyFrameWork/Editor/Generator/AssetStoreToolsPackager/AssetStoreToolsVersionLogStore.cs` | 正当 |
| 314 | `Assets/SymphonyFrameWork/Editor/Orchestrator/Internal/SymphonyEditorOrchestrator.cs` | 正当 |

## 付録B: 60行超のメソッド（全8件）

| 行数 | 場所 | 判定 |
| --- | --- | --- |
| 91 | `Assets/SymphonyFrameWork/Runtime/System/SceneLoader/Internal/Application/SceneLoadService.cs:136` `LoadScene()` | **指摘** |
| 90 | `Assets/SymphonyFrameWork/Editor/Configs/Drawer/SubclassSelectorDrawer.cs:244` `GetFieldInfo()` | 正当（型解決の分岐） |
| 71 | `Assets/SymphonyFrameWork/Runtime/Utility/SymphonyVisualElement.cs:64` `Initialize()` | 要検討（ロード方式3種の分岐） |
| 70 | `Assets/SymphonyFrameWork/Editor/SettingProvider/AssetStoreToolsPackagerProvider.cs:87` `DrawPipelines()` | 正当（UI描画） |
| 68 | `Assets/SymphonyFrameWork/Runtime/System/SceneLoader/Internal/Application/SceneLoadService.cs:282` `UnloadScene()` | **指摘** |
| 68 | `Assets/SymphonyFrameWork/Editor/Generator/EnumGenerate/EnumGenerator.cs:37` `EnumGenerate()` | 正当（コード生成） |
| 65 | `Assets/SymphonyFrameWork/Editor/SettingProvider/AssetStoreToolsPackagerProvider.cs:208` `DrawConfig()` | 正当（UI描画） |
| 63 | `Assets/SymphonyFrameWork/Editor/Generator/AssetStoreToolsPackager/AssetStoreToolsPackageWindow.cs:158` `DrawExportTab()` | 正当（UI描画） |

再生成:

```bash
python scripts/audit_scan.py --category 16_long_file --category 16_long_method
```
