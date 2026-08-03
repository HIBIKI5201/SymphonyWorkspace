# Service Locate Query / ViewModel — Round H2

## 目的

Round H1で分離した`ServiceRegistrationEntity`、`ServiceLocateRegistry`、`ServiceLocateService`の読み取り経路をAdaptorの`ServiceLocateQuery`へ集約し、公開API、Editor Window、MCP診断がRegistryやEntityを直接読まない構造へ移行する。

H1完了時点では互換維持のため、`ServiceLocator`がRegistryを直接読み、`ServiceLocatorWindow`がEditor更新ごとに登録スナップショットを再取得している。H2では次を解決する。

- 公開FacadeのQuery処理とCommand処理を分離する
- 登録状態を公開する不変な`ServiceRegistrationInfo`を追加する
- View用の`ServiceLocateDto`と`ServiceLocateViewModel`を追加する
- Editor Windowの毎Editorフレームpollingを状態変更eventの購読へ置き換える
- MCP診断をinternal accessorではなく公開Infoから生成する
- H1の暫定`RegisteredInstances`と`SingletonRoot`を削除する

本RoundはH1を含む`develop`から開始し、単独で検証・リリース可能な2.15.0とする。`LocateType`の改名、公開Componentやinterfaceの移動、constructor injectionは含めない。

## 公開API

### `ServiceRegistrationInfo`

`Runtime/System/ServiceLocator/ServiceRegistrationInfo.cs`へ次の公開Value Objectを追加する。

```csharp
public readonly struct ServiceRegistrationInfo : IEquatable<ServiceRegistrationInfo>
{
    public Type ServiceType { get; }
    public object Instance { get; }
    public LocateType LocateType { get; }
}
```

- `ServiceType`はRegistryの登録キー
- `Instance`は登録された公開payload。Entityや内部コレクションは公開しない
- `LocateType`は登録時に指定された方式
- Query取得時点の値を保持し、後続の登録解除で値自体は変化しない
- payloadは既存公開APIでも取得可能な参照であり、深い複製は行わない
- 等値比較は`ServiceType`と`LocateType`の値、`Instance`の参照同一性で行う

公開する根拠は、利用側とEditor/MCP診断が登録方式を含む管理状態を安全に照会するためである。Domain EntityやRegistryを公開すると内部ライフサイクルを変更できるため、公開範囲は不変Infoに限定する。

### `ServiceLocator`の一覧・点検索

```csharp
public static IReadOnlyList<ServiceRegistrationInfo> GetRegistrationInfos();

public static bool TryGetRegistrationInfo(
    Type serviceType,
    out ServiceRegistrationInfo registrationInfo);
```

- 一覧は`ServiceType.FullName ?? ServiceType.Name`のordinal昇順とする
- 返却一覧は変更不能な取得時点スナップショットとする
- 未登録型の点検索は`false`と`default`を返す
- `serviceType == null`は既存のType入力APIと同様に`ArgumentNullException`とする
- 未初期化時は既存Facadeと同様に`SymphonyNotInitializedException`とする

既存の登録、解除、破棄、取得、非同期待機APIのシグネチャは変更しない。既存の`GetInstance<T>`／`TryGetInstance<T>`はpayload取得という公開Query契約として維持する。

## 内部型

### `ServiceLocateQuery`

`Runtime/System/ServiceLocator/Internal/Adaptor/ServiceLocateQuery.cs`へ追加する。

責務:

- `TryGetInstance(Type, out object)`で登録payloadを取得する
- `Contains(Type)`で登録有無を返す
- `TryGetInfo(Type, out ServiceRegistrationInfo)`で1件の公開Infoへ変換する
- `GetInfos()`で並べ替え済みの変更不能な公開Info一覧を返す
- `GetDtos()`で並べ替え済みの変更不能なView用Dto一覧を返す

QueryだけがRegistryとEntityを読み、ServiceやViewModelへ依存しない。Command、待機callback、Host操作、ログを扱わない。

`ServiceLocateRegistry`にはQueryが読むための`internal IReadOnlyDictionary<Type, ServiceRegistrationEntity> Entities`を追加する。H1の暫定`GetInstancesSnapshot()`は削除する。

### `ServiceLocateDto`

`Runtime/System/ServiceLocator/Internal/Adaptor/ServiceLocateDto.cs`へ追加する不変値とする。

```csharp
internal readonly struct ServiceLocateDto : IEquatable<ServiceLocateDto>
{
    internal string ServiceTypeName { get; }
    internal string InstanceName { get; }
    internal LocateType LocateType { get; }
}
```

- `ServiceTypeName`はEditor表示向けの短い型名
- `InstanceName`は`Component.name`、Unity Object名、通常objectの実行時型名、破棄済みUnity Objectの`(Destroyed)`をQuery取得時に文字列化する
- `LocateType`は登録時の方式
- 文字列とenumの内容比較を実装する

Editor WindowへpayloadやEntityを渡さず、表示に必要な値だけを渡す。

### `ServiceLocateViewModel`

`Runtime/System/ServiceLocator/Internal/View/ServiceLocateViewModel.cs`へ追加する。

- constructorで`ServiceLocateQuery`と`ServiceLocateService`を受け取る
- 初期値を`query.GetDtos()`から生成する
- `service.OnStateChanged`を購読し、変更時だけ最新Dto一覧を取得する
- `IReadOnlyReactiveProperty<IReadOnlyList<ServiceLocateDto>> Registrations`を公開する
- 一覧用comparerは件数、順序、各Dtoの値を比較し、内容同値なら通知しない
- `Dispose`でService event購読とReactivePropertyを冪等に解放する

ViewModelはCommandを実行せず、Registry／Entityを直接参照しない。

## 公開FacadeとComposition

`ServiceLocator`はH2後に次の依存を保持する。

```text
ServiceLocator
  Command ──> ServiceLocateService
  Query   ──> ServiceLocateQuery
  View    ──> ServiceLocateViewModel（internal accessorのみ）
```

- `IsExistInstance`、同期取得、登録済みインスタンスとの同一性確認、登録済み判定をQueryへ転送する
- 公開Info APIをQueryへ転送する
- `RegisteredInstances`と`SingletonRoot`を削除する
- `IsInitialized`へQueryとViewModelを含める
- `Initialize`でRegistry、Service、Query、ViewModelを結合する
- `ResetRuntimeState`は最初にViewModelをDisposeし、その後RegistryとHostを解放して全参照をnullへ戻す
- `CurrentViewModel`をEditor Window接続用の`internal` accessorとして追加する

Orchestratorの公開・初期化順は変えず、従来どおり`ServiceLocator.Initialize(serviceHost)`と`ResetRuntimeState`をComposition入口として使用する。

## Editor Window

`ServiceLocatorWindow`を`IDisposable`にし、`SceneLoaderWindow`と同じPlay Mode接続方式へ変更する。

- `EditorApplication.playModeStateChanged`を購読する
- EnteredPlayMode後の`delayCall`で初期化済みViewModelへ接続する
- `Registrations.Subscribe(ApplyRegistrations)`で変更時だけListViewを更新する
- ExitingPlayMode／EnteredEditModeで購読を解除して空表示へ戻す
- `Dispose`でplayMode callback、delayCall、ReactiveProperty購読を解除する
- `Update()`とEditor更新ごとの登録スナップショット取得を削除する
- 既存のログ設定Toggleは維持する

`SymphonyAdministrator.Update()`から`_serviceLocatorWindow.Update()`を削除し、`OnDisable()`で`ServiceLocatorWindow.Dispose()`を呼ぶ。

Runtimeの`InternalsVisibleTo("SymphonyFrameWork.Editor")`が存在するため、Editor assemblyからinternal ViewModel／Dtoへ到達できる。新しいasmdef参照は不要である。

## MCP診断

`SymphonyMcpTools.GetServiceLocatorJson()`は`ServiceLocator.GetRegistrationInfos()`を使用する。

既存JSONフィールドを維持する。

```json
{
  "initialized": true,
  "registrationCount": 1,
  "registrations": [
    {
      "typeName": "Namespace.Service",
      "effectiveLocateType": "Singleton",
      "instanceName": "ServiceObject"
    }
  ]
}
```

`effectiveLocateType`は既存互換のため、`Component`以外では登録時にSingletonが指定されてもLocatorとして返す。`Component`ではInfoの`LocateType`を返す。instance名の既存規則も維持する。

MCPから`RegisteredInstances`、`SingletonRoot`、Registry、Entityを参照しない。

## ファイル構成

### 新規

| パス | レイヤー | 公開範囲 |
| --- | --- | --- |
| `Runtime/System/ServiceLocator/ServiceRegistrationInfo.cs` | Adaptor公開Info | public |
| `Runtime/System/ServiceLocator/Internal/Adaptor/ServiceLocateQuery.cs` | Adaptor Query | internal sealed |
| `Runtime/System/ServiceLocator/Internal/Adaptor/ServiceLocateDto.cs` | Adaptor Dto | internal readonly struct |
| `Runtime/System/ServiceLocator/Internal/View/ServiceLocateViewModel.cs` | View | internal sealed |
| `Tests/Editor/ServiceRegistrationInfoTests.cs` | EditMode test | test assembly |
| `Tests/Editor/ServiceLocateQueryTests.cs` | EditMode test | test assembly |
| `Tests/Editor/ServiceLocateViewModelTests.cs` | EditMode test | test assembly |

`Internal/Adaptor.meta`と`Internal/View.meta`はUnityに生成させる。

### 変更

- `Runtime/System/ServiceLocator/Internal/Application/ServiceLocateRegistry.cs`
- `Runtime/System/ServiceLocator/ServiceLocator.cs`
- `Editor/Administrator/UITK/CS/ServiceLocatorWindow.cs`
- `Editor/Administrator/SymphonyAdministrator.cs`
- `Editor/Debug/SymphonyMcpTools.cs`
- `README.md`
- `Documentation~/Architecture.md`
- `Documentation~/AgentUsage.md`
- `CHANGELOG.md`
- `package.json`

## 依存方向

```text
ServiceLocator ──Command──> ServiceLocateService ──> Registry ──> Entity
       │
       └──Query───────────> ServiceLocateQuery ────> Registry / Entity
                                      │
                         ┌────────────┴────────────┐
                         v                         v
            ServiceRegistrationInfo       ServiceLocateDto
                                                   │
ServiceLocateService.OnStateChanged ──> ServiceLocateViewModel
                                                   │
                                    IReadOnlyReactiveProperty
                                                   │
                                                   v
                                      ServiceLocatorWindow
```

ApplicationはQuery、Info、Dto、ViewModelへ依存しない。EditorはViewModel／Dtoと公開Facadeだけを参照する。

## エラー処理

- 公開Info APIの未初期化は既存と同じ`SymphonyNotInitializedException`
- `TryGetRegistrationInfo`のnull Typeは`ArgumentNullException`
- 未登録型は通常の検索失敗として`false`と`default`
- ViewModel未接続のEdit Modeでは例外を出さず空一覧を表示する
- MCPは既存どおり例外をJSONの`error`へ変換する
- subscriber例外の分離は既存`ReactiveProperty`の規則に委ねる

## 影響範囲

- 既存公開APIとシリアライズ形式は変更しない
- 公開Infoと一覧・点検索APIの追加は後方互換
- Administratorはpollingからevent購読へ変わるが表示項目とログ設定操作を維持する
- MCP JSONのフィールド名と値の意味を維持する
- `RegisteredInstances`と`SingletonRoot`はinternalのため利用側への破壊的変更ではない
- Issue #109、Issue #105、Phase 4の改名は対象外

## テストの置き場と種別

すべて`Tests/Editor/`のEditModeテストとして追加する。`InternalsVisibleTo`によりinternal型へ直接アクセスする。

### `ServiceRegistrationInfoTests`

- constructor値、参照同一性による等値／非等値、operator、hashを通常objectで検証する

### `ServiceLocateQueryTests`

- Registryへ複数型を逆順登録し、Info／Dto一覧が型名ordinal昇順になることを検証する
- 点検索の成功値と未登録時の`false/default`を検証する
- payload取得とContainsを検証する
- 取得後にRegistryを変更してもInfo／Dto一覧の件数が変わらないことを検証する
- Dtoの通常object名と破棄済みUnity Object表示をEditModeのGameObjectで検証する

### `ServiceLocateViewModelTests`

- constructor直後の初期値を検証する
- 登録／解除／破棄のService eventで一覧が更新されることをFake Hostで検証する
- 重複登録失敗では通知されないことを購読回数で検証する
- `Dispose`後はService変更を反映せず、多重Disposeが無害であることを検証する

既存Service／Registryテストも全件実行する。

## 動作確認手順

1. Unity Scene検証ガードに従い、親とsubmoduleのdirty状態を記録する
2. `uloop-clear-console`後に`uloop-compile`を実行し、Error 0、意図しないWarning 0を確認する
3. EditModeとPlayModeの全テストを実行する
4. Service Locator SampleをPlayし、Administratorの一覧に型名、instance名、LocateTypeが表示されることを確認する
5. 登録／解除時に一覧がevent駆動で更新され、`SymphonyAdministrator.Update()`がService Locatorをpollingしないことを確認する
6. `SymphonyMcpTools.GetServiceLocatorJson()`が既存フィールドと登録内容を返すことを確認する
7. Play Modeの開始・終了を2回繰り返し、2回目に前回のViewModel購読、登録、Hostが残らないことを確認する
8. ConsoleのError／Exceptionが0件であることを確認する
9. Play Mode停止後にpackageの`.unity`／`.prefab`差分が無いことを確認する
10. `rg -n "RegisteredInstances|SingletonRoot" Runtime Editor -g '*.cs'`が0件であることを確認する
11. `rg -n "UnityEditor|EditorPrefs" Runtime Core -g '*.cs'`で新規違反が0件であることを確認する

## バージョン判断

**マイナー（2.15.0）。** 後方互換な公開`ServiceRegistrationInfo`と一覧・点検索APIを追加するため。internal読み取り経路とEditor表示更新方式の置換は利用側の既存シグネチャを壊さない。

## このRoundで触るバージョン関連ファイル

| ファイル | H2での変更 |
| --- | --- |
| `Assets/SymphonyFrameWork/package.json` | `2.14.0` → `2.15.0` |
| `Assets/SymphonyFrameWork/CHANGELOG.md` | 2.15.0へ公開Info／Query／ViewModel／Editor／MCP変更とテストを記録 |
| `Assets/SymphonyFrameWork/README.md` | 現在バージョンと登録Infoの照会例を追加 |
| `Assets/SymphonyFrameWork/Documentation~/Architecture.md` | Query／Info／Dto／ViewModelの関係を追加 |
| `Assets/SymphonyFrameWork/Documentation~/AgentUsage.md` | 登録一覧・登録方式の照会APIを追加 |

Sampleの公開API利用はH1で更新済みのため、本Roundでは変更しない。`AGENTS.md`の導線とAPI早見表は本Roundの追加をREADME／AgentUsageへ委譲できるため変更しない。

## GitHub Issue

H2と完全一致する既存Issueは無いため、H1のIssue #111 branchへ混ぜず、`develop`から`feature/service-locate-query-viewmodel`を作成する。

