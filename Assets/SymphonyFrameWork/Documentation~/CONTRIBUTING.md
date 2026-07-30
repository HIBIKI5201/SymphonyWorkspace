# CONTRIBUTING.md — Symphony Framework 本体開発ガイド

このファイルは、**このリポジトリ（`SymphonyFrameWork` パッケージ本体）のソースを変更する**人およびAIエージェント向けの作業手順です。

パッケージを**利用する側**のプロジェクトでコードを書く場合は、このファイルではなく [AGENTS.md](../AGENTS.md) を読んでください。AGENTS.md には「パッケージ本体を編集しない」旨の記述がありますが、それは利用者側エージェントへの指示であり、本体開発者には適用されません。

## 0. 最初に読むもの

| ドキュメント | 読むタイミング |
| --- | --- |
| このファイル | 常に。作業の進め方・コミット・検証の手順 |
| [CodeGuidelines.md](./CodeGuidelines.md) | コードを書く前に。命名・書式・非同期・Unity固有ルール |
| [DesignPhilosophy.md](./DesignPhilosophy.md) | 型・クラス・名前空間を**新設**する前、または公開範囲を判断する前。全部を通読せず該当する節（`## クラス設計`、`## 公開APIとバージョニング` など）を参照する |
| [AGENTS.md](../AGENTS.md) | 公開APIを変更したとき。更新対象として |
| [CHANGELOG.md](../CHANGELOG.md) | 直近の変更の経緯を知りたいとき。記述の粒度の参考にもする |

既存コードの修正だけであれば、このファイルと CodeGuidelines.md で足ります。

## 1. リポジトリの構造上の前提

- **gitリポジトリのルート = パッケージのルート**（`package.json` があるフォルダ）です。Unityプロジェクトのルートではありません。
- 実体は Unityプロジェクト `SymphonyTemplate/` の `Assets/SymphonyFrameWork/` に置かれています。`.sln` / `.csproj` / `Library/` / `ProjectSettings/` はすべてリポジトリの**外側**（親のUnityプロジェクト側）にあります。
- したがって**このリポジトリ単体ではコンパイルできません**。`dotnet build`、`msbuild`、`csc` は使わないでください。asmdefの参照解決もUnityが行うため、コンパイル可否の判断はUnity Editorに委ねます（→ §4）。
- `Cache/` はランタイム生成物（`SymphonyDebugLogger` のログ）です。`.gitignore` 済みで、編集も削除も自由ですが、コミットしません。
- `.claude/` と `.agents/`、および `CLAUDE.md` は `.gitignore` 済みのローカル専用領域です。**他の開発者やエージェントにも守らせたいルールは、必ずこのファイルなど追跡対象のドキュメントに書いてください。**
- **ドキュメントの置き場所には基準があります。** リポジトリのルートには、外部の規約で置き場所が決まっているものだけを置きます（`README.md`・`CHANGELOG.md`・`LICENSE.txt` はUPMの標準レイアウト、`AGENTS.md` はエージェントツールがルートしか探さないため）。それ以外の開発向けドキュメントは `Documentation~/` へ置きます。新しいドキュメントを追加するときもこの基準に従ってください。
- `Documentation~` は末尾の `~` によってUnityのアセットデータベース対象外になります。配下のファイルは利用者側プロジェクトへインポートされず、`.meta` も不要です（→ §2）。

## 2. `.meta` ファイルの扱い（最優先事項）

Unityは全ファイル・全フォルダに `.meta` を対で持ちます。GUIDによって参照とシリアライズ済みデータが繋がっているため、ここを崩すと利用者側プロジェクトの参照が切れます。

例外は `Documentation~/` 配下だけです（末尾の `~` でアセットデータベース対象外のため、`.meta` は生成されず、置いても無意味です）。それ以外の場所に追加したファイルには、すべて `.meta` が必要です。

- **`.meta` を手書きしない。** GUIDを自分で生成しないでください。新規ファイルを追加したら、Unity Editorに一度フォーカスを当てて生成させます。エージェントが新規ファイルを作った場合は、コミット前に「Unityをアクティブにして `.meta` を生成する」よう依頼者へ伝えてください。
- **既存の `.meta` の中身（特に `guid`）を編集しない。**
- **移動・リネームは `.meta` と必ずセットで行う。** `git mv` を使い、`.cs` だけを動かして `.meta` を置き去りにしないこと。GUIDを維持すれば利用者側の参照は切れません（実例: CHANGELOG 2.2.1 の `Runtime/Obsolete/` への集約）。
- **削除も対で行う。** 片方だけ残った `.meta` はUnityが警告を出します。
- コミット前に `git status` で、追加・削除・移動したファイルと `.meta` の数が対応しているか確認してください。

## 3. 文字コードと改行

- `.cs` は **UTF-8 BOM付き**（コミット `ccac325` で統一済みの既存の取り決め。日本語コメントを含むファイルの文字化けを避けるため）。
- `.md` / `.json` / `.asmdef` / `.uxml` / `.uss` は **UTF-8 BOMなし**。
- 改行コードは `core.autocrlf=true` 前提で、リポジトリにはLFで格納されます。改行コードだけの差分を作らないでください。
- 既存ファイルを編集するときは、そのファイルの現在の文字コードを維持します。

## 4. 検証の方法

エージェントはUnity Editorを起動できないため、**コンパイル確認とPlay Mode確認は行えません**。できることとできないことを分けて扱ってください。

### エージェントが自分で確認すること

- asmdefの参照方向（`Core` → `Runtime` → `Editor`、循環なし、Runtimeから `UnityEditor` を参照していない）
- 名前空間とフォルダ配置の一致（CodeGuidelines `## 名前空間`）
- 追加・削除・移動したファイルと `.meta` の対応
- 変更した公開APIが README / AGENTS.md の記述と矛盾していないか
- ドキュメント内の相対リンクが切れていないか

### 依頼者に確認してもらうこと

変更を報告するときは、**「Unityで何を確認してほしいか」を具体的に添えてください**。[CodeGuidelines.md `## 変更時の確認`](./CodeGuidelines.md) から、その変更に該当する項目だけを抜き出して提示するのが望ましい形です。

例:
> Unity Editorで以下を確認してください。
> 1. コンパイルが通ること（Console にエラーがないこと）
> 2. Play Mode の開始・終了を2回繰り返し、`ServiceLocator` にゴースト参照が残らないこと
> 3. Domain Reload を無効にした状態でも初期化されること

### サンプルによる確認

各機能には `Samples/Runtime/*Sample/` に動作確認用のシーンとスクリプトがあります。挙動を変えた機能に対応するサンプルがある場合は、そのサンプルで確認してもらうのが最短です。サンプルは公開APIの利用例であり、`internal` APIへ依存させないでください。

## 5. ブランチとコミット

- 作業ブランチは `develop` から `feature/<機能名>` を切ります。`main` へ直接コミットしません。
- `feature/*` → `develop` → `main` の順にPull Requestでマージします。
- コミットメッセージは **`[prefix]日本語の要約`** の1行形式です（prefixと本文の間にスペースを入れません）。

| prefix | 用途 | 実例 |
| --- | --- | --- |
| `[add]` | 機能・ファイル・サンプルの追加 | `[add]LazyObjectを追加` |
| `[update]` | 既存の挙動・構造・ドキュメントの変更 | `[update]Awaitableを使用してGC軽減` |
| `[fix]` | バグ修正 | `[fix]SubclassSelectorDrawerで配列要素のパス解決を修正` |

- コミットメッセージ、PR説明、コードコメント、XMLドキュメント、ドキュメント類はすべて**日本語**で書きます（コードレビューbotのCodeRabbitも `language: ja` 設定です）。
- 1コミットは1つの意図にまとめます。`.meta` の追加は対応する実ファイルと同じコミットに含めます。

## 6. 変更に応じて同時に更新するもの

**コードとドキュメントの乖離はバグとして扱います。** 変更の種類ごとに、同一の変更（同じPR）内で更新すべきものは次のとおりです。

| 変更の種類 | 同時に更新するもの |
| --- | --- |
| 公開API（`public`/`protected`）の追加・変更・削除 | XMLドキュメント、[README.md](../README.md)、[AGENTS.md](../AGENTS.md)、[CHANGELOG.md](../CHANGELOG.md)、`package.json` の `version`、該当する Sample |
| 公開挙動の変更（シグネチャは同じだが結果が変わる） | CHANGELOG.md、`version`、必要なら README のクイックスタート |
| 設定アセット（Config）の項目の追加・変更 | README の初期設定、AGENTS.md、CHANGELOG.md |
| Sample の追加 | `package.json` の `samples`、CHANGELOG.md |
| 依存パッケージの追加・更新 | `package.json` の `dependencies`、README の必要なパッケージ |
| 非推奨化 | `[Obsolete("代替APIの案内", error: false)]`、CHANGELOG の `### Deprecated`（移行方法を明記）、AGENTS.md |
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

## 7. Pull Request前のチェック

コード品質のチェックは [CodeGuidelines.md `## レビュー用チェックリスト`](./CodeGuidelines.md) を使ってください。それに加えて、本体開発では次を確認します。

- [ ] 追加・削除・移動したファイルに対して `.meta` が対で揃っている（`Documentation~/` 配下を除く）
- [ ] 移動・リネームでGUIDを維持している
- [ ] `.cs` がUTF-8 BOM付きで保存されている
- [ ] 公開APIを変更したなら、§6の表にある全ファイルを更新した（不要と判断したものは理由を書いた）
- [ ] `package.json` の `version` と CHANGELOG の見出しが一致している
- [ ] `Cache/` や親Unityプロジェクトの生成物をコミットしていない
- [ ] Unityでの確認事項を依頼者へ提示した

## 8. やってはいけないこと

- `.meta` を手書きする、GUIDを書き換える、`.meta` と実ファイルを別々に動かす。
- `Cache/`、`Library/`、`.csproj`、`.sln`、親Unityプロジェクトのファイルをコミットする。
- `main` や `develop` へ直接コミットする。
- 公開APIを変更したのにREADME / AGENTS.md / CHANGELOGを更新しない。
- Sampleから `internal` APIを使う。Sampleは公開APIだけで書けることの証明です。
- Runtimeコード（`Runtime/`、`Core/`）から `UnityEditor` を参照する。必要なら `Editor/` へ処理を分離し、やむを得ない場合のみ `#if UNITY_EDITOR` で囲む。
- 「将来使うかもしれない」を理由にinterface・抽象・拡張点を追加する（DesignPhilosophy `## 避ける設計`）。
- 明示された作業範囲を超えて、ロジック・公開API・シリアライズ形式を変更する。
