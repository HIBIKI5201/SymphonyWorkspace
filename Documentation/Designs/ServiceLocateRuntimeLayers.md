# Service Locate Runtime Layers — Round H1

## 目的

`ArchitectureRevision.md`のPhase 3にあるService Locate分割を、単独で検証・リリースできる2 Roundへ分けて進める。本書は前半の**Round H1**を扱う。

現行のService Locateは、次の責務が`ServiceLocator`、`ServiceLocateManager`、`ServiceLocateData`へ集中している。

- 公開Facadeが入力検証、登録待機、Registry参照、Unity Object破棄を直接行う
- `ServiceLocateManager`が登録順序と`Component`の親子付け・破棄を同時に扱う
- `ServiceLocateData`が登録辞書、待機callback、GameObject所有、Transform操作、終了検知を同時に扱う
- 同じ型が登録済みの場合、通常の`RegisterInstance`でも新しいインスタンスを暗黙に破棄する
- Editor WindowがFacadeとDataのprivate fieldをリフレクションで取得する

H1では登録単位をDomain Entity、型検索と待機者をRegistry、処理順をService、Unity固有の所有処理を`ServiceHostComponent`へ分ける。公開`ServiceLocator`は既存シグネチャを維持して内部実装へ転送する。

併せてIssue #111を解決し、通常登録の失敗では呼び出し側のインスタンスを破棄せず、自動破棄を明示的に選ぶAPIを追加する。

## Round分割

| Round | 内容 | 破壊的変更 | バージョン |
| --- | --- | --- | --- |
| **H1（本書）** | `ServiceRegistrationEntity`、`ServiceLocateRegistry`、`ServiceLocateService`、`ServiceHostComponent`、Issue #111、自動テスト | なし | 2.14.0 |
| **H2** | `ServiceLocateQuery`、`ServiceRegistrationInfo`、`ServiceLocateDto`、`ServiceLocateViewModel`、Editor Windowの購読化、MCPの公開Info利用 | なし | 2.15.0予定 |
| Phase 4 | `LocateType` → `LocateTypeEnum`、`ServiceLocatorWindow` → `ServiceLocateWindow`、Component／Interfaceの所属移動 | あり | 3.0.0 |

H1完了時点でもFacade、Sample、MCP、Editor Windowは動作する。H2を実施しなくても登録・取得・解除・破棄の公開契約は整合した状態になる。

Issue #109のconstructor injectionは新しい依存解決機能であり、内部レイヤー分割とは独立して検証すべきため含めない。Issue #105の型移動は公開namespaceとシリアライズ参照へ影響するためPhase 4へ据え置く。

## 公開API

### 既存APIの維持

次の既存APIはシグネチャを変更しない。

- `RegisterInstance<T>`／`RegisterInstance(Type, object, LocateType)`
- `UnregisterInstance`の全overload
- `DestroyInstance`の全overload
- `IsExistInstance`、`GetInstance`、`GetRequiredInstance`、`TryGetInstance`
- `GetInstanceAsync`、`TryGetInstanceAsync`、`RegisterAfterLocate`
- `LocateType`

公開非同期APIの`ValueTask`と内部の登録待機方式は、本Roundでは互換性のため維持する。`Awaitable`へのシグネチャ移行と待機者ごとの`AwaitableCompletionSource`化はPhase 5で扱う。

### 通常登録の所有権

通常の`RegisterInstance`は、同じ型が登録済みなら`false`を返すだけに変更する。渡されたインスタンスを`Dispose`または`Destroy`せず、所有権は呼び出し側に残る。

登録成功後も、`UnregisterInstance`は登録解除とSingleton階層からの切り離しだけを行い、インスタンスを破棄しない。登録済みインスタンスを明示的に破棄する操作は既存の`DestroyInstance`を使用する。

### `RegisterInstanceWithAutoDispose`

Issue #111の明示的な所有権移譲APIとして、次のoverloadを追加する。

```csharp
public static bool RegisterInstanceWithAutoDispose<T>(
    T instance,
    LocateType type = LocateType.Locator)
    where T : class;

public static bool RegisterInstanceWithAutoDispose(
    Type type,
    object instance,
    LocateType locateType = LocateType.Locator);
```

- 型の重複によって登録できなかった場合だけ、渡された新しいインスタンスを自動解放する
- `IDisposable`なら`Dispose`を呼ぶ
- `Component`ならその`GameObject`を`UnityEngine.Object.Destroy`する
- 両方に該当する場合は、既存挙動と同じく`Dispose`とGameObject破棄の両方を行う
- `null`は既存登録APIと同じく`false`を返し、解放処理を行わない
- `type`がnull、instanceがtypeへ代入不能、`LocateType`が不正な場合は、既存APIと同じ例外を送出し、所有権は移譲しない
- Singleton階層への追加中に例外が起きた場合はRegistry登録をrollbackする。自動破棄overloadだけが新しいインスタンスを解放し、通常overloadは呼び出し側へ所有権を返したまま例外を伝播する

`RegisterInstanceWithAutoDispose`は利用側が「登録に失敗した新規候補を以後使用しない」と判断した場合だけ使用する。既存インスタンスを置換するAPIではない。

## 内部設計

### `ServiceRegistrationEntity`

`Runtime/System/ServiceLocator/Internal/Domain/ServiceRegistrationEntity.cs`へ置く`internal sealed class`とする。

保持する値:

- 登録キーの`Type`
- 登録payloadの`object`
- 登録時に指定された`LocateType`
- 現在Registryへ登録中かを表す状態

constructorで登録済み状態を生成し、登録解除時は`Unregister`だけが状態を変更する。Unity Objectの有効判定、Transform操作、Dispose、Destroyは行わない。公開API、Registry、Serviceを参照しない。

### `ServiceLocateRegistry`

`Runtime/System/ServiceLocator/Internal/Application/ServiceLocateRegistry.cs`へ置く`internal sealed class`とする。

責務:

- `Type`をキーに`ServiceRegistrationEntity`を重複なしで登録する
- Entityの点検索、存在判定、削除、全消去を行う
- 既存`RegisterAfterLocate`と`GetInstanceAsync`が使う型別待機callbackを保持する
- 登録成功時に対象型の待機callbackを1回だけ取り出して削除する
- timeout／cancel時に該当callbackだけを解除する

RegistryはGameObject、Transform、`UnityEngine.Object.Destroy`、ログ、公開Info／Dtoを扱わない。H1中のFacade・Editor・MCP互換用に登録payloadの変更不能なスナップショットを`internal`で返すが、H2で`ServiceLocateQuery`へ読み取りを一本化した時点で削除する。

### `IServiceHost`

`Runtime/System/ServiceLocator/Internal/Application/IServiceHost.cs`へ置くUnity境界の最小契約とする。

```csharp
internal interface IServiceHost
{
    void Attach(object instance);
    void Detach(object instance);
    bool DisposeInstance(object instance);
}
```

Applicationはこのinterfaceだけを参照し、Unity型や具象Componentを認識・生成しない。

### `ServiceHostComponent`

`Runtime/System/ServiceLocator/Internal/Infrastructure/ServiceHostComponent.cs`へ置く`internal sealed MonoBehaviour`とし、`IServiceHost`を実装する。

- Singleton登録された`Component`を自身のTransform直下へ移す
- 登録解除時、現在の親が自身なら親子関係を解除する
- `IDisposable.Dispose`とComponentのGameObject破棄を実行する
- `OnApplicationQuit`で終了中を記録し、破棄済みTransformへ触れない
- `DisposeHost`で自身の所有GameObjectを破棄する。このメソッドはApplication契約へ含めず、Composition Rootだけが呼ぶ
- Registry、Entity、Facade、待機callbackを参照しない

`SymphonyOrchestrator`は`ISystemObjectFactory.CreateComponent<ServiceHostComponent>`で具象を生成し、`IServiceHost`として`ServiceLocator.Initialize`へ渡す。`ServiceLocateData.InitializeQuittingState`／`ResetQuittingState`は不要になる。

### `ServiceLocateService`

`Runtime/System/ServiceLocator/Internal/Application/ServiceLocateService.cs`へ置く`internal sealed class`とする。constructorで`ServiceLocateRegistry`と`IServiceHost`を受け取る。

登録成功時の順序:

1. RegistryへEntityを追加する
2. `LocateType.Singleton`ならHostへ親子付けを依頼する
3. 状態変更eventを1回発行する
4. 登録待ちcallbackを実行する

待機callbackから見える時点ではRegistry登録とSingleton親子付けが完了している。callbackが例外を投げても確定済み登録は維持し、例外は呼び出し側へ伝播する。

登録失敗時:

- 通常登録は`false`を返すだけで、候補インスタンスへ触れない
- 自動破棄登録はHostへ候補インスタンスの解放を依頼して`false`を返す

解除時はHostからの切り離し、Registryからの削除、状態変更eventの順に行う。破棄時は既存と同様にインスタンスを解放してから登録を削除する。

`OnStateChanged`は論理的な登録、解除、破棄が確定した時だけ1回発行する。H2のViewModelが購読するため本Roundで追加するが、ViewModel自体は生成しない。

ServiceはUnity API、Editor API、ViewModel、Query、公開Facade、具象Hostを参照しない。ログ設定に基づく既存の診断ログはFacadeまたはHostへ漏らさず、Service内の成功／失敗結果に応じてFacade側から従来どおり出力する。

### `ServiceLocator`

H1中の暫定Composition Rootとして次を所有する。

```text
ServiceLocator.Initialize(ServiceHostComponent)
  ├─ ServiceLocateRegistry
  └─ ServiceLocateService
```

- 渡された具象Hostを`IServiceHost`としてServiceへ注入し、Facade自身はComposition用途としてHostの終了だけを所有する
- CommandはServiceへ転送する
- 登録payload取得、存在判定、同期取得はH1中のみRegistryへ転送する
- 登録待機の追加／解除はServiceへ転送する
- `ResetRuntimeState`はServiceの接続を解除し、RegistryをClearし、Hostを破棄して全参照をnullへ戻す
- Domain Reload無効で再初期化されても、登録、待機callback、event購読、Hostを残さない

既存の`RegisteredInstances`と`SingletonRoot`はEditor／MCP互換用の`internal` accessorとしてH1では残す。前者は新Registry、後者はFacadeがComposition用途で保持する具象Hostから値を返す。H2でQuery／Infoへ移行後に削除する。

### Editor Windowの暫定追従

`ServiceLocatorWindow`はH1でprivate fieldへのReflectionを削除し、`ServiceLocator.RegisteredInstances`の変更不能なスナップショットを使用する。毎Editorフレームの`Update`とListView再構築は互換維持のため残し、H2でViewModel購読へ置換する。

この追従により`ServiceLocateData`を削除してもWindowが空表示やcast例外にならず、H1を単独でリリースできる。

## 依存方向

```text
利用側 ──> ServiceLocator ──> ServiceLocateService ──> ServiceLocateRegistry
                                   │                         │
                                   │                         └──> ServiceRegistrationEntity
                                   v
                              IServiceHost
                                   ^
                                   │
                         ServiceHostComponent ──> Unity API

SymphonyOrchestrator ──生成・注入──> ServiceHostComponent
```

- EntityはApplication、Unity、Editorを参照しない
- RegistryはEntityと標準C#型だけを参照する
- ServiceはRegistryと`IServiceHost`契約だけを参照する
- InfrastructureのHostだけがTransform、Component、Destroyへ触れる
- Runtimeから`UnityEditor`を参照しない
- Editor Windowは`InternalsVisibleTo("SymphonyFrameWork.Editor")`により暫定accessorへ到達できる
- EditModeテストは既存`InternalsVisibleTo("SymphonyFrameWork.Tests.Editor")`によりEntity、Registry、Service、Host契約へ到達できる

## ファイル構成

| パス | 変更 |
| --- | --- |
| `Runtime/System/ServiceLocator/ServiceLocator.cs` | Service／Registryへの転送、新しい自動破棄API、初期化・終了 |
| `Runtime/System/ServiceLocator/Internal/Domain/ServiceRegistrationEntity.cs` | 新規。登録単位の同一性と状態 |
| `Runtime/System/ServiceLocator/Internal/Application/IServiceHost.cs` | 新規。Unity所有境界の最小契約 |
| `Runtime/System/ServiceLocator/Internal/Application/ServiceLocateRegistry.cs` | 新規。型検索、Entity、登録待機callback |
| `Runtime/System/ServiceLocator/Internal/Application/ServiceLocateService.cs` | 新規。登録、解除、破棄、自動破棄の処理順 |
| `Runtime/System/ServiceLocator/Internal/Infrastructure/ServiceHostComponent.cs` | 新規。親子付け、終了検知、Dispose、Destroy |
| `Runtime/System/ServiceLocator/Internal/ServiceLocateManager.cs` | 削除。ServiceとHostへ責務移管 |
| `Runtime/System/ServiceLocator/Internal/ServiceLocateData.cs` | 削除。RegistryとHostへ責務移管 |
| `Runtime/Orchestrator/Internal/SymphonyOrchestrator.cs` | Hostの生成・注入、旧終了検知処理の削除 |
| `Editor/Administrator/UITK/CS/ServiceLocatorWindow.cs` | Reflectionを削除し暫定accessorへ追従 |
| `Samples/Runtime/ServiceLocatorSample/Scripts/ServiceLocatorSample_1.cs` | 重複Singleton候補の自動破棄を明示する利用例 |
| `Tests/Editor/ServiceRegistrationEntityTests.cs` | Entityの値と状態遷移 |
| `Tests/Editor/ServiceLocateRegistryTests.cs` | 登録、検索、重複、削除、待機callback、Clear |
| `Tests/Editor/ServiceLocateServiceTests.cs` | 通常／自動破棄の差、Host呼び出し、event、rollback |

新しい`.cs`とフォルダの`.meta`は手書きせずUnity Editorへ生成させる。削除する既存`.cs`は対応する`.meta`も同時に削除する。既存の公開型、MonoBehaviour、SceneのGUIDは変更しない。

`ServiceLocateLogOption`、`ServiceInjector`、`SymphonyLocate`、`SymphonyLocateObject`は公開契約または別責務であり、本Roundでは移動・改名しない。

## エラー処理

- 未初期化時は既存どおり`SymphonyNotInitializedException`
- nullの登録キーは`ArgumentNullException`
- 登録キーへ代入不能なpayloadは`ArgumentException`
- 不正な`LocateType`は`ArgumentOutOfRangeException`
- null payloadは既存どおり`false`
- 型重複は例外にせず`false`
- 未登録型の解除／破棄は既存どおり`false`
- Host操作の例外は握りつぶさず、Registryを登録前の状態へrollbackして再送出する
- 自動破棄中の`Dispose`例外は握りつぶさず呼び出し側へ伝播する。Registryの既存登録は変更しない
- 待機callbackが例外を投げた場合は既存どおり登録呼び出しへ伝播する。callback一覧は再実行されないよう取り出し時点で削除する

## 影響範囲

後方互換な公開overload追加と、Issue #111で要求された通常登録失敗時の副作用除去である。既存の型名、namespace、enum値、シリアライズ済みfield、Sample Scene、Configは変更しない。

利用側への主な影響:

- 通常の`RegisterInstance`が重複時に新候補を破棄しなくなる。呼び出し側は`false`の場合も候補の所有権を保持する
- 従来の暗黙破棄が必要な箇所は`RegisterInstanceWithAutoDispose`へ変更する
- 登録成功、取得、解除、明示破棄、Singletonのシーン跨ぎ、登録待機の結果は維持する
- `SymphonyLocate`は通常登録を使い続けるため、重複したInspector対象を勝手に破棄しない
- `ServiceLocatorSample_1`はシーン再ロードで生じる重複Singleton候補を明示的に自動破棄する

## テストの置き場と種別

すべて`Tests/Editor/`のEditModeテストとし、Unity APIを使わないfake `IServiceHost`でApplicationを検証する。

### `ServiceRegistrationEntityTests`

- constructorでType、payload、LocateType、登録中状態を保持する
- `Unregister`で状態が1回だけ解除され、2回目も不変である
- 書き方: object payloadを使ってEntityを直接生成し、propertyと状態遷移をassertする

### `ServiceLocateRegistryTests`

- 型ごとの登録、点検索、存在判定、重複拒否、削除、Clear
- 取得済みEntityが別登録へすり替わらない
- 引数なし／payload付きの待機callbackが登録成功時に1回だけ実行される
- 待機解除後とClear後にcallbackが実行されない
- 書き方: Registryを直接生成し、テスト用classのTypeをキーに同期的に検証する

### `ServiceLocateServiceTests`

- 通常登録の重複では新候補を解放しない
- 自動破棄登録の重複では新候補を1回解放する
- Singleton成功時だけHostのAttachを呼ぶ
- 解除時にDetachしてRegistryから除去するがDisposeしない
- 明示破棄時にDisposeしてRegistryから除去する
- 登録、解除、破棄の成功ごとに`OnStateChanged`を1回だけ通知し、失敗時は通知しない
- Attach例外時にRegistryをrollbackし、自動破棄の有無をoverload選択に合わせる
- 書き方: 呼び出し回数と例外を記録するfake Hostを注入し、UnityEngine.Objectを生成せず処理順をassertする

既存EditMode／PlayModeテストも全数実行する。テストasmdefの`UNITY_INCLUDE_TESTS`は維持する。

## 動作確認手順

1. `uloop-clear-console`後に`uloop-compile`を実行し、Error 0、意図しないWarning 0を確認する
2. EditModeとPlayModeの全テストを実行する
3. `ServiceLocatorSample`をPlayし、CameraとSingletonが取得できる
4. Scene再ロード後も最初の`ServiceLocatorSample_1`が残り、新しい重複候補だけが自動破棄される
5. 遅延登録された`ServiceLocatorSample_2`を`GetInstanceAsync`で取得できる
6. `SymphonyMcpTools.GetServiceLocatorJson()`が登録件数、型名、実効登録方式、instance名を従来どおり返す
7. Symphony AdministratorのService Locator一覧が表示され、Reflection例外が無い
8. Play Modeの開始・終了を2回繰り返し、2回目の開始時に前回の登録、待機callback、Hostが残らない
9. ConsoleのError／Exceptionが0件である
10. `rg -n "System.Reflection|BindingFlags|FieldInfo" Editor/Administrator/UITK/CS/ServiceLocatorWindow.cs`が0件
11. `rg -n "UnityEditor|EditorPrefs" Runtime Core -g '*.cs'`で新規違反が0件

ServiceLocator SampleのBuild Settings追加は検証中だけ行い、検証後に元のBuild Settingsと自動生成`SceneListEnum`を戻す。Play Mode中に生成したGameObjectをSample Sceneへ保存しない。

## バージョン判断

**マイナー（2.14.0）。** 後方互換な`RegisterInstanceWithAutoDispose`を追加するため。通常登録の重複時に暗黙破棄しなくなる点はIssue #111で要求された副作用除去であり、既存シグネチャを壊さない。

H2は公開`ServiceRegistrationInfo`と一覧APIを追加するため、別のマイナー2.15.0を予定する。Phase 4の型改名は3.0.0で行う。

## このRoundで触るバージョン関連ファイル

| ファイル | H1での変更 |
| --- | --- |
| `Assets/SymphonyFrameWork/package.json` | `2.13.0` → `2.14.0` |
| `Assets/SymphonyFrameWork/CHANGELOG.md` | 2.14.0へ自動破棄API、通常登録の所有権変更、内部レイヤー分割、テストを記録 |
| `Assets/SymphonyFrameWork/README.md` | 現在バージョンと通常登録／自動破棄登録の使い分けを更新 |
| `Assets/SymphonyFrameWork/Samples/Runtime/ServiceLocatorSample/Scripts/ServiceLocatorSample_1.cs` | 自動破棄を明示するAPI利用例へ変更 |
| `Assets/SymphonyFrameWork/Documentation~/Architecture.md` | H1後のEntity／Registry／Service／Host関係を反映 |
| `Assets/SymphonyFrameWork/Documentation~/AgentUsage.md` | 登録失敗時の所有権と自動破棄APIの選択基準を追加 |

`AGENTS.md`はドキュメント導線と常時ルールが変わらないため更新しない。H2は同じREADME／CHANGELOG／package.json／Architecture／AgentUsageのService Locate節だけを2.15.0として更新し、H1と同じcommitへ混ぜない。

## GitHub Issue

- **Issue #111**: H1の完了条件と一致するため、本ブランチ`feature/111-service-locate-runtime-layers`で解決する。PR本文へ`Issue: #111`を記載し、マージ後にcloseする
- Issue #109: constructor injectionは別の公開機能として後続Roundで設計する
- Issue #105: Component／Interface移動はPhase 4へ据え置く
