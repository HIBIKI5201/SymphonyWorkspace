# EditorComposition

## 目的

`ArchitectureRevision.md` の Phase 3 のうち、**Editor 側の Composition 集約**だけを扱う。

現在、Editor の初期化は4つの型がそれぞれ Unity の自動初期化属性を持ち、互いの順序を知らないまま並行して走る。

| 型 | 属性 | 起動時にしていること |
| --- | --- | --- |
| `PackageInitializer` | `[InitializeOnLoad]` | Config 確認、Loader 解決の注入、ログ設定の注入、UXML ローダーの注入、enum 生成、asmdef 参照追加、`AssetDatabase.Refresh` |
| `AutoEnumGenerator` | `[InitializeOnLoad]` | Tag/Layer/Scene の変更監視の登録 |
| `SymphonyDebugLogFileWriter` | `[InitializeOnLoad]` | ログ購読の登録、Timer 開始 |
| `TagsAndLayersPostProcessor` | `[InitializeOnLoadMethod]` | ProjectSettings の監視対象登録 |

これは `DesignPhilosophy.md` が禁止している構造である。

> `[RuntimeInitializeOnLoadMethod]`は`SymphonyOrchestrator`、`[InitializeOnLoad]`と`[InitializeOnLoadMethod]`は`SymphonyEditorOrchestrator`だけが所有する。サブシステムやInitializerへUnityの自動初期化属性を付けない。

起動時の `AssetDatabase.Refresh()` も5箇所に分散しており、Refresh 後に同一ドメインで処理が続くことを暗黙に前提にしている。

## この Round に含めないもの

Runtime 側の Composition 集約は **Round E** で扱う。具体的には次の3つ。

- `destroyCancellationToken` への個別登録3件（`SaveSystem`・`SceneLoader`・`ServiceLocator`）を1件へ集約
- `ServiceLocateData` の `[RuntimeInitializeOnLoadMethod]` 除去
- `PreserveObject` / `CreateSystemObject` のサブシステム利用除去（`SymphonyDebugHUD`・`AudioManager`・`ServiceLocateData` の3箇所）

分けた理由は、`PreserveObject` の除去がサブシステム内部の作り替えを伴い、Editor 側の集約と混ぜると差分が読めなくなるため。

## 公開API

利用側の Runtime API は変更しない。

Editor アセンブリには次の変更が入る。**Editor アセンブリは Player ビルドに含まれないため、パッケージ利用者のランタイムコードには影響しない。**

| 型 | 変更 |
| --- | --- |
| `SymphonyEditorOrchestrator`（新規） | `internal static`。Editor の Composition Root |
| `PackageInitializer` | `public static` → `internal static`。自動初期化属性を除去し、Orchestrator から呼ばれるモジュールにする |
| `AutoEnumGenerator` | 自動初期化属性を除去。Orchestrator から呼ばれる |
| `SymphonyDebugLogFileWriter` | 既に `internal static`。自動初期化属性を除去 |
| `TagsAndLayersPostProcessor` | `[InitializeOnLoadMethod]` を除去 |

`SymphonyEditorOrchestrator` を `internal` にする根拠は `DesignPhilosophy.md` の「Composition Rootは`internal`にする」。Editor 側も同じ扱いにする。

## ファイル構成

| パス | 名前空間 | 内容 |
| --- | --- | --- |
| `Editor/Orchestrator/Internal/SymphonyEditorOrchestrator.cs`（新規） | `SymphonyFrameWork.Editor` | Editor の Composition Root |

`CodeGuidelines.md` の「EditorのComposition Rootは`Editor/Orchestrator/Internal/`へ置く」「Editorの`SymphonyEditorOrchestrator`は`SymphonyFrameWork.Editor`へ置く」に従う。`Internal/` は名前空間へ含めない。

既存4ファイルは属性の除去と `Initialize()` / `Shutdown()` の公開（`internal`）に留め、**ロジックは動かさない**。移動を伴う整理は後続 Round で行う。

## 依存方向

```text
Unity Editor のホストイベント
  （[InitializeOnLoad] / AssemblyReloadEvents / EditorApplication.quitting / playModeStateChanged）
        │ 唯一の入口
        v
SymphonyEditorOrchestrator（Editor / Composition）
        │ 明示的に Initialize / Shutdown を呼ぶ
        v
PackageInitializer, AutoEnumGenerator, SymphonyDebugLogFileWriter,
TagsAndLayersPostProcessor（Editor モジュール）
```

各モジュールは Orchestrator を参照しない。一方向を保つ。

## AssetPostprocessor の扱い

`SymphonyAssetProtector` と `TagsAndLayersPostProcessor` は `AssetPostprocessor` を継承しており、**Unity が型を発見して任意のタイミングで呼ぶ**。Orchestrator が所有できない。

`MenuItem` / `SettingsProvider` / `CustomEditor` と同じく「Unity が発見するための入口」として扱い、次の規則を課す。

- **コールバック内で package-wide な初期化を行わない。** 対応するモジュールの処理を呼ぶだけにする
- Orchestrator が `Ready` でない間に呼ばれた変更は、その場で処理せず**溜めておき、`Ready` 後に1回だけ処理する**（`DesignPhilosophy.md` の「Editor初期化中にhost callbackが再入した場合は、その場で再初期化せず変更をcoalesceして`Ready`後に1回処理する」）

## 初期化フェーズと状態

`DesignPhilosophy.md` の Orchestrator の節に従い、`Uninitialized` / `Initializing` / `Ready` / `ShuttingDown` を明示する。

| ホストからの入口 | 実行内容 |
| --- | --- |
| `[InitializeOnLoad]`（domain load / script reload 後） | Editor モジュールの `Init` → `Build` → `Ready` |
| `AssemblyReloadEvents.beforeAssemblyReload` | `Shutdown` |
| `EditorApplication.quitting` | `Shutdown` |
| `EditorApplication.playModeStateChanged` | 後続 Round で ViewModel の再取得に使う。**この Round では入口の用意のみ** |

- 初期化したモジュールを順序付きで記録し、`Shutdown` では逆順に解放する
- `Shutdown` は多重呼び出しを無害にする。同期的かつ非ブロッキングにし、非同期保存や完了待ちを開始しない
- 1つのモジュールの終了処理が例外を出しても残りを逆順で解放し、最後にまとめて記録する。終了処理から例外を外部へ再送出しない
- `Ready` 以外では、モジュールを再初期化しない

## `AssetDatabase.Refresh` の集約

起動経路にある5箇所を Orchestrator の初期化最終段階での**1回**へ集約する。

| ファイル | 行 | 扱い |
| --- | --- | --- |
| `Editor/Configs/SymphonyConfigManager.cs` | 59, 82 | 集約対象。呼び出しを除去し、変更があったことを Orchestrator へ伝える |
| `Editor/Generator/EnumGenerate/EnumGenerator.cs` | 110 | 集約対象。ただし**メニューからも呼ばれる**ため、起動経路とメニュー経路を区別できるようにする |
| `Editor/PackageInitializer.cs` | 31, 68 | 集約対象 |
| `Editor/Generator/AssetStoreToolsPackager/AssetStoreToolsPackager.cs` | 279 | **対象外**（メニュー起点） |
| `Editor/Generator/FolderGenerate/FolderGenerator.cs` | 36 | **対象外**（メニュー起点） |
| `Editor/SettingProvider/SaveSystemSettingProvider.cs` | 83 | **対象外**（設定変更起点） |

Refresh は**アセット変更が実際にあった場合だけ**実行する。毎回無条件に呼ばない。

## エラー処理

- モジュールの初期化が失敗した場合、後続フェーズを実行しない。成功済みモジュールだけを逆順で終了してから失敗状態を記録する
- 例外は握りつぶさず `Debug.LogError` で型名と原因を出す。ただし Orchestrator の外へ再送出しない（Editor の起動を止めないため）
- `Shutdown` 中の例外は記録するのみ

## 影響範囲

**破壊的変更ではない。** Runtime の公開API、シリアライズ形式、利用側から見た挙動はいずれも変わらない。

観測できる変更は次の2点。

1. **`PackageInitializer` が `public` から `internal` になる。** Editor アセンブリの型であり、利用側が呼ぶ想定の API ではない（`[InitializeOnLoad]` で自動実行されるもの）。外部から明示的に呼んでいたコードがあれば影響するが、そのような使い方は想定されていない
2. **起動時の `AssetDatabase.Refresh` が最大5回から1回になる。** Editor の起動が速くなる方向の変更

## テストの置き場と種別

**自動テストは追加しない。** この Round の中核が `[InitializeOnLoad]` と `AssemblyReloadEvents` という Unity のホストライフサイクルであり、テストランナーから再現できないため。

代わりに下記の「動作確認手順」を手動で実施する。既存の43件（EditMode）と4件（PlayMode）が引き続き成功することは確認する。

## 動作確認手順

1. `uloop-compile` がエラー0・警告0
2. 既存テストが全数成功（EditMode 43 / PlayMode 4）
3. **Editor を再起動**し、Console に `Symphony Framework Initialized` が**1回だけ**出ること。多重初期化していない証拠
4. `Assets/Resources/SymphonyFrameWork/` の3つの Config と `Assets/Scripts/SymphonyFrameWork/` の4つの enum が、削除後の再起動で再生成されること
5. **スクリプトを1つ編集して再コンパイル**（domain reload）させ、初期化が1回だけ走り、ログファイル書き込みや enum 監視が二重登録されないこと
6. Tag または Layer を追加し、enum が自動再生成されること（`TagsAndLayersPostProcessor` が Orchestrator 経由で機能している証拠）
7. `Assets/SymphonyFrameWork/` 配下のファイルを移動し、Round A で追加したアセット保護が引き続き動くこと
8. Play Mode の開始・終了を2回繰り返し、Editor 側の初期化が壊れないこと

## バージョン判断

**マイナー（2.8.0）。** Runtime の公開APIに変更が無く、Editor アセンブリ内の可視性変更のみ。破壊的変更には当たらない。

## この Round で触るバージョン関連ファイル

| ファイル | 触る箇所 |
| --- | --- |
| `Assets/SymphonyFrameWork/package.json` | `version` を `2.7.0` → `2.8.0` |
| `Assets/SymphonyFrameWork/CHANGELOG.md` | `## [2.8.0]` の見出しと本文 |
| `Assets/SymphonyFrameWork/README.md` | 「現在のバージョン」を `2.8.0` へ |

`AGENTS.md` は触らない（利用側から見えるAPIが変わらないため）。後続 Round はこれらのファイルの別の行を触るので、この Round はコミットまで完了させてから次へ進む。
