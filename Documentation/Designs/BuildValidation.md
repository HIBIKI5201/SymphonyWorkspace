# BuildValidation

## 目的

プレイヤービルドで壊れやすいのに、Editor上では気づけない2種類の設定不備を、ビルド前と手動実行の両方で検出する。

- **UXML/USSの依存切れ**: `Template` / `Style` の `src` が指すファイルや `.meta` の欠落、`src` のGUIDと実アセットのGUIDの不一致、インポートエラー、インポート結果から名前付き要素が消えている状態。Editorでは `Library/` に残ったキャッシュで表示できてしまい、CIやクリーンな環境のビルドで初めて画面が欠ける。
- **フォント設定の不備**: TextMesh Pro と UI Toolkit の既定フォントが未設定、Dynamicフォントの元フォントがビルド依存に含まれない、ビルド時にDynamicデータを消去する設定、必須文字が表示できない、Panel Settingsが共通のText Settingsを参照していない。Editorでは動的に生成されたグリフが見えているため気づけず、ビルド後に豆腐（□）になる。

Symphony Kill Chord の `UiDocumentBuildValidator` と `FontBuildValidator`（`Assets/Editor/Scripts/Build/`）が元である。どちらもゲーム固有のパス、必須コンテナ名、必須文字列を定数で持っているため、**それらを Project Settings の設定へ出し、既定では何も失敗させない汎用機能として取り込む。**

既存では、パッケージ自身のUXMLについてだけ `AdministratorUxmlStyleSrcTests` が `Style` の `src` を検証している。利用側のUXMLとフォントを検証する手段は無い。

## 公開API

**追加しない。** すべて `internal` にする。

- 入口は `Tools > SymphonyFrameWork > Build Validation > Validate UXML Dependencies` / `Validate Fonts` のメニューと、`IPreprocessBuildWithReport` によるビルド前検証だけである。利用側がコードから呼ぶ経路は作らない。
- 設定は Project Settings から編集する Editor Config（`internal`）であり、DesignPhilosophy「公開範囲」のどれにも該当しない。
- 公開型が増えないため `PublicTypeTestCoverageTests` の対象も増えない。

## 設定

`Project Settings > SymphonyFrameWork > Build Validation` に置く。保存先は `ProjectSettings/Packages/symphonyframework/BuildValidationConfig.asset`（プロジェクト共有、版管理に含める）。

| 項目 | 既定 | 意味 |
| --- | --- | --- |
| UXML: Validate On Build | **false** | trueならビルド前に検証し、不備があれば `BuildFailedException` でビルドを止める |
| UXML: Force Reimport | false | 検証前に対象UXML/USSを依存順に同期インポートし直す。CIで `Library/` の欠落状態を作り直す用途。重いので既定は無効 |
| UXML: Roots | 空 | 検証の起点にするUXML（`VisualTreeAsset` 参照）と、そのインポート結果に必須の要素名の一覧。**空なら `Assets/` 配下の全UXMLを起点にする**（必須要素名の検査は無し） |
| Font: Validate On Build | **false** | 同上 |
| Font: TextMesh Pro | true | TMP Settings（プロジェクト内で最初に見つかった `t:TMP_Settings`）の既定フォントを検証する。TMPが無い、またはTMP Settingsが無いなら検査を飛ばしてログに残す |
| Font: UI Toolkit | true | 起点のText Settings（下記）の既定フォントと、`Assets/` 配下の全 Panel Settings の参照を検証する |
| Font: UI Toolkit Text Settings | 未設定 | Panel Settings が共通で参照すべき Text Settings。**未設定なら「全Panel Settingsが同じText Settingsを参照していること」だけを見る** |
| Font: Required Characters | 空 | 既定フォントで必ず表示できるべき文字。**空なら文字の検査を飛ばす** |
| Font: Require Multi Atlas | false | Dynamicフォントで Multi Atlas Textures を必須にする。SKCでは必須だったが、プロジェクトの方針に依存するため既定は無効 |

**既定を「ビルドを止めない」にするのは、既存利用者のビルドを更新だけで止めないため**である（→「バージョン判断」）。

## 検証ルール

### UXML/USS依存（`UxmlDependencyValidator`）

SKC版の検査をそのまま移す。起点ごとに `Template` / `Style` の `src` を再帰的にたどる。

1. `src` が空 → エラー
2. `project://database/<path>?...` と相対パスの両方を解決し、プロジェクト外、または `.uxml` / `.uss` 以外を指す → エラー
3. ファイル本体か `.meta` が存在しない → エラー
4. `src` の `guid=` と、解決先の実GUIDが一致しない → エラー
5. 依存が循環している → エラー
6. 読み込んだ `VisualTreeAsset` / `StyleSheet` が null、または `importedWithErrors` → エラー
7. ソースXMLにある `name` 属性付き要素（`Template` を除く）が `CloneTree()` の結果に存在しない → エラー
8. 起点に設定された必須要素名が `CloneTree()` の結果に存在しない → エラー

SKC版と異なり、**最初の1件で例外を投げず、全件を集めてから報告する。** 1回のビルド失敗で全ての不備が分かるようにするためで、FontBuildValidator の形に揃える。

### フォント（`FontSettingsValidator`）

TMPのアセンブリへ参照を足さないため、**フォントとText Settingsのフィールドは `SerializedObject` のプロパティ名で読む。** TMP_FontAsset と UI Toolkit の FontAsset はどちらも同じ名前（`m_AtlasPopulationMode`、`m_IsMultiAtlasTexturesEnabled`、`m_ClearDynamicDataOnBuild`、`m_SourceFontFile`、`m_CharacterTable[].m_Unicode`）でシリアライズしている。**プロパティ名は実装前に `uloop execute-dynamic-code` で実アセットから確かめる。** 見つからないプロパティは「検証できない」というエラーとして報告し、黙って通さない。

**確認済み（2026-09-30、Unity 6000.3.10f1）**: TMP_Settings は `m_defaultFontAsset` / `m_ClearDynamicDataOnBuild`、PanelTextSettings は `m_DefaultFontAsset` / `m_ClearDynamicDataOnBuild`（先頭が大文字で TMP と違う）、PanelSettings は `textSettings`。TMP_FontAsset と UI Toolkit の FontAsset はどちらも `m_AtlasPopulationMode`（Enum: 0=Static, 1=Dynamic, 2=DynamicOS）、`m_IsMultiAtlasTexturesEnabled`、`m_ClearDynamicDataOnBuild`、`m_SourceFontFile`、`m_CharacterTable[].m_Unicode` を持つ。`AssetDatabase.FindAssets("t:PanelSettings")` は `Packages/` 配下（Cinemachine）も返すため、検索範囲を `Assets` に限る。

**DynamicOS** はOSのフォントを使いSource Font Fileをビルドへ含めないため、ルール5・6を適用しない。

Text Settings（TMP Settings / UI Toolkit Text Settings）ごとに:

1. Text Settings の `Clear Dynamic Data On Build` が有効 → エラー
2. 既定フォントが未設定 → エラー（以降の検査を飛ばす）
3. 既定フォントがDynamicで、フォント側の `Clear Dynamic Data On Build` が有効 → エラー
4. 既定フォントがDynamicで、`Require Multi Atlas` なのに Multi Atlas が無効 → エラー
5. 既定フォントがDynamicで、Source Font File が未設定 → エラー
6. Source Font File と既定フォントが Text Settings のビルド依存（`AssetDatabase.GetDependencies`）に含まれない → エラー
7. `Required Characters` の各文字が、既定フォントのCharacter Tableにも、（Dynamicなら）Source Font File（`Font.HasCharacter`）にも無い → エラー。欠けている文字を列挙する

UI Toolkitだけ:

8. `Assets/` 配下の Panel Settings が、設定した Text Settings（未設定なら最初の Panel Settings と同じ Text Settings）を参照していない → エラー

SKC版は「フォントアセットのCharacter Tableに必須文字が保存されていること」と「元フォントが収録していること」の両方を要求していた。**Dynamicフォントでは実行時に元フォントから生成できるため、どちらか一方にあればよい**と緩める（SKCは Clear Dynamic Data On Build を無効にしたうえで両方を要求していたが、それはSKCの運用であって一般則ではない）。

## ファイル構成

すべて `SymphonyFrameWork.Editor` 名前空間、`internal`。

| パス | 役割 |
| --- | --- |
| `Editor/BuildValidation/BuildValidationMenu.cs` | メニュー2つ。結果をダイアログとログへ出す |
| `Editor/BuildValidation/BuildValidationBuildProcessor.cs` | `IPreprocessBuildWithReport`。設定で有効な検証だけ走らせ、エラーがあれば `BuildFailedException` |
| `Editor/BuildValidation/Internal/BuildValidationReport.cs` | 検証名とエラー一覧。メッセージ組み立て |
| `Editor/BuildValidation/Internal/UxmlDependencyValidator.cs` | 上記UXMLルール。AssetDatabaseを触る部分 |
| `Editor/BuildValidation/Internal/UxmlSourceReference.cs` | `src` 文字列の解析（パス解決・GUID抽出）。**純粋関数** |
| `Editor/BuildValidation/Internal/FontSettingsValidator.cs` | 上記フォントルール。SerializedObjectから `FontSnapshot` を作る部分 |
| `Editor/BuildValidation/Internal/FontSnapshot.cs` | フォントとText Settingsから読んだ値。ルール判定（`CollectErrors`）は**純粋関数** |
| `Editor/Configs/ConfigData/BuildValidationConfig.cs` | `ScriptableSingleton`、`[FilePath]` は既存Configと同じ |
| `Editor/SettingProvider/BuildValidationSettingProvider.cs` | Project Settings画面（`internal`、PauseCategorySettingProviderに倣う） |
| `Editor/Documentation/SymphonyDocumentPageEnum.cs` | `BuildValidation` を追加（Project Settings画面の「ドキュメントを開く」用） |
| `Documentation~/Modules/BuildValidation.md` | 新しいモジュール文書 |
| `Documentation~/EditorTools.md` | `## 一覧` と設定ファイルの表へ行を追加 |

## 依存方向

Editorアセンブリ内で閉じる。`SymphonyFrameWork.Editor.asmdef` への参照追加は**無し**（UXMLは `UnityEngine.UIElements`、UI Toolkitのフォントは `UnityEngine.TextCore.Text`、TMPは `SerializedObject` と型名文字列の `AssetDatabase.FindAssets("t:TMP_Settings")` で扱う）。Runtime/Coreへは何も足さない。

`IPreprocessBuildWithReport` はUnityがビルド時に生成するため、Editor Orchestratorへの登録は不要である。`InitializeOnLoad`、static constructor、`EditorApplication.update` は使わない（DesignPhilosophy「避ける設計」）。

## エラー処理

- 検証の不備は例外でなくエラー一覧（`BuildValidationReport`）として集める。
- ビルド前処理ではエラーが1件でもあれば `BuildFailedException` を1回だけ投げる。メッセージに全件を並べる。
- 検証中の予期しない例外（XMLの構文エラーなど）は、その起点のエラーとして記録して次の起点へ進む。

## 影響範囲

- 既存の公開API・シリアライズ形式に変更なし。
- 新しいProject Settings アセットが1つ増える（開いたときに生成）。
- 既定ではビルドを止めないため、更新しただけの利用者に影響しない。

## テストの置き場と種別

EditMode、`Tests/Editor/`。純粋関数を中心にし、AssetDatabaseを使う部分は一時フォルダへ実アセットを作って確かめる。

| テスト | どう書くか |
| --- | --- |
| `UxmlSourceReferenceTests.Resolve_ProjectUri_ReturnsAssetPath` ほか | `project://database/Assets/A.uss?fileID=..&guid=..`、相対パス、`../` でのプロジェクト外、`.png` を与えて戻り値とエラーを見る |
| `UxmlSourceReferenceTests.TryGetGuid_*` | `guid=` の有無、大文字小文字 |
| `FontSnapshotTests.CollectErrors_*` | `FontSnapshot` を直接組み立て、ルール3〜7が該当時だけエラーを返すことを1ルール1テストで見る。必須文字が空なら文字検査しないこと、Dynamicなら元フォントにあれば通ることを含む |
| `UxmlDependencyValidatorTests.Validate_*` | `Assets/SymphonyBuildValidationTests_Temp/` に UXML/USS を `File.WriteAllText` → `AssetDatabase.ImportAsset` で作り、GUID不一致・欠落ファイル・循環・必須要素名の欠落を検出すること、正常系でエラー0件を確かめる。`[TearDown]` で `AssetDatabase.DeleteAsset` |
| `FontSettingsValidatorTests.ReadSnapshot_*` | 実際の UI Toolkit FontAsset を `FontAsset.CreateFontAsset` で作り、`SerializedObject` 経由の読み取りが期待値（Population Mode など）を返すことを確かめる。**プロパティ名の変更をテストで検出するため** |
| `BuildValidationConfigTests.*` | 既定値（両方の Validate On Build が false）|

ダイアログとProject Settings画面の描画は自動検証できない（→「動作確認手順」）。

## 動作確認手順

自動:
- `verify_round.py`（compile 0/0、EditMode/PlayMode 全数、Play Mode 2往復）
- `uloop execute-dynamic-code` で、ワークスペースの実UXML（`Editor/Administrator/UITK/`）を起点に検証を走らせ、エラー0件になること。**パッケージ自身のUXMLが通ることが最低限の実データでの確認になる**
- 同じく実プロジェクトの TMP Settings と UI Toolkit の Text Settings でフォント検証を走らせ、結果を記録する（ワークスペースの設定で不備が出るなら、それは正しい検出か誤検出かを判定して実施レポートへ書く）
- `uloop-screenshot` で Project Settings 画面の表示を見る。設定を変えて開いたままの画面が追従すること

人の操作:
- メニューから実行してダイアログが出ること
- Validate On Build を有効にし、わざとGUIDを壊したUXMLでビルドが止まること（プレイヤービルドはエージェントから走らせない）

## バージョン判断

マイナー（後方互換な機能追加。公開APIの追加は無いが、利用者から見える新しいEditor機能のため）。

## この Round で触るバージョン関連ファイル

`package.json` の `version`、`Core/SymphonyConstant.cs` の `VERSION`、`CHANGELOG.md` の見出し、`README.md` の「現在のバージョン」と機能一覧。加えて `Documentation~/EditorTools.md`、`Documentation~/Modules/BuildValidation.md`、`Documentation~/Html/`（`build_module_docs.py` の生成物）。

## Round 分割

| Round | 内容 | 対象リポジトリ |
| --- | --- | --- |
| 1 | 上記のビルド前検証（UXML/USS依存、フォント） | パッケージ（implementフロー） |
| 2 | ワークスペースのCI（下記） | ワークスペース（ホスト側の変更なのでimplementフロー外） |

### Round 2: ワークスペースのGitHub Actions

SKCの `.github/workflows/` は、ブランチ規則の検査（`ValidateFeatureMasterPR` / `ValidateDevelopPR`）、PR本文からのDraft PR自動生成、Unityでの自動ビルドから成る。**ブランチ規則とDraft PR生成はSKCの `feature/<stage>/<name>/master` 運用に固有で、`release_round.py` がマージまで行うこのワークスペースには合わない。** 取り込むのは「Unityの無い環境で回せる検査を、pushとPRごとに自動で回す」考え方だけとする。

ワークスペースには `.github/` が無く、現在これらの検査は `release_round.py preflight` を実行したときにしか走らない。`.github/workflows/verify-without-unity.yml` を追加し、`main` へのpushとPRで次を実行する（submoduleごとチェックアウト）。

- `python scripts/generate_meta.py --check`
- `python scripts/build_module_docs.py --check`
- `python scripts/sync_agent_skill_locators.py --check`
- `remote.md` の同名型の重複検査（`CS0101` の代わり）

`audit_scan.py` は指摘の一覧を出すもので合否を返さないため含めない。Unityを使うビルド・テストはライセンスとランナーの都合で対象外とする（SKCは自前ランナーで回している）。

## 実施レポート

実施日: 2026-09-30 / バージョン: 6.15.0 / PR: HIBIKI5201/SymphonyFramework#224

### 実装した内容

Round 1 を設計のファイル構成どおりに実装した（Codex CLI ワーカーへ委譲し、差分を通読して修正）。

- UXMLルール1〜8: `Editor/BuildValidation/Internal/UxmlDependencyValidator.cs`。`src` の解析は純粋関数 `UxmlSourceReference.cs`
- フォントルール1〜8: 読み取りは `FontSettingsValidator.cs`（SerializedObject）、判定は純粋関数 `FontSnapshot.CollectErrors`
- 入口: `BuildValidationMenu.cs`（メニュー2つ）、`BuildValidationBuildProcessor.cs`（`callbackOrder` -300）
- 設定: `Editor/Configs/ConfigData/BuildValidationConfig.cs`、`Editor/SettingProvider/BuildValidationSettingProvider.cs`
- 文書: `Documentation~/Modules/BuildValidation.md` を新設し、EditorTools.md、index.md、README.md、`SymphonyDocumentPageEnum` / `SymphonyDocumentPathResolver` へ追加
- ワークスペース側: `scripts/build_module_docs.py` の `MODULE_ORDER` へ `BuildValidation` を追加（索引の順序検査に必要）

### 設計から変えた点

- **フォントのルール1（Text Settings の Clear Dynamic Data On Build）を、既定フォントが Dynamic / DynamicOS のときだけ適用するよう変えた。** ワークスペースの実データ（`Assets/GameProject/_Tanks/.../TMP Settings.asset`、既定フォントは Static の LiberationSans SDF）で誤検出したため。この設定は Dynamic フォントのデータだけに作用する。SKC 版の無条件の判定をそのまま移したのが設計の誤りだった。テスト `CollectErrors_StaticFontWithTextSettingsClear_ReturnsNoErrors` を追加
- ワーカーが `Debug.Log` を直接使っていたため `SymphonyDebugLogger.LogDirect` へ置き換えた（`FrameworkLoggingTests` が検出）
- `FontSettingsValidatorTests` の SetUp で、`FontAsset.CreateFontAsset` の生成物が保存しない指定（`HideFlags`）のままアセット化されて Unity がアサーションを出していたため、アセット化の前に `hideFlags` を外すよう直した
- 設計のテスト一覧に加え、パッケージ自身の Symphony Administrator の UXML を実データとして検証する `Validate_AdministratorUxml_ReturnsNoErrors` を追加した

### 検証結果

- `verify_round.py`（最終）: compile エラー0/警告0、EditMode 812/812、PlayMode 21/21 × 2往復、Enter Play Mode Options 3 へ復元。Console の警告4件は `Assets/Scripts` の検証用スクラッチ（`TestNameSpace`）由来で本変更と無関係
- `release_round.py preflight`: 全項目OK（tests: ソース5件に対しテスト10件）
- 実データ（一時テストで実行し、確認後に削除）: `Assets/` 配下の全UXML でエラー0件。TMP Settings は上記の誤検出を直した後エラー0件
- Project Settings 画面をスクリーンショットで確認（表示崩れ・文言の誤り無し）

### 未実施の確認

- 「人の操作」の2項目: メニュー実行時のダイアログ表示、`Validate On Build` 有効時にプレイヤービルドが止まること
- 「自動」のうち「設定を変えて開いたままの画面が追従すること」。設定型が `internal` で `execute-dynamic-code` から書き換えられないため未確認（`DrawSettings` は毎回 `SerializedObject.Update()` を呼ぶ実装）
- UI Toolkit のフォント検証は、ワークスペースに PanelTextSettings が無いため実データでは未確認（テストでは実 FontAsset と PanelTextSettings を作って検証済み）

### 振り返り

- **版を上げた直後に、ホスト側の `Assets/Editor/Scripts/SymphonySampleImporter.cs` の自動取り込みが `Assets/Samples/Symphony Framework/6.15.0/` を作り、残っていた `6.14.6/` とサンプルの型が重複して（CS0101）検証が止まった。** 版を上げる Round では毎回起きる。古い版のフォルダを片付けるよう importer を直すことを別タスクとして提案した（今回は `6.14.6/` を退避して回避）
- **SKC 版を移す際、判定条件の妥当性を「元がそうしていたから」で受け入れていた。** 実データで回して初めて誤検出に気づいた。他プロジェクトの検証ロジックを取り込む設計では、ルールごとに「どの条件で問題になるか」を書く欄があるとよい（design-doc.md のテンプレートへの追加候補）
- ワーカーの報告が韓国語で返った。プロンプトに出力言語の指定が無かったため（worker.md のテンプレートへ「日本語で報告する」を足す候補）
