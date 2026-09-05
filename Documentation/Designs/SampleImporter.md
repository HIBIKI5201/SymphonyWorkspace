# SampleImporter

## 目的

`Assets/SymphonyFrameWork/Samples~/` の各サンプルは、フォルダ名末尾の `~` によって**Unityのインポート対象から外れている**。UPM導入時は Package Manager の `Samples > Import` が `Assets/Samples/` へコピーするが、**このワークスペースのようにFrameworkを `Assets/` 直下へ置いている場合、その入口が存在しない**。`UnityEditor.PackageManager.UI.Sample.FindByPackage` はUPMが解決したパッケージにしか答えないため、Assets直置きでは空を返す。

結果として、サンプルを試すには利用者が手でフォルダをコピーするしかない。**Samples~ の取り込み口をFramework自身のEditor機能として持たせ、UPM導入とAssets直置きのどちらでも同じ手順で試せるようにする。**

## 公開API

`SymphonyFrameWork.Editor` 名前空間へ、Editorアセンブリの公開エントリポイントとして置く。**利用側がEditor拡張やバッチから呼べる必要がある**（サンプルの取り込みをプロジェクト側の初期セットアップへ組み込む用途）ため `public` とする。

```csharp
public static class SymphonySampleImporter
{
    /// <summary> package.json が宣言するサンプルの一覧を返す。 </summary>
    public static IReadOnlyList<SymphonySampleInfo> GetSamples();

    /// <summary> サンプル1件を Assets へ取り込む。 </summary>
    public static bool TryImport(SymphonySampleInfo sample, bool overwrite, out string destinationPath);

    /// <summary> 取り込み済みサンプルのシーンを Build Settings へ追加する。 </summary>
    public static int RegisterSampleScenes();
}

public sealed class SymphonySampleInfo
{
    public string DisplayName { get; }
    public string Description { get; }
    public string SourceRelativePath { get; }   // "Samples~/Runtime/PauseManagerSample"
    public string DestinationAssetPath { get; } // "Assets/Samples/Symphony Framework/6.15.0/Pause Manager Sample"
    public bool IsImported { get; }
}
```

- 取り込みの失敗（コピー先が既にある、`Samples~` が見つからない）は**通常起こり得る**ため Try pattern とし、例外を投げない。引数 `null` は `ArgumentNullException`。
- `Info` は渡された値だけを保持し、抽出ロジックを持たない。

## ファイル構成

| パス | 名前空間 | 公開範囲 |
| --- | --- | --- |
| `Editor/Samples/SymphonySampleImporter.cs` | `SymphonyFrameWork.Editor` | `public` 公開エントリポイント |
| `Editor/Samples/SymphonySampleInfo.cs` | `SymphonyFrameWork.Editor` | `public` Info |
| `Editor/Samples/Internal/SymphonySampleManifestReader.cs` | `SymphonyFrameWork.Editor` | `internal`。package.json の `samples` を読む |
| `Editor/Samples/Internal/SymphonySamplePathResolver.cs` | `SymphonyFrameWork.Editor` | `internal`。**Unity APIへ触れない純粋ロジック** |
| `Editor/Samples/Internal/SymphonySampleCopier.cs` | `SymphonyFrameWork.Editor` | `internal`。`System.IO` による再帰コピー |
| `Editor/Samples/Internal/SymphonySampleSceneRegistrar.cs` | `SymphonyFrameWork.Editor` | `internal`。Build Settings への追加 |
| `Editor/Samples/Internal/SymphonySampleAutoImporter.cs` | `SymphonyFrameWork.Editor` | `internal`。自動同期。Orchestratorから初期化 |
| `Editor/Configs/ConfigData/SymphonySampleImportConfig.cs` | `SymphonyFrameWork.Editor` | `public` Config（既存 `AutoEnumGeneratorConfig` と同型） |
| `Editor/Administrator/UITK/CS/SampleWindow.cs` | `SymphonyFrameWork.Editor` | `internal` View。Administrator の1節 |

**`Runtime/` と `Core/` へは機能を追加しない。** 取り込みはEditor専用の作業であり、Runtimeへ持ち込む理由が無い（`Core/SymphonyConstant.cs` の `VERSION` は版更新でのみ変わる）。

## 依存方向

Editor → Runtime/Core の一方向のみ。`SymphonyConstant.GetFrameworkAbsolutePath()`（`public`、`#if UNITY_EDITOR` 内）だけをCoreから使う。**確認済み**: `Core/SymphonyConstant.cs:31` にあり、UPMなら `PackageInfo.resolvedPath`、Assets直置きなら `Application.dataPath` 基準を返す。`Samples~` はどちらの配置でも実ファイルとして存在する。

**UPMの `Sample` APIは使わない。** Assets直置きで空を返すため分岐が2系統になり、片方をこのワークスペースで検証できない。自前のコピーに一本化すれば、UPM導入時も同じ経路が走る。

`SymphonySamplePathResolver` はUnity APIへ触れないため、EditModeテストで直接検証する。

## エラー処理

| 事象 | 扱い |
| --- | --- |
| `Samples~` またはサンプルフォルダが無い | `TryImport` が `false`。Consoleへ Warning |
| コピー先が既に存在し `overwrite: false` | `TryImport` が `false`。Windowは上書き確認ダイアログを出す |
| package.json の `samples` が空・壊れている | `GetSamples()` が空を返し、Consoleへ Warning。例外にしない |
| `path` が `Samples~/` の外を指す（`..` を含む） | **`SymphonySamplePathResolver` が拒否し、コピーしない。** package.json は人が編集するため、パッケージ外への書き込みを防ぐ |
| 引数が `null` | `ArgumentNullException`（呼び出し側の実装ミス） |

Editorの補助機能であり、取り込めないことで利用側の作業を止めない。**失敗はConsoleのログで通知する**（`SymphonyDocumentation.Open` と同じ方針）。

## 取り込み先とコピー内容

コピー先はUPMの慣習へ揃える。

```text
Assets/Samples/Symphony Framework/<version>/<サンプルのdisplayName>/
```

`<version>` は `SymphonyConstant.VERSION`。**版ごとに分かれるため、Framework更新後も旧版のサンプルが残り、上書き事故が起きない。**

**`.meta` も一緒にコピーする。** `Samples~` はAssetDatabaseの管理外でGUIDが衝突しないため、`.meta` を持ち込むことでサンプルシーン内のスクリプト参照とアセット参照がそのまま解決する。`.meta` を捨てるとシーンの参照が全て切れる。

## 自動同期

`SymphonySampleImportConfig.AutoImportSamples`（既定 **false**）が `true` のときだけ、**Editor初期化時に1回**、未取り込みのサンプルを取り込む。

- 設定の保存先は `ProjectSettings/Packages/`（`EditorSymphonyConstant.PROJCET_SETTING_FILE_PATH`）。プロジェクト共有設定であり、`EditorPrefs` は使わない
- `[InitializeOnLoad]` を自前で持たず、**`SymphonyEditorOrchestrator.Initialize()` の登録モジュールとして初期化・終了する**（既存の `AutoEnumGenerator` などと同じ並び）
- **既存の取り込み済みフォルダを上書きしない。** 自動同期が利用者の編集を消さないための境界である。削除したサンプルが復活するのは仕様とし、Windowの説明文へ書く
- `AssetDatabase.Refresh` はOrchestratorの集約経路（`_requiresAssetDatabaseRefresh`）へ委ね、モジュールから直接呼ばない

## シーンのBuild Settings登録

**取り込みとは別の操作にする。** `Scene Loader` と `Scene Block` のサンプルはシーンがBuild Settingsに無いと動かないが、利用者のBuild Settingsとその変更で走る `SceneListEnum` 再生成を、コピーの副作用として起こさない。

`RegisterSampleScenes()` は取り込み済みサンプル配下の `.unity` を集め、**既に登録済みのパスを除いた差分だけ**を `EditorBuildSettings.scenes` の末尾へ追加し、追加件数を返す。`AutoEnumGeneratorConfig.AutoSceneListUpdate` が `true` のプロジェクトでは、この登録を契機に `SceneListEnum` が再生成される。**これは既存の仕組みであり、この Round では触らない。**

差分の算出（`SymphonySampleSceneRegistrar.ResolveScenesToAdd`）はUnity APIへ触れない純粋関数として切り出し、テスト対象にする。

## 影響範囲

- 既存の公開APIとシリアライズ形式への影響は無い。**追加のみ。**
- `SymphonyAdministrator` へ節が1つ増える
- `SymphonyEditorOrchestrator` へモジュール登録が1つ増える
- `Documentation~/EditorTools.md` の一覧へ1行、`README.md` のサンプル導入手順へAssets直置き時の記述を追加する

## テストの置き場と種別

すべて EditMode（`Assets/SymphonyFrameWork/Tests/Editor/`）。命名は既存に合わせ `対象_条件_期待`。

| ファイル | テスト | どう書くか |
| --- | --- | --- |
| `SymphonySamplePathResolverTests.cs` | `ResolveDestination_SampleName_UsesVersionedSamplesPath` | 版とdisplayNameを渡し、返る文字列を `Is.EqualTo` で比較 |
| | `ResolveDestination_NameWithSeparator_Throws` | `/` や `\` を含む名前で `ArgumentException` を `Throws.TypeOf` |
| | `TryResolveSource_PathOutsideSamples_ReturnsFalse` | `"Samples~/../Runtime"` と `"../x"` を渡して `false` |
| | `TryResolveSource_ValidPath_ReturnsAbsolutePathUnderRoot` | 仮のルート文字列を渡し、戻り値が `StartsWith(root)` |
| `SymphonySampleManifestReaderTests.cs` | `Read_ValidJson_ReturnsDeclaredSamples` | **JSON文字列を直接受け取る形へ切る**（ファイルI/Oを挟まない）。件数と1件目の値 |
| | `Read_MissingSamplesField_ReturnsEmpty` | `{}` を渡して空 |
| | `Read_BrokenJson_ReturnsEmpty` | 壊れたJSONで空。`LogAssert.Expect(LogType.Warning, new Regex(...))` |
| `SymphonySampleCopierTests.cs` | `Copy_Directory_CopiesFilesAndMeta` | `Path.GetTempPath()` 配下に入力を作り、`.cs` と `.cs.meta` の両方が現れることを確認。`TearDown` で削除 |
| | `Copy_ExistingDestination_WithoutOverwrite_ReturnsFalse` | 同じ宛先へ2回呼び、2回目が `false` |
| `SymphonySampleSceneRegistrarTests.cs` | `ResolveScenesToAdd_AlreadyRegistered_ReturnsEmpty` | 登録済みパス配列と候補配列を渡す純粋関数として検証 |
| | `ResolveScenesToAdd_NewScenes_ReturnsThemInOrder` | 順序を `Is.EqualTo` で比較 |

**`SymphonySampleImporter` 自体と `SampleWindow` のボタン押下は自動検証しない。** Editor GUI の押下手段が無く、`EditorBuildSettings` の実書き換えはプロジェクト状態を汚す。**ロジックは上記4つの型へ寄せ、Facadeは配線だけにする。**

`PublicTypeTestCoverageTests` は公開型に対して `Tests/Editor/<型名>Tests.cs` を要求する。`SymphonySampleImporter`・`SymphonySampleInfo`・`SymphonySampleImportConfig` の3つが新しく公開型になるため、**Facadeの検証可能な範囲**（`GetSamples()` の件数と `DestinationAssetPath` の形、`null` 引数の例外、Configの既定値と `Save()` 後の読み戻し）でテストファイルを置く。残作業一覧へは追記しない。

## 動作確認手順

自動で確認する項目（`verify_round.py` とスクリーンショット）:

1. コンパイル エラー0・警告0、EditMode/PlayMode 全数成功
2. `SymphonySampleImporter.GetSamples()` が7件返し、`DestinationAssetPath` が版付きパスであること（`uloop execute-dynamic-code` で戻り値を読む）
3. Administrator を開いてスクリーンショットを撮り、7件のサンプル名と説明が並ぶこと、レイアウトが崩れていないこと
4. 自動同期を `true` にして再コンパイルし、`Assets/Samples/Symphony Framework/<version>/` が生成されること

人が操作する項目:

5. **Import ボタンの押下**（GUI操作のため自動化不可）
6. **「Build Settings へ登録」ボタンの押下**と、その後 `SceneBlockSample` を Play して依存順ロードが動くこと
7. 上書き確認ダイアログの表示（モーダルのため自動化不可）
8. **Administrator を開いたまま**サンプルを手で削除し、一覧の取り込み済み表示が追従すること

## バージョン判断

**マイナー更新 6.15.0。** 後方互換な機能追加のみで、既存APIとシリアライズ形式を変えない。

## この Round で触るバージョン関連ファイル

| ファイル | 変更 |
| --- | --- |
| `Assets/SymphonyFrameWork/package.json` | `version` を `6.15.0` |
| `Assets/SymphonyFrameWork/Core/SymphonyConstant.cs` | `VERSION` を `6.15.0`（`release_round.py bump` が行う） |
| `Assets/SymphonyFrameWork/CHANGELOG.md` | `## 6.15.0` の Add 項目 |
| `Assets/SymphonyFrameWork/README.md` | サンプル導入手順へAssets直置き時の記述 |
| `Assets/SymphonyFrameWork/Documentation~/EditorTools.md` | 一覧へ1行 |
