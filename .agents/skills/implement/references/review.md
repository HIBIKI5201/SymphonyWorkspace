# ステップ3: 実装を確認する

差分レビューの観点、機械的に検索する違反、Unity Scene の検証ガード。

---

## 3. 実装を確認する

ワーカーの報告を鵜呑みにしない。**必ず差分を自分で読む。** Codex が自身をワーカーとする場合も、このレビュー工程を省略しない。

**検証はファイル変更を止めてから実行する。** コンパイルやテストの実行中にファイルを書き換えると、Unity が途中の状態をリコンパイルし、結果が実装の欠陥なのか編集との競合なのか切り分けられなくなる。テストの追加・修正も同じで、書き終えてから実行する。**結果が前回と食い違ったら、まず自分が実行中に何か書いていなかったかを疑い、変更を止めて再実行してから調査に入る。**

1. **差分レビュー** — `git -C "Assets/SymphonyFrameWork" status` と `git -C "Assets/SymphonyFrameWork" diff` で全変更を確認する。設計書に無い変更、範囲外のファイル、`public` の増加を特に見る。
2. **規約チェック** — `Documentation/CodeGuidelines.md` の `## レビュー用チェックリスト` を通す。名前空間とフォルダの一致、`Internal/` の使い分け、XMLドキュメント、CancellationToken の伝播、登録と解除の対。

   目視だけに頼らず、**機械的に検索できる違反は検索する**。特に次の3つは過去に見落としが起きている。

   ```
   rg -n "UnityEditor|EditorPrefs" Assets/SymphonyFrameWork/Runtime Assets/SymphonyFrameWork/Core -g '*.cs'
   ```

   - Runtime／Core から `UnityEditor` への参照（`#if UNITY_EDITOR` で囲んであっても違反）
   - テスト用 asmdef の `defineConstraints` に `UNITY_INCLUDE_TESTS` があるか（無いと nunit を参照するアセンブリが Player ビルドへ入る）

   **この Round で追加・変更した `.cs` に UTF-8 BOM が付いているか。**

   ```bash
   cd "Assets/SymphonyFrameWork" && { git diff --name-only --diff-filter=d origin/develop -- '*.cs'; git ls-files -o --exclude-standard -- '*.cs'; } | sort -u | while read -r f; do
     [ "$(head -c 3 "$f" | od -An -tx1 | tr -d ' \n')" != "efbbbf" ] && echo "BOM無し: $f"
   done
   ```

   `CONTRIBUTING.md` は `.cs` を UTF-8 BOM付きと定めているが、**BOM が無くてもコンパイルは通るため、検索しない限り気づけない。** ファイル書き込みツールの多くは BOM を付けないので、ワーカーの成果物でも自分の実装でも起こる。

   **新規ファイルだけでなく、既存ファイルを全面上書きした場合も落ちる。** 部分置換（Edit）は BOM を含む前後のバイト列をそのまま残すが、**全面書き込み（Write）は BOM ごと差し替える**。既存の `.cs` を書き直すときは Edit を使うか、Write を使ったなら書いた直後に BOM を付け直す。実際に、既存ファイル1件を Write で作り直して BOM を落としている（上の検索で拾えたため事故にはならなかった）。

   ```bash
   printf '\xef\xbb\xbf' | cat - "$f" > "$f.tmp" && mv "$f.tmp" "$f"
   ```

   対象をこの Round の差分に限っているのは、パッケージ内に BOM 無しの既存ファイルが残っているためである（全体の一括修正は独立した Round で扱う）。毎回同じ既存違反を報告する検索は読まれなくなる。
3. **コンパイル** — `uloop-clear-console` → `uloop-compile` → `uloop-get-logs`。エラー0件、意図しない警告なし。

   **`uloop-compile` が `is compiling` や `Domain Reload in progress` を返し続ける場合、`--force-recompile true` で再試行しない。** 再試行のたびに新しい Domain Reload を起こすため、次のポーリングが必ず「reloading」を見る。**自分で終わらない状態を作っていることに気づけない。** `Temp/` の `compiling.lock`・`domainreload.lock`・`serverstarting.lock` を確認して `uloop fix` を実行し、そのうえで **`uloop-get-logs` のコンソール内容を真実として読む。**

   実際に、`compiling.lock` が残っているだけでコンパイル自体は完了していたケースで、再試行ループを回してユーザーに指摘されるまで気づけなかった。**`uloop-compile` の応答はコンパイル結果そのものではなく、コンパイル結果を読める状態かどうかを示すに過ぎない。**

   **コンパイル直後の初回テスト実行は信用しない。** `uloop-clear-console` を挟んでから実行し、**同じ結果が2回続くことを確認してから合格と判断する。**

   意図的に例外ログを出すテスト（購読者例外の隔離を検証するものなど）のログが、リコンパイル直後の初回実行に限り、後続の async テストの未処理ログとして計上されることがある。実測では3回連続実行のうち1回目だけが失敗し、2回目・3回目は全数成功した。**1回の失敗を実装の欠陥と決めつけて調査に入らない。** 逆に、2回続けて同じテストが落ちるなら実装を疑う。

   テスト側では `LogAssert.ignoreFailingMessages` を使わず `LogAssert.Expect` を使う。**`ignoreFailingMessages` はログを無視するだけで消費しないため、遅れて届いたログがテスト間へ漏れる。**

   結果は `Success` / `Passed` / `Failed` / `Skipped` を**すべて記録する**。件数だけを抜き出して判断しない。

   **失敗0件でも `Success` が false なら合格にしない。** 実測で `Success=False` / `Failed=0` という結果が出ている。件数だけ見ていると気づかずに通してしまう。`Skipped` も同様で、意図せず実行されなかったテストは「成功」ではない。
4. **ランタイム確認** — `uloop-control-play-mode` で設計書の「動作確認手順」を実行し、`uloop-get-logs` で期待値と照合する。**Domain Reload が無効なので、Play Mode の開始・終了を2回繰り返し、static 状態のゴースト参照が残らないことを確認する。**
5. **`.meta` の生成** — 新規ファイルを追加した場合、`.meta` は Unity Editor がフォーカスを得たときに生成される。`uloop-focus-window` を使うか、ユーザーへ依頼する。`git status` で `.cs` と `.meta` が対で揃っていることを確認してからコミットへ進む。

### Unity Scene検証ガード

Sample Scene、Build Settings、Prefabを使うランタイム確認では、検証操作を成果物へ混入させないため次の順序を守る。

1. **検証前の状態を記録する。** 親とsubmoduleの`git status --short`を取得し、対象の`.unity`、`.prefab`、`ProjectSettings/EditorBuildSettings.asset`、自動生成enum、`.slnx`が既にdirtyか確認する。既存のdirty変更はユーザーのものとして扱い、自動復元の対象にしない
2. **検証に使うSceneは、フレームワークのAPIを呼ぶホスト側スクリプトを含まないものを選ぶ。** 含むSceneしか使えない場合、観測したログは**スタックトレースで発生元を確認してから**自分の変更へ帰属させる。ホスト側スクリプトが同じAPIを別の意図で呼んでいると、症状が見分けられない。

   このガードの他の項目が「検証操作を成果物へ混入させない」向きなのに対し、**これは逆向きの「ホスト側の既存の挙動が検証結果へ混入する」ことを防ぐ。** 実際に、ホストのデバッグ用MonoBehaviourが自分で`SceneLoader.UnloadSceneAsync`を呼んで出したエラーを、修正対象の経路が出したものと読みかけている（スタックトレースを開いて判明した）。**Sceneを開いた時点の`SceneManager`の状態と、Play Mode開始後の状態を別々に記録する**と、ホスト側スクリプトが状態を書き換えた後のスナップショットを見て誤読することも防げる
3. **Sceneを保存しない。** 検証用GameObjectの生成・破棄はPlay Mode内だけで行う。`EditorSceneManager.SaveScene`、`SaveCurrentModifiedScenesIfUserWantsTo`、動的実行のsave相当オプションを使わない。Sceneを開いた直後は`isDirty == false`を確認する
4. **複数行の動的コードは一時`.csx`へ書く。** PowerShell上のinlineコードは補間文字列や空白で引数分割されるため、`Temp/`配下の`.csx`を`--code-file`で実行し、完了後に削除する。短い1文だけinline実行を許可する
5. **時間依存の確認ではフレーム進行を検証する。** `uloop-focus-window`後も`Time.time`が進まない場合だけ、Play Mode中の`Application.runInBackground`を一時的に`true`へ設定する。待機時間だけで成功と判断せず、期待する状態またはログを読み取る
6. **Play Mode停止後に差分を照合する。** submoduleの`.unity`／`.prefab`差分と親のBuild Settings・生成物を確認する。意図しないpackage asset差分があればコミットへ進まず、保存せずに原因を調べる
7. **復元は事前にcleanだった既知ファイルだけへ限定する。** Unityが今回の検証で書き換えたと確認できるファイルだけを明示パスで戻す。作業ツリー全体への`git restore`や、検証前からdirtyだったファイルの復元を行わない

問題があればワーカーへ差し戻す。Claude Code / Gemini CLI は同じ `codex exec` に修正内容を渡し、Codex は現在のタスク内で修正する。軽微ならレビュー担当が直接直してもよい。**設計書と実装が食い違った場合は、どちらが正しいかをユーザーに確認する。**

---

