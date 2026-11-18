# 7.0.0 命名の全面整理

> **状態: Round 1 実装済み（Unity での検証は R6 でまとめて行う）。**
> 親Issue: #227。子Issue: #226（ルート名前空間）、#225（サービス名の音楽用語化）、#232（シーン管理の入口の整理）、#233（Service Locator の入口の整理）。

## 目的

7.0.0 で、フレームワークの公開名と公開APIの入口を全面的に整理する。

- ルート名前空間・asmdef・フォルダの `SymphonyFrameWork` を `SymphonyFramework` へそろえる（#226）
- 公開サービス名を音楽用語へ改名し、名前空間 `SymphonyFrameWork.System.*` をやめる（#225）
- 同じ改名の中で、入口が多い・役割が重なるAPIを整理する（#232、#233）
- 「次のメジャー更新」で削除すると予告していた `[Obsolete]` を削除する（`Deprecations.md`）

#227 の方針どおり、**子Issueの作業は `develop` に順に入れ、7.0.0 のリリース（`develop` → `main`）はすべて終わってから行う。** 途中の Round は単独ではリリースしない。

## 全体の Round 分割

| Round | 内容 | 対応Issue | 互換の扱い |
| --- | --- | --- | --- |
| **R1** | ルート名前空間・asmdef・定数・生成物のパスを `SymphonyFramework` へ。利用側プロジェクトの生成物フォルダを移行する処理。ワークスペースの submodule パス | #226 | `[SerializeReference]` で保存される型の `[MovedFrom]` だけ入れる（入れないと、この Round の時点でワークスペースの設定アセットが壊れるため）。旧名前空間の転送は R6 |
| **R2** | `ServiceLocator` + `ServiceInjector` → `Partitura`。取得・解除の入口の整理 | #225（Partitura）、#233 | 入れない（R6） |
| **R3** | `SceneLoader` → `Attacca`、`SceneBlockLoader` → `Suite`。ロード入口の統合、ブロックが持つシーンの保護 | #225（Attacca・Suite）、#232 | 入れない（R6） |
| **R4** | `SaveStore` → `Manuscript`、`AudioManager` → `Sonority` | #225 | 入れない（R6） |
| **R5** | `PauseManager` → `Fermata`。6.12.0 で非推奨にした旧ポーズAPIと、`Deprecations.md` で「次のメジャー更新」としていた旧APIの削除 | #225、Deprecations.md | 入れない（R6） |
| **R6** | 互換レイヤー: 旧名前空間・旧Facade名の `[Obsolete]` 転送クラス、6.x の名前を指す `[MovedFrom]`、移行ガイド、`Deprecations.md`、版を 7.0.0 へ。#227 の「リリース前のチェック」 | #227 | ここでまとめて入れる |

### 互換レイヤーを最後の Round へまとめる理由

**利用者は 6.x から 7.0.0 へ直接上がる。R1〜R5 の途中の名前は、どの版としても配布されない。** そのため `[MovedFrom]` は常に「6.x での名前」を指す必要がある。

`[MovedFrom]` は1つの型に1つしか付けられない。R1 で `SymphonyFrameWork.System.ServiceLocate.ServiceLocator` を指す `[MovedFrom]` を付けても、R2 で型名と名前空間が変わるため、R2 で付け直すことになる。**各 Round で付けると、毎回付け直しになり、途中の名前を指したまま残す事故も起きやすい。** 改名がすべて終わった R6 で、6.x の名前と 7.0.0 の名前の対応表から一括で付ける。

例外は `[SerializeReference]` で保存される型だけである。これは利用側だけでなく**ワークスペース自身の `SaveDataConfig.asset`** にも `asm: SymphonyFrameWork` が焼かれていて、R1 でアセンブリ名を変えた時点でローダーが解決できなくなる。R1 で付け、R4（`Manuscript`）で名前空間が変わるときに付け直す。

### 各 Round の大きさ

Round の目安（20ファイル以内）は R1 では守れない。名前空間の置換が `.cs` 300件以上に及ぶためである。**R1 の差分の大半は機械的な置換であり、置換以外の変更（定数、パス、移行処理、asmdef）を別のコミットに分けて、レビューで読む範囲をそちらに絞る。** 詳しくは R1 の「コミットの分け方」。

---

# Round 1: ルート名前空間を `SymphonyFramework` へそろえる（#226）

## 目的

ルート名前空間、asmdef 名、フォルダ名、定数の `SymphonyFrameWork`（W が大文字）を `SymphonyFramework` へそろえる。製品名は `Symphony Framework`、パッケージ名は `symphonyframework` で、「Framework」は1語であるため。

## 公開API

**型名・メンバー名は変えない。名前空間とアセンブリ名だけが変わる。**

| 現在 | R1 の後 |
| --- | --- |
| 名前空間 `SymphonyFrameWork` とその配下すべて（`SymphonyFrameWork.System.ServiceLocate` など） | `SymphonyFramework` とその配下（`SymphonyFramework.System.ServiceLocate` など。`System.*` 自体は R2 以降で消す） |
| asmdef `SymphonyFrameWork` / `.Core` / `.Editor` / `.Tests.Editor` / `.Tests.Runtime` | `SymphonyFramework` / `.Core` / `.Editor` / `.Tests.Editor` / `.Tests.Runtime` |
| 自動生成 asmdef `SymphonyFrameWork.Enum` / `SymphonyFrameWork.PauseCategory` | `SymphonyFramework.Enum` / `SymphonyFramework.PauseCategory` |
| `SymphonyConstant.SYMPHONY_FRAMEWORK`（値 `"SymphonyFrameWork"`） | 値を `"SymphonyFramework"` へ。定数名は変えない |
| サンプルの名前空間 `SymphonyFrameWork.Samples.*` | `SymphonyFramework.Samples.*` |

新しい公開型は作らない。追加する移行処理は `internal`。

## ファイル構成

### パッケージ（`Assets/SymphonyFrameWork/` → R1 後は `Assets/SymphonyFramework/`）

| 対象 | 変更 |
| --- | --- |
| `**/*.cs`（約330件）、`Samples~/**/*.cs` | 名前空間・`using`・完全修飾名の置換 |
| `SymphonyFrameWork.asmdef` ほか asmdef 5件 | ファイル名と `name`、名前で参照している `references` を変更。**`.meta` はファイルと一緒に改名し、GUID を保つ** |
| `Core/AssemblyInfo.cs`、`Runtime/AssemblyInfo.cs`、`Editor/AssemblyInfo.cs` | `InternalsVisibleTo` の文字列 |
| `Core/SymphonyConstant.cs` | `SYMPHONY_FRAMEWORK` の値。これを使う `RESOURCES_EDITOR_PATH`、`ENUM_PATH`、Library のログパス、`FRAMEWORK_PATH`、ログの接頭辞が追従する |
| `Editor/Generator/EnumGenerate/EnumGenerator.cs`、`Editor/Generator/PauseCategoryGenerate/PauseCategoryGenerator.cs` | 生成する asmdef 名 |
| `Editor/Administrator/UITK/CS/SaveDataWindow.cs` | SessionState のキー |
| `Editor/Generator/AssetStoreToolsPackager/Pipeline/AssetStoreToolsPackagePipeline.cs` | `CreateAssetMenu` のメニュー名 |
| `Editor/SymphonyAssetProtector.cs` | メッセージ |
| `*.uxml` / `*.uss` | 名前空間を含む属性があれば置換 |
| `Runtime/Service/SaveSystem/Internal/Infrastructure/*SaveDataLoaderStrategy.cs` と `Template/PlayerPrefsSaveDataLoaderStrategy.cs` | `[MovedFrom]` を旧名前空間・旧アセンブリへ向ける（「互換」を参照） |
| **新規** `Editor/Configs/Internal/RootFolderCaseMigration.cs`（名前空間 `SymphonyFramework.Editor`、`internal static`） | 利用側プロジェクトの生成物フォルダを新しい表記へ移す（「利用側プロジェクトの移行」を参照） |
| `Tests/Editor/TypeModuleOwnershipTests.cs`、`PauseCategoryGeneratorTests.cs`、`SamplePackagingTests.cs` | 期待値の文字列 |
| **新規** `Tests/Editor/RootFolderCaseMigrationTests.cs` | 移行計画のテスト |
| `README.md`、`AGENTS.md`、`Documentation~/**/*.md`（約210か所）、生成HTML | 表記の置換。HTML は `scripts/build_module_docs.py` で再生成 |
| `package.json`、`SymphonyConstant.VERSION`、`CHANGELOG.md`、`README.md` | 版（「バージョン判断」） |

### ワークスペース

| 対象 | 変更 |
| --- | --- |
| `.gitmodules`、submodule のパス | `Assets/SymphonyFrameWork` → `Assets/SymphonyFramework`。**大文字小文字だけの改名なので、一時名を経由する2段階で行う** |
| `Assets/SymphonyFrameWork.meta` | `Assets/SymphonyFramework.meta` へ改名。**GUID を保つ。削除しない** |
| `Assets/Resources/SymphonyFrameWork/`、`Assets/Scripts/SymphonyFrameWork/`、`Assets/Editor/SymphonyFrameWork/` | パッケージの移行処理と同じ結果になるよう改名（`.meta` を保つ）。生成 asmdef の `name` も変更 |
| `Assets/Scripts/DebugScripts/*.cs`、`Assets/Editor/Scripts/*.cs` | `using` の置換 |
| `Assets/Level/Scenes/*.unity` | 名前空間・アセンブリ名を文字列で持っている箇所があれば置換（`[SerializeReference]` など）。**GUID 参照は触らない** |
| `scripts/*.py`、`.github/workflows/verify-without-unity.yml`、`.agents/skills/**`、`AGENTS.md`、`Documentation/*.md` | パスと表記の置換。**`Documentation/Designs/`、`Reports/`、`Audit/` の過去の記録は書き換えない**（当時の名前の記録であるため） |
| `.idea/` | 置換しない（各自の環境で再生成される） |

## 利用側プロジェクトの移行

R1 で `SYMPHONY_FRAMEWORK` の値が変わると、次の場所が新しい表記を指す。**既存プロジェクトには旧表記のフォルダが残っている。**

| 場所 | 中身 | 読み書きする処理 |
| --- | --- | --- |
| `Assets/Resources/SymphonyFrameWork/` | 設定アセット（`SceneLoadConfig` など） | `SymphonyConfigLocator.GetConfig`（`Resources.Load`）、`SymphonyConfigManager` |
| `Assets/Editor/SymphonyFrameWork/Configs/` | Editor 用設定アセット | `RESOURCES_EDITOR_PATH` |
| `Assets/Scripts/SymphonyFrameWork/` | 自動生成 enum と `SymphonyFrameWork.Enum.asmdef`、`PauseCategory/` | `EnumGenerator`、`PauseCategoryGenerator` |
| `UserSettings/SymphonyFrameWork/` | ユーザー設定 | `SymphonyUserSettingConfig` |
| `Library/SymphonyFrameWork/Cache/` | ログのキャッシュ | 移行しない（消えても困らない） |

**Windows と macOS では、`SymphonyFrameWork` と `SymphonyFramework` は同じフォルダとして開けてしまう。** そのため移行しなくても Editor では動いて見えるが、次の問題が残る。

- `AssetDatabase` のパス文字列は大文字小文字を区別する場面がある（`StartsWith` による判定、`IsValidFolder`）
- Linux（CI、ビルドサーバー）では別のフォルダになる。旧フォルダの設定は読まれず、`SymphonyConfigManager` が既定値の設定を新しく作る。**エラーもログも出ない**（`SceneLoadConfigRename.md` で実測した障害と同じ形）
- ビルドの `Resources` で大文字小文字が区別されるかは、Unity の実装に依存する

### 方針

**`[InitializeOnLoad]` の初期化（`PackageInitializer`）で、`SymphonyConfigManager.AllConfigCheck()` より前に、旧表記のフォルダを新しい表記へ改名する。**

1. `AssetDatabase.MoveAsset` で、まず一時名（`SymphonyFramework__case_migration`）へ、次に新しい表記へ移す。**大文字小文字だけの改名は、Windows と macOS では1回では反映されないため。** `MoveAsset` を使うのは、`.meta` と GUID を保つため
2. 生成 asmdef（`SymphonyFrameWork.Enum.asmdef`、`SymphonyFrameWork.PauseCategory.asmdef`）は、ファイルを `MoveAsset` で改名したうえで `name` を書き換える。**GUID を保つので、GUID で参照している利用側の asmdef は壊れない**
3. `UserSettings/` は `AssetDatabase` の管理外なので、`System.IO` で同じ2段階の改名をする
4. 移したものを1行ずつログへ出す。失敗したら、何が旧表記のまま残っているかをエラーで出し、**移行を途中で止める**（半端に移した状態で設定を新規作成させない）

**判定と手順の組み立ては、Unity API へ触れない `internal static` の関数へ切り出す。**「どのパスが旧表記で、どの順にどこへ移すか」を返す純粋な関数をテストし、`AssetDatabase` を呼ぶ部分は薄くする。

### 確かめる必要がある前提

`SceneLoadConfigRename.md` では、**型を改名したあと旧アセットを `LoadAssetAtPath` で読めなくなり、設定値が失われた。** R1 では型名も `.cs` の GUID も変えないので同じ障害は起きない見込みだが、**アセンブリ名が変わったあとも `m_Script` の GUID だけで設定アセットが読めること**を、PC の Unity で最初に確かめる。読めなければ R1 を止めて設計を見直す。

## 依存方向

変えない。asmdef の参照関係（`Editor → Runtime → Core`、`PauseCategory → Runtime`）はそのまま。移行処理は Editor アセンブリに置き、Runtime からは呼ばない。

## エラー処理

| 状況 | 動き |
| --- | --- |
| 旧表記のフォルダが無い | 何もしない |
| 旧表記と新しい表記の両方が別のフォルダとして存在する（Linux、または手で作った） | 自動では統合せず、両方のパスをエラーで出して移行を止める。利用者が手で片方へまとめる |
| `MoveAsset` が失敗した | 返されたメッセージと対象パスをエラーで出し、以降の移行をしない |

## 互換

**R1 で保証するもの:**

- **MonoBehaviour / ScriptableObject の参照**: `.cs` の `.meta` を変えないので GUID は保たれる
- **GUID で参照している利用側の asmdef**: asmdef の `.meta` を改名と一緒に動かして GUID を保つ
- **`[SerializeReference]` で保存されたセーブローダー**: `SaveDataConfig` は `asm: SymphonyFrameWork, ns: SymphonyFrameWork.System.SaveSystem` を保存している。ローダー3型の `[MovedFrom]` を `sourceNamespace: "SymphonyFrameWork.System.SaveSystem"`、`sourceAssembly: "SymphonyFrameWork"` へ向ける
- **利用側の生成物フォルダ**: 上の移行処理

**R1 では保証せず、R6 で扱うもの:**

- 利用側コードの `using SymphonyFrameWork...`。R1 の時点では、利用側コードはコンパイルが通らない（R6 で、6.x の名前を指す `[MovedFrom]` と転送クラスを入れる）
- 名前で参照している利用側の asmdef

**R1 で失うもの（要判断。下の質問1）:**

- ローダーの `[MovedFrom]` は現在、2.x 以前の旧クラス名（`JsonUtilitySaveDataLoader` など）を指している。**`[MovedFrom]` は1つしか付けられないため、旧アセンブリ名へ向け直すと、旧クラス名のまま保存されている設定は解決できなくなる。** ワークスペースの `SaveDataConfig.asset` は既に新しいクラス名で保存されている

## テストの置き場と種別

EditMode（`Tests/Editor/`）:

- `RootFolderCaseMigrationTests.Plan_OldCaseFolder_ReturnsTwoStepMove` — 旧表記のパスの一覧を渡し、一時名経由の2段階の移動が順に返ることを、返り値のリストで検証する
- `RootFolderCaseMigrationTests.Plan_NoOldFolder_ReturnsEmpty`
- `RootFolderCaseMigrationTests.Plan_BothCasesAsDistinctFolders_ReturnsConflict` — 両方が別に存在する入力で、移動ではなく衝突が返ること
- `RootFolderCaseMigrationTests.Plan_GeneratedAsmdef_RenamesFileAndName` — 生成 asmdef のファイル名と `name` の書き換えが計画に入ること
- 既存の `TypeModuleOwnershipTests`、`PauseCategoryGeneratorTests`、`SamplePackagingTests` の期待値を新しい名前へ
- `SaveDataLoaderStrategyMovedFromTests.Loaders_HaveMovedFromToOldAssembly` — 3型の `MovedFromAttribute` の値をリフレクションで検証する

自動化できない確認（PC の Unity で人またはエージェントが行う。「動作確認手順」）:

- 旧表記のフォルダを持つプロジェクトを開いたときの、`AssetDatabase.MoveAsset` の実際の動き
- 設定アセットの値が移行後も保たれていること

## 動作確認手順

PC のワークスペース（`C:\Sinfonia Studio\SymphonyFrameworkWorkspace`）で行う。

1. **作業前に** `Project Settings > SymphonyFrameWork` の `Asset Protection Mode` を `Disabled` にする（アセット保護がフォルダの移動を差し戻すため）
2. 設定アセット（`SceneLoadConfig` の `_isResetAndLoadOnPlay` など）の現在値を控える
3. ブランチを取り込み、Unity を開く。`python scripts/verify_round.py` を実行する。コンパイルはエラー0・警告0、EditMode と PlayMode のテストが全数成功、Play Mode の開始・終了を2回繰り返す
4. 設定アセットの値が 2. と同じであること
5. `Project Settings > SymphonyFramework` と `Window > SymphonyFramework` が新しい表記で出ること
6. Save Data の Administrator パネルで、ローダーが解決されていること（`SaveDataConfig` の `[SerializeReference]`）
7. **移行処理の確認**: 別のプロジェクト（または一時コピー）に 6.15.2 を入れて生成物を作らせてから R1 へ更新し、旧表記のフォルダが新しい表記へ移り、設定値と GUID が保たれていることを見る

## コミットの分け方

差分のレビューを機械的な置換と、それ以外に分けるため、submodule 側は次の順にコミットする。

1. **置換だけ**: `SymphonyFrameWork` → `SymphonyFramework` の文字列置換と、asmdef・`.meta` の改名。手で書いた変更を含めない
2. **置換以外**: 移行処理、`[MovedFrom]`、生成器、テスト
3. **文書**: 生成HTMLの再生成、CHANGELOG、版

レビューでは 1. について「置換以外の差分が混ざっていないこと」を機械的に確かめ（置換を逆に適用すると元に戻ること）、2. と 3. を読む。

## バージョン判断

**破壊的変更（メジャー）。** ただし #227 の方針で、7.0.0 は R6 の後にだけ出す。R1〜R5 の版は質問2で決める。

## この Round で触るバージョン関連ファイル

`package.json` の `version`、`Core/SymphonyConstant.cs` の `VERSION`、`CHANGELOG.md` の見出し、`README.md` の「現在のバージョン」、生成HTML の `README.html` と `CHANGELOG.html`。

## 決定事項（2026-11-18、依頼者の回答）

1. **2.x 以前の旧ローダー名のまま保存された `SaveDataConfig` は切り捨てる。** 6.x の間に一度開いて保存していれば、新しいクラス名で保存されているため
2. **R1〜R5 はマイナーを上げる（R1 = 6.16.0、R2 = 6.17.0 …）。** R6 で 7.0.0 にする
3. **Unity での検証は R6 でまとめて行う。** R1〜R5 はリモートの代替検査（`remote.md`）だけで進め、未実施の確認を下の「未実施リスト」へ積み上げる

## 実施レポート（Round 1）

実施日: 2026-11-18 / バージョン: 6.16.0 / PR: （作成後に追記）

### 実装した内容

submodule 側は3コミットに分けた。

1. `96b4783` 置換だけ: 405ファイルの `SymphonyFrameWork` → `SymphonyFramework` と、asmdef 5件の改名（`.meta` は一緒に改名して GUID を保った）。`CHANGELOG.md` と生成HTMLの CHANGELOG は過去の記録なので除外。**置換を逆に適用すると親コミットと一致することを機械的に確認した**
2. `701e5a7` 置換以外:
   - `Editor/Configs/RootFolderCaseMigration.cs`（新規、`internal static`）。`SymphonyEditorOrchestrator` の初期化で `PackageInitializer` より前に呼ぶ
   - セーブローダー2型の `[MovedFrom]` を旧名前空間・旧アセンブリへ向け直した
   - Asset Store Tools Package Pipeline の手順4型へ `[MovedFrom]` を追加（下の「設計から変えた点」）
   - `Tests/Editor/RootFolderCaseMigrationTests.cs`（新規、11ケース）
   - 置換で触れた2ファイルへ UTF-8 BOM を付けた（preflight の指摘）
3. `3f3fd6b` 文書: 版を 6.16.0 へ、CHANGELOG、`EditorTools.md` の「設定アセットの自動生成」へ移行処理の説明を追加、生成HTMLを再生成

ワークスペース側:

- submodule のパスを `Assets/SymphonyFramework` へ、`Assets/SymphonyFrameWork.meta` を `Assets/SymphonyFramework.meta` へ改名（GUID 維持）。**`.gitmodules` の submodule 名（`[submodule "Assets/SymphonyFrameWork"]`）は変えていない。** 名前を変えると各クローンの `.git/modules/` の場所も変わり、既存クローンの更新手順が増えるため
- `scripts/*.py`、`.github/workflows/verify-without-unity.yml`、`.agents/skills/**`、`.claude/skills/**`、`AGENTS.md`、`Documentation/*.md`（`Designs/`・`Reports/`・`Audit/` を除く）、`Assets/Editor/Scripts/*.cs`、`Assets/Scripts/DebugScripts/*.cs`、`SymphonyFrameworkWorkspace.slnx` の表記を置換

### 設計から変えた点

- **Asset Store Tools Package Pipeline の手順（`AssetStoreToolsCombinePackageStrategy` / `CreateZipStrategy` / `SinglePackageStrategy` / `UsedDependenciesStrategy`）にも `[MovedFrom]` を付けた。** 設計では `[SerializeReference]` で保存される型を `SaveDataConfig` のローダーだけと見ていたが、ワークスペースの `Assets/Editor/SymphonyFrameWork/Configs/AssetStoreToolsPackagePipeline*.asset` に `asm: SymphonyFrameWork.Editor` が保存されていた。利用側（SymphonyKillChord など）のパイプラインアセットも同じ形なので、付けないと手順が消える
- **ワークスペースの `Assets/Resources/SymphonyFrameWork/`、`Assets/Scripts/SymphonyFrameWork/`、`Assets/Editor/SymphonyFrameWork/` は改名しなかった。** 設計では手で改名する予定だったが、PC で Unity を開いたときに移行処理に移させれば、**移行処理そのものの実地検証になる。** 移った結果を R6 の検証でコミットする
- `.unity` / `.asset` / `ProjectSettings` の `m_EditorClassIdentifier` は置換しなかった。Unity が保存時に書き直す表示用の値で、型の解決には GUID を使うため

### 検証結果（リモートの代替検査）

- `release_round.py preflight`: tests OK（ソース218件・テスト107件の変更）、version OK（6.16.0）、meta OK、layer OK、asmdef OK、docs OK、question OK。**NG 2件はどちらもこの Round の変更ではない**: submodule のブランチ名がこのセッションの指定（`claude/project-thread-lzbam6`）で命名規則外、ワークスペースの Enter Play Mode Options が `Options=1`（`main` の時点でそうなっている）
- `generate_meta.py --check`: OK（483件）。**`RootFolderCaseMigration.cs.meta` と `RootFolderCaseMigrationTests.cs.meta` はスクリプトで生成した**
- `build_module_docs.py --check`: OK（21件）
- `sync_agent_skill_locators.py --check`: OK
- 同名型の検査: `IInjectable`（ジェネリック引数違い、既知）だけ

### 未実施リスト（R6 の検証で確認する）

- [ ] コンパイル エラー0・警告0、EditMode・PlayMode テスト全数成功、Play Mode 2往復
- [ ] **アセンブリ名が変わったあとも、設定アセットが `m_Script` の GUID で読めて値が保たれること**（`SceneLoadConfigRename.md` の障害の再発がないこと）
- [ ] PC のワークスペースを開いたとき、`Assets/Resources`・`Assets/Scripts`・`Assets/Editor`・`UserSettings` の `SymphonyFrameWork` が `SymphonyFramework` へ移り、GUID と設定値が保たれること。移った結果をコミットする
- [ ] `SaveDataConfig` のローダーと、パイプラインアセットの手順が解決されること（`[MovedFrom]`）
- [ ] `InitializeOnLoad` の中で `AssetDatabase.MoveAsset` が期待どおり動くこと
- [ ] 生成した `.meta` 2件を Unity が再生成・差分にしないこと
- [ ] **PC の既存クローンで、submodule のパス変更（大文字小文字だけ）を取り込めること。** Windows では一時名を経由しないと反映されない可能性がある

### 振り返り

- `[SerializeReference]` の型の洗い出しを、設計の時点で Runtime の `SaveDataConfig` だけに絞っていた。**ワークスペースのアセットに `asm:` で検索をかけていれば、設計の時点で見つかった。** 次の Round（型名と名前空間がさらに変わる）では、着手前に `git grep -n "asm: Symphony"` をワークスペースと利用側で実行して対象を確定する。implement スキルの `rounds.md` への追記を提案する

---

# Round 2: `ServiceLocator` + `ServiceInjector` → `Partitura`、入口の整理（#225、#233）

> **状態: 設計中。下の「依頼者に決めてもらうこと」の回答待ち。** 版は 6.17.0。

## 目的

- `ServiceLocator` と `ServiceInjector` を1つの Facade `Partitura`（名前空間 `SymphonyFramework.Services`、フォルダ `Runtime/Services/`）へまとめる（#225）
- 取得・解除の入口を減らし、インスタンスを渡すAPIの挙動をそろえる（#233）
- `SymphonyConfigLocator` の「Locator」という紛らわしい名前をなくす（#233-4）

## 公開API（6.16.0 → 6.17.0）

| 6.16.0 | 6.17.0 | 備考 |
| --- | --- | --- |
| `ServiceLocator` / `ServiceInjector` | `Partitura` | `static class` |
| `RegisterInstance<T>(T, LocateTypeEnum)` / `(Type, object, …)` | `Register<T>(T, ServiceLifetimeEnum)` / `Register(Type, object, …)` | |
| `RegisterInstanceWithAutoDispose` | `RegisterWithAutoDispose` | |
| `UnregisterInstance<T>()` / `(Type)` / `<T>(T)` | `Unregister<T>()` / `(Type)` / `<T>(T)` | `(T)` は同一参照のときだけ解除（6.15.2 と同じ） |
| `DestroyInstance<T>()` / `<T>(T)` | `Destroy<T>()` / `<T>(T)` | **`(T)` を同一参照のときだけ破棄に変更**（質問2） |
| `IsExistInstance<T>()` / `(Type)` / `<T>(T)` | `IsRegistered<T>()` / `(Type)` / `<T>(T)` | **`(T)` を同一参照の判定に変更**（質問2） |
| `GetInstance<T>()`（無ければ null） | 削除（質問1） | `TryGet` へ移行 |
| `GetRequiredInstance<T>()` | `Get<T>()`（無ければ `ServiceNotRegisteredException`） | 質問1 |
| `TryGetInstance<T>(out T)` | `TryGet<T>(out T)` | |
| `GetInstanceAsync` / `TryGetInstanceAsync` | `GetAsync` / `TryGetAsync` | |
| `RegisterAfterLocate<T>(Action)` / `(Action<T>)` | `WhenRegistered<T>(Action<T>)` だけ | 質問3 |
| `GetRegistrationInfos` / `TryGetRegistrationInfo` | 名前そのまま | デバッグ用 |
| `ServiceInjector.Inject` / `CreateInstance` | `Partitura.Inject` / `Partitura.CreateInstance` | `TryAutoInject` は internal のまま |
| `ServiceLocateComponent` | `ServiceComponent` | |
| `SymphonyLocateObject<T>`（`GetInstance` / `GetInstanceAsync` …） | `ServiceReference<T>`（`Get` / `GetAsync` / `TryGet`） | 質問4。`Get` は `Partitura.Get` と同じく必須取得 |
| `LocateTypeEnum.Singleton` / `.Locator` | `ServiceLifetimeEnum.Persistent` / `.Scene` | 質問5 |
| `IInjectable<…>`、`ServiceNotRegisteredException`、`ServiceRegistrationInfo` | 名前そのまま、名前空間だけ `SymphonyFramework.Services` | |
| `SymphonyConfigLocator`（public） | `internal` にして名前を `SymphonyConfigProvider` へ | 質問6 |

### 入口の使い分け（ServiceLocator.md → Partitura.md に表で載せる）

| したいこと | 使うAPI |
| --- | --- |
| 必ずあるはずのサービスを取る | `Get<T>()` |
| 無いかもしれないサービスを取る | `TryGet<T>(out T)` |
| 登録されるまで待って取る | `GetAsync<T>()` / `TryGetAsync<T>()` |
| 登録されたら処理する（コールバック） | `WhenRegistered<T>(Action<T>)` |
| フィールドに持って何度も取る | `ServiceReference<T>` |
| 依存を注入する | `IInjectable` の自動注入、`Inject`、`CreateInstance` |

## 依頼者に決めてもらうこと

1. **取得APIの数**: ⭐a. `Get`＝必須（無ければ例外）、`TryGet`＝任意、`GetAsync`/`TryGetAsync`＝待つ。null を返す取得は無くす（6.x の `GetInstance` は R6 の互換層で従来どおり null を返す） / b. `Get`＝null を返す、`GetRequired`＝必須 として両方残す
2. **インスタンスを渡すAPI**: ⭐a. `Unregister`・`Destroy`・`IsRegistered` の `(T instance)` をすべて同一参照の判定にそろえる / b. `Unregister(T)` 以外の `(T instance)` 版を削除する
3. **`RegisterAfterLocate`**: ⭐a. `WhenRegistered<T>(Action<T>)` だけ残す（`Action` 版は `_ => …` で書ける） / b. 両方残す
4. **`SymphonyLocateObject<T>`**: ⭐a. `ServiceReference<T>` として残す / b. 削除して `TryGet` に寄せる
5. **`LocateTypeEnum` の値**: ⭐a. `Persistent`（Partitura の GameObject の子にして永続化）/ `Scene`（親子関係を変えない） / b. 値の名前は `Singleton` / `Locator` のまま
6. **`SymphonyConfigLocator`**: ⭐a. `internal` にして `SymphonyConfigProvider` へ改名（SymphonyKillChord では未使用。Editor とテストは `InternalsVisibleTo` 済み） / b. public のまま改名だけ

## 利用側（SymphonyKillChord）への影響の目安

R6 の互換層が入るまでは移行不可（R2 の版は配布しない）。7.0.0 で旧名は `[Obsolete]` 警告になる。使用数: `TryGetInstance` 241、`RegisterInstance` 80、`UnregisterInstance` 74、`GetInstance` 約60、`LocateTypeEnum.Locator` 11、`DestroyInstance` 2。`SymphonyConfigLocator`・`SymphonyLocateObject`・`RegisterAfterLocate`・`ServiceInjector`・`ServiceLocateComponent`・`IInjectable` は 0。質問2a で挙動が変わるのは `DestroyInstance(instance)` の2か所だけで、R6 の移行ガイドに書く。

## ファイル構成

- `Runtime/Service/ServiceLocator/` → `Runtime/Services/`（`Internal/` の各層も同じ名前空間 `SymphonyFramework.Services.Internal.*` へ）
- `ServiceLocator.cs` + `ServiceInjector.cs` → `Partitura.cs`（partial で `Partitura.Registration.cs` / `Partitura.Injection.cs` に分けてもよい。1ファイルが400行を超える場合）
- `.meta` の GUID は `git mv` で保つ。新規ファイルの `.meta` は `generate_meta.py`
- テスト（14ファイル）・Samples（4）・Editor（デバッグウィンドウ、ログ設定）・Orchestrator の参照を置き換える
- `Documentation/Modules/ServiceLocator.md` → `Partitura.md`、生成HTML も

## 互換

R6 まで入れない（全体方針）。`ServiceLocateComponent` は MonoBehaviour なので、利用側シーンの参照は `.meta` GUID で保たれる。`[SerializeReference]` で保存される型はこの Round に無い。

## テスト

- 既存テストを新しい名前へ置き換える
- 追加: `Destroy(instance)` と `IsRegistered(instance)` が別インスタンスでは false を返し、登録を消さないこと（質問2a）
- 追加: `Get<T>()` が未登録で `ServiceNotRegisteredException`、破棄済み Unity Object でも同じ例外を出すこと
- `PublicTypeTestCoverageTests` の公開型一覧を更新する

## 動作確認

Unity 検証は R6（決定事項3）。リモートでは R1 と同じ代替検査に加え、旧名（`ServiceLocator`、`GetInstance`、`LocateTypeEnum` など）がパッケージ内に残っていないことを grep で確かめる。
