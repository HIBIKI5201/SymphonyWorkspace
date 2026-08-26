# Asset Store Tools Packager 出力先リンク

Issue: [#113](https://github.com/HIBIKI5201/SymphonyFramework/issues/113)

## 目的

Asset Store Tools Packager の出力成功ログに表示している出力先をクリック可能にし、
Unity Console から OS のファイルブラウザーで出力フォルダを直接開けるようにする。

現在も完了ログへ `ExportLocalPath` を出しているが、プレーンテキストなので利用者が
パスをコピーして Explorer へ貼り付ける必要がある。Unity Console が標準で処理する
`<a href="...">...</a>` のリッチテキストリンクへ置き換え、独自の常駐コールバックや
Packager ウィンドウの新しいボタンは追加しない。

## Round 分割

1 Round で実施する。完了ログの組み立て、EditMode テスト、利用者向け文書、HTML 生成物、
バージョン更新を合わせても 10 ファイル前後であり、単独で検証・リリースできる。

## 公開 API

公開 API は追加・変更しない。

ログ文字列の組み立ては `AssetStoreToolsPackagePipelineRunner` の
`internal static string BuildExportCompletionMessage(...)` へ切り出す。これは Unity Console の
GUI 操作を伴わずに URI と表示文字列を EditMode テストするための内部検証点であり、利用側の
拡張点ではない。

## 表示とリンクの契約

出力成功ログの構造は次の形にする。

```text
[AssetStoreToolsPackager]
パッケージを出力しました
path : <a href="file:///C:/.../Export_AssetStoreToolsPackage_..."><表示用の相対パス></a>
```

- `href` は `AssetStoreToolsPackageExportContext.ExportFullPath` から生成した絶対 `file:` URI とする。
- 表示文字列は、従来どおり短く読める `ExportLocalPath` とする。
- URI は `System.Uri.AbsoluteUri` で生成し、空白、`#`、日本語などを URI として安全な表現へ変換する。
- リッチテキストの表示文字列と属性値は `&`、`<`、`>`、`"` をエスケープし、出力パスを
  Console のマークアップとして解釈させない。
- リンクのクリック処理は Unity Console の標準動作へ委ねる。`EditorGUI.hyperLinkClicked` の
  購読や `EditorApplication` コールバックは追加しない。

## ファイル構成

| パス | 変更内容 |
| --- | --- |
| `Assets/SymphonyFrameWork/Editor/Generator/AssetStoreToolsPackager/Pipeline/AssetStoreToolsPackagePipelineRunner.cs` | 完了ログを `file:` URI のリンクとして組み立てる内部処理を追加 |
| `Assets/SymphonyFrameWork/Tests/Editor/AssetStoreToolsPackagePipelineTests.cs` | URI、表示パス、リッチテキストのエスケープを EditMode テスト |
| `Assets/SymphonyFrameWork/Documentation~/Modules/AssetStoreToolsPackager.md` | 出力完了ログのリンクから出力先を開けることを追記 |
| `Assets/SymphonyFrameWork/Documentation~/EditorTools.md` | Packager の一覧・説明へ出力先リンクを追記 |
| `Assets/SymphonyFrameWork/Documentation~/Html/**` | 利用者向け Markdown から再生成 |
| `Assets/SymphonyFrameWork/package.json` | バージョン更新 |
| `Assets/SymphonyFrameWork/Core/SymphonyConstant.cs` | バージョン定数を同期 |
| `Assets/SymphonyFrameWork/README.md` | 現在のバージョンを同期 |
| `Assets/SymphonyFrameWork/CHANGELOG.md` | Issue #113 の変更を記録 |

新しい型、名前空間、アセンブリ参照、Editor 初期化処理は追加しない。

## 依存方向とアクセス手段

変更は既存の `SymphonyFrameWork.Editor` アセンブリ内で完結する。
`AssetStoreToolsPackagePipelineRunner.Export` は既に出力成功時に
`AssetStoreToolsPackageExportContext` を保持しており、絶対パスの `ExportFullPath` と
表示用の `ExportLocalPath` の両方へ直接アクセスできる。

URI 生成には .NET Standard の `System.Uri` だけを使用する。OS 固有 API、
`Process.Start`、`EditorUtility.RevealInFinder` は使用しない。Unity Console がパスまたは URI の
リンクを標準で処理するため、Windows では Explorer、macOS では Finder へ委ねられる。

テストは既存の `InternalsVisibleTo("SymphonyFrameWork.Tests.Editor")` と
Editor テストアセンブリから内部メソッドへ到達する。実際のパッケージ出力や
ファイルブラウザー起動は行わず、純粋な文字列生成として検証する。

## エラー処理

完了メッセージは、すべての出力手順を実行した後という既存の位置でだけ出す。
出力処理や個別 Strategy の例外処理は変更しない。

`ExportFullPath` はコンテキスト生成時に確定する絶対パスであり、通常は有効な URI へ変換できる。
それでも URI 生成に失敗した場合、出力済みという事実まで失わせないよう例外を外へ出さず、
従来のプレーンテキスト `ExportLocalPath` を含む完了ログへフォールバックする。
リンク生成の失敗をパッケージ出力の失敗として扱わない。

## 影響範囲

- `.unitypackage`、ZIP、Manifest、バージョンログの内容と出力先は変わらない。
- Packager ウィンドウ、Project Settings、パイプライン Strategy の公開契約は変わらない。
- Console の完了ログだけが、見た目をほぼ維持したままクリック可能になる。
- `Debug.Log` のリッチテキストを表示しない外部ログ収集ではタグが見える可能性があるが、
  情報量は従来と同じであり、絶対 URI が追加される。
- Runtime、Player ビルド、Domain Reload 前後の static 状態には影響しない。

## テストの置き場と種別

`Assets/SymphonyFrameWork/Tests/Editor/AssetStoreToolsPackagePipelineTests.cs` へ
次の EditMode テストを追加する。

| テスト | 検証内容 |
| --- | --- |
| `BuildExportCompletionMessage_AbsolutePath_ContainsFileUri` | 絶対パスから `file:` URI を生成し、空白と `#` が安全に符号化される |
| `BuildExportCompletionMessage_LocalPath_DisplaysExistingPath` | リンクの表示文字列として従来の `ExportLocalPath` が残る |
| `BuildExportCompletionMessage_MarkupCharacters_EscapesRichText` | `&`、`<`、`>`、`"` を含む入力でもリンクタグの境界が壊れない |
| `BuildExportCompletionMessage_InvalidFullPath_FallsBackToPlainText` | URI を生成できない入力で例外を投げず、相対パスを含むプレーンログを返す |

既存テストと同じテストファイルへ置き、Packager のパイプライン挙動と完了ログの回帰を
一か所で確認できるようにする。

## 動作確認手順

### 自動確認

1. `python scripts/build_module_docs.py` で HTML 文書を再生成する。
2. `python scripts/verify_round.py` で Unity コンパイル、Console、EditMode、PlayMode 2 往復を確認する。
3. コンパイルがエラー 0・警告 0、EditMode と PlayMode が全数成功することを確認する。

### 人が操作して確認する項目

1. `Tools > SymphonyFrameWork > ExportAssetStoreToolsFolder` から任意のフォルダを出力する。
2. Console の `[AssetStoreToolsPackager] パッケージを出力しました` に、従来の相対パスが
   リンクとして表示されることを確認する。
3. リンクをクリックし、Windows Explorer が開いて、その回の `ExportFullPath` と一致する
   出力フォルダが選択または表示されることを確認する。
4. 出力先のパスに空白または日本語が含まれる場合も同じフォルダを開けることを確認する。

Console 内リンクの実クリックと OS ファイルブラウザーの起動は Unity Test Runner から
自動化できないため、この 1 点だけを人手確認として残す。

## バージョン判断

公開 API を変えない Editor ツールの利便性向上だが、利用者から見える機能追加であるため、
`6.3.2` から `6.4.0` へのマイナー更新とする。

## この Round で触るバージョン関連ファイル

| パス | 更新内容 |
| --- | --- |
| `Assets/SymphonyFrameWork/package.json` | `version` を `6.4.0` へ更新 |
| `Assets/SymphonyFrameWork/Core/SymphonyConstant.cs` | `VERSION` を `6.4.0` へ同期 |
| `Assets/SymphonyFrameWork/README.md` | 「現在のバージョン」を `6.4.0` へ同期 |
| `Assets/SymphonyFrameWork/CHANGELOG.md` | `6.4.0` の機能追加として Issue #113 を記録 |

`Documentation~/Architecture.md` はアセンブリ関係、初期化、公開型の関係が変わらないため更新しない。
`Documentation~/Deprecations.md` は非推奨 API の追加・削除が無いため更新しない。

## 実施レポート

実施日: 2026-08-26 / バージョン: 6.4.0 / PR: [#198](https://github.com/HIBIKI5201/SymphonyFramework/pull/198)

### 実装した内容

| 設計 | 実装 |
| --- | --- |
| ログ組み立ての内部検証点 | `AssetStoreToolsPackagePipelineRunner.BuildExportCompletionMessage(string, string)` を `internal static` で追加。`Export` の末尾はこのメソッドの戻り値を `SymphonyDebugLogger.LogDirect` へ渡すだけになった |
| 絶対 `file:` URI の生成 | `Uri.TryCreate(exportFullPath, UriKind.Absolute, out _)` と `IsFile` の両方を満たす場合だけ `AbsoluteUri` を `href` に使う |
| リッチテキストのエスケープ | `private static string EscapeRichText(string)` を追加。`&` を先に置換して文字参照を二重エスケープしない順序にした。`href` の値と表示文字列の両方へ適用する |
| フォールバック | URI を作れない場合は例外を投げず、`path : <表示パス>` のプレーンテキストを返す |
| テスト | `Tests/Editor/AssetStoreToolsPackagePipelineTests.cs` へ設計書の表どおり4件を追加 |
| 利用者向け文書 | `Documentation~/Modules/AssetStoreToolsPackager.md` の出力先の節へ1段落、`Documentation~/EditorTools.md` の一覧行へ追記 |

`EditorUtility.RevealInFinder`、`Process.Start`、`EditorGUI.hyperLinkClicked` の購読はいずれも追加していない。公開 API の追加・変更は無い。

### 設計から変えた点

無し。ファイル構成、契約、テストの4件、バージョン判断（6.3.2 → 6.4.0 のマイナー更新）はすべて設計どおり。

### 検証結果

`python scripts/verify_round.py`（ワーカーの報告ではなく再実行した実測値）

- コンパイル: エラー0件 / 警告0件
- Console: エラー0件（コンパイル直後に警告4件が出たが、取り直すと0件で確定した）
- EditMode: 479/479 成功（失敗0 / スキップ0）。本 Round で4件増えた
- PlayMode: 21/21 成功（失敗0 / スキップ0）を2往復
- Enter Play Mode Options: PlayMode テストで書き戻された値を 1 → 3 へ復元

`python scripts/release_round.py preflight`: branch / tests / version / changelog / bom / meta / layer / asmdef / playmode / docs すべて OK。

初回の `verify_round.py` は uloop の応答が180秒でタイムアウトしたが、Unity Editor は稼働しており、再実行で全項目通過した。

### 未実施の確認

設計書「動作確認手順」の「人が操作して確認する項目」4件すべてが未実施。

1. `Tools > SymphonyFrameWork > ExportAssetStoreToolsFolder` からの実出力
2. 完了ログに相対パスがリンクとして表示されること
3. リンクのクリックで Explorer がその回の `ExportFullPath` を開くこと
4. 出力先に空白または日本語が含まれる場合も同じフォルダを開けること

3 は設計時点から自動化不可として残した項目だが、1・2・4 も今回は実出力を行っていないため未確認である。文字列生成としては 4 に相当する符号化を EditMode テストで検証している。

### 振り返り

- **設計書が完成していながら実装が未着手の状態で引き継いだ。** 作業ツリーに未追跡の設計書だけがあり、パッケージ側は作業ブランチでクリーンという状態は、ステップ1完了・ステップ2未着手として一意に読めた。チェックポイントを残さない区切りでも状態が判別できたのは、設計書をワークスペース側、実装をパッケージ側へ分けている構成による。
- **`verify_round.py` の初回タイムアウトに対する扱いが手順に無い。** Unity が生きているのに uloop が180秒で応答しない場合、再実行してよいのか、Editor の状態を先に見るべきなのかが書かれていない。今回はプロセスの生存を確認してから再実行して通った。`scripts/verify_round.py` がタイムアウト時に1回だけ自動で再試行するか、失敗メッセージへ「Unity プロセスの生存を確認して再実行する」旨を含めることを提案する。ドキュメントやスクリプトはユーザーの承認を得るまで変更しない。
