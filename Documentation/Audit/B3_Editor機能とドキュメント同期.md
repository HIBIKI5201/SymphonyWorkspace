# B3. Editor機能と EditorTools.md の同期

**指摘なし。**

`AGENTS.md` §0.1 は「作業中に EditorTools.md へ記載の無い Editor モジュールを見つけたら
節を追加する。記載漏れは、その機能が存在しないのと同じ」と定めている。
`Editor/` 配下のディレクトリと
`Assets/SymphonyFrameWork/Documentation~/EditorTools.md` の節を突き合わせた。

## 調査サマリ

| 分類 | 件数 |
| --- | --- |
| `Editor/` 直下のディレクトリ | 8 |
| `EditorTools.md` の節 | 17 |
| 機械走査が「記載漏れ」と判定 | 2 |
| **読解の結果、実際の記載漏れ** | **0** |

**誤検出率が100%だったため、この検査は走査から削除した**（下記）。
以降この観点は読解で確認する。

---

## 機械走査の2件はいずれも誤検出

走査はディレクトリ名が `EditorTools.md` に文字列として現れるかを見ている。
**`EditorTools.md` は機能名で節を立てているため、ディレクトリ名とは一致しない。**

| ディレクトリ | 走査結果 | 実際の記載先 |
| --- | --- | --- |
| `Editor/AttributeDrawer` | 記載なし | **「## Inspector属性」節に記載あり** |
| `Editor/SettingProvider` | 記載なし | **「## Framework設定」「## Save System設定」「## Asset Store Tools Packager設定」の3節に記載あり** |

`SettingProvider` は Project Settings の3ページを提供するモジュールで、
**ドキュメントが利用者視点（設定ページごと）で書かれている**。
実装ディレクトリ単位で書くより読み手に有用であり、現状のほうが良い。

## 全ディレクトリの対応

| ディレクトリ | `EditorTools.md` の節 |
| --- | --- |
| `Administrator` | ## Symphony Administrator |
| `AttributeDrawer` | ## Inspector属性 |
| `Configs` | ## 設定アセットの自動生成 / ### 設定ファイルの置き場 |
| `Debug` | ## SymphonyDebugHUD / ## ログのファイル出力 / ## SymphonyMcpTools |
| `Generator` | ## Asset Store Tools Packager / ## AutoEnumGenerator / ## FolderGenerator / ## AssemblyGenerator |
| `Orchestrator` | ## Editorの初期化 |
| `PackageLoader` | ## SymphonyPackageLoader |
| `SettingProvider` | ## Framework設定 / ## Save System設定 / ## Asset Store Tools Packager設定 |

ディレクトリ直下の単独ファイルも対応している。

| ファイル | 節 |
| --- | --- |
| `SymphonyAssetProtector.cs` | ## アセット保護 |
| `TagsAndLayersPostProcessor.cs` | ## AutoEnumGenerator（変更検知の説明内） |
| `PackageInitializer.cs` | ## Editorの初期化 |

**8ディレクトリ・3ファイルすべてに対応する記載がある。**

## 走査からこの検査を削除した

現在の検査（ディレクトリ名の文字列一致）は**誤検出率が100%**で、
このままでは次回監査でも同じ2件が出続ける。

**`scripts/audit_scan.py` の `check_editor_docs_sync` を削除した。**
機能名の対応表を持たせる案もあったが、表自体の保守が必要になり、
`EditorTools.md` を直接読むより手間が増える。

「Editor機能を追加したらドキュメントを更新する」は `AGENTS.md` §0.1 の規約として
既に明文化されており、**実際に守られている（記載漏れ0件）**。
この観点は読解に委ねる。`.agents/skills/audit/references/perspectives.md` の B3 を
「検出: 読解」へ変更し、同種の検査を再び足さないよう理由を記載してある。

**削除した分の検査枠は、機械化が有効な2件へ振り替えた。**

| 追加した検査 | 拾えるもの |
| --- | --- |
| `B_deprecation_stale` | 削除済みシンボルの行が `Deprecations.md` に残っている（→ [B2](B2_Obsoleteとドキュメント同期.md)） |
| `B_line_ending` | リポジトリ側の改行コードがLFでない（→ [B8](B8_文字コードとmeta.md)） |

どちらも**ドキュメントやindexの側にしか手掛かりが無く、コードを読んでも分からない**種類の
検査で、機械化の価値が高い。

## メニューパスと設定位置の一致

`AGENTS.md` は「Project Settings へ統合済みの `Symphony Asset Lock` メニューへの言及が、
廃止後も残っていた」前例を挙げている。同種の記載を探した。

- `EditorTools.md` の「## アセット保護」節は Project Settings 経由での設定を説明しており、
  **廃止済みメニューへの言及は残っていない**
- `Documentation/CONTRIBUTING.md` も同様

**前例の修正は完了している。**

---

## 付録A: 削除前の機械走査の判定（全2件）

| 場所 | 走査結果 | 判定 |
| --- | --- | --- |
| `Assets/SymphonyFrameWork/Editor/AttributeDrawer` | 記載なし | 誤検出 |
| `Assets/SymphonyFrameWork/Editor/SettingProvider` | 記載なし | 誤検出 |

**この分類（`B_editor_module_undocumented`）は削除済みのため再生成できない。**
記録として残す。次回監査では上の対応表を手で辿ること。
