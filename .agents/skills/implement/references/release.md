# ステップ4-5: バージョン更新とコミット・PR

CHANGELOG の書き方と、submodule から親リポジトリまでの2段階コミット手順。

---

## 手順は `scripts/release_round.py` に固定してある

**git を手で叩かず、このスクリプトを使う。** 順序と検証がコード化してある。

```bash
python scripts/release_round.py preflight
python scripts/release_round.py commit --message "[add]日本語の要約" --issue 119 --pr --pr-body-file <本文ファイル>
python scripts/release_round.py finalize --paths Documentation/Designs/<機能名>.md
```

| フェーズ | 内容 | 落ちる条件 |
| --- | --- | --- |
| `preflight` | ブランチ、`version` と CHANGELOG の一致、`.cs` の UTF-8 BOM、`.meta` の対、Runtime/Core からの `UnityEditor` 参照、テスト asmdef の `UNITY_INCLUDE_TESTS` を検証する。**git の状態は変更しない** | 検証に1件でも失敗 |
| `commit` | preflight を通してから submodule へコミットし push する。`--pr` で Pull Request も作成する | メッセージが `[add]`/`[update]`/`[fix]` で始まる1行でない、preflight 失敗、変更が無い |
| `finalize` | **gitlink が `origin/develop` から到達可能かを確認してから**、submodule を `develop` へ揃え、マージ済みの `feature/*` ローカルブランチを削除し、親リポジトリをコミットして push する | submodule に未コミット変更がある、gitlink が到達不能（＝PR 未マージ） |

スクリプトが機械的に防いでいるのは次の4点。手で叩くと落としやすい。

1. **submodule を push する前に親の gitlink を更新しない**
2. **gitlink が `origin/develop` から到達可能である**
3. **親リポジトリへ `git add -A` しない**（明示したパスだけ staging する）
4. **マージ後に submodule を `develop` へ戻し、ローカルの `feature/*` を残さない**

**PR のマージだけがスクリプトの対象外。** 承認を挟む余地を残すため意図的に外してある。マージは下記のとおり手で実行する。

### finalize が行う後始末

`gh pr merge --delete-branch` は**リモートのブランチしか消えない**ことがある。`--repo` を付けた `gh` は cwd のリポジトリを見るため、ワークスペースルートから実行すると submodule 側のローカルブランチが残る。`finalize` はこれを含めて片付ける。

1. submodule の HEAD が `origin/develop` から到達可能かを確認する
2. 現在のブランチが `feature/*` なら、`origin/develop` へマージ済みかを確認して `develop` へ切り替える
3. `develop` を `origin/develop` へ fast-forward する
4. マージ済みの `feature/*` ローカルブランチを削除する（`--keep-branches` で抑止できる）

**1 と 3 の順序を入れ替えてはいけない。** 先に `develop` を進めると、PR が未マージでも「develop の HEAD は develop から到達可能」となって 1 の検査が素通りし、作業を含まないコミットを gitlink へ記録してしまう。

削除対象を `feature/*` に限っているのは、フローの命名規則に沿うブランチだけを消し、別目的のローカルブランチへ触らないためである。

`preflight` はステップ3の検証のうち機械的に判定できる部分だけを見る。**`uloop-compile` とテストの実行は別途行う。** スクリプトは Unity を起動しない。

以下は、スクリプトが何をしているかと、スクリプトが担当しない部分の説明である。

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

**一括修正で規約を揃えた Round では、同じ Round 内で再発を検出する機械チェックを追加する。** チェックの無い一括修正は必ず戻る。過去に `.cs` の UTF-8 BOM をコミット `ccac325` で一度統一したが、検出手段を残さなかったため、その後に追加されたファイルで17件まで崩れている。追加先は `references/review.md` のステップ2（機械的に検索する違反）。

---

## 5. コミットし、Pull Request を作成する

submodule と親リポジトリの2段階。**順序を守る。**

**submodule・親リポジトリとも、コミットと push は確認を取らずに実行してよい。** このフローに含まれる操作であり、都度の承認は不要。承認が要るのは `develop` から `main` へのマージだけ（ステップ5の注記）。

1〜4 は `release_round.py commit` が行う。

1. submodule が、この Round 専用の作業ブランチになっていることを確認する。特定の Issue へ対応する場合は、着手前に作成したブランチを使う
2. submodule でコミットする。prefix は `[add]` / `[update]` / `[fix]`。prefix と本文の間にスペースを入れない。メッセージは日本語
3. submodule を push する。**push しないまま親の gitlink を更新すると、他の開発者が解決できない参照になる。**
4. submodule で `develop` 向けの Pull Request を作成する。Issue 対応の場合は、PR 本文に `Issue: #<Issue番号>` を記載する

```bash
python scripts/release_round.py commit --message "[add]<日本語の要約>" --issue <番号> --pr --pr-body-file <本文ファイル>
```
5. **`develop` へのマージまで実行してよい。**
   ```
   gh pr merge <PR番号> --merge --delete-branch
   ```
   - 対応する Issue を閉じる。Issue が無いラウンドなら不要
   - **`develop` へ戻す操作とローカルブランチの削除は手で行わない。** 次の `finalize` が行う

   **`develop` から `main` へのマージだけは人間が行う。** ここは実行しない。
6. マージ後、親リポジトリで gitlink と設計書をコミットし、**push する**。submodule を `develop` へ戻す操作もここに含まれる。

   ```bash
   python scripts/release_round.py finalize --paths Documentation/Designs/<機能名>.md
   ```

**gitlink は `develop` に到達可能なコミットを指すこと。** PR がマージされる前に feature ブランチのコミットを指すと、squash マージや作業ブランチ削除で**そのコミットが到達不能になり、新規クローンの `git submodule update` が失敗する**。

`finalize` はこれを実行前に確認し、到達できなければ何もせず止まる。PR のマージを待ってから再実行する。

`.meta` の追加は対応する `.cs` と同じコミットに含める。1コミットは1つの意図にまとめる。

**コミットとpushはこのフローの一部として実行する。** ステップ3の検証がすべて通っていることが前提。

- **検証が1つでも失敗しているならコミットしない。** 直してから戻る
- 今回の変更と無関係な既存の未コミット変更が混ざっている場合は、**別コミットへ分ける**。判断がつかないものは残したまま、その旨を報告する
- Unity や IDE が書き換えた `ProjectSettings/`、`.slnx`、生成物などは、自分の変更でない限り含めない
- 何をコミットし、何を意図的に残したかを必ず報告する

---

