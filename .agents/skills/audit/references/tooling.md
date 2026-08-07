# 監査に使うツール

機械が担当する範囲と、機械には担当できない範囲の切り分け。

| ツール | 担当する観点 | 実行方法 |
| --- | --- | --- |
| `scripts/audit_scan.py` | 01 / 03 / 05 / 06 / 09 / 11 / 12 / 14 / 16 / 18 / B1 / B2 / B7 / B8 | `python` |
| Project Auditor | 04 / 05 / 06 の IL レベル | `uloop-execute-dynamic-code` |
| uLoopMCP | 03 の実挙動、B6 のテスト実行 | `uloop-*` スキル |
| 読解 | 02 / 07 / 08 / 10 / 13 / 15 / 17 / **B3** / B4 / B5 | — |

**B3（Editor機能とドキュメントの同期）は機械化を試みて失敗している。**
`EditorTools.md` は実装ディレクトリではなく機能名で節を立てるため、
ディレクトリ名の文字列一致では誤検出率100%になった（初回監査で2件、いずれも誤検出）。
対応表を持たせる案もあるが、その表自体の保守が要る。**読解に委ねる。**

**プロジェクト固有の規約はツールが知らない。** B群のほとんどは `audit_scan.py` の自作検査でしか
拾えない。汎用ツールの出力だけで監査を終わらせない。

---

## scripts/audit_scan.py

```bash
python scripts/audit_scan.py --format markdown --out Documentation/Audit/_raw/scan.md
```

| オプション | 意味 |
| --- | --- |
| `--format markdown` | 件数サマリ + 分類ごとの `file:line` 付録（既定） |
| `--format json` | `{fileCount, lineCount, findings[]}`。件数の集計や差分比較に使う |
| `--out <path>` | 出力先。省略時は標準出力 |
| `--category <name>` | その分類だけを出力。複数指定可。観点ごとに読むときに使う |

対象は `Assets/SymphonyFrameWork/` の `Runtime` / `Core` / `Editor`。`Tests` と `Samples` は含まない。

### 既知の誤検出と限界

**すべて正規表現ベースなので、以下は必ず読解で裏を取る。**

| 分類 | 限界 |
| --- | --- |
| `01_subscribe_imbalance` | **`-=` という字面しか見ない。** このフレームワークで主流の `existing - action` や `OnSettingChanged = null` による解除を検出できず、誤検出率が高い（初回監査では5件中4件が誤検出） |
| `01_lambda_subscribe` | 使い捨てのローカルイベントへの購読も拾う。EditorWindow内の `VisualElement` への購読はウィンドウと同寿命なので実害が無い |
| `03_static_without_reset` | `static` な可変状態を持っていても、意図的にセッションを跨ぐもの（キャッシュ等）は正当 |
| `09_magic_number` | **最も誤検出が多い。** 初回監査では37件中22件が `GetHashCode` の慣用素数（`397` / `17` / `31`）、9件が単位変換だった。実質的な指摘は1件 |
| `11_missing_cancellation_token` | メソッド**引数**の有無しか見ない。「受け取っているが下流へ渡していない」は拾えない |
| `14_single_implementation_interface` | 実装数を文字列一致で数えるため、部分一致で過小・過大の両方に振れる |
| `16_long_method` | 波括弧の対応で本文長を測るため、文字列リテラル中の `{` `}` でずれる |
| `B_public_without_xmldoc` | 直前12行以内の `///` を見る。属性が長いと取りこぼす |
| `B_obsolete_undocumented` | Deprecations.md 内の**シンボル名の文字列一致**で判定する。同名の別シンボルがあると通ってしまう |
| `B_deprecation_stale` | 上と同じ文字列一致。`## 削除予定の一覧` 節の表だけを読み、`###` 以下の補足表は対象外 |

**検出できないもの**: 実行時にしか分からないリーク、呼び出し経路に依存する到達性、
設計の妥当性、ドキュメントの内容が正しいかどうか。

### `[Obsolete]` は2方向から検査している

片方向だけでは片方の不整合しか拾えない。

| 分類 | 向き | 拾えるもの |
| --- | --- | --- |
| `B_obsolete_undocumented` | コード → ドキュメント | `[Obsolete]` を付けたのに Deprecations.md へ書いていない |
| `B_deprecation_stale` | ドキュメント → コード | 削除が済んだのに Deprecations.md の行を `## 削除済み` へ移していない |

**後者はコード側からは原理的に検出できない。** シンボルが消えているので、
消えたことを知る手掛かりがドキュメント側にしか無い。

### 改行コードはワーキングツリーを見ても分からない

`CONTRIBUTING.md` §3 は `core.autocrlf=true` 前提でリポジトリにLFで格納すると定めている。
Windows のワーキングツリーはCRLFになるため、**ファイルのバイト列を読んでも判定できない**。
`B_line_ending` は `git ls-files --eol` でindex側を見ている。

この検査だけは `git` の実行を伴い、対象が `Tests/` `Samples/` を含む全 `.cs`（204ファイル）へ
広がる。BOM検査（`B_missing_bom`、155ファイル）と件数が合わないのはこのためである。

---

## Project Auditor

`com.unity.project-auditor` は `Packages/manifest.json` に導入済み。**UI を開かずに実行できる。**

出力先ディレクトリを先にシェルで作ってから（`audit_scan.py --out` が作るので通常は不要）、
`uloop-execute-dynamic-code --code-file` へ次のコードを渡す。

```csharp
using System.Linq;
using UnityEngine;
using Unity.ProjectAuditor.Editor;

var outputPath = Application.dataPath.Replace("/Assets", "")
    + "/Documentation/Audit/_raw/project-auditor.json";

var analysisParams = new AnalysisParams
{
    Categories = new[] { IssueCategory.Code },
    AssemblyNames = new[]
    {
        "SymphonyFrameWork",
        "SymphonyFrameWork.Core",
        "SymphonyFrameWork.Editor",
    },
    // Player は AssemblyBuilder の完了を Thread.Sleep で待つ。メインスレッドから呼ぶと固まる。
    CompilationMode = CompilationMode.Editor,
};

var report = new ProjectAuditor().Audit(analysisParams);
report.Save(outputPath);

// AssemblyNames を指定しても依存アセンブリの指摘が混ざるため、パスで絞り込む。
var issues = report.FindByCategory(IssueCategory.Code)
    .Where(issue => issue.RelativePath != null
        && issue.RelativePath.StartsWith("Assets/SymphonyFrameWork/"))
    .ToArray();

return $"saved={outputPath} total={report.NumTotalIssues} framework={issues.Length}";
```

**このスニペットには実行して分かった制約が2つ織り込んである。書き換えるときに戻さないこと。**

- **`System.IO` を使わない。** `uloop-execute-dynamic-code` は `System.IO.*` を
  コンパイル時に拒否する。`Path` / `Directory` / `File` は呼べないため、
  出力パスは `Application.dataPath` からの文字列連結で組み立て、
  ファイル書き込みは `Report.Save`（パッケージ側の実装）へ委ねる。
  **集計結果をファイルへ書きたい場合も、スニペット内では書けない。**
  JSONだけ保存し、集計はシェル側のPythonで行う
- **ローカル変数を `parameters` という名前にしない。** dynamic-code のラッパーが
  `Dictionary<string, object> parameters` を引数に持つため、CS0136 で落ちる。
  実際にこれで1回失敗している

**新しいスニペットは `--compile-only true` で先に通す。** Roslyn 診断だけが返り、
Unity を動かさずに済む。上の CS0136 はこれで見つかった。

保存したJSONの読み方（先頭2行はヘッダとバージョン）:

```python
with io.open(path, encoding="utf-8") as handle:
    handle.readline()   # PROJECT_AUDITOR_REPORT
    handle.readline()   # 0.2
    report = json.load(handle)

issues = report["issues"]            # 各要素は location.path / location.line を持つ
descriptors = {d["id"]: d for d in report["descriptors"]}
```

**`ReportItem.RelativePath`（C#側）は JSON では `location.path` になる。**
JSONを直接フィルタするときに `relativePath` を探しても存在しない。

### 飛ばすと事故る規則

- **`CompilationMode.Editor` を使う。** `Player` / `DevelopmentPlayer` は `AssemblyBuilder` の
  完了を `Thread.Sleep(10)` のループで待つ。`Audit()` 自体も完了まで `Thread.Sleep(50)` で
  ブロックするため、**メインスレッドから呼ぶと Unity が応答しなくなる**。
  `Editor` はコンパイル済みアセンブリをそのまま読むのでこの問題が起きない
- **`IssueCategory.ProjectSetting` を混ぜない。** ホストプロジェクトの設定に対する指摘であり、
  パッケージの品質とは無関係。混ぜると指摘の大半が無関係な項目で埋まる
- **`AssemblyNames` は依存アセンブリも一緒に解析対象へ引き込む**（`CollectAssemblyDependencies`）。
  `RelativePath` での絞り込みを省略しない
- 実行前に `uloop-clear-console`。Unity 未起動なら `uloop-launch`
- **診断IDごとの一括判断をしない。** 同じ `Id` でも、初期化時の1回とホットパスの毎フレームでは
  意味が違う。**呼び出し頻度を確認してから確度を付ける**

### 出力の読み方

`Severity` は Project Auditor の既定値であって、このフレームワークにおける重要度ではない。
**`Moderate` でもホットパスなら「確定」、`Major` でも起動時1回なら「設計指摘」**になる。

---

## uLoopMCP

観点03（static 状態のリセット）と B6（テスト）は、静的解析では判断できない。

```text
uloop-launch          # Unity が起動していない場合
uloop-clear-console
uloop-compile         # エラー0・警告0 を確認
uloop-control-play-mode  # 開始 → 終了 → 開始 → 終了
uloop-get-logs        # 2周目でゴースト参照の例外が出ないこと
uloop-run-tests --test-mode EditMode
uloop-run-tests --test-mode PlayMode
```

- **`uloop-compile` が `is compiling` を返し続けても `--force-recompile` で再試行しない。**
  再試行のたびに Domain Reload が起きて終わらなくなる。`Temp/` のロックを確認して `uloop fix`
- **コンパイル直後の初回テスト実行は信用しない。** `uloop-clear-console` を挟んで
  同じ結果が2回続くことを確認する
- テスト結果は `Success` / `Passed` / `Failed` / `Skipped` を**すべて記録する**。
  **失敗0件でも `Success` が false なら合格にしない**

---

## 採用しないツール

| ツール | 理由 |
| --- | --- |
| Rider InspectCode CLI | 内部で MSBuild を呼ぶ。`AGENTS.md` §7 が `dotnet build` / `msbuild` / `csc` を禁じており、コンパイル可否の判断は Unity へ委ねる方針と衝突する |
| Memory Profiler | スナップショットの取得と比較が対話操作を要する。監査では「ここを実測してほしい」と依頼する形にとどめる |
| Roslyn Analyzer の新規導入 | 監査ではなく**予防**の手段。監査で同じ指摘が2回続いた観点について、導入を提案する側に回す |
