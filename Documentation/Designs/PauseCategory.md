# ポーズのカテゴリー化（Issue #168）

## 目的

**今は「ポーズ中か否か」が真偽値1つしかなく、止めたいものだけを止められない。**
`PauseStateEntity` が `bool IsPaused` を1つ持ち、`PauseManager.Pause = true` は
登録済みの `IPausable` を全部止める。

実際には「UIは動かしたままゲームプレイだけ止める」「カットシーン中は入力だけ止める」のように、
**止める対象を分けたい**。これを型で表す。カテゴリーは `IPausable` を継承した空の interface で、
ポーズ状態は `Type` をキーにした辞書で管理し、状態の変更は型パラメータで行う。

## 設計の骨格

### カテゴリーは型、状態は型ごと

```csharp
// 利用側が定義する（Round 4 で SettingsProvider から自動生成できるようにする）
public interface IGameplayPausable : PauseManager.IPausable { }
public interface IUiPausable : PauseManager.IPausable { }

// 止める対象は、属したいカテゴリーを実装する
public sealed class Enemy : MonoBehaviour, IGameplayPausable
{
    public void Pause() { }
    public void Resume() { }
}

// 状態の操作は型パラメータで行う
PauseManager.SetPause<IGameplayPausable>(true);
bool paused = PauseManager.IsPaused<IGameplayPausable>();
```

### 複数カテゴリーの合成は OR、再開は全解除後

対象が複数のカテゴリーを実装できる。**どれか1つでもポーズなら `Pause()` を呼び、
すべて解除されたときだけ `Resume()` を呼ぶ。**

対象ごとに「現在ポーズ中のカテゴリー数」を持ち、`0 → 1` の遷移で `Pause()`、
`1 → 0` の遷移で `Resume()` を通知する。**件数で持たないと、2カテゴリーがポーズ中に
片方だけ解除したときへ誤って `Resume()` を通知する。**

### 「全部止める」はカテゴリーではなく操作

`SetPauseAll(bool)` を用意する。**全カテゴリーを表す型を作らない。**
そのような型を作ると、利用側が「全体カテゴリーを実装するべきか」を毎回考えることになり、
かつ全体カテゴリー自体のポーズ状態と個別カテゴリーの状態の関係を定義する必要が生じる。
操作にすれば、状態は常に「カテゴリーごとの真偽値」だけで済む。

`IsPausedAny()` は「どれか1つでもポーズ中か」を返す。

### `IPausable` を直接実装した対象

**カテゴリーを1つも実装していない対象は登録時に `ArgumentException` にする。**
黙って「どのカテゴリーにも属さない＝絶対に止まらない」対象になると、
ポーズしても動き続ける原因が分からない。

## 公開API

`PauseManager`（`SymphonyFrameWork.System`、`public static`）へ追加する。

| シグネチャ | 内容 |
| --- | --- |
| `static void SetPause<TCategory>(bool)` | カテゴリーのポーズ状態を設定する |
| `static bool IsPaused<TCategory>()` | カテゴリーのポーズ状態を返す |
| `static void SetPauseAll(bool)` | 登録済みの全カテゴリーへ設定する |
| `static bool IsPausedAny()` | どれか1つでもポーズ中なら true |
| `static event Action<bool> OnPauseChanged<TCategory>` | **不可。** event はジェネリックにできない（→ 下記） |
| `static void AddPauseChangedHandler<TCategory>(Action<bool>)` | 上記の代替 |
| `static void RemovePauseChangedHandler<TCategory>(Action<bool>)` | 同上 |
| `static PauseInfo GetPauseInfo<TCategory>()` | カテゴリー単位の管理状態 |
| `static PauseInfo GetPauseInfo()` | 全体の管理状態（`IsPausedAny` と総購読件数） |

`TCategory` の制約は `where TCategory : PauseManager.IPausable` とする。
**`IPausable` そのものを型引数に渡せてしまうため、実行時に「`IPausable` 自身は
カテゴリーにできない」と検証する。** 制約だけでは表現できない。

> **`event` はジェネリックにできない。** C# の `event` はメンバーであり、型パラメータを
> 持てるのはメソッドだけである。既存の `OnPauseChanged` は `add`/`remove` を持つ
> event プロパティなので、カテゴリー版は `AddPauseChangedHandler<T>` /
> `RemovePauseChangedHandler<T>` メソッドとして出す。**既存の Service には同名の
> メソッドが既にあり、命名はそこへ揃う。**

### 待機系APIのカテゴリー対応

型パラメータ版を追加し、既存の非ジェネリック版は `[Obsolete]` にする。

| 既存（Obsolete化） | 追加 |
| --- | --- |
| `PausableNextFrameAsync` | `PausableNextFrameAsync<TCategory>` |
| `PausableWaitForSecond` | `PausableWaitForSecond<TCategory>` |
| `PausableWaitForSecondAsync` | `PausableWaitForSecondAsync<TCategory>` |
| `PausableWaitUntil` | `PausableWaitUntil<TCategory>` |
| `PausableDestroy` | `PausableDestroy<TCategory>` |
| `PausableInvoke` | `PausableInvoke<TCategory>` |
| `Pause`（プロパティ） | `SetPause<T>` / `IsPaused<T>` / `SetPauseAll` / `IsPausedAny` |
| `OnPauseChanged`（event） | `AddPauseChangedHandler<T>` / `RemovePauseChangedHandler<T>` |

**Obsolete にした既存APIは「全カテゴリーのいずれかがポーズ中か」（`IsPausedAny`）で
動き続ける。** 削除まではこれまでどおり動く。

### `SymphonyTween`

`SymphonyTween` は `PauseManager.Pause` を直接読んでいる（`Runtime/Utility/SymphonyTween.cs:80`）。
カテゴリーを受け取るオーバーロードを追加し、既存シグネチャは `IsPausedAny()` を見る形へ内部を差し替える。
**Tween の公開シグネチャは変えない。** 変えると `[Obsolete]` の対象が Tween まで広がる。

## ファイル構成

| パス | 変更 | 名前空間 |
| --- | --- | --- |
| `Runtime/Service/Pause/Internal/Domain/PauseStateEntity.cs` | 変更。`bool` → `Dictionary<Type, bool>` | `SymphonyFrameWork.System` |
| `Runtime/Service/Pause/Internal/Domain/PauseCategoryResolver.cs` | **新規。** 対象の型から属するカテゴリー型を解決する純粋関数 | 同上 |
| `Runtime/Service/Pause/Internal/Application/PausableRegistry.cs` | 変更。カテゴリーと保持数を持つ | 同上 |
| `Runtime/Service/Pause/Internal/Application/PauseService.cs` | 変更。カテゴリー単位の通知 | 同上 |
| `Runtime/Service/Pause/Internal/Adaptor/PauseQuery.cs` | 変更 | 同上 |
| `Runtime/Service/Pause/Internal/Adaptor/PauseDto.cs` | 変更。カテゴリー別の一覧を持つ | 同上 |
| `Runtime/Service/Pause/Internal/View/PauseViewModel.cs` | 変更 | 同上 |
| `Runtime/Service/Pause/PauseInfo.cs` | 変更。カテゴリー情報を持つ | 同上 |
| `Runtime/Service/Pause/PauseManager.cs` | 変更。公開APIの追加とObsolete化 | 同上 |
| `Runtime/Utility/SymphonyTween.cs` | 変更。カテゴリー版オーバーロード | `SymphonyFrameWork.Utility` |
| `Editor/Generator/PauseCategoryGenerate/PauseCategoryGenerator.cs` | **新規。** カテゴリー interface の生成 | `SymphonyFrameWork.Editor` |
| `Editor/Configs/ConfigData/PauseCategoryConfig.cs` | **新規。** カテゴリー名の保持 | 同上 |
| `Editor/SettingProvider/PauseCategorySettingProvider.cs` | **新規。** Project Settings の入力欄 | 同上 |
| `Editor/Administrator/UITK/CS/PauseWindow.cs` | 変更。カテゴリー別表示 | 同上 |
| `Editor/Administrator/UITK/UXML/PauseWindow.uxml` | 変更。ListView 追加 | — |

## 依存方向

`PauseCategoryResolver` は Domain に置き、`System.Type` と `System.Reflection` にだけ依存する。
**Unity API へ触れないため EditMode テストから直接検証できる。**

### 生成先のアセンブリ（着手前に検証済み・設計の制約）

**生成したカテゴリー interface を、既存の自動生成先 `SymphonyFrameWork.Enum` へ置くことはできない。**

- `Assets/Scripts/SymphonyFrameWork/SymphonyFrameWork.Enum.asmdef` は `"references": []`
- `Assets/SymphonyFrameWork/SymphonyFrameWork.asmdef` が `SymphonyFrameWork.Enum` を参照する
  （`PackageInitializer` が `AssemblyGenerator.AddAsssemblyReference` で注入する）
- カテゴリー interface は `PauseManager.IPausable` を継承するため `SymphonyFrameWork` の参照が要る
- **したがって `SymphonyFrameWork.Enum` へ置くと参照が循環する**

生成先は **`Assets/Scripts/SymphonyFrameWork/PauseCategory/` に新しい
`SymphonyFrameWork.PauseCategory.asmdef`** を作り、`SymphonyFrameWork` を参照させる。
**フレームワーク側はこのアセンブリを参照しない。** `SetPause<TCategory>` はジェネリックであり、
フレームワークが具体的なカテゴリー型を名指しする必要が無いため、片方向で成立する。

```text
SymphonyFrameWork.Editor ──> SymphonyFrameWork ──> SymphonyFrameWork.Core
                                   │                        ▲
                                   └──> SymphonyFrameWork.Enum
                                                            │
        SymphonyFrameWork.PauseCategory（自動生成）──────────┘
                    （利用側のコードだけが参照する）
```

## エラー処理

| 状況 | 扱い |
| --- | --- |
| `TCategory` に `IPausable` 自身を渡した | `ArgumentException`。カテゴリーとして使えない |
| カテゴリーを1つも実装しない対象を `Register` | `ArgumentException`。止まらない対象が黙って生まれるのを防ぐ |
| `Register(null)` / `Unregister(null)` | `ArgumentNullException`（現状維持） |
| 未登録カテゴリーの `IsPaused<T>()` | **例外にしない。** `false` を返す。まだ誰も止めていない状態と区別する意味が無い |
| 初期化前の呼び出し | `SymphonyNotInitializedException`（現状維持） |
| 購読者の例外 | そのまま伝播（現状維持）。表示側の例外だけ握って `SymphonyDebugLogger` へ |

## 影響範囲

- **既存の公開API は削除しない。** `[Obsolete]` を付け、`IsPausedAny()` 相当で動かし続ける
- `Deprecations.md` へ Obsolete にした8件を追記する。削除予定は「未定」
- シリアライズ形式への影響は無い
- 破壊的変更は `PauseInfo` の構造。`readonly struct` へカテゴリー情報を足すため、
  `PauseInfo(bool, int)` の `internal` コンストラクタが変わる。**公開されているのは
  プロパティだけなので、利用側のコンパイルは壊れない**（プロパティは追加のみ）
- サンプル `PauseManagerSample_Mover` はカテゴリーを実装する形へ書き換える

## テストの置き場と種別

すべて `Assets/SymphonyFrameWork/Tests/Editor/`（EditMode）。命名は既存に合わせて `対象_条件_期待`。

| テスト | どう書くか |
| --- | --- |
| `PauseCategoryResolverTests.Resolve_MultipleCategories_ReturnsAll` | テスト用に2カテゴリーを実装したクラスを定義し、解決結果を `Is.EquivalentTo` で比較 |
| `PauseCategoryResolverTests.Resolve_NoCategory_ReturnsEmpty` | `IPausable` だけ実装したクラスで空を確認 |
| `PauseCategoryResolverTests.Resolve_IPausableItself_IsNotCategory` | 解決結果に `IPausable` が含まれないこと |
| `PauseStateEntityTests.SetPaused_PerCategory_IsIndependent` | 2カテゴリーの一方だけ設定し、他方が `false` のまま |
| `PauseStateEntityTests.SetPaused_SameValue_ReturnsFalse` | 既存テストのカテゴリー版 |
| `PausableRegistryTests.HeldCount_TwoCategories_ResumesAfterBothCleared` | 2カテゴリーをポーズ→片方解除で `Resume()` が呼ばれないこと。**準備直後の呼び出し回数を記録し、差分で比較する** |
| `PauseServiceTests.SetPaused_OtherCategory_DoesNotNotify` | 別カテゴリーの対象へ通知が行かないこと。準備は Service 経由で行い、Registry を直接触らない |
| `PauseServiceTests.Register_NoCategory_Throws` | `ArgumentException` を `Assert.Throws` で |
| `PauseServiceTests.SetPauseAll_NotifiesEveryCategory` | 3カテゴリー分の通知件数 |
| `PauseServiceTests.Reset_ClearsCategories` | `Reset` 後に `IsPausedAny()` が `false` |
| `PauseInfoTests.*` | 既存の等価性テストへカテゴリー情報を足す |
| `PauseViewModelTests.*` | カテゴリー別 Dto の反映 |
| `PauseManagerTests`（**新規**） | `IPausable` を型引数に渡すと `ArgumentException`。`PauseManager` は `UntestedPublicTypes` に載っているため、**この Round でバックログから外す** |
| `PauseCategoryGeneratorTests` | 生成する **文字列** を返す純粋関数を分離し、そちらを検証する。ファイル書き出しは検証しない |

**`IPausable` と `PauseManager` は現在 `UntestedPublicTypes` のバックログにある。**
この Issue で両方に触るため、**バックログから外して実テストを置く。**

**Editor の GUI 操作（Project Settings の入力欄、生成ボタン）は自動で検証できない。**
生成ロジックは「カテゴリー名の一覧 → C# ソース文字列」の純粋関数へ切り出し、そちらをテストする。
押下と生成結果のコンパイルは「動作確認手順」で人へ回す。

## Round 分割

**リモート環境（Unity 無し）で進めるため、Round を小さく保つ。**
各 Round は単独でリリースでき、次の Round はその上に積む。

| Round | 内容 | 版 | 区分 |
| --- | --- | --- | --- |
| 1 | Domain と Application のカテゴリー化。`PauseCategoryResolver` 新規、`PauseStateEntity` / `PausableRegistry` / `PauseService` を多カテゴリー対応。**公開APIは変えない**（`PauseManager` は既定の単一カテゴリーで従来どおり動く） | 6.10.0 | Add |
| 2 | 公開API。`SetPause<T>` / `IsPaused<T>` / `SetPauseAll` / `IsPausedAny` / `AddPauseChangedHandler<T>` / `GetPauseInfo<T>`、`PauseInfo` のカテゴリー化 | 6.11.0 | Add |
| 3 | 既存APIの `[Obsolete]` 化と `Deprecations.md`。待機系APIの型パラメータ版、`SymphonyTween` のオーバーロード | 6.12.0 | Add / Change |
| 4 | `PauseCategoryConfig` + `PauseCategorySettingProvider` + `PauseCategoryGenerator`。生成先アセンブリの作成 | 6.13.0 | Add |
| 5 | Administrator の Pause パネルをカテゴリー別表示へ。サンプル書き換え、モジュール文書、`EditorTools.md` | 6.14.0 | Add / Change |

**Round 3 は `Fix` を含めない。** `preflight` が同一版での `Fix` と `Change` の同居を止めるため、
不具合が出た場合は独立したパッチ版へ分ける。

## この Round で触るバージョン関連ファイル

**版は4か所ある。** 各 Round で毎回すべて更新する。

| ファイル | 触る Round |
| --- | --- |
| `package.json` の `version` | 1〜5（全部） |
| `Core/SymphonyConstant.cs` の `VERSION` | 1〜5（全部） |
| `CHANGELOG.md` の見出し | 1〜5（全部） |
| `README.md` の「現在のバージョン」 | 1〜5（全部） |
| `Documentation~/Modules/Pause.md` | **Round 5 のみ**（Round 2〜4 の内容もまとめてここで書く） |
| `Documentation~/Deprecations.md` | **Round 3 のみ** |
| `Documentation~/EditorTools.md` | **Round 4 と 5**。Round 4 は生成機能の行、Round 5 は Administrator の行 |
| `Tests/Editor/PublicTypeTestCoverageTests.cs` の backlog | **Round 2 のみ**（`IPausable` と `PauseManager` を外す） |
| `AGENTS.md` のアセンブリ構成図 | **Round 4 のみ**（`SymphonyFrameWork.PauseCategory` を追加） |

## 動作確認手順

自動で確認する範囲と、人が操作する範囲の境目を明示する。

**自動（EditMode テスト）で確認する**

- カテゴリーごとに独立してポーズ状態を持つこと
- 複数カテゴリーの OR 合成と、全解除後にだけ `Resume()` が呼ばれること
- カテゴリー未実装の登録が `ArgumentException` になること
- 生成する C# ソース文字列が期待どおりであること

**人が Unity で確認する**

1. Project Settings > SymphonyFrameWork でカテゴリー名を2つ設定し、生成を実行する
2. `Assets/Scripts/SymphonyFrameWork/PauseCategory/` に interface と asmdef ができ、**コンパイルが通ること**
3. サンプルシーンで、片方のカテゴリーだけをポーズし、**もう片方の対象が動き続けること**
4. Symphony Administrator の Pause パネルが**カテゴリー別に一覧表示**されること
5. **カテゴリー名を追加・削除したとき、開いたままの Administrator と Project Settings が追従すること**
   （開き直せば直る不具合を見逃さないため）
6. Play Mode の開始・終了を2回繰り返し、`ResetRuntimeState()` 後にカテゴリー状態が残らないこと
