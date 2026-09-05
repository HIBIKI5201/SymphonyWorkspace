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
