# CONTRIBUTING.md — Symphony Framework 本体開発ガイド

このファイルは、**`Assets/SymphonyFrameWork/` にある Symphony Framework パッケージ本体のソースを変更する**人およびAIエージェント向けの作業手順です。

パッケージを**利用する側**のコード（このワークスペースの `Assets/Scripts/` など）を書く場合は、このファイルではなく [Assets/SymphonyFrameWork/AGENTS.md](../Assets/SymphonyFrameWork/AGENTS.md) を読んでください。ワークスペース全体の構成は [AGENTS.md](../AGENTS.md) にあります。

## 0. 最初に読むもの

| ドキュメント | 読むタイミング |
| --- | --- |
| このファイル | 常に。作業の進め方・コミット・検証の手順 |
| [CodeGuidelines.md](./CodeGuidelines.md) | コードを書く前に。命名・書式・非同期・Unity固有ルール |
| [DesignPhilosophy.md](./DesignPhilosophy.md) | 型・クラス・名前空間を**新設**する前、または公開範囲を判断する前。全部を通読せず該当する節（`## クラス設計`、`## 公開APIとバージョニング` など）を参照する |
| [../AGENTS.md](../AGENTS.md) | ワークスペースの構成、環境設定、自動生成物の区別を知りたいとき |
| [../Assets/SymphonyFrameWork/AGENTS.md](../Assets/SymphonyFrameWork/AGENTS.md) | 公開APIを変更したとき。更新対象として |
| [../Assets/SymphonyFrameWork/CHANGELOG.md](../Assets/SymphonyFrameWork/CHANGELOG.md) | 直近の変更の経緯を知りたいとき。記述の粒度の参考にもする |

既存コードの修正だけであれば、このファイルと CodeGuidelines.md で足ります。

## 1. リポジトリの構造上の前提

このワークスペースには **gitリポジトリが2つ** あります。どちらを操作しているかを常に意識してください。

| | ワークスペース（親） | パッケージ（submodule） |
| --- | --- | --- |
| リポジトリ | `HIBIKI5201/SymphonyWorkspace` | `HIBIKI5201/SymphonyFramework` |
| ルート | このリポジトリのルート | `Assets/SymphonyFrameWork/` |
| 役割 | 開発環境。Unityプロジェクト、検証用シーン、本ドキュメント | 配布物。UPMパッケージ本体 |
| 既定ブランチ | `dev` | `develop` |

- **パッケージのソース変更は、必ず submodule 側（`Assets/SymphonyFrameWork/`）でコミットします。** 親リポジトリはパッケージのファイルを追跡せず、submodule のコミットハッシュ（gitlink）だけを記録します。
- コマンドラインからは `git -C "Assets/SymphonyFrameWork" ...` のように対象を明示すると取り違えを防げます。
- クローンは `git clone --recurse-submodules` で行います。忘れた場合は `git submodule update --init --recursive` を実行してください。
- `Assets/SymphonyFrameWork.meta` は submodule の**外側**にあり、親リポジトリが管理します。削除すると利用側プロジェクトのGUID参照が壊れるため、絶対に削除しないでください。
- **このリポジトリ単体ではコンパイルできません。** `dotnet build`、`msbuild`、`csc` は使わないでください。asmdefの参照解決はUnityが行うため、コンパイル可否の判断はUnity Editorに委ねます（→ §4）。
- `Assets/SymphonyFrameWork/Cache/` はランタイム生成物（`SymphonyDebugLogger` のログ）です。`.gitignore` 済みで、編集も削除も自由ですが、コミットしません。
- `.claude/` と `.agents/` はローカル専用領域です。**他の開発者やエージェントにも守らせたいルールは、必ずこのファイルなど追跡対象のドキュメントに書いてください。**
- **ドキュメントの置き場所には基準があります。** 本体開発向けのドキュメント（このファイル、CodeGuidelines、DesignPhilosophy）は**ワークスペース側の `Documentation/`** に置きます。パッケージリポジトリのルートには、外部の規約で置き場所が決まっているものだけを置きます（`README.md`・`CHANGELOG.md`・`LICENSE.txt` はUPMの標準レイアウト、`AGENTS.md` はエージェントツールがルートしか探さないため）。これらはすべて**パッケージ利用者向け**の内容に限定します。

## 2. `.meta` ファイルの扱い（最優先事項）

Unityは全ファイル・全フォルダに `.meta` を対で持ちます。GUIDによって参照とシリアライズ済みデータが繋がっているため、ここを崩すと利用者側プロジェクトの参照が切れます。

例外は `Assets/` の外にあるファイル（このワークスペースの `Documentation/` など）だけです。`Assets/` 配下に追加したファイルには、すべて `.meta` が必要です。

- **`.meta` を手書きしない。** GUIDを自分で生成しないでください。新規ファイルを追加したら、Unity Editorに一度フォーカスを当てて生成させます。エージェントが新規ファイルを作った場合は、`.meta` が生成されたことを確認してからコミットしてください（→ §4）。
- **既存の `.meta` の中身（特に `guid`）を編集しない。**
- **移動・リネームは `.meta` と必ずセットで行う。** `git mv` を使い、`.cs` だけを動かして `.meta` を置き去りにしないこと。GUIDを維持すれば利用者側の参照は切れません（実例: CHANGELOG 2.2.1 の `Runtime/Obsolete/` への集約）。
- **削除も対で行う。** 片方だけ残った `.meta` はUnityが警告を出します。
- コミット前に `git status` で、追加・削除・移動したファイルと `.meta` の数が対応しているか確認してください。
- `Assets/SymphonyFrameWork/` 配下のアセットは `SymphonyAssetProtector` が移動を自動で差し戻します。意図して動かす場合は `Tools/SymphonyFrameWork/Settings/Symphony Asset Lock` を解除してから `git mv` してください。

## 3. 文字コードと改行

- `.cs` は **UTF-8 BOM付き**（コミット `ccac325` で統一済みの既存の取り決め。日本語コメントを含むファイルの文字化けを避けるため）。
- `.md` / `.json` / `.asmdef` / `.uxml` / `.uss` は **UTF-8 BOMなし**。
- 改行コードは `core.autocrlf=true` 前提で、リポジトリにはLFで格納されます。改行コードだけの差分を作らないでください。
- 既存ファイルを編集するときは、そのファイルの現在の文字コードを維持します。

## 4. 検証の方法

このワークスペースには **uLoopMCP**（`io.github.hatayama.uloopmcp`）が導入されており、`.claude/skills/` の `uloop-*` スキルから **Unity Editorのコンパイル・Play Mode・ログ取得をエージェント自身が実行できます**。パッケージ単体を別プロジェクトへ導入した場合はこの手段がないため、そちらの前提で書かれた手順とは異なります。

### 標準の検証手順

1. `uloop-launch` — Unity Editorが起動していない場合
2. `uloop-clear-console` — 古いログが結果を隠さないよう、先にクリアする
3. `uloop-compile` — C#を変更したら必ず実行する
4. `uloop-get-logs` — エラーと警告を確認する
5. `uloop-control-play-mode` + `uloop-get-logs` — ランタイム挙動を確認する
6. `uloop-screenshot` — 表示に関わる変更の場合

**このプロジェクトは Enter Play Mode Options が有効で、Domain Reload と Scene Reload の両方が無効です。** そのためstatic状態はPlay Mode終了時にリセットされません。**Play Modeの開始・終了を2回繰り返し、`ServiceLocator` などにゴースト参照が残らないことを確認する**のが定番のチェックです。各Facadeの `ResetRuntimeState()` はこのための仕組みです。

### 自動テストについて

**現時点でこのリポジトリに自動テストは存在しません。** テスト用asmdefもテストコードもないため、`uloop-run-tests` は実行できても0件を返します。回帰確認は次の「サンプルによる確認」で行ってください。

### サンプルによる確認

各機能には `Assets/SymphonyFrameWork/Samples/Runtime/*Sample/` に動作確認用のシーンとスクリプトがあります。挙動を変えた機能に対応するサンプルがある場合は、そのサンプルをPlayして確認するのが最短です。サンプルは公開APIの利用例であり、`internal` APIへ依存させないでください。

### 依頼者に確認してもらうこと

uLoopで確認できない範囲は依頼者へ依頼します。変更を報告するときは、**「Unityで何を確認してほしいか」を具体的に添えてください**。[CodeGuidelines.md `## 変更時の確認`](./CodeGuidelines.md) から、その変更に該当する項目だけを抜き出して提示するのが望ましい形です。

- 実機・スタンドアロンビルドでの動作
- 表示や操作感など、ログでは判断できない品質
- 新規ファイルの `.meta` が生成されたか（Unity Editorにフォーカスを当てる必要があります）

## 5. ブランチとコミット

パッケージを変更した場合、**submodule と親リポジトリの2段階でコミット**します。

1. submodule（`Assets/SymphonyFrameWork/`）で `develop` から `feature/<機能名>` を切り、変更をコミットする。`main` へ直接コミットしません。
2. submodule の変更を push する。**push しないまま親の gitlink だけを更新すると、他の開発者が解決できない参照になります。**
3. 親リポジトリで、更新された gitlink をコミットする。

`feature/*` → `develop` → `main` の順にPull Requestでマージします。

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
| 公開API（`public`/`protected`）の追加・変更・削除 | XMLドキュメント、`Assets/SymphonyFrameWork/README.md`、`Assets/SymphonyFrameWork/AGENTS.md`、`Assets/SymphonyFrameWork/CHANGELOG.md`、`Assets/SymphonyFrameWork/package.json` の `version`、該当する Sample |
| 公開挙動の変更（シグネチャは同じだが結果が変わる） | CHANGELOG.md、`version`、必要なら README のクイックスタート |
| 設定アセット（Config）の項目の追加・変更 | README の初期設定、AGENTS.md、CHANGELOG.md |
| Sample の追加 | `package.json` の `samples`、CHANGELOG.md |
| 依存パッケージの追加・更新 | `package.json` の `dependencies`、README の必要なパッケージ |
| 非推奨化 | `[Obsolete("代替APIの案内", error: false)]`、CHANGELOG の `### Deprecated`（移行方法を明記）、AGENTS.md |
| 本体開発の手順・規約の変更 | ワークスペース側の `Documentation/`（このファイル、CodeGuidelines、DesignPhilosophy）と `AGENTS.md` |
| 内部実装（`internal`/`private`）のみの変更 | 原則不要。ただし**更新不要と判断した理由**をPR説明かコミットメッセージに書く |

### バージョンとCHANGELOG

- `package.json` の `version` と CHANGELOG の見出しは同時に更新します。SemVerの判断基準は [DesignPhilosophy.md `### バージョニング`](./DesignPhilosophy.md) を参照してください（破壊的変更=メジャー / 後方互換な追加=マイナー / 実装のみの修正=パッチ）。
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

## 7. Pull Request前のチェック

コード品質のチェックは [CodeGuidelines.md `## レビュー用チェックリスト`](./CodeGuidelines.md) を使ってください。それに加えて、本体開発では次を確認します。

- [ ] 追加・削除・移動したファイルに対して `.meta` が対で揃っている
- [ ] 移動・リネームでGUIDを維持している
- [ ] `.cs` がUTF-8 BOM付きで保存されている
- [ ] 公開APIを変更したなら、§6の表にある全ファイルを更新した（不要と判断したものは理由を書いた）
- [ ] `package.json` の `version` と CHANGELOG の見出しが一致している
- [ ] `uloop-compile` がエラーなく通り、Consoleに意図しない警告がない
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
- 公開APIを変更したのにREADME / AGENTS.md / CHANGELOGを更新しない。
- 本体開発向けの手順や規約を、パッケージ側（`README.md`・`AGENTS.md`）へ書き戻す。パッケージ側は利用者向けの内容だけに保つ。
- Sampleから `internal` APIを使う。Sampleは公開APIだけで書けることの証明です。
- Runtimeコード（`Runtime/`、`Core/`）から `UnityEditor` を参照する。必要なら `Editor/` へ処理を分離し、やむを得ない場合のみ `#if UNITY_EDITOR` で囲む。
- 「将来使うかもしれない」を理由にinterface・抽象・拡張点を追加する（DesignPhilosophy `## 避ける設計`）。
- 明示された作業範囲を超えて、ロジック・公開API・シリアライズ形式を変更する。
