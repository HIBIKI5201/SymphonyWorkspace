# ステップ4-5: バージョン更新とコミット・PR

CHANGELOG の書き方と、submodule から親リポジトリまでの2段階コミット手順。

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
5. **`develop` へのマージまで実行してよい。** マージ後は次を行う
   ```
   gh pr merge <PR番号> --merge --delete-branch
   ```
   - 作業ブランチを削除する（上のオプションで同時に消える）
   - 対応する Issue を閉じる。Issue が無いラウンドなら不要
   - **`develop` へ戻る**（`--delete-branch` で自動的に戻るが、`git branch --show-current` で確認する）

   **`develop` から `main` へのマージだけは人間が行う。** ここは実行しない。
6. 親リポジトリで gitlink と、設計書（`Documentation/Designs/<機能名>.md`）をコミットし、**push する**。
   ```
   git add "Assets/SymphonyFrameWork" "Documentation/Designs/<機能名>.md"
   git commit -m "[add]<日本語の要約>を取り込み"
   git push origin main
   ```

**gitlink は `develop` に到達可能なコミットを指すこと。** PR がマージされる前に feature ブランチのコミットを指すと、squash マージや作業ブランチ削除で**そのコミットが到達不能になり、新規クローンの `git submodule update` が失敗する**。

更新前に必ず確認する。

```
git -C "Assets/SymphonyFrameWork" fetch origin
git -C "Assets/SymphonyFrameWork" merge-base --is-ancestor <gitlinkにするコミット> origin/develop && echo OK
```

`OK` が出ない場合は、PR のマージを待ってから gitlink を貼り直す。

`.meta` の追加は対応する `.cs` と同じコミットに含める。1コミットは1つの意図にまとめる。

**コミットとpushはこのフローの一部として実行する。** ステップ3の検証がすべて通っていることが前提。

- **検証が1つでも失敗しているならコミットしない。** 直してから戻る
- 今回の変更と無関係な既存の未コミット変更が混ざっている場合は、**別コミットへ分ける**。判断がつかないものは残したまま、その旨を報告する
- Unity や IDE が書き換えた `ProjectSettings/`、`.slnx`、生成物などは、自分の変更でない限り含めない
- 何をコミットし、何を意図的に残したかを必ず報告する

---

