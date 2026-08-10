# Round 分割と Issue 対応ブランチ

1回のフローで扱えないタスクの分け方と、Issue 対応時のブランチ運用。着手前に読む。

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

**削除・改名するメンバーがあるなら、その参照元を機械的に列挙し、同じ Round へ入れる。** メンバーが消えた時点で参照元はコンパイルが通らなくなるため、参照元だけを後の Round へ回すことはできない。「表示を変えるのは次の Round」と決めても、表示元のフィールドを今の Round で消すなら、表示の変更も今の Round に入る。

```bash
rg -n "\.Mode\b|\.CreateZip\b|\.UsedDependencies\b" Assets/SymphonyFrameWork -g '*.cs'
```

目視で分割を決めない。**この検索は Round 分割を書く時点で実行する。** 実際に、確認ウィンドウの表示変更を Round 2 へ置いた設計書が、Round 1 でフィールドを削除する都合で Round 1 へ戻っている（Issue #124）。検索していれば着手前に分かった。

### ワークスペース側で古くなる記述も、同じ検索で洗い出す

**パッケージ側の構造（ファイル配置、文書の分割、公開APIの入口）を変える Round は、ワークスペース側 `Documentation/` の記述も同時に古くする。** ワーカーの変更範囲は `Assets/SymphonyFrameWork/` に限定するため、その Round の中では直せない。**どの Round でワークスペース側を直すかを、分割を書く時点で決める。**

Round 分割を書くときに、次を検索して結果を設計書へ書く。

```bash
rg -n "README\.md|Documentation~/|EditorTools\.md|AgentUsage\.md" Documentation/ AGENTS.md
```

特に落ちやすいのは次の3箇所である。

| ファイル | 古くなりやすい記述 |
| --- | --- |
| `Documentation/CONTRIBUTING.md` §6 | 「変更に応じて同時に更新するもの」の表が指す文書名とパス |
| `Documentation/CONTRIBUTING.md` §7 | Pull Request前のチェック項目 |
| `AGENTS.md` §0・§0.1 | 作業内容ごとの参照先と、正本の対応表 |

実際に、README のクイックスタートをモジュール別文書へ移した Round で、CONTRIBUTING.md §6 が「README のクイックスタート」「`Documentation~/EditorTools.md` の該当節」を指したまま2 Round分残っている（Issue #101）。

### 進め方

1. **設計書に Round 分割を書く。** 各 Round が何を含み、何を含まないかを明示する。依存順があるなら順序も書く
2. **Round は1つずつ完了させる。** 前の Round がコミットまで終わってから次へ進む。複数 Round の変更を作業ツリーに同時に載せると、コミットの切り分けができなくなる
3. **Round ごとにバージョンを刻む。** 破壊的変更を含む Round は、複数 Round にまたがる改修の最後にまとめる。途中の Round は後方互換に保ち、マイナーまたはパッチとして出す
4. **振り返りは Round ごとに行う。** 次の Round の進め方へ即座に反映できる

