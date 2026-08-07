# 04. GC Alloc・メモリリーク

Project Auditor 1.0.2 の `IssueCategory.Code`（Mono.Cecil によるIL静的解析）を
`CompilationMode.Editor` で実行し、`Assets/SymphonyFrameWork/` 配下の指摘へ絞り込んだ。
grepでは原理的に取れない、ボックス化・クロージャ確保・文字列連結を対象にしている。

## 調査サマリ

Project Auditor の全1,619件のうち、フレームワーク該当は1,268件。
うちランタイム側アセンブリ（`SymphonyFrameWork` / `SymphonyFrameWork.Core`）は599件。

| 診断ID | 内容 | ランタイム側 | 全体 |
| --- | --- | --- | --- |
| PAC2002 | Object Allocation | 305 | 589 |
| PAC2000 | **Boxing Allocation** | 73 | 97 |
| PAC1002 | System.String.Concat | 68 | 174 |
| PAC2004 | Array Allocation | 36 | 90 |
| PAC1001 | System.Reflection.* | 36 | 67 |
| PAC2003 | Closure Allocation | 26 | 43 |
| PAC0231 | UnityEngine.Object.name | 12 | 16 |
| PAC1000 | System.Linq.* | 5 | 117 |

**Project Auditor の `Severity` は当フレームワークにおける重要度ではない。**
`Info` 949件・`Moderate` 315件・`Major` 4件という内訳だが、判断は呼び出し頻度で行った。
フレームワークのコードは大半が初期化時・シーン遷移時にしか走らないため、
**毎フレーム走る経路にあるものだけを指摘としている**。

---

## 【確定】HUDのメモリ表示が毎フレームボックス化を起こす

**場所**: [SymphonyHUDDrawer.cs:96-102](../../Assets/SymphonyFrameWork/Runtime/Debug/DebugHUD/Internal/SymphonyHUDDrawer.cs)

**Project Auditor が `Major` と判定した4件は、すべてこのメソッドに集中している。**

```csharp
private string GetMemoryUsageString(long bytes)
{
    if (bytes < 1024) { return $"{bytes} B"; }                              // long をボックス化
    if (bytes < 1024 * 1024) { return $"{(bytes / 1024d):0.00} KB"; }       // double をボックス化
    if (bytes < 1024 * 1024 * 1024) { return $"{(bytes / (1024d * 1024d)):0.00} MB"; }
    return $"{(bytes / (1024d * 1024d * 1024d)):0.00} GB";
}
```

呼び出し元は `Update()` から毎フレーム走る `GetProfilingText`
（[同ファイル:64-90](../../Assets/SymphonyFrameWork/Runtime/Debug/DebugHUD/Internal/SymphonyHUDDrawer.cs)）で、
**1フレームあたり3回呼ばれる**（Mono Memory / Total Allocated / Total Reserved）。

C#の文字列補間は、書式指定を伴う値型を `string.Format` の `object[]` へ渡す形へ落ちるため、
値型が必ずボックス化される。**メモリ使用量を表示するHUDが、自らGCを発生させている。**

**修正方針**: **正しい書き方が同じファイルの11行上にある。**

```csharp
// line 87 — 既にボックス化を避けている
text.AppendLine($"FPS: {fps.ToString("0.")} ({msec.ToString("0,0")} ms)");
```

`ToString()` を明示的に呼べば、補間へ渡るのは `string` になりボックス化が消える。

```csharp
if (bytes < 1024) { return bytes.ToString() + " B"; }
if (bytes < 1024 * 1024) { return (bytes / 1024d).ToString("0.00") + " KB"; }
```

**この修正は4行で、挙動は一切変わらない。**

なお `_textToDisplay` は `StringBuilder` として再利用されており（[同ファイル:22](../../Assets/SymphonyFrameWork/Runtime/Debug/DebugHUD/Internal/SymphonyHUDDrawer.cs)）、
`Update()` の冒頭で `Clear()` している。**この部分は正しく書かれている。**

---

## 【要検証】`GetExtraText` が毎フレーム文字列を結合する

**場所**: [SymphonyHUDDrawer.cs:106-120](../../Assets/SymphonyFrameWork/Runtime/Debug/DebugHUD/Internal/SymphonyHUDDrawer.cs)

```csharp
private string GetExtraText()
{
    StringBuilder extraTextBuilder = new();   // 毎フレーム新規確保
    foreach (var textFunc in _extraTexts) { ... }
}
```

`_textToDisplay` は再利用されているのに、`GetExtraText` は毎フレーム `StringBuilder` を
新規に確保している。**同じクラス内で方針が食い違っている。**

加えて、返した `string` を `_textToDisplay.AppendLine(GetExtraText())` で結合するため、
中間文字列が1つ余分に生まれる。

**要検証としたのは、`SymphonyDebugHUD` がデバッグ機能であり、
出荷ビルドで有効になるかどうかが利用側の設定に依存するため。**
常時有効なら「確定」相当、Editor限定なら影響は小さい。

**修正方針**: `_textToDisplay` を直接渡し、フィールドの `StringBuilder` を再利用する。

---

## 【設計指摘】リフレクションがランタイム側に36箇所ある

`PAC1001`（System.Reflection.*）がランタイム側アセンブリで36件出ている。
主な集中箇所は `ServiceLocator`（45件中の一部）と `SaveSystem` の型解決である。

型をキーにしたサービス解決・セーブデータ解決という設計上、リフレクション自体は避けられない。
ただし**呼び出しごとに解決しているのか、初回のみ解決してキャッシュしているのか**は
本監査では追い切れていない。

**修正方針**: 指摘ではなく計測の提案である。`ServiceLocator.GetInstance<T>` と
`SaveStore` の取得経路について、Performance Test Framework でGC Allocを実測し、
`Tests/` へ回帰テストとして固定することを勧める。
`com.unity.test-framework.performance` は既に導入済みである。

---

## 検証したが問題が無かった項目

- **`System.Linq` のランタイム側使用は5件のみ**（→ [05](05_不必要な繰り返し処理.md)）。
  全体117件のうち112件は `Editor` アセンブリで、ビルドへ影響しない
- **`UnityEngine.Object.name`（PAC0231）12件は、すべてログ用途か初期化時**。
  毎フレーム経路には無い
- **マテリアルの暗黙クローン（`.material` アクセス）は0件。**
  フレームワークはレンダリングに関与しないため該当が無い
- **`Physics` / `Instantiate` の毎フレーム呼び出しは0件**

---

## 付録A: 生レポート

Project Auditor の全指摘は `_raw/project-auditor.json` にある（コミット対象外）。
再生成は `.agents/skills/audit/references/tooling.md` の「Project Auditor」節を参照。

```text
uloop-execute-dynamic-code --code-file <スニペット>
  → Categories = [IssueCategory.Code]
  → AssemblyNames = [SymphonyFrameWork, SymphonyFrameWork.Core, SymphonyFrameWork.Editor]
  → CompilationMode = CompilationMode.Editor
```

## 付録B: ランタイム側で指摘の多いファイル（上位10件）

サンプル由来のものは[B1](B1_公開APIの妥当性.md)の問題であり、本観点の対象外。

| 件数 | 場所 |
| --- | --- |
| 59 | `Assets/SymphonyFrameWork/Runtime/Utility/SymphonyAwaitable.cs` |
| 45 | `Assets/SymphonyFrameWork/Runtime/System/ServiceLocator/ServiceLocator.cs` |
| 38 | `Assets/SymphonyFrameWork/Samples/Runtime/SaveDataSystemSample/Scripts/SaveDataSystemSample_Controller.cs` |
| 34 | `Assets/SymphonyFrameWork/Runtime/System/SceneLoader/Internal/Application/SceneLoadService.cs` |
| 22 | `Assets/SymphonyFrameWork/Runtime/System/SceneLoader/SceneLoader.cs` |
| 22 | `Assets/SymphonyFrameWork/Runtime/Utility/SymphonyTween.cs` |
| 19 | `Assets/SymphonyFrameWork/Runtime/System/SaveSystem/SaveDataLoaderStrategy.cs` |
| 18 | `Assets/SymphonyFrameWork/Runtime/System/SaveSystem/Internal/Application/SaveDataService.cs` |
| 16 | `Assets/SymphonyFrameWork/Runtime/Orchestrator/Internal/SymphonyOrchestrator.cs` |
| 15 | `Assets/SymphonyFrameWork/Runtime/Debug/SymphonyDebugLogger.cs` |

**件数はファイルの大きさに比例する。** `SymphonyAwaitable.cs` の59件は1,030行に対するもので、
密度としてはむしろ低い。指摘としては扱わない。

## 付録C: Project Auditor が `Major` とした4件（全件）

| 場所 | 内容 |
| --- | --- |
| `Assets/SymphonyFrameWork/Runtime/Debug/DebugHUD/Internal/SymphonyHUDDrawer.cs:98` | `Int64` → 参照型への変換 |
| `Assets/SymphonyFrameWork/Runtime/Debug/DebugHUD/Internal/SymphonyHUDDrawer.cs:99` | `double` → 参照型への変換 |
| `Assets/SymphonyFrameWork/Runtime/Debug/DebugHUD/Internal/SymphonyHUDDrawer.cs:100` | `double` → 参照型への変換 |
| `Assets/SymphonyFrameWork/Runtime/Debug/DebugHUD/Internal/SymphonyHUDDrawer.cs:101` | `double` → 参照型への変換 |
