---
name: implement
description: "設計書を書き、利用中のAIに応じたワーカーが実装し、実装を確認してバージョン更新・コミット・振り返りまで行うフロー。SymphonyFramework 本体に機能を追加・変更するときに使う。"
---

# 実装フロー

SymphonyFramework 本体（`Assets/SymphonyFrameWork/`）へ機能を追加・変更するときの標準フロー。

```text
1. 設計書を書く  →  2. ワーカーが実装する  →  3. 実装を確認する  →  4. バージョンを更新する  →  5. コミット・PR  →  6. 振り返る
```

各ステップは前のステップの完了を前提にする。**ステップを飛ばさない。** 特に 1 を飛ばしてワーカーに実装させないこと。設計判断が残らず、レビューの基準も失われる。

ホスト側（`Assets/Scripts/` など、パッケージを利用するだけのコード）の変更にはこのフローを使わない。通常どおり直接実装する。

---

## Issue 対応は専用ブランチで行う

特定の GitHub Issue に対応する場合は、**設計や実装へ着手する前に**、submodule の `develop` からその Issue 専用のブランチを作る。複数の無関係な Issue を同じブランチで扱わない。

- 命名規則は `feature/<Issue番号>-<短い機能名>`（例: `feature/101-module-docs`）
- 修正と検証が完了したらブランチを push し、`develop` をベースとする Pull Request を作成する
- PR 本文には `Issue: #<Issue番号>` を記載し、マージ後に対応する Issue を閉じる

GitHub の自動クローズ用キーワードは、PR のベースがリポジトリの既定ブランチである場合だけ有効になる。現在の既定ブランチは `main` なので、`develop` 向けPRの `Closes #<Issue番号>` では自動クローズされない。既定ブランチを `develop` へ変更した場合は、`Issue: #<Issue番号>` の代わりに `Closes #<Issue番号>` を使う。

Issue が無い機能追加・変更でも、従来どおり `develop` から `feature/<短い機能名>` を作り、`develop` へ Pull Request を作成する。

---

## 大きなタスクは Round に分割する

**1回のフローで扱えないタスクは、着手前に「Round」へ分割する。** 上のステップ1〜6は Round 1つ分の手順であり、Round ごとに設計書・実装・検証・バージョン・コミット・振り返りが1周する。

分割せずに大きなままワーカーへ渡すと、次が同時に起きる。

- 差分が数十ファイルに及び、ステップ3の差分レビューが実質できなくなる
- どの変更がどの設計判断に対応するのか追えなくなる
- 検証が落ちたときに切り分けられない
- 1コミット1意図が守れない

### Round の切り方

**「単独で検証でき、単独でリリースできる」単位で切る。**

- 各 Round の終わりにコンパイルとテストが通り、公開APIが壊れていない状態になること
- 後続 Round を実施しなくても、その時点で整合が取れていること
- 目安として、ワーカーが1回で実装でき、差分を自分で全部読める規模（おおむね20ファイル以内）

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
テストは**パッケージ内の `Assets/SymphonyFrameWork/Tests/`** へ置く（EditMode は `Tests/Editor/`、PlayMode は `Tests/Runtime/`）。
`InternalsVisibleTo` によりテストアセンブリから `internal` な内部実装も検証できるため、
Entity・Service・Registry など公開されない型も単体テストの対象にする。

**何を検証するかだけでなく、どう書くかを1行で書く。** 検証内容だけを書くと、実現不可能な要件に気づけない。
過去に「PlayModeテストで Play Mode の開始・終了を2回繰り返す」と書いたが、PlayModeテストは
Play Mode 内で走るため1テストの中で抜けて再入できず、原理的に不可能だった。
テストは `internal` へ触れられない（公開APIの範囲に留める方針）ことも前提に置く。

## 動作確認手順
Play Mode で何をどう確認すれば成功と言えるか。期待するログや状態。

## バージョン判断
破壊的変更=メジャー / 後方互換な追加=マイナー / 実装のみ=パッチ のどれか。理由も書く。
複数Roundに分けるなら、Roundごとにどのバージョンを出すかも書く。

## この Round で触るバージョン関連ファイル
`package.json` の `version`、`CHANGELOG.md` の見出し、`README.md` の「現在のバージョン」、
`AGENTS.md` のAPI早見表など、この Round で更新するものを列挙する。
**複数Roundが同じファイルを触る場合は、どのRoundがどの行を触るかまで書く。**
同じファイルへ複数Roundの変更が同時に載ると、コミットを意図ごとに分けられなくなる。
```

設計書は実装後も設計判断の記録として残す。破棄しない。

### 確定する前に、アクセス手段の成立を検証する

**「この経路で目的の状態や型へ到達できる」と書いたなら、書いた時点でコードを見て確かめる。** 机上で成立するはずの経路が実際には届かない、という誤りは設計書の中で最も高くつく。

最低限、次を確認する。

- 参照しようとしているメンバーの**可視性**（`private` / `internal` / `public`）。型が `internal` でも、それを保持するフィールドが `private` なら `InternalsVisibleTo` では届かない
- そのアセンブリから本当に参照できるか（asmdef の参照、`InternalsVisibleTo` の有無、Editor/Runtime の境界）
- 必要な値を返す既存のアクセサがあるか。無いなら何を追加する必要があるか

**既存コードに回避策（リフレクション、`#if`、コピー実装）がある場合は、それが存在する理由を説明できるまで設計を確定しない。** 回避策は多くの場合、素直な経路が塞がっている証拠である。「既存コードがなぜか遠回りしている」と感じたら、そこに設計書が見落とす制約がある。

**「各サブシステムの X を〜する」のような横断的な前提を置いたら、その X が全対象に存在し、必要な可視性を持つことを列挙して確認する。** 一部にあることは全部にあることの証拠にならない。過去に「各サブシステムの `ResetRuntimeState` を逆順で呼ぶ」と書いた設計書が、6つ中3つに存在せず、残る3つのうち2つが `private` だったために差し戻しになっている。対称性は思い込みやすい。

**設計書ができたら、実装へ進む前にユーザーへ提示して合意を取る。**

---

## 2. ワーカーが実装する

ワーカーは、合意済みの設計書に従って実装差分を作る主体を指す。利用中のAIごとに次のワーカーを使う。

| 利用中のAI | ワーカー | 実行方法 |
| --- | --- | --- |
| Claude Code | Codex CLI | `scripts/codex_runner.py` 経由で別プロセスへ委任する |
| Codex | 自身 | 現在のタスク内でそのまま実装する。`codex_runner.py` や別の Codex CLI は起動しない |
| Gemini CLI | Codex CLI | `scripts/codex_runner.py` 経由で別プロセスへ委任する |

どのワーカーでも、設計書の前提が崩れた場合は推測で補わず、ファイルを変更する前にユーザーへ報告する。実装後は追加・変更したファイルを一覧で示し、ステップ3のレビュー担当が全差分を確認できる状態にする。

### Claude Code / Gemini CLI: Codex CLI ワーカー

**`scripts/codex_runner.py` を使う。** `codex` を直接呼ばない。このラッパーが次を担う。

- **残量チェック** — Codex の残枠が閾値未満なら API を叩かずに `exit 2` で止まる。無駄打ちを防ぐ
- **タイムアウト** — 既定2700秒で打ち切る。Codex が Unity 再起動などでハングしたまま何時間も止まる事故を防ぐ
- **プロンプトを stdin で渡す** — 長文の日本語プロンプトでもコマンドライン長と文字化けの影響を受けない
- **実行ファイルの解決** — `CODEX_BIN` → PATH → `config.toml` → 同梱バイナリの最新、の順で探す

プロンプトはファイルへ書いてから渡す。作業ルート（`--cd`）は**ワークスペースのルート**にする。Codex が `Documentation/` の規約と `Assets/SymphonyFrameWork/` のソースの両方を読めるようにするため。

```bash
python scripts/codex_runner.py --prompt-file <プロンプトファイル> --cd . --last-message <保存先>
```

終了コードで分岐する。

| コード | 意味 | 対応 |
| --- | --- | --- |
| 0 | 正常終了 | ステップ3へ進む |
| 1 | 実行失敗・タイムアウト | **途中まで書き込まれている可能性がある。** `git status` で確認してから判断する |
| 2 | 残枠不足でスキップ | Codex CLI の残量回復を待つか、その Round を呼び出し側が実装する |

実行前に残量だけ見たい場合:

```bash
python scripts/codex_runner.py --check-only --json
```

`--output` を渡すと単一ファイル生成モードになるが、**このフローでは使わない**。1 Round は複数ファイルを横断的に変更するため。

実装は数分以上かかるため、呼び出し側が長時間コマンドを扱える設定で実行する。バックグラウンド実行が可能なら使い、短いツールタイムアウトのまま前景で走らせない。

**Codex CLI ワーカーの検証環境はネットワークが遮断されていることがあり、`npx` が `ENOTCACHED` で失敗する場合がある。** そのためワーカーが報告するコンパイル結果やテスト件数は環境差を含む。**報告された数値は必ずステップ3で自分で再実行して確認すること。**

**Codex CLI ワーカーが長時間応答しない場合は打ち切って自分で引き継ぐ。** 実質的な出力が止まってから30分を目安にする。過去に Unity の再起動待ちで3時間半ハングし、その間コンパイルエラーを1件残したままだった事例がある。`codex_runner.py` のタイムアウトが最終的な歯止めになるが、それ以前に気づいたら待たずに止めてよい。

#### Codex CLI ワーカー用プロンプトのテンプレート

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

## 前提が崩れたときの扱い

設計書の前提が実際のコードと食い違っていた場合、**推測で埋めずに実装を中断し、報告してください。**
勝手に代替手段（リフレクション、可視性の変更、設計書に無い型の追加など）へ切り替えないこと。

報告には次を含めてください。

- 何が食い違っていたか（該当ファイルと行）
- 取りうる選択肢と、それぞれが何を犠牲にするか
- どれを推奨するか

判断を仰いでから再開します。**この時点ではファイルを変更しないでください。**

実装が完了したら、追加・変更したファイルのパスを一覧で報告してください。
```

このブロックは**毎回そのままプロンプトへ含める。** 設計書の前提はしばしば誤っており、ワーカーが黙って回避策へ切り替えると、その Round の目的自体が失われることがある。実際に「`InternalsVisibleTo` で届く」という誤った前提のまま実装させかけた事例がある（ワーカーが中断して確認したため防げた）。

#### Codex CLI ワーカーのオプション

| 用途 | オプション |
| --- | --- |
| 最終メッセージをファイルへ保存 | `-o <ファイルパス>` |
| 進行をJSONLで取得 | `--json` |
| セッションを保存しない | `--ephemeral` |
| モデルを変える | `-m <モデル名>`（既定は `~/.codex/config.toml` の設定） |

`-s workspace-write` はファイル書き込みに必須。`--dangerously-bypass-approvals-and-sandbox` は使わない。

### Codex: 自身がワーカー

設計書への合意後、現在のタスク内で実装を続ける。外部の `codex` プロセスや `scripts/codex_runner.py` は起動しない。

実装時は上のプロンプトテンプレートにある制約を自分の作業制約として適用する。設計書の前提とコードが食い違った場合は実装を止め、食い違い・選択肢・推奨案をユーザーへ示して判断を仰ぐ。実装完了後、そのままステップ3へ進む。

---

## 3. 実装を確認する

ワーカーの報告を鵜呑みにしない。**必ず差分を自分で読む。** Codex が自身をワーカーとする場合も、このレビュー工程を省略しない。

1. **差分レビュー** — `git -C "Assets/SymphonyFrameWork" status` と `git -C "Assets/SymphonyFrameWork" diff` で全変更を確認する。設計書に無い変更、範囲外のファイル、`public` の増加を特に見る。
2. **規約チェック** — `Documentation/CodeGuidelines.md` の `## レビュー用チェックリスト` を通す。名前空間とフォルダの一致、`Internal/` の使い分け、XMLドキュメント、CancellationToken の伝播、登録と解除の対。

   目視だけに頼らず、**機械的に検索できる違反は検索する**。特に次の2つは過去に見落としが起きている。

   ```
   rg -n "UnityEditor|EditorPrefs" Assets/SymphonyFrameWork/Runtime Assets/SymphonyFrameWork/Core -g '*.cs'
   ```

   - Runtime／Core から `UnityEditor` への参照（`#if UNITY_EDITOR` で囲んであっても違反）
   - テスト用 asmdef の `defineConstraints` に `UNITY_INCLUDE_TESTS` があるか（無いと nunit を参照するアセンブリが Player ビルドへ入る）
3. **コンパイル** — `uloop-clear-console` → `uloop-compile` → `uloop-get-logs`。エラー0件、意図しない警告なし。
4. **ランタイム確認** — `uloop-control-play-mode` で設計書の「動作確認手順」を実行し、`uloop-get-logs` で期待値と照合する。**Domain Reload が無効なので、Play Mode の開始・終了を2回繰り返し、static 状態のゴースト参照が残らないことを確認する。**
5. **`.meta` の生成** — 新規ファイルを追加した場合、`.meta` は Unity Editor がフォーカスを得たときに生成される。`uloop-focus-window` を使うか、ユーザーへ依頼する。`git status` で `.cs` と `.meta` が対で揃っていることを確認してからコミットへ進む。

### Unity Scene検証ガード

Sample Scene、Build Settings、Prefabを使うランタイム確認では、検証操作を成果物へ混入させないため次の順序を守る。

1. **検証前の状態を記録する。** 親とsubmoduleの`git status --short`を取得し、対象の`.unity`、`.prefab`、`ProjectSettings/EditorBuildSettings.asset`、自動生成enum、`.slnx`が既にdirtyか確認する。既存のdirty変更はユーザーのものとして扱い、自動復元の対象にしない
2. **Sceneを保存しない。** 検証用GameObjectの生成・破棄はPlay Mode内だけで行う。`EditorSceneManager.SaveScene`、`SaveCurrentModifiedScenesIfUserWantsTo`、動的実行のsave相当オプションを使わない。Sceneを開いた直後は`isDirty == false`を確認する
3. **複数行の動的コードは一時`.csx`へ書く。** PowerShell上のinlineコードは補間文字列や空白で引数分割されるため、`Temp/`配下の`.csx`を`--code-file`で実行し、完了後に削除する。短い1文だけinline実行を許可する
4. **時間依存の確認ではフレーム進行を検証する。** `uloop-focus-window`後も`Time.time`が進まない場合だけ、Play Mode中の`Application.runInBackground`を一時的に`true`へ設定する。待機時間だけで成功と判断せず、期待する状態またはログを読み取る
5. **Play Mode停止後に差分を照合する。** submoduleの`.unity`／`.prefab`差分と親のBuild Settings・生成物を確認する。意図しないpackage asset差分があればコミットへ進まず、保存せずに原因を調べる
6. **復元は事前にcleanだった既知ファイルだけへ限定する。** Unityが今回の検証で書き換えたと確認できるファイルだけを明示パスで戻す。作業ツリー全体への`git restore`や、検証前からdirtyだったファイルの復元を行わない

問題があればワーカーへ差し戻す。Claude Code / Gemini CLI は同じ `codex exec` に修正内容を渡し、Codex は現在のタスク内で修正する。軽微ならレビュー担当が直接直してもよい。**設計書と実装が食い違った場合は、どちらが正しいかをユーザーに確認する。**

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

## 5. コミットし、Pull Request を作成する

submodule と親リポジトリの2段階。**順序を守る。**

1. submodule が、この Round 専用の作業ブランチになっていることを確認する。特定の Issue へ対応する場合は、着手前に作成したブランチを使う
   ```
   git -C "Assets/SymphonyFrameWork" branch --show-current
   ```
2. submodule でコミット
   ```
   git -C "Assets/SymphonyFrameWork" add -A
   git -C "Assets/SymphonyFrameWork" commit -m "[add]<日本語の要約>"
   ```
   prefix は `[add]` / `[update]` / `[fix]`。prefix と本文の間にスペースを入れない。メッセージは日本語。
3. submodule を push する。**push しないまま親の gitlink を更新すると、他の開発者が解決できない参照になる。**
4. submodule で `develop` 向けの Pull Request を作成する。Issue 対応の場合は、PR 本文に `Issue: #<Issue番号>` を記載する
5. PR がマージされたら、対応する Issue を閉じる。既定ブランチが `develop` へ変更されている場合は、PR 本文の `Closes #<Issue番号>` による自動クローズを使う
6. 親リポジトリで gitlink と、設計書（`Documentation/Designs/<機能名>.md`）をコミットする。

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

- **ワーカーへ差し戻した内容** — 何を指示し忘れたのか。プロンプトのテンプレートか設計書のテンプレートに項目が足りていなかった可能性がある。
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
