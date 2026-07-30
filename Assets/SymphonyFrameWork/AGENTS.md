# AGENTS.md — Symphony Framework 利用ガイド（AIエージェント向け）

このファイルは、本パッケージ（`SymphonyFrameWork`）をUPM経由で導入した**プロジェクト**でコードを書くAIエージェント（Claude Code、Cursor等）向けの実務ガイドです。
パッケージは通常 `Packages/symphonyframework/` （または `Assets/SymphonyFrameWork/` としてソース導入）に配置されます。このファイル自体もそのままそこに同梱されるので、プロジェクトを触る前に必ず読んでください。

人間向けの機能一覧・詳細クイックスタートは [README.md](./README.md) にあります。本ファイルはそれを前提に、**エージェントが実装・検証する際に踏みがちな誤りと、動作確認の手順**に絞っています。

## 0. 最初に確認すること

1. `Packages/manifest.json` または `Assets/SymphonyFrameWork/package.json` を見て、実際に導入されているバージョンを確認する（READMEやこのファイルの記述が古い可能性がある）。
2. 消費者側の asmdef に **`SymphonyFrameWork`（Runtimeアセンブリ）への参照**があるか確認する。無ければ追加が必要。
3. 自動生成enumを使う場合のみ `SymphonyFrameWork.Enum` も参照に追加する（後述）。
4. `internal` なクラス（`SceneManagerConfig` / `AudioManagerConfig` / `SaveSystemConfig` など設定ScriptableObject本体）はコンシューマー側から直接 `new` したり型として参照したりできない。設定はInspector経由でしか触れない前提でコードを書く。
5. パッケージ直下（`Assets/SymphonyFrameWork/` または `Packages/symphonyframework/`）に自動生成される `Cache/Log.txt` は、`SymphonyDebugLogger.LogDirect` 経由のログをEditorが永続化キャッシュしたものであり、削除しても実害はない。手動編集・バージョン管理への追加は不要（`.gitignore` 済み）。

## 1. アセンブリ／namespace 早見表

| namespace | 主なAPI | 用途 |
| --- | --- | --- |
| `SymphonyFrameWork.System.ServiceLocate` | `ServiceLocator`, `ServiceInjector`, `LocateType`, `ServiceNotRegisteredException` | Service LocatorのFacade、Value Object、必須サービス取得時の例外 |
| `SymphonyFrameWork.System.SceneLoad` | `SceneLoader`, `SceneLoadState`, `SceneInitializationException` | シーンロードのFacade、Value Object、シーン初期化時の例外 |
| `SymphonyFrameWork.System.SaveSystem` | `SaveDataRegistry`, `SaveDataContent`, `SaveDataLoader`, `SaveDataOperationException` | セーブデータのFacade、基底クラス／拡張点、操作失敗時の例外 |
| `SymphonyFrameWork.System` | `AudioManager`, `PauseManager` | どのサブシステムにも属さないFacade |
| `SymphonyFrameWork` | `IInjectable<T...>`, `IInitializeAsync` | DI用インターフェース |
| `SymphonyFrameWork.Utility` | `SymphonyLocate`, `SymphonyTask`, `SymphonyTween` | 補助コンポーネント／ユーティリティ |
| `SymphonyFrameWork.Attribute` | `[ReadOnly]`, `[SubclassSelector]`, `[SceneNameSelector]`, `[TagSelector]` 等 | Inspector拡張属性 |
| `SymphonyFrameWork.Exceptions` | `SymphonyNotInitializedException` | Facadeを初期化前に使用した場合の共通例外 |
| `SymphonyFrameWork.Editor` (Editor専用) | `SymphonyAdministrator` 他 | エディタツール。Runtimeコードから参照不可 |

主要APIは**すべて `public static class`**（`ServiceLocator` / `SceneLoader` / `AudioManager` / `PauseManager` / `SaveDataRegistry`）。Facadeは自分が属するサブシステムの名前空間にあるため、1つのサブシステムを使うのに必要な `using` は1つだけ。インスタンス化やシングルトンの `.Instance` パターンは存在しない。`XxxManager.Instance` のようなコードを書いたら誤り。

> バージョン2.2.0〜2.2.1では、これらのFacadeが `SymphonyFrameWork.System.API` 名前空間に集約されていた。2.3.0でサブシステムごとの名前空間へ戻したため、`using SymphonyFrameWork.System.API;` を含むコードはコンパイルできない。上の表に従って `using` を張り替えること（クラス名とメンバーは変わっていない）。

初期化は `SymphonyOrchestrator`（internal, `[RuntimeInitializeOnLoadMethod]`）が最初のシーンより前に自動実行し、管理GameObjectを `DontDestroyOnLoad` で永続化する。**Bootstrap用GameObjectや専用シーンを手動で用意する必要はない**。逆に、これらのstaticクラスを `Awake` より前（エディタの `InitializeOnLoad` など）で呼び出すのは避ける。

## 2. ボイラープレートと作法

### 2.1 Service Locator — 登録は必ず対で行う

```csharp
using SymphonyFrameWork.System.ServiceLocate;
using UnityEngine;

public sealed class GameSession : MonoBehaviour
{
    private void OnEnable() => ServiceLocator.RegisterInstance(this, LocateType.Locator);
    private void OnDisable() => ServiceLocator.UnregisterInstance(this);
}
```

- `LocateType.Locator`: 参照登録のみ。GameObjectの階層は変更されない。
- `LocateType.Singleton`: Componentの場合、管理オブジェクト配下へ**移動**する（シーン破棄から切り離される）。シーンローカルに留めたいオブジェクトには使わない。
- 存在が任意なら `TryGetInstance<T>`、未登録時のnullを扱う既存コードでは `GetInstance<T>`、必須依存なら `GetRequiredInstance<T>` を使う。必須サービスが未登録なら `ServiceNotRegisteredException` が発生する。
- `GetInstanceAsync<T>` の期限超過は `TimeoutException`、呼び出し側キャンセルは `OperationCanceledException`。`TryGetInstanceAsync<T>` がfalseへ変換するのは期限超過だけなので、キャンセルは呼び出し側で処理する。
- シーンロードで生成されるルートオブジェクトへの注入は `IInjectable<T0..T3>` を実装すれば `SceneLoader` が自動で行う。`Instantiate` で動的生成したオブジェクトには **`ServiceInjector.Inject(...)` を手動で呼ぶこと**（自動注入されない）。

**アンチパターン**: `OnEnable`/`OnDisable` の片方だけ登録・解除を書く（Unregisterし忘れるとシーン跨ぎでゴースト参照が残る）。`RegisterInstance` を `Start` で呼び `OnDestroy` で解除しない、なども同様に漏れの原因。

### 2.2 Scene Loader — Build Settingsへの追加が前提

```csharp
using SymphonyFrameWork.System.SceneLoad;
using UnityEngine.SceneManagement;

public async void OpenGameScene()
{
    bool ok = await SceneLoader.LoadScene("Game", mode: LoadSceneMode.Additive,
        priority: 10, token: destroyCancellationToken);
    if (ok) SceneLoader.SetActiveScene("Game");
}
```

- ロード対象シーンが **File > Build Settings > Scenes In Build** に無いと失敗する。エージェントはシーン名を書く前に `EditorBuildSettings.scenes` に含まれているか確認するか、ユーザーに追加を促すこと。
- `LoadSceneMode.Single` は対象シーンをAdditiveでロードした後、それ以外の追跡シーンをアンロードする。フレームワークの管理オブジェクトは `DontDestroyOnLoad` のため保持される。現在のシーンを残す場合は `Additive` を使う。
- ロードしたシーンのルートに `IInitializeAsync` を実装すると、その完了を `LoadScene` が待ってから成功を返す。重い初期化をルートで行うならこれを使う（`Start()` 内の非同期処理を呼び出し側で別途待つ必要はない）。
- ルートへの依存注入または `IInitializeAsync` が失敗すると `SceneInitializationException` が発生する。`SceneName`、`GameObjectName`、`InitializerType` と `InnerException` を診断に使う。Build Settings未登録などの通常のロード失敗は従来どおりfalse。

**アンチパターン**: `SceneManager.LoadSceneAsync` をUnity標準APIで直接呼ぶ（フレームワークの優先度管理・IInjectable注入・SceneLoadStateが効かなくなる）。本パッケージ導入後は、シーン遷移は原則 `SceneLoader` 経由に統一する。

### 2.3 Save Data System — 抽象クラスを継承した具象classを定義

```csharp
using System;
using SymphonyFrameWork.System.SaveSystem;

[Serializable]
public sealed class PlayerData : SaveDataContent
{
    public int Level = 1;
    public int Gold;
}
```

```csharp
PlayerData data = SaveDataRegistry.Get<PlayerData>(); // 初回は同期ロード
data.Gold += 100;
await SaveDataRegistry.SaveAsync<PlayerData>();
```

- `SaveDataContent` の派生classは**デフォルトコンストラクタが必須**（`Get<T>() where T : SaveDataContent, new()`）。
- カスタム保存先（ファイル・クラウド等）が要る場合は `SaveDataLoader` を継承し `ExistsCore` / `LoadJsonAsync` / `SaveJsonAsync` / `DeleteCoreAsync` / `SerializeToJson` / `OverwriteFromJson` を実装する。実装したクラスは自動的に `Project Settings > SymphonyFrameWork > Save System` のドロップダウンに現れる（`[SerializeReference, SubclassSelector]` 経由）ので、ScriptableObjectアセットを手動生成する必要はない。
- 非同期I/Oを行う独自ローダーを使う場合は、メインスレッドをブロックしないよう `await SaveDataRegistry.LoadAsync<T>()` を先に呼んでから `Get<T>()` する。
- ローダーまたは保存先で操作が失敗すると `SaveDataOperationException` が発生する。`Operation`、`DataType`、`LoaderType`、`InnerException` を確認する。キャンセルはラップされず `OperationCanceledException` のまま伝播する。破損JSONをデフォルト状態へ戻す既存の復旧動作は例外に変更されていない。

**アンチパターン**: `SaveDataContent` を継承したclassにコンストラクタ引数を必須にする（`new()` 制約違反でコンパイルエラー）。`Get<T>()` を呼ばずに独自にインスタンスを保持して保存・ロードのタイミングをずらす（Registryのキャッシュと二重管理になる）。

### 2.4 Audio Manager — 事前にAudioMixer/グループ登録が必須

```csharp
using SymphonyFrameWork.System;

AudioSource bgm = AudioManager.GetAudioSource("BGM");
bgm.clip = bgmClip;
bgm.Play();
AudioManager.VolumeSliderChanged("BGM", 0.5f); // 0.0〜1.0の比率 → dBへ変換される
```

- `"BGM"` のようなグループ名は、事前に `Assets/Resources/SymphonyFrameWork/AudioManagerConfig.asset` にAudioMixerと共に登録されている必要がある。未登録の名前で `GetAudioSource` を呼ぶと取得できない。コードを書く前に、そのグループがConfigに存在するかユーザーに確認する。
- `VolumeSliderChanged` の引数は**0〜1の比率**であり、dB値をそのまま渡さない。

### 2.5 Pause Manager

```csharp
using SymphonyFrameWork.System;

PauseManager.OnPauseChanged += paused => { /* UI更新 */ };
PauseManager.Pause = true;
await PauseManager.PausableWaitForSecondAsync(1.0f, destroyCancellationToken);
```

- ポーズ中に停止させたい待機処理は `Task.Delay` や標準 `WaitForSeconds` ではなく `PausableWaitForSecondAsync` / `PausableWaitForSecond`（Coroutine版）/ `PausableNextFrameAsync` を使う。標準APIを使うとポーズが効かない。
- オブジェクト単位でPause/Resumeの通知を受けたい場合は `PauseManager.IPausable` を実装し、有効化時に `IPausable.RegisterPauseManager(this)`、無効化時に `UnregisterPauseManager(this)` を呼ぶ（登録漏れ・解除漏れに注意、Service Locatorと同様のペアパターン）。

## 3. 動作確認・テスト手順（エージェントが「試す」ための手順）

コンシューマー側プロジェクトの `Assets/` 配下（パッケージ本体ではない場所）に検証用スクリプトを置いて確認する。パッケージのソース自体は編集しないこと（Git URL経由の場合は読み取り専用）。

### 3.1 最小疎通確認（コンパイル・namespace解決）

1. `Assets/Scripts/_SymphonyVerify/` のようなフォルダを作成。
2. 以下のようなEditorスクリプトを置き、`Window` メニューまたは `[MenuItem]` から実行してAPIが解決できることを確認する:

```csharp
// Assets/Scripts/_SymphonyVerify/Editor/SymphonyVerifyMenu.cs
using SymphonyFrameWork.System;
using SymphonyFrameWork.System.SaveSystem;
using SymphonyFrameWork.System.SceneLoad;
using SymphonyFrameWork.System.ServiceLocate;
using UnityEditor;
using UnityEngine;

internal static class SymphonyVerifyMenu
{
    [MenuItem("Tools/SymphonyVerify/Log API Availability")]
    private static void LogApiAvailability()
    {
        Debug.Log($"[SymphonyVerify] ServiceLocator type: {typeof(ServiceLocator).FullName}");
        Debug.Log($"[SymphonyVerify] SceneLoader type: {typeof(SceneLoader).FullName}");
        Debug.Log($"[SymphonyVerify] AudioManager type: {typeof(AudioManager).FullName}");
        Debug.Log($"[SymphonyVerify] SaveDataRegistry type: {typeof(SaveDataRegistry).FullName}");
        Debug.Log("[SymphonyVerify] OK: assembly reference resolved.");
    }
}
```

期待される結果: コンパイルエラーなし。メニュー実行でエラーなく4行＋OKログが出る。エラーが出る場合は asmdef 参照漏れ（`SymphonyFrameWork` 未参照）が最有力原因。

### 3.2 設定アセット・自動生成コードの存在確認

Playモードに一度入るか、スクリプトコンパイルを一度発生させたあと、以下が生成されているか確認する:

```text
Assets/Resources/SymphonyFrameWork/SceneManagerConfig.asset
Assets/Resources/SymphonyFrameWork/AudioManagerConfig.asset
Assets/Resources/SymphonyFrameWork/SaveSystemConfig.asset
Assets/Scripts/SymphonyFrameWork/SceneListEnum.cs 等
```

無ければ、`Window > SymphonyFrameWork > Symphony Administrator` を一度開くか、Playモードに入ることでトリガーされる（`SymphonyOrchestrator` の `RuntimeInitializeOnLoadMethod`／Editor側の生成処理）。存在しない場合、コードから設定を参照するAPI呼び出し（Audioグループ登録など）は動かない。

### 3.3 ランタイム動作確認（Play Mode / テストシーン）

各サンプル（`Samples/Runtime/*Sample/`）をPackage Managerからプロジェクトへインポートし、そのままPlayすることで各システムの実動作を確認できるのが最短経路。エージェントが独自に確認する場合は、`Assets/` 配下に最小のテスト用シーン＋スクリプトを作り、以下のログパターンを期待値とする:

```csharp
// Assets/Scripts/_SymphonyVerify/SymphonyVerifyRuntime.cs
using System;
using SymphonyFrameWork.System;
using SymphonyFrameWork.System.SaveSystem;
using SymphonyFrameWork.System.ServiceLocate;
using UnityEngine;

public sealed class SymphonyVerifyRuntime : MonoBehaviour
{
    [Serializable]
    private sealed class VerifyData : SaveDataContent
    {
        public int Counter;
    }

    private async void Start()
    {
        // Service Locator
        ServiceLocator.RegisterInstance(this, LocateType.Locator);
        bool found = ServiceLocator.TryGetInstance<SymphonyVerifyRuntime>(out _);
        Debug.Log($"[SymphonyVerify] ServiceLocator round-trip: {found}"); // 期待値: True

        // Pause Manager
        PauseManager.Pause = true;
        Debug.Log("[SymphonyVerify] Pause set. Waiting 0.2s pausable...");
        await PauseManager.PausableWaitForSecondAsync(0.2f, destroyCancellationToken);
        PauseManager.Pause = false;
        Debug.Log("[SymphonyVerify] Pausable wait completed after unpausing."); // Pause中は進まないことを目視/ログのタイムスタンプで確認

        // Save Data System
        VerifyData data = SaveDataRegistry.Get<VerifyData>();
        data.Counter++;
        await SaveDataRegistry.SaveAsync<VerifyData>();
        await SaveDataRegistry.DeleteAsync<VerifyData>(); // 後片付け
        Debug.Log($"[SymphonyVerify] Save/Delete cycle done. Counter was {data.Counter}");
    }

    private void OnDestroy() => ServiceLocator.UnregisterInstance(this);
}
```

期待されるコンソール出力の順序:

```
[SymphonyVerify] ServiceLocator round-trip: True
[SymphonyVerify] Pause set. Waiting 0.2s pausable...
[SymphonyVerify] Pausable wait completed after unpausing.
[SymphonyVerify] Save/Delete cycle done. Counter was 1
```

`ServiceLocator round-trip` が `False` になる場合はSymphonyOrchestratorの初期化が完了する前にコードが走っている（別シーンの `Awake` が早すぎる等）ことを疑う。Save/Delete cycleでエラーが出る場合はSave System設定（ローダー選択）かPlayerPrefsの権限を確認する。

### 3.4 ビルド時の確認

実機/スタンドアロンビルドを行う場合、フレームワークの管理オブジェクトはランタイムで生成され `DontDestroyOnLoad` で保持されるため、管理用シーンのBuild Settings登録は不要。`SceneLoader` でロードする**遷移先シーン**は必ずBuild Settingsに含める。ビルドログで `Scene 'X' couldn't be loaded` 系のエラーが出たら、まずこのシーン未登録を疑う。

### 3.5 検証用コードの後始末

検証が終わったら `Assets/Scripts/_SymphonyVerify/` 配下は削除する。パッケージ本体（`Packages/symphonyframework/` または `Assets/SymphonyFrameWork/`）には一切書き込まない。

## 4. やってはいけないこと（まとめ）

- 設定用 `internal` ScriptableObject（`SceneManagerConfig` 等）を型として参照・`CreateInstance` しようとする → コンパイルエラー。設定はInspector/Project Settings経由のみ。
- `AudioManager` / `SceneLoader` / `ServiceLocator` / `PauseManager` / `SaveDataRegistry` を `new` する、または `.Instance` プロパティを探す → 存在しない。すべて `static class`。
- `Register~` 系APIを呼んでおいて対応する `Unregister~` を書かない。
- `SceneManager.LoadScene` を直接使ってフレームワークのシーン管理をバイパスする。
- Editorアセンブリ（`SymphonyFrameWork.Editor`）のクラスをRuntimeコード（ビルド対象のasmdef）から参照する → ビルドエラー。Editor拡張は必ず `Editor` フォルダ配下・Editor専用asmdefに置く。
- `Assets/SymphonyFrameWork`（またはPackages配下のパッケージ本体）を直接編集する。カスタマイズは必ずコンシューマー側 `Assets/` の継承・拡張（例: `SaveDataLoader` の継承）で行う。
