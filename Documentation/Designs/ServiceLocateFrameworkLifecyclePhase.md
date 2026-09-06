# ServiceLocateFrameworkLifecyclePhase

## 目的

[`ServiceLocateComponent`](../../Assets/SymphonyFrameWork/Runtime/Service/ServiceLocator/ServiceLocateComponent.cs#L13)
は`OnEnable`で`ServiceLocator.RegisterInstance`を直接呼ぶ。他コンポーネントの`Start`がその登録を
読みに行けるよう、`[DefaultExecutionOrder(-1000)]`で「最初に実行されるようにする」という回避策を使っている。

DesignPhilosophyの「避ける設計」はこれを名指しで禁止している。

> `DefaultExecutionOrder`やScript Execution Orderでフレームワーク全体の初期化順を作る。
> — `Documentation/DesignPhilosophy.md`

**この回避策は2つの意味で不十分でもある。**

1. `DefaultExecutionOrder`はコンポーネント間の相対順序を変えるだけで、Unityが保証する
   「初期シーンのオブジェクトはOnEnableが全部終わってからStartが始まる」という前提の外では効かない。
   `Instantiate`で実行時に生成したGameObjectは、Awake/OnEnable/Startが同じフレーム内で
   まとめて処理され、既存シーンオブジェクトの`Start`と順序を共有しない。
2. 複数の`ServiceLocateComponent`が同時に有効化されたとき、どちらが先に`OnEnable`を終えるかは
   結局Unityのヒエラルキー順・スクリプト実行順に依存したままで、フレームワークが明示的に制御していない。

## 要求

利用側からの指示: 「Unity low level APIを使って、OnEnableとStartの間にフレームワークのライフサイクルを
挟みたい。オーケストレーターが登録して、そこから全体に実行を行き渡らせる想定。やり方はMagicaCloth2の
実装を参考にする。」

MagicaCloth2などの物理・アニメーション拡張パッケージは、`MonoBehaviour`の`Update`任せにせず
`UnityEngine.LowLevel.PlayerLoop` / `PlayerLoopSystem`を直接書き換えて、Unity標準のUpdateループへ
自前の同期ポイントを挿入する。挿入位置をコード側で明示的に選べるため、Script Execution Orderの
数値やコンポーネントの有効化順という「暗黙の前提」に頼らずに実行順を保証できる。

本Roundはこの技法を、`ServiceLocateComponent`の`DefaultExecutionOrder`回避策を置き換える形で導入する。

## アクセス手段の検証

### 挿入位置は`Update.ScriptRunBehaviourUpdate`の直前

Unityの`MonoBehaviour.Start`は、PlayerLoopの独立したノードとして公開されていない。Unity Managerが
`Update.ScriptRunBehaviourUpdate`ネイティブ呼び出しの中で、その同期フレームに保留中の`Start`を
`Update`より先にまとめて実行してから`Update`へ進む（これはUnityが「初期シーンのオブジェクトは
OnEnableが全部終わってからStartが始まる」という保証を実現している実装そのものであり、
`Update.ScriptRunBehaviourUpdate`という1つのノードとしてしか公開されない）。

したがって、PlayerLoopへ挿入できる最も近いフックポイントは「`Update.ScriptRunBehaviourUpdate`の直前」
になる。ここに独自の`PlayerLoopSystem`を挿む場合:

- **OnEnable→フック→Start→Updateの順序が成立する対象**: 初期シーンに置かれたオブジェクト（フレーム開始前に
  Awake/OnEnableが完了しており、`Update.ScriptRunBehaviourUpdate`で初めてStartとUpdateが走る）、および
  前フレーム以前に`Instantiate`されたオブジェクト（Awake/OnEnable済みで、Startが次フレームへ持ち越されている）。
- **成立しない対象**: 同一フレーム内で`Instantiate`直後にStartまで実行されるケース——ただしこれは
  現在の`DefaultExecutionOrder`でも同様に保証できておらず、退行ではない。

`UnityEngine.PlayerLoop.Update`と`UnityEngine.PlayerLoop.Update.ScriptRunBehaviourUpdate`は
`UnityEngine.PlayerLoop`名前空間の`public`なマーカー型で、Unity公式ドキュメント
（Customizing the player loop）が`PlayerLoopSystem.type`へ渡す例として案内している。
リフレクションや`internal`型への依存は無い。

**このワークスペースにはUnity Editorプロセスが接続されておらず、`PlayerLoop.GetCurrentPlayerLoop()`の
実機ダンプは未実施。** 挿入位置の妥当性は、EditModeテストで「挿入後に`Update`配下で自分のシステムが
`ScriptRunBehaviourUpdate`の直前に存在すること」を`PlayerLoop.GetCurrentPlayerLoop()`を読み返して
検証する（後述）。これはPlay Modeを必要とせず、Editor起動時に必ず実行されるため、
実装時点でこの前提が崩れていれば即座にテストが落ちる。

### 依存方向: Compositionへの逆依存を作らない

`ServiceLocateComponent`（Adaptor）や`ServiceLocateService`（Application）が`SymphonyOrchestrator`
（Composition）を呼び出す経路は作らない。データフローは次の一方向にする。

```text
ServiceLocateComponent.OnEnable  ---enqueue--->  ServiceLocateService（保留キュー）
SymphonyOrchestrator（PlayerLoopで駆動）  ---flush呼び出し--->  ServiceLocator.FlushPendingRegistrations
```

`SymphonyOrchestrator`は起動時に`ServiceLocator.Initialize`をすでに呼んでおり、Composition→Adaptorの
参照は既存の依存方向のままである。Adaptor/ApplicationからOrchestratorへの参照は追加しない。

## 設計

### 1. `SymphonyFrameworkLifecycleLoop`（新規、Composition/Orchestrator専有）

`Runtime/Orchestrator/Internal/SymphonyFrameworkLifecycleLoop.cs`。`PlayerLoop`の読み書きだけを担当する
`internal static`なInfrastructure的ヘルパーで、`SymphonyOrchestrator`だけが呼ぶ。

```csharp
internal static class SymphonyFrameworkLifecycleLoop
{
    internal static bool IsInstalled { get; }

    // Update.ScriptRunBehaviourUpdateの直前へtickを挿入する。二重挿入は自動でUninstallしてから積み直す。
    internal static void Install(PlayerLoopSystem.UpdateFunction tick);

    // 挿入したシステムをPlayerLoopから取り除く。未挿入なら何もしない。
    internal static void Uninstall();
}
```

- マーカー型`private struct SymphonyFrameworkLifecycleSystem { }`を`PlayerLoopSystem.type`に使う。
- `Install`は`PlayerLoop.GetCurrentPlayerLoop()`を取得し、直下の`Update`ノードを探し、その
  `subSystemList`から`Update.ScriptRunBehaviourUpdate`のindexを探して直前に挿入した配列へ差し替え、
  `PlayerLoop.SetPlayerLoop`で書き戻す。該当ノードが見つからない場合は`InvalidOperationException`。
- `Uninstall`は`SymphonyFrameworkLifecycleSystem`型のノードを除去して書き戻す。
- Domain Reload無効時の再初期化に対応するため、`Install`は必ず内部で`Uninstall`してから積み直す
  （多重挿入によるtickの二重実行を防ぐ）。

### 2. `SymphonyOrchestrator`の変更

- `_resetActions`と対になる`_frameworkTickActions`（`List<Action>`）を追加する。
- `GameBeforeSceneLoaded`で`ServiceLocator.Initialize(serviceHost)`の直後に
  `RecordFrameworkTick(ServiceLocator.FlushPendingRegistrations);`を追加する。
  `_resetActions`と同様、将来他のサブシステムが同じ仕組みへ相乗りできる形にする
  （**本Roundで追加の呼び出し先は作らない**。KISSに従い、現時点で必要な1件だけを登録する）。
- `systemObject`生成後・サブシステム初期化の前に`SymphonyFrameworkLifecycleLoop.Install(FrameworkLifecycleTick);`
  を呼ぶ。
- `FrameworkLifecycleTick`は`_frameworkTickActions`を順に呼び、1件が例外を投げても残りを継続し、
  `SymphonyDebugLogger.LogException`へ記録する（`Shutdown`の逆順解放と同じ例外集約方針）。
- `Shutdown`で`SymphonyFrameworkLifecycleLoop.Uninstall();`と`_frameworkTickActions.Clear();`を呼ぶ
  （`_resetActions.Clear()`と同じタイミング）。

### 3. `ServiceLocateService`（Application）に保留キューを追加

```csharp
internal void EnqueuePendingRegistration(Type type, object instance, LocateTypeEnum locateType);
internal bool CancelPendingRegistration(Type type, object instance); // 戻り値: 取り消せた場合true
internal void FlushPendingRegistrations();
```

- `List<(Type type, object instance, LocateTypeEnum locateType)>`を保持する。
- `FlushPendingRegistrations`はキューをスナップショットしてから空にし、各要素を既存の`Register`へ渡す。
  `Register`実行中に別の`EnqueuePendingRegistration`が起きても同一Flush内で処理せず、次回Flushへ回す
  （スナップショット方式のため再入しても壊れない）。
- Flush時点で対象がUnityオブジェクトとして破棄済み（`instance is UnityEngine.Object u && u == null`）
  なら、その要素は`Register`へ渡さずスキップする。

### 4. `ServiceLocator`（Adaptor）に橋渡しメソッドを追加

```csharp
internal static void EnqueueAutoRegistration(Type type, object instance, LocateTypeEnum locateType);
internal static bool CancelPendingRegistration(Type type, object instance);
internal static void FlushPendingRegistrations();
```

いずれも`EnsureInitialized()`または`IsInitialized`ガードを経由し、既存の`RegisterInstance`と同じ
検証（null、型対応、`locateType`の範囲）は`Register`側で共通に効くため重複させない。
`EnqueueAutoRegistration`だけは`EnsureInitialized()`で未初期化を例外にする
（登録系は例外のまま、という既存方針`ServiceLocatorTeardownGuard`に合わせる）。
`CancelPendingRegistration`と`FlushPendingRegistrations`は未初期化なら何もしない
（照会・後始末系の戻り値方針に合わせる）。

**公開APIの追加・変更は無い。** `RegisterInstance`系の既存シグネチャと同期登録の挙動は変更しない。
これらのメソッドを直接呼ぶのは`ServiceLocateComponent`だけである。

### 5. `ServiceLocateComponent`の変更

- `[DefaultExecutionOrder(-1000)]`を削除する。
- `OnEnable`: `ServiceLocator.RegisterInstance`の直接呼び出しをやめ、
  `ServiceLocator.EnqueueAutoRegistration(_targetType, _target, _locateType);`に変更する。
- `OnDisable`: 既存の解除処理の前に`ServiceLocator.CancelPendingRegistration(_targetType, _target)`を試す。
  取り消せた場合（＝同フレーム内でFlush前にOnEnable→OnDisableが起きた場合）は、
  一度も登録されていないためそこで`return`し、既存の解除処理を実行しない。

```csharp
private void OnDisable()
{
    if (!_autoUnregister) { return; }
    if (_target == null) { return; }
    if (!ServiceLocator.IsInitialized) { return; }

    // Flush前にOnDisableへ回った場合は、保留中の登録を取り消すだけで完了する。
    if (ServiceLocator.CancelPendingRegistration(_targetType, _target)) { return; }

    bool isExist = ServiceLocator.IsExistInstance(_targetType);
    if (!isExist) { return; }

    ServiceLocator.UnregisterInstance(_targetType);
}
```

`Awake`と`OnValidate`は変更しない。

## 公開API

追加・変更・削除は無い。`ServiceLocateComponent`の`public`フィールド、`ServiceLocator`の`public`メソッドの
シグネチャと戻り値の意味は変わらない。変わるのは`ServiceLocateComponent`による自動登録が
**「`OnEnable`で同期的に見える」から「同じフレームの`Update.ScriptRunBehaviourUpdate`直前までに見える」**
へ変わる点だけで、これは[影響範囲](#影響範囲)で扱う。

## ファイル構成

| パス | 名前空間 | 変更 |
| --- | --- | --- |
| `Runtime/Orchestrator/Internal/SymphonyFrameworkLifecycleLoop.cs` | `SymphonyFrameWork.Orchestrator` | **新規** |
| `Runtime/Orchestrator/Internal/SymphonyOrchestrator.cs` | `SymphonyFrameWork.Orchestrator` | 変更 |
| `Runtime/Service/ServiceLocator/Internal/Application/ServiceLocateService.cs` | `SymphonyFrameWork.System.ServiceLocate` | 変更 |
| `Runtime/Service/ServiceLocator/ServiceLocator.cs` | `SymphonyFrameWork.System.ServiceLocate` | 変更 |
| `Runtime/Service/ServiceLocator/ServiceLocateComponent.cs` | `SymphonyFrameWork.System.ServiceLocate` | 変更 |
| `Tests/Editor/SymphonyFrameworkLifecycleLoopTests.cs` | `SymphonyFrameWork.Tests` | **新規** |
| `Tests/Editor/ServiceLocatePendingRegistrationTests.cs` | `SymphonyFrameWork.Tests` | **新規** |
| `Documentation~/Modules/ServiceLocator.md` | — | 変更（1文追記） |

新規ファイルは3件。`.meta`はUnity Editorに生成させる。

`ServiceLocateRegistry`・`ServiceLocateQuery`・`ServiceLocateViewModel`には手を入れない。保留キューは
Serviceが持つ責務（処理順の管理）そのものであり、Registryの責務（Entityの所有・検索）ではない。

## 依存方向

- `SymphonyFrameworkLifecycleLoop`はUnityの`PlayerLoop` APIだけに依存し、他サブシステムを参照しない。
- `SymphonyOrchestrator`→`ServiceLocator`→`ServiceLocateService`の参照方向は既存のまま。新しい逆依存は無い。
- `ServiceLocateComponent`（Adaptor）→`ServiceLocator`（Adaptor内の同一エントリポイント）の呼び出しは
  既存と同じで、レイヤーを跨がない。

## エラー処理

- `SymphonyFrameworkLifecycleLoop.Install`が挿入位置を見つけられない場合は`InvalidOperationException`を
  投げ、`SymphonyOrchestrator`の既存の`catch`（初期化失敗時のロールバック）へ合流する。
- `FrameworkLifecycleTick`内の例外は個別にキャッチし、残りのtickActionsを継続してから
  `SymphonyDebugLogger.LogException`へ集約する。毎フレーム呼ばれるため、1回の例外でPlayerLoopから
  自コンポーネントを外すような自己修復は行わない（`Shutdown`まで挿入を維持し、Editor Console
  へは繰り返し記録されうる。既存の`Update`内例外と同じ扱いで、新しい例外型は追加しない）。
- `EnqueueAutoRegistration`は未初期化なら`SymphonyNotInitializedException`（既存方針どおり）。
- `CancelPendingRegistration` / `FlushPendingRegistrations`は未初期化なら何もしない（無害なno-op）。

## 影響範囲

- 公開APIのシグネチャとシリアライズ形式に変更は無い。
- **`ServiceLocateComponent`の自動登録が可視になるタイミングが変わる。** 変更前は`OnEnable`内で
  同期的に`ServiceLocator`へ反映されていたが、変更後は同じフレームの`Update.ScriptRunBehaviourUpdate`
  直前まで遅延する。
  - 影響が無いケース（ドキュメント化済みの基本形）: `OnEnable`で登録→他コンポーネントの`Start`以降で取得。
    Unityの`Start`呼び出しは`Update.ScriptRunBehaviourUpdate`の中で行われるため、常にFlush後になる。
  - 影響があるケース: `gameObject.SetActive(true)`などで`ServiceLocateComponent`を有効化した**直後、
    同一フレーム内・同期的に**`ServiceLocator.GetInstance`を呼ぶコード。このパターンは
    `Documentation~/Modules/ServiceLocator.md`にも`AgentUsage.md`にも書かれておらず、公開契約として
    案内していない。
- `DefaultExecutionOrder(-1000)`の削除により、`ServiceLocateComponent`同士の有効化順への依存も無くなる
  （複数の`ServiceLocateComponent`が同じフレームで有効化されても、Flushは1回にまとまり順序非依存になる）。
- 実行時に`Instantiate`したオブジェクトの`Start`から見た可視性が、**むしろ改善する**。
  従来の`DefaultExecutionOrder`は同一フレーム内Instantiateの順序を制御できていなかった。

## テストの置き場と種別

EditMode。`Assets/SymphonyFrameWork/Tests/Editor/`。

### `SymphonyFrameworkLifecycleLoopTests.cs`

`PlayerLoop`はプロセス全体で共有される状態なので、**すべてのテストで`[TearDown]`に
`SymphonyFrameworkLifecycleLoop.Uninstall()`を置き、後始末漏れがEditorの以後のフレームへ
汚染しないようにする。**

| テスト名 | 検証内容 | どう書くか |
| --- | --- | --- |
| `Install_InsertsSystemImmediatelyBeforeScriptRunBehaviourUpdate` | 挿入後、`Update`直下で自システムの直後に`Update.ScriptRunBehaviourUpdate`が続くこと | `PlayerLoop.GetCurrentPlayerLoop()`を読み、`Update`ノードの`subSystemList`をたどってindexを比較する |
| `Install_CalledTwice_DoesNotDuplicateSystem` | 二重`Install`後もPlayerLoop内に自システムが1個だけ | `subSystemList`を数える |
| `Uninstall_RemovesInsertedSystem` | `Uninstall`後、`Update`直下に自システムが存在しない | 同上の探索が0件になることを確認 |
| `Uninstall_WithoutInstall_DoesNothing` | 未挿入での`Uninstall`が例外を投げない | `Assert.DoesNotThrow` |
| `Install_InvokesTickEveryManualUpdateCall` | 挿入した`updateDelegate`を手動で呼ぶと渡した`tick`が実行される | 挿入後、`PlayerLoop.GetCurrentPlayerLoop()`から自システムを探して`updateDelegate`を直接呼び出し、カウンタが増えることを確認（Play Modeを使わず検証できる） |

### `ServiceLocatePendingRegistrationTests.cs`

既存`ServiceLocateServiceTests.cs`と同じ`FakeServiceHost`パターンを使う（重複防止のため
`internal`な`FakeServiceHost`をこのテストへも複製する。既存ファイルに依存させて結合を作らない）。

| テスト名 | 検証内容 | どう書くか |
| --- | --- | --- |
| `FlushPendingRegistrations_WithQueuedEntry_RegistersInstance` | Enqueue後Flushで登録される | `EnqueuePendingRegistration`→`FlushPendingRegistrations`→`registry.Contains`が`true` |
| `FlushPendingRegistrations_EmptyQueue_DoesNothing` | 空キューでのFlushが何もしない | `host.AttachCount`などが0のまま |
| `CancelPendingRegistration_BeforeFlush_PreventsRegistration` | Flush前のCancelで登録されない | Enqueue→Cancel→Flush→`registry.Contains`が`false` |
| `CancelPendingRegistration_UnknownEntry_ReturnsFalse` | 未Enqueueの対象へのCancelは`false` | 戻り値を確認 |
| `FlushPendingRegistrations_DestroyedUnityObjectBetweenEnqueueAndFlush_SkipsEntry` | Flush前に破棄されたUnityObjectはスキップされる | `Component`を`Enqueue`後、テスト内で`Object.DestroyImmediate`してから`Flush`し、`registry.Contains`が`false`であることを確認（EditModeなので`DestroyImmediate`が使える） |

`ServiceLocator`側の橋渡しメソッド（`EnqueueAutoRegistration`等）は、`ServiceLocatorTeardownGuard`の
既存テスト群と同じ`[SetUp]`/`[TearDown]`パターン（`ServiceLocator.Initialize`→`ResetRuntimeState`）を
再利用し、`ServiceLocatorRegistrationInfoTests.cs`と同居させず新規ファイルに置く
（未初期化時の挙動を検証するテストと初期化済み前提のテストを混在させない、という既存方針を踏襲）。

| テスト名 | 検証内容 | どう書くか |
| --- | --- | --- |
| `EnqueueAutoRegistration_NotInitialized_Throws` | 未初期化での`EnqueueAutoRegistration`が例外 | `Assert.Throws<SymphonyNotInitializedException>` |
| `CancelPendingRegistration_NotInitialized_ReturnsFalse` | 未初期化での`Cancel`が`false` | 戻り値を確認 |
| `FlushPendingRegistrations_NotInitialized_DoesNotThrow` | 未初期化での`Flush`が例外を投げない | `Assert.DoesNotThrow` |

## 動作確認手順

自動で確認する項目:

- `uloop-compile`がエラー0・警告0
- EditModeテスト全数成功（追加分を含む）

人が操作する項目:

1. サンプルシーンへ、`ServiceLocateComponent`で自動登録する`GameObject`と、その型を`Start`で
   `ServiceLocator.GetInstance`する別の`MonoBehaviour`を、**ヒエラルキー順を意図的に「取得側が先」**
   にして配置する。
2. Play Modeへ入り、取得側の`Start`でインスタンスが取得できること（`DefaultExecutionOrder`が
   無くなった後も、ヒエラルキー順に関係なく成立することを確認する）。
3. 実行時に`Instantiate`した`ServiceLocateComponent`付きプレハブが、次フレームの`Start`から
   問題なく取得されること。
4. Domain Reloadが無効なため、Play Modeの開始・終了を2回繰り返し、2回目もPlayerLoopへ自システムが
   二重に残らないこと（`SymphonyFrameworkLifecycleLoopTests`の`Install_CalledTwice`と対になる実機確認）。
5. Play Mode終了後、Editor上で数フレーム待って例外や警告が出ないこと（`Uninstall`漏れが無いことの確認）。

## バージョン判断

**パッチ（6.3.1 → 6.3.2）。** 公開APIのシグネチャ、シリアライズ形式、既定値の意味を変えない。
[影響範囲](#影響範囲)で述べた「同一フレーム同期読み取り」という非公開契約への依存だけが挙動を変える。
`### Fix`として、`DefaultExecutionOrder`依存の除去と実行時Instantiateでの可視性改善を書く。

Roundは分割しない。新規2ファイル・変更4ファイルで、差分を一括してレビューできる規模に収まる。

## この Round で触るバージョン関連ファイル

- `package.json`の`version` → `6.3.2`
- `CHANGELOG.md`に`## [6.3.2]`の見出しと`### Fix`を追加
- `README.md`の「現在のバージョン」 → `6.3.2`

`AGENTS.md`のAPI早見表はシグネチャが変わらないため触らない。
`Documentation~/Modules/ServiceLocator.md`は**バージョン関連ではなく内容の更新**として、
「自動登録は同一フレームのStart呼び出し前までに反映される（DefaultExecutionOrderに依存しない）」ことを
1文追記する。

## 実施レポート

実施日: 2026-09-06 / バージョン: 6.3.2 / PR: 無し（利用側の指示によりPRは作成せず、
`claude/framework-lifecycle-integration-1sx8we`ブランチへ直接push）

### 実装した内容

設計どおり、`ファイル構成`に記載した7ファイル（新規3・変更4）+バージョン関連3ファイルを実装した。

- `SymphonyFrameworkLifecycleLoop`（新規）: `UnityEngine.LowLevel.PlayerLoop`を用い、
  `Update.ScriptRunBehaviourUpdate`の直前へ独自ノードを挿入/除去する。
- `SymphonyOrchestrator`: `_frameworkTickActions`を追加し、`GameBeforeSceneLoaded`で
  `SymphonyFrameworkLifecycleLoop.Install`を呼ぶ。`ServiceLocator.Initialize`直後に
  `RecordFrameworkTick(ServiceLocator.FlushPendingRegistrations)`を登録。`Shutdown`で
  `Uninstall`と`_frameworkTickActions.Clear()`を呼ぶ。
- `ServiceLocateService`: `EnqueuePendingRegistration` / `CancelPendingRegistration` /
  `FlushPendingRegistrations`と保留キュー`_pendingRegistrations`を追加。
- `ServiceLocator`: `EnqueueAutoRegistration` / `CancelPendingRegistration` /
  `FlushPendingRegistrations`の橋渡しメソッドを追加（いずれも`internal`、公開APIの追加無し）。
- `ServiceLocateComponent`: `[DefaultExecutionOrder(-1000)]`を削除。`OnEnable`は
  `EnqueueAutoRegistration`を呼ぶだけに変更。`OnDisable`は既存の解除処理の前に
  `CancelPendingRegistration`を試すよう変更。
- `Documentation~/Modules/ServiceLocator.md`へ、自動登録の可視化タイミングに関する2文を追記。
- `package.json` / `CHANGELOG.md` / `README.md`をパッチ版6.3.2へ更新。

### 設計から変えた点

無し。設計書どおりに実装した。

### 検証結果

**このセッションにUnity Editorプロセスが接続されておらず、`uloop-compile`と`uloop-run-tests`を
実行できなかった。** 設計書の「アクセス手段の検証」節で記載したとおり、実機でのPlayerLoop構造の
確認も未実施のまま実装している。

代わりに次を実施した。

- 変更した4ファイルと新規3ファイルを最終形まで通読し、`using`・名前空間・シグネチャの対応を
  手動で確認した（`UnityEngine.LowLevel.PlayerLoop`クラスと`UnityEngine.PlayerLoop`名前空間の
  名前衝突が無いことを含む）。
- `Tests/Editor/TypeModuleOwnershipTests.cs`・`RuntimeFolderLayoutTests.cs`・
  `PublicTypeTestCoverageTests.cs`など、変更が触れうる既存テストの内容を読み、
  今回の変更（新規`internal`型の追加、`ServiceLocateComponent`の属性削除）と衝突しないことを
  コードレビューで確認した。
- 新規`.meta`は自動生成させず、リポジトリ内の既存`.cs.meta`（`fileFormatVersion: 2` + `guid`のみの
  最小形式）に合わせて手動で追加した。GUIDは`uuid4`で新規生成し重複が無いことを確認済み。

**コンパイル0エラー・0警告の実測と、EditModeテスト全数成功の実測は未実施。** 次にUnity Editorへ
接続できるセッションで、`uloop-compile`→`uloop-run-tests`（`Tests/Editor/SymphonyFrameworkLifecycleLoopTests`
と`Tests/Editor/ServiceLocatePendingRegistrationTests`を含む）を最初に実行して確認する必要がある。

### 未実施の確認

「動作確認手順」の1〜5すべてが未実施（Unity Editor未接続のため）。特に次はPlay Modeでの実機確認が
必須で、コードレビューだけでは担保できない。

1. ヒエラルキー順を意図的に崩した状態での取得成功
2. 実行時`Instantiate`したプレハブの次フレーム取得成功
3. Domain Reload無効でのPlay Mode 2往復後もPlayerLoopへ二重挿入が残らないこと
4. Play Mode終了後、`Uninstall`漏れによる例外・警告が出ないこと

### 振り返り

- **ワーカーへの差し戻し**: 無し（Codex CLIがこの環境に存在しなかったため、設計書のとおり
  自分で実装した。`.agents/skills/implement/SKILL.md`が想定する「ワーカーが使えない場合」の
  対応として、独立した目が入らない分を設計書の詳細度とテストで補う方針を取った）。
- **設計書と実装が食い違った箇所**: 無し。
- **手順の途中で判断に迷った点**: `.meta`ファイルの生成方法。Unity Editorが無い環境では
  自動生成に頼れないため、既存ファイルの形式を確認してから手動生成する判断をした。
  この手順（Unity非接続時の`.meta`生成手順）は`implement`スキルに明文化されていない。
- **繰り返した定型動作**: 無し（Round単発のため）。
- **仕組みへの提案**: `implement`スキルの「ステップ2: ワーカーが実装する」に、Codex CLIが
  存在しない環境（このリモートセッションのような）での`.meta`ファイル生成手順
  （既存の最小形式`fileFormatVersion: 2` + `guid`を踏襲し、`uuid4`などでGUIDを新規生成する）を
  1項目として追記する余地がある。ただし今回1回の発生であり、次に同じ状況が起きるかは不明なため、
  **提案にとどめ、今回はスキルを変更しない。**
