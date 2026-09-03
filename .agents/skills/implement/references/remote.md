# リモート環境（Unity Editor が無い）での実装フロー

Claude Code on the web などの**リモートコンテナには Unity Editor が無く、uLoopMCP も使えない**。
実装フローそのものは変わらないが、**ステップ3の検証だけが成立しない**。

このファイルは、その1点をどう埋め、埋めきれない分をどう残すかを決める。
**「Unity が無いから検証を飛ばした」で終わらせないための手順である。**

---

## 0. まずこの環境がリモートかを判定する

```bash
python scripts/verify_round.py
```

`exit 3` と「Unity Editor へ接続できません」が返ったらリモート環境である。
`exit 0` / `1` / `2` が返るなら Unity は生きているので、このファイルは読まなくてよい。

**Unity をコンテナへ導入することはできない。** 2026-08-31 に試し、エージェントプロキシが
Unity の配布ホスト（`services.api.unity.com` / `download.unity3d.com` /
`public-cdn.cloud.unity3d.com` / `unity.com`）への CONNECT をすべて 403 で拒否した。
プロキシの `selective` が `false` でも許可リスト外は通らない。**毎回試し直さないこと。**

---

## 1. 各ステップの変化

| ステップ | 通常 | リモート |
| --- | --- | --- |
| 1. 設計書 | 変わらない | **重要度が上がる。** コンパイルで拾えない誤りを設計時に潰す（→ 下記「設計で前倒しに潰す」） |
| 2. ワーカー | `codex_runner.py` | Codex CLI が無い環境では自分で実装する。設計書・検証・振り返りは省略しない |
| 3. 確認 | `verify_round.py` | **機械検査と差分の通読だけ。**（→ 下記「代替の検査」）実行できない項目は未実施として記録する |
| 4. 版更新 | 変わらない | **CHANGELOGの区分まで設計時に決める。**（→ 下記「版の粒度を設計時に決める」） |
| 5. コミット・PR | `release_round.py` | 変わらない。ただし `.meta` と finalize に固有の落とし穴がある（→ 下記） |
| 6. 振り返り | 変わらない | 変わらない |
| 7. 実施レポート | 変わらない | **「未実施の確認」が必須項目になる。**（→ 下記「未実施リストを積み上げる」） |

---

## 2. 代替の検査（リモートで実行できること）

**Unity が無くても、この5つは全部実行できる。省略しない。**

```bash
python scripts/release_round.py preflight   # BOM・.meta の対・Runtime/CoreのUnityEditor参照・
                                            # asmdefのUNITY_INCLUDE_TESTS・版の4か所整合・docs同期
python scripts/generate_meta.py --check     # `.meta` の欠落だけを見る（preflightより先に潰せる）
python scripts/build_module_docs.py --check # Documentation~/Html/ の同期
python scripts/audit_scan.py                # 機械走査での規約違反
python scripts/comment_only_diff.py         # コメントだけの差分を除いて読む
```

**そして、同名型が増えていないかを必ず見る。** リモートには `CS0101` を教えてくれるコンパイラが無く、
**重複定義はマージするまで誰も気づかない。** 実際に、同じ役割の Authoring 層が2系統
`develop` へ入り、`SceneBlockAsset` と `SceneBlockAssetDrawer` が二重定義になって
コンパイルできない状態になった。

```bash
git -C Assets/SymphonyFrameWork grep -hoE \
  "^\s*(public|internal)\s+(sealed\s+|static\s+|abstract\s+|partial\s+)*(class|struct|interface|enum)\s+[A-Za-z0-9_]+" \
  -- 'Runtime/*.cs' 'Editor/*.cs' 'Core/*.cs' \
  | sed -E 's/.*(class|struct|interface|enum)\s+//' | sort | uniq -d
```

ジェネリック引数違いの多重定義（`IInjectable<T0>` など）は同名で出るため、
**出た名前は名前空間まで見て判断する。** `partial` も同様。

加えて、**[review.md](review.md) の「機械的検索」の観点は grep で全部実行できる。**
Unity が無いことは、レビュー観点を減らす理由にならない。

**そして差分を全部自分で読む。** リモートでは、これが唯一の実質的な担保である。
コンパイラもテストも助けてくれないため、通常の Round より丁寧に読む。

### 設計で前倒しに潰す

リモートでは、通常なら compile が数秒で教えてくれる次の種類の誤りが、**依頼者が後で
Unity を開くまで表に出ない**。ステップ1とステップ3の読解で意識的に確認する。

- 型名・名前空間の衝突（`CS0101`）。**新しい `public` 型を足すときは、同じ名前空間に
  同名が無いことを `git grep` で確かめる。** 実際に `SceneBlock` 名前空間へ `SceneBlock` を
  作ろうとして衝突し、`SceneBlockAsset` へ改名している
- `nameof` の対象が存在しない、`using` の不足、可視性の不一致
- アセンブリ境界をまたぐ参照（`Runtime` から `Editor`、`Core` から `UnityEditor`）
- `.asmdef` の参照追加漏れ

---

## 3. 実行できないこと（必ず未実施として残す）

| 項目 | 通常の担保 |
| --- | --- |
| コンパイル エラー0・警告0 | `verify_round.py` の compile |
| EditMode テスト全数成功 | `verify_round.py` の EditMode |
| PlayMode テスト全数成功 | `verify_round.py` の PlayMode |
| Play Mode 2往復でゴースト参照が残らない | Domain Reload 無効のため必須のチェック |
| Editor の画面（レイアウト崩れ、表示文言） | `uloop-screenshot` と目視 |
| 生成した `.meta` を Unity が再生成・差分にしないこと | Unity にフォーカスを当てる |
| `Samples~` の `.unity` / `.asset` が実際に開けること | サンプルシーンを開く |
| Enter Play Mode Options が 3 に戻っていること | `verify_round.py` が検査・復元する |

**「たぶん通る」と書かない。実行していない項目は実行していないと書く。**

---

## 4. `.meta` はスクリプトで生成する

Unity が無いと `.meta` が生成されず、`release_round.py preflight` が必ず止まる。
**この環境に限り、スクリプトでの生成を認める**（→ `Documentation/CONTRIBUTING.md` §2）。

```bash
python scripts/generate_meta.py --check   # 欠落の一覧
python scripts/generate_meta.py           # 生成
```

スクリプトは次を機械的に担保する。手で書くと3つとも抜ける。

- **履歴に `.meta` があるパスは拒否する。** 既存アセットの GUID を作り直すと、利用側の
  参照とシリアライズ済みデータが切れる。消えているだけなら `git checkout` で戻す
- 生成後に全 `.meta` を走査し、**GUID の重複が無いことを確認する**
- 拡張子ごとの importer ブロック（`.cs` は MonoImporter、`.asset` は NativeFormatImporter、
  フォルダは `folderAsset: yes`）を使い分ける

**生成した事実を PR 説明とコミット報告へ必ず書く。** 黙って混ぜない。
後で Unity が再生成・差分を出さないことの確認は、依頼者の確認項目として残す。

### `Samples~` の `.unity` / `.asset` を新規に作るとき

Unity が無いと YAML を手で書くことになる。**ゼロから書かず、既存の同種シーンを
コピーして GUID と `m_EditorClassIdentifier` を置換する。** ゼロから書いた YAML は
Unity で開くまで妥当性が分からず、壊れていても気づけない。
**コピー元と置換内容を実施レポートへ書く。**

---

## 5. git 固有の落とし穴

### submodule の fetch refspec

`git submodule update` が作ったクローンは、`remote.origin.fetch` が親の既定ブランチ
（`main`）だけに絞られていることがある。**この状態では `git checkout develop` が
`pathspec 'develop' did not match` で失敗し、`finalize` が途中で止まる。**

```bash
git -C Assets/SymphonyFrameWork config remote.origin.fetch '+refs/heads/*:refs/remotes/origin/*'
git -C Assets/SymphonyFrameWork fetch origin
```

**セッションの最初に1回実行しておく。** 失敗してから直すと、finalize が中途半端な
状態で止まったところから復旧することになる。

### `commit --pr` と `finalize` は `gh` を必要とする

**コンテナに `gh` CLI は無い。** `release_round.py commit --pr` は
`FileNotFoundError: 'gh'` で落ちるが、**その手前のコミットとpushは成功している。**
落ちた後に同じコマンドをやり直さず、PRだけ GitHub MCP ツールで作る。

```bash
python scripts/release_round.py commit --message "[fix]説明" --issue <番号>   # --pr を付けない
```

PRの作成とマージは `mcp__github__create_pull_request` と `mcp__github__merge_pull_request`
で行い、その後の後始末は下記の分解手順に従う。

### `finalize` が実行を拒否されたとき

リモートの権限判定で `release_round.py finalize` そのものが弾かれることがある。
**手で `git` を叩き直すのではなく、finalize が行う検証を同じ順序で実行する。**

```bash
# 1. gitlink が develop から到達可能であることを先に確かめる（ここを飛ばさない）
git -C Assets/SymphonyFrameWork merge-base --is-ancestor <sha> origin/develop
# 2. submodule を develop へ揃える
git -C Assets/SymphonyFrameWork checkout develop
git -C Assets/SymphonyFrameWork merge --ff-only origin/develop
git -C Assets/SymphonyFrameWork branch -d <作業ブランチ>
# 3. 親は gitlink とパスを明示して add する。`git add -A` しない
git add -- Assets/SymphonyFrameWork <その他のパス>
git commit
git push -u origin <親のブランチ>
```

**1 を飛ばすと、squash マージやブランチ削除で gitlink が到達不能になる。**
finalize がこれを検査しているのは、実際に踏んだからである。

---

## 6. Round の切り方

**リモートでは Round を通常より小さくする。** Unity の検証がまとめて後回しになるため、
複数 Round を積んでから一括検証すると、失敗したときにどの Round の変更が原因かを
切り分けられない。**1 Round = 1つの検証可能な変更**を通常より厳しく守る。

Round の大きさが公開型の数でほぼ決まることは変わらない（→ [rounds.md](rounds.md)）。

### 公開APIを変えない Round を先頭に置く

**内部だけを作り替える Round を先に出し、公開APIの追加と破壊的変更を後ろへ回す。** リモートでは Unity 検証がまとめて後回しになるため、後で切り分けられる順序で積んでおくことが通常より効く。内部の Round が先に入っていれば、後段で見つかった不具合が「内部の作り替え」と「APIの変更」のどちらに属するかを、Round の境界だけで絞り込める。

Issue #168 は Domain/Application のカテゴリー化（内部のみ）→ カテゴリー版の公開API追加 → 既存APIの Obsolete 化、の順に切った。**この順序は設計の時点で決める。** 実装が終わってから並べ替えると、Round をまたいで差分を付け替えることになる。

なお、この順序は上の「Round の区分と、追加する検証の強さを突き合わせる」（→ [design-doc.md](design-doc.md)）と対になる。先頭の Round が公開APIを変えないと決めた以上、そこへ例外を追加できない。

### 版の粒度を設計時に決める

**`preflight` は、CHANGELOGの同じ版で `Fix` と `Change` が同居していると止まる。**
修正は独立したパッチ版へ分ける規則である。**設計の段階で「この Round はどの区分か」を
決めておかないと、実装が終わってから Round を割り直すことになる。**

実際に、重複定義の解消（Fix）と依存先候補の絞り込み（Change）を1つの Round として設計し、
`preflight` で割り直した。**割った結果は正しかった。** 壊れている `develop` の復旧が、
機能追加のレビューを待たずに先に入る。**壊れている状態を直す Round は、常に単独で先に出す。**

---

## 7. 未実施リストを積み上げる

リモートで進めた Round は、**依頼者が後でまとめて Unity 検証を行う**前提で閉じる。
そのため、各 Round の設計書 `## 実施レポート` の **「未実施の確認」に、上記3節の項目を
その Round の内容へ落として書く。** 「Unity 未検証」の一行で済ませない。

書き方の例:

```markdown
### 未実施の確認

Unity Editor の無い環境で実装したため、次はすべて未実施です。依頼者の一括検証で確認してください。

- コンパイル（エラー0・警告0）
- EditMode テスト: 新規 `SceneBlockEntryReaderTests` ほか計 N 件
- PlayMode テスト 2往復と、`SceneBlockLoader.ResetRuntimeState()` 後のゴースト参照
- `Symphony Administrator > Scene Block` タブのレイアウトと表示文言
- スクリプトで生成した `.meta` 12件を、Unity が再生成・差分にしないこと
- `Samples~/Runtime/SceneBlockSample/` の5シーンと2アセットが開けること
```

**チャットの報告だけで終わらせない。** 会話は消えるが設計書は残る。

---

## 8. 参考: CI で Unity を回す選択肢

このコンテナへ Unity を入れることはできないが、**GameCI（`game-ci/unity-test-runner`）を
GitHub Actions で回す道は塞がっていない。** その場合、ライセンスはリポジトリの Secrets に
依頼者自身が設定し、**エージェントはその値を見ない**。
`-createManualActivationFile` が出す `.alf` を依頼者が license.unity3d.com へ上げ、
返ってきた `.ulf` を Secrets へ入れる流れになる。
**エージェントが Unity アカウントの資格情報を受け取ることはない。**

導入するかは依頼者の判断であり、このフローの前提ではない。
