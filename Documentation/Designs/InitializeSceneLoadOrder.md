# InitializeSceneLoadOrder

Issue: [#147](https://github.com/HIBIKI5201/SymphonyFramework/issues/147)

## 目的

起動時のScene整理（`SceneLoadConfig.IsResetAndLoadOnPlay`）が、**ロード済みSceneを1つも残さない構成で失敗する。**

`SceneLoadService.InitializeAfterSceneLoad` は「整理対象を全部アンロード → 初期Sceneをロード」の順で動く
（[SceneLoadService.cs:474](../../Assets/SymphonyFrameWork/Runtime/System/SceneLoader/Internal/Application/SceneLoadService.cs#L474) でアンロード、
[:485](../../Assets/SymphonyFrameWork/Runtime/System/SceneLoader/Internal/Application/SceneLoadService.cs#L485) でロード）。
Unityは**最後の1シーンをアンロードできない**ため、整理対象がロード済みSceneの全件になると
`SceneManager.UnloadSceneAsync` が `null` を返し、
`UnitySceneLoader` が `Failed to start unloading scene: <name>.` を出して失敗する。

3.8.1 時点でこの順序が成立していないのは、2.4.3 で予備シーンが無くなったためである。
2.4.3 より前は `SymphonyOrchestrator` が実シーン `SymphonySystem` を `SceneManager.CreateScene` で生成し、
Scene Loader がそれを常に整理の除外対象へ加えていた。**必ず1シーンが残ることが保証されていたので、
アンロード先行でも成立していた。** 2.4.3 で永続オブジェクトを `DontDestroyOnLoad` へ移した際
（[CHANGELOG.md 2.4.3](../../Assets/SymphonyFrameWork/CHANGELOG.md)）、この順序依存が見落とされている。

発生する構成:

| 状況 | ロード済み | 整理対象 | 結果 |
| --- | --- | --- | --- |
| 初期Sceneに含まれないSceneを単独で開いてPlay | `{InGame}` | `{InGame}` | **失敗** |
| ビルドで、最初のSceneが初期Scene一覧にも除外一覧にも無い | `{Boot}` | `{Boot}` | **失敗** |
| 初期Sceneや除外対象を一緒に開いてPlay | `{Persistent, InGame}` | `{InGame}` | 成功 |

**失敗はエラーログだけで止まらない。** アンロードできなかったSceneが残ったまま初期Sceneが加算ロードされるため、
利用側は「消えたはずのSceneのオブジェクトが、初期Sceneの初期化より先に動く」状態を踏む。
Issue #147 で報告された2件目以降のエラーはこの連鎖である。

既存の何では足りないか: `LoadScene(mode: LoadSceneMode.Single)` は既に「ロード → `ResetScene` で他をアンロード」の
順で動いており、この問題を持たない（[SceneLoadService.cs:215](../../Assets/SymphonyFrameWork/Runtime/System/SceneLoader/Internal/Application/SceneLoadService.cs#L215)）。
**起動時経路だけが逆順のまま残っている。**

## 公開API

**変更しない。** `SceneLoader` の公開シグネチャ、`SceneLoadConfig` のシリアライズ形式、
`SceneLoadInfo` のいずれにも変更を加えない。修正対象は `internal sealed class SceneLoadService` の
`internal async Task InitializeAfterSceneLoad(bool, string[], string[])` の内部順序だけである。

DesignPhilosophy「公開範囲」に照らすと、`SceneLoadService` は Application層のServiceで `internal`、
`SceneLoadRegistry` も `internal` である。今回追加する判断はすべてService内部に閉じるため、
公開範囲を広げる必要はない。

## ファイル構成

| パス | 名前空間 | 変更 |
| --- | --- | --- |
| `Assets/SymphonyFrameWork/Runtime/System/SceneLoader/Internal/Application/SceneLoadService.cs` | `SymphonyFrameWork.System.SceneLoad` | 変更（`InitializeAfterSceneLoad` のみ） |
| `Assets/SymphonyFrameWork/Tests/Editor/SceneLoadServiceTests.cs` | `SymphonyFrameWork.Tests` | 変更（テスト3件追加、`FakeSceneLoader` 拡張） |

新規ファイルは無い。`Internal/` の内外の区分にも変更は無い。

## 依存方向

Application層の内部順序変更のみ。`SceneLoadService` → `ISceneLoader` / `SceneLoadRegistry` の
参照方向は変わらない。Infrastructure（`UnitySceneLoader`）とAdaptor（`SceneLoader`）には手を入れない。

## 設計

**順序を「初期Sceneのロード → 整理対象のアンロード」へ入れ替える。ロード済みSceneの数で分岐しない。**

```text
変更前: 整理対象をアンロード → 初期Sceneをロード
変更後: 初期Sceneをロード → 成功したら整理対象をアンロード
```

**シーン数で分岐させない理由**: 「0件になるときだけロードを先にする」ガードも同じ不具合を閉じられるが、
**構成によって初期化の順序が変わることになる。** 利用側から見て、単独で開いてPlayしたときと
初期Sceneを一緒に開いてPlayしたときで、初期Sceneの `IInitializeAsync` が旧Sceneの破棄前に走るか後に走るかが
入れ替わる。**再現条件が「開いていたシーンの数」に依存する不具合を将来生む形なので採らない。**
全構成で1つの順序に統一する。

この順序は `LoadScene(mode: LoadSceneMode.Single)` の「ロード → `ResetScene`」と一致する。
**Single相当の遷移と起動時整理が同じ順序になる**ため、Scene Loader全体としても一貫する。

**トレードオフ（承知のうえで受け入れる）**: 整理対象と初期Sceneが同時にロードされる時間が生まれ、
メモリのピークが上がる。上がる量は「開いていたSceneのうち整理対象になるもの」の分で、
起動時の1回に限られる。

**一時的な空Sceneを作る案は採らない。** `SceneManager.CreateScene` で足場を作れば
ロードとアンロードを同時に投げられるが、(1) Unityは非同期Scene操作をキューで直列に処理するため
同時に投げても実際に重なるとは限らない、(2) 得られるのは起動時1回・短いほうの所要時間だけ、
(3) 旧Sceneの破棄と新Sceneの `InitializeRootObjectsAsync`（DIと `IInitializeAsync`）が重なり、
`ServiceLocator` の登録解除と登録の前後関係がフレーム依存になる。
**順序の不確定性がこのIssueの原因そのものなので、それを増やす方向は採らない。**

### ロードに失敗した場合はアンロードしない

初期SceneがBuild Settingsに無いなどで `LoadScenes` が `false` を返した場合、整理対象のアンロードを行わない。
置き換え先が用意できていない状態で現在のSceneを捨てると、ロード済みSceneが0件になるか、
最後の1件のアンロードが失敗して中途半端な構成が残るかのどちらかにしかならない。
現在のSceneを残すほうが復帰しやすく、ロード失敗自体は `UnitySceneLoader.LoadSceneAsync` が
既にエラーログで通知している。

**これはシーン数による分岐ではなく、失敗時の分岐である。** 正常系の順序は構成によらず1つに保たれる。

### Active Sceneの扱い

追加の対応を要しない。この経路の `SceneLoadRequest` は既定の優先度0で作られ、
`SynchronizeLoadedScenes` 直後のActive Sceneの優先度も0であるため、
`_registry.ActiveScenePriority <= request.Priority` が必ず成立して初期SceneがActiveになる。
その後アンロードされる旧SceneはRegistry上のActiveではなくなっているため、
`UnloadScene` の再選択分岐（[:337](../../Assets/SymphonyFrameWork/Runtime/System/SceneLoader/Internal/Application/SceneLoadService.cs#L337)）は動かない。

## エラー処理

- 例外は追加しない。既存どおり、`ValidateSceneNames` の不正引数だけが例外を投げる。
- ロード失敗は戻り値で扱う。`InitializeAfterSceneLoad` の戻り値型（`Task`）は変えず、
  アンロードを実行するかどうかの内部判断にのみ使う。
- アンロード失敗時のログは `UnitySceneLoader` が持つ既存の実装のままとする。

## 影響範囲

- 公開API・シリアライズ形式への影響なし。移行手順は不要。
- **正常に動いていた構成でも順序が変わる。** これまで「旧Sceneが消えてから初期Sceneが初期化される」順だったものが、
  「初期Sceneが初期化されてから旧Sceneが消える」順になる。旧Sceneの `OnDestroy` が
  初期Sceneの `IInitializeAsync` より後に走るため、**旧Sceneの破棄処理が初期Sceneの登録した状態へ
  触れる作りになっていた場合は影響を受ける。** CHANGELOGに明記する。
- アンロードとロードの回数は変わらない。

## テストの置き場と種別

EditMode。`Assets/SymphonyFrameWork/Tests/Editor/SceneLoadServiceTests.cs` へ追加する。
`InitializeAfterSceneLoad` は `internal` だが、`InternalsVisibleTo` によりテストアセンブリから呼べる
（既存テストが同じ経路で `SceneLoadService` を直接生成している）。

**`FakeSceneLoader` をUnityの制約に合わせる。** 現状の fake は最後の1シーンでもアンロードに成功するため、
今のままではこの不具合を再現できない。次を加える。

- `UnloadSceneAsync` は、対象がロード済みかつロード済み件数が1件のとき `false` を返す（Unityの拒否を模す）
- `LoadSceneAsync` / `UnloadSceneAsync` の呼び出しを `"Load:<name>"` / `"Unload:<name>"` の順で記録し、`Operations` で公開する
- 指定したScene名のロードを失敗させる `FailLoad(string)` を持つ

既存テスト3件はロード済み3件の状態からの1件アンロードなので、この変更で期待値は変わらない。

準備は公開APIまたはService経由で行う（`service.LoadScene(...)` でロード済み状態を作る）。
Registryを直接操作しない。

| テスト名 | 検証内容 | どう書くか |
| --- | --- | --- |
| `InitializeAfterSceneLoad_UnloadsEveryLoadedScene_LoadsBeforeUnload` | 残るSceneが無い構成でも整理が成立する | `service.LoadScene("InGame")` で準備 → `InitializeAfterSceneLoad(true, ["Persistent"], null)` → `loader.Operations` が `["Load:InGame", "Load:Persistent", "Unload:InGame"]` と一致し、`loader.GetLoadedSceneNames()` が `["Persistent"]` だけであることを `Is.EquivalentTo` で確認 |
| `InitializeAfterSceneLoad_KeepsIgnoredScene_LoadsBeforeUnload` | **残るSceneがある構成でも同じ順序になる**（シーン数で分岐しないことの検証） | `service.LoadScene("Persistent")` と `service.LoadScene("InGame")` で準備 → `InitializeAfterSceneLoad(true, ["Title"], ["Persistent"])` → 準備分2件を除いた `Operations` が `["Load:Title", "Unload:InGame"]` の順であることと、ロード済みが `["Persistent", "Title"]` であることを確認 |
| `InitializeAfterSceneLoad_InitializeSceneLoadFails_KeepsLoadedScene` | 初期Sceneのロードに失敗したらアンロードしない | `loader.FailLoad("Missing")` → `service.LoadScene("InGame")` で準備 → `InitializeAfterSceneLoad(true, ["Missing"], null)` → `loader.GetLoadedSceneNames()` が `["InGame"]` のままで、`Operations` に `"Unload:InGame"` を含まないことを確認 |

準備段階の `LoadScene` が `Operations` へ記録を残すため、**期待値には準備分の `"Load:..."` を必ず織り込む。**
1件目は全件を列挙し、2件目は準備直後の件数を記録して差分だけを比較する。

例外ログを伴うテストは無い（fake は `Debug.LogError` を呼ばない）。`LogAssert` は不要。

PlayModeテストは追加しない。この不具合はUnityのScene数に対するUnity側の拒否が起点であり、
fake で拒否を模して順序を検証できる。実機での確認は下記「動作確認手順」で人が行う。

## 動作確認手順

自動で確認する項目:

- `uloop-compile` がエラー0・警告0
- EditModeテスト全数成功（追加3件を含む）

人が操作する項目:

1. `Project Settings > SymphonyFrameWork > Scene Load` で `Is Reset And Load On Play` を有効にし、
   `Initialize Scene List` に初期Sceneを1つ設定する
2. **初期Sceneにも除外一覧にも含まれないSceneを単独で開き、Play Modeへ入る。**
   - `Failed to start unloading scene:` が出ないこと
   - Hierarchyに初期Sceneだけが残り、開いていたSceneが消えていること
3. 初期Sceneと別Sceneの2つを開いてPlay Modeへ入り、別Sceneだけが消えること。
   **2と同じく、初期Sceneが先に現れてから別Sceneが消える順になっていること**
4. `Initialize Scene List` に Build Settings へ未登録の名前を入れてPlay Modeへ入り、
   ロード失敗のエラーログが出たうえで、**開いていたSceneが残っていること**（0件にならないこと）
5. Domain Reload が無効のため、Play Modeの開始・終了を2回繰り返し、
   2回目も同じ結果になること・ゴースト参照が残らないこと

## バージョン判断

**パッチ（3.8.1 → 3.8.2）。** `internal` の内部順序のみの変更で、公開API・シリアライズ形式・
既定値の意味を変えない。DesignPhilosophy「バージョニング」の「公開契約を変えない修正はパッチ」に当たる。

正常系の順序が変わる点はCHANGELOGの `### Fix` の中で明記する。**変更は「起動時整理の順序」1つであり、
`### Change` を別に立てて `Fix` と同居させない**（`release_round.py preflight` の同居検査に掛かる）。

Roundは分割しない。変更は2ファイル・1メソッドに収まり、単独で検証・リリースできる。

## この Round で触るバージョン関連ファイル

- `Assets/SymphonyFrameWork/package.json` の `version` → `3.8.2`
- `Assets/SymphonyFrameWork/CHANGELOG.md` に `## [3.8.2]` の見出しと `### Fix` を追加
- `Assets/SymphonyFrameWork/README.md` の「現在のバージョン」 → `3.8.2`

`AGENTS.md` のAPI早見表は公開APIが変わらないため触らない。
`Documentation~/Architecture.md` の起動シーケンス図も、フェーズ構成が変わらないため触らない。
