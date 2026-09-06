# SceneBlockServiceLoadUnloadResilience

Issue: [#202](https://github.com/HIBIKI5201/SymphonyFramework/issues/202) 再オープン分（[P1] 2件）

## 目的

`SceneBlockService`（#204で追加）に、非同期の重複要求と失敗後の状態管理に関する2件の不具合がある。

1. **同じブロックへのロード要求が、ロード未完了でも成功する。** `LoadBlock` は `_registry.TryGet` でEntityの存在だけを見て、実ロード中でも即 `true` を返す（[SceneBlockService.cs L51-56](../../Assets/SymphonyFrameWork/Runtime/Service/SceneBlock/Internal/Application/SceneBlockService.cs)）。1回目が完了する前に2回目が「成功」を返すため、呼び出し側がまだ存在しないシーンを使い得る。失敗後に`Loading`のまま止まった場合も同じ分岐に入り、再試行が機能しない。
2. **アンロード失敗後、同じAPIで再試行できない。** `UnloadBlock` は保持を実処理より先に解放し、`UnloadScenesAsync` が `false` を返しても最後に無条件で `_registry.Remove(blockName)` を呼ぶ（同ファイル L163-197）。シーンが残った状態で再度呼んでも「未追跡」として `true` を返すだけで、残存シーンを永久に処理できなくなる。

対応するのは #202 再オープン分の[P1] 2件のみ。両方とも同じ`SceneBlockService`の同時実行制御に関わるため、1つのRoundにまとめる。

## 実行モデルの前提（確認済み）

Unity側で `SceneBlockLoader.LoadAsync`/`UnloadAsync` は `Awaitable`/`Task` を返すのみで、`SceneBlockService` 自体はロックや`SynchronizationContext`切り替えを持たない。C#の`async`/`await`は、最初の`await`に到達するまで呼び出し元のスレッド上で同期的に実行される。したがって「同じブロックへの2回目の要求」は物理的な別スレッドからの競合ではなく、**1回目の`await`（実ロード中）で制御が戻った後、同じスレッドから2回目が呼ばれる**という論理的な競合である。ロックは不要で、`Task`の共有だけで解決できる。Issueの再現手順（Fake loaderのTaskを`TaskCompletionSource<bool>`で保留する）もこの前提に合致する。

## 公開API

変更なし。`SceneBlockLoader.LoadAsync` / `UnloadAsync` のシグネチャ・戻り値は変わらない。

XMLドキュメントの `<exception cref="InvalidOperationException">` へ、既存の「同名の別アセット」に加えて次の条件を追記する。

- `LoadAsync`: 同じブロックがアンロード中の場合
- `UnloadAsync`: 同じブロックがロード中の場合

## ファイル構成

- `Runtime/Service/SceneBlock/Internal/Application/SceneBlockService.cs`（変更）: `LoadBlock` / `UnloadBlock` の制御フローを変更し、`RunLoad` / `ExecuteLoad` / `RunUnload` / `ExecuteUnload` / `WaitShared` を追加
- `Runtime/Service/SceneBlock/SceneBlockLoader.cs`（変更）: `LoadAsync` / `UnloadAsync` のXMLドキュメントへ新しい`InvalidOperationException`条件を追記
- `Tests/Editor/SceneBlockServiceTests.cs`（変更）: 新しいテストケースと、`FakeBlockSceneLoader`への保留・アンロード失敗機能の追加
- `Documentation~/Modules/SceneBlock.md`（変更）: 87-88行目の記述を新しい契約に合わせて更新

依存方向・レイヤーへの変更はない。`SceneBlockService`はApplication層のまま、`Task`の共有もUnity APIへ触れない範囲で完結する。

## 修正方針

### 1. Load: 状態で分岐し、実行中は共有、失敗後は同じEntityで再試行する

`LoadBlock`冒頭の「追跡済みなら即成功」という分岐を書き換える。**ゲートの判定はEntityの`State`ではなく、`_pendingLoads`/`_pendingUnloads`に実際に実行中のTaskがあるかどうかで行う。**

`State`だけでは「実行中」と「失敗して止まったまま」を区別できない。`BeginLoading()`後、失敗しても`State`は`Loading`のままで、`CompleteLoading()`を呼ぶまで変わらない（[SceneBlockLoadEntity.cs](../../Assets/SymphonyFrameWork/Runtime/Service/SceneBlock/Internal/Domain/SceneBlockLoadEntity.cs)）。`Unloading`も同様に、失敗後は`Remove`されるまで残り続ける（今回の2番目の修正後は特に、失敗時は`Remove`しなくなるため長く残る）。**`tracked.State == Unloading`でUnloadを拒否すると、失敗して止まったUnloadの後始末としてもう一度`LoadAsync`を呼ぶ経路（既存の`### 実装時の注意`が想定する「失敗後は`UnloadAsync`で片付ける」の逆方向）まで塞いでしまう。** これは実装時にワーカーが指摘し、判明した（[前提不一致の報告](#実施レポート参照)）。正しくは「今まさに実行中かどうか」だけを見る。

```csharp
if (_registry.TryGet(blockName, out SceneBlockLoadEntity tracked))
{
    EnsureSameAsset(tracked, blockName, assetInstanceId);

    // 今まさにUnloadが動いているときだけ拒否する。Unloadが失敗して止まっているだけなら、
    // Loadで後始末を再開してよい（実処理は実際のロード済み状態を見て安全に振る舞う）。
    if (_pendingUnloads.ContainsKey(blockName))
    {
        throw new InvalidOperationException(
            $"Scene Block {blockName} はアンロード中のためロードできません。アンロード完了後に呼び出してください。");
    }

    // 今まさにLoadが動いていれば、それを共有して待つ。
    if (_pendingLoads.TryGetValue(blockName, out Task<bool> inFlightLoad))
    {
        return await WaitShared(inFlightLoad, token);
    }

    // 実行中の何かが無ければ、既に完了しているかを見る。
    if (tracked.State == SceneBlockLoadStateEnum.Complete)
    {
        progress?.Report(1f);
        return true;
    }

    // Complete以外（Loadingで止まった失敗、またはUnloadingで止まった失敗）は、
    // 同じEntityで再試行する。層ループは実際のロード済み状態だけを見るため、
    // どちらの理由で止まっていても安全に再開できる。
    return await RunLoad(tracked, progress, token);
}

// 新規登録は現状どおり（依存グラフの解決、Entity生成、Register、BeginLoading）。
...
return await RunLoad(entity, progress, token);
```

`RunLoad`が実処理（`ExecuteLoad`。既存の層ループをそのまま移すだけ）を`_pendingLoads`辞書へ登録し、完了時に自分が登録した参照と一致する場合だけ取り除く。

```csharp
private readonly Dictionary<string, Task<bool>> _pendingLoads = new(StringComparer.Ordinal);

private async Task<bool> RunLoad(SceneBlockLoadEntity entity, IProgress<float> progress, CancellationToken token)
{
    Task<bool> executing = ExecuteLoad(entity, progress, token);
    _pendingLoads[entity.BlockName] = executing;
    try
    {
        return await executing;
    }
    finally
    {
        if (_pendingLoads.TryGetValue(entity.BlockName, out Task<bool> current) && current == executing)
        {
            _pendingLoads.Remove(entity.BlockName);
        }
    }
}
```

`ExecuteLoad`は既存の層ループ（`entity.Layers`を使うよう`entity`経由に統一するだけ）で、`entity.BeginLoading()`は呼ばない（新規登録側だけが呼ぶ。再試行で呼び直すと`Progress`が0へ戻り、既にロード済みの層の進捗表示が一瞬後退するため）。層ループ自体は「ロード済みのシーンは再要求しない」という既存条件（L91）により、失敗した層だけを自然に再試行する。追加のresume状態は持たない。

**失敗後は再試行または明示的な拒否として扱い、成功を返さない**という受け入れ条件は、この分岐そのもの（`Complete`でなければ即成功を返さない）で満たす。「明示的な拒否」ではなく「再試行」を選ぶ。理由: 既存の`### 実装時の注意`に「ロードに失敗しても、ロード済みのシーンは巻き戻さない。`UnloadAsync`を呼べば片付く」とあるが、`LoadAsync`を再度呼んだときに何が起きるかは未定義だった。同じAPIをもう一度呼べば続きから進む方が、利用側の再試行実装（「失敗したらもう一度呼ぶ」）を素直に許容できる。

### 2. Unload: 失敗時はRegistryから消さず、実状態から再試行できるようにする

`UnloadBlock`も同じ形へ揃える。

```csharp
if (!_registry.TryGet(blockName, out SceneBlockLoadEntity entity))
{
    progress?.Report(1f);
    return true;
}

EnsureSameAsset(entity, blockName, assetInstanceId);

// LoadBlockと対称に、ゲートは「今まさに実行中か」だけで判定する（Stateでは判定しない。理由は上のLoad側と同じ）。
if (_pendingLoads.ContainsKey(blockName))
{
    throw new InvalidOperationException(
        $"Scene Block {blockName} はロード中のためアンロードできません。ロード完了後に呼び出してください。");
}

if (_pendingUnloads.TryGetValue(blockName, out Task<bool> inFlightUnload))
{
    return await WaitShared(inFlightUnload, token);
}

return await RunUnload(entity, progress, token);
```

`RunUnload`は`RunLoad`と対になる形（`_pendingUnloads`辞書、同じfinallyパターン）。`ExecuteUnload`は既存のループを移し、**`_registry.Remove(entity.BlockName)`を呼ぶのは`isAllUnloaded`が`true`のときだけ**にする。これが本体の修正。

```csharp
private async Task<bool> ExecuteUnload(SceneBlockLoadEntity entity, IProgress<float> progress, CancellationToken token)
{
    entity.BeginUnloading();
    NotifyStateChanged();

    bool isAllUnloaded = true;
    int processedSceneCount = 0;
    string blockName = entity.BlockName;

    for (int layerIndex = entity.Layers.Count - 1; layerIndex >= 0; layerIndex--)
    {
        IReadOnlyList<string> layer = entity.Layers[layerIndex];
        List<string> unloadTargets = new(layer.Count);

        foreach (string sceneName in layer)
        {
            // 既存どおり: hasNoHolder / IsExternallyHeld / IsPersistent / IsSceneLoaded の判定はそのまま。
            ...
            unloadTargets.Add(sceneName);
        }

        if (unloadTargets.Count > 0)
        {
            ...
            try
            {
                if (!await _loader.UnloadScenesAsync(unloadTargets, layerProgress, token))
                {
                    isAllUnloaded = false;
                }
            }
            finally
            {
                // 実際にはまだロードされたままのシーンだけ、保持をこのブロックへ戻す。
                // 成功・失敗・キャンセルのどの終わり方でも、実状態と保持情報を一致させるために必ず行う。
                RestoreHolderForStillLoadedScenes(unloadTargets, blockName);
            }
        }

        processedSceneCount += layer.Count;
        ReportBlockProgress(entity, progress, processedSceneCount, entity.SceneCount);
    }

    if (isAllUnloaded)
    {
        _registry.Remove(blockName);
    }

    progress?.Report(1f);
    NotifyStateChanged();
    return isAllUnloaded;
}

/// <summary>
///     アンロードを試みた後も実際にロードされたままのシーンへ、保持をこのブロックへ戻す。
/// </summary>
/// <remarks>
///     アンロード対象は実処理の前に保持を解いている（他のブロックの保持有無を判定する必要があるため）。
///     解いたまま実処理が失敗・キャンセルすると、まだロードされているのに保持者が誰も居ない状態になり、
///     次のLoadBlockがそのシーンを「ブロック外でロードされたシーン」と誤認して<c>MarkExternallyHeld</c>してしまう。
/// </remarks>
private void RestoreHolderForStillLoadedScenes(IReadOnlyList<string> sceneNames, string blockName)
{
    foreach (string sceneName in sceneNames)
    {
        if (_loader.IsSceneLoaded(sceneName))
        {
            _registry.AddHolder(sceneName, blockName);
        }
    }
}
```

**この`RestoreHolderForStillLoadedScenes`が、実装前の照合でワーカーが2回目に指摘した所有権破損への対応である。** 経緯: 1回目の照合で「`State`だけで逆方向を拒否すると、失敗停止後の後始末を塞ぐ」という不一致が見つかり、対称的な再開（失敗停止中なら逆方向のAPIでも再開してよい）へ設計を訂正した。ところがUnload失敗後にLoadを再開する経路を検証したところ、Unloadループが**実処理の前に**保持を解放しているため、失敗で残ったシーンの保持者が消えたままになり、再開したLoadループがそのシーンを「ブロック外でロードされた」と誤認して`MarkExternallyHeld`し、以後どのブロックのUnloadでも二度と片付かなくなることが分かった。`finally`で「まだロードされているシーンだけ保持を戻す」ことで、保持情報を常に実状態（`_loader.IsSceneLoaded`）と一致させる。

再試行時に何が起きるかをここで確認しておく（コードを読んで裏取り済み）。

- **保持（Holder）**: `_registry.ReleaseHolder`は1度解いた後にもう一度呼んでも安全（`_sceneHolders`にキーが無ければ即`true`を返すだけ）。前回の試行で既に解いた保持を再度解こうとしても副作用は無い。`RestoreHolderForStillLoadedScenes`で戻した保持は、次の`UnloadBlock`呼び出しで通常どおりもう一度解かれる
- **未アンロードのシーン**: 失敗またはキャンセルで残ったシーンは`_loader.IsSceneLoaded(sceneName)`が`true`のままなので、再試行時のループが同じ層で再び`unloadTargets`へ拾う。保持が正しく復元されているため、`IsHeldByAnyBlock`の判定も実状態と食い違わない。これで「レイヤー単位・同一レイヤー内の部分成功でも実状態と保持情報が一致する」「キャンセル後の保持情報も実状態と整合する」を満たす
- **キャンセル**: `_loader.UnloadScenesAsync`が`OperationCanceledException`を投げた場合、`try/finally`の`finally`で保持を復元してから、例外は捕捉せずそのまま伝播させる（`LoadBlock`の既存のキャンセル契約と揃える）。`isAllUnloaded`判定に到達しないため`_registry.Remove`は呼ばれず、Entityは追跡されたまま残る
- **共有保持・外部保持・永続シーンの保護**: `unloadTargets`に入るのはこれらの判定を通過したシーンだけなので、`RestoreHolderForStillLoadedScenes`は永続シーンや既に外部保持済みのシーンには影響しない（対象に入らないため）

### 3. 共有待機のキャンセル契約

2人目以降の呼び出しは「自分の`CancellationToken`」と「他人が実行中のTask」の両方を持つ。自分のトークンだけを対象にキャンセルできるようにし、実処理そのもの（1人目のToken）は止めない。

```csharp
private static async Task<bool> WaitShared(Task<bool> inFlight, CancellationToken token)
{
    if (!token.CanBeCanceled) { return await inFlight; }

    TaskCompletionSource<bool> cancellationSignal = new();
    using (token.Register(() => cancellationSignal.TrySetCanceled(token)))
    {
        Task completed = await Task.WhenAny(inFlight, cancellationSignal.Task);
        if (completed == cancellationSignal.Task) { await cancellationSignal.Task; }
        return await inFlight;
    }
}
```

- 2人目のTokenが先にキャンセルされると、2人目の呼び出しだけが`OperationCanceledException`を受け取る。1人目の実処理と`_pendingLoads`/`_pendingUnloads`の登録はそのまま残り、1人目が完了すれば通常どおり片付く
- 2人目のTokenがキャンセルされなければ、1人目の結果（成功・失敗どちらも）がそのまま2人目へ返る。**これが「共有したロードの失敗結果が両呼び出しへ届く」を満たす**

### 4. LoadとUnloadの重複要求の契約（決定）

| 状況 | 挙動 |
| --- | --- |
| Loadが実行中にLoad | 実行中のTaskを共有して待つ（同じ結果を返す） |
| Loadが失敗して停止中にLoad | 同じEntityで再試行する |
| Unloadが実行中にUnload | 実行中のTaskを共有して待つ |
| Unloadが失敗して停止中にUnload | 同じEntityで再試行する |
| **Loadが実行中にUnload** | **`InvalidOperationException`** |
| **Unloadが実行中にLoad** | **`InvalidOperationException`** |
| Unloadが失敗して停止中にLoad | 同じEntityでLoadを実行する（後始末としてのLoad呼び出しを許可する） |
| Loadが失敗して停止中にUnload | 同じEntityでUnloadを実行する（既存の「失敗後はUnloadAsyncで片付ける」契約） |

**「実行中」は`_pendingLoads`/`_pendingUnloads`に実際にTaskが登録されているかどうかで判定し、`State`では判定しない。** `State`は失敗後も`Loading`/`Unloading`のまま残るため、`State`だけで拒否すると「失敗して止まったあと、逆方向のAPIで後始末する」という既存の契約（Load失敗後はUnloadで片付ける。今回追加するUnload失敗後はLoadで再開できる）まで塞いでしまう。この点は実装時にワーカーが指摘し、設計を訂正した（下記「実施レポート」参照）。

逆方向の**実行中の**重複要求だけを待機や自動判定にせず例外にするのは、Issueが要求している範囲（同方向の重複排除と失敗後の再試行）を超えて、ロードとアンロードが**同時に**同じシーン集合を取り合う経路まで設計すると、保持の解放順序と実行順序の整合を新たに設計する必要が生じ、Roundの範囲を大きく超えるため。利用側が想定する通常の使い方（「次をロード→前をアンロード」を順に呼ぶ。`Documentation~/Modules/SceneBlock.md`に既存の注意書きあり）では、前の呼び出しが完了してから次を呼ぶため、この例外に当たらない。

## エラー処理

- `InvalidOperationException`: 同名の別アセット（既存）、Load中のUnload、Unload中のLoad（今回追加）。いずれも呼び出し側の使い方の誤りであり、状態を変更する前に検証する
- `OperationCanceledException`: 既存どおり、実処理のTokenがキャンセルされた場合にそのまま伝播する。共有待機用の`WaitShared`は、共有側のTokenがキャンセルされた場合にも同じ例外を投げる

## 影響範囲

- 公開APIのシグネチャ・戻り値は変更しない
- **`LoadAsync`/`UnloadAsync`の新しい例外条件**（Load中のUnload、Unload中のLoad）は、`SceneBlockLoader.cs`のXMLドキュメントと`Documentation~/Modules/SceneBlock.md`を更新する
- **「同じアセットの再ロードは何もせず成功します」の記述**（`SceneBlock.md` 88行目）を、「`Complete`状態でのみ即成功、`Loading`で止まっている場合は再試行する」という新しい契約に書き換える
- 既存の公開シリアライズ形式への影響なし

## テストの置き場と種別

`Tests/Editor/SceneBlockServiceTests.cs`（EditMode、既存ファイルへ追加）。`FakeBlockSceneLoader`を拡張し、Unity APIへ触れない範囲で非同期の競合を再現する。

### FakeBlockSceneLoaderの拡張

- `internal TaskCompletionSource<bool> HoldLoadOn;` — 設定されている場合、対象シーンを含む層の`LoadScenesAsync`は即完了せず、このTCSの`Task`を返す。テストが`SetResult`/`SetException`/`SetCanceled`を呼ぶまで保留する
- `internal TaskCompletionSource<bool> HoldUnloadOn;` — 同上、`UnloadScenesAsync`向け
- `internal string UnloadFailOnScene;` — このシーンを含む層の`UnloadScenesAsync`は`false`を返し、`LoadedScenes`から取り除かない（アンロード失敗を模す）

### 追加するテスト

各テストは「何を検証するか」と「どう書くか」を1行で対応させる。

- `LoadBlock_ConcurrentSameBlock_SharesSingleLoadCall`: `HoldLoadOn`でPropsの層を保留した状態で`LoadBlock("Town", ...)`を2回、両方awaitせずに呼ぶ（`Task<bool> task1 = ...; Task<bool> task2 = ...;`）。保留を`SetResult(true)`で解放後、両方を`await`して`true`を返すこと、`_loader.LoadCalls`にPropsの層が1回しか無いことを確認する
- `LoadBlock_ConcurrentSameBlock_SharesFailureResult`: 同様に保留し、`SetResult(false)`で解放する。両方の呼び出し結果が`false`になることを確認する
- `LoadBlock_RetryAfterFailure_OnlyReloadsFailedLayer`: `FailOnScene = "Props"`で1回目を失敗させた後、`FailOnScene`を解除して同じブロックへ`LoadBlock`を再度呼ぶ。結果が`true`になり、`_loader.LoadCalls`の2回目の呼び出しに`Base`が含まれない（Baseは既にロード済みのため再要求しない）ことを確認する
- `LoadBlock_WhileUnloading_ThrowsInvalidOperation`: `HoldUnloadOn`でアンロードを保留した状態で`UnloadBlock`を呼び出し中（await前）に、同じブロック名で`LoadBlock`を呼ぶと`InvalidOperationException`を投げることを確認する
- `UnloadBlock_ConcurrentSameBlock_SharesSingleUnloadCall`: `HoldUnloadOn`で保留した状態で`UnloadBlock`を2回、両方awaitせずに呼ぶ。解放後、両方が同じ結果を返し、`_loader.UnloadCalls`が1回だけであることを確認する
- `UnloadBlock_WhileLoading_ThrowsInvalidOperation`: `HoldLoadOn`でロードを保留した状態で`LoadBlock`を呼び出し中（await前）に、同じブロック名で`UnloadBlock`を呼ぶと`InvalidOperationException`を投げることを確認する
- `UnloadBlock_PartialFailure_KeepsTrackingForRetry`: `UnloadFailOnScene = "Props"`で1回目を失敗させる（`Base`は成功、`Props`は失敗して残る）。1回目の戻り値が`false`であることと、`SceneBlockLoader`相当の`_registry.TryGet("Town", out _)`がまだ`true`であることを確認する
- `UnloadBlock_RetryAfterPartialFailure_UnloadsRemainingScene`: 上のテストに続けて`UnloadFailOnScene`を解除し、同じブロックへ`UnloadBlock`を再度呼ぶ。結果が`true`になり、`_loader.LoadedScenes`から`Props`が取り除かれ、`_registry.TryGet("Town", out _)`が`false`（追跡から外れる）になることを確認する
- `LoadBlock_AfterUnloadFailure_ResumesWithoutThrowing`: `UnloadFailOnScene = "Props"`で`UnloadBlock`を1回失敗させて止める（`Props`が残る。Entityは`_registry`に残り、`_pendingUnloads`には何も無い）。その状態で同じブロックへ`LoadBlock`を呼ぶと、`InvalidOperationException`を投げずに`true`を返すことを確認する。**`State == Unloading`のまま`_pendingUnloads`が空、という「失敗して停止中」を正しく再開できることの検証** — これは実装前の照合で見つかった設計上の見落とし（Stateだけで拒否すると誤って塞いでしまうケース）を直接カバーする
- `UnloadBlock_AfterFailedUnloadAndReload_StillUnloadsRemainingScene`: 上のテストに続けて、Load再開後にもう一度`UnloadBlock`を呼ぶ。`Props`が`_loader.UnloadCalls`へ再び現れ、`_loader.LoadedScenes`から取り除かれることを確認する。**`RestoreHolderForStillLoadedScenes`が保持を正しく戻し、Load再開時に`Props`を「ブロック外でロードされた」と誤認して`MarkExternallyHeld`しないことの検証** — 実装前の照合の2回目でワーカーが指摘した所有権破損（保持を戻さないと、外部保持と誤認されて二度とアンロードできなくなる）を直接カバーする。合わせて`_registry`の`IsExternallyHeld("Props")`が`false`のままであることも確認する
- `LoadBlock_WhileLoading_SecondCallerCancelToken_ThrowsOperationCanceled_FirstContinues`: `HoldLoadOn`で保留した状態で1回目を呼び、2回目を「既にキャンセル済みの`CancellationToken`」で呼ぶ。2回目が`OperationCanceledException`を投げること、その後1回目を`SetResult(true)`で解放すると1回目は`true`で完了することを確認する

**`LoadBlock_SameAssetTwice_ReturnsTrueWithoutReload`と`LoadBlock_SceneLoadFails_ReturnsFalseAndKeepsHeldScenes`（既存）はそのまま通ることを確認する。** 前者は`Complete`状態の即成功パス、後者は「ロード失敗後に`UnloadBlock`で片付ける」という既存契約で、どちらも変えていない。

## 動作確認手順

Unity Editor上で以下を確認する（自動検証の対象外、人による確認）。

1. `SceneBlockAsset`を1つ用意し、Play Modeで`SceneBlockLoader.LoadAsync`を呼ぶ最中（awaitが返る前）に同じアセットへもう一度`LoadAsync`を呼ぶ疑似コードをTemporaryスクリプトで実行し、例外なく両方が完了することを確認する
2. Symphony Administrator の Scene Block パネルで、ロード中のブロックの状態表示が`Loading`のまま二重に増えないことを確認する
3. 既存のScene Block Sampleで、通常のロード→アンロードが従来どおり動作することを確認する（回帰確認）

## バージョン判断

パッチ（実装のみ・不具合修正。例外の追加条件はあるが、新しい公開APIの追加ではなく、既存の公開APIが既存の例外型を投げる条件を追加するだけであり、後方互換）。

## この Round で触るバージョン関連ファイル

- `package.json` の `version`: パッチを1つ上げる
- `CHANGELOG.md`: `### Fix` 見出しへ本件を追記
