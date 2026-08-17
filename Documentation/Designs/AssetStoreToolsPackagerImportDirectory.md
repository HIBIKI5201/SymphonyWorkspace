# Packagerのインポート元ディレクトリ指定

## 目的

Asset Store Tools Packager の Import タブは、Project Settings の `Exported Packages Path` 配下にある出力履歴だけをポップアップへ列挙している。別の場所へコピーした出力物や、他プロジェクトから受け取った出力物を取り込むため、エクスプローラーから任意の出力済みディレクトリを直接指定できるようにする。

## Round分割

1 Roundで実施する。Import タブの入力UI、任意ディレクトリを受け付ける経路のテスト、利用者向け文書、バージョン更新をまとめて変更する。公開APIやシリアライズ形式の変更、選択ディレクトリの永続化、パッケージのインポート判定規則の変更は含めない。

既存参照を `rg` で確認した結果、履歴一覧取得の `GetExportDirectories` と `_exportDirectories` / `_exportDirectoryLabels` / `_selectedExportIndex` は `AssetStoreToolsPackageWindow` の Import タブだけから使われている。削除によって他の参照元は壊れない。ワークスペース側の `Documentation/` と `AGENTS.md` には、履歴ポップアップの構造を説明する記述は無い。

## 公開API

変更しない。対象は `SymphonyFrameWork.Editor` アセンブリ内の EditorWindow と `internal` なインポート処理だけである。

## ファイル構成

| パス | 変更内容 | 名前空間 |
| --- | --- | --- |
| `Assets/SymphonyFrameWork/Editor/Generator/AssetStoreToolsPackager/AssetStoreToolsPackageWindow.cs` | Import タブへフォルダ選択ボタンと選択パス表示を追加し、履歴ポップアップを削除する | `SymphonyFrameWork.Editor` |
| `Assets/SymphonyFrameWork/Editor/Generator/AssetStoreToolsPackager/AssetStoreToolsPackageImporter.cs` | `Exported Packages Path` 配下の履歴を列挙する未使用処理を削除する | `SymphonyFrameWork.Editor` |
| `Assets/SymphonyFrameWork/Tests/Editor/AssetStoreToolsImportPlannerTests.cs` | 任意の一時ディレクトリにあるマニフェストから候補を構築できることを追加検証する | `SymphonyFrameWork.Tests` |
| `Assets/SymphonyFrameWork/Documentation~/Modules/AssetStoreToolsPackager.md` | Import タブの操作手順と注意点をフォルダ直接指定へ更新する | Markdown |
| `Assets/SymphonyFrameWork/Documentation~/Html/Modules/AssetStoreToolsPackager.html` | モジュール文書から再生成する | HTML |

新しい型とファイルは追加しない。

## 依存方向

EditorWindow は View 層として `EditorUtility.OpenFolderPanel` を呼び、選択されたパスを既存の `AssetStoreToolsPackageImporter.BuildCandidates` と `Import` へ渡す。候補判定は既存の `AssetStoreToolsImportPlanner` へ委譲し、Windowへ判定規則を追加しない。

すべて `Editor/` と `Tests/Editor/` に閉じるため、Core → Runtime → Editor の依存方向と Player ビルドには影響しない。`EditorUtility.OpenFolderPanel` は `public static` な UnityEditor APIであり、同じ Editor アセンブリから直接呼べることを確認済みである。`BuildCandidates(string)` と `Import(string, IEnumerable<...>)` は `internal static` で、Windowとテストアセンブリから既存どおり到達できる。

## エラー処理

- フォルダ選択ダイアログをキャンセルした場合は、現在の選択パスと候補を維持する。
- ディレクトリをまだ選択していない場合は案内を表示し、候補一覧とインポート操作を表示しない。
- 選択したディレクトリに `PackageManifest.json` が無い場合は、既存と同じ警告を表示してインポート操作を止める。
- 選択後にディレクトリやパッケージが削除された場合は、`Refresh` で既存の読込結果へ更新する。欠けたパッケージは既存の `Import` がエラーログを出し、残りを続行する。

## 影響範囲

公開APIとシリアライズ形式は変わらない。Import タブの取り込み元選択だけが変わり、`Exported Packages Path` は引き続き Export タブの出力先と、フォルダ選択ダイアログの初期位置に使う。

従来の出力履歴は自動列挙されなくなる。利用者は取り込み元の出力済みディレクトリをフォルダ選択ダイアログで指定する。選択後の状態比較、既定選択、インポート処理は変えない。

## テストの置き場と種別

EditMode の `Assets/SymphonyFrameWork/Tests/Editor/AssetStoreToolsImportPlannerTests.cs` へ次を追加する。

| テスト | 検証内容 | 書き方 |
| --- | --- | --- |
| `BuildCandidates_ArbitraryDirectory_LoadsManifest` | Project Settings の出力先に依存せず、任意ディレクトリのマニフェストから候補を作れる | テスト用一時ディレクトリへ `TryWriteManifest` で1件を書き、`BuildCandidates` の件数・名前・リビジョンを検証し、`finally` で削除する |

フォルダ選択ボタンの押下とOSダイアログの操作は Editor GUI のため自動化できない。ボタンから選択パスが既存の候補構築経路へ渡ることは差分レビューと下記の手動確認で検証する。

## 動作確認手順

| 区分 | 手順 | 期待結果 |
| --- | --- | --- |
| 自動 | `python scripts/verify_round.py` を実行する | コンパイルのError/Warningが0、EditMode/PlayModeテストが全件成功し、Play Mode 2往復でも新しいログが出ない |
| 自動 | `python scripts/build_module_docs.py` を実行する | モジュール文書のHTMLが生成され、リンク検査が成功する |
| 人の操作 | Packagerを開いて Import タブへ切り替える | 取り込み元未選択の案内、パス表示、フォルダ選択ボタンが表示される |
| 人の操作 | `Exported Packages Path` の外にある、`PackageManifest.json` を持つ出力済みディレクトリを選ぶ | 選択パスと候補一覧が表示され、状態とリビジョンが従来どおり判定される |
| 人の操作 | フォルダ選択を再度開いてキャンセルする | 直前の選択パスと候補一覧が維持される |
| 人の操作 | 選択中のマニフェストを変更して `Refresh` を押す | ウィンドウを開いたまま候補一覧が最新内容へ更新される |
| 人の操作 | マニフェストを持たないディレクトリを選ぶ | 警告が表示され、インポート操作へ進めない |

## バージョン判断

`6.1.1` のパッチ更新とする。公開契約を変えず、Editorツールの取り込み元選択を Issue #184 の意図どおり修正するためである。

## この Round で触るバージョン関連ファイル

| パス | 変更内容 |
| --- | --- |
| `Assets/SymphonyFrameWork/package.json` | `version` を `6.1.0` から `6.1.1` へ更新する |
| `Assets/SymphonyFrameWork/CHANGELOG.md` | `6.1.1` のFixとして Issue #184 と操作変更を記録する |
| `Assets/SymphonyFrameWork/Documentation~/Modules/AssetStoreToolsPackager.md` | Import タブの正本を更新する |
| `Assets/SymphonyFrameWork/Documentation~/Html/Modules/AssetStoreToolsPackager.html` | 正本から再生成する |

