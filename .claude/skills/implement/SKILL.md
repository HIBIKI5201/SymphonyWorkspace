---
name: implement
description: "設計書を書き、Codex CLI に実装させ、実装を確認し、バージョンを更新してコミットするまでの実装フロー。SymphonyFramework 本体に機能を追加・変更するときに使う。"
---

# 実装フロー

SymphonyFramework 本体（`Assets/SymphonyFrameWork/`）へ機能を追加・変更するときの標準フロー。

```text
1. 設計書を書く  →  2. Codex に実装させる  →  3. 実装を確認する  →  4. バージョンを更新する  →  5. コミットする  →  6. 振り返る
```

各ステップは前のステップの完了を前提にする。**ステップを飛ばさない。** 特に 1 を飛ばして Codex を呼ばないこと。設計判断が残らず、レビューの基準も失われる。

ホスト側（`Assets/Scripts/` など、パッケージを利用するだけのコード）の変更にはこのフローを使わない。通常どおり直接実装する。

---

## 大きなタスクは Round に分割する

**1回のフローで扱えないタスクは、着手前に「Round」へ分割する。** 上のステップ1〜6は Round 1つ分の手順であり、Round ごとに設計書・実装・検証・バージョン・コミット・振り返りが1周する。

分割せずに大きなまま Codex へ投げると、次が同時に起きる。

- 差分が数十ファイルに及び、ステップ3の差分レビューが実質できなくなる
- どの変更がどの設計判断に対応するのか追えなくなる
- 検証が落ちたときに切り分けられない
- 1コミット1意図が守れない

### Round の切り方

**「単独で検証でき、単独でリリースできる」単位で切る。**

- 各 Round の終わりにコンパイルとテストが通り、公開APIが壊れていない状態になること
- 後続 Round を実施しなくても、その時点で整合が取れていること
- 目安として、Codex へ1回投げて差分を自分で全部読める規模（おおむね20ファイル以内）

切り方の例:

| 分割の軸 | 例 |
| --- | --- |
| 基盤 → 利用 | ユーティリティを追加する Round → それを使って既存を書き換える Round |
| サブシステム単位 | Scene Load → Service Locate → Save Data → Audio/Pause |
| 非破壊 → 破壊的 | 新APIの追加と `[Obsolete]` 化の Round → 旧API削除の Round |

### 進め方

1. **設計書に Round 分割を書く。** 各 Round が何を含み、何を含まないかを明示する。依存順があるなら順序も書く
2. **Round は1つずつ完了させる。** 前の Round がコミットまで終わってから次へ進む。複数 Round の変更を作業ツリーに同時に載せると、コミットの切り分けができなくなる
3. **Round ごとにバージョンを刻む。** 破壊的変更を含む Round は、複数 Round にまたがる改修の最後にまとめる。途中の Round は後方互換に保ち、マイナーまたはパッチとして出す
4. **振り返りは Round ごとに行う。** 次の Round の進め方へ即座に反映できる

---

## 1. 設計書を書く

`Documentation/Designs/<機能名>.md` を作成する。命名は PascalCase（例: `EventBus.md`、`SceneTransitionEffect.md`）。

書く前に必ず読む:

- `Documentation/DesignPhilosophy.md` — レイヤー、依存方向、公開範囲の判断基準。特に `## クラス設計` と `## 公開APIとバージョニング`
- `Documentation/CodeGuidelines.md` `## 名前空間` — 配置先の決定に必要

設計書の構成:

```markdown
# <機能名>

## 目的
何を解決するか。既存の何では足りないか。

## 公開API
利用側から見えるシグネチャ。Facade のメソッド、Value Object、例外、interface。
`public` にする根拠を DesignPhilosophy の「公開範囲」に照らして書く。

## ファイル構成
新規・変更するファイルのパスと名前空間。`Internal/` の内外を明示する。

## 依存方向
どのレイヤーに属し、何を参照するか。Core → Runtime → Editor の向きを崩していないこと。

## エラー処理
通常起こり得る失敗（戻り値・Try pattern）と、不変条件違反（例外）の区別。

## 影響範囲
既存の公開API・シリアライズ形式への影響。互換性を壊す場合は移行方法。

## テストの置き場と種別
自動テストを書くか。書くなら EditMode と PlayMode のどちらへ、どのパスへ置くか。
書かないなら理由を書く（モーダルダイアログを伴う、Unity のコールバックに強く依存する等）。
テストはパッケージではなくワークスペース側の `Assets/Tests/` へ置く。

## 動作確認手順
Play Mode で何をどう確認すれば成功と言えるか。期待するログや状態。

## バージョン判断
破壊的変更=メジャー / 後方互換な追加=マイナー / 実装のみ=パッチ のどれか。理由も書く。
```

設計書は実装後も設計判断の記録として残す。破棄しない。

**設計書ができたら、実装へ進む前にユーザーへ提示して合意を取る。**

---

## 2. Codex に実装させる

`codex` は PATH に無いため、実行ファイルを解決してから呼ぶ（インストール先のディレクトリ名は更新で変わるので固定しない）。

作業ルート（`-C`）は**ワークスペースのルート**にする。Codex が `Documentation/` の規約と `Assets/SymphonyFrameWork/` のソースの両方を読めるようにするため。

```powershell
$exe = (Get-ChildItem "$env:LOCALAPPDATA\OpenAI\Codex\bin\*\codex.exe" | Sort-Object LastWriteTime -Descending | Select-Object -First 1).FullName
& $exe exec -s workspace-write -C "C:\Sinfonia Studio\SymphonyFrameworkWorkspace" "<プロンプト>"
```

実装は数分以上かかるため、**`run_in_background: true` で実行する**こと。前景で走らせるとタイムアウトする。

**Codex の検証環境はネットワークが遮断されていることがあり、`npx` が `ENOTCACHED` で失敗する場合がある。** そのため Codex が報告するコンパイル結果やテスト件数は環境差を含む。**報告された数値は必ずステップ3で自分で再実行して確認すること。**

### プロンプトのテンプレート

設計書の中身をコマンドラインへ展開せず、パスを渡して Codex に読ませる。

```text
Documentation/Designs/<機能名>.md の設計に従って実装してください。

必ず先に読むもの:
- Documentation/CodeGuidelines.md（命名、書式、メンバー順、XMLドキュメント、非同期処理、Unity固有ルール）
- Documentation/DesignPhilosophy.md（レイヤー、依存方向、公開範囲）
- Documentation/CONTRIBUTING.md の「2. .metaファイルの扱い」と「3. 文字コードと改行」

制約:
- 変更してよいのは Assets/SymphonyFrameWork/ 配下だけ。それ以外のファイルは触らない。
- .cs は UTF-8 BOM付きで保存する。
- .meta は生成しない。手書きもしない。新規ファイルの .meta は後で Unity Editor に生成させる。
- package.json と CHANGELOG.md は変更しない。バージョン更新は別ステップで行う。
- 設計書に書かれていない public API を追加しない。迷ったら internal にする。
- Runtime/ と Core/ から UnityEditor を参照しない。

実装が完了したら、追加・変更したファイルのパスを一覧で報告してください。
```

### オプション

| 用途 | オプション |
| --- | --- |
| 最終メッセージをファイルへ保存 | `-o <ファイルパス>` |
| 進行をJSONLで取得 | `--json` |
| セッションを保存しない | `--ephemeral` |
| モデルを変える | `-m <モデル名>`（既定は `~/.codex/config.toml` の設定） |

`-s workspace-write` はファイル書き込みに必須。`--dangerously-bypass-approvals-and-sandbox` は使わない。

---

## 3. 実装を確認する

Codex の報告を鵜呑みにしない。**必ず差分を自分で読む。**

1. **差分レビュー** — `git -C "Assets/SymphonyFrameWork" status` と `git -C "Assets/SymphonyFrameWork" diff` で全変更を確認する。設計書に無い変更、範囲外のファイル、`public` の増加を特に見る。
2. **規約チェック** — `Documentation/CodeGuidelines.md` の `## レビュー用チェックリスト` を通す。名前空間とフォルダの一致、`Internal/` の使い分け、XMLドキュメント、CancellationToken の伝播、登録と解除の対。

   目視だけに頼らず、**機械的に検索できる違反は検索する**。特に次の2つは過去に見落としが起きている。

   ```
   grep -rn "UnityEditor\|EditorPrefs" Assets/SymphonyFrameWork/Runtime Assets/SymphonyFrameWork/Core --include=*.cs
   ```

   - Runtime／Core から `UnityEditor` への参照（`#if UNITY_EDITOR` で囲んであっても違反）
   - テスト用 asmdef の `defineConstraints` に `UNITY_INCLUDE_TESTS` があるか（無いと nunit を参照するアセンブリが Player ビルドへ入る）
3. **コンパイル** — `uloop-clear-console` → `uloop-compile` → `uloop-get-logs`。エラー0件、意図しない警告なし。
4. **ランタイム確認** — `uloop-control-play-mode` で設計書の「動作確認手順」を実行し、`uloop-get-logs` で期待値と照合する。**Domain Reload が無効なので、Play Mode の開始・終了を2回繰り返し、static 状態のゴースト参照が残らないことを確認する。**
5. **`.meta` の生成** — 新規ファイルを追加した場合、`.meta` は Unity Editor がフォーカスを得たときに生成される。`uloop-focus-window` を使うか、ユーザーへ依頼する。`git status` で `.cs` と `.meta` が対で揃っていることを確認してからコミットへ進む。

問題があれば Codex へ差し戻す（同じ `codex exec` に修正内容を渡す）か、軽微なら自分で直す。**設計書と実装が食い違った場合は、どちらが正しいかをユーザーに確認する。**

---

## 4. バージョンを更新する

`Assets/SymphonyFrameWork/package.json` の `version` と `CHANGELOG.md` の見出しを**同時に**更新する。SemVer の判断は設計書の「バージョン判断」と `Documentation/DesignPhilosophy.md` の `### バージョニング` に従う。

CHANGELOG の形式:

```markdown
## [x.y.z] - YYYY-MM-DD
### Add
- 追加したものと、それが何を解決するか。

### Change
- 変えた内容と、利用側への影響（影響がないならその理由）。
```

見出しは `Add` / `Change` / `Fix` / `Deprecated` / `Breaking`。`Breaking` と `Deprecated` には**移行方法を必ず書く**。「何をしたか」だけでなく「なぜそうしたか」「利用側にどう影響するか」まで書く。

公開APIを変更した場合は、`Documentation/CONTRIBUTING.md` の `## 6. 変更に応じて同時に更新するもの` の表にあるファイル（`README.md`、`AGENTS.md`、該当する Sample）も同じ変更内で更新する。

---

## 5. コミットする

submodule と親リポジトリの2段階。**順序を守る。**

1. submodule で作業ブランチを切る（未作成なら）
   ```
   git -C "Assets/SymphonyFrameWork" switch -c feature/<機能名>
   ```
2. submodule でコミット
   ```
   git -C "Assets/SymphonyFrameWork" add -A
   git -C "Assets/SymphonyFrameWork" commit -m "[add]<日本語の要約>"
   ```
   prefix は `[add]` / `[update]` / `[fix]`。prefix と本文の間にスペースを入れない。メッセージは日本語。
3. submodule を push する。**push しないまま親の gitlink を更新すると、他の開発者が解決できない参照になる。**
4. 親リポジトリで gitlink と、設計書（`Documentation/Designs/<機能名>.md`）をコミットする。

`.meta` の追加は対応する `.cs` と同じコミットに含める。1コミットは1つの意図にまとめる。

**コミットとpushはこのフローの一部として実行する。** ステップ3の検証がすべて通っていることが前提。

- **検証が1つでも失敗しているならコミットしない。** 直してから戻る
- 今回の変更と無関係な既存の未コミット変更が混ざっている場合は、**別コミットへ分ける**。判断がつかないものは残したまま、その旨を報告する
- Unity や IDE が書き換えた `ProjectSettings/`、`.slnx`、生成物などは、自分の変更でない限り含めない
- 何をコミットし、何を意図的に残したかを必ず報告する

---

## 6. 振り返る

ラウンドを閉じる前に、**そのラウンドで起きた手戻りを振り返り、仕組みへ還元できるものをユーザーへ提案する**。ここを飛ばすと、同じ種類の欠陥を毎回レビューで拾い直すことになる。

見る対象:

- **Codex へ差し戻した内容** — 何を指示し忘れたのか。プロンプトのテンプレートか設計書のテンプレートに項目が足りていなかった可能性がある。
- **レビューで見つけた欠陥の傾向** — 同じ種類が2回以上出たら、それは個別の見落としではなく仕組みの穴。
- **設計書と実装が食い違った箇所** — 設計書の記述が曖昧だったのか、実装が逸脱したのか。
- **手順の途中で判断に迷った点** — ドキュメントに書かれていない暗黙の前提がある。

提案する先:

| 気づき | 提案先 |
| --- | --- |
| 繰り返す手作業、毎回同じ確認 | 新しいスキル、またはこのスキルへの手順追加 |
| コードの書き方・配置・命名の抜け | `Documentation/CodeGuidelines.md` |
| レイヤー、依存方向、公開範囲の判断が割れた | `Documentation/DesignPhilosophy.md` |
| 作業手順、コミット、バージョン、検証の抜け | `Documentation/CONTRIBUTING.md` |
| 設計書に書くべき項目の不足 | このスキルのステップ1のテンプレート |

**提案にとどめる。ドキュメントやスキルを勝手に書き換えない。** 何をどう変えるかまで具体的に示し、ユーザーの承認を得てから反映する。

気づきが無ければ「無し」と述べて閉じる。無理に挙げる必要はない。
