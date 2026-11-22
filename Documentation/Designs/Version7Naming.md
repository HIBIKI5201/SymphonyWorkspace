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

実施日: 2026-11-21 / バージョン: 6.19.0 / PR: HIBIKI5201/SymphonyFramework#237（#236 のマージ後の develop を取り込み済み）

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

> **状態: 実装済み（回答は下の「決定事項」）。** 版は 6.20.0。上の「Round 5 の追加」はこの節で置き換える。

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

## 決定事項（2026-11-21、依頼者の回答）

1. **カテゴリーを指定しない場合は全てを指定する。** カテゴリー無しの旧API8件は非推奨のまま転送へ回すのではなく、`Fermata` の正式なAPIとして残す。対象は全カテゴリーで、どれか1つでもポーズ中なら止まる（6.12.0 以降の非推奨版と同じ動き）。`Pause` プロパティは `SetPauseAll` / `IsPausedAny` と重なるため置かず、`OnPauseChanged` はカテゴリー無しの `AddPauseChangedHandler(Action<bool>)` / `RemovePauseChangedHandler(Action<bool>)` にする
2. a: Logger 4件・Tween 2件を R5 で削除する
3. a: `Fermata.Register(IPausable)` / `Fermata.Unregister(IPausable)`
4. a: Editor の起動時に旧 asmdef を削除して移行する

## 実施レポート（Round 5）

実施日: 2026-11-21 / バージョン: 6.20.0 / PR: HIBIKI5201/SymphonyFramework#238（#237 のマージ後の develop を取り込み済み）

### 実装した内容

submodule 側は2コミット（R4 の上に積んでいる）。

1. `5992174` コード
   - `Runtime/Service/Pause/` → `Runtime/Pause/`（`SymphonyFramework.Pause`）。`PauseManager.cs` → `Fermata.cs`（`.meta` は `git mv`）。`Runtime/Service/` は空になったため `.meta` ごと消した。`SymphonyFramework.System` 名前空間はパッケージから無くなった
   - `IPausable` をトップレベルの `Runtime/Pause/IPausable.cs` へ（`.meta` は Unity なしで生成）。interface の static だった登録は `Fermata.Register` / `Unregister`
   - 待機・遅延の改名（`NextFrameAsync` / `WaitForSeconds` / `WaitForSecondsAsync` / `WaitUntilAsync` / `DestroyAsync` / `InvokeAsync`）。カテゴリー無し版は `[Obsolete]` を外した。秒数の引数名を `time` / `t` から `seconds` にそろえた
   - 削除: `Pause` プロパティ、`OnPauseChanged`、Logger 4件と専用の `DescribeNullKind`、Tween 2件、`ASSET_STORE_TOOLS_IGNORE_FILE` と `ignore.txt` の移行処理、`PackageModeEnum` / 旧 `Export` / `CreateStepsFromOptions` / `LEGACY_PIPELINE_NAME`、`AssetStoreToolsPackageContext`
   - asmdef: パッケージの `SymphonyFramework.asmdef` から Enum の GUID を外した。`AssemblyGenerator.CreateEnumAssembly` は Enum → Framework の参照を入れる向きに変え、`RemoveAssemblyReference` を足した。`PackageInitializer.EnumInitialize` は起動時に「Framework → Enum の参照を外す」「Enum → Framework の参照を入れる」「旧 PauseCategory asmdef を移行する」を行う
   - `PauseCategoryGenerator`: 専用 asmdef を作らない。`MigrateLegacyAssembly` は旧 asmdef（`SymphonyFramework.PauseCategory` と 6.15 の `SymphonyFrameWork.PauseCategory`）があれば、フォルダ内の全 `.cs` を `RewriteLegacySource` で書き換え（設定から消えたカテゴリーも含む）、asmdef を削除して警告を出す
   - サンプル `PauseManagerSample` → `FermataSample`、パネル名 `Fermata`、`SymphonyDocumentPageEnum.Fermata`
   - テスト: `FermataTests`（カテゴリー無しの購読2件を追加、`Pause` プロパティのテストを置き換え）、`FermataAwaitableRuntimeTests`、`PauseCategoryGeneratorTests`（生成先、旧 asmdef 名、`RewriteLegacySource` 3件、Framework asmdef が Enum を参照しないこと）、`RuntimeFolderLayoutTests`（`Runtime/Service/` が無いこと、`Runtime/Pause/`）。削除した `CheckComponentNull` のテスト3件を消した
2. `34a5439` 文書: 6.20.0、CHANGELOG（対応表と削除一覧）、`Modules/Fermata.md`（旧 `PauseManager.md`）、`Deprecations.md`（削除予定を空にし、14行を「削除済み」へ）、各文書のリンク、生成HTML

ワークスペース側: `AGENTS.md`（アセンブリ構成図）、CodeGuidelines（名前空間の木から `System` を消し `Pause` を足す、`Runtime/<Subsystem>/`）、DesignPhilosophy（`PackageModeEnum` の例）、ModuleInternals（`## Fermata`）、`build_module_docs.py` のモジュール順。

### 設計から変えた点

- 質問1 の回答により、カテゴリー無しのAPIは R6 の転送ではなく `Fermata` の正式なAPIになった
- 秒数の引数名を `seconds` にそろえた（設計に書いていなかった。#225 の「最も自然な状態にする」に従った）
- 移行は asmdef の削除だけでなく、旧生成物の本文の書き換えも行う。設定から消したカテゴリーのファイルも旧 `PauseManager.IPausable` を継承しており、asmdef を消すとコンパイルできなくなるため

### 検証結果（リモートの代替検査）

- `release_round.py preflight`: 既知の NG 2件以外は OK
- `generate_meta.py --check`、`build_module_docs.py --check`、`sync_agent_skill_locators.py --check`: OK
- 同名型の検査、公開型のテスト網羅の再現: OK
- 重複 `using`、自分の名前空間の `using` の grep: 該当なし（`PauseCategoryGenerator` と、そのテストの文字列リテラルだけ）
- 旧名の残り: パッケージの `.cs` / `.uxml` / `.json` に `PauseManager`、`SymphonyFramework.System`、`Pausable` で始まる旧待機API名が残っていない（移行処理の文字列と `RootFolderCaseMigrationTests` の 6.15 → 6.16 の検査値を除く）

### 未実施リスト（R6 の検証へ追加）

- コンパイルと全テスト。特に `PauseCategoryGeneratorTests.FrameworkAsmdef_DoesNotReferenceGeneratedAssembly`（Unity が `JsonUtility` で asmdef を読めること）、`FermataTests` の購読2件
- 名前空間 `SymphonyFramework.Pause` が、`SymphonyFramework` 配下で `Pause()` を呼ぶコードを隠していないこと（メンバー参照なので隠れない見込み。grep で単純名の `Pause` は無し）
- **既存プロジェクトの移行（PC の SKC 相当の構成で確かめる）**: ① Framework asmdef に Enum の参照が残っている状態からの起動で、参照が外れ、Enum → Framework が入ること ② 旧 `PauseCategory` asmdef と旧生成物がある状態で、書き換え・削除・再コンパイルが通ること。**ただし ② は旧生成物が `PauseManager.IPausable` を使っており、R6 の互換層が無いと最初のコンパイルが通らず Editor の起動処理が動かない。** R6 で `PauseManager.IPausable` の転送を入れてから確かめる
- Administrator の Fermata パネル、`FermataSample`、Asset Store Tools のパイプライン出力（旧APIの削除後も動くこと）

### 振り返り

- 回答1 は推奨（転送クラスにだけ残す）と違う「全カテゴリー対象の正式API」だった。非推奨にした時点の理由（カテゴリー指定を促す）より、全体を対象にした待機の需要を優先した判断として記録する
- 生成物の移行は「最初のコンパイルが通らないと Editor の移行処理が走らない」という順序の問題を持つ。互換層（R6）と移行処理の両方がそろって初めて検証できるため、R6 の検証項目に明記した


---

# Round 6: 互換レイヤーと 7.0.0（#227）

> **状態: 実装済み、PR #239（develop 向け）。** 版は 7.0.0。Unity での検証は PC で行う（下の「実施レポート（Round 6）」）。

## 目的

6.x（基準は main の 6.15.2）から 7.0.0 へ直接上げた利用側が、**まず警告付きでコンパイルでき**、シーン・アセット・設定・セーブデータが壊れない状態にする（#225 の「互換の保証範囲」、#227 の「リリース前のチェック」）。そのうえで移行ガイドに従えば警告が消える。

## 前提: 6.x の名前はすべて `SymphonyFrameWork.*`

6.x の公開型は、アセンブリ `SymphonyFrameWork`、名前空間 `SymphonyFrameWork.*` にあった。7.0.0 では全公開型（Runtime 約55、Editor 約50）の名前空間が変わる。互換の手段は型の種類で分かれる。

| 種類 | 例 | 互換の手段 |
| --- | --- | --- |
| 名前と使い方が変わった Facade | `ServiceLocator`、`SceneLoader`、`SaveStore`、`AudioManager`、`PauseManager` など7件 | **旧名前空間・旧名の `[Obsolete]` 転送クラス**（#225 §5）。中身は新しい Facade を呼ぶだけ |
| 利用者が実装・継承する型 | `IInjectable` 系、`IInitializeAsync`、`PauseManager.IPausable`、`SaveDataContent`、`SaveDataLoaderStrategy`、`PlayerPrefsSaveDataLoaderStrategy` | **旧名前空間に、新しい型を継承した `[Obsolete]` 型**（#225 §5） |
| 旧Facadeの引数に出る型 | `LocateTypeEnum` | `[Obsolete]` で残し、転送クラスの中で `ServiceLifetimeEnum` へ変換する |
| 名前空間だけ変わった型 | 属性、`SymphonyTween`、`SymphonyDebugLogger`、`SymphonyAwaitable`、例外など | 質問1 |
| `[SerializeReference]` で保存される型 | ローダー3型 | R1 で付けた `[MovedFrom]`（6.15 の名前）のまま |
| MonoBehaviour / ScriptableObject | `ServiceComponent`、`SuiteAsset`、Config | `.meta` の GUID で保たれている（R1〜R5） |

## 転送クラスの一覧

置き場は CodeGuidelines どおり `Runtime/Obsolete/<旧名前空間>/`。名前空間 `SymphonyFrameWork.*` の中では `System.X` が `SymphonyFrameWork.System` に隠されるため、`global::System` を使う。

| 旧（6.15） | 転送先 | 変換が要るもの |
| --- | --- | --- |
| `SymphonyFrameWork.System.ServiceLocate.ServiceLocator` | `Partitura` | `GetInstance` は従来どおり null を返す（R2 の決定事項）。`LocateTypeEnum` → `ServiceLifetimeEnum` |
| `SymphonyFrameWork.System.ServiceLocate.ServiceInjector` | `Partitura.Inject` / `CreateInstance` | |
| `SymphonyFrameWork.System.SceneLoad.SceneLoader` | `Attacca` | `Action<float>` の進捗を `IProgress<float>` へ（R3 で残した `WrapProgress`）。単体・配列の旧メソッド名 |
| `SymphonyFrameWork.System.SceneBlock.SceneBlockLoader` | `Suite` | 旧 `SceneBlockInfo` を返す API は、新しい `SuiteInfo` を返す（明示的に型を書いたコードは保証外） |
| `SymphonyFrameWork.System.SaveSystem.SaveStore` | `Manuscript` | `GetEntries` の未初期化は例外になる（R4 の決定事項）。移行ガイドに書く |
| `SymphonyFrameWork.System.AudioManager` | `Sonority` | `GetAudioSource` は従来どおり未登録で null（`TryGetSource` で実装） |
| `SymphonyFrameWork.System.PauseManager` | `Fermata` | `Pause` プロパティ、`OnPauseChanged`、`Pausable*`（カテゴリー有無とも）、入れ子の `IPausable`（`RegisterPauseManager` 付き） |

旧 `PauseManager.IPausable` は新しい `IPausable` を継承する。**R5 の PauseCategory 移行は、この型があって初めて旧生成物がコンパイルでき、Editor の移行処理が走る**（R5 の未実施リスト）。

削除予定はすべて「次のメジャー更新（8.0.0）」とし、`Deprecations.md` へ行を足す。

## 依頼者に決めてもらうこと

1. **名前空間だけ変わった型（属性、Tween、Logger など約90型）の互換**: ⭐a. 旧名前空間ごとに空の目印（internal 型）を置いて `using SymphonyFrameWork.X;` の行だけはコンパイルを通し、型そのものには 6.x の名前を指す `[MovedFrom]` を付けて、Unity の API Updater に利用側スクリプトを書き換えさせる。効かない分は移行ガイドの一括置換（`SymphonyFrameWork` → `SymphonyFramework`）で直す / b. すべてに `[Obsolete]` の派生型を置く（`sealed` の属性、enum、struct は派生できないため、半分ほどしか効かない） / c. 移行ガイドの一括置換だけにする
2. **SymphonyKillChord の扱い**: ⭐a. 7.0.0 を main へ入れる前に、SKC の参照を 6.15.2 に固定する PR を出す。7.0.0 のリリース後に、新しい名前へ書き換える PR を別に出す（警告0で移る） / b. 固定せず、7.0.0 が main に入った時点で SKC は転送クラスの警告付きで動かす

## 移行ガイド

`Documentation~/Migration7.md` を新しく書く。

- 旧名 → 新名の対応表（CHANGELOG 6.16.0〜6.20.0 の表をまとめる）
- 挙動が変わったもの: `Partitura.Destroy` の同一参照、`Attacca.UnloadAsync` が Suite の保持シーンで `false`、`Manuscript.GetEntries` の未初期化例外、`Sonority.GetSource` の例外、`PauseCategory` の asmdef
- 保証しない範囲と書き換え方（#225 §5 の「保証しない」）
- API Updater が書き換える範囲（PC で確かめた結果を書く）

## 検証（PC の Unity）

R1〜R5 の未実施リストに加え、#227 の「リリース前のチェック」を行う。

1. ワークスペースでコンパイル 0/0、EditMode / PlayMode の全テスト、Play の開始と終了を2回
2. **6.x の利用側を模した状態からの更新**: 一時プロジェクト（ワークスペースの別クローンは作らない方針のため、ワークスペースのブランチを 6.15.2 へ戻して生成物を作らせ、7.0.0 へ戻す手順）で、警告だけでコンパイルが通ること、設定・セーブデータ・Suite アセット・PauseCategory が移行されること
3. API Updater の動作（質問1 が a の場合）
4. SKC の PC クローンで、7.0.0 を指したときのコンパイル（質問2 の a でも、書き換え PR の前に一度確かめる）

## テスト

- 転送クラスごとに、旧APIが新しい Facade と同じ結果を返すこと（例: 旧 `ServiceLocator.GetInstance` が未登録で null、旧 `PauseManager.Pause = true` で全カテゴリーが止まる）
- 旧 `IPausable` / `IInjectable` / `SaveDataContent` を実装した型が、新しい Facade で扱えること
- `[MovedFrom]` の値が 6.15 の名前を指していること（`CustomAttributeData`。R1 のローダーと同じ形）
- R2 の振り返りで取り下げた「旧名前空間に型が無いこと」のテストは、「旧名が解決できること」のテストとして入れ直す
- 公開型の網羅: 転送クラスは `Runtime/Obsolete/` 以下に置き、網羅検査の対象から外す（旧名と新名でテストが二重になるため）。外す仕組みは検査側に足す

## 利用側（SymphonyKillChord）への影響の目安

`using SymphonyFrameWork.*` が約170行（`ServiceLocate` 77、`Attribute` 47、`SaveSystem` 30、`SceneLoad` 9 ほか）。~~`Attribute` の47ファイルのうち実際に属性を使うのは `DisplayText` の1か所~~ **（訂正、R6 実装時）: `Attribute` を `using` する47ファイルのうち45ファイルが `[SubclassSelector]` などの属性を使う。** 属性は転送クラスを置かない型（質問1 の a）なので、SKC は API Updater の書き換えに頼る（下の実施レポート）。

## 決定事項（2026-11-21、依頼者の回答）

1. a: 旧名前空間ごとに目印の internal 型を置いて `using` を通し、名前空間だけ変わった型には 6.x の名前を指す `[MovedFrom]` を付けて API Updater に書き換えさせる。効かない分は移行ガイドの一括置換
2. **b**: SKC は固定しない。7.0.0 が main に入った時点で、SKC は互換層の警告付きで動かす。**そのため、PC の検証 4.（SKC で 7.0.0 を指したときのコンパイル）は main へのマージ前に必ず行う**

## 実施レポート（Round 6）

### 実装した内容

- **転送クラス**（`Runtime/Obsolete/<旧名前空間>/`、すべて `[Obsolete]`、削除は 8.0.0）
  - Facade: `ServiceLocator`、`ServiceInjector`、`SceneLoader`、`SceneBlockLoader`、`SaveStore`、`AudioManager`、`PauseManager`。設計の7件に加え、`SymphonyLocateObject<T>`（→ `PartituraReference<T>`）も転送にした
  - 6.x と戻り値を合わせたもの: `ServiceLocator.GetInstance` / `SymphonyLocateObject.GetInstance` / `AudioManager.GetAudioSource` は未登録で `null`、インスタンスを渡す `DestroyInstance` / `IsExistInstance` は型だけで処理、`RegisterAfterLocate(Action)` は `WhenRegistered` へ包む
  - 継承させた旧型: `IInjectable` 5種、`PauseManager.IPausable`（`RegisterPauseManager` 付き）、`SaveDataContent`、`SaveDataLoaderStrategy`、`PlayerPrefsSaveDataLoaderStrategy`。`LocateTypeEnum` は値の順序で変換
- **旧名前空間の目印**: 3アセンブリ（Runtime / Core / Editor）の `Obsolete/LegacyNamespaceMarkers.cs` に internal な空の型
- **`[MovedFrom]`**: 80型（Runtime / Core / Editor）。改名した `SceneBlock*` → `Suite*`、`ServiceLocateComponent` → `ServiceComponent`、Editor ウィンドウ3種、`SymphonyEditorConfigLocator` → `SymphonyEditorConfigProvider` は旧型名も指す
- テスト `Version7CompatibilityTests`（旧名前空間の存在、`[MovedFrom]` の値、転送の結果、旧ローダーの橋渡し）。公開型の網羅検査は `/Obsolete/` を対象外にした
- 文書: `Documentation~/Migration7.md`（新規）、Deprecations の「7.0.0（6.x の名前からの転送）」、CHANGELOG 7.0.0、README / AGENTS / index / Architecture（Runtime のフォルダ一覧が古かったので現状へ直した）。ワークスペースの CodeGuidelines に旧名前空間の転送の置き方を足した

### 設計から変えた点

- **`IInitializeAsync` は継承した旧型を置かず、`[MovedFrom]` にした。** メンバーが `protected` で、実装は必ず明示的な実装（`Task IInitializeAsync.InitializeAsync()`）になる。旧型が新しい型を継承しても、明示的な実装の宣言元が合わずコンパイルできない（CS0539）
- **旧ローダー2種は、旧 `SaveDataContent` を受け取る抽象メソッドへ橋渡しする形にした。** 6.x の派生型（SKC の `EncryptedFileSaveDataLoaderStrategy` もそう）は旧型を引数にした override を持つため、単に新しい型を継承させるだけでは override が合わない。新しい型だけを継承したデータを渡すと `InvalidOperationException`
- **`PauseCategoryResolver` が旧 `PauseManager.IPausable` を既定カテゴリーとして扱うようにした。** 旧型は新しい `IPausable` を継承するため、何もしないと1つのカテゴリーに数えられ、6.x の書き方の対象が既定カテゴリーから外れる。旧 `PauseManager.SetPause<PauseManager.IPausable>` なども既定カテゴリーへ読み替える
- 重複型名の除外リストへ、転送の4型と目印を足した

### 検証結果（リモートの代替検査）

- `release_round.py preflight`: 既知の2件（ブランチ名、Enter Play Mode Options）以外は OK（版 7.0.0、BOM、meta、asmdef、文書の同期）
- `generate_meta.py --check`、`build_module_docs.py --check`、`sync_agent_skill_locators.py --check`: OK
- 重複型名の検査（CI と同じコマンド）: OK
- 公開型の網羅（`cov.py`、`/Obsolete/` を除外）: 欠落・残り・古い行なし
- 変更した C# の構文検査（tree-sitter）: エラー 0。**意味のコンパイル（型の解決、override の一致、CS0618 以外の警告）は Unity でしか確かめられない**

### 未実施リスト（PC の Unity で行う）

1. ワークスペースのコンパイル 0 エラー（警告は `[Obsolete]` の自己参照が無いこと）、EditMode / PlayMode の全テスト、Play の開始と終了を2回
2. `Version7CompatibilityTests` の全件
3. 6.15.2 の状態からの更新（設計の「検証」2.）: PauseCategory の旧生成物が転送でコンパイルされ、Editor の起動時に移行されること
4. API Updater: `using SymphonyFrameWork.Attribute;` と `[SubclassSelector]` を書いたスクリプトが書き換えられること。internal な目印だけの名前空間で `using` がコンパイルできること
5. **SKC の PC クローンで 7.0.0 を指したときのコンパイル（決定事項 2.、main へのマージ前に必須）**

### SKC への影響（決定事項 2. の前提の訂正）

設計時は「`Attribute` を使うのは1か所」と見積もったが、実際は **45ファイルが属性を使い、ほかに6ファイルが名前空間だけ移った型（`SymphonyAwaitable`、`SymphonyDebugLogger`、`LogKindEnum` など）を使う。** これらは転送ではなく API Updater で直る型のため、SKC は 7.0.0 を初めて開いたときに API Updater が約51ファイルを書き換える。Editor では確認のダイアログが出る。**バッチモード（CI のビルド）では `-accept-apiupdate` が無いと書き換えずにコンパイルエラーになる。** 転送で直る `ServiceLocator` などの約120行は、警告だけで通る見込み。

### 振り返り

- 設計時の SKC の見積もりを、`using` の行数と1つの属性名だけで出していた。属性は `[SubclassSelector]` のように `Attribute` を省いて書くため、型名の検索では見つからなかった。互換の範囲を見積もるときは、`using` した名前空間の型すべてを短縮名（属性は `Attribute` を除いた名前）でも数える
- 「継承した旧型を置けば実装側はそのまま動く」は、暗黙の実装だけの話だった。明示的な実装と、引数に旧型を持つ override は別に扱う必要がある（`IInitializeAsync`、ローダー）

---

# Round 7: 旧名の書き換えツール（7.0.0 に含める）

> **状態: 設計（2026-11-22、依頼者が「書き換えツールを足す」を選択）。**

## 目的

R6 は、名前空間だけ移った型と改名した型を `[MovedFrom]` に任せ、Unity の API Updater が利用側スクリプトを書き換える前提だった。PC で 6.15.0 のサンプル（`Assets/Samples/`）を入れたまま 3 回開いたが、**`-accept-apiupdate` を付けても API Updater は一度も動かず、82 ファイルは 1 つも書き換わらなかった。** ログに APIUpdater / ScriptUpdater の行は無く、`Library/Bee/artifacts/mvdfrm/` に SymphonyFramework 由来の抽出結果も無い。Assets 内のソースとしてコンパイルされるアセンブリの `[MovedFrom]` は、この Unity（6000.3.10f1）では書き換えに使われていないと判断する（推測。Unity の内部仕様は確認できていない）。

そこで、**パッケージ自身が旧名を見つけて書き換えるツールを持つ。** 利用側（SKC を含む）は Editor のメニューから一括で書き換えられる。

## 公開API

無し。メニュー項目と起動時の通知だけを足し、型はすべて `internal`。

- メニュー: `Tools/SymphonyFramework/Migrate 6.x Names`（`SymphonyConstant.TOOL_MENU_PATH` + 名前）
  - 対象を走査し、書き換えるファイル数と置き換え件数をダイアログで示す。「書き換える」でファイルを書き、`AssetDatabase.Refresh()`
  - 結果（ファイルごとの置き換え、追加した `using`、残った `[Obsolete]` 転送の利用）をログへ出す
- 起動時の通知: `SymphonyEditorOrchestrator` の初期化の最後に、対象のスクリプトに旧表記 `SymphonyFrameWork` が残っていれば、メニューの場所を警告ログで1回知らせる（`SessionState` で1セッション1回）。**失敗しても初期化を止めない。**

## 書き換えの規則

対応表は**読み込み済みの `SymphonyFramework*` アセンブリから作る**（手書きの表を持たない。`[MovedFrom]` と食い違わないため）。

1. `[MovedFrom]` を持つ型ごとに、(旧名前空間, 旧型名) → (新名前空間, 新型名) を得る。旧型名が省略されていれば新型名と同じ。属性型は `Attribute` を除いた短縮名も対象にする
2. 旧名前空間に残した public な `[Obsolete]` 転送（`ServiceLocator` など）は**書き換えない**。名前もメンバーも変わるため、機械的に置き換えると別の API を呼ぶことになる。警告のまま残し、ログで移行ガイドを示す
3. 1 ファイルずつ、コメントと文字列を除いた識別子を見て次を行う
   - `using SymphonyFrameWork.X;` で取り込んだ旧名前空間の型が使われていれば、型名を新型名へ置き換え、新名前空間の `using` を足す
   - 完全修飾（`SymphonyFrameWork.X.Old`）は新しい完全修飾へ置き換える
   - 旧名前空間から移った型に拡張メソッドを持つ static クラスがあれば、その新名前空間も足す（`LogAndCheckComponentNull` など。型名が本文に現れないため）
   - 旧 `using` は、その名前空間の転送をまだ使っていれば残し、使っていなければ消す
   - **転送と同じ名前の新しい型**（`SaveDataContent` など）は、旧 `using` が残る場合に曖昧（CS0104）になるため、`global::` 付きの完全修飾で書く
   - メンバー（`.` の直後）の識別子は置き換えない
4. メンバー名の変更（`BlockName` → `SuiteName` など）は対象外。ログに `Migration7.md` を示す
5. 文字コード（BOM の有無）と改行コードを保つ

対象は `Assets/` 以下の `.cs`。パッケージ自身（`SymphonyFramework.Editor` の asmdef があるフォルダの親）を除く。

## ファイル構成

- `Editor/Migration/LegacyNameMap.cs`（`SymphonyFramework.Editor`、internal）: 対応表。アセンブリの一覧から作る `Create(IEnumerable<Assembly>)` と、テスト用に項目から作るコンストラクタ
- `Editor/Migration/LegacyNameRewriter.cs`（internal static、Unity API に触れない）: `Rewrite(string source, LegacyNameMap map)` → `LegacyNameRewriteResult`
- `Editor/Migration/LegacyNameMigration.cs`（internal static）: 走査、ダイアログ、書き込み、メニュー、起動時の通知
- `Editor/Orchestrator/Internal/SymphonyEditorOrchestrator.cs`: 通知の呼び出しを足す
- テスト: `Tests/Editor/LegacyNameRewriterTests.cs`、`Tests/Editor/LegacyNameMapTests.cs`

## 依存方向

Editor 内だけ。Runtime / Core の型は対応表を作るときにリフレクションで読むだけで、新しい参照は増えない。

## エラー処理

- 読み書きの失敗（IOException など）はファイルごとにログへ出して次へ進む。1 ファイルの失敗で全体を止めない
- 起動時の通知は例外を握りつぶしてログへ出すだけにする（初期化を止めない）

## 影響範囲

公開 API とシリアライズ形式は変わらない。利用側のファイルは、メニューで確認したときだけ書き換える（自動では書き換えない）。

## テストの置き場と種別（EditMode、`Tests/Editor/`）

- `LegacyNameRewriterTests`: 文字列を入れて結果の文字列を比べる
  - `Rewrite_RenamedType_ReplacesNameAndUsing`（`SceneBlockAsset` → `SuiteAsset` と `using SymphonyFramework.Scenes;`）
  - `Rewrite_NamespaceOnlyMove_AddsNewUsingAndRemovesOldUsing`
  - `Rewrite_AttributeShortName_Replaces`（`[SubclassSelector]`）
  - `Rewrite_FullyQualifiedName_Replaces`
  - `Rewrite_ForwarderStillUsed_KeepsOldUsing`（`ServiceLocator` は残る）
  - `Rewrite_SameNameAsForwarder_UsesGlobalQualifiedName`
  - `Rewrite_ExtensionMethodNamespace_AddsUsing`
  - `Rewrite_CommentsStringsAndMemberAccess_AreUntouched`
  - `Rewrite_NoLegacyName_ReturnsUnchanged`
  - `Rewrite_PreservesLineEndings`
- `LegacyNameMapTests`: 読み込み済みのアセンブリから作った表に、`SceneBlockAsset` → `SuiteAsset`、`SubclassSelectorAttribute` の短縮名、転送 `ServiceLocator` が含まれることを確かめる

メニューとダイアログは自動で押せない（`--no-tests-reason` は使わない。ロジックはテストがある）。

## 動作確認手順（PC）

1. ワークスペース: 6.15.0 のサンプルを入れたまま開き、起動時の警告が出ること。メニューで書き換え、サンプルがコンパイルできること（残るのはメンバー名の変更だけで、件数を記録する）
2. SKC の PC クローン: 7.0.0 を指してメニューで書き換え、コンパイルが通ること。書き換えた差分を SKC の PR にする（コミットはユーザーの確認後）

## バージョン判断

7.0.0 はまだ main へ出していないため、7.0.0 に含める（CHANGELOG 7.0.0 の Add へ追記）。版は変えない。
