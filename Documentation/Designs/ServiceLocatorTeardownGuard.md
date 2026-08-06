# ServiceLocatorTeardownGuard

Issue: [#150](https://github.com/HIBIKI5201/SymphonyFramework/issues/150)

## 目的

Play Mode 終了時、利用側の `OnDestroy` / `OnDisable` からの `ServiceLocator.UnregisterInstance` が
`SymphonyNotInitializedException` になる。

`SymphonyOrchestrator` が Service Locator を解放した後に Unity がシーンオブジェクトを破棄するため、
利用側の解除処理は必ず未初期化の Locator を叩く。公開APIはすべて `EnsureInitialized()` を通り
（[ServiceLocator.cs:506](../../Assets/SymphonyFrameWork/Runtime/System/ServiceLocator/ServiceLocator.cs#L506)）、
判定に使える `IsInitialized` は `internal`
（[:465](../../Assets/SymphonyFrameWork/Runtime/System/ServiceLocator/ServiceLocator.cs#L465)）である。

**フレームワークが利用側へ勧めている書き方そのものが落ちる。**

> 登録と解除を同じライフサイクルの対として書く。基本形は `OnEnable` で `RegisterInstance`、`OnDisable` で `UnregisterInstance`。
> — `Documentation~/AgentUsage.md`

### なぜ今顕在化したか

| 版 | 変更 |
| --- | --- |
| 1.27.20 | `UnregisterInstance<T>()` はガード無しの `_manager.UnregisterInstance(typeof(T))` |
| 2.4.1 | 未初期化アクセスを `SymphonyNotInitializedException` へ統一 |
| 2.9.0 | 終了処理を Orchestrator へ集約し、解放順が確定 |

3つが重なり、この経路が**必ず**例外を出すようになった。

### フレームワークは同じ問題を自分だけ解決している

2.15.1 の `### Fix` がこれである。

> Play Mode終了時にOrchestratorがService Locatorを解放した後で `SymphonyLocate.OnDisable()` が登録状態を照会し、`SymphonyNotInitializedException` を記録する問題を修正した。未初期化時は解除処理をスキップし…

**「未初期化なら解除をスキップする」が正しい、とフレームワーク自身が結論を出している。**
その実装が [ServiceLocateComponent.cs:50](../../Assets/SymphonyFrameWork/Runtime/Component/ServiceLocateComponent.cs#L50) の
`if (!ServiceLocator.IsInitialized) { return; }` で、`internal` メンバーを使っているため利用側は同じ手が打てない。

## 設計

**`EnsureInitialized()` の適用範囲を、操作の性質で2つに分ける。**

| 分類 | 未初期化のときの扱い | 根拠 |
| --- | --- | --- |
| 解除・照会 | **「何も登録されていない」として戻り値で表現する** | 未登録と区別する意味が無い。`false` / `null` / 空一覧はいずれも既存の戻り値表現の範囲内 |
| 登録・必須取得・待機 | **`SymphonyNotInitializedException` のまま** | 黙って落とすと不具合を隠す、または永久に発火しない |

戻り値で表現するもの（12メソッド）:

- `UnregisterInstance<T>(T)` / `UnregisterInstance(Type)` / `UnregisterInstance<T>()` → `false`
- `DestroyInstance<T>(T)` / `DestroyInstance<T>()` → `false`
- `IsExistInstance<T>()` / `IsExistInstance<T>(T)` / `IsExistInstance(Type)` → `false`
- `GetInstance<T>()` → `null`
- `TryGetInstance<T>(out T)` → `false`、`result` は `default`
- `GetRegistrationInfos()` → 空一覧
- `TryGetRegistrationInfo(Type, out)` → `false`、`registrationInfo` は `default`

例外のまま残すもの（9メソッド）:

- `RegisterInstance` ×2 / `RegisterInstanceWithAutoDispose` ×2 — **状態を足す操作。** 未初期化のまま成功したことにすると、登録したつもりの依存が存在しない状態で先へ進む
- `GetRequiredInstance<T>()` — 「必須」がこのAPIの契約。厳格に取りたい呼び出し側の逃げ道として残す
- `GetInstanceAsync` / `TryGetInstanceAsync` / `RegisterAfterLocate` ×2 — 未初期化では登録待機が**永久に発火しない**。黙って待たせるのは例外より悪い

### 解除系だけでなく照会系も含める理由

[ServiceLocateComponent.cs:53](../../Assets/SymphonyFrameWork/Runtime/Component/ServiceLocateComponent.cs#L53) 自身が
`IsExistInstance` → `UnregisterInstance` の順で呼んでいる。**解除系だけ直すと、そのパターンを真似た利用側は
1つ手前の `IsExistInstance` で同じ例外を踏む。** 「フレームワークは自分の Component だけを救っている」という
問題が、そのまま隣へずれるだけになる。

`GetInstance` / `TryGetInstance` を含めるのは、破棄処理で
「取れたら購読解除する」形（`if (TryGetInstance(out var x)) x.Unsubscribe(this);`）が同じ経路にあるため。
`GetInstance<T>()` のXMLドキュメントは既に「見つからない場合や破棄済みの場合はnull」と書いており、
未初期化を `null` に含めても記述と矛盾しない。

### `IsInitialized` を公開しない

利用側の全呼び出し元へ定型チェックを配ることになる。**フレームワーク側で1度解けば済む問題を、
利用側の作法へ押し出す形になるため採らない。** `IsInitialized` は `internal` のまま残す
（Editor Window と `SymphonyMcpTools` が使っており、不要にはならない）。

### 引数検証は未初期化判定より先に行う

`UnregisterInstance(Type)` と `TryGetRegistrationInfo(Type, out)` は `ArgumentNullException` を投げる。
**`null` 引数は状態によらず呼び出し側の誤りなので、未初期化の早期returnより前で検証する。**
現在は `EnsureInitialized()` が先にあるため順序が逆になっており、入れ替える。

既存テスト `TryGetRegistrationInfo_RegisteredMissingAndNullTypes_UsesTryContract` は
初期化済みで `null` を渡しているため、この入れ替えで期待値は変わらない。

### 空一覧は初期化済みと同じ具象型で返す

`GetRegistrationInfos()` の初期化済み経路は `ServiceLocateQuery.GetInfos()` が
`Array.AsReadOnly(...)` で `ReadOnlyCollection<T>` を返す。**未初期化の空一覧も同じ型に揃える。**

素の配列（`Array.Empty<T>()`）を返すと、`Count` は明示的インターフェース実装になるため
`IReadOnlyList<T>` 越しでは見えても、リフレクションやduck typingでは見えない。
**同じAPIが状態によって別の具象型を返すことになり、利用側から観測できる差になる。**
実装時に実際にこれを踏み、追加したテストが検出した（NUnit の `Has.Count` が
`Property Count was not found` で落ちた）。

毎回 `Array.AsReadOnly` を呼ばないよう、`private static readonly` の共有インスタンスを持つ。

### 警告ログを出さずに返す

`UnregisterInstance(Type)` の `Debug.LogWarning($"{type.Name}は登録されていません。")` と
`DestroyInstance<T>()` の同種の警告より**前**で返す。終了時に毎回コンソールへ警告が積まれるのでは、
例外が警告に変わっただけで解決にならない。

### `ServiceLocateComponent` の既存ガードは残す

[ServiceLocateComponent.cs:50](../../Assets/SymphonyFrameWork/Runtime/Component/ServiceLocateComponent.cs#L50) の
`IsInitialized` チェックはこの変更で不要になるが、**削除しない。** 2回の no-op 呼び出しを省く早期returnとして
正しく、削除は今回の目的と無関係な変更になる。

## 公開API

**シグネチャの追加・変更・削除は無い。** 変わるのは未初期化時の挙動だけである。

`public` の増加は無く、`IsInitialized` は `internal` のまま。DesignPhilosophy「公開範囲」に対する変更は無い。

## ファイル構成

| パス | 名前空間 | 変更 |
| --- | --- | --- |
| `Assets/SymphonyFrameWork/Runtime/System/ServiceLocator/ServiceLocator.cs` | `SymphonyFrameWork.System.ServiceLocate` | 変更（12メソッドの先頭のみ） |
| `Assets/SymphonyFrameWork/Tests/Editor/ServiceLocatorTeardownTests.cs` | `SymphonyFrameWork.Tests` | **新規** |
| `Assets/SymphonyFrameWork/Documentation~/AgentUsage.md` | — | 変更（1文追記） |

新規ファイルが1件あるため、`.meta` は Unity Editor に生成させる。

`ServiceLocateService` / `ServiceLocateRegistry` / `ServiceLocateQuery` には手を入れない。
判定はすべて Facade（Adaptor層の公開エントリポイント）に閉じる。

## 依存方向

Adaptor層の公開エントリポイント内の分岐追加のみ。`ServiceLocator` → `ServiceLocateService` / `ServiceLocateQuery` の
参照方向は変わらない。

## エラー処理

- 新しい例外型は追加しない。
- `SymphonyNotInitializedException` は登録・必須取得・待機の9メソッドで引き続き投げる。
- `ArgumentNullException` は未初期化でも投げる（上記「引数検証は未初期化判定より先」）。

## 影響範囲

- 公開APIのシグネチャとシリアライズ形式に変更なし。移行手順は不要。
- **未初期化時に例外を捕捉して分岐していたコードがある場合、その `catch` は動かなくなる。**
  戻り値を見る形へ書き換える必要がある。ただし未初期化を例外で判定する手段は文書化しておらず、
  `Documentation~/` にも「未初期化で例外を投げる」という契約の記述は無い。
- 初期化済みでの挙動は一切変わらない。

## テストの置き場と種別

EditMode。**新規ファイル** `Assets/SymphonyFrameWork/Tests/Editor/ServiceLocatorTeardownTests.cs`。

既存の `ServiceLocatorRegistrationInfoTests` は `[SetUp]` で `ServiceLocator.Initialize(host)` を呼ぶため、
未初期化状態を要求する本件のテストは同居させず別ファイルにする。

`Initialize` / `ResetRuntimeState` / `IsInitialized` は `internal` だが、`InternalsVisibleTo` により
テストアセンブリから呼べる（既存テストが同じ経路を使っている）。

`[SetUp]` と `[TearDown]` の両方で `ServiceLocator.ResetRuntimeState()` を呼び、**未初期化であることを
`Assert.That(ServiceLocator.IsInitialized, Is.False)` で確認してから**各テストの本題に入る。
`ResetRuntimeState` は全フィールドが `null` でも安全（`?.` で書かれている）。

| テスト名 | 検証内容 | どう書くか |
| --- | --- | --- |
| `UnregisterInstance_NotInitialized_ReturnsFalse` | 解除3種が例外にならず `false` | 3つのオーバーロードを順に呼び、すべて `Is.False`。`Assert.DoesNotThrow` ではなく、例外が出れば素通りできないので直接呼ぶ |
| `DestroyInstance_NotInitialized_ReturnsFalse` | 破棄2種が `false` | 同上 |
| `IsExistInstance_NotInitialized_ReturnsFalse` | 照会3種が `false` | 同上 |
| `GetInstance_NotInitialized_ReturnsNull` | `GetInstance<T>()` が `null`、`TryGetInstance` が `false` かつ `out` が `null` | 2つをまとめて確認する |
| `GetRegistrationInfos_NotInitialized_ReturnsEmpty` | 一覧が空、点検索が `false` | `Has.Count.EqualTo(0)` と `Is.False`、`out` は `Is.EqualTo(default(ServiceRegistrationInfo))` |
| `RegisterInstance_NotInitialized_Throws` | 登録系は例外のまま | `Assert.Throws<SymphonyNotInitializedException>` を `RegisterInstance` と `RegisterInstanceWithAutoDispose` に対して |
| `GetRequiredInstance_NotInitialized_Throws` | 必須取得と待機系は例外のまま | `GetRequiredInstance` / `GetInstanceAsync` / `RegisterAfterLocate` に対して `Assert.Throws<SymphonyNotInitializedException>` |
| `UnregisterInstance_NotInitializedWithNullType_ThrowsArgumentNullException` | 引数検証が未初期化判定より先 | `ServiceLocator.UnregisterInstance(null)` と `TryGetRegistrationInfo(null, out _)` に `Assert.Throws<ArgumentNullException>` |

**「初期化済みでの挙動が変わらないこと」は既存テストが担保する。** `ServiceLocatorRegistrationInfoTests` が
初期化済みの `GetRegistrationInfos` / `TryGetRegistrationInfo` を検証しており、今回の変更で落ちてはならない。

例外ログを伴うテストは無い。**`Debug.LogWarning` を出さずに返すことは自動では検証しない。**
Unity のテストフレームワークは想定外の警告でテストを落とさず、「ログが出ないこと」を表明する
アサーションが無いため。警告より前で `return` していることは差分レビューで確認し、
実機での確認は下記「動作確認手順」に含める。

`GetInstanceAsync` は `Awaitable` を返すが、`EnsureInitialized()` が同期部分にあるため
`Assert.Throws` で捕まえられる（`await` せずに呼び出しだけで投げる）。

## 動作確認手順

自動で確認する項目:

- `uloop-compile` がエラー0・警告0
- EditModeテスト全数成功（追加8件を含む）

人が操作する項目:

1. `OnEnable` で `ServiceLocator.RegisterInstance`、`OnDestroy` で `ServiceLocator.UnregisterInstance` を呼ぶ
   `MonoBehaviour` をシーンへ置く（`AgentUsage.md` が勧めている基本形そのもの）
2. Play Mode へ入り、**停止する**
   - `SymphonyNotInitializedException` が出ないこと
   - **`は登録されていません` の警告も出ないこと**
3. Domain Reload が無効のため、Play Mode の開始・終了を2回繰り返し、2回目も同じ結果になること
4. Play Mode 中に `UnregisterInstance` を呼び、従来どおり `true` が返り登録が消えること
   （初期化済みの挙動が変わっていないこと）

### 実測結果と、Play Modeで確認できなかったこと

`OnEnable` で登録し `OnDestroy` で解除する `MonoBehaviour` を、**(a) 通常のシーン上**と
**(b) `DontDestroyOnLoad` 上**の2通りで Play Mode 終了まで走らせた。どちらも
`OnDestroy unregistered=True`、つまり**解除時点で Locator がまだ初期化済みだった**。

**このワークスペースでは、報告された「Orchestrator の解放が先、`OnDestroy` が後」という順序を
再現できていない。** したがって Play Mode の実測が確認したのは次の2点にとどまる。

- 初期化済みの登録・解除が従来どおり動く（回帰していない）
- 終了時にエラーも `は登録されていません` の警告も出ない

**未初期化時の契約は EditMode テストが担保する。** 再現できないことは修正の要否を否定しない。
Unity の Play Mode 終了時の破棄順は DDOL オブジェクトとシーンオブジェクトの間で保証されず、
プロジェクトの構成によって前後する。**フレームワーク自身が 2.15.1 で同じ順序を踏んで
`ServiceLocateComponent` へガードを入れている**ことが、この順序が実際に起こる証拠である。

## バージョン判断

**パッチ（3.8.2 → 3.8.3）。** 公開APIのシグネチャ、シリアライズ形式、既定値の意味を変えない。
`### Fix` 単独の版として出す（`release_round.py preflight` の同居検査に従う）。

Roundは分割しない。変更は1メソッド群・3ファイルに収まり、単独で検証・リリースできる。

## この Round で触るバージョン関連ファイル

- `Assets/SymphonyFrameWork/package.json` の `version` → `3.8.3`
- `Assets/SymphonyFrameWork/CHANGELOG.md` に `## [3.8.3]` の見出しと `### Fix` を追加
- `Assets/SymphonyFrameWork/README.md` の「現在のバージョン」 → `3.8.3`

`AGENTS.md` のAPI早見表はシグネチャが変わらないため触らない。
`Documentation~/AgentUsage.md` は**バージョン関連ではなく内容の更新**として、
「解除は Orchestrator の解放後でも安全な no-op になる」ことを1文追記する。
