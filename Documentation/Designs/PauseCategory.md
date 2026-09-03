# ポーズのカテゴリー化（Issue #168）

## 目的

**今は「ポーズ中か否か」が真偽値1つしかなく、止めたいものだけを止められない。**
`PauseStateEntity` が `bool IsPaused` を1つ持ち、`PauseManager.Pause = true` は
登録済みの `IPausable` を全部止める。

実際には「UIは動かしたままゲームプレイだけ止める」「カットシーン中は入力だけ止める」のように、
**止める対象を分けたい**。これを型で表す。カテゴリーは `IPausable` を継承した空の interface で、
ポーズ状態は `Type` をキーにした辞書で管理し、状態の変更は型パラメータで行う。

## 起票者への質問と決定

Issue [#168](https://github.com/HIBIKI5201/SymphonyFramework/issues/168) へ質問し、回答を得た。
選択によって作るものが変わる点だけを聞いている。

| 問い | 決定 | 設計への影響 |
| --- | --- | --- |
| 既存の `PauseManager.Pause`（bool）とカテゴリー未指定の `IPausable` 実装の扱い | **既存APIを `[Obsolete]` にして `Pause<T>` へ一本化** | Round 3 で既存8件を Obsolete 化。`Deprecations.md` への追記を伴う |
| 待機系APIと `SymphonyTween` が見るカテゴリー | **全部カテゴリー対応にする** | 待機系6件に型パラメータ版を追加。`SymphonyTween` はオーバーロードを足す |
| 今回の対応範囲 | **Issue を最後まで閉じる** | Runtime・生成・Administrator表示・サンプル・文書まで。Round 5つ |

### 着手前の検証で分かったこと

**生成先アセンブリを新設する必要がある。** 詳細は下記「依存方向」にある。
Issue には「SettingsProvider でカテゴリー名を設定するとインターフェースが生成される」とあるが、
**既存の自動生成先へは置けない**（参照が循環する）。起票時の想定と現実が違うため Issue へも記録した。

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

**カテゴリーを1つも実装していない対象は、既定カテゴリー（`IPausable` 自身）に属する。**
黙って「どのカテゴリーにも属さない＝絶対に止まらない」対象になると、
ポーズしても動き続ける原因が分からないためで、`SetPauseAll` でも `SetPause<IPausable>` でも止まる。

> **着手後に変えた判断。** 当初は「カテゴリー未実装の登録は `ArgumentException`」としていたが、
> **それは公開APIの破壊的変更である。** `IPausable` を直接実装した対象は既に存在し
> （サンプルの `PauseManagerSample_Mover`、テストの `TestPausable`）、Round 1 は
> 「公開APIを変えない」Round であるため成立しない。既定カテゴリーへ寄せれば
> 「絶対に止まらない対象を作らない」という当初の意図は保たれる。

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
| `TCategory` に `IPausable` 自身を渡した | **例外にしない。** 既定カテゴリー、つまり「カテゴリーを明示していない対象」を指す |
| カテゴリーを1つも実装しない対象を `Register` | **例外にしない。** 既定カテゴリーへ入れる（→ 上記「着手後に変えた判断」） |
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

## 実施レポート

実施日: 2026-09-03 / バージョン: 6.10.0 〜 6.14.0 / PR: [#210](https://github.com/HIBIKI5201/SymphonyFramework/pull/210) [#211](https://github.com/HIBIKI5201/SymphonyFramework/pull/211) [#212](https://github.com/HIBIKI5201/SymphonyFramework/pull/212) [#213](https://github.com/HIBIKI5201/SymphonyFramework/pull/213) [#214](https://github.com/HIBIKI5201/SymphonyFramework/pull/214)

### 実装した内容

| Round | 版 | 内容 |
| --- | --- | --- |
| 1 | 6.10.0 | `PauseCategoryResolver` 新規。`PauseStateEntity` を `Dictionary<Type, bool>` へ、`PausableRegistry` へ停止要因の件数、`PauseService` をカテゴリー単位通知へ。**公開APIは据え置き** |
| 2 | 6.11.0 | `SetPause<T>` / `IsPaused<T>` / `SetPauseAll` / `IsPausedAny` / `AddPauseChangedHandler<T>` / `GetPauseInfo<T>`、`PauseInfo.Category` |
| 3 | 6.12.0 | 待機系6件の型パラメータ版、`SymphonyTween.PausableTweening<T, TCategory>`、旧API 8件の `[Obsolete]` 化と `Deprecations.md` |
| 4 | 6.13.0 | `PauseCategoryGenerator` / `PauseCategoryConfig` / `PauseCategorySettingProvider`、生成先 `SymphonyFrameWork.PauseCategory` アセンブリ |
| 5 | 6.14.0 | Administrator の `Categories` 一覧、`PauseCategoryDto`、サンプルのカテゴリー対応、モジュール文書 |

### 設計から変えた点

**1. カテゴリー未実装の登録を例外にしなかった。** 当初は「`ArgumentException` にする」としていたが、**公開APIの破壊的変更**になる。`IPausable` を直接実装した対象は既に存在し（サンプル、テスト）、Round 1 は「公開APIを変えない」Round であるため成立しない。既定カテゴリー（`IPausable` 自身）へ寄せることで「絶対に止まらない対象を作らない」という当初の意図は保った。

**2. モジュール文書の更新を Round 5 から Round 4 へ前倒した。** 版関連ファイルの表では Round 5 のみとしていたが、Round 4 で `EditorTools.md` から `PauseManager.md#editor機能` を参照する行を足したため、参照先に内容が無い状態が残る。コードとドキュメントの乖離はバグとして扱う規約に反する。

**3. `PauseDto` へカテゴリー一覧を持たせた。** 設計では「カテゴリー別の一覧を持つ」とだけ書いており、要素型を決めていなかった。`PauseCategoryDto` を新設した。

### 実装中に見つけて直した欠陥

いずれもコンパイラでは見つからない類で、**リモート環境では差分の通読だけが担保だった**。

| 見つけたもの | 内容 |
| --- | --- |
| `SetPausedAll` の取りこぼし | 状態側は「一度でも操作したカテゴリー」しか知らないため、まだ誰も止めていないカテゴリーの対象へ届かない。レジストリの `GetAllCategories()` との和を取る形にした |
| カテゴリーごとの通知で中断する | 購読者が例外を投げると残りのカテゴリーが未設定のまま中断する。**状態を全部確定させてから通知する**形へ変えた。表示への通知も1回にまとまった |
| 非推奨APIの内部呼び出し | `PausableDestroy` が `PausableWaitForSecondAsync` を呼んでおり、`[Obsolete]` を付けると自分のコードから警告が出る。待機処理の本体を private ヘルパーへ切り出した |
| カテゴリー一覧の並びが揺れる | 辞書の列挙順は保証されず、内容が同じでも ViewModel が変化として通知してしまう。表示名の昇順で並べ替え、テストで固定した |
| `generate_meta.py` が `Samples~` を見ない | `~` で終わるディレクトリを一律除外していたが、preflight は `Samples~` 配下の `.meta` を要求する。「除外するか」と「中を走査するか」を分けた |

### 検証結果

`python scripts/release_round.py preflight` — 5 Round とも全項目通過。

| | R1 | R2 | R3 | R4 | R5 |
| --- | --- | --- | --- | --- | --- |
| version | 6.10.0 | 6.11.0 | 6.12.0 | 6.13.0 | 6.14.0 |
| tests | ソース7/テスト6 | 6/6 | 4/2 | 3/2 | 5/1 |
| bom / meta / layer / asmdef / playmode / docs / question | すべてOK | 同左 | 同左 | 同左 | 同左 |

加えて機械的に確認した項目。

- 同名型の重複は `IInjectable`（ジェネリック引数違いの多重定義）だけで、`CS0101` の対象は無い
- フレームワーク内から非推奨APIを呼んでいる箇所が0件。**サンプルからも0件**
- テストが Editor の `internal` を呼ぶ経路は `Editor/AssemblyInfo.cs` の `InternalsVisibleTo` で成立する

**テストは合計 60 件前後増えた。** `PauseCategoryResolverTests`（14）、`PauseManagerTests`（新規17）、`PauseCategoryGeneratorTests`（新規12）、`PauseServiceTests` / `PausableRegistryTests` / `PauseStateEntityTests` / `PauseViewModelTests` / `PauseInfoTests` への追加。

**`PauseManager` と `IPausable` を `UntestedPublicTypes` のバックログから外した。** 一覧は縮む方向にしか変えていない。

### 未実施の確認

**Unity Editor の無いリモート環境で実装したため、次はすべて未実施。** 依頼者の一括検証で確認する。優先度の高い順に並べる。

1. **コンパイルの警告0。** `[Obsolete]` を8件付けたため、フレームワーク内とホスト側 `Assets/Scripts/` の双方に `CS0618` が出ていないこと
2. **`Project Settings > SymphonyFrameWork > Pause Category` が表示されること。** `[SettingsProvider]` を `internal` にしたため、**出なければ `public` へ戻す必要がある**。この環境では実行確認できていない
3. **PlayMode テスト全数成功。** `PauseAwaitableRuntimeTests` を型パラメータ版へ書き換えたため、壊れやすい
4. **`PauseManagerSample` のシーンで `_range` / `_speed` が 3 / 2 のまま繋がっていること。** 基底クラスを抽出したため
5. EditMode テスト全数成功（約60件増）
6. カテゴリーを2つ生成し、実装したクラスが `SetPause<T>` で個別に止まること
7. `Symphony Administrator > Pause` の `Categories` 一覧の表示。**カテゴリーを追加・削除したとき、開いたままのウィンドウが追従すること**
8. サンプルで `Pause Gameplay` が白いCubeだけを止め、水色は動き続けること
9. Play Mode 2往復で `ResetRuntimeState()` 後にカテゴリー状態が残らないこと
10. スクリプトで生成した `.meta` 計11件を、Unity が再生成・差分にしないこと

### 振り返り

| 気づき | 扱い |
| --- | --- |
| **「公開APIを変えない Round」で例外を追加すると破壊的変更になる。** Round の区分と、追加する検証の強さが噛み合っているかを設計時に照合する | 設計書テンプレートへの追加候補。今回は実施レポートへ記録するに留める |
| **`generate_meta.py` と `preflight` で `.meta` の要否判定が食い違っていた。** 同じ規則を2箇所が別々に持つと、片方だけ直っても気づけない | 判定を揃えた。**将来は preflight 側の免除リストを generate_meta が読む形にするのが本筋** |
| 表示用の値を毎回作り直す Query と、内容比較で通知する ViewModel の組み合わせでは、**列挙順の非決定性がそのまま無駄な再描画になる** | Scene Block でも同じ構造を使っている。`review.md` のレビュー観点へ「一覧を返す Query は順序を決めているか」を足す候補 |
| Round を5つに割ったことで、各 Round の差分を全部読み切れた。**リモートでは「公開APIを変えない内部だけの Round」を先頭に置くと、後段の破壊的変更が切り分けやすい** | remote.md §6 への追加候補 |

**提案にとどめる。** 上記のうちスキルとドキュメントへの反映は、承認をもらってから行う。
