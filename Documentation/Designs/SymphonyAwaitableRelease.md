# SymphonyAwaitableRelease

## 目的

`SymphonyAwaitable`（`ArchitectureRevision.md` の Phase 2）は実装済みだが、その後2つの方針が決まったため、リリース前に反映する。

1. **テストはフレームワークの外へ置く。** 現在 `Assets/SymphonyFrameWork/Tests/` にあるテストをワークスペース側の `Assets/Tests/` へ移す。submodule 化・ドキュメント移設と同じ「ワークスペースが開発環境、パッケージが配布物」という方針に揃える
2. **`SymphonyTask` は `SymphonyAwaitable` の登場により役目を終える。** ただし public 型のため、この Round では `[Obsolete]` 化にとどめ、削除は 3.0.0（`ArchitectureRevision.md` の Phase 6）

この Round の完了をもって `SymphonyAwaitable` を 2.6.0 としてリリースする。

## 公開API

### `SymphonyAwaitable` へ移すもの

`SymphonyTask` にあって `SymphonyAwaitable` に無い機能を移す。シグネチャは変更しない。

```csharp
public static async void BackGroundThreadAction(Action action);
public static async Awaitable BackGroundThreadActionAsync(Action action);
public static async Awaitable OnComplete(this Awaitable awaitable, Action action, CancellationToken token = default);
```

### 設計書からの逸脱: `WaitUntil` を追加する

`ArchitectureRevision.md` の `SymphonyAwaitable` API 一覧には `WaitWhile` しか無い。しかし `SymphonyTask.WaitUntil(cond)` の利用側は、移行時に `SymphonyAwaitable.WaitWhile(() => !cond())` と条件を反転して書き直す必要があり、**反転の書き間違いは静かに無限待機や即時完了を招く**。

移行の安全性を優先し、`WaitUntil` を追加する。

```csharp
public static Awaitable WaitUntil(Func<bool> predicate, CancellationToken token = default);
```

`WaitWhile` の否定として実装し、両者の関係を XML ドキュメントへ明記する。

### `SymphonyTask` の非推奨化

型全体へ `[Obsolete("SymphonyAwaitableを使用してください。", error: false)]` を付ける。メンバーは削除せず、3.0.0 まで動作を維持する。

既に `[Obsolete]` な `OnComplete(this Task, ...)` はそのまま残す（GCアロケーション削減のための旧来の非推奨であり、今回の非推奨化とは理由が異なる）。

**フレームワーク内の `SymphonyTask` 参照は既にゼロ**であることを確認済み。1箇所でも残ると非推奨警告が出て「コンパイル警告0」の受け入れ基準を満たせなくなるため、実装後に再確認する。

## ファイル構成

### 移動

`Assets/SymphonyFrameWork/Tests/` → `Assets/Tests/`

| 移動するもの | 備考 |
| --- | --- |
| `Editor/SymphonyAwaitableTests.cs`（+`.meta`） | EditMode 37件 |
| `Editor/SymphonyFrameWork.Tests.Editor.asmdef`（+`.meta`） | |
| `Runtime/SymphonyAwaitableRuntimeTests.cs`（+`.meta`） | PlayMode 4件 |
| `Runtime/SymphonyFrameWork.Tests.Runtime.asmdef`（+`.meta`） | |
| `Tests.meta`、`Tests/Editor.meta`、`Tests/Runtime.meta` | フォルダのmeta |

**`.meta` を必ず一緒に動かす。** Unity が既に付与した GUID を維持するため。submodule 側では未追跡（一度もコミットしていない）ので git 履歴の引き継ぎは不要。

**移動前に `Project Settings > SymphonyFrameWork` のアセット保護モードを「無効化」または「警告」にする。** Round A で追加した保護が働き、「有効化」のままだと移動が差し戻される。

asmdef 名（`SymphonyFrameWork.Tests.Editor` / `.Runtime`）はテスト対象を表しているため変更しない。

### 変更

| パス | 内容 |
| --- | --- |
| `Assets/Tests/Runtime/SymphonyFrameWork.Tests.Runtime.asmdef` | `"defineConstraints": ["UNITY_INCLUDE_TESTS"]` を追加 |
| `Runtime/Utility/SymphonyAwaitable.cs` | `BackGroundThreadAction` 系、`OnComplete`、`WaitUntil` を追加 |
| `Runtime/Utility/SymphonyTask.cs` | 型へ `[Obsolete]` |
| `AGENTS.md` | 既に `SymphonyAwaitable` を追記済み。`SymphonyTask` が非推奨である旨を追記 |

### PlayMode テスト asmdef の `defineConstraints`

現在 `defineConstraints` が空かつ `includePlatforms` も空（全プラットフォーム）のため、**nunit を参照するアセンブリがワークスペースの Player ビルドへ含まれる。** Unity の PlayMode テスト asmdef の標準構成に反し、`DesignPhilosophy.md` の「開発コードの分離」（Runtime には Player ビルドで必要なコードだけを含める）にも反する。

EditMode 側は `includePlatforms: ["Editor"]` のため対応不要。

## 依存方向

変更なし。テストがワークスペース側へ移ることで、パッケージ → テストの依存が無くなる（元々テストからパッケージへの一方向）。

`Assets/Tests/` の asmdef は `SymphonyFrameWork` を参照する。ワークスペース側からパッケージへの参照であり、既存の `SymphonyFrameWork.Enum` と同じ方向。

## エラー処理

変更なし。`SymphonyAwaitable` へ移す3メソッドの挙動は現行のまま維持する（`BackGroundThreadAction` 系の null 検証とメインスレッド復帰を含む）。

## 影響範囲

**破壊的変更ではない。** 公開APIの削除もシグネチャ変更も無い。

利用側から観測できる変更は2点。

1. **`SymphonyTask` を使うと非推奨警告が出る。** 動作は変わらない。3.0.0 で削除するため、それまでに `SymphonyAwaitable` へ移行してもらう
2. **テストがパッケージに同梱されなくなる。** パッケージ単体を clone した外部コントリビューターはテストを実行できない

移行方法は CHANGELOG の `Deprecated` に明記する。特に `WaitUntil` → `WaitWhile` の条件反転が不要になったこと（`WaitUntil` をそのまま使える）を書く。

## テストの置き場と種別

**`Assets/Tests/`（ワークスペース側）。** EditMode は `Assets/Tests/Editor/`、PlayMode は `Assets/Tests/Runtime/`。

新規テストは追加しない。既存の41件（EditMode 37 / PlayMode 4）が移動後も全数成功することを確認する。

`SymphonyAwaitable` へ移した3メソッドと新規 `WaitUntil` については、**`WaitUntil` のみ EditMode テストを追加する**（条件が最初から true の場合に即時完了すること、事前キャンセルで `OperationCanceledException` になること）。`BackGroundThreadAction` 系はスレッド遷移を伴い EditMode で安定しないため、テストを追加せず手動確認に回す。

## 動作確認手順

1. `uloop-compile` がエラー0・**警告0**。`SymphonyTask` の非推奨警告が出ないこと（＝内部参照が全廃されている証拠）
2. EditMode テストが移動後も全数成功（37件＋`WaitUntil` の追加分）
3. PlayMode テストが移動後も全数成功（4件）
4. `Assets/SymphonyFrameWork/Tests/` が存在せず、`Assets/Tests/` に `.cs` と `.meta` が対で揃っていること
5. `git -C "Assets/SymphonyFrameWork" status` に `Tests/` が残っていないこと
6. 利用側コードで `SymphonyTask.WaitUntil(...)` を書くと非推奨警告が出て、なお動作すること

## バージョン判断

**マイナー（2.6.0）。**

後方互換な公開API追加（`SymphonyAwaitable` とそのメソッド群）と非推奨化のみ。削除もシグネチャ変更も無い。`SymphonyTask` の削除は 3.0.0。

## この Round で触るバージョン関連ファイル

| ファイル | この Round で触る箇所 |
| --- | --- |
| `Assets/SymphonyFrameWork/package.json` | `version` を `2.5.0` → `2.6.0` |
| `Assets/SymphonyFrameWork/CHANGELOG.md` | `## [2.6.0]` の見出しと本文を追加 |
| `Assets/SymphonyFrameWork/README.md` | 「現在のバージョン」を `2.6.0` へ。Utility 一覧への `SymphonyAwaitable` 追記は変更済み |
| `Assets/SymphonyFrameWork/AGENTS.md` | namespace 早見表（変更済み）に加え、`SymphonyTask` の非推奨を追記 |

これ以降の Round（Round C / Phase 3以降）はこれらのファイルの**別の行**を触る。同一ファイルへ複数 Round の変更を同時に載せないため、この Round はコミットまで完了させてから次へ進む。
