# AGENTS.md — SymphonyFramework ワークスペース

このリポジトリは **Symphony Framework を開発するためのホストUnityプロジェクト**です。成果物であるフレームワーク本体は、submodule として `Assets/SymphonyFrameWork/` に置かれています。

このファイルはワークスペース固有の情報（リポジトリ構成、環境設定、自動生成物の区別、検証手段）に限定しています。フレームワークのAPIや設計規約は重複させず、下表のドキュメントへ委譲します。

## 0. 作業内容ごとの参照先

| やること | 読むもの |
| --- | --- |
| パッケージ本体（`Assets/SymphonyFrameWork/` の `Runtime/` `Editor/` `Core/`）に**機能を追加・変更する** | [.agents/skills/implement/SKILL.md](./.agents/skills/implement/SKILL.md)。設計書 → ワーカーによる実装 → 確認 → バージョン更新 → コミット → 振り返り の一連のフロー |
| パッケージ本体の**リファクタリング観点を洗い出す** | [.agents/skills/audit/SKILL.md](./.agents/skills/audit/SKILL.md)。機械走査（`scripts/audit_scan.py`）→ Project Auditor → 読解による確度付け → [Documentation/Audit/](./Documentation/Audit/) へ観点別レポート生成。**指摘と修正方針だけを出し、コードは変更しない。** 修正は観点ごとに implement のフローへ載せ替える |
| パッケージ本体のソースを修正する（小さな修正、上記フローに乗らないもの） | [Documentation/CONTRIBUTING.md](./Documentation/CONTRIBUTING.md)。コードを書く前に [Documentation/CodeGuidelines.md](./Documentation/CodeGuidelines.md)、型や名前空間を新設する前に [Documentation/DesignPhilosophy.md](./Documentation/DesignPhilosophy.md) |
| パッケージを**使う**コードをホスト側（`Assets/Scripts/` など）に書く | [Assets/SymphonyFrameWork/AGENTS.md](./Assets/SymphonyFrameWork/AGENTS.md)。使うモジュールの文書は [Documentation~/Modules/](./Assets/SymphonyFrameWork/Documentation~/Modules/) に1モジュール1ファイルであり、機能一覧は [README.md](./Assets/SymphonyFrameWork/README.md) |
| Markdown文書（`README.md`、`Documentation~/`、`AGENTS.md`、`Documentation/`）を書く | [Documentation/DocumentationGuidelines.md](./Documentation/DocumentationGuidelines.md)。文書ごとの役割、節の順序、冒頭に置かないもの、分量、AIに書かせるときの手順 |
| 利用者向けドキュメント（`README.md`、`Documentation~/**/*.md`）を変更する | 上記に加えて、変更後に `python scripts/build_module_docs.py` を実行し、`Documentation~/Html/` の生成物を同じコミットへ含める。詳細は [Documentation/CONTRIBUTING.md](./Documentation/CONTRIBUTING.md) の §6 |
| このワークスペースを題材に**技術記事を書く** | 記事は別リポジトリ `HIBIKI5201/TechArticles` にあります（`HIBIKI5201/HIBIKI5201` の `Articles/`）。執筆手順はそちらの `.claude/skills/tech-article/SKILL.md`。**このワークスペースは題材の一次資料であり、記事のために変更しません** |
| ホストプロジェクトの設定・シーン・アセットを触る | このファイル |
| 直近の変更の経緯を知る | [Assets/SymphonyFrameWork/CHANGELOG.md](./Assets/SymphonyFrameWork/CHANGELOG.md) |

## 0.1 Editor機能と削除予定のドキュメント

次の2つは、記載漏れが起きやすいわりに参照されるドキュメントです。**該当する変更をしたら、同じ変更の中で必ず更新してください。**

| 正本 | 対象 |
| --- | --- |
| [Assets/SymphonyFrameWork/Documentation~/Modules/](./Assets/SymphonyFrameWork/Documentation~/Modules/) | モジュールごとの利用方法、実装時の注意、そのモジュールのEditor機能、内部構造 |
| [Assets/SymphonyFrameWork/Documentation~/EditorTools.md](./Assets/SymphonyFrameWork/Documentation~/EditorTools.md) | Editor機能の索引と、単一モジュールに属さない横断的な仕組み（Symphony Administrator、アセット保護、設定アセットの自動生成、Editorの初期化） |
| [Assets/SymphonyFrameWork/Documentation~/Deprecations.md](./Assets/SymphonyFrameWork/Documentation~/Deprecations.md) | 非推奨API（`[Obsolete]`）と、その削除予定 |

- **作業中に、どのモジュール文書にも EditorTools.md にも記載の無いEditorモジュールを見つけたら、対応するモジュール文書へ節を追加してください。** 今回の変更で触っていないモジュールでも構いません。記載漏れは、その機能が存在しないのと同じです。記載の有無は `Assets/SymphonyFrameWork/Editor/` 配下のディレクトリと照らして判断します。
- **EditorTools.md の `## 一覧` は、モジュール文書へ移した機能も行として残します。** Editor機能の全体像をこの表1つで見られる状態を壊さないでください。
- **`[Obsolete]` を付けたら、同じ変更で Deprecations.md へ行を追加してください。** 削除予定が決まっていない場合は「未定」と書きます。書かずに済ませないでください。
- **`[Obsolete]` なメンバーを削除したら、Deprecations.md の行を `## 削除済み` へ移してください。**
- **メニューパスや設定の場所を変えたら、EditorTools.md の該当箇所を直してください。** 実際に、Project Settings へ統合済みの `Symphony Asset Lock` メニューへの言及が、廃止後もこのファイルと CONTRIBUTING.md に残っていました。

コードとドキュメントの乖離はバグとして扱います（[Documentation/CONTRIBUTING.md](./Documentation/CONTRIBUTING.md) §6）。

## 1. 2つのリポジトリ

| | ワークスペース（親） | パッケージ（submodule） |
| --- | --- | --- |
| リポジトリ | `HIBIKI5201/SymphonyWorkspace` | `HIBIKI5201/SymphonyFramework` |
| ルート | このリポジトリのルート | `Assets/SymphonyFrameWork/` |
| 役割 | 開発環境。Unityプロジェクト、検証用シーン、開発ドキュメント | 配布物。UPMパッケージ本体 |
| 既定ブランチ | `main` | `develop` |

- **コミットからgitlink更新までは `scripts/release_round.py` を使ってください。** 手順と検証がコード化してあります（`preflight` / `commit` / `finalize`）。詳細は [Documentation/CONTRIBUTING.md](./Documentation/CONTRIBUTING.md) の §5。`finalize` がPRの `develop` へのマージ、作業ブランチの削除、gitlink更新まで自動で行います。人が行うのは `develop` から `main` へのリリースだけです。
- **git コマンドを実行する前に、どちらのリポジトリが対象かを必ず確認してください。** submodule 側は `git -C "Assets/SymphonyFrameWork" ...` と明示するのが安全です。
- パッケージの特定の GitHub Issue に対応する場合は、`develop` から Issue 専用の `feature/*` ブランチを作ります。命名とPull Requestの手順は [Documentation/CONTRIBUTING.md](./Documentation/CONTRIBUTING.md) の §5 に従ってください。
- パッケージのソース変更は **submodule 側でコミットし、push してから**、親リポジトリで gitlink の更新をコミットします。手順の詳細は [Documentation/CONTRIBUTING.md](./Documentation/CONTRIBUTING.md) の §1 と §5 にあります。
- クローンは `git clone --recurse-submodules`。忘れた場合は `git submodule update --init --recursive`。
- `Assets/SymphonyFrameWork.meta` は submodule の**外側**にあり、親リポジトリが管理します。**削除しないでください**（利用側プロジェクトのGUID参照が壊れます）。

## 2. 環境

- Unity **6000.3.10f1** / URP **17.3.0** / Color Space: Linear / API Compatibility Level: .NET Standard 2.1
- Active Input Handling: **Both**（旧Input Managerと新Input Systemの両方）
- Asset Serialization: **Force Text**
- Scripting Define Symbols: `UNITY_POST_PROCESSING_STACK_V2;DOTWEEN`
- **Enter Play Mode Options が有効で、Domain Reload と Scene Reload の両方が無効です**（`ProjectSettings/EditorSettings.asset` の `m_EnterPlayModeOptions: 3`）。static状態はPlay Mode終了時にリセットされません。フレームワーク各Facadeに `ResetRuntimeState()` があるのはこのためです。この前提を崩すコードを書かないでください。
- **DOTween は `.gitignore` 済み（`Assets/Plugins/`）で、リポジトリに含まれません。** 依存しているのは `Assets/LibraryResearch/DOTween/` の検証用サンプルだけで（`LibraryResearch.DOTween.asmdef` が `DOTween.dll` を `precompiledReferences` に持つ）、**パッケージ本体（`SymphonyTween` を含む）は DOTween を参照しません。** クローン直後にコンパイルエラーが出るのは LibraryResearch 側であり、パッケージの検証には影響しません。
- 主な依存パッケージ（`Packages/manifest.json`）: Addressables 2.9.0、Input System、Cinemachine 3.1.5、Behavior、ProBuilder、Visual Effect Graph、Test Framework 1.6.0。git URL 経由で `com.unity.springbone` と `io.github.hatayama.uloopmcp`。

## 3. `Assets/` 配下の区分

| パス | 種別 | 備考 |
| --- | --- | --- |
| `Assets/SymphonyFrameWork/` | **submodule**（成果物） | 変更時は [Documentation/CONTRIBUTING.md](./Documentation/CONTRIBUTING.md) に従う |
| `Assets/Scripts/SymphonyFrameWork/` | **自動生成** | `SceneListEnum` / `TagsEnum` / `LayersEnum` / `AudioGroupTypeEnum` と `SymphonyFrameWork.Enum.asmdef`。`EnumGenerator` / `AutoEnumGenerator` が生成する。**手で編集しない** |
| `Assets/Resources/SymphonyFrameWork/` | **自動生成** | `SceneLoadConfig` / `AudioConfig` / `SaveDataConfig`。`SymphonyConfigManager.AllConfigCheck()` が `[InitializeOnLoad]` で生成する。型は `internal` なのでコードから参照できず、Inspector と Project Settings 経由でのみ設定する |
| `Assets/Scripts/DebugScripts/` | 手書きの検証用スクラッチ | ServiceLocator等の動作確認用。テストではない |
| `Assets/Level/Scenes/` | 動作確認用シーン | `NewScene` / `Scene2` / `Scene3` の3つだけがBuild Settingsに登録済み。**この登録順が `SceneListEnum` に反映される** |
| `Assets/Arts/Shaders/` | Shader Graph + HLSL | `ToonShader` / `CharacterToonShader` / `OutLineShader` / `DashedLineShader`、`ToonLighting.hlsl` / `AdvancedOutline.hlsl` |
| `Assets/Settings/` | URP設定 | `PC_*` / `Mobile_*` の RP Asset と Renderer |
| `Assets/Editor/Scripts/VersionLogGenerator.cs` | ワークスペース側のEditorツール | `Tools/VersionLogGenerator`。CHANGELOGへのエントリ追記と `package.json` の version 更新を補助する |
| `Assets/Plugins/` | **git管理外** | DOTween の導入先。既定では存在しません |
| `Assets/LibraryResearch/` | 外部ライブラリの検証用サンプル | `DOTween` / `R3` など。ライブラリ本体は含まず、未導入だとこの配下だけがコンパイルエラーになります |
| `Assets/TextMesh Pro/`, `Assets/TutorialInfo/` | Unityテンプレート由来 | 触らない |

`Assets/SymphonyFrameWork/` 配下のアセットは、`SymphonyAssetProtector`（`AssetPostprocessor`）が移動を検知して自動的に差し戻します。意図して動かす場合は `Project Settings > SymphonyFrameWork` の `Asset Protection Mode` を `Warning` または `Disabled` にしてください（保存先は `UserSettings/SymphonyFrameWork/SymphonyUserSettingConfig.asset`）。

## 4. アセンブリ構成

```text
SymphonyFrameWork.Editor ──> SymphonyFrameWork ──> SymphonyFrameWork.Core
（Editor専用）                     │
                                   ├──> SymphonyFrameWork.Enum（自動生成・Assets/Scripts 配下）
                                   └──> Unity.Addressables / Unity.ResourceManager
```

- ホスト側 `Assets/Scripts/` に新しい asmdef を作る場合は、`SymphonyFrameWork` を参照に追加してください。自動生成enumを直接使うなら `SymphonyFrameWork.Enum` も追加します。
- **`SymphonyFrameWork.asmdef` の `SymphonyFrameWork.Enum` への参照は、`PackageInitializer` がEditor起動時に自動で注入します**（`AssemblyGenerator.AddAsssemblyReference`）。submodule に asmdef の差分が出ても手で戻さないでください。

## 5. AI Skill の管理

- Skill の正本は `.agents/skills/` に置きます。Codex と Gemini CLI はこの共通パスから直接読みます。
- Claude Code は `.claude/skills/` のロケーターから正本を読みます。ロケーターを手で編集せず、正本の追加・frontmatter変更後に `python scripts/sync_agent_skill_locators.py` を実行してください。`--check` で同期状態だけを検証できます。
- uLoop skill を再生成するときは `.agents/skills/` を対象にします（CLIを使う場合は `uloop skills install --agents`）。`.codex/skills/` や `.gemini/skills/` へ個別生成しないでください。
- Gemini CLI が workspace skill を表示しない場合は、workspace を trust してから `/skills reload` を実行してください。

## 6. 検証（uLoopMCP）

このワークスペースには uLoopMCP が導入されており、`.agents/skills/` の `uloop-*` スキルから **Unity Editorのコンパイル・Play Mode・ログ取得をエージェント自身が実行できます**。

1. `uloop-launch` — Unity Editorが起動していない場合
2. `uloop-clear-console` — 古いログが結果を隠さないよう先にクリアする
3. `uloop-compile` — C#を変更したら必ず実行する
4. `uloop-get-logs` — エラーと警告を確認する
5. `uloop-control-play-mode` + `uloop-get-logs` — ランタイム挙動を確認する
6. `uloop-screenshot` — 表示に関わる変更の場合

Domain Reload が無効なため、**Play Modeの開始・終了を2回繰り返し、static状態のゴースト参照が残らないことを確認する**のが定番のチェックです。

**テストは `Assets/SymphonyFrameWork/Tests/` にあります**（EditMode は `Tests/Editor/`、PlayMode は `Tests/Runtime/`）。`uloop-run-tests --test-mode EditMode` と `--test-mode PlayMode` で実行します。`InternalsVisibleTo` によりテストアセンブリから `internal` な内部実装も検証できます。

テストで再現できない範囲（モーダルダイアログ、Unityのホストライフサイクル、Play Mode の往復など）は、`Assets/SymphonyFrameWork/Samples~/Runtime/*Sample/` のサンプルシーンと手動確認で担保します。**`Samples~` はUnityのインポート対象外です。**このワークスペースではサンプルシーンをそのまま開けません（→ [Documentation/CONTRIBUTING.md](./Documentation/CONTRIBUTING.md) の §4）。

権限設定は `.uloop/settings.permissions.json`（`allowThirdPartyTools: false`、`dynamicCodeSecurityLevel: 1`）にあります。

### Unity Editor が無い実行環境

**Claude Code on the web などのリモートコンテナには Unity Editor が無く、uLoopMCP も使えません。** `python scripts/verify_round.py` が `exit 3` を返したらその環境です。**Unity をコンテナへ導入することはできません**（配布ホストへの接続がプロキシに拒否されます）。

この場合もフローは飛ばさず、**上記の検証だけを代替の機械検査へ差し替えます。** 代替の検査、`.meta` のスクリプト生成（`scripts/generate_meta.py`）、未実施項目の残し方、git 固有の落とし穴は [.agents/skills/implement/references/remote.md](./.agents/skills/implement/references/remote.md) にまとめてあります。

## 7. やってはいけないこと

- 自動生成物（`Assets/Scripts/SymphonyFrameWork/*Enum.cs`、`Assets/Resources/SymphonyFrameWork/*.asset`）を手で編集する。再生成で失われます。
- `Assets/SymphonyFrameWork/` 配下のアセットを移動・リネームする。`SymphonyAssetProtector` に差し戻されます。
- `Assets/SymphonyFrameWork.meta` を削除する。
- submodule の変更を push せずに、親リポジトリの gitlink だけを更新する。
- パッケージのファイルを親リポジトリ側で直接追跡・コミットする。
- `dotnet build` / `msbuild` / `csc` を使う。コンパイル可否の判断は Unity（uLoop）に委ねます。
- ルートの `*.csproj` / `*.sln` / `Library/` / `Temp/` / `Build/` をコミットする。
