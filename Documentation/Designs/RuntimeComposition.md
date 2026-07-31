# RuntimeComposition

> **「テストアセンブリから `internal` へ届かない」という制約は後に解消した。** 2.11.0 でテストをパッケージ内へ戻し、`InternalsVisibleTo` を与えている。この Round で見送った解放順の自動検証は、後から追加できる状態になっている。

## 目的

`ArchitectureRevision.md` の Phase 3 のうち、**Runtime 側の Composition 集約**を扱う。Round D（Editor 側）の対になる。

現在、Runtime のサブシステムは Composition Root である `SymphonyOrchestrator` を**逆参照**している。

| 逆参照している箇所 | 呼んでいるもの | 生成タイミング |
| --- | --- | --- |
| `Runtime/System/ServiceLocator/Internal/ServiceLocateData.cs:17` | `PreserveObject(_gameObject)` | **コンストラクタ内**（即時） |
| `Runtime/System/AudioManager.cs:68` | `PreserveObject(instance)` | 初回 AudioSource 要求時（遅延） |
| `Runtime/Debug/DebugHUD/SymphonyDebugHUD.cs:131` | `CreateSystemObject<SymphonyHUDDrawer>()` | 初回 HUD 表示時（遅延） |

これは `DesignPhilosophy.md` が禁止している依存方向である。

> 依存方向はOrchestratorから各レイヤーへの一方向にする。Runtimeの各レイヤーからOrchestratorを検索または呼び出してGameObject生成、永続化、初期化を依頼しない。
>
> Orchestratorを汎用FactoryやUnity helperとして公開しない。動的なUnityオブジェクト生成が必要なら、Compositionが生成済みComponentまたはInfrastructure契約を注入する。

あわせて、終了処理の登録も3箇所に分散している。

| ファイル | 登録内容 |
| --- | --- |
| `Runtime/System/SaveSystem/Internal/SaveSystem.cs:18` | `token.Register(SaveDataRegistry.ResetRuntimeState)` |
| `Runtime/System/SceneLoader/SceneLoader.cs:272` | `token.Register(ResetRuntimeState)` |
| `Runtime/System/ServiceLocator/ServiceLocator.cs:362` | `token.Register(ResetRuntimeState)` |

`DesignPhilosophy.md` は「`SymphonyOrchestrator`だけがpackage-wideな`destroyCancellationToken`へ1回登録し、キャンセル時に全サブシステムの`Shutdown`を逆順で実行する」と定めている。現状は登録が3件あり、**解放順が保証されない**。

さらに `ServiceLocateData` は `[RuntimeInitializeOnLoadMethod]` を自前で持っており、「自動初期化属性は Orchestrator だけ」という規則に反する。

## 公開APIへの影響

**無い。** 変更対象はすべて `internal` またはそれ以下。

`AudioManager` と `SymphonyDebugHUD` は `public static` だが、変更するのは内部の生成経路だけで、公開メソッドのシグネチャと挙動は変えない。

## 設計

### GameObject 生成を契約として注入する

Orchestrator を直接呼ぶ代わりに、**内側が定義した契約を Composition が実装して注入する**（依存性逆転）。

```csharp
// Core/Internal/ISystemObjectFactory.cs
internal interface ISystemObjectFactory
{
    GameObject CreateObject(string name);
    T CreateComponent<T>(string name) where T : Component;
}
```

- 契約は `Core/Internal/` に置く。複数サブシステムが必要とする横断的なものであり、上位機能へ依存しないため
- 実装 `SystemObjectFactory` は `Runtime/Orchestrator/Internal/` に置き、`DontDestroyOnLoad` を担当する
- Composition が各サブシステムの `Initialize` で注入する
- **サブシステムは `SymphonyOrchestrator` を参照しない。** 参照するのは契約だけ

`CodeGuidelines.md` の命名規則に従い `Factory` サフィックスを使う。

### 遅延生成を維持する

`AudioManager` と `SymphonyDebugHUD` は現在、GameObject を**必要になった時点で**生成する。

**Composition が事前に GameObject を作って渡す形にはしない。** そうすると HUD を使わないプロジェクトでも常に GameObject が存在することになり、挙動が変わる。契約（factory）を注入し、生成のタイミングは各サブシステムが従来どおり握る。

`ServiceLocateData` はコンストラクタで即時生成しているが、**コンストラクタから副作用を出さない**ため、生成済みの `GameObject` を受け取る形へ変える。これは `DesignPhilosophy.md` の「初期化に失敗した場合は、部分的に構築された状態を残さない」にも沿う。

### 前提の訂正: `ResetRuntimeState` は半数に存在しない

当初この設計書は「各サブシステムの `ResetRuntimeState` を逆順で呼ぶ」としていたが、**現状を確認した結果、6サブシステム中3つに存在せず、存在する3つのうち2つは `private` だった。**

| サブシステム | 現状 | 必要な変更 |
| --- | --- | --- |
| `SaveDataRegistry` | `internal static` | そのまま使える |
| `SceneLoader` | **`private static`** | `internal` へ引き上げる |
| `ServiceLocator` | **`private static`** | `internal` へ引き上げる |
| `PauseManager` | **無い** | 新設（`_pause`・`_isInitialized`・`_pauseEventDictionary` を初期化） |
| `AudioManager` | **無い** | 新設（`_instance`・生成済み AudioSource を解放） |
| `SymphonyDebugHUD` | **無い** | 新設（`_debugHUD` の `SymphonyLazyObject` を解放） |

`private` のままでは Orchestrator から呼べず、現在は token 登録のクロージャ経由でしか到達できない。集約するには可視性の引き上げが必須である。

**3つの新設は挙動の追加ではなく、これまで解放されていなかった状態を解放するようにする変更である。** Domain Reload 無効環境では、これらが残ることでゴースト参照や二重購読の原因になりうる。

### 終了処理を1件へ集約する

- `SaveSystem` / `SceneLoader` / `ServiceLocator` の `Initialize` から `CancellationToken` 引数と `token.Register(...)` を除去する
- `SymphonyOrchestrator` が `destroyCancellationToken` へ**1回だけ**登録し、キャンセル時に各サブシステムの `ResetRuntimeState` を**構築順の逆順**で呼ぶ
- 初期化したサブシステムを順序付きで記録する。初期化に失敗した場合は成功済みのものだけを逆順で解放する
- 1つの解放が例外を出しても残りを解放し、最後にまとめて記録する。終了処理から例外を外部へ再送出しない

現在の初期化順は `SaveSystem` → `PauseManager` → `ServiceLocator` → `SceneLoader` → `AudioManager` → `SymphonyDebugHUD`。解放はこの逆順になる。

### `ServiceLocateData` の自動初期化属性を除去する

`ServiceLocateData` の `#if UNITY_EDITOR` 内にある `[RuntimeInitializeOnLoadMethod]` は、`s_IsQuitting` のリセットと `Application.quitting` の再購読を Play Mode 開始ごとに行っている。

これを Orchestrator の初期化フェーズから明示的に呼ぶ形へ変える。購読解除も Orchestrator の終了処理で行い、二重購読を残さない。

## ファイル構成

| パス | 変更 |
| --- | --- |
| `Core/Internal/ISystemObjectFactory.cs`（新規） | GameObject 生成の契約 |
| `Runtime/Orchestrator/Internal/SystemObjectFactory.cs`（新規） | 実装。`DontDestroyOnLoad` を担当 |
| `Runtime/Orchestrator/Internal/SymphonyOrchestrator.cs` | factory の生成と注入、終了登録の1件化、逆順解放、`ServiceLocateData` の初期化呼び出し。`PreserveObject` / `CreateSystemObject` は Composition 内部へ閉じる |
| `Runtime/System/ServiceLocator/ServiceLocator.cs` | `Initialize` から token 引数を除去。生成済み `GameObject` を受け取って `ServiceLocateData` へ渡す |
| `Runtime/System/ServiceLocator/Internal/ServiceLocateData.cs` | コンストラクタで GameObject を生成せず受け取る。`[RuntimeInitializeOnLoadMethod]` を除去し、終了検知の初期化・解除を `internal` メソッドとして公開 |
| `Runtime/System/SceneLoader/SceneLoader.cs` | `Initialize` から token 引数と登録を除去 |
| `Runtime/System/SaveSystem/Internal/SaveSystem.cs` | 同上 |
| `Runtime/System/AudioManager.cs` | `PreserveObject` の呼び出しを factory 経由へ。遅延生成は維持。`ResetRuntimeState` を新設 |
| `Runtime/Debug/DebugHUD/SymphonyDebugHUD.cs` | `CreateSystemObject` の呼び出しを factory 経由へ。遅延生成は維持。`ResetRuntimeState` を新設 |
| `Runtime/System/PauseManager.cs` | `ResetRuntimeState` を新設（`_pause`・`_isInitialized`・`_pauseEventDictionary` を初期化） |

## 依存方向

```text
SymphonyOrchestrator（Composition）
        │ 生成・注入
        ├──> SystemObjectFactory（実装）
        │
        └──> 各サブシステム ──参照──> ISystemObjectFactory（Core / 契約）
                                              ^
                                              │ 実装
                                        SystemObjectFactory
```

サブシステムから `SymphonyOrchestrator` への参照は**ゼロになる**。実装後に全文検索で確認する。

## エラー処理

- factory が未注入のまま生成を要求された場合は `SymphonyNotInitializedException` を送出する。無言で null を返さない
- 終了処理中の例外は記録するのみで、外部へ再送出しない
- `ResetRuntimeState` は多重呼び出しされても安全にする

## 影響範囲

**破壊的変更ではない。** 公開APIのシグネチャ、シリアライズ形式、利用側から見た挙動は変わらない。

内部的な変更として、`Initialize` の `internal` シグネチャから `CancellationToken` 引数が消える。`InternalsVisibleTo` で参照している Editor 側に影響が出ないか確認する。

## テストの置き場と種別

**自動テストは追加しない。**

当初この設計書は PlayMode テストで解放順を検証するとしていたが、実現できないことが判明した。

- **テストアセンブリから `internal` へ届かない。** `Runtime/AssemblyInfo.cs` は `SymphonyFrameWork.Editor` にしか `InternalsVisibleTo` を与えていない。テストは公開APIの範囲に留める方針のため、これを広げない
- **解放順を観測する手段が無い。** 検証するには製品コードへテスト専用の観測フックを入れることになり、「テストのためだけの拡張点を作らない」という方針に反する
- **PlayMode テストは Play Mode 内で走るため、1つのテストの中で Play Mode を抜けて再入できない。** 「開始・終了を2回」は自動化できない
- `ISystemObjectFactory` が `internal` であるため、モックを差し込むテストも書けない

この Round で変わるのは内部構造であり、公開APIから観測できる差分がほとんど無い。無理に公開API経由のテストを作っても、検証しているのは既存の挙動になる。

既存の43件（EditMode）と4件（PlayMode）が引き続き成功することは確認する。**解放順と遅延生成の維持は下記の手動確認で担保する。**

## 動作確認手順

1. `uloop-compile` がエラー0・警告0
2. 既存テストが全数成功（EditMode 43 / PlayMode 4）
3. **Play Mode の開始・終了を2回繰り返し**、`ServiceLocator` にゴースト参照が残らないこと。`SymphonyMcpTools.GetServiceLocatorJson()` の `registrationCount` が2回目の開始直後に0であることで確認できる
4. 同じく `GetPauseJson()` の `IPausable` 購読件数が2回目の開始直後に0であること（`PauseManager.ResetRuntimeState` 新設の効果）
4. `AudioManager.GetAudioSource("BGM")` を初めて呼ぶまで `AudioManager` という名前の GameObject が生成されないこと（遅延生成の維持）
5. `SymphonyDebugHUD.Show()` を初めて呼ぶまで `SymphonyHUDDrawer` の GameObject が生成されないこと
6. `Runtime/` 配下を全文検索し、`SymphonyOrchestrator` への参照が Composition 内部以外に無いこと
7. Play Mode 終了時に `Application.quitting` の購読が残らないこと

## バージョン判断

**マイナー（2.9.0）。** 公開APIの追加・変更・削除が無く、内部構造の整理のみ。

## この Round で触るバージョン関連ファイル

| ファイル | 触る箇所 |
| --- | --- |
| `Assets/SymphonyFrameWork/package.json` | `version` を `2.8.0` → `2.9.0` |
| `Assets/SymphonyFrameWork/CHANGELOG.md` | `## [2.9.0]` の見出しと本文 |
| `Assets/SymphonyFrameWork/README.md` | 「現在のバージョン」を `2.9.0` へ |

`AGENTS.md` は触らない（利用側から見えるAPIが変わらないため）。
