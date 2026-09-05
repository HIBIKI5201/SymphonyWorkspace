# パッケージのドキュメント配置

Issue [#215](https://github.com/HIBIKI5201/SymphonyFramework/issues/215)

## 目的

`Documentation~/` に**入口の文書が無い**。5つの文書と`Modules/`が並んでいるだけで、初めて開いた人がどれから読むかを決められない。Package Manager から来ても、GitHub Pages から来ても、最初の1枚が存在しない。

HTML側には入口がある（`Documentation~/Html/index.html`）が、**これは`scripts/build_module_docs.py`のPython文字列から生成しており、Markdownの正本を持たない。** 「正本はMarkdown」という原則の例外がここだけにある。

Issueは併せて「LicenseとchangelogをDocumentation~へ移す」ことを求めているが、**着手前の検証でこの前提が崩れた。** 次節に記す。

## 着手前の検証で分かったこと

Unityの Package Validation Suite（`com.unity.package-validation-suite`）が要求する配置を、実装コードで確認した。

| 標準 | 検査内容 | 位置 |
| --- | --- | --- |
| US-0032 `LicenseIncluded` | `LICENSE.md` の存在。無ければ "Every package must have a LICENSE.md file." | **パッケージルート** |
| US-0039 `ChangelogExistsAndContainValidEntriesCheck` | `Path.Combine(packagePath, ChangeLogFilename)` を見る | **パッケージルート** |
| US-0040 `UserManualDocumentationIncluded` | ルートの `Documentation~/` に `*.md` が1つ以上。`Documentation` という名前なら "Please rename your \"Documentation\" folder to \"Documentation~\"" | `Documentation~/` |
| US-0065 `ThirdyPartyNotice` | `Third Party Notices.md` | **パッケージルート** |

実物でも同じである。Unity公式の `com.unity.inputsystem` は `CHANGELOG.md`・`LICENSE.md`・`README.md`・`package.json` をルートに置き、`Documentation~/` を別に持つ。

**したがって License と CHANGELOG は移さない。** 移すと Package Manager の Changelog / License リンクとパッケージ検証が壊れる。`Documentation/CONTRIBUTING.md` §2 も既に「パッケージリポジトリのルートには、外部の規約で置き場所が決まっているものだけを置きます」と書いており、現状の配置がその規約に従っている。

**ただし現状に US-0032 違反が1件ある。** ライセンスファイルが `LICENSE.txt` で、Unityが探すのは `LICENSE.md` である。**Issueの「Licenseの置き場がおかしい」という指摘自体は当たっており、直し方が移動ではなく拡張子の変更だった。**

`.meta` の importer は `LICENSE.txt` も `README.md` も同じ `TextScriptImporter` であるため、`.meta` ごとリネームすればGUIDは保たれる（両ファイルの`.meta`を実際に比較して確認した）。

GitHub Pages への配信は既に動いている（`.github/workflows/deploy-docs.yml`、2026-08-19に1回成功）。**配信元は `Documentation~/Html/` そのもの**であり、`index.html` がサイトのトップページになる。

## 決定

2026-09-05に回答を得た。**いずれも推奨どおり。**
根拠のやり取りは [質問](https://github.com/HIBIKI5201/SymphonyFramework/issues/215#issuecomment-5549949388) と
[決定](https://github.com/HIBIKI5201/SymphonyFramework/issues/215#issuecomment-5553312480) に残してある。

### 1. `Html/index.html` の正本をMarkdownへ移す

`Documentation~/index.md` を正本にし、`index.html` をそこから生成する。

現状の `build_module_docs.py` は `Documentation~/*.md` を1対1でHTMLへ変換したうえで、**`index.html` だけを `render_index()` というPython関数から別途生成している。** モジュールの並び順は `MODULE_ORDER` というPythonの定数が持つ。この Round で `render_index()` を廃止し、**`MODULE_ORDER` は「`index.md` が全モジュール文書をこの順でリンクしているか」の検査へ転用する。**

見送った案は「`Documentation.md` を普通のページとして足し、`index.html` は今まで通りPythonから生成する」である。変更は最小だが、**入口が2枚になり、同じリンク一覧をPythonとMarkdownの2箇所で保守することになる。** Issue #168 の振り返りで挙げた「同じ規則を2箇所が別々に持つと、片方だけ直っても気づけない」（`generate_meta.py` と preflight の `.meta` 判定）と同じ形を、新しく作ることになる。

**ファイル名は `index.md` とする。** Issueの文面は `documentation.md` だが、出力が `index.html` になるため名前が1対1で対応するほうが後から迷わない。US-0040 は `Documentation~/` 直下の `*.md` を1つ以上求めるだけで、名前は問わない（`your-package-name.md` というテンプレート由来の名前だけを明示的に弾く）。

**`Documentation~/index.md` という名前は、現状の実装では衝突する。** `build()` が per-document のループの**後**に `rendered[INDEX_OUTPUT] = render_index(documents)` を実行するため、`index.md` から作った `index.html` が黙って上書きされる。`render_index()` の廃止はこの衝突の解消でもある。

### 2. `package.json` へ `documentationUrl` を足す

`documentationUrl` に GitHub Pages のURL（`https://hibiki5201.github.io/SymphonyFramework/`）を設定する。Pagesの配信は `.github/workflows/deploy-docs.yml` で既に1回成功している（2026-08-19）。**`main` へのpushでしか更新されない**ため、`develop` の内容とは最大1リリース分ずれる。同梱の `index.md` が別途あるため、どちらか一方しか無い状態にはならない。

併せて `description` を差し替える。現在は `"This is Symphony Framework"` で**テンプレートのまま**であり、これがPackage Managerの一覧に出る説明文である。Issueの「UPMの説明文を付けるべき」に直接あたる。

**このコンテナからは `hibiki5201.github.io` への接続がプロキシに拒否されるため、URLが実際に開けることを確認できない。** 到達確認は依頼者の確認項目として残す。

## 公開API

**変更しない。** C#の型・メンバーを追加も変更も削除もしない。

## ファイル構成

推奨（質問1=B、質問2=A）を採った場合。

| パス | 変更 | 内容 |
| --- | --- | --- |
| `Assets/SymphonyFrameWork/LICENSE.txt` | **リネーム** | `LICENSE.md` へ。`.meta` も同時にリネームしGUIDを保つ |
| `Assets/SymphonyFrameWork/Documentation~/index.md` | **新規** | 入口の文書。概要と全文書への導線 |
| `Assets/SymphonyFrameWork/package.json` | 変更 | `description` を実体へ、`documentationUrl` を追加 |
| `Assets/SymphonyFrameWork/README.md` | 変更 | `LICENSE.txt` へのリンクを `LICENSE.md` へ |
| `Assets/SymphonyFrameWork/Documentation~/Html/` | 再生成 | `index.html` と新規ページ |
| `scripts/build_module_docs.py` | 変更 | `index.md` → `index.html` の対応付け、`render_index()` の廃止、モジュールリンクの検査追加 |
| `Documentation/CONTRIBUTING.md` | 変更 | §2 の `LICENSE.txt` を `LICENSE.md` へ |

名前空間の変更は無い。

## 依存方向

**該当しない。** パッケージ内のC#コードを変更しないため、レイヤーも依存の向きも動かない。`scripts/build_module_docs.py` はワークスペース側のビルドツールであり、パッケージのアセンブリからは参照されない。

## エラー処理

`build_module_docs.py` の `check()` へ検査を1つ足す。

- **`Modules/*.md` のうち `index.md` からリンクされていないものがあれば、そのファイル名を挙げて失敗させる。** 逆に `index.md` が存在しないモジュールへリンクしている場合も失敗させる
- 並び順が `MODULE_ORDER` と違う場合も失敗させる。**順序は「READMEとAGENTS.mdの導線と同じ」という既存の意図を持っており、崩れても目視では気づけない**

これは `release_round.py preflight` の `[docs]` から呼ばれるため、モジュール文書を足して`index.md`へ書き忘れた Round はリリースできない。

例外は投げず、`check()` の戻り値である問題文字列のリストへ積む（既存の実装に合わせる）。

## 影響範囲

- **公開APIへの影響は無い。** シリアライズ形式も変えない
- `LICENSE.txt` → `LICENSE.md` は、**ファイル名を直接参照している利用側があれば壊れる。** ただしライセンス文書はコードから参照するものではなく、リポジトリ内の参照は `README.md` と `Documentation/CONTRIBUTING.md` の2箇所だけである（全文検索で確認済み）
- `Documentation~/Html/` の再生成により、`index.html` の生成元が変わる。**GitHub Pagesのトップページの内容が変わる**が、URLは変わらない
- Unityから `Window > SymphonyFrameWork > Documentation` で開く索引も同じ `index.html` である。**開く先のパスは変わらない**ため、Editor側のコード変更は不要（`EditorTools.md` に記載の導線をコードで確認する）

## テストの置き場と種別

**本体のソース（`Runtime/` `Core/` `Editor/`）を変更しないため、パッケージ内のテストは追加しない。**

```
--no-tests-reason "ドキュメントとpackage.jsonのみの変更で、本体のソースを変更していない"
```

`scripts/` はフローの解説どおり「ソースを変更していない」に該当する。

**ただし追加する検査そのものは実測する。** `build_module_docs.py --check` を、次の3状態で実行して結果を記録する。

1. 正しい `index.md` で通ること
2. モジュールへのリンクを1つ消すと、そのファイル名を挙げて落ちること
3. 並び順を入れ替えると落ちること

2と3は一時的に書き換えて確認し、確認後に戻す。**「たぶん落ちる」で済ませない。**

## 動作確認手順

Unityを使わずに確認できるもの。

| 項目 | 手段 |
| --- | --- |
| HTMLが正本と同期している | `python scripts/build_module_docs.py --check` |
| `index.html` が `index.md` から生成されている | 生成物先頭の `Generated by ... from Assets/SymphonyFrameWork/Documentation~/index.md` を確認 |
| リンク切れが無い | `index.html` の各 `href` に対応するファイルが存在することを確認 |
| `.meta` の対が揃っている | `python scripts/generate_meta.py --check` |
| `LICENSE.md` のGUIDが変わっていない | リネーム前後で `.meta` の `guid` を比較 |

**人（Unity）が要るもの。**

| 項目 | 理由 |
| --- | --- |
| `Window > SymphonyFrameWork > Documentation` が新しい `index.html` を開くこと | Editorメニューの実行 |
| Package Managerの説明文と Documentation リンクの表示 | Package Managerウィンドウの目視 |
| `LICENSE.md` がUnityで再インポートされ、GUIDが保たれること | Asset Databaseの再インポート |
| `documentationUrl` のURLが実際に開けること | **このコンテナからは接続が拒否される** |

## バージョン判断

**パッチ（6.14.1）。**

`Documentation/DesignPhilosophy.md` の `### バージョニング` は「公開契約を変えない修正はパッチ」としている。この Round は公開API・シグネチャ・既定値・シリアライズ形式のいずれも変えない。文書の追加は含むが、それは公開APIの追加ではない。

CHANGELOGの見出しは `Add`（入口の文書）と `Change`（LICENSEのファイル名、`description`）。**`Fix` は使わない**ため、`preflight` の「`Fix` を他と同居させない」検査には抵触しない。

## この Round で触るバージョン関連ファイル

**版は4か所ある。** `package.json` の `version`、`CHANGELOG.md` の見出し、`README.md` の「現在のバージョン」、`Core/SymphonyConstant.cs` の `VERSION`。**4か所とも `bump` が書く**（実装を読んで確認した）。加えて `bump` は `build_module_docs.write()` も呼ぶため、**版更新の時点でHTMLが再生成される。**

そのため `Core/SymphonyConstant.cs` が変更ファイルに入り、`preflight` の「本体のソースを変更した Round はテストも変更する」検査に掛かる。**この Round は `--no-tests-reason` で通す**（前節のとおり）。

この Round で更新するその他:

- `Documentation/CONTRIBUTING.md` §2（`LICENSE.txt` → `LICENSE.md`）
- `Assets/SymphonyFrameWork/README.md` の `## ドキュメント` 節（LICENSEリンク、入口文書への導線）

`AGENTS.md` のAPI早見表は**変更しない**。公開APIが変わらないため。

---

## 実施レポート

実施日: 2026-09-05 / バージョン: 6.14.1（Fix）・6.14.2（Add・Change） / PR: [#216](https://github.com/HIBIKI5201/SymphonyFramework/pull/216)

### 実装した内容

| 設計 | 実現した場所 |
| --- | --- |
| 入口のMarkdownを置く | `Documentation~/index.md`。モジュール12件、全体5件、AIエージェント向け2件を表で並べた |
| 索引の正本をMarkdownへ移す | `build_module_docs.py` の `render_index()` を削除し、`build()` から `rendered[INDEX_OUTPUT] = render_index(...)` の行を外した。`collect_documents()` は `Documentation~/*.md` を `Html/<stem>.html` へ写すため、**`index.md` は変更なしで `index.html` になる** |
| `MODULE_ORDER` を検査へ転用 | `check_module_links()` を追加し、`check()` の先頭で呼ぶ。`preflight` の `[docs]` から届く |
| `LICENSE.txt` → `LICENSE.md` | `git mv` を `.meta` にも行い、GUID `13026097c7a3f624fbb25f53037de6ba` を保持 |
| `package.json` の説明文 | `description` を差し替え、`documentationUrl` を `unity` の直後へ追加 |
| 参照の追随 | `README.md`（LICENSEリンク、索引への導線）、`Documentation/CONTRIBUTING.md` §2 |

### 設計から変えた点

**1. 検査の対象に `README.md` を足した。** 設計では `index.md` だけを見るつもりだったが、`README.md` も同じモジュール一覧を持っていることが着手後に分かった。**ただし README は機能紹介・文書一覧・モジュール一覧の3箇所から同じ文書へ張っており、出現順に意味が無い。** そのため README は件数（網羅）だけを見て、順序は `index.md` にだけ課している。

**2. `.module-list` のCSSを削除した。** `render_index()` だけが出力していたクラスで、廃止と同時に死んだ。全20ページのインラインCSSに残り続けるため落とした。

**3. Fix を独立した版へ切り出した。** 設計では 6.14.1 の1版で出すつもりだったが、着手後に**索引から Scene Block が抜けている**ことが判明した。`preflight` は `Fix` を他の見出しと同居させないため、6.14.1（Fix）と 6.14.2（Add・Change）の2版へ分けた。

### 検証結果

Unity Editor の無い環境で作業したため、コンパイルとテストは実行していない。

| 検査 | 実測値 |
| --- | --- |
| `build_module_docs.py --check` | `OK: 20件の生成物が正本と同期しています` |
| `release_round.py preflight` | 全項目OK。`[changelog]` 255件、`[bom]` 1件、`[meta]` 0件、`[docs]` 同期、`[question]` 未回答なし |
| `generate_meta.py --check` | `OK: .meta の欠落はありません（460件を走査）` |
| `LICENSE.md` のGUID | リネーム前後で `13026097c7a3f624fbb25f53037de6ba` と一致 |
| `package.json` | `json.load` で読めることを確認 |

**追加した検査は4つの失敗状態で実測した。** 「たぶん落ちる」で済ませていない。

| 状態 | 結果 |
| --- | --- |
| `index.md` から Pause Manager のリンクを消す | 期待と実際の一覧を並べて失敗 |
| `index.md` の Debug と Utility を入れ替える | 同上 |
| `README.md` から SceneBlock と Debug のリンクを落とす | `SceneBlock、Debug` を挙げて失敗 |
| `MODULE_ORDER` に無い `ProbeModule.md` を置く | `MODULE_ORDER` への追加を促して失敗 |

いずれも確認後に元へ戻し、`--check` が `OK` へ復帰することを確認した。

### 実装中に見つけた欠陥

**索引から Scene Block が抜けていた。** `MODULE_ORDER` は11件で、`Modules/` には12件ある。`render_index()` は `for stem in MODULE_ORDER if stem in by_stem` と書いており、**定数側に無いモジュールは黙って落ちる。** 文書は 6.6.0（2026-08-30）から存在していたが、索引からは辿れなかった。この Round で追加した検査が最初に捕まえた指摘である。

### 未実施の確認

Unity が要るもの。**依頼者の一括検証で確認する。**

- `Window > SymphonyFrameWork > Documentation` が新しい `index.html` を開くこと
- Package Manager での `description` の表示と、`Documentation` リンクの遷移先
- `LICENSE.md` が再インポートされ、GUIDが保たれること（`.meta` を書き換えていないため変わらないはずだが、Unityを通していない）
- **`documentationUrl`（`https://hibiki5201.github.io/SymphonyFramework/`）が実際に開けること。** 作業コンテナからは `hibiki5201.github.io` への接続がプロキシに拒否されるため、こちらでは確認できない。Pagesの配信ワークフローが2026-08-19に1回成功していることまでは確認した
- GitHub Pages のトップページが新しい `index.html` に入れ替わること。**配信は `main` への push でのみ走る**ため、`develop` から `main` へマージするまで反映されない

### 振り返り

| 気づき | 扱い |
| --- | --- |
| **「一覧を生成する定数」と「一覧の実体」がずれても、生成物は静かに出来上がる。** `for x in ORDER if x in available` の形は、欠落を落とすのではなく無かったことにする | この Round で検査を追加して解消した。**同じ形が他にもないかは見ていない。** `Documentation/CodeGuidelines.md` へ「定数の一覧で絞り込むとき、絞り落とした要素を報告するか」を足す候補 |
| Issueの前提が外部規約と食い違っていた。**規約の実装（Package Validation Suite）を読むまでは、どちらが正しいか決められなかった** | `design-doc.md` の「アクセス手段の成立を検証する」は自リポジトリのコードを想定している。**外部規約が絡む場合は「規約の実装か、公式が出している実物を確認する」**を足す候補 |
| リモートで `gh` が無いため `commit --pr` と `finalize` が毎回落ちる。今回もPR作成で例外停止した | 既知（`remote.md` §5に記載済み）。**ただし `release_round.py` 側が `gh` の不在を検知して案内する形にできる。** 例外のスタックトレースを読ませる必要はない |

**提案にとどめる。** スキルとドキュメントへの反映は、承認をもらってから行う。
