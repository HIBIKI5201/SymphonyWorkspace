# ステップ3: 実装を確認する

差分レビューの観点、機械的に検索する違反、Unity Scene の検証ガード。

---

## 3. 実装を確認する

ワーカーの報告を鵜呑みにしない。**必ず差分を自分で読む。** Codex が自身をワーカーとする場合も、このレビュー工程を省略しない。

**検証はファイル変更を止めてから実行する。** コンパイルやテストの実行中にファイルを書き換えると、Unity が途中の状態をリコンパイルし、結果が実装の欠陥なのか編集との競合なのか切り分けられなくなる。テストの追加・修正も同じで、書き終えてから実行する。**結果が前回と食い違ったら、まず自分が実行中に何か書いていなかったかを疑い、変更を止めて再実行してから調査に入る。**

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

