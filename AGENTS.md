# AGENTS.md — SymphonyFramework ワークスペース

このリポジトリは **Symphony Framework を開発するためのホストUnityプロジェクト**です。成果物であるフレームワーク本体は、submodule として `Assets/SymphonyFrameWork/` に置かれています。

このファイルはワークスペース固有の情報（リポジトリ構成、環境設定、自動生成物の区別、検証手段）に限定しています。フレームワークのAPIや設計規約は重複させず、下表のドキュメントへ委譲します。

## 0. 作業内容ごとの参照先

| やること | 読むもの |
| --- | --- |
| パッケージ本体（`Assets/SymphonyFrameWork/` の `Runtime/` `Editor/` `Core/`）に**機能を追加・変更する** | [.claude/skills/implement/SKILL.md](./.claude/skills/implement/SKILL.md)（`/implement`）。設計書 → Codex による実装 → 確認 → バージョン更新 → コミット → 振り返り の一連のフロー |
| パッケージ本体のソースを修正する（小さな修正、上記フローに乗らないもの） | [Documentation/CONTRIBUTING.md](./Documentation/CONTRIBUTING.md)。コードを書く前に [Documentation/CodeGuidelines.md](./Documentation/CodeGuidelines.md)、型や名前空間を新設する前に [Documentation/DesignPhilosophy.md](./Documentation/DesignPhilosophy.md) |
| パッケージを**使う**コードをホスト側（`Assets/Scripts/` など）に書く | [Assets/SymphonyFrameWork/AGENTS.md](./Assets/SymphonyFrameWork/AGENTS.md)。機能一覧とクイックスタートは [Assets/SymphonyFrameWork/README.md](./Assets/SymphonyFrameWork/README.md) |
| ホストプロジェクトの設定・シーン・アセットを触る | このファイル |
| 直近の変更の経緯を知る | [Assets/SymphonyFrameWork/CHANGELOG.md](./Assets/SymphonyFrameWork/CHANGELOG.md) |

## 1. 2つのリポジトリ

| | ワークスペース（親） | パッケージ（submodule） |
| --- | --- | --- |
| リポジトリ | `HIBIKI5201/SymphonyWorkspace` | `HIBIKI5201/SymphonyFramework` |
| ルート | このリポジトリのルート | `Assets/SymphonyFrameWork/` |
| 役割 | 開発環境。Unityプロジェクト、検証用シーン、開発ドキュメント | 配布物。UPMパッケージ本体 |
| 既定ブランチ | `dev` | `develop` |

- **git コマンドを実行する前に、どちらのリポジトリが対象かを必ず確認してください。** submodule 側は `git -C "Assets/SymphonyFrameWork" ...` と明示するのが安全です。
- パッケージのソース変更は **submodule 側でコミットし、push してから**、親リポジトリで gitlink の更新をコミットします。手順の詳細は [Documentation/CONTRIBUTING.md](./Documentation/CONTRIBUTING.md) の §1 と §5 にあります。
- クローンは `git clone --recurse-submodules`。忘れた場合は `git submodule update --init --recursive`。
- `Assets/SymphonyFrameWork.meta` は submodule の**外側**にあり、親リポジトリが管理します。**削除しないでください**（利用側プロジェクトのGUID参照が壊れます）。

## 2. 環境

- Unity **6000.3.10f1** / URP **17.3.0** / Color Space: Linear / API Compatibility Level: .NET Standard 2.1
- Active Input Handling: **Both**（旧Input Managerと新Input Systemの両方）
- Asset Serialization: **Force Text**
- Scripting Define Symbols: `UNITY_POST_PROCESSING_STACK_V2;DOTWEEN`
- **Enter Play Mode Options が有効で、Domain Reload と Scene Reload の両方が無効です**（`ProjectSettings/EditorSettings.asset` の `m_EnterPlayModeOptions: 3`）。static状態はPlay Mode終了時にリセットされません。フレームワーク各Facadeに `ResetRuntimeState()` があるのはこのためです。この前提を崩すコードを書かないでください。
- **DOTween（`Assets/Plugins/Demigiant/`）は `.gitignore` 済みで、リポジトリに含まれません。** クローン直後は存在せず、`SymphonyTween` 周辺がコンパイルエラーになります。別途導入が必要です。
- 主な依存パッケージ（`Packages/manifest.json`）: Addressables 2.9.0、Input System、Cinemachine 3.1.5、Behavior、ProBuilder、Visual Effect Graph、Test Framework 1.6.0。git URL 経由で `com.unity.springbone` と `io.github.hatayama.uloopmcp`。

## 3. `Assets/` 配下の区分

| パス | 種別 | 備考 |
| --- | --- | --- |
| `Assets/SymphonyFrameWork/` | **submodule**（成果物） | 変更時は [Documentation/CONTRIBUTING.md](./Documentation/CONTRIBUTING.md) に従う |
| `Assets/Scripts/SymphonyFrameWork/` | **自動生成** | `SceneListEnum` / `TagsEnum` / `LayersEnum` / `AudioGroupTypeEnum` と `SymphonyFrameWork.Enum.asmdef`。`EnumGenerator` / `AutoEnumGenerator` が生成する。**手で編集しない** |
| `Assets/Resources/SymphonyFrameWork/` | **自動生成** | `SceneManagerConfig` / `AudioManagerConfig` / `SaveSystemConfig`。`SymphonyConfigManager.AllConfigCheck()` が `[InitializeOnLoad]` で生成する。型は `internal` なのでコードから参照できず、Inspector と Project Settings 経由でのみ設定する |
| `Assets/Scripts/DebugScripts/` | 手書きの検証用スクラッチ | ServiceLocator等の動作確認用。テストではない |
| `Assets/Level/Scenes/` | 動作確認用シーン | `NewScene` / `Scene2` / `Scene3` の3つだけがBuild Settingsに登録済み。**この登録順が `SceneListEnum` に反映される** |
| `Assets/Arts/Shaders/` | Shader Graph + HLSL | `ToonShader` / `CharacterToonShader` / `OutLineShader` / `DashedLineShader`、`ToonLighting.hlsl` / `AdvancedOutline.hlsl` |
| `Assets/Settings/` | URP設定 | `PC_*` / `Mobile_*` の RP Asset と Renderer |
| `Assets/Editor/Scripts/VersionLogGenerator.cs` | ワークスペース側のEditorツール | `Tools/VersionLogGenerator`。CHANGELOGへのエントリ追記と `package.json` の version 更新を補助する |
| `Assets/Plugins/` | **git管理外** | DOTween |
| `Assets/TextMesh Pro/`, `Assets/TutorialInfo/` | Unityテンプレート由来 | 触らない |

`Assets/SymphonyFrameWork/` 配下のアセットは、`SymphonyAssetProtector`（`AssetPostprocessor`）が移動を検知して自動的に差し戻します。意図して動かす場合は `Tools/SymphonyFrameWork/Settings/Symphony Asset Lock` を解除してください。

## 4. アセンブリ構成

```text
SymphonyFrameWork.Editor ──> SymphonyFrameWork ──> SymphonyFrameWork.Core
（Editor専用）                     │
                                   ├──> SymphonyFrameWork.Enum（自動生成・Assets/Scripts 配下）
                                   └──> Unity.Addressables / Unity.ResourceManager
```

- ホスト側 `Assets/Scripts/` に新しい asmdef を作る場合は、`SymphonyFrameWork` を参照に追加してください。自動生成enumを直接使うなら `SymphonyFrameWork.Enum` も追加します。
- **`SymphonyFrameWork.asmdef` の `SymphonyFrameWork.Enum` への参照は、`PackageInitializer` がEditor起動時に自動で注入します**（`AssemblyGenerator.AddAsssemblyReference`）。submodule に asmdef の差分が出ても手で戻さないでください。

## 5. 検証（uLoopMCP）

このワークスペースには uLoopMCP が導入されており、`.claude/skills/` の `uloop-*` スキルから **Unity Editorのコンパイル・Play Mode・ログ取得をエージェント自身が実行できます**。

1. `uloop-launch` — Unity Editorが起動していない場合
2. `uloop-clear-console` — 古いログが結果を隠さないよう先にクリアする
3. `uloop-compile` — C#を変更したら必ず実行する
4. `uloop-get-logs` — エラーと警告を確認する
5. `uloop-control-play-mode` + `uloop-get-logs` — ランタイム挙動を確認する
6. `uloop-screenshot` — 表示に関わる変更の場合

Domain Reload が無効なため、**Play Modeの開始・終了を2回繰り返し、static状態のゴースト参照が残らないことを確認する**のが定番のチェックです。

**テストは `Assets/SymphonyFrameWork/Tests/` にあります**（EditMode は `Tests/Editor/`、PlayMode は `Tests/Runtime/`）。`uloop-run-tests --test-mode EditMode` と `--test-mode PlayMode` で実行します。`InternalsVisibleTo` によりテストアセンブリから `internal` な内部実装も検証できます。

テストで再現できない範囲（モーダルダイアログ、Unityのホストライフサイクル、Play Mode の往復など）は、`Assets/SymphonyFrameWork/Samples/Runtime/*Sample/` のサンプルシーンと手動確認で担保します。

権限設定は `.uloop/settings.permissions.json`（`allowThirdPartyTools: false`、`dynamicCodeSecurityLevel: 1`）にあります。

## 6. やってはいけないこと

- 自動生成物（`Assets/Scripts/SymphonyFrameWork/*Enum.cs`、`Assets/Resources/SymphonyFrameWork/*.asset`）を手で編集する。再生成で失われます。
- `Assets/SymphonyFrameWork/` 配下のアセットを移動・リネームする。`SymphonyAssetProtector` に差し戻されます。
- `Assets/SymphonyFrameWork.meta` を削除する。
- submodule の変更を push せずに、親リポジトリの gitlink だけを更新する。
- パッケージのファイルを親リポジトリ側で直接追跡・コミットする。
- `dotnet build` / `msbuild` / `csc` を使う。コンパイル可否の判断は Unity（uLoop）に委ねます。
- ルートの `*.csproj` / `*.sln` / `Library/` / `Temp/` / `Build/` をコミットする。
