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
| **R5** | `PauseManager` → `Fermata`。6.12.0 で非推奨にした旧ポーズAPIと、`Deprecations.md` で「次のメジャー更新」としていた旧APIの削除。自動生成の `SymphonyFramework.PauseCategory` を `SymphonyFramework.Enum` へ統合（依頼者の提案、2026-11-18） | #225、Deprecations.md | 入れない（R6） |
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

実施日: 2026-11-18 / バージョン: 6.16.0 / PR: HIBIKI5201/SymphonyFramework#234（develop へマージ済み）

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

> **状態: 実装済み（PR HIBIKI5201/SymphonyFramework#235）。Unity での検証は R6。** 版は 6.17.0。

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
| `SymphonyLocateObject<T>`（`GetInstance` / `GetInstanceAsync` …） | `PartituraReference<T>`（`Get` / `GetAsync` / `TryGet`） | 質問4。`Get` は `Partitura.Get` と同じく必須取得 |
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
| フィールドに持って何度も取る | `PartituraReference<T>` |
| 依存を注入する | `IInjectable` の自動注入、`Inject`、`CreateInstance` |

## 依頼者に決めてもらうこと

1. **取得APIの数**: ⭐a. `Get`＝必須（無ければ例外）、`TryGet`＝任意、`GetAsync`/`TryGetAsync`＝待つ。null を返す取得は無くす（6.x の `GetInstance` は R6 の互換層で従来どおり null を返す） / b. `Get`＝null を返す、`GetRequired`＝必須 として両方残す
2. **インスタンスを渡すAPI**: ⭐a. `Unregister`・`Destroy`・`IsRegistered` の `(T instance)` をすべて同一参照の判定にそろえる / b. `Unregister(T)` 以外の `(T instance)` 版を削除する
3. **`RegisterAfterLocate`**: ⭐a. `WhenRegistered<T>(Action<T>)` だけ残す（`Action` 版は `_ => …` で書ける） / b. 両方残す
4. **`SymphonyLocateObject<T>`**: ⭐a. `ServiceReference<T>` として残す / b. 削除して `TryGet` に寄せる → **回答: a、ただし名前は `PartituraReference<T>`**（どの Facade の参照かが名前で分かるため）
5. **`LocateTypeEnum` の値**: ⭐a. `Persistent`（Partitura の GameObject の子にして永続化）/ `Scene`（親子関係を変えない） / b. 値の名前は `Singleton` / `Locator` のまま
6. **`SymphonyConfigLocator`**: ⭐a. `internal` にして `SymphonyConfigProvider` へ改名（SymphonyKillChord では未使用。Editor とテストは `InternalsVisibleTo` 済み） / b. public のまま改名だけ

## 利用側（SymphonyKillChord）への影響の目安

R6 の互換層が入るまでは移行不可（R2 の版は配布しない）。7.0.0 で旧名は `[Obsolete]` 警告になる。使用数: `TryGetInstance` 241、`RegisterInstance` 80、`UnregisterInstance` 74、`GetInstance` 約60、`LocateTypeEnum.Locator` 11、`DestroyInstance` 2。`SymphonyConfigLocator`・`SymphonyLocateObject`・`RegisterAfterLocate`・`ServiceInjector`・`ServiceLocateComponent`・`IInjectable` は 0。質問2a で挙動が変わるのは `DestroyInstance(instance)` の2か所だけで、R6 の移行ガイドに書く。

## ファイル構成

- `Runtime/Service/ServiceLocator/` → `Runtime/Services/`（`Internal/` の各層も同じ名前空間 `SymphonyFramework.Services` へ。6.x でも Internal は親と同じ名前空間だった）
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

## 決定事項（2026-11-18、依頼者の回答）

1〜3、5、6 は ⭐a。4 は a で、名前を `PartituraReference<T>` にする。

## 実施レポート（Round 2）

実施日: 2026-11-18 / バージョン: 6.17.0 / PR: HIBIKI5201/SymphonyFramework#235

### 実装した内容

submodule 側は2コミット。

1. `e8e634b` コード: フォルダ移動と改名（`.meta` は `git mv` で GUID を保った）、識別子の置換、API の変更、テスト
   - `Partitura.cs`（旧 `ServiceLocator.cs`）と `Partitura.Injection.cs`（旧 `ServiceInjector.cs`）を `public static partial class Partitura` に
   - 同一参照の判定を `IsSameRegisteredInstance` へまとめ、`Unregister(instance)`・`Destroy(instance)`・`IsRegistered(instance)` が共有する
   - `Get<T>()` は旧 `GetRequiredInstance` の本体に、旧 `GetInstance` の取得ログを移した
   - 内部型 `ServiceLocate*`（Dto、Query、Registry、Service、ViewModel、LogOption）と Editor の `ServiceLocateWindow` を `Partitura*` へ
   - サンプルを `Samples~/Runtime/PartituraSample/` へ（`package.json` の samples も）
   - 新規 `Tests/Editor/PartituraTests.cs`（9ケース: Get の必須取得、破棄済み Unity Object、Destroy/IsRegistered の同一参照、WhenRegistered の即時・後から、Persistent の親）。`RuntimeFolderLayoutTests` に置き場の検査を追加
2. `890dca5` 文書: 6.17.0、CHANGELOG（名前の対応表）、`Modules/Partitura.md`（旧 `ServiceLocator.md`）へ「どの入口を使うか」の表、各文書のリンク、生成HTML

ワークスペース側: `Assets/Scripts/DebugScripts/` の2ファイル、`AGENTS.md`、`Documentation/` の規約文書（CodeGuidelines、DesignPhilosophy、ModuleInternals、IssueImplementationRoadmap）、`scripts/build_module_docs.py` のモジュール順、`.github/duplicate-type-allowlist.txt` に `Partitura` を追加。

### 設計から変えた点

- `SymphonyLocateObject<T>` の新しい名前を `ServiceReference<T>` から `PartituraReference<T>` に変えた（依頼者の回答4）
- 設計に無かった改名を足した: `ServiceRegistrationInfo.LocateType` → `Lifetime`、`SymphonyEditorConfigLocator` → `SymphonyEditorConfigProvider`（public のまま）、`SymphonyMcpTools.GetServiceLocatorJson` → `GetPartituraJson`（JSON の `effectiveLocateType` → `effectiveLifetime`）、`SymphonyDocumentPageEnum.ServiceLocator` → `Partitura`、ログ設定のプロパティ名。どれも旧 Facade 名から派生した名前で、#225 の規則（派生した型名は新しい名前へ揃える）に当たるため
- `SymphonyUserSettingConfig` のシリアライズ名（`_isServiceLocator*LogEnabled`）は変えなかった。`UserSettings` に保存済みの値を読むため
- 内部の `PartituraRegistry` / `PartituraService` の、引数なし `Action` の待機は残した。公開の入口は無くなったが、Registry のテストが呼び出し順を検証しているため。削除するかは R6 で判断する

### 検証結果（リモートの代替検査）

- `release_round.py preflight`: R1 と同じ既知の NG 2件（ブランチ名、Enter Play Mode Options）以外は OK（テスト29件の変更、BOM、`.meta`、レイヤー、asmdef、文書の同期）
- `generate_meta.py --check`、`build_module_docs.py --check`、`sync_agent_skill_locators.py --check`: OK
- 同名型の検査: `Partitura`（partial）を許可リストへ追加して OK
- 旧名の残り: パッケージの `.cs` に `ServiceLocator`、`ServiceInjector`、`LocateTypeEnum`、`GetInstance` などの公開名が残っていないことを grep で確認（CHANGELOG の過去の版、`.unity` の `m_EditorClassIdentifier`、互換のためのシリアライズ名は除く）

### 未実施リスト（R6 の検証へ追加）

- コンパイル（エラー0・警告0）と全テスト。特に `PartituraTests`、partial に分けた `Partitura` の静的初期化、`Unregister<T>(T)` と `Unregister(Type)` などのオーバーロード解決
- Symphony Administrator の Partitura パネル（UXML の `PartituraWindow` 要素）と Project Settings のログ設定の表示
- `ServiceComponent` を置いた既存シーン（ワークスペースの `NewScene` / `Scene2`）で、登録方式の値が引き継がれること
- サンプル `PartituraSample` を取り込んで動くこと

### 振り返り

- R1 の一括置換スクリプトを、旧メソッド名の対応表つきで作り直した。`ServiceLocator.X` の形だけを置換し、null を返す `GetInstance` は印（`GETNULL_REVIEW`）へ置き換えて呼び出し箇所を1件ずつ判断した。**意味が変わる改名は、機械置換で別の名前へ寄せず、印を残して手で決める**形が効いた
- 置換の対象に `.unity` を含めてしまい、戻した。R1 と同じ判断（表示用の値は Unity に任せる）を毎回思い出す必要があるので、置換対象の拡張子をスクリプト側で固定するとよい
- R6 で入れる転送クラスと衝突するテスト（旧名前空間に型が無いことを確かめるもの）を一度足して、取り下げた。R6 の互換層の設計で、旧名が「解決できる」ことを確かめるテストへ切り替える

---

# Round 3: `SceneLoader` → `Attacca`、`SceneBlockLoader` → `Suite`、シーン管理の入口の整理（#225、#232）

> **状態: 実装済み（回答は下の「決定事項」）。** 版は 6.18.0。

## 目的

- `SceneLoader` を `Attacca`、`SceneBlockLoader` を `Suite` へ改名し、どちらも名前空間 `SymphonyFramework.Scenes` へ置く（フォルダは `Runtime/Scenes/Attacca/` と `Runtime/Scenes/Suite/`）（#225）
- ロードの入口を1系統の形にそろえ、ブロックが持つシーンを `Attacca` から落とせてしまう所有権の衝突をなくす（#232）

## 公開API（6.17.0 → 6.18.0）

### Attacca

| 6.17.0 | 6.18.0 | 備考 |
| --- | --- | --- |
| `SceneLoader` | `Attacca` | |
| `GetExistScene` / `IsExist` | `TryGetScene` / `IsLoaded` | |
| `LoadSceneAsync(string, Action<float>, mode, priority, token)` | `LoadAsync(string, IProgress<float>, mode, priority, token)` | 質問1 |
| `LoadSceneAsync(SceneLoadRequest, IProgress<float>, mode, token)` | `LoadAsync(SceneLoadRequest, IProgress<float>, mode, token)` | |
| `LoadScenesAsync(string[], Action<float>, token)` | `LoadAsync(string[], IProgress<float>, token)` | 質問1 |
| `LoadScenesAsync(SceneLoadRequest[], IProgress<float>, token)` | `LoadAsync(SceneLoadRequest[], IProgress<float>, token)` | |
| `UnloadSceneAsync` / `UnloadScenesAsync`（`Action<float>`） | `UnloadAsync`（オーバーロード、`IProgress<float>`） | 質問1、質問2 |
| `RegisterAfterSceneLoad` | 削除 | 質問3 |
| `WaitForLoadSceneAsync` | `WaitForLoadAsync` | |
| `TryGetState` / `GetSceneInfos` / `TryGetSceneInfo` / `SetActiveScene` / `RegisterLoadedScene` | そのまま | |
| `SceneLoadInfo` / `SceneLoadRequest` / `SceneLoadStateEnum` / `SceneInitializationException` / `IInitializeAsync` | 名前はそのまま、名前空間だけ | 質問4 |

複数シーン版に `mode` が無いのは今のままにする。`Single` で複数を読むと、先に読んだシーンを後のシーンが落とすため。

### Suite

| 6.17.0 | 6.18.0 |
| --- | --- |
| `SceneBlockLoader` | `Suite` |
| `SceneBlockAsset` / `SceneBlockEntry` / `SceneBlockInfo` | `SuiteAsset` / `SuiteEntry` / `SuiteInfo` |
| `SceneBlockLoadStateEnum` / `SceneBlockPlanException` | `SuiteLoadStateEnum` / `SuitePlanException` |
| `TryGetBlockInfo` / `GetBlockInfos` | `TryGetInfo` / `GetInfos` |
| `SceneBlockAsset.BlockName`、引数名 `blockName` | `SuiteAsset.SuiteName`、`suiteName` |
| `LoadAsync` / `UnloadAsync` / `IsLoaded` | そのまま |

`SuiteAsset` のシリアライズ名（`_blockName`、`_entries` など）は `[FormerlySerializedAs]` を付けて改名するか、そのまま残す。利用側のアセットの値を失わないため。クラスの改名は `.meta` の GUID で引き継がれる。

### 所有権の衝突（質問2 が a の場合）

`Suite` が保持しているシーンを `Attacca.UnloadAsync` に渡すと `InvalidOperationException` を投げ、何もアンロードしない。`Suite` は `Attacca` を使う側なので、`Attacca` から `Suite` を参照すると依存が逆向きになる。**`Attacca` の内部に「保持中か」を問い合わせる判定（`Func<string, bool>`）の差し込み口を置き、Orchestrator が組み立て時に `Suite` の判定を渡す。** `Suite` 自身のアンロードはこの判定を通らない内部経路を使う。

### 関連して変わる名前

`SymphonyMcpTools.GetSceneLoaderJson` などの MCP ツール、Symphony Administrator のパネル名、`SymphonyDocumentPageEnum`、モジュール文書（`SceneLoader.md` → `Attacca.md`、`SceneBlock.md` → `Suite.md`）、サンプル。R2 と同じく、旧 Facade 名から派生した名前を新しい名前へそろえる。

## 依頼者に決めてもらうこと

1. **進捗の型**: ⭐a. `IProgress<float>` にそろえる（`Suite` と Request 版がすでにこれ。.NET 標準） / b. `Action<float>` にそろえる
2. **`Suite` が持っているシーンを `Attacca.UnloadAsync` で落とそうとしたとき**: ⭐a. `InvalidOperationException` で拒否する / b. 警告を出して false を返す / c. アンロードして `Suite` 側の状態を更新する
3. **ロード完了を待つ方法**: ⭐a. `WaitForLoadAsync`（await）だけ残し、`RegisterAfterSceneLoad` は削除する / b. `WhenLoaded(Action)` としても残す（Partitura の `WhenRegistered` と同じ形）
4. **`IInitializeAsync`**: ⭐a. `Task` のまま。複数の呼び出し元が同じ初期化を待つため（`Awaitable` は1回しか await できない） / b. `Awaitable` にする
5. **起動時のシーン一覧で `Suite` を指定できるようにするか**: ⭐a. 7.0.0 ではやらず、別Issueにする（機能追加で、改名とは別に検証したい） / b. R3 で追加する

## 利用側（SymphonyKillChord）への影響の目安

使用数: `GetExistScene` 11、`RegisterLoadedScene` 7、`LoadSceneAsync` 6、`UnloadSceneAsync` 5、`SetActiveScene` 2。`SceneBlockLoader` と `IInitializeAsync` は 0。**`LoadSceneAsync` と `UnloadSceneAsync` は進捗を `Action<float>` で渡しているため、質問1 が a なら書き換えが要る。** R6 の互換層の旧 `SceneLoader` は `Action<float>` を受けて変換するので、7.0.0 へ上げた時点では警告だけで動く。

## テスト

- 既存テストを新しい名前へ置き換える
- 追加: `Suite` が保持中のシーンを `Attacca.UnloadAsync` に渡すと拒否され、`Suite` の状態が `Loaded` のまま残ること（Service 層を fake の判定で検証する）
- 追加: `Suite.UnloadAsync` は自分の保持を解いたシーンをアンロードできること
- `SuiteAsset` の改名後もシリアライズ名で値を読めること（`[FormerlySerializedAs]` を付けた場合、`CustomAttributeData` で値を確かめる）

## 決定事項（2026-11-18、依頼者の回答）

1. a: 進捗は `IProgress<float>` にそろえる
2. **b**: Suite が保持中のシーンを `Attacca.UnloadAsync` に渡すと、警告をログへ出して `false` を返す（例外にしない）
3. **b**: `RegisterAfterSceneLoad` は削除せず `WhenLoaded(string, Action)` として残す
4. a: `IInitializeAsync` は `Task` のまま
5. **b**: 起動時のシーン一覧で Suite を指定できるようにする（R3 で追加）

## 実施レポート（Round 3）

実施日: 2026-11-18 / バージョン: 6.18.0 / PR: HIBIKI5201/SymphonyFramework#236

### 実装した内容

submodule 側は2コミット（#235 のマージ後、develop を取り込んで #236 へ）。

1. `46a79b0` コード
   - `Runtime/Service/SceneLoader/` → `Runtime/Scenes/Attacca/`、`Runtime/Service/SceneBlock/` → `Runtime/Scenes/Suite/`（`.meta` は `git mv`）。名前空間は `SymphonyFramework.Scenes`
   - 公開型と Editor の改名（`AttaccaWindow`、`SuiteWindow`、`SuiteAssetDrawer`、各 `.uxml`）。内部の `SceneLoad*` と `ISceneLoader`、`UnitySceneLoader` は名前を変えていない（Facade 名から派生した名前ではないため）
   - `Attacca` の Load/Unload を単体・配列の overload へまとめ、進捗を `IProgress<float>` に統一。旧 `Action<float>` の変換（`WrapProgress`）は R6 の互換層で使うため internal で残した
   - 保持中シーンの保護: `Attacca.SetSuiteHoldQuery(Func<string, bool>)` を Orchestrator が `Suite.IsSceneHeld` で呼ぶ。複数指定は全件を先に確かめ、1件でも保持中なら何もしない
   - 起動時の Suite: `SceneLoadConfig._initializeSuiteList`（Inspector にリスト）を追加。`SceneLoadService.InitializeAfterSceneLoad` に「残すシーン名」と「追加ロード」を渡せるようにし、順序は「初期シーン → Suite → 残りの整理」。どちらかが失敗したら整理しない。Suite 側は `Suite.LoadInitialAsync`（例外はログへ出して `false`）と `Suite.CollectSceneNames`
   - `SuiteAsset._suiteName` に `[FormerlySerializedAs("_blockName")]`。サンプルのアセットは旧名のまま残し、移行の経路を通す
   - テスト: 新規 `AttaccaTests`（保持中シーンの単体・複数アンロードが `false`、リセットで判定が外れる）。`SceneLoadServiceTests` に起動時 Suite の4ケース（順序、Suite だけ、Suite の失敗で整理しない、リセット無効）。`SuiteTests` に `CollectSceneNames` と `LoadInitialAsync` の3ケース。`SuiteAssetTests` に `FormerlySerializedAs` の検査。`RuntimeFolderLayoutTests` に置き場の検査
2. `ad23c09` 文書: 6.18.0、CHANGELOG（名前の対応表）、`Modules/Attacca.md`（旧 `SceneLoader.md`、起動時処理の節を追加）、`Modules/Suite.md`（旧 `SceneBlock.md`）、各文書のリンク、生成HTML、`package.json` のサンプル名

ワークスペース側: `Assets/Scripts/DebugScripts/` の2ファイル、`Assets/Editor/Scripts/SymphonySampleImporter.cs` のコメント、`Documentation/` の規約文書（CodeGuidelines、DesignPhilosophy、DocumentationGuidelines、ModuleInternals）、`scripts/build_module_docs.py` のモジュール順。

### 設計から変えた点

- 回答に合わせ、質問2 は例外ではなく警告と `false`、質問3 は `WhenLoaded` を残し、質問5 は R3 で実装した
- R2 の置換で残っていた誤り（ModuleInternals の `Partitura ..> Partitura`、重複した `class Partitura`、旧メソッド名、CodeGuidelines の「`Partitura`と`Partitura`」）を直した
- CodeGuidelines の「`Runtime/Service/` はフォルダと名前空間が一致しない」の節を、7.0.0 で順に揃えている現状へ書き換えた

### 検証結果（リモートの代替検査）

- `release_round.py preflight`: 既知の NG 2件（ブランチ名、Enter Play Mode Options）以外は OK
- `generate_meta.py --check`、`build_module_docs.py --check`、`sync_agent_skill_locators.py --check`: OK
- 同名型の検査: 追加なし。公開型のテスト網羅の検査を手元で再現し、未テストの公開型が増えていないこと（`Attacca` は一覧から外した）を確認
- 旧名の残り: パッケージの `.cs` / `.uxml` に `SceneLoader`、`SceneBlock*`、`LoadSceneAsync` などの公開名が残っていないことを grep で確認（内部の `ISceneLoader` のメソッド名は除く）

### 未実施リスト（R6 の検証へ追加）

- コンパイルと全テスト。特に `AttaccaTests`（EditMode で `Attacca.Initialize` を呼ぶ）、`SuiteTests.LoadInitialAsync_EmptySuite_LogsAndReturnsFalse`（`LogAssert.Expect` の一致）、Orchestrator の `Func<Task<bool>>` の組み立て
- Play Mode: `SceneLoadConfig` に Suite を入れて起動し、初期シーン → Suite → 整理の順になること。Suite のシーンが整理で残ること
- Play Mode: Suite が保持中のシーンを `Attacca.UnloadAsync` に渡して警告と `false` になり、Suite の状態が変わらないこと
- 既存の `SceneBlockAsset` アセット（サンプル）が `SuiteAsset` として開け、`Suite Name` に旧 `Block Name` の値が入っていること
- Symphony Administrator の Attacca / Suite パネル、`SceneLoadConfig` の Inspector に Suite のリストが出ること
- サンプル `AttaccaSample` / `SuiteSample` を取り込んで動くこと

### 振り返り

- 改名スクリプトがファイル名の部分一致で `ISceneLoader.cs` / `UnitySceneLoader.cs` まで `IAttacca` / `UnityAttacca` へ変えた。**型名の改名は単語境界つきの完全一致に限る**べきだった。R4 の `SaveStore` / `AudioManager` でも同じ形（`*SaveStore*`）が起きうるので、スクリプト側で境界を固定する
- `using` の重複（同じ名前空間を2回、自分の名前空間を `using`）が置換後に8ファイルで出た。名前空間を統合する Round では、置換後に重複 `using` を機械的に検出する手順を足す
- R2 の文書置換の誤りを R3 で見つけた。置換後に「同じ名前が2回並ぶ」パターン（`X と X`、`X ..> X`）を grep する検査を、R4 から置換手順に加える

---

# Round 5 の追加: `PauseCategory` を `SymphonyFramework.Enum` へ統合する（依頼者の提案、2026-11-18）

R5 の設計時に詳しく書く。今の見通しだけを残す。

- 自動生成の asmdef `SymphonyFramework.PauseCategory` をやめ、Pause のカテゴリー用インターフェースも `SymphonyFramework.Enum` へ生成する
- 今は `SymphonyFramework.asmdef` が Enum asmdef を参照しているが、Runtime / Core に生成 enum の利用は無い（grep で確認済み）。この参照を外し、`SymphonyFramework.Enum` から `SymphonyFramework` を参照する向きにする（PauseCategory のインターフェースが Runtime の型を使うため）
- 利用側の既存の `PauseCategory` フォルダと asmdef は、Editor の起動時に Enum 側へ移す（R1 の生成物フォルダ移行と同じ仕組み）。**利用側の asmdef 参照が変わるため破壊的変更**


---

# Round 4: `SaveStore` → `Manuscript`、`AudioManager` → `Sonority`（#225）

> **状態: 実装済み（回答は下の「決定事項」）。** 版は 6.19.0。

## 目的

- `SaveStore` を `Manuscript`（名前空間 `SymphonyFramework.SaveData`、フォルダ `Runtime/SaveData/`）、`AudioManager` を `Sonority`（名前空間 `SymphonyFramework.Audio`、フォルダ `Runtime/Audio/`）へ改名する（#225 の表どおり）
- `SymphonyFramework.System` から2つのサブシステムを外す。R5 で Pause を移すと `System` 名前空間は無くなる

## 公開API（6.18.0 → 6.19.0）

### Manuscript

| 6.18.0 | 6.19.0 | 備考 |
| --- | --- | --- |
| `SaveStore` | `Manuscript` | |
| `Exists` / `Get` / `IsLoaded` / `LoadAsync` / `SaveAsync` / `DeleteAsync` | そのまま | `Get` は未ロードで例外（今のまま。R2 の「`Get` は必須取得」と同じ意味） |
| `GetEntries` | そのまま | 質問2 |
| `SaveDataContent` / `SaveDataLoaderStrategy` / `PlayerPrefsSaveDataLoaderStrategy` / `SaveDataEntryInfo` / `SaveDataOperationEnum` / `SaveDataOperationException` | 名前はそのまま、名前空間だけ | 「セーブデータ」は概念の名前で、旧 Facade 名から派生した名前ではないため |

### Sonority

| 6.18.0 | 6.19.0 | 備考 |
| --- | --- | --- |
| `AudioManager` | `Sonority` | |
| `VolumeSliderChanged(string, float)` | `SetVolume(string, float)` | 値は今と同じ 0〜1 の割合（内部で dB へ変換） |
| `GetAudioSource(string)`（未登録は `null`） | 質問1 | |

## 依頼者に決めてもらうこと

1. **`GetAudioSource` の新しい形**: ⭐a. R2 の Partitura にそろえ、`GetSource`（未登録は例外）と `TryGetSource(string, out AudioSource)` の2つにする / b. `GetSource`（未登録は `null`）の1つだけにする（#225 の表どおり）
2. **未初期化のときの `GetEntries`**: ⭐a. 今のまま空の一覧を返す（Editor のパネルやサンプルが Play 前に呼んでも落ちない） / b. 他の API と同じく `SymphonyNotInitializedException` を投げる
3. **Editor の表示名**: ⭐a. Symphony Administrator のパネル名、Project Settings の項目名（今は `Save System`）、モジュール文書、サンプル名を `Manuscript` / `Sonority` にそろえる。型名の `SaveDataWindow` などは概念の名前なので変えない / b. 表示名も今のまま（`Save Data`、`Audio Manager`）にする

## 互換とシリアライズ

- `SaveDataConfig` は `[SerializeReference]` でローダーを保存している。`JsonUtilitySaveDataLoaderStrategy` などの `[MovedFrom]` は **6.15 の名前（`SymphonyFrameWork.System.SaveSystem`、アセンブリ `SymphonyFrameWork`）を指したまま**にする。6.16〜6.18 は単独で配布しないため、利用側に残るのは 6.15 の名前だけ。`[MovedFrom]` は移動元だけを書く属性なので、移動先の名前空間を変えても書き換えは要らない
- 保存キー（`PlayerPrefs` のキー、ファイル名）は利用側のデータ型の `FullName` で、フレームワークの名前空間には依存しない。既存のセーブデータはそのまま読める
- `AudioConfig` / `SaveDataConfig` は internal で、型名は変えない（`.meta` で引き継ぐ）
- 旧名の転送（`SaveStore`、`AudioManager`、旧名前空間の `SaveDataContent` / `SaveDataLoaderStrategy` / `PlayerPrefsSaveDataLoaderStrategy`）は R6

## 関連して変わる名前

`SymphonyMcpTools.GetSaveDataJson` → `GetManuscriptJson`、`SymphonyDocumentPageEnum.SaveDataSystem` / `.AudioManager` → `.Manuscript` / `.Sonority`、モジュール文書（`SaveDataSystem.md` → `Manuscript.md`、`AudioManager.md` → `Sonority.md`）、サンプル（`SaveDataSystemSample` → `ManuscriptSample`、`AudioManagerSample` → `SonoritySample`）、`build_module_docs.py` のモジュール順。

## 利用側（SymphonyKillChord）への影響の目安

`SaveStore.` が 124 か所（`IsLoaded` 35、`Get<` 35、`LoadAsync` 31、`SaveAsync` 18）、`using SymphonyFramework.System.SaveSystem` が 30 か所、`SaveDataLoaderStrategy` を継承した独自ローダーが1つ（`EncryptedFileSaveDataLoaderStrategy`）。`AudioManager` は 0。**メソッド名は変わらないため、7.0.0 では R6 の転送で警告だけで動き、`SaveStore` → `Manuscript` と `using` の書き換えで警告が消える。** 独自ローダーは旧名前空間の `[Obsolete]` 型経由で継承が通る。

## テスト

- 既存テストを新しい名前へ置き換える（`SaveStore*Tests` → `Manuscript*Tests` など。公開型のテスト網羅の一覧も更新）
- 追加: `Sonority.SetVolume` / `GetSource`（質問1 が a なら `TryGetSource` も）の未初期化・引数検証・未登録の扱い
- `RuntimeFolderLayoutTests`: `Runtime/SaveData/` と `Runtime/Audio/` にあり、`Runtime/Service/` の旧置き場に残っていないこと
- `JsonUtilitySaveDataLoaderStrategy` / `NewtonsoftSaveDataLoaderStrategy` の `[MovedFrom]` が 6.15 の名前を指していること（`CustomAttributeData` で確かめる）

## 進め方

R3 と同じ。改名スクリプトは R3 の振り返りどおり、型名を単語境界つきの完全一致に限り、置換後に重複 `using` と「同じ名前が2回並ぶ」文を grep する。

## 決定事項（2026-11-21、依頼者の回答）

1. a: `GetSource`（未登録は例外）と `TryGetSource` の2つにする
2. **b**: 未初期化の `GetEntries` は他の API と同じく `SymphonyNotInitializedException` を投げる。**Play 前のパネルやサンプルは try-catch を加え、未初期化用の動きにする**
3. a: パネル名、Project Settings の項目名、文書、サンプル名を `Manuscript` / `Sonority` にそろえる

## 実施レポート（Round 4）

実施日: 2026-11-21 / バージョン: 6.19.0 / PR: （R3 の #236 がマージされた後に作る）

### 実装した内容

submodule 側は2コミット（R3 のブランチの上に積んでいる）。

1. `9db6c17` コード
   - `Runtime/Service/SaveSystem/` → `Runtime/SaveData/`（`SymphonyFramework.SaveData`）、`Runtime/Service/Audio/` → `Runtime/Audio/`（`SymphonyFramework.Audio`）。`.meta` は `git mv`
   - `SaveStore` → `Manuscript`、`AudioManager` → `Sonority`。`VolumeSliderChanged` → `SetVolume`。`GetAudioSource` は `GetSource`（空の名前・未登録は `ArgumentException`）と `TryGetSource`（空の名前・未登録は `false`）に分けた。Service 側の `GetAudioSource` は内部名のまま
   - `Manuscript.GetEntries` は `EnsureInitialized` を通す。呼び出し側は、MCP ツールが元から `IsInitialized` と try-catch で守っていた。Manuscript Sample の `IsCacheLoaded` に `SymphonyNotInitializedException` の catch を加え、未初期化ではキャッシュ無しとして表示する。Administrator のパネルは `GetEntries` を使っていない（ViewModel 経由）
   - Editor の表示名: パネルのタイトル `Save Data Registry` → `Manuscript`、Project Settings の `Save System` → `Manuscript`。型名（`SaveDataWindow`、`SaveDataSettingProvider`）は概念の名前なので変えていない
   - `SymphonyMcpTools.GetManuscriptJson`、`SymphonyDocumentPageEnum.Manuscript` / `.Sonority`、サンプル（`ManuscriptSample`、`SonoritySample`）
   - `JsonUtilitySaveDataLoaderStrategy` / `NewtonsoftSaveDataLoaderStrategy` の `[MovedFrom]` は 6.15 の名前のまま（既存の `RootFolderCaseMigrationTests` が値を検査している）
   - テスト: 新規 `SonorityTests`（未初期化3件、未登録・空の名前、範囲外の音量、未登録グループの音量変更）。`RuntimeFolderLayoutTests` に置き場の検査
2. `c808e50` 文書: 6.19.0、CHANGELOG（対応表）、`Modules/Manuscript.md`（旧 `SaveDataSystem.md`）、`Modules/Sonority.md`（旧 `AudioManager.md`）、各文書のリンク、生成HTML、`package.json` のサンプル名

ワークスペース側: `Assets/Scripts/DebugScripts/DbugObj.cs`、`Documentation/` の規約文書（CodeGuidelines、DesignPhilosophy、ModuleInternals）、`scripts/build_module_docs.py` のモジュール順。

### 設計から変えた点

- `Manuscript.GetEntries` を例外にした（回答2）。設計の推奨（空の一覧）とは逆
- R3 で直し漏れていた、パッケージの `AGENTS.md` の `SceneLoader.md` へのリンク（#236 の時点でリンク切れ）を R4 で直した。`build_module_docs.py --check` は `AGENTS.md` を検査対象にしていないため、R3 では見つからなかった
- CHANGELOG の過去の版にあった `SaveDataSystem.md` へのリンクを `Manuscript.md` へ書き換えた（リンク検査が過去の版も見ているため）

### 検証結果（リモートの代替検査）

- `release_round.py preflight`: 既知の NG 2件以外は OK
- `generate_meta.py --check`、`build_module_docs.py --check`: OK
- 同名型の検査、公開型のテスト網羅の再現（`Sonority` を一覧から外した。`Manuscript` は残す。Editor の起動時に初期化されるため、EditMode で未初期化の状態を作れない）: OK
- 重複 `using`、自分の名前空間の `using`、「同じ名前が2回並ぶ」文の grep: 該当なし
- 旧名の残り: パッケージの `.cs` / `.uxml` / `.json` に `SaveStore`、`AudioManager`、`VolumeSliderChanged`、`SymphonyFramework.System.SaveSystem` が残っていない（`[MovedFrom]` の移動元を除く）

### 未実施リスト（R6 の検証へ追加）

- コンパイルと全テスト。特に `SonorityTests`（`Sonority.Initialize(null, factory)` で警告ログが出るだけで落ちないこと）、`ManuscriptAwaitableRuntimeTests`
- `SymphonyFramework.Audio` / `SymphonyFramework.SaveData` の名前空間が、`SymphonyFramework` 配下のコードの単純名（`Audio`、`SaveData`）を隠していないこと（grep では該当なし）
- Project Settings の `Manuscript` 項目と、既存の `SaveDataConfig` のローダーが読めること。Administrator の Manuscript パネル
- サンプル `ManuscriptSample`（Play 前・直後の表示）と `SonoritySample`（空のグループ名を入れても例外にならない）

### 振り返り

- R3 の振り返りどおり、置換スクリプトを `\b` 付きにし、サンプル名だけは `_` で続く識別子（`ManuscriptSample_Controller`）があるため境界なしにした。置換後の重複 `using` の検査も手順に入れ、該当なしを確かめられた
- `Audio` 系のテストが `using SymphonyFramework.System;` で内部型を参照していた。名前空間を分ける Round では、移す型を使うテストの `using` を名前空間の単位で洗い出す
- `System.Collections.Generic.List` のように完全修飾を書くと、`SymphonyFramework.System` に隠されてコンパイルできない。R5 で `System` 名前空間が無くなるまでは、テストでも `using` を使う


---

# Round 5: `PauseManager` → `Fermata`、旧APIの削除、`PauseCategory` の統合（#225、Deprecations.md）

> **状態: 設計中（依頼者の回答待ち）。** 版は 6.20.0。上の「Round 5 の追加」はこの節で置き換える。

## 目的

- `PauseManager` を `Fermata`（名前空間 `SymphonyFramework.Pause`、フォルダ `Runtime/Pause/`）へ改名する（#225 の表どおり）
- これで `SymphonyFramework.System` 名前空間が空になり、無くなる。`System.Exception` などが隠される問題（R4 の振り返り）も解消する
- `Deprecations.md` の「次のメジャー更新」の4行（Asset Store Tools 関連）を消す。「未定」の14行は質問1・2で決める
- 自動生成の `SymphonyFramework.PauseCategory` asmdef をやめ、カテゴリーの interface を `SymphonyFramework.Enum` へ入れる（依頼者の提案）

## 公開API（6.19.0 → 6.20.0）

| 6.19.0 | 6.20.0 | 備考 |
| --- | --- | --- |
| `PauseManager` | `Fermata` | |
| `PauseManager.IPausable`（入れ子） | `IPausable`（トップレベル） | `Pause()` / `Resume()` はそのまま |
| `IPausable.RegisterPauseManager` / `UnregisterPauseManager`（interface の static） | 質問3 | 旧Facade名から派生した名前のため変える |
| `SetPause<T>` / `IsPaused<T>` / `SetPauseAll` / `IsPausedAny` / `GetPauseInfo[<T>]` / `Add・RemovePauseChangedHandler<T>` | そのまま | |
| `PausableNextFrameAsync<T>` | `NextFrameAsync<T>` | |
| `PausableWaitForSecond<T>` / `PausableWaitForSecondAsync<T>` | `WaitForSeconds<T>` / `WaitForSecondsAsync<T>` | 単数形の誤りも直す |
| `PausableWaitUntil<T>` | `WaitUntilAsync<T>` | `Awaitable` を返すため |
| `PausableDestroy<T>` / `PausableInvoke<T>` | `DestroyAsync<T>` / `InvokeAsync<T>` | |
| カテゴリー無しの旧API8件（`Pause`、`OnPauseChanged`、`Pausable*` の非ジェネリック版） | 質問1 | 6.12.0 から `[Obsolete]` |
| `PauseInfo` | そのまま（名前空間だけ） | |

内部型（`PauseService`、`PauseQuery`、`PauseViewModel`、`PausableRegistry` など）は `Internal/` ごと移し、型名は変えない。`PausableRegistry` は概念（ポーズ可能な対象の登録簿）の名前なので残す。

## 旧APIの削除（Deprecations.md）

- **「次のメジャー更新」の4行は R5 で消す。** `ASSET_STORE_TOOLS_IGNORE_FILE`（`ignore.txt` の移行処理ごと）、`PackageModeEnum`（`Combine` を含む）、`Export(string[], PackageModeEnum, bool, bool)`、`AssetStoreToolsPackageContext`。手順は Deprecations.md の「Combineの削除手順」の表どおり。`AssetStoreToolsCombinePackageStrategy` は残す
- 消した行は「削除済み」へ移し、CHANGELOG の `### Breaking` に移行方法を書く
- 「未定」のうち Logger 4件・Tween 2件は質問2、PauseManager 8件は質問1

## `PauseCategory` を `SymphonyFramework.Enum` へ統合する

- 生成先 `Assets/Scripts/SymphonyFramework/PauseCategory/` は Enum の asmdef と同じフォルダの下にある。**サブ asmdef を作らなければ、そのまま `SymphonyFramework.Enum` に含まれる。** `PauseCategoryGenerator` は asmdef を作る処理をやめ、interface だけを生成する（`using SymphonyFramework.Pause;`、`public interface X : IPausable`）
- 依存の向きを逆にする: 今はパッケージの `SymphonyFramework.asmdef` が Enum の asmdef を参照している（`PackageInitializer.EnumInitialize` / `EnumGenerator` が GUID を書き込む）。Runtime / Editor / Core に生成 enum の利用は無い。**この参照と書き込み処理を消し、Enum の asmdef から `SymphonyFramework` を参照する**（interface が `IPausable` を使うため）。逆向きを残すと循環になる
- Enum の asmdef は `autoReferenced: true` のまま。`Assembly-CSharp` からは今までどおり使える
- 既存プロジェクトの移行: 質問4

## 依頼者に決めてもらうこと

1. **カテゴリー無しの旧ポーズAPI8件**（6.12.0 から非推奨）: ⭐a. `Fermata` には入れず、R6 で残す `[Obsolete]` の `PauseManager`（転送クラス）にだけ残す。旧コードは警告付きで動き、新しいコードには出てこない / b. 7.0.0 で完全に消す（転送クラスからも外す）
2. **Logger 4件・Tween 2件の「未定」の旧API**（いつ非推奨にしたか記録が無いもの）: ⭐a. 7.0.0 で消す（R5 で削除。SKC の利用は0件） / b. 残し、削除予定を「未定」のままにする
3. **`IPausable` の登録メソッド**: ⭐a. `Fermata.Register(IPausable)` / `Fermata.Unregister(IPausable)` にする（他の操作と同じく Facade に置く） / b. interface の static のまま `IPausable.Register` / `IPausable.Unregister` にする
4. **既存の `PauseCategory` asmdef の移行**: ⭐a. Editor の起動時に、生成物の `SymphonyFramework.PauseCategory.asmdef`（6.15 の `SymphonyFrameWork.PauseCategory` も）を消し、interface を生成し直す。自前の asmdef がそれを参照していた場合は、`SymphonyFramework.Enum` を参照するよう警告で案内する / b. 自動では消さず、警告だけ出して利用者に生成し直してもらう

## 互換とシリアライズ

- `IPausable` を実装した MonoBehaviour の GUID は利用側のもので、影響しない。`PauseManager.cs` → `Fermata.cs` は `git mv`
- `[SerializeReference]` でポーズ関連の型を保存している箇所は無い（grep で確認する）
- ポーズの `ResetRuntimeState()` は名前を変えずに移す
- 旧名の転送（`PauseManager`、入れ子の `PauseManager.IPausable` を新しい `IPausable` の派生として残す、`RegisterPauseManager`）は R6

## 利用側（SymphonyKillChord）への影響の目安

`PauseManager.IPausable` が `BarragePauseBridge.cs` の3か所（実装1、`RegisterPauseManager` / `UnregisterPauseManager` 各1）。旧ポーズAPI・Logger・Tween の旧APIは0件。`PauseCategory` は未使用で、SKC の asmdef は Enum を参照していない。**7.0.0 では R6 の転送で警告だけで動く。**

## テスト

- 既存の Pause 系テストを `Fermata` / `IPausable` へ置き換える。公開型の網羅一覧（`PauseManager` → `Fermata`、`IPausable`）
- `PauseCategoryGeneratorTests`: 生成ソースが `SymphonyFramework.Pause` と `IPausable` を使うこと、asmdef を作らないこと、旧 asmdef の移行（質問4 が a の場合）
- `PackageInitializer` / `EnumGenerator`: パッケージ asmdef へ Enum の参照を書き込まないこと、Enum の asmdef が `SymphonyFramework` を参照すること
- `RuntimeFolderLayoutTests`: `Runtime/Pause/` にあり、`Runtime/Service/` が無くなっていること
- 名前空間 `SymphonyFramework.System` がパッケージに残っていないこと（grep と、アセンブリの型の名前空間を列挙するテスト）
- 削除した Asset Store Tools の旧APIを参照するテストを消す

## 進め方

R4 と同じ。`System` 名前空間が無くなるため、R4 までテストで避けていた `System.` の完全修飾はそのまま使えるようになる（書き換えは不要）。置換後に `using SymphonyFramework.System;` が残っていないこと（今は20ファイル）を grep で確かめる。
