# CodeCommentConventions

## 目的

`Documentation/CodeGuidelines.md` へ追加したコメント・可読性規約（Issue #153）を、既存の本体コード160ファイルへ適用する。規約は文書として存在するが、コード側は1ファイルも適合していない。

現状の乖離は次のとおり（`Assets/SymphonyFrameWork/` の `Tests/` と `Samples/` を除く160ファイル、18,551行を機械走査した結果）。

| 規約 | 該当する節 | 現状 |
| --- | --- | --- |
| `#region 外部向けAPI` / `#region 内部処理` の二分割 | `### regionで外部向けAPIと内部処理を分ける` | **0ファイル**。代わりに規約が禁じる話題別regionが4ファイルに存在する |
| 処理のまとまりごとのコメント | `### 処理コメント` | 18,551行に対し278行。ほぼ全メソッドが無コメント |
| 型・メソッドのサマリーは複数行形式 | `### サマリーの形式` | 1行形式が886件。うち型・メソッド分は複数行化が必要 |
| ローカル変数の型明示 | `### ローカル変数の型は明示する` | `var` が121件 |
| 短縮形でも波括弧 | `### 短縮形でも波括弧を省略しない` | 波括弧省略が265件。加えて本文1文のAllman形式が多数（1行形式へ） |

規約を書いただけでは、次に触ったファイルだけが部分適合していく。**適合していないファイルが多数派である限り、新規コードの書き手（人・AI）は周囲のコードを手本にするため、規約は定着しない。**

## 公開API

**公開APIの追加・変更・削除は一切行わない。** シグネチャ、可視性、名前空間、シリアライズ形式のいずれも変更しない。

本改修が触るのは次の5種類だけである。

1. コメント（`//`）の追加
2. XMLドキュメント（`///`）の書式変更と加筆
3. `#region` / `#endregion` の挿入と、既存の話題別regionの除去
4. ローカル変数宣言の `var` → 型名（右辺は必要に応じてターゲット型 `new()` へ短縮）
5. 制御構文の波括弧補完と、本文1文のAllman形式 → 1行形式

`4` と `5` はコンパイル結果が変わらない書き換えに限る。`var` を型名へ置き換えるとき、推論結果と異なる型名を書かない（`IReadOnlyList<T>` を返すメソッドの結果を `List<T>` として受けない、など）。

## ファイル構成

新規ファイルは無い。既存160ファイルを変更するのみ。名前空間、ファイル名、配置はすべて現状を維持する。

対象は `Assets/SymphonyFrameWork/` 配下の次を除く全 `.cs`。

| 除外 | ファイル数 | 理由 |
| --- | --- | --- |
| `Tests/` | 39 | テストクラスに「外部向けAPI／内部処理」の二分割が意味を持たない。テストの可読性規約は別途定める必要があり、本改修の範囲外とする |
| `Samples/` | 13 | 利用例であり、規約適用の価値はある。ただし本体160ファイルの適合を先に完了させ、必要なら後続で扱う |

## 依存方向

変更しない。レイヤー、asmdef参照、`Core → Runtime → Editor` の向きに影響しない。

## エラー処理

変更しない。例外、戻り値、ログの挙動に手を入れない。

**既存の挙動がガイドラインに反していても、本改修では直さない。** たとえば `Debug.Log` が `#if UNITY_EDITOR` の外にある、`Manager` で終わる型名がある、といった指摘は記録するだけにして、コメント追加と同じコミットへ混ぜない。混ぜると「コメントだけの変更」というレビュー前提が崩れ、差分を機械的に検証できなくなる。

## 並べ替えで壊さないための不変条件

region の二分割は、記述順1〜22に合わせたメンバーの並べ替えを伴う場合がある。**次の3つは並べ替えてはならない。**

| 不変条件 | 理由 |
| --- | --- |
| **初期化子を持つフィールドどうしの相対順序** | C#はフィールド初期化子を宣言順に実行する。順序を変えると初期化結果が変わる。対象コードに129件存在する |
| **`static` コンストラクタと `static` フィールド初期化子の順序** | 同上。加えてDomain Reload無効環境では初期化順の誤りが再現しにくい |
| **`switch` の `case` や `enum` メンバーの順序** | `enum` の暗黙の数値が変わると、シリアライズ済みデータの意味が変わる |

初期化子を持たないフィールドどうし、およびメソッドどうしの並べ替えは安全であり、記述順に従って行ってよい。

## 既存regionの扱い

規約が禁じる話題別regionが4ファイルにある（`#region` を折りたたみや細分類に使わない、3つ目のregionを作らない）。**いずれも除去し、必要な区切りは通常のコメント行で表す。**

| ファイル | 現状 | 対応 |
| --- | --- | --- |
| `Core/Editor/EditorSymphonyConstant.cs` | 話題別region 3つ（自動生成物のパス / Setting Provider / Enumの名前） | regionを除去し、各グループの先頭へ `// 自動生成物のパス` 等のコメントを置く。定数だけのstatic classのため `#region 内部処理` は書かない |
| `Runtime/Debug/SymphonyDebugLogger.cs` | `#region Obsolete機能` | 除去する。`[Obsolete]` な公開メンバーは `#region 外部向けAPI` の中へ入れ、コメントで区切る |
| `Editor/Configs/Drawer/SceneLoadConfigDrawer.cs` | メソッド本体の中のregion | 除去し、処理コメントへ置き換える |
| `Runtime/Utility/SymphonyVisualElement.cs` | メソッド本体の中のregion | 同上 |

`partial` な型（UITKの4ウィンドウと `SymphonyVisualElement`、`AutoEnumGeneratorWindow`）は、**ファイルごとに二分割する。** 型全体で1組にしようとすると、片方のファイルにregionの開始だけが残る。

## テストの置き場と種別

**新規テストは書かない。** 本改修は実行時の振る舞いを持たない変更（コメント、書式、宣言の書き換え）に限定しており、検証すべき新しい契約が無いため。

代わりに、**既存テストの全数成功を回帰の検出手段として使う**（EditMode 39ファイル分、PlayMode 3ファイル分）。`var` の置き換えと波括弧の補完が振る舞いを変えていないことは、ここで捕まえる。

加えて、Round ごとに次の機械的検証を行う（`## 動作確認手順`）。

## 動作確認手順

Round ごとに、次をすべて満たすことを確認する。

1. `uloop-clear-console` → `uloop-compile` が **エラー0・警告0**
2. `uloop-run-tests --test-mode EditMode` が全数成功
3. `uloop-run-tests --test-mode PlayMode` が全数成功
4. **コメントを除去した差分が、意図した書き換えだけであること。** 下記の検証スクリプトで確認する
5. `python scripts/release_round.py preflight` が通る（`.cs` のUTF-8 BOM、`.meta` の対、Runtime/Coreからの `UnityEditor` 参照を含む）
6. Play Mode の開始・終了を2回繰り返し、Consoleに新しいエラーが出ない（Domain Reload無効環境の確認）

### コメント以外の差分を抽出する

**12 Round すべてで同じ確認を行うため、目視ではなくスクリプトで抽出する。** 本改修の差分は大半がコメントであり、そのまま `git diff` を読むと、`var` の置き換えや波括弧の補完といった**振る舞いを変えうる変更がコメントに埋もれる**。

Round 1 で `scripts/comment_only_diff.py` を作り、以降の Round で使う。やることは次の3つ。

1. 変更前後の `.cs` から `//` と `///` の行、および空行を除去する
2. 残った行を正規化する（連続空白の圧縮）
3. 差分を出す。ここに出てくる行だけを人が読む

コメント追加のみの Round なら出力は空になる。`var` の置き換えと波括弧の補完を含む Round では、その件数分だけが出る。**出力件数が走査で数えた件数と一致するかを照合する。**

## 影響範囲

利用側への影響は無い。公開APIとシリアライズ形式を変更しないため、移行手順も不要。

利用者向け文書（`Documentation~/`、`README.md`、`Deprecations.md`）の更新も不要である。**`Documentation/CONTRIBUTING.md` §6 の表が挙げる更新条件（公開APIの追加・変更・削除、メニューパスの変更、`[Obsolete]` の追加）のいずれにも該当しない。** 更新不要と判断した理由は各Roundのコミットメッセージへ記載する。

ワークスペース側 `Documentation/` にも古くなる記述は生じない。本改修はファイル配置、文書の分割、公開APIの入口のいずれも変えないため。

## Round 分割

**12 Round に分ける。** 各Roundは単独でコンパイル・テストが通り、単独でリリースできる。Roundの境界はサブシステムに揃え、1Roundあたり20ファイル以内に収めた。

| Round | 範囲 | ファイル | 行数 |
| --- | --- | --- | --- |
| 1 | `Core/` | 7 | 533 |
| 2 | `Runtime/System/ServiceLocator/` | 14 | 1,736 |
| 3 | `Runtime/System/SceneLoader/` | 13 | 2,272 |
| 4 | `Runtime/System/SaveSystem/` | 16 | 1,486 |
| 5 | `Runtime/System/Audio/` + `Runtime/System/Pause/` | 14 | 1,276 |
| 6 | `Runtime/Utility/` + `Runtime/Debug/` | 10 | 2,197 |
| 7 | `Runtime/` 残り（Orchestrator、Component、Configs、Interface、Attribute、Exception、AssemblyInfo） | 19 | 740 |
| 8 | `Editor/Generator/AssetStoreToolsPackager/` 直下 | 18 | 2,596 |
| 9 | 同 `Pipeline/` `PipelineStandardStrategy/` + `AssemblyGenerate/` `EnumGenerate/` `FolderGenerate/` | 14 | 1,436 |
| 10 | `Editor/Administrator/` + `Editor/Debug/` | 10 | 1,723 |
| 11 | `Editor/Configs/` + `Editor/AttributeDrawer/` + `Editor/SettingProvider/` | 15 | 1,418 |
| 12 | `Editor/` 直下 + `Documentation/` + `Orchestrator/Internal/` + `PackageLoader/` | 10 | 1,138 |

**Round 1 をパイロットとする。** 最小規模（7ファイル）で、`Core/` はRuntimeとEditorの両方から参照されるため、規約適用で問題が出れば早期に分かる。Round 1 の差分レビューを終えるまで Round 2 へ進まない。Round 1 で確定させるのは次の3点。

- ワーカーへ渡すプロンプトの雛形（どこまで書けば処理コメントの粒度が揃うか）
- `#region` を入れる際の並べ替え範囲（不変条件に触れずどこまで動かすか）
- `scripts/comment_only_diff.py` の出力形式

依存順は無い。Round 1 以降は上の順で進めるが、途中で優先順位を変えても他Roundの前提は崩れない。

## ブランチとコミットの単位

**ブランチとPullRequestは全Roundで1本に集約する。** `develop` から `feature/153-code-comments` を切り、12 Round分をこのブランチへ積む。

Round は git の単位ではなく**実装と検証の単位**として維持する。Roundごとに「ワーカーへ委譲 → 差分レビュー → コンパイル → テスト」を1周し、通ってから1コミットを積む。12コミット・1PRになる。

| 操作 | 回数 | 実行時期 |
| --- | --- | --- |
| `release_round.py commit --message ... --issue 153` | 12回 | 各Roundの検証が通った直後。`--pr` は付けない |
| `release_round.py bump --level patch` | 1回 | Round 12 の完了後、最後のコミットの前 |
| PullRequest の作成 | 1回 | 全Round完了後 |
| `release_round.py finalize` | 1回 | PRマージ後 |

**途中のRoundではバージョンを据え置く。** `preflight` のバージョン検査は `package.json`・CHANGELOG・README の3者が一致していることだけを見るため、3.9.4 のまま据え置いても各Roundのコミットは通る。

**Round 1 のコミット前に、ワークツリーへ他Roundの変更を載せない。** 1ブランチに集約しても、コミットの切り分けはRound単位で保つ。

## バージョン判断

**patch を1回。** 公開APIの追加も変更も無く、実装の可読性だけを変えるため（`Documentation/DesignPhilosophy.md` の `### バージョニング`）。

現在 3.9.4 のため **3.9.5** となる。Roundごとに刻まないのは、12回の patch がすべて同一の意図（規約の適用）であり、利用者から見て区別する意味が無いため。

CHANGELOG は `### Change` へ、適用した規約と対象範囲を数行で書く。**規約の内容そのものを複製しない。** 正本は `Documentation/CodeGuidelines.md` である。

## 最後に触るバージョン関連ファイル

**`python scripts/release_round.py bump --level patch` が3箇所を同時に書き換えるため、手で編集しない。**

| ファイル | 触る箇所 |
| --- | --- |
| `Assets/SymphonyFrameWork/package.json` | `version` を 3.9.5 へ |
| `Assets/SymphonyFrameWork/CHANGELOG.md` | 先頭へ `## [3.9.5]` と `### Change` |
| `Assets/SymphonyFrameWork/README.md` | 「現在のバージョン」 |

この3ファイルを触るのは Round 12 完了後の1回だけであり、他のRoundのコミットとは重ならない。PR本文へ `Issue: #153` を記載する。
