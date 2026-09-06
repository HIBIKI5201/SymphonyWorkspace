# コンストラクタ注入

## 目的

`ServiceInjector` が持つのは `Inject<T0..T3>(IInjectable<...> target)` だけで、**生成済みのインスタンスへ後から差し込む形しかない。** 利用側は `IInjectable<T0, T1>` を実装し、`Inject` メソッドを書き、フィールドへ代入する手作業を型ごとに繰り返す必要がある。

```csharp
// 現状。依存が2件でもこれだけ書く。
public sealed class BattleService : IInjectable<SaveStore, AudioPlayer>
{
    private SaveStore _saveStore;
    private AudioPlayer _audioPlayer;

    public void Inject(SaveStore saveStore, AudioPlayer audioPlayer)
    {
        _saveStore = saveStore;
        _audioPlayer = audioPlayer;
    }
}
```

これは `MonoBehaviour` のようにフレームワークが生成を握れない型には必要な形だが、**ピュアC#の型は「生成時に依存を渡す」ほうが素直**である。コンストラクタで受け取れば `readonly` にでき、未注入の状態が存在しなくなる。[DesignPhilosophy.md `## 依存性注入とService Locator`](../DesignPhilosophy.md#依存性注入とservice-locator) も「ピュアC#クラスのモジュール内依存には、コンストラクタ注入を第一候補とする」と定めている。**その第一候補を実現する手段が公開APIに無い。**

Issue [#109](https://github.com/HIBIKI5201/SymphonyFramework/issues/109)。VContainerのコンストラクタ注入に似た、生成時に依存を自動解決する機能を `ServiceInjector` へ追加する。

## Round 分割

1 Round で完了する。**公開型を増やさない**（既存の `ServiceInjector` へメソッドを2つ足すだけ）ため、テストファイルの追加要求も発生しない。

`develop` から `feature/109-constructor-injection` を切る。

## 公開API

```csharp
namespace SymphonyFrameWork.System.ServiceLocate
{
    public static class ServiceInjector
    {
        // 既存の Inject<T0..T3> はそのまま

        public static T CreateInstance<T>() where T : class;
        public static object CreateInstance(Type type);
    }
}
```

利用側の書き方は次のとおり。

```csharp
public sealed class BattleService
{
    private readonly SaveStore _saveStore;
    private readonly AudioPlayer _audioPlayer;

    public BattleService(SaveStore saveStore, AudioPlayer audioPlayer)
    {
        _saveStore = saveStore;
        _audioPlayer = audioPlayer;
    }
}

// 依存はService Locatorから解決される。
BattleService service = ServiceInjector.CreateInstance<BattleService>();
```

**`IInjectable` は残す。** `MonoBehaviour` はUnityが生成するためコンストラクタ注入ができず、シーンロード時の自動注入経路（`TryAutoInject`）もそのまま必要である。**2つは置き換え関係ではなく、生成を誰が握るかで使い分ける。**

### コンストラクタの選び方

| 状況 | 扱い |
| --- | --- |
| `public` なコンストラクタが1つ | それを使う |
| `public` なコンストラクタが複数 | **引数が最も多いものを使う。** VContainerと同じ既定である |
| 引数が最多のものが複数ある | `InvalidOperationException`。どれを使うか決められない |
| `public` なコンストラクタが無い | `InvalidOperationException` |

`private` / `protected` / `internal` なコンストラクタは候補にしない。**「利用側が生成してよい形」をアクセス修飾子で表しているものを尊重する。**

### 引数の解決

| 状況 | 扱い |
| --- | --- |
| Service Locatorに登録がある | 登録済みインスタンスを渡す |
| 登録が無く、既定値がある | **既定値を使う。** `BattleService(SaveStore store, int retryCount = 3)` のような設定値をコンストラクタへ書ける |
| 登録が無く、既定値も無い | `ServiceNotRegisteredException` |

**引数を1つでも解決できない場合は、インスタンスを生成しない。** 全引数を解決してから `ConstructorInfo.Invoke` を1回だけ呼ぶ。半端に構築された対象を残さないためである。

### 対象外

- **`[Inject]` のような属性でコンストラクタを指定する仕組みは作らない。** 属性を1つ増やすと公開型が増え、利用側が覚えることも増える。「引数が最多のものを選ぶ」で足りない場合は、`public` なコンストラクタを1つに絞れば決まる。
- **プロパティ注入とフィールド注入は行わない。** コンストラクタで受け取れない依存は、既存の `IInjectable` を使う。
- **生成したインスタンスをService Locatorへ自動登録しない。** 生成と登録は別の判断であり、`CreateInstance` の戻り値を利用側が `RegisterInstance` へ渡せばよい。
- **循環依存の検出は行わない。** `CreateInstance` は登録済みインスタンスを渡すだけで、依存を再帰的に生成しない。循環の起こる余地が無い。
- **`Try` 形式は作らない。** 依存の未登録は利用側の結線ミスであり、[DesignPhilosophy.md `## クラス設計`](../DesignPhilosophy.md#クラス設計) の区分では例外にあたる。`GetRequiredInstance` と揃える。

## 確定前に検証したアクセス手段

コードを読んで確認した。**1件、現状では届かない経路が見つかった。**

| 前提 | 確認結果 |
| --- | --- |
| `ServiceInjector` へメソッドを足せる | ✔ `public static class`、名前空間 `SymphonyFrameWork.System.ServiceLocate` |
| `ServiceLocator` から `Type` を指定して登録済みインスタンスを取れる | ✘ **取れない。** 公開APIは `GetInstance<T>()` / `GetRequiredInstance<T>()` の**ジェネリックだけ**で、`Type` を受ける取得APIが無い |
| `ServiceLocateQuery.TryGetInstance(Type, out object)` は存在する | ✔ `internal`。ただし `ServiceLocator._query` は `private static` で、外から届かない |
| `ServiceNotRegisteredException(Type)` を投げられる | ✔ `public` なコンストラクタが `Type` を受ける |
| テストから `internal` を触れる | ✔ `Runtime/AssemblyInfo.cs` が `SymphonyFrameWork.Tests.Editor` へ `InternalsVisibleTo` を与えている |

**`ServiceLocator` へ `internal static bool TryGetInstance(Type serviceType, out object instance)` を追加する。** `GetInstance<T>()` が使っている `_query.TryGetInstance` と破棄済みUnity Objectの判定（`IsAvailableInstance`）を、型引数ではなく `Type` で通す内部専用の入口である。**公開APIは増やさない。** リフレクションで `GetInstance<T>` を呼ぶ回避策は取らない。ジェネリックメソッドの `MakeGenericMethod` は毎回のコストが大きく、例外も `TargetInvocationException` へ包まれて原因が分かりにくくなる。

## ファイル構成

パスはすべて `Assets/SymphonyFrameWork/` 起点。

- 変更 `Runtime/Service/ServiceLocator/ServiceInjector.cs`
  - `CreateInstance<T>()` と `CreateInstance(Type)` を追加する。名前空間は変えない。
- 変更 `Runtime/Service/ServiceLocator/ServiceLocator.cs`
  - `internal static bool TryGetInstance(Type, out object)` を追加する。
- 新規 `Runtime/Service/ServiceLocator/Internal/Application/ServiceConstructionPlanner.cs`
  - `internal static`。コンストラクタの選択と引数の解決を持つ。**Unity APIにもService Locatorにも触れず、解決手段を `Func<Type, object>` で受け取る。**
- 新規 `Tests/Editor/ServiceConstructionPlannerTests.cs`
- 新規 `Tests/Editor/ServiceInjectorTests.cs`
- 変更 `Tests/Editor/PublicTypeTestCoverageTests.cs`
  - `ServiceInjector` を `UntestedPublicTypes` から消す。
- 変更 `Samples~/Runtime/ServiceLocatorSample/Scripts/ServiceLocatorSample_Sequences.cs`
  - シーケンスの最後にコンストラクタ注入の実演を足す。**シーンとアセットは変更しない**（スクリプトの追記だけで済む）。
- 新規 `Samples~/Runtime/ServiceLocatorSample/Scripts/ServiceLocatorSample_Consumer.cs`
  - コンストラクタで依存を受け取るピュアC#の型。サンプルが何を見せているかを1ファイルで読めるようにする。
- 変更 `Documentation~/Modules/ServiceLocator.md`、`Documentation~/Html/`（再生成）
- 変更 `CHANGELOG.md`、`package.json`、`Core/SymphonyConstant.cs` の `VERSION`、`README.md`（版）
- 設計記録 `Documentation/Designs/ConstructorInjection.md`

新規 `.cs` の `.meta` は、Unity Editorが無い実行環境のためスクリプトで生成する（[CONTRIBUTING.md §2](../CONTRIBUTING.md)）。

## 依存方向

```text
利用側 ──> ServiceInjector（Adaptor 公開エントリポイント）
                 │
                 ├──> ServiceConstructionPlanner（Application・純粋なリフレクション）
                 └──> ServiceLocator.TryGetInstance（同じサブシステムの内部入口）
```

- `Runtime -> Core`、`Editor -> Runtime` の向きは変わらない。`UnityEditor` への参照も増えない。
- **`ServiceConstructionPlanner` は Service Locator を知らない。** 引数の解決手段を `Func<Type, object>` で受け取るため、**Service Locatorを初期化せずに単体テストできる。**
- `ServiceInjector` と `ServiceLocator` は同じサブシステムの Adaptor 層であり、既存の `Inject` も `ServiceLocator.GetRequiredInstance` を直接呼んでいる。この Round で依存の向きは変えない。

## エラー処理

| 状況 | 扱い |
| --- | --- |
| `type` が `null` | `ArgumentNullException` |
| `type` が abstract / interface / 値型 / open generic | `ArgumentException`。生成できない型を渡すのは呼び出し側の誤り |
| `public` なコンストラクタが無い | `InvalidOperationException` |
| 引数最多の `public` コンストラクタが複数 | `InvalidOperationException`。**メッセージへ引数の数と候補数を含め、どう直すかを書く** |
| 引数を解決できない | `ServiceNotRegisteredException`。**解決できなかった引数の型を `ServiceType` へ入れる** |
| コンストラクタ本体が例外を投げた | `TargetInvocationException` へ包まず、**元の例外をそのまま伝播する**。既存の `TryAutoInject` と同じく `ExceptionDispatchInfo` を使う |
| Service Locatorが未初期化 | 「登録が無い」として扱う。`GetInstance<T>()` が未初期化時に `null` を返す既存の挙動に揃える。既定値が無ければ `ServiceNotRegisteredException` になる |

`CreateInstance<T>` の型引数は `where T : class` にする。値型はService Locatorへ登録できず（`RegisterInstance<T>` が `where T : class`）、生成しても依存を1つも解決できないためである。

## 影響範囲

- **公開APIの追加のみ。** 既存の `Inject<T0..T3>` と `TryAutoInject` の挙動、シリアライズ形式は変わらない。
- `ServiceInjector` の `_injectMethods` は「`Inject` という名前の公開staticメソッド」だけを集めるため、`CreateInstance` を足しても対応表は変わらない。**この前提が壊れないことを、テストで固定する。**
- `ServiceLocator` へ `internal` メソッドが1つ増える。利用側からは見えない。

## テストの置き場と種別

EditMode（`Assets/SymphonyFrameWork/Tests/Editor/`）へ置く。

`ServiceConstructionPlannerTests.cs` — **Service Locatorを初期化せず、解決手段を辞書で差し替えて呼ぶ。** テスト内に `private sealed class` のコンストラクタ定義用スタブを置く。

| テスト | どう書くか |
| --- | --- |
| `SelectConstructor_SinglePublicConstructor_IsSelected` | 引数2件のコンストラクタが1つだけの型を渡し、`GetParameters().Length` が2であることを確認する |
| `SelectConstructor_MultipleConstructors_SelectsMostParameters` | 引数1件と3件を持つ型を渡し、3件のほうが選ばれることを確認する |
| `SelectConstructor_TiedParameterCount_Throws` | 引数1件のコンストラクタを2つ持つ型で `InvalidOperationException` になることを確認する |
| `SelectConstructor_NoPublicConstructor_Throws` | `private` コンストラクタだけの型で `InvalidOperationException` になることを確認する |
| `SelectConstructor_ParameterlessConstructor_IsSelected` | 引数無しの型で `GetParameters()` が空になることを確認する |
| `ResolveArguments_RegisteredServices_AreResolved` | 辞書で解決させ、渡された配列の要素と順序を確認する |
| `ResolveArguments_MissingWithDefault_UsesDefaultValue` | 既定値付きの引数を解決させず、既定値が入ることを確認する |
| `ResolveArguments_MissingWithoutDefault_Throws` | `ServiceNotRegisteredException` になり、`ServiceType` が解決できなかった引数の型であることを確認する |
| `ResolveArguments_Parameterless_ReturnsEmpty` | 引数無しのコンストラクタで空配列が返ることを確認する |

`ServiceInjectorTests.cs` — Service Locatorを初期化して実際に生成する。`ServiceLocator.Initialize(host)` は `ServiceHostComponent` を要求するため、**`SetUp` で `GameObject` へ付けて生成し、`TearDown` で破棄する。** `ServiceLocator.ResetRuntimeState()` を前後で呼ぶ。

| テスト | どう書くか |
| --- | --- |
| `CreateInstance_RegisteredDependencies_AreInjected` | スタブのサービスを登録し、生成したインスタンスが同じ参照を持つことを確認する |
| `CreateInstance_Parameterless_CreatesInstance` | 依存の無い型を生成できることを確認する |
| `CreateInstance_MissingDependency_Throws` | 未登録の依存で `ServiceNotRegisteredException` になることを確認する |
| `CreateInstance_NullType_Throws` | `ArgumentNullException` を確認する |
| `CreateInstance_AbstractType_Throws` | `ArgumentException` を確認する |
| `CreateInstance_InterfaceType_Throws` | `ArgumentException` を確認する |
| `CreateInstance_ConstructorThrows_PropagatesOriginalException` | コンストラクタで `InvalidOperationException` を投げる型を渡し、**`TargetInvocationException` ではなく元の例外が出る**ことを確認する |
| `Inject_ExistingOverloads_StillResolveAutoInject` | `CreateInstance` を足しても `TryAutoInject` の対応表が壊れていないことを、`IInjectable<T0>` の実装で確認する |

**`ServiceInjector` を `PublicTypeTestCoverageTests.UntestedPublicTypes` から消す。** この Round で初めてテストが付く。

## 動作確認手順

自動で確認する範囲:

1. `python scripts/verify_round.py` — コンパイル エラー0・警告0、EditMode / PlayMode 全数成功。
2. EditModeテストの件数が増えていること。

人が操作して確認する範囲:

3. `Samples~/Runtime/ServiceLocatorSample/` を `Assets/` 配下へコピーして Play し、実況ログの最後にコンストラクタ注入の結果が出ること。確認後にコピーを削除する。
4. Play Mode の開始・終了を2回繰り返し、`ServiceLocator.ResetRuntimeState` 後に `CreateInstance` が `ServiceNotRegisteredException` になること（古い登録を掴み続けていないこと）。

## バージョン判断

**マイナー更新（6.7.1 → 6.8.0）。** 公開APIへ後方互換なメソッドを2つ追加する。既存の呼び出し、シリアライズ形式、挙動は変わらない。

## この Round で触るバージョン関連ファイル

| ファイル | 触る箇所 |
| --- | --- |
| `Assets/SymphonyFrameWork/package.json` | `version` |
| `Assets/SymphonyFrameWork/Core/SymphonyConstant.cs` | `VERSION` |
| `Assets/SymphonyFrameWork/CHANGELOG.md` | 見出しと `### Add` |
| `Assets/SymphonyFrameWork/README.md` | 「現在のバージョン」 |

---

## 実施レポート

実施日: 2026-08-30 / バージョン: 6.8.0 / PR: [#207](https://github.com/HIBIKI5201/SymphonyFramework/pull/207)

### 実装した内容

| 設計 | 実装 |
| --- | --- |
| 公開API | `ServiceInjector.CreateInstance<T>()` と `CreateInstance(Type)`。公開型は増えていない |
| コンストラクタの選択と引数の解決 | `Runtime/Service/ServiceLocator/Internal/Application/ServiceConstructionPlanner.cs`（`internal static`） |
| `Type` を受ける取得口 | `ServiceLocator.TryGetInstance(Type, out object)` を `internal` で追加 |
| サンプル | `ServiceLocatorSample_Consumer` を追加し、シーケンス末尾で生成して実況ログへ出す |
| テスト | `ServiceConstructionPlannerTests`（14件）、`ServiceInjectorTests`（9件）。`ServiceInjector` を残作業一覧から削除 |

### 設計から変えた点

- **`ValidateCreatableType(Type, string parameterName)` を `ServiceConstructionPlanner` へ足した。** 設計書では生成できない型の判定を `ServiceInjector` 側に置く想定だったが、コンストラクタ選択と同じ「生成できるか」の判断であり、**Service Locatorを初期化せずにテストできる場所へ寄せたほうが検証しやすい。**
- それ以外は設計どおり。

### 検証結果

| 項目 | 結果 |
| --- | --- |
| `release_round.py preflight` | 全項目通過（tests ソース4件/テスト5件、version 6.8.0、bom 9件、meta 4件、docs 同期） |
| `build_module_docs.py --check` | OK 20件 |
| `verify_round.py` | **実行できず。** 実行環境に Unity Editor が無い |

**コンパイル、EditModeテスト、サンプルの動作確認は未実施。** 依頼者が後日まとめて実施する方針。新規4件の `.meta` は [CONTRIBUTING.md §2](../CONTRIBUTING.md) の例外規定に従いスクリプトで生成した。

### 振り返り

無し。今回の Round で新たに仕組みへ還元すべき手戻りは発生しなかった。**版の4か所とAuthoringアセットの公開範囲は、この Round の前に文書へ反映済みである。**
