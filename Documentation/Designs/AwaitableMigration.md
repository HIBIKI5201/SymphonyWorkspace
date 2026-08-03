# Awaitable シグネチャ移行 — Phase 5 の全体計画と Round M1（Pause）

## Phase 5 を 3.0.0 系として進める理由

**公開APIの戻り値型を `Task` / `ValueTask` から `Awaitable` へ変えるため、追加による後方互換化ができない。**

C# は戻り値型だけが異なる多重定義を許さない。新しい名前を付ければ共存できるが、`SaveStore.LoadAsync<T>()` のように**すでに `Async` 接尾辞を使っている API では逃げ場が無い**。`LoadAwaitable` のような名前は利用側にとって意味を持たない。

したがって Phase 5 は破壊的変更として 3.0.0 系で行う。

### バージョンの積み上げ方

各 Round を個別にリリース可能な状態に保つため、**`3.0.0-preview.N` を積み上げ、Phase 6 の最後の Round で接尾辞を落として `3.0.0` にする。**

破壊的変更を1つの `develop` へ溜め込むと、途中の Round が単独でリリースできなくなる。prerelease 版なら、利用側が「壊れることを承知で先行して試す」ことができる。

| Round | バージョン | 内容 |
| --- | --- | --- |
| M1 | `3.0.0-preview.1` | Pause の待機API3件の Awaitable 移行（完了） |
| M1b | `3.0.0-preview.2` | **Pause の `async void` 2件を Awaitable へ**（本 Round） |
| M2 | `3.0.0-preview.3` | Save Data の Awaitable 移行 |
| M3 | `3.0.0-preview.4` | Scene Load の Awaitable 移行 |
| M4 | `3.0.0-preview.5` | Service Locate の Awaitable 移行 |
| M5 | `3.0.0-preview.6` | Debug HUD、Component、`IInitializeAsync` |
| N1〜 | `3.0.0-preview.N` | Phase 6（enum 改名、シム削除、`SceneLoadConfig`） |
| 最終 | `3.0.0` | 接尾辞を落とす |

## Awaitable を使うときの制約

移行の各 Round で必ず守る。**`Awaitable` は `Task` の置き換えではない。**

- **1回しか await できない。** 2回目は例外になる
- **保存・共有してはいけない。** 完了後にプールへ返却されるため、フィールドへ持つと別の待機と衝突する
- したがって「複数の呼び出し元へ同じ待機を返す」用途には使えない

### 内部で共有が必要な箇所は `Task` のまま残す

現行コードには、共有を前提とした箇所が2つある。

| 箇所 | 用途 | 扱い |
| --- | --- | --- |
| `SaveDataService._loadingTasks` | 進行中ロードの重複排除。複数の呼び出し元が同じ待機を受け取る | **`Task` のまま。** 公開APIは `SymphonyAwaitable.FromTask` で包んで返す |
| `IInitializeAsync.InitializeTask` | 初期化の完了状態を保持し、`IsDone` で参照する | Round M5 で扱う。保存が前提のため `Task` を維持する方向で検討する |

**「同期ブロック、保存された非同期値、共有された非同期値を全文検索で除去する」という当初計画は、この2箇所については採らない。** 除去すると重複ロードの排除（Round I1 で回帰テストを書いた不変条件）が壊れる。公開面だけ `Awaitable` にし、内部の共有は `Task` で行う。

---

# Round M1 — Pause の Awaitable 移行

## 目的

`PauseManager` の非同期APIを `Task` から `Awaitable` へ移行する。Phase 5 の最初の Round として、**移行の型と CHANGELOG の書き方を確立する**ことを兼ねる。

Pause を最初に選んだ理由:

- 対象が3メソッドと小さい
- 共有・保存された非同期値が無い
- Round J1 でレイヤー分割済みで、EditMode テスト34件がある
- 内部で既に `Awaitable.NextFrameAsync` を await しており、戻り値型を変えるだけで済む

## 前提の確認

| 前提 | 確認結果 |
| --- | --- |
| 対象メソッド | `PausableNextFrameAsync`、`PausableWaitForSecondAsync`、`PausableWaitUntil` の3つが `Task` を返す |
| 共有・保存 | 無い。いずれも呼び出しごとに完結する |
| 内部実装 | 既に `Awaitable.NextFrameAsync(token)` と `SymphonyAwaitable.WaitWhile` を await している |
| `async Awaitable` の可否 | 使える。`SymphonyAwaitable` が既に `public static async Awaitable` を宣言している |
| 利用箇所 | Sample の2箇所（`PausableWaitForSecondAsync`、`PausableNextFrameAsync`）。Runtime／Editor 内に呼び出し元は無い |
| 既存テスト | Pause の34件はいずれも同期APIの検証であり、非同期メソッドを呼んでいない |

## 変更内容

```csharp
public static Awaitable PausableNextFrameAsync(CancellationToken token = default);
public static Awaitable PausableWaitForSecondAsync(float time, CancellationToken token = default);
public static Awaitable PausableWaitUntil(Func<bool> action, CancellationToken token = default);
```

`async Task` を `async Awaitable` へ変える。**本体の処理は変更しない。**

引数、例外の種類と条件、キャンセルの扱いはいずれも変えない。

### `async void` の2メソッドは Round M1b で移行する

`PausableDestroy` と `PausableInvoke` は `async void` である。M1 の時点では「await されなかった例外が失われる」ことを理由に据え置いたが、**利用者の判断で移行することになった**（Round M1b）。

実装時に検討したところ、**構造を変えれば観測性はむしろ上がる**ことが分かった。詳細は下記 Round M1b。

### `IEnumerator` 版は変更しない

`PausableWaitForSecond(float)` は Unity Coroutine 用であり、`Awaitable` とは用途が違う。

## 公開API

上記3メソッドの戻り値型が変わる。**破壊的変更。**

利用側の移行は、`await` している箇所ではソース変更が**不要**である。`Awaitable` も `await` できるため、次のようなコードはそのまま動く。

```csharp
await PauseManager.PausableWaitForSecondAsync(1.0f, destroyCancellationToken);
```

移行が必要になるのは、**戻り値を `Task` として受けている場合**だけである。

```csharp
// 2.x
Task task = PauseManager.PausableWaitForSecondAsync(1.0f);
await Task.WhenAll(task, other);

// 3.0.0
await PauseManager.PausableWaitForSecondAsync(1.0f);
// 複数待機は SymphonyAwaitable.WhenAll、Task と混ぜる場合は SymphonyAwaitable.AsTask
```

CHANGELOG にはこの2つの例をそのまま載せる。

## エラー処理

変更しない。未初期化は `SymphonyNotInitializedException`、待機時間が負なら `ArgumentOutOfRangeException`、`action` が null なら `ArgumentNullException`、キャンセルは `OperationCanceledException`。

**キャンセル時の例外型が `Awaitable` でも同じであることを確認する**（動作確認手順）。`Task` と `Awaitable` でキャンセルの表現が異なると、利用側の `catch` が空振りする。

## 影響範囲

- 3メソッドの戻り値型が変わる。**`await` して使っている限りソース変更は不要**
- Sample 2箇所は `await` しているためソース変更不要。ただし記述の確認は行う
- `async void` の2メソッドと `IEnumerator` 版は変わらない
- 内部実装、例外、キャンセルの挙動は変わらない

## テストの置き場と種別

`Tests/Runtime/`（PlayMode）へ追加する。**フレーム進行を伴うため EditMode では検証できない。**

`PauseManager` は `SymphonyOrchestrator` が Play Mode で初期化するため、PlayMode テストなら初期化済みの状態で呼べる。

### `PauseAwaitableRuntimeTests`

- `PausableNextFrameAsync` を await するとフレームが進むこと（`Time.frameCount` の増加で確認）
- `PausableWaitForSecondAsync` が指定秒後に完了すること
- **ポーズ中は `PausableWaitForSecondAsync` が進まないこと**（ポーズ→数フレーム→解除→完了、で確認）
- キャンセル済みトークンを渡すと `OperationCanceledException` になること
- `PausableWaitUntil` が条件成立で完了すること

**戻り値が `Awaitable` であることは型として保証されるためテストしない。** 検証するのは、型を変えてもタイミングとキャンセルの契約が保たれていることである。

既存の EditMode 222件・PlayMode 4件も全数成功することを確認する。

## 動作確認手順

1. Unity Scene 検証ガードに従い、親と submodule の dirty 状態を記録する
2. `uloop-clear-console` 後に `uloop-compile` でエラー0・**SymphonyFrameWork 由来の警告0**
3. `uloop-clear-console` を挟んで EditMode と PlayMode の全テストを実行し、`Success` / `Passed` / `Failed` / `Skipped` を記録する。**同じ結果が2回続くことを確認する**
4. Pause Manager Sample を Play し、カウントダウンとフレームカウンタがポーズ中に止まることを確認する
5. **キャンセル時の例外型が `OperationCanceledException` のままであることを確認する**
6. Console の Error / Exception が0件であることを確認する
7. この Round で追加・変更した `.cs` に UTF-8 BOM が付いていることを確認する

## バージョン判断

**`3.0.0-preview.1`。** 公開APIの戻り値型が変わる破壊的変更である。3.0.0 の一部として積み上げる。

CHANGELOG は `## [3.0.0-preview.1]` の見出しに `### Breaking` を置き、移行方法を明記する。

## この Round で触るバージョン関連ファイル

| ファイル | 変更 |
| --- | --- |
| `Assets/SymphonyFrameWork/package.json` | `2.20.0` → `3.0.0-preview.1` |
| `Assets/SymphonyFrameWork/CHANGELOG.md` | `3.0.0-preview.1` へ Breaking と移行方法を記録 |
| `Assets/SymphonyFrameWork/README.md` | 現在のバージョンと Pause の例 |
| `Assets/SymphonyFrameWork/Documentation~/AgentUsage.md` | Pause の非同期APIの記載 |

## ブランチ

`develop` から `feature/pause-awaitable` を作成する。

---

# Round M1b — Pause の `async void` を Awaitable へ

## 目的

`PausableDestroy` と `PausableInvoke` を `async void` から `Awaitable` を返す形へ変更する。呼び出し側が完了を待機できるようにする。

## 観測性は下がらない — 構造を変えるため

M1 では「await されなかった例外が失われる」ことを懸念して据え置いた。**引数検証を同期的に行う構造にすれば、むしろ観測性は上がる。**

現在の `async void` では、`ArgumentNullException` や `SymphonyNotInitializedException` も**非同期メソッドの中で投げられる**。呼び出し側の `try/catch` では捕まえられず、Unity の未処理例外ハンドラへ流れる。

検証を同期部分に出し、待機と本処理だけを private な `async Awaitable` へ分ける。

```csharp
public static Awaitable PausableDestroy(GameObject obj, float t, CancellationToken token = default)
{
    EnsureInitialized();

    if (obj == null)
    {
        throw new ArgumentNullException(nameof(obj));
    }

    ValidateDuration(t, nameof(t));

    return DestroyAfterDelayAsync(obj, t, token);
}

private static async Awaitable DestroyAfterDelayAsync(
    GameObject obj, float durationSeconds, CancellationToken token)
{
    await PausableWaitForSecondAsync(durationSeconds, token);
    Object.Destroy(obj);
}
```

これにより:

- **引数と初期化の誤りは呼び出し元へ同期的に伝わる。** `async void` では不可能だった
- 待機中に起きうる例外は `OperationCanceledException` だけであり、これは待機を中断したときの想定内の結果である

### 残る差分

`Awaitable` を戻り値にすると、**await されなかった場合にプールへ返却されない。** Unity の `Awaitable` は完了の観測時にプールへ戻るため、無視された分は通常の GC 対象になる。

無制限に溜まるものではなく、プールの効果が失われて通常のアロケーションに戻るだけである。fire-and-forget として使う API であり、この代償は受け入れる。**CHANGELOG へ明記する。**

## 呼び出し側への影響

**文として呼んでいる限りソース変更は不要。** 戻り値のない呼び出しは `Awaitable` を返す形でもそのまま書ける。

```csharp
// 2.x でも 3.0.0 でもそのまま動く
PauseManager.PausableDestroy(gameObject, 1.0f);

// 3.0.0 では待機もできる
await PauseManager.PausableDestroy(gameObject, 1.0f);
```

## テスト

`Tests/Runtime/PauseAwaitableRuntimeTests.cs` へ追加する。

- `PausableInvoke` を await すると、指定秒後に処理が呼ばれていること
- **await せずに呼んでも処理が実行されること**（fire-and-forget の契約）
- **引数が不正な場合、同期的に例外が投げられること**（`Assert.Throws` で捕まえられる＝呼び出し元へ届く）
- `PausableDestroy` が指定秒後に GameObject を破棄すること

`PausableDestroy` の検証には `new GameObject()` を作って破棄を確認する。PlayMode 内で完結し、Scene を保存しないため検証ガードに抵触しない。

## バージョン判断

**`3.0.0-preview.2`。** 公開APIのシグネチャが変わる破壊的変更である。
