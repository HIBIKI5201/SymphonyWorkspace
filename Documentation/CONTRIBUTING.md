# CONTRIBUTING.md — Symphony Framework 本体開発ガイド

このファイルは、**`Assets/SymphonyFrameWork/` にある Symphony Framework パッケージ本体のソースを変更する**人およびAIエージェント向けの作業手順です。

パッケージを**利用する側**のコード（このワークスペースの `Assets/Scripts/` など）を書く場合は、このファイルではなく [Assets/SymphonyFrameWork/AGENTS.md](../Assets/SymphonyFrameWork/AGENTS.md) を読んでください。ワークスペース全体の構成は [AGENTS.md](../AGENTS.md) にあります。

## 0. 最初に読むもの

| ドキュメント | 読むタイミング |
| --- | --- |
| このファイル | 常に。作業の進め方・コミット・検証の手順 |
| [CodeGuidelines.md](./CodeGuidelines.md) | コードを書く前に。命名・書式・コメント・非同期・Unity固有ルール |
| [DocumentationGuidelines.md](./DocumentationGuidelines.md) | Markdown文書を書く前に。文書の役割分担、節の順序、分量、AIに書かせるときの手順 |
| [DesignPhilosophy.md](./DesignPhilosophy.md) | 型・クラス・名前空間を**新設**する前、または公開範囲を判断する前。全部を通読せず該当する節（`## クラス設計`、`## 公開APIとバージョニング` など）を参照する |
| [../AGENTS.md](../AGENTS.md) | ワークスペースの構成、環境設定、自動生成物の区別を知りたいとき |
| [../Assets/SymphonyFrameWork/AGENTS.md](../Assets/SymphonyFrameWork/AGENTS.md) | 利用者向け文書の導線や、全作業で常時守る制約を変更するとき |
| [../Assets/SymphonyFrameWork/Documentation~/AgentUsage.md](../Assets/SymphonyFrameWork/Documentation~/AgentUsage.md) | 公開型のnamespaceや入口が変わり、AI向けAPI索引を更新するとき |
| [../Assets/SymphonyFrameWork/Documentation~/EditorTools.md](../Assets/SymphonyFrameWork/Documentation~/EditorTools.md) | Editor機能を追加・変更するとき。**記載の無いモジュールを見つけたら追記する** |
| [../Assets/SymphonyFrameWork/Documentation~/Deprecations.md](../Assets/SymphonyFrameWork/Documentation~/Deprecations.md) | `[Obsolete]` を付ける・外すとき。今どのAPIが非推奨で残っているかを知りたいとき |
| [../Assets/SymphonyFrameWork/CHANGELOG.md](../Assets/SymphonyFrameWork/CHANGELOG.md) | 直近の変更の経緯を知りたいとき。記述の粒度の参考にもする |
| [../.agents/skills/implement/SKILL.md](../.agents/skills/implement/SKILL.md) | 機能を**追加・変更**するとき。設計書からコミットまでの実装フロー |

既存コードの修正だけであれば、このファイルと CodeGuidelines.md で足ります。

### 機能の追加・変更は実装フローに従う

パッケージ本体へ機能を追加・変更する場合は、次のフローを踏みます。手順の詳細は [`.agents/skills/implement/SKILL.md`](../.agents/skills/implement/SKILL.md) にあります。Claude Code、Codex、Gemini CLI は同じ正本を利用し、実装ワーカーだけを実行中のAIに応じて切り替えます。

```text
1. 設計書を書く  →  2. ワーカーが実装する  →  3. 実装を確認する  →  4. バージョンを更新する  →  5. コミットする  →  6. 振り返る
```

設計書は [`Documentation/Designs/`](./Designs/) に機能ごとの1ファイルとして置き、実装後も設計判断の記録として残します。既存コードの小さな修正や、ホスト側（`Assets/Scripts/` など）のコードにはこのフローを使いません。

## 1. リポジトリの構造上の前提

このワークスペースには **gitリポジトリが2つ** あります。どちらを操作しているかを常に意識してください。

| | ワークスペース（親） | パッケージ（submodule） |
| --- | --- | --- |
| リポジトリ | `HIBIKI5201/SymphonyWorkspace` | `HIBIKI5201/SymphonyFramework` |
| ルート | このリポジトリのルート | `Assets/SymphonyFrameWork/` |
| 役割 | 開発環境。Unityプロジェクト、検証用シーン、本ドキュメント | 配布物。UPMパッケージ本体 |
| 既定ブランチ | `main` | `develop` |

- **パッケージのソース変更は、必ず submodule 側（`Assets/SymphonyFrameWork/`）でコミットします。** 親リポジトリはパッケージのファイルを追跡せず、submodule のコミットハッシュ（gitlink）だけを記録します。
- コマンドラインからは `git -C "Assets/SymphonyFrameWork" ...` のように対象を明示すると取り違えを防げます。
- クローンは `git clone --recurse-submodules` で行います。忘れた場合は `git submodule update --init --recursive` を実行してください。
- `Assets/SymphonyFrameWork.meta` は submodule の**外側**にあり、親リポジトリが管理します。削除すると利用側プロジェクトのGUID参照が壊れるため、絶対に削除しないでください。
- **このリポジトリ単体ではコンパイルできません。** `dotnet build`、`msbuild`、`csc` は使わないでください。asmdefの参照解決はUnityが行うため、コンパイル可否の判断はUnity Editorに委ねます（→ §4）。
- `Assets/SymphonyFrameWork/Cache/` はランタイム生成物（`SymphonyDebugLogger` のログ）です。`.gitignore` 済みで、編集も削除も自由ですが、コミットしません。
- `.claude/` と `.agents/` はローカル専用領域です。**他の開発者やエージェントにも守らせたいルールは、必ずこのファイルなど追跡対象のドキュメントに書いてください。**
- **ドキュメントの置き場所には基準があります。** 本体開発向けのドキュメント（このファイル、CodeGuidelines、DocumentationGuidelines、DesignPhilosophy）は**ワークスペース側の `Documentation/`** に置きます。パッケージ利用者向けの詳細文書は、Unity PackageのAsset Import対象外となる`Documentation~/`へ置きます。パッケージリポジトリのルートには、外部の規約で置き場所が決まっているものだけを置きます（`README.md`・`CHANGELOG.md`・`LICENSE.txt`はUPMの標準レイアウト、`AGENTS.md`はエージェントツールがルートから探索するため）。`AGENTS.md`は導線と常時ルールに限定し、API説明やコード例を複製しません。

## 2. `.meta` ファイルの扱い（最優先事項）

Unityは全ファイル・全フォルダに `.meta` を対で持ちます。GUIDによって参照とシリアライズ済みデータが繋がっているため、ここを崩すと利用者側プロジェクトの参照が切れます。

例外は、`Assets/`の外にあるファイル（このワークスペースの`Documentation/`など）と、Unity PackageでAsset Import対象外となる`Documentation~/`です。それ以外の`Assets/`配下へ追加したファイルには、すべて`.meta`が必要です。

- **`.meta` を手書きしない。** GUIDを自分で生成しないでください。新規ファイルを追加したら、Unity Editorに一度フォーカスを当てて生成させます。エージェントが新規ファイルを作った場合は、`.meta` が生成されたことを確認してからコミットしてください（→ §4）。
- **Unity Editorが無い実行環境では、新規ファイルの `.meta` を `scripts/generate_meta.py` で生成してかまいません。** `release_round.py preflight` が `.meta` の欠落で必ず止まるため、この場合だけ例外を認めます。

  ```bash
  python scripts/generate_meta.py --check   # 欠落を一覧するだけ
  python scripts/generate_meta.py           # 生成する
  ```

  **手で `.meta` を書かず、必ずこのスクリプトを使ってください。** 手書きでは次の3つが担保されません。

  - **認めるのは新規ファイルに限る。** 既存アセットのGUIDは参照とシリアライズ済みデータが繋がっているため、決して作り直さない。スクリプトは履歴に `.meta` があるパスを拒否し、`git checkout` での復元を促します
  - 生成後に全 `.meta` を走査し、**GUIDが重複していないことを確認する**
  - 拡張子ごとの importer ブロック（`.cs` は `MonoImporter`、`.asset` は `NativeFormatImporter`、フォルダは `folderAsset: yes`）を使い分ける

  そのうえで、次の2つは人が行います。

  - **スクリプトで生成した事実を、PR説明とコミットの報告へ明記する**
  - 後で Unity で開いたときに再生成や差分が出ないことを、依頼者の確認項目として提示する（→ §4「依頼者に確認してもらうこと」）
- **既存の `.meta` の中身（特に `guid`）を編集しない。**
- **移動・リネームは `.meta` と必ずセットで行う。** `git mv` を使い、`.cs` だけを動かして `.meta` を置き去りにしないこと。GUIDを維持すれば利用者側の参照は切れません（実例: CHANGELOG 2.2.1 の `Runtime/Obsolete/` への集約）。
- **削除も対で行う。** 片方だけ残った `.meta` はUnityが警告を出します。
- コミット前に `git status` で、追加・削除・移動したファイルと `.meta` の数が対応しているか確認してください。
- `Assets/SymphonyFrameWork/` 配下のアセットは `SymphonyAssetProtector` が移動を自動で差し戻します。意図して動かす場合は `Project Settings > SymphonyFrameWork` の `Asset Protection Mode` を `Warning` または `Disabled` にしてから `git mv` してください。

## 3. 文字コードと改行

- `.cs` は **UTF-8 BOM付き**（コミット `ccac325` で統一済みの既存の取り決め。日本語コメントを含むファイルの文字化けを避けるため）。
- `.md` / `.json` / `.asmdef` / `.uxml` / `.uss` は **UTF-8 BOMなし**。
- 改行コードは `core.autocrlf=true` 前提で、リポジトリにはLFで格納されます。改行コードだけの差分を作らないでください。
- 既存ファイルを編集するときは、そのファイルの現在の文字コードを維持します。

## 4. 検証の方法

このワークスペースには **uLoopMCP**（`io.github.hatayama.uloopmcp`）が導入されており、`.agents/skills/` の `uloop-*` スキルから **Unity Editorのコンパイル・Play Mode・ログ取得をエージェント自身が実行できます**。パッケージ単体を別プロジェクトへ導入した場合はこの手段がないため、そちらの前提で書かれた手順とは異なります。

### Unity Editor が無い実行環境

**リモートコンテナ（Claude Code on the web など）には Unity Editor が無く、下記の検証手順が成立しません。** `python scripts/verify_round.py` が `exit 3` を返したらその環境です。**Unity をコンテナへ導入することはできません**（配布ホストへの接続がプロキシに拒否されます）。

この場合も検証を飛ばさず、代替の機械検査（`release_round.py preflight`、`generate_meta.py --check`、`build_module_docs.py --check`、`audit_scan.py`、差分の通読）へ差し替えます。**実行できなかった項目は「未実施」として設計書の実施レポートへ残し、依頼者の一括検証へ引き渡します。** 手順は [.agents/skills/implement/references/remote.md](../.agents/skills/implement/references/remote.md) にあります。

### 標準の検証手順

1. `uloop-launch` — Unity Editorが起動していない場合
2. `uloop-clear-console` — 古いログが結果を隠さないよう、先にクリアする
3. `uloop-compile` — C#を変更したら必ず実行する
4. `uloop-get-logs` — エラーと警告を確認する
5. `uloop-control-play-mode` + `uloop-get-logs` — ランタイム挙動を確認する
6. `uloop-screenshot` — 表示に関わる変更の場合

**このプロジェクトは Enter Play Mode Options が有効で、Domain Reload と Scene Reload の両方が無効です。** そのためstatic状態はPlay Mode終了時にリセットされません。**Play Modeの開始・終了を2回繰り返し、`ServiceLocator` などにゴースト参照が残らないことを確認する**のが定番のチェックです。各Facadeの `ResetRuntimeState()` はこのための仕組みです。

### 自動生成物の再生成

**自動生成enum（`SceneListEnum` / `TagsEnum` / `LayersEnum` / `AudioGroupTypeEnum`）に関わる変更をしたら、再生成してもコンパイルが通ることを確認してください。**

1. `Symphony Administrator > AutoEnumGenerator` から3種のenumを再生成する
2. `uloop-compile` — エラー0・警告0
3. `uloop-run-tests` — EditMode / PlayMode の両方

生成物の中身は**利用側プロジェクトの内容によって変わります**。パッケージ本体が特定のenum値（`SceneListEnum.NewScene` など）を前提にしていると、利用側で必ず壊れます。enum型を型引数やパラメータとして扱うのは問題ありませんが、**値を直接参照しないでください**。

再生成せずに済ませると、この違反はワークスペースでは検出できません。ワークスペースの生成物は Build Settings に登録済みの3シーンから作られており、たまたま参照が解決してしまうためです。

### 自動テストについて

テストは**パッケージ内の `Assets/SymphonyFrameWork/Tests/`** にあります。EditMode は `Tests/Editor/`、PlayMode は `Tests/Runtime/`。

- 両方の asmdef に `"defineConstraints": ["UNITY_INCLUDE_TESTS"]` を付けており、利用者のビルドには含まれません
- `Runtime/AssemblyInfo.cs` と `Core/AssemblyInfo.cs` がテストアセンブリへ `InternalsVisibleTo` を与えているため、**`internal` な内部実装も単体テストできます**
- 実行は `uloop-run-tests --test-mode EditMode` と `--test-mode PlayMode`

コードを変更したら、既存テストが全数成功することを確認してください。

**パッケージ本体（`Runtime/` `Core/` `Editor/`）のソースを変更する場合、テストの追加または変更は必須です。** `release_round.py preflight` がソースの変更に対する `Tests/` の変更を検査し、無ければコミットを止めます。検証手段が無い場合だけ `--no-tests-reason "理由"` で通せます。理由は `No-Tests-Reason:` トレーラとしてコミットへ残ります。使ってよいのは、Editor の GUI 操作を伴う変更、Unity のホストライフサイクルに依存する変更、ソースを変更していない変更に限ります。**「テストが書きにくい」は理由になりません。**

**公開型の網羅は `Tests/Editor/PublicTypeTestCoverageTests.cs` が固定しています。** 公開型 `X` に対して `Tests/Editor/XTests.cs` が無い場合、そのテストの `UntestedPublicTypes` へ載っている型だけが許されます。**この一覧は減らすためにあり、足してはいけません。**新しい公開型をテスト無しで追加すると落ちます。テストを書いたら一覧から行を消してください（消し忘れも検出します）。

テストで再現できない範囲（モーダルダイアログ、Unityのホストライフサイクルなど）は次の「サンプルによる確認」と手動確認で担保します。

### サンプルによる確認

各機能には `Assets/SymphonyFrameWork/Samples~/Runtime/*Sample/` に動作確認用のシーンとスクリプトがあります。サンプルは公開APIの利用例であり、`internal` APIへ依存させないでください。

**`Samples~` は末尾のチルダによりUnityのインポート対象外です。**利用側では出荷物に含まれず、Package Managerからインポートしたときだけ `Assets/Samples/` へコピーされます。**その代わり、このワークスペースではサンプルシーンをProjectビューから開けません。**サンプルをPlayして確認する場合は、対象の `Samples~/Runtime/<名前>/` を `Assets/` 配下の作業用フォルダへコピーしてから開き、確認後に削除してください。コピーしたものをコミットしないでください。

### 依頼者に確認してもらうこと

uLoopで確認できない範囲は依頼者へ依頼します。変更を報告するときは、**「Unityで何を確認してほしいか」を具体的に添えてください**。[CodeGuidelines.md `## 変更時の確認`](./CodeGuidelines.md) から、その変更に該当する項目だけを抜き出して提示するのが望ましい形です。

- 実機・スタンドアロンビルドでの動作
- 表示や操作感など、ログでは判断できない品質
- 新規ファイルの `.meta` が生成されたか（Unity Editorにフォーカスを当てる必要があります）。**`generate_meta.py` で生成した場合は、Unityが再生成や差分を出さないこと**
- Unity Editor が無い環境で進めた変更の、コンパイル・EditMode/PlayModeテスト・Editor画面の目視（→ 上記「Unity Editor が無い実行環境」）

## 5. ブランチとコミット

### `scripts/release_round.py` で手順を固定する

この節の手順は `scripts/release_round.py` にコード化してあります。**手で git を叩く代わりにこれを使ってください。**

```bash
python scripts/release_round.py checkpoint --message "[checkpoint]途中成果を退避" --issue 119
python scripts/release_round.py preflight
python scripts/release_round.py commit --message "[add]日本語の要約" --issue 119 --pr --pr-body-file body.md
python scripts/release_round.py finalize --paths Documentation/Designs/Foo.md
```

| フェーズ | 内容 |
| --- | --- |
| `checkpoint` | 作業中の途中成果を、**コンパイル・テスト・preflight なしで** submodule の作業ブランチへコミットして push する。版更新、PR作成、親の gitlink 更新は行わない |
| `preflight` | ブランチ名、**ソース変更に対するテストの有無**、`version` と CHANGELOG の一致、`.cs` の UTF-8 BOM、`.meta` の対、Runtime/Core からの `UnityEditor` 参照、テスト asmdef の `UNITY_INCLUDE_TESTS` を検証する。**git の状態は変更しない** |
| `commit` | preflight を通してから submodule へコミットし push する。`--pr` で Pull Request も作成する |
| `finalize` | PRを `develop` へマージし、**gitlink が `origin/develop` から到達可能かを確認してから**、作業ブランチを削除し、親リポジトリをコミットして push する |

スクリプトが機械的に防いでいるのは次の3点です。手で叩くと落としやすく、落ちたときの影響が大きい順に並んでいます。

1. **submodule を push する前に親の gitlink を更新しない**
2. **gitlink が `origin/develop` から到達可能である**（feature ブランチのコミットを指すと、squash マージやブランチ削除で到達不能になり、新規クローンの `git submodule update` が失敗する）
3. **親リポジトリへ `git add -A` しない**（無関係な未コミット変更を巻き込む）

**PR の `develop` へのマージと作業ブランチの削除は `finalize` が自動で行います。** `gh pr merge` を手で実行しないでください。人の承認を必要とする `develop` から `main` へのリリースだけは、このスクリプトの対象外です。

作業中は、変更の意図がひとまとまりになるたびに `checkpoint` を実行してかまいません。チェックポイントはバックアップ用の未検証コミットで、`Checkpoint: true` と `Verification: not-run` が履歴へ残ります。**Round の最後には全差分をまとめてコンパイル・テスト・preflight し、通常の `commit` と `finalize` を省略しないでください。**

以下は、スクリプトが何をしているかの説明です。

パッケージを変更した場合、**submodule と親リポジトリの2段階でコミット**します。

1. submodule（`Assets/SymphonyFrameWork/`）で `develop` から `feature/<機能名>` を切り、変更をコミットする。`main` へ直接コミットしません。
2. submodule の変更を push する。**push しないまま親の gitlink だけを更新すると、他の開発者が解決できない参照になります。**
3. `finalize` でPRを `develop` へマージし、submoduleを `develop` へ揃えて作業ブランチを削除する。
4. 親リポジトリで、更新された gitlink をコミットしてpushする。

`feature/*` → `develop` → `main` の順にPull Requestでマージします。

### Issue に対応するブランチと Pull Request

特定の GitHub Issue に対応する場合は、**設計や実装へ着手する前に**、submodule の `develop` からその Issue 専用のブランチを作ります。複数の無関係な Issue を同じブランチで扱わないでください。

- 命名規則: `feature/<Issue番号>-<短い機能名>`（例: `feature/101-module-docs`）。不具合修正のIssueには `fix/<Issue番号>-<短い名前>` を使えます（例: `fix/160-check-component-null`）
- **接頭辞は `feature/` か `fix/` のどちらかにしてください。** `release_round.py finalize` はこの2つで始まるブランチだけをマージ後に削除します。規則外の名前を付けると、コミットもマージも通ったうえでブランチだけが残ります。`preflight` が着手時点で弾きます
- 修正と検証が完了したらブランチを push し、`develop` をベースとする Pull Request を作成する
- PR 本文へ `Issue: #<Issue番号>` を記載し、PR がマージされたら対応する Issue を閉じる

GitHub の自動クローズ用キーワードは、PR のベースがリポジトリの既定ブランチである場合だけ有効です。現在の既定ブランチは `main` なので、`develop` 向けPRの `Closes #<Issue番号>` では自動クローズされません。既定ブランチを `develop` へ変更した場合は、`Issue: #<Issue番号>` の代わりに `Closes #<Issue番号>` を使用します。

Issue が無い機能追加・変更では、`feature/<短い機能名>` を使用します。

コミットメッセージは **`[prefix]日本語の要約`** の1行形式です（prefixと本文の間にスペースを入れません）。

| prefix | 用途 | 実例 |
| --- | --- | --- |
| `[add]` | 機能・ファイル・サンプルの追加 | `[add]LazyObjectを追加` |
| `[update]` | 既存の挙動・構造・ドキュメントの変更 | `[update]Awaitableを使用してGC軽減` |
| `[fix]` | バグ修正 | `[fix]SubclassSelectorDrawerで配列要素のパス解決を修正` |

- コミットメッセージ、PR説明、コードコメント、XMLドキュメント、ドキュメント類はすべて**日本語**で書きます（コードレビューbotのCodeRabbitも `language: ja` 設定です）。
- 1コミットは1つの意図にまとめます。`.meta` の追加は対応する実ファイルと同じコミットに含めます。

## 6. 変更に応じて同時に更新するもの

**コードとドキュメントの乖離はバグとして扱います。** 変更の種類ごとに、同一の変更（同じPR）内で更新すべきものは次のとおりです。パスはすべてワークスペースルート起点です。

| 変更の種類 | 同時に更新するもの |
| --- | --- |
| 公開API（`public`/`protected`）の追加・変更・削除 | XMLドキュメント、**`Documentation~/Modules/<モジュール>.md`**、`Assets/SymphonyFrameWork/CHANGELOG.md`、`Assets/SymphonyFrameWork/package.json` の `version`、該当する Sample。索引が変わる場合は `README.md`、アセンブリ・初期化・公開型の関係が変わる場合は`Documentation~/Architecture.md` |
| 公開挙動の変更（シグネチャは同じだが結果が変わる） | CHANGELOG.md、`version`、必要なら `Documentation~/Modules/<モジュール>.md` のクイックスタートと「実装時の注意」 |
| 設定アセット（Config）の項目の追加・変更 | `Documentation~/Modules/<モジュール>.md` の「Editor機能」、README の初期設定、CHANGELOG.md |
| Editor機能（ウィンドウ、メニュー、Project Settings、生成物）の追加・変更 | 対応する `Documentation~/Modules/<モジュール>.md`。横断的な機能（Symphony Administrator、アセット保護、Editorの初期化）は `Documentation~/EditorTools.md`。CHANGELOG.md。索引が変わる場合は `Documentation~/EditorTools.md` の `## 一覧` と README |
| 利用者向けドキュメント（`README.md`、`Documentation~/**/*.md`）の変更 | 書き方は [DocumentationGuidelines.md](./DocumentationGuidelines.md) に従う。**`python scripts/build_module_docs.py` を実行して `Documentation~/Html/` を再生成し、生成物も同じコミットへ含める。** 忘れると `release_round.py preflight` が落ちる |
| Sample の追加 | `package.json` の `samples`、CHANGELOG.md |
| 依存パッケージの追加・更新 | `package.json` の `dependencies`、README の必要なパッケージ |
| 非推奨化 | `[Obsolete("代替APIの案内", error: false)]`、**`Documentation~/Deprecations.md` への行追加（削除予定が未定なら「未定」と書く）**、CHANGELOG の `### Deprecated`（移行方法を明記）、READMEまたはSampleの旧API利用箇所 |
| 非推奨APIの削除 | `Documentation~/Deprecations.md` の行を `## 削除済み` へ移す、CHANGELOG の `### Breaking`（移行方法を明記）、`package.json` の `version`（メジャー更新） |
| 本体開発の手順・規約の変更 | ワークスペース側の `Documentation/`（このファイル、CodeGuidelines、DocumentationGuidelines、DesignPhilosophy）と `AGENTS.md` |
| 内部実装（`internal`/`private`）のみの変更 | 原則不要。ただし**更新不要と判断した理由**をPR説明かコミットメッセージに書く |

### バージョンとCHANGELOG

- **版は4か所を同時に更新します。** `package.json` の `version`、`Assets/SymphonyFrameWork/Core/SymphonyConstant.cs` の `VERSION`、CHANGELOG の見出し、**README の「現在のバージョン」**。SemVerの判断基準は [DesignPhilosophy.md `### バージョニング`](./DesignPhilosophy.md) を参照してください（破壊的変更=メジャー / 後方互換な追加=マイナー / 実装のみの修正=パッチ）。
- **`SymphonyConstant.VERSION` は設計書の「この Round で触るバージョン関連ファイル」から落ちやすい箇所です。** `release_round.py preflight` の `[version]` が `package.json` との一致を検査するため事故にはなりませんが、設計を書く時点で4か所すべてを列挙してください。READMEは4か所目で、こちらは検査が無いぶん漏れが残りやすい箇所です。
- CHANGELOG の形式:

```markdown
## [2.2.1] - 2026-07-29
### Add
- 追加したものと、それが何を解決するか。

### Fix
- 直した現象と、その原因。

### Change
- 変えた内容と、利用側への影響（影響がないならその理由）。
```

- 節の見出しは `Add` / `Change` / `Fix` / `Deprecated` / `Breaking` を使います。`Breaking` と `Deprecated` には**移行方法を必ず書きます**。
- 「何をしたか」だけでなく「なぜそうしたか」「利用側にどう影響するか」まで書くのがこのリポジトリの粒度です。
- `Tools/VersionLogGenerator`（ワークスペース側のEditorツール）は、CHANGELOGへのエントリ追記と `package.json` の version 更新を補助します。

### 利用者向けドキュメントとHTML生成物

**正本はMarkdownです。** `Assets/SymphonyFrameWork/Documentation~/Html/` 配下のHTMLは `scripts/build_module_docs.py` の生成物で、Editorから「ドキュメントを開く」操作をしたときにブラウザが読む先です。手で編集しないでください。

```bash
python scripts/build_module_docs.py
```

- `--check` を付けると生成せず、正本と生成物が一致しているかだけを検証します。`release_round.py preflight` から呼ばれます。
- **スクリプトが対応するMarkdownの記法は、このリポジトリの既存ドキュメントが実際に使っているものに限ります**（見出し、段落、リスト、テーブル、フェンス付きコードブロック、インラインコード、`**強調**`、リンク、水平線）。対応外の記法を書くと生成時にエラーで止まります。**黙って素通しさせないための仕様です。**
- **相対リンクとアンカーの実在も生成時に検査します。** リンク切れがあると生成が止まります。
- **mermaid のブロックは、生成時にSVGへ変換して `Documentation~/Html/Diagrams/` へ書き出し、HTMLからは `<img>` で参照します。** 実行時のスクリプトを読み込まないため、オフラインでもそのまま図として表示されます。
- **図を追加・変更したときだけ Node.js が必要です。** 変換には `npx @mermaid-js/mermaid-cli` を使います（初回はChromiumの取得が走ります）。SVGはmermaidソースのダイジェストをファイル名にしてコミットするため、**図を触らない変更では Node.js は要りません。** 既存のSVGがそのまま使われます。
- SVGが足りない場合と、参照されなくなったSVGが残っている場合は、生成と `--check` の両方で検出されます。

## 7. Pull Request前のチェック

コード品質のチェックは [CodeGuidelines.md `## レビュー用チェックリスト`](./CodeGuidelines.md) を使ってください。それに加えて、本体開発では次を確認します。

- [ ] 本体のソースを変更したなら、テストを追加または変更した（できない場合は `--no-tests-reason` の理由を用意した）
- [ ] 追加・削除・移動したファイルに対して `.meta` が対で揃っている
- [ ] 移動・リネームでGUIDを維持している
- [ ] `.cs` がUTF-8 BOM付きで保存されている
- [ ] 公開APIを変更したなら、§6の表にある全ファイルを更新した（不要と判断したものは理由を書いた）
- [ ] `package.json` の `version` と CHANGELOG の見出しが一致している
- [ ] `uloop-compile` がエラーなく通り、Consoleに意図しない警告がない
- [ ] `python scripts/build_module_docs.py --check` が通っている（ドキュメントを変更した場合）
- [ ] 変更した文書が [DocumentationGuidelines.md `## 7. 更新時の確認`](./DocumentationGuidelines.md#7-更新時の確認) を満たしている
- [ ] `Cache/` や親ワークスペースの生成物をコミットしていない
- [ ] submodule の変更を push してから、親の gitlink を更新した
- [ ] uLoopで確認できない事項を依頼者へ提示した

## 8. やってはいけないこと

- `.meta` を手書きする、GUIDを書き換える、`.meta` と実ファイルを別々に動かす。
- `Assets/SymphonyFrameWork.meta`（親リポジトリの管理物）を削除する。
- submodule の変更を push せずに、親リポジトリの gitlink だけを更新する。
- パッケージのファイルを親リポジトリ側で直接追跡・コミットしようとする。
- `Cache/`、`Library/`、`.csproj`、`.sln`、親ワークスペースの生成物をコミットする。
- `main` や `develop` へ直接コミットする。
- 公開APIを変更したのにREADME / CHANGELOG / Sampleなど該当する利用者向け文書を更新しない。
- 本体開発向けの手順や規約を、パッケージ側（`README.md`・`AGENTS.md`）へ書き戻す。パッケージ側は利用者向けの内容だけに保つ。
- Sampleから `internal` APIを使う。Sampleは公開APIだけで書けることの証明です。
- Runtimeコード（`Runtime/`、`Core/`）から `UnityEditor` を参照する。必要なら `Editor/` へ処理を分離し、やむを得ない場合のみ `#if UNITY_EDITOR` で囲む。
- 「将来使うかもしれない」を理由にinterface・抽象・拡張点を追加する（DesignPhilosophy `## 避ける設計`）。
- 明示された作業範囲を超えて、ロジック・公開API・シリアライズ形式を変更する。
