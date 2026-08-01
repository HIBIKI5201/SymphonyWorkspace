# SymphonyTaskEcsTimer

## 目的

大量の時間待機を1つのECSワールドへ集約し、タイマー更新だけをJob SystemとBurstで並列処理する。利用側には、通常の`await`と同じ形で1回だけ待機できる軽量な値型を提供する。

```csharp
await SymphonyTask.DelayEcsAsync(1.5f, destroyCancellationToken);
```

現行の`Awaitable.NextFrameAsync`による待機を一律に置き換える機能ではない。数千件以上の同時タイマーでECS化の効果が計測できる用途を対象とし、少数の待機には既存の`Awaitable.WaitForSecondsAsync`と`SymphonyAwaitable`を引き続き使用する。

添付案にある「何回awaitしても完全アロケーションフリー」「数万件を常に1ms未満」は、利用側のasyncステートマシン、キャンセル登録、Pool拡張、実行環境まで含めると保証できない。この設計では次を計測可能な受け入れ条件へ置き換える。

- PoolとNative Containerのウォームアップ後、タイマー更新だけを行うフレームのManaged GC Allocが0 Bである。
- Timer作成の第2波では、`CancellationToken.None`かつPool容量内ならフレームのManaged GC Allocが0 Bである。
- 10,000件のアクティブタイマーを更新する処理が、基準環境で中央値1 ms未満かつ95パーセンタイル1 ms未満である。
- 上記は基準環境における合格条件であり、すべての端末に対する公開APIの性能保証にはしない。

## 実装前に確定した前提

| 前提 | 確認結果と設計への反映 |
| --- | --- |
| `SymphonyTask`という型名を新設できる | 現行2.11.0には同じ完全修飾名の`public static class SymphonyTask`が存在し、3.0.0で削除予定である。2.xでは同名の値型を追加できないため、本機能は3.0.0完了後の3.1.0を対象とする |
| 新しい独自awaitableを公開できる | 現行規約は公開非同期型を`Awaitable`へ統一している。本機能は計測で優位性が確認できた場合だけ、タイマー専用の例外として追加する。一般的なasync戻り値の置換にはしない |
| ECS依存は既にパッケージに含まれる | ホストプロジェクトにはEntities 1.4.8、Burst 1.8.29、Collections 2.6.8があるが、配布パッケージの`package.json`とRuntime asmdefには依存が無い。両方へ明示的に追加する必要がある |
| Burst内からPause状態を読める | Burst対象コードからmanaged staticの`PauseController`を直接呼べない。Pause状態はCompositionがメインスレッドで読み、ECS更新を呼ぶかどうかの値として渡す |
| フレームごとの`NativeQueue`生成はallocation-freeである | `Allocator.TempJob`の生成と破棄はManaged GCでなくても毎フレームのNative allocationになる。QueueはSystemが`Allocator.Persistent`で所有し、`OnDestroy`で解放する |
| Jobを非同期のまま完了通知できる | managed continuationを同じフレームで再開するにはJob完了を待つ必要がある。SystemはJobをscheduleした後にCompleteし、完了IDをメインスレッドで解決する。この同期点を性能計測へ含める |

## 前提となるバージョンと実装順

この設計の実装前提は、`ArchitectureRevision.md`のPhase 6が完了し、次が成立していることである。

- 旧`SymphonyTask`が3.0.0で削除済みである。
- `PauseManager`から`PauseController`への移行と、内部の`PauseService`／`PauseRegistry`分割が完了している。
- Runtime Compositionの初期化・終了が`SymphonyOrchestrator`へ集約されている。
- 公開非同期APIの既定が`Awaitable`へ統一されている。

3.0.0より前に提供する必要が生じた場合は、旧`SymphonyTask`と衝突しない別名を選び、この設計書の公開API、移行説明、バージョン判断を改訂する。同じ完全修飾名のまま2.xへ実装しない。

## Round分割

### Round 1 — ECSタイマー、専用awaiter、ライフサイクル、利用例

本機能は1 Roundで実装する。公開awaiterだけ、または利用されないECS基盤だけを先にリリースすると、単独で価値を持たない中間状態になるため分割しない。

ただし、ファイル変更の途中に性能ゲートを置く。最小の内部Scheduler、System、Jobを作成した時点で性能を計測し、既存`Awaitable.WaitForSecondsAsync`に対する優位性または本書の受け入れ値を満たさない場合は、公開APIを追加せず作業を中断してユーザーへ報告する。推測で閾値を緩和しない。

このRoundに含むもの:

- 専用のprivate ECS WorldとBurst Jobによるタイマー更新。
- 1回だけawaitできる`SymphonyTask`と`SymphonyTaskAwaiter`。
- Pool再利用、Promise ID、世代token、キャンセル、Shutdown処理。
- Pause追従、Domain Reloadなしの再初期化。
- correctness test、性能計測用Sample、利用者向け文書、依存パッケージ宣言。

このRoundに含まないもの:

- 結果付き`SymphonyTask<T>`。
- `async SymphonyTask`メソッドを実現するcustom async method builder。
- WhenAll、WhenAny、timeout、条件待機、Task／Awaitable変換。これらは`SymphonyAwaitable`の責務とする。
- Editor上でPlay Mode外に進行するタイマー。
- real-time、unscaled-time、fixed-stepを選ぶoverload。
- Default ECS Worldまたは利用側が作成したWorldへのSystem注入。

## 公開API

```csharp
namespace SymphonyFrameWork.Utility
{
    using System.Threading;

    public readonly struct SymphonyTask
    {
        public static SymphonyTask DelayEcsAsync(
            float durationSeconds,
            CancellationToken token = default);

        public SymphonyTaskAwaiter GetAwaiter();
    }

    public readonly struct SymphonyTaskAwaiter :
        System.Runtime.CompilerServices.ICriticalNotifyCompletion
    {
        public bool IsCompleted { get; }

        public void GetResult();
        public void OnCompleted(System.Action continuation);
        public void UnsafeOnCompleted(System.Action continuation);
    }
}
```

### API契約

| 項目 | 契約 |
| --- | --- |
| 時間 | `Time.deltaTime`相当のscaled game timeを積算する。`PauseController`がPause中のフレームは進めない |
| `durationSeconds == 0` | Entityを作らず同期的に正常完了する |
| 負数、NaN、正負Infinity | `ArgumentOutOfRangeException`を同期的に送出する |
| 事前キャンセル | Entityを作らず、await時に`OperationCanceledException`を送出する |
| 実行中キャンセル | cancellation callbackではUnity APIへ触れずPromise IDをthread-safe queueへ積む。次のメインスレッド更新でEntityを破棄し、待機をキャンセルする |
| 完了スレッド | 正常完了と実行中キャンセルのcontinuationはUnityメインスレッドで実行する |
| await回数 | 1回だけ。二重await、二重continuation登録、消費済み世代へのアクセスは`InvalidOperationException` |
| default値 | `default(SymphonyTask)`のawaitは`InvalidOperationException` |
| 呼び出しスレッド | `DelayEcsAsync`はUnityメインスレッド限定。違反時は`InvalidOperationException` |
| 未初期化／終了中 | `SymphonyNotInitializedException`。独自の遅延初期化は行わない |
| 未awaitの値 | 完了後もSourceを再利用できないため、Shutdownまで保持される。呼び出し側は作成した値を必ず1回awaitする |

`SymphonyTask`はタイマー待機を表す汎用Utilityであり、`DesignPhilosophy.md`の公開可能範囲にある「サブシステムに依存しない汎用ユーティリティ」としてpublicにする。`SymphonyTaskAwaiter`はC# awaiter patternの公開シグネチャに必要なValue Objectであり、利用側が直接生成できないようコンストラクタは`internal`にする。

この型には`AsyncMethodBuilderAttribute`を付けない。`async SymphonyTask FooAsync()`はサポートせず、`DelayEcsAsync`が返した値を直接awaitする用途だけを公開契約とする。

## 内部設計

```text
利用側
  └─ await SymphonyTask.DelayEcsAsync(...)
          │
          v
SymphonyTask（Adaptor / 公開Utility）
  └─ SymphonyTaskService（Application）
       ├─ SymphonyTaskSourcePool ──> SymphonyTaskSource
       ├─ SymphonyTaskRegistry（Promise ID ──> Source）
       ├─ cancellation request queue
       └─ IEcsTimerScheduler（Applicationが定義する契約）
            ^
            │ 実装
       EcsTimerScheduler（Infrastructure）
            ├─ Promise ID ──> ECS Entity の対応表
            └─ private World
                 └─ EcsTimerSystem（managed SystemBase）
                      ├─ UpdateTimerJob（Burst / IJobEntity）
                      └─ persistent NativeQueue<CompletedTimerEvent>

SymphonyOrchestrator（Composition）
  └─ SymphonyTaskInitializer
       ├─ Schedulerを生成してSymphonyTaskへ注入
       ├─ SymphonyTaskRunnerComponentを生成・接続
       └─ Shutdownを逆順解放リストへ登録
```

### 専用World

`EcsTimerScheduler`が`new World("SymphonyTaskWorld")`で専用Worldを所有する。Default Worldは利用側が無効化、差し替え、破棄する可能性があるため使用しない。専用WorldはPlayerLoopへ直接追加せず、`SymphonyTaskRunnerComponent.Update`から1フレームに1回だけ`EcsTimerSystem.Update()`を呼ぶ。

Runnerは次を順に実行する。

1. `SymphonyTaskService`がbackground threadから届いたキャンセル要求をメインスレッドで処理する。
2. Compositionから渡されたPause状態を確認する。
3. Pause中でなければ`IEcsTimerScheduler`へscaled delta secondsを渡して更新する。
4. JobをCompleteした後、完了eventをdrainし、Promiseを解決してEntityを破棄する。

この構成により、利用側のECS World構成へ干渉せず、Burst対象Jobからmanaged stateへアクセスしない。

### ECS更新

`EcsTimerComponentData`はblittableな値だけを持つ。

```csharp
internal struct EcsTimerComponentData : Unity.Entities.IComponentData
{
    public float RemainingSeconds;
    public long PromiseId;
}
```

`UpdateTimerJob`は`RemainingSeconds`からdelta secondsを減算し、0以下になったEntityとPromise IDを`NativeQueue<CompletedTimerEvent>.ParallelWriter`へ追加する。managed object、`PauseController`、`CancellationToken`には触れない。

`EcsTimerSystem`はmanagedな`SystemBase`とし、persistent queueとJobのschedule／completeだけを所有する。System本体をBurst化するのではなく、データ並列部分である`UpdateTimerJob`を`[BurstCompile]`する。Entity破棄は`EcsTimerScheduler`、managed continuation解決は`SymphonyTaskService`がメインスレッドで行う。

`SymphonyTaskService`は入力検証後の作成順、Promiseの登録、cancelとcompleteの競合解決、Source返却を担当する。Unity APIとECS型を直接参照せず、Application側に定義する`IEcsTimerScheduler`だけを使用する。`EcsTimerScheduler`はこの契約を実装し、World、Entity、Native Containerの所有と解放を担当する。この境界によりSource／cancel／世代のEditModeテストでECS Worldを必要とせず、ApplicationからInfrastructureへの具象依存を作らない。

### SourceとPool

`SymphonyTaskSource`は次の状態を明示する。

```text
Pending → Succeeded → Consumed
       └→ Canceled  → Consumed
```

- Sourceごとに`uint`の世代tokenを持ち、Rent時に更新する。
- Schedulerは単調増加する`long`のPromise IDでSourceとECS Entityを対応付ける。
- continuationは1件だけ保持し、登録済みかを別flagで判定する。
- `GetResult`は状態と世代を検証し、キャンセル登録をDisposeしてからSourceをPoolへ返す。キャンセル結果の場合は返却後に`OperationCanceledException`を送出する。
- Poolは初期化時に既定数をwarm upし、不足時だけSourceを追加生成する。容量不足時の割り当ては仕様として許容し、性能計測では事前に必要件数をwarm upする。
- Source、Pool、Promise対応表へ触る通常経路はメインスレッドに限定する。cancellation callbackだけはthread-safe queueへのenqueueに限定し、lock中にcontinuationやUnity APIを呼ばない。

### 初期化と終了

`SymphonyTaskInitializer`はUnityの自動初期化属性を持たず、`SymphonyOrchestrator`から明示的に呼ぶ。

- PauseサブシステムのBuild完了後にSchedulerとRunnerを構築する。
- Schedulerを`SymphonyTask`へ注入してから公開操作をReadyにする。
- Orchestratorの逆順解放リストへ`SymphonyTaskInitializer.Shutdown`を1件だけ登録する。
- 再初期化時は残存Schedulerを先にShutdownする。
- Shutdownでは新規作成を拒否し、対応表から全Promiseを外してから各Sourceをキャンセルし、cancellation registration、Native Container、private Worldを同期的に解放する。
- Shutdownは多重呼び出し可能にし、Jobを新しく開始せず、実行中Jobがある場合だけCompleteしてからWorldをDisposeする。
- Runner ComponentはOrchestratorが所有するGameObjectとともに破棄する。Runner自身はpackage-wide lifetime tokenへ登録しない。

## ファイル構成

名前空間はすべて`SymphonyFrameWork.Utility`とし、`Internal`や概念レイヤー名を名前空間へ含めない。

| パス | 新規／変更 | 責務 |
| --- | --- | --- |
| `Assets/SymphonyFrameWork/Runtime/Utility/SymphonyTask/SymphonyTask.cs` | 新規 | 公開の1回待機値と`DelayEcsAsync`入口 |
| `Assets/SymphonyFrameWork/Runtime/Utility/SymphonyTask/SymphonyTaskAwaiter.cs` | 新規 | awaiter patternの公開Value Object |
| `Assets/SymphonyFrameWork/Runtime/Utility/SymphonyTask/Internal/Application/SymphonyTaskSource.cs` | 新規 | continuation、完了状態、世代token、キャンセル登録 |
| `Assets/SymphonyFrameWork/Runtime/Utility/SymphonyTask/Internal/Application/SymphonyTaskSourcePool.cs` | 新規 | Sourceのwarm up、Rent、Return |
| `Assets/SymphonyFrameWork/Runtime/Utility/SymphonyTask/Internal/Application/SymphonyTaskRegistry.cs` | 新規 | Promise IDとSourceの登録、取得、除去 |
| `Assets/SymphonyFrameWork/Runtime/Utility/SymphonyTask/Internal/Application/IEcsTimerScheduler.cs` | 新規 | Applicationが必要とするschedule、cancel、update、completion取得契約 |
| `Assets/SymphonyFrameWork/Runtime/Utility/SymphonyTask/Internal/Application/SymphonyTaskService.cs` | 新規 | 作成、cancel／complete競合、Source消費を統括 |
| `Assets/SymphonyFrameWork/Runtime/Utility/SymphonyTask/Internal/Infrastructure/EcsTimerComponentData.cs` | 新規 | Remaining secondsとPromise IDを持つECS Component |
| `Assets/SymphonyFrameWork/Runtime/Utility/SymphonyTask/Internal/Infrastructure/EcsTimerSystem.cs` | 新規 | persistent completion queueとBurst Jobのschedule／complete |
| `Assets/SymphonyFrameWork/Runtime/Utility/SymphonyTask/Internal/Infrastructure/EcsTimerScheduler.cs` | 新規 | private World、Entity、Promise対応表の所有 |
| `Assets/SymphonyFrameWork/Runtime/Utility/SymphonyTask/Internal/Infrastructure/SymphonyTaskRunnerComponent.cs` | 新規 | Unity Update、delta time、Pause状態をServiceへ渡す境界 |
| `Assets/SymphonyFrameWork/Runtime/Utility/SymphonyTask/Internal/Composition/SymphonyTaskInitializer.cs` | 新規 | 構築、注入、Ready、逆順Shutdown |
| `Assets/SymphonyFrameWork/Runtime/Orchestrator/Internal/SymphonyOrchestrator.cs` | 変更 | InitializerをPause後に構築し、終了処理を記録 |
| `Assets/SymphonyFrameWork/SymphonyFrameWork.asmdef` | 変更 | `Unity.Entities`、`Unity.Burst`、`Unity.Collections`参照を追加 |
| `Assets/SymphonyFrameWork/Tests/Editor/SymphonyTaskSourceTests.cs` | 新規 | Source、Pool、世代、不正利用のEditModeテスト |
| `Assets/SymphonyFrameWork/Tests/Runtime/SymphonyTaskRuntimeTests.cs` | 新規 | 実時間、Pause、cancel、再利用のPlayModeテスト |
| `Assets/SymphonyFrameWork/Tests/Runtime/SymphonyFrameWork.Tests.Runtime.asmdef` | 変更 | テストからECS型を直接使う場合だけ`Unity.Entities`参照を追加 |
| `Assets/SymphonyFrameWork/Samples/Runtime/SymphonyTaskSample/` | 新規 | 公開APIだけを使うSample sceneとscript、性能計測用の件数設定 |
| `Assets/SymphonyFrameWork/package.json` | 変更 | 依存パッケージ、Sample、versionを更新 |
| `Assets/SymphonyFrameWork/README.md` | 変更 | 対象用途、single-await、キャンセル、性能条件、quick start |
| `Assets/SymphonyFrameWork/Documentation~/AgentUsage.md` | 変更 | API選択基準と禁止事項 |
| `Assets/SymphonyFrameWork/Documentation~/Architecture.md` | 変更 | private World、Runtime依存、初期化・終了順 |
| `Assets/SymphonyFrameWork/CHANGELOG.md` | 変更 | Add、依存追加、Awaitable既定規約の例外を記録 |

新規`.meta`は手書きせず、Unity Editorに生成させる。既存の旧`Runtime/Utility/SymphonyTask.cs`は3.0.0で削除済みであることを実装開始前に確認し、旧ファイルのGUIDを新しい型へ流用しない。旧型から意味が変わるため、GUID継承による見かけ上の互換性を作らない。

## 依存方向

```text
利用側 ──> SymphonyTask / SymphonyTaskAwaiter
                         │
                         v
              SymphonyTaskService ──> IEcsTimerScheduler
                     │                         ^
                     v                         │ 実装
      Registry / SymphonyTaskSourcePool   EcsTimerScheduler
                                               │
                                               └─> private Unity.Entities World
                                                          │
                                                          └─> Unity.Collections / Unity.Burst

SymphonyOrchestrator ──> SymphonyTaskInitializer ──> 上記の具象型
Pause Application ──状態をCompositionへ通知──> Runner ──bool値だけをSchedulerへ渡す
```

RuntimeからEditorへの参照は追加しない。Burst対象Jobからmanaged Symphony Framework型への参照を追加しない。Default ECS Worldと利用側の具象型へ依存しない。

配布パッケージはコード上で直接使用する次の依存を`package.json`へ追加する。

```json
"com.unity.burst": "1.8.29",
"com.unity.collections": "2.6.8",
"com.unity.entities": "1.4.8"
```

Runtime asmdefには、各パッケージのRuntime asmdefをGUID参照で追加する。

- `Unity.Burst`: `2665a8d13d1b3f18800f46e256720795`
- `Unity.Collections`: `e0cd26848372d4e5c891c569017e11f1`
- `Unity.Entities`: `734d92eba21c94caba915361bd5ac177`

## エラー処理

| 状況 | 扱い |
| --- | --- |
| durationが負数、NaN、Infinity | 公開入口で`ArgumentOutOfRangeException` |
| `continuation == null` | awaiterで`ArgumentNullException` |
| 未初期化、Shutdown中／後 | `SymphonyNotInitializedException` |
| main thread以外から作成 | `InvalidOperationException`。Entity作成をbackground threadへ逃がさない |
| 二重await、消費済み世代、二重continuation | `InvalidOperationException`。黙って別Promiseへ接続しない |
| CancellationTokenのキャンセル | await時に、元tokenを含む`OperationCanceledException` |
| Job／World更新中の内部例外 | 新規受付を停止し、残るSourceをキャンセルしてから例外をOrchestratorへ伝える。部分的なReady状態を残さない |
| Shutdown中の個別Source解放失敗 | 残りを解放し、Orchestratorの既存集約方式で最後に記録する |

## 影響範囲

- 後方互換な公開型と公開メソッドの追加である。ただし2.xの旧`SymphonyTask`と同じ完全修飾名を別の意味で再導入するため、3.0.0より前のバイナリ互換を主張しない。
- 3.0.0で`SymphonyTask`から`SymphonyAwaitable`へ移行した利用側コードを自動的に戻さない。本機能は大量タイマー向けの明示的opt-inとする。
- `com.unity.entities`、`com.unity.burst`、`com.unity.collections`が必須依存になり、パッケージ導入サイズ、import時間、compile時間が増える。READMEへ明記する。
- 既存の`SymphonyAwaitable`、Pause、Tweenのシグネチャと挙動は変更しない。
- シリアライズ形式への影響は無い。
- private Worldを所有するため、利用側のDefault World設定とEntity数へ本機能のEntityを露出しない。

## テストの置き場と種別

### EditMode: `Assets/SymphonyFrameWork/Tests/Editor/SymphonyTaskSourceTests.cs`

`InternalsVisibleTo("SymphonyFrameWork.Tests.Editor")`を使い、Unity Worldを作らずSourceとPoolを直接組み立てる。

- 初期状態、正常完了、キャンセル、GetResult後のPool返却を、テスト用continuationの呼び出し回数とPool件数で確認する。
- 同じSourceをRentし直した後、旧世代tokenで`IsCompleted`／`GetResult`／continuation登録すると`InvalidOperationException`になることを確認する。
- 二重await相当の二重continuation登録と二重GetResultを個別テストにする。
- 事前キャンセルと`durationSeconds == 0`がEntityなしで同期完了することを、テスト用SchedulerのEntity件数で確認する。

### PlayMode: `Assets/SymphonyFrameWork/Tests/Runtime/SymphonyTaskRuntimeTests.cs`

公開APIをawaitし、フレーム経過とPauseをUnity Test Frameworkの`UnityTest`で進める。

- 0秒が同一フレームで完了する。
- 正の秒数が指定時間より前に完了せず、その後にメインスレッドで完了する。
- Pause中は進まず、Resume後に残時間から再開する。
- 事前キャンセルと実行中キャンセルが`OperationCanceledException`になる。
- 100件を同時開始し、各continuationが1回だけ実行され、終了後のEntity件数とPromise件数が0になる。
- Pool warm up後の第2波でSourceが再利用され、旧世代tokenが新しい待機を完了させない。

PlayModeテスト内でPlay Modeを終了・再開することはできないため、Domain Reloadなしの2往復は後述の手動確認で行う。

### 性能計測

`SymphonyTaskSample`へ10,000件を一括作成するDevelopment用Componentを置き、ProfilerRecorderとUnity Profilerで次を別々に測る。

1. Burst compileとPool拡張を除外するwarm up run。
2. 10,000件が未完了の定常更新フレームを300フレーム。
3. 同じ容量を再利用する第2波の作成フレーム。
4. 10,000件が同一フレームで完了するworst-case drain。
5. 同条件の`Awaitable.WaitForSecondsAsync` baseline。

定常更新のManaged GC Allocが0 B、中央値と95パーセンタイルが1 ms未満であることを必須とする。作成、完了burst、cancelの値は別に記録し、定常値へ混ぜない。基準PC、Editor／Development Player、Mono／IL2CPP、Burst有効状態を結果へ併記する。

## 動作確認手順

1. `package.json`の依存解決後、`uloop-clear-console` → `uloop-compile` → `uloop-get-logs`を実行し、Error 0件、意図しないWarning 0件を確認する。
2. EditModeとPlayModeの全テストを実行し、既存テストを含めて成功することを確認する。
3. `SymphonyTaskSample`で0秒、通常完了、Pause／Resume、事前キャンセル、実行中キャンセルを操作し、Consoleに例外漏れが無いことを確認する。
4. Play Modeの開始・終了を2回繰り返し、2回目の開始直後にprivate Worldが1つ、Runnerが1つ、Promise／Entityが0件であることを内部診断またはdynamic codeで確認する。
5. 未完了timerを残してPlay Modeを終了し、全待機がキャンセルされ、Native Containerのleak warningが無いことを確認する。
6. Profilerで性能受け入れ条件を確認し、baselineと計測環境を記録する。
7. `rg -n "UnityEditor|EditorPrefs" Assets/SymphonyFrameWork/Runtime Assets/SymphonyFrameWork/Core -g '*.cs'`でRuntime／CoreからEditor参照が無いことを確認する。
8. Burst InspectorまたはBurstログで`UpdateTimerJob`がBurst compileされていることを確認する。
9. `git diff --check`とsubmoduleの全差分を確認し、新規`.cs`／Sample assetと`.meta`が対になっていることを確認する。

## バージョン判断

**マイナー（3.1.0）。**

3.0.0完了後に、後方互換な公開Utility、公開awaiter、公開メソッドを追加するため。既存公開APIの削除や変更、シリアライズ変更は含まない。ECS系依存パッケージの追加と「公開非同期型は原則Awaitable」という規約への性能限定の例外は、CHANGELOGとREADMEへ明記する。

性能ゲートを満たさず公開APIを追加しない場合、このRoundはリリースせず、versionとCHANGELOGを更新しない。

## この Round で触るバージョン関連ファイル

| ファイル | 触る箇所 |
| --- | --- |
| `Assets/SymphonyFrameWork/package.json` | `version`を`3.0.0`から`3.1.0`へ更新。`dependencies`へEntities／Burst／Collections、`samples`へSymphonyTask Sampleを追加 |
| `Assets/SymphonyFrameWork/CHANGELOG.md` | `## [3.1.0]`の`Add`へ公開API、`Change`へ必須依存と非同期型規約の例外、性能測定条件を追加 |
| `Assets/SymphonyFrameWork/README.md` | 「現在のバージョン」、必要パッケージ、Utility一覧、quick start、single-awaitと適用件数の注意を更新 |
| `Assets/SymphonyFrameWork/Documentation~/AgentUsage.md` | 少数待機はAwaitable、大量タイマーだけSymphonyTaskという選択基準と、保存／再await禁止を追加 |
| `Assets/SymphonyFrameWork/Documentation~/Architecture.md` | Runtime asmdef依存、private World、Orchestratorの初期化・終了順を更新 |

`Assets/SymphonyFrameWork/AGENTS.md`は導線と常時ルールが変わらないため更新しない。`ArchitectureRevision.md`は3.0.0までの移行記録であり、3.1.0の新機能は本設計書を正本とするため変更しない。

## 実装開始時の停止条件

次のいずれかに該当した場合は、代替実装へ勝手に切り替えず、ファイル変更を止めてユーザーへ報告する。

- 3.0.0で旧`SymphonyTask`が残っている。
- Pause状態をCompositionから取得する型安全な経路が無い。
- Runtime asmdefからEntities／Burst／Collectionsを参照すると依存循環またはPlayer build errorが起きる。
- private Worldの明示更新でBurst Job、time、Shutdownのいずれかが成立しない。
- 性能ゲートを満たさず、既存Awaitable方式に対する採用根拠が得られない。
- continuationのメインスレッド実行、キャンセル、Source再利用の安全性を同時に満たせない。
