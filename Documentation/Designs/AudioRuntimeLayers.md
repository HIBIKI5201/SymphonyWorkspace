# Audio のレイヤー分割 — Round J2

## 目的

`AudioManager`（235行）が、公開Facade・Configの解釈・AudioSourceの遅延生成・GameObjectの所有・音量のデシベル変換をすべて担っている。Scene Load / Service Locate / Save Data / Pause と同じ形へ揃える。

**Query と ViewModel は作らない。** Audio には Editor Window も MCP 診断も無く、読み取り経路の利用者がいない。DesignPhilosophy の「使用されていないクラス設計は作らない」に従う。Round J1 の設計書で予告したとおりだが、着手時にコードで再確認した（下記「前提の確認」）。

本 Round は 2.18.0 を含む `develop` から開始し、単独で検証・リリース可能な **2.18.1** とする。

## 前提の確認

着手時に実コードで確認した事実を記録する。

| 前提 | 確認結果 |
| --- | --- |
| Audio を表示する経路があるか | **無い。** Editor Window は5枚（Pause / ServiceLocator / SceneLoader / SaveDataRegistry / AutoEnumGenerator）で Audio は無い。`SymphonyMcpTools` の公開メソッドは4件で `GetAudioJson` は無い |
| `AudioManagerConfigDrawer` は状態表示か | **違う。** `[CustomEditor(typeof(AudioManagerConfig))]` の Config インスペクタで、enum 再生成ボタンを描くだけ |
| Round C が Audio を除外した理由 | `McpStateInspection.md` の対象は「Editor Window がリフレクションで覗いている4サブシステム」。Audio は Window が無いため対象外だった。見落としではない |
| Audio は Play Mode 専用か | **専用である。** `AudioManager.Initialize` の呼び出し元は `SymphonyOrchestrator.cs:61` だけ |
| GameObject の生成タイミング | **遅延生成である。** `CreateInstance()` は `AudioSourceInitialize()` の中でだけ呼ばれる。Round E で「Composition が事前に GameObject を作って渡す形にしない」と決めた制約であり、**本 Round でも維持する** |
| 既存テスト | Audio のテストは0件 |
| `ISystemObjectFactory` | `CreateObject(string)` と `CreateComponent<T>(string)` を持つ `internal` 契約。実装が `DontDestroyOnLoad` を担当する |

## 現状の問題

分割の動機になっている具体的な欠陥を挙げる。

1. **「構築済みかどうか」が `_audioDict != null` で表現されている。** フィールド初期化子は `new()` だが `ResetRuntimeState()` が `null` を代入し、`AudioSourceInitialize()` は `_audioDict != null` で早期 return する。「null なら未構築」という直感と逆の読み方が要る
2. **AudioMixer が未割り当てでも GameObject が生成される。** `AudioSourceInitialize()` は `CreateInstance()` を呼んでからミキサーを確認するため、警告を出して return したあとも空の `AudioManager` GameObject が `DontDestroyOnLoad` に残る
3. **デシベル変換の `80` がリテラルで埋め込まれている。** `value * (data.OriginalVolume.Value + 80) - 80` の `80` は AudioMixer の最小音量 -80dB を指すが、名前が無い
4. `AudioSettingData` が `private` の入れ子structであり、変換規則の置き場が無い

## 公開API

**変更しない。** `GetAudioSource(string)`、`VolumeSliderChanged(string, float)` のシグネチャ、例外の種類と条件をいずれも維持する。追加もしない。

`internal` の `Initialize(AudioManagerConfig, ISystemObjectFactory)` と `ResetRuntimeState()` のシグネチャも変更しない。Orchestrator を触らずに済む。

## 内部型

### `AudioGroupEntity`

`Runtime/System/Audio/Internal/Domain/AudioGroupEntity.cs`（`internal sealed`）。

現在の `private struct AudioSettingData` を置き換える。グループ名を同一性とし、再生元と音量変換の規則を持つ。

```csharp
internal sealed class AudioGroupEntity
{
    internal const float MINIMUM_VOLUME_DECIBEL = -80f;

    internal string GroupName { get; }
    internal AudioMixerGroup Group { get; }
    internal AudioSource Source { get; }
    internal string ExposedVolumeParameterName { get; }
    internal float? OriginalVolumeDecibel { get; }

    internal bool TryGetVolumeDecibel(float ratio, out float decibel);
}
```

`TryGetVolumeDecibel` が `ratio * (OriginalVolumeDecibel + 80) - 80` を担う。`OriginalVolumeDecibel` が `null`（公開パラメーターが見つからなかった）なら `false` を返す。現在 `VolumeSliderChanged` の中にある変換と分岐がここへ移る。

`MINIMUM_VOLUME_DECIBEL` は AudioMixer が扱える最小音量で、Unity の仕様に由来する定数である。

### `AudioGroupRegistry`

`Runtime/System/Audio/Internal/Application/AudioGroupRegistry.cs`（`internal sealed`）。

グループ名をキーに `AudioGroupEntity` を所有する。

- `bool IsBuilt { get; }` — 構築処理を実行済みかどうか。**結果が空でも実行済みとして扱う**（ミキサー未割り当てでの警告が毎回出るのを防ぐ、現在と同じ挙動）
- `void MarkBuilt()`
- `bool TryGet(string groupName, out AudioGroupEntity entity)`
- `void Add(AudioGroupEntity entity)`
- `int Count { get; }`
- `void Clear()` — 登録と構築済みフラグを消す

`IsBuilt` を明示することで、問題1の「null なら未構築」という逆転した読み方が無くなる。

### `IAudioSourceHost` と `AudioSourceHost`

`Runtime/System/Audio/Internal/Application/IAudioSourceHost.cs`（`internal interface`）と
`Runtime/System/Audio/Internal/Infrastructure/AudioSourceHost.cs`（`internal sealed`）。

Service Locate の `IServiceHost` / `ServiceHostComponent` と同じ役割で、GameObject と AudioSource の所有を Application から追い出す。

```csharp
internal interface IAudioSourceHost
{
    AudioSource CreateAudioSource();
    void Release();
}
```

`AudioSourceHost` は `ISystemObjectFactory` を受け取り、**最初の `CreateAudioSource()` で初めて GameObject を生成する。** これにより問題2（ミキサー未割り当てでも GameObject が残る）が解消する。AudioSource を1つも作らなければ GameObject も作られない。

`Release()` が GameObject を破棄する。

### `AudioService`

`Runtime/System/Audio/Internal/Application/AudioService.cs`（`internal sealed`）。

- constructor で `AudioManagerConfig`、`AudioGroupRegistry`、`IAudioSourceHost` を受け取る
- `AudioSource GetAudioSource(string groupName)` — 構築を保証してから検索する
- `void SetVolumeRatio(string groupName, float ratio)` — Entity へ変換を委譲し、AudioMixer へ反映する
- `void Reset()` — Host を解放し、Registry を消去する
- `private void EnsureGroupsBuilt()` — Config を読み、グループごとに Entity を作る（現在の `AudioSourceInitialize`）

Config の解釈と AudioMixer への問い合わせは Service に残る。`AudioMixer` は Unity のアセットであり、抽象化しても差し替える相手がいないため契約を増やさない。

## 公開Facade と Composition

```text
AudioManager
  Command ──> AudioService ──> AudioGroupRegistry ──> AudioGroupEntity
                    │
                    └────────> IAudioSourceHost ──> AudioSourceHost ──> ISystemObjectFactory
```

- `Initialize(config, systemObjectFactory)` が Registry、`AudioSourceHost`、Service を結合する。**`ISystemObjectFactory` は `AudioSourceHost` へ渡すだけになり、`AudioManager` は GameObject を持たない**
- `ResetRuntimeState()` は Service を Reset して全参照を null へ戻す。多重呼び出しで安全にする
- 引数の検証（グループ名が空、比率が0〜1の範囲外）は Facade に残す。現在と同じ例外を同じ条件で投げる

Orchestrator の初期化順と `AudioManager.Initialize` の入口は変更しない。

### ファイル移動

`Runtime/System/AudioManager.cs` を `Runtime/System/Audio/AudioManager.cs` へ移す。名前空間 `SymphonyFrameWork.System` は変更しない（公開型のため、変更は Phase 4）。Round J1 の `Pause/` と同じ扱いで、名前空間とフォルダの不一致はこの時点では残る。

`.meta` を `.cs` と一緒に移動し、GUID を維持する。

## 依存方向

```text
AudioManager ──Command──> AudioService ──> AudioGroupRegistry ──> AudioGroupEntity
                              │                                        │
                              ├──> IAudioSourceHost                    └──> AudioMixerGroup / AudioSource
                              │         ^
                              │         │
                              │    AudioSourceHost ──> ISystemObjectFactory ──> GameObject
                              │
                              └──> AudioManagerConfig / AudioMixer
```

Domain（`AudioGroupEntity`）は Unity の型（`AudioMixerGroup`、`AudioSource`）を保持するが、GameObject の生成・破棄には関与しない。Infrastructure（`AudioSourceHost`）だけが Unity のオブジェクト寿命を扱う。

## エラー処理

現在の挙動を維持する。

- 未初期化での公開API呼び出しは `SymphonyNotInitializedException`
- グループ名が null または空は `ArgumentException`
- 音量比率が0〜1の範囲外は `ArgumentOutOfRangeException`
- AudioMixer 未割り当ては `Debug.LogWarning` を1回出し、以降は構築済みとして扱う
- 未登録のグループ名は `GetAudioSource` が `null` を返す（例外にしない。現在と同じ）
- 公開パラメーターが見つからないグループの音量変更は `Debug.LogWarning` を出して何もしない

## 影響範囲

- 公開APIのシグネチャ、例外の種類と条件、シリアライズ形式はいずれも変更しない
- **挙動が1つ変わる（修正）**: AudioMixer が未割り当ての場合、`AudioManager` という名前の空の GameObject が `DontDestroyOnLoad` に生成されなくなる。AudioSource を1つも作らないため
- `AudioManager.cs` の移動は名前空間を変えないため、利用側の `using` に影響しない
- `AudioManagerConfig` は変更しない

## テストの置き場と種別

`Tests/Editor/` の EditMode テストとして追加する。

### `AudioGroupEntityTests`

`new AudioGroupEntity(...)` を直接生成する。`AudioMixerGroup` と `AudioSource` は `null` を渡してよい。**検証対象は音量変換の計算だけで、再生元には触れないため。**

- `TryGetVolumeDecibel(1f)` が元の音量をそのまま返すこと
- `TryGetVolumeDecibel(0f)` が `MINIMUM_VOLUME_DECIBEL` を返すこと
- 中間値が線形補間になること（元が0dBなら0.5で-40dB）
- `OriginalVolumeDecibel` が `null` なら `false` を返し、`decibel` に触れないこと
- グループ名が null の生成が `ArgumentNullException` になること

### `AudioGroupRegistryTests`

`new AudioGroupRegistry()` を直接生成する。

- 生成直後は `IsBuilt` が false、`Count` が0であること
- `Add` した Entity をグループ名で取得できること
- 未登録名の検索が false を返すこと
- `MarkBuilt` 後に `IsBuilt` が true になること
- **`Clear` で `IsBuilt` が false へ戻ること**（Play Mode をまたいで再構築されるため）
- 同名の再追加が後勝ちにならず拒否されること

### `AudioService` のテストは追加しない

**理由: `AudioMixer` を単体テストから用意できない。** `AudioMixer` は Unity のアセットで、`new` や `ScriptableObject.CreateInstance` では実用的に作れず、グループも `FindMatchingGroups` でアセットから引く必要がある。テスト用の `.mixer` アセットをパッケージへ置くと利用側のプロジェクトへ同梱されるうえ、生成にも Editor の内部型が要る。

`AudioService` の構築処理と音量反映は **Audio Manager Sample での手動確認**に回す。分割によって計算規則（`AudioGroupEntity`）と保持（`AudioGroupRegistry`）はテストで固定できるため、手動確認に残るのは「Config を正しく読めているか」「AudioMixer へ反映されるか」の2点になる。

## 動作確認手順

1. Unity Scene 検証ガードに従い、親と submodule の dirty 状態を記録する
2. `uloop-clear-console` 後に `uloop-compile` を実行し、Error 0、SymphonyFrameWork 由来の Warning 0 を確認する
3. `uloop-clear-console` を挟んで EditMode と PlayMode の全テストを実行し、**同じ結果が2回続くことを確認する**
4. Audio Manager Sample を Play し、`GetAudioSource` で AudioSource を取得して再生できることを確認する
5. Volume スライダーで音量が変わること、AudioMixer の公開パラメーターが更新されることを確認する
6. **AudioMixer を未割り当てにした Config で Play し、警告が1回だけ出て、`AudioManager` という GameObject が生成されないことを確認する**（本 Round の修正点）
7. Play Mode の開始・終了を2回繰り返し、2回目に前回の AudioSource と GameObject が残らないことを確認する
8. Console の Error / Exception が0件であることを確認する
9. Play Mode 停止後に package の `.unity` / `.prefab` 差分が無いことを確認する
10. `git status` で `AudioManager.cs` の移動が rename として記録され、`.meta` が対で動いていることを確認する
11. この Round で追加・変更した `.cs` に UTF-8 BOM が付いていることを確認する

## バージョン判断

**パッチ（2.18.1）。** DesignPhilosophy の `### バージョニング` に「後方互換な公開API追加はマイナー、公開契約を変えない修正はパッチ」「`internal`／`private`だけの変更にバージョニング上の制約は課さない」とある。本 Round は公開APIを追加も変更もせず、内部構造の分割と、ミキサー未割り当て時の不要な GameObject 生成の修正だけである。

## この Round で触るバージョン関連ファイル

| ファイル | 変更 |
| --- | --- |
| `Assets/SymphonyFrameWork/package.json` | `2.18.0` → `2.18.1` |
| `Assets/SymphonyFrameWork/CHANGELOG.md` | 2.18.1 へレイヤー分割と GameObject 生成の修正を記録 |
| `Assets/SymphonyFrameWork/README.md` | 現在のバージョン |
| `Assets/SymphonyFrameWork/Documentation~/Architecture.md` | Audio の内部構成図を追加 |

`Documentation~/AgentUsage.md` は変更しない。公開APIと利用側の制約が変わらないため。

## ブランチ

`develop` から `feature/audio-runtime-layers` を作成する。

## 実装後の記録

### 実装中に見つけた既存バグ（設計書に無かった修正）

`AudioSettingData` の生成が `new AudioSettingData(group, source, name, volume ?? 0)` となっており、**`OriginalVolume` に null が入ることが無かった。** そのため `VolumeSliderChanged` の

```csharp
if (data.OriginalVolume == null) { Debug.LogWarning($"{name}のボリュームがありません"); return; }
```

は到達不能で、公開パラメーターが見つからないグループでも「初期音量0dB」として扱われ、存在しないパラメーター名で `SetFloat` が呼ばれていた（Unity 側で黙って失敗する）。

分割にあたり `volume` を nullable のまま保持するよう直した。これにより上記の警告が到達可能になる。**設計書の `TryGetVolumeDecibel` が false を返す条件は、この修正を前提として書いていた**（設計時にはバグと認識しておらず、意図した仕様として書いていた）。回帰テストで固定した。

観測される差は「公開パラメーターが設定されていないグループの音量を変えたとき、警告が出るようになる」だけである。従来も音量は変わっていなかった。

### 音量変換式の書き換え

`value * (OriginalVolume + 80) - 80` を `ratio * (Original - MINIMUM) + MINIMUM` へ書き換えた。`MINIMUM_VOLUME_DECIBEL = -80f` を代入すると恒等であることを確認している。リテラルの `80` が2箇所に散っていたのを定数1つへ寄せるため。

### テスト実行の不安定さ（再掲）

Round J1 で記録した現象がここでも出た。**リコンパイル直後の初回実行だけ `Success=False` になり、失敗件数は0だった。** 2回目・3回目はいずれも `Success=True` で 222/222。`references/review.md` の「同じ結果が2回続くことを確認してから合格と判断する」に従って合格とした。

## この Round で扱わないもの

- **MCP 診断（`GetAudioJson`）の追加。** Audio の状態（構築済みグループ、音量）は調べられると有用だが、公開APIまたは Editor 専用 API の追加にあたり、本 Round のバージョン判断（パッチ）と整合しない。必要なら独立した Round で扱う
- `AudioManager` の改名と名前空間の変更（Phase 4）
- `AudioManagerConfig` の構造変更
