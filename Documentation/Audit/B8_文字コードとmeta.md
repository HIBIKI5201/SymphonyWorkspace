# B8. 文字コードと `.meta`

**指摘なし。**

`Documentation/CONTRIBUTING.md` §3 は `.cs` を UTF-8 BOM付きと定めている。
`.meta` の対応は `AGENTS.md` §7 および CONTRIBUTING §2 が扱う。

## 調査サマリ

| 分類 | 件数 |
| --- | --- |
| `Runtime` / `Core` / `Editor` の `.cs` | 155 |
| **UTF-8 BOM の無い `.cs`** | **0** |
| リポジトリ側の `.cs`（`Tests` / `Samples` 含む） | 204 |
| **改行コードがLFでない `.cs`** | **0** |
| `.cs` に対応する `.meta` の欠落 | 0 |

**155ファイルすべてがBOM付き、204ファイルすべてがindex側でLFである。**

---

## なぜこの観点を機械検査するか

**BOM が無くてもコンパイルは通る。** したがって検索しない限り気づけない。

`.agents/skills/implement/references/review.md` は、
この問題が実際に発生した経緯を記録している。

> 部分置換（Edit）は BOM を含む前後のバイト列をそのまま残すが、
> **全面書き込み（Write）は BOM ごと差し替える**

AIエージェントによる編集ではファイル書き込みツールがBOMを付けないことが多く、
**既存ファイルを Write で作り直したときに落ちる**。
実際に1件発生し、検索で拾えたため事故にならなかった、と記録されている。

**本監査の時点でBOM無しが0件ということは、その後の作業でも混入していない。**

## 監査時点で残っていた既存違反について

`review.md` は「パッケージ内にBOM無しの既存ファイルが残っている」として、
検査対象をラウンドの差分に限定していた。

**本監査は全155ファイルを対象に検査し、0件だった。**
`Runtime` / `Core` / `Editor` の範囲では、既存違反も解消済みである。

なおBOM検査は `Tests/` と `Samples/` を対象に含めていない
（改行検査は含む）。次回は対象を広げることを提案する
（`scripts/audit_scan.py` の `SHIPPED_DIRS` を変更する）。

## `.meta` の対応

`Assets/SymphonyFrameWork/` 配下で `.cs` と `.meta` の対が崩れているものは無かった。
submodule のワーキングツリーが clean（`7b0fa91`）であることからも、
未追跡の `.cs` や孤立した `.meta` が無いことが確認できる。

`Assets/SymphonyFrameWork.meta`（submodule の**外側**にあり親リポジトリが管理する）も
存在している。**これが消えると利用側プロジェクトのGUID参照が壊れる**ため、
`AGENTS.md` §7 が削除を禁じている。

## 改行コード

`CONTRIBUTING.md` §3 は「改行コードは `core.autocrlf=true` 前提で、
リポジトリにはLFで格納されます」と定めている。

**この規約はファイルのバイト列を読んでも検証できない。**
Windows のワーキングツリーはチェックアウト時にCRLFへ変換されるため、
BOM検査と同じ要領で `.cs` を開いても、常にCRLFが見えるだけである。

```text
i/lf    w/crlf  attr/       Core/AssemblyInfo.cs
  ↑              ↑
  リポジトリ側   ワーキングツリー側
```

`scripts/audit_scan.py` へ `B_line_ending` を追加し、
`git ls-files --eol` でindex側（`i/`）を見る形にした。

**204ファイルすべてが `i/lf` である。** リポジトリにはLFで格納されており、規約どおり。

`.gitattributes` はワークスペース・submodule のどちらにも存在しない。
`core.autocrlf` の設定だけで運用されているため、
**`core.autocrlf=false` の環境でコミットするとCRLFが混入しうる**。
現時点では混入していないが、`.gitattributes` に `*.cs text eol=lf` を置けば
設定に依存せず保証できる。**これは本監査の指摘ではなく、任意の堅牢化である。**

---

## 付録A: UTF-8 BOM の無い `.cs` / 改行コードがLFでない `.cs`

**いずれも該当なし（0件）。**

再生成:

```bash
python scripts/audit_scan.py --category B_missing_bom --category B_line_ending
```

**この2つは対象範囲が異なる。** BOM検査は `Runtime`/`Core`/`Editor`（155ファイル）、
改行検査は `git ls-files` 経由で `Tests`/`Samples` を含む全 `.cs`（204ファイル）である。
