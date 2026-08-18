# Debugログファイルの保存先とMCP取得

## 目的

`SymphonyDebugLogger` のファイル出力が、Assets直置きの開発環境では
`Assets/SymphonyFrameWork/Cache/Log.txt` を生成している問題を修正する。
ログパスをEditor共通定数へ集約し、UPM導入時とSymphony Workspaceでの開発時のどちらも
`<Project>/Library/SymphonyFrameWork/Cache/Log.txt`へ出力する。

さらに、uLoopMCPなどの自動化からログを調査できるよう、`SymphonyMcpTools`へ
直近のログをJSONで取得する公開ツールを追加する。

`Library/PackageCache`はUnity Package Managerが管理する読み取り専用のパッケージ内容であり、
ログの所有先にはしない。Issue #193の「Library/PackageCache側」という想定から、
生成物を`Assets`外へ出す目的を満たしつつ、キャッシュの再解決でログを失う配置を避ける。

## Round分割

1 Roundで実施する。共通パス、ログ出力、MCP取得、EditModeテスト、利用者向け文書、
HTML生成物、バージョン更新をまとめても20ファイル以内であり、単独で検証・リリースできる。

## 公開API

### Editor共通定数

`SymphonyFrameWork.Core.EditorSymphonyConstant`へ次を追加する。

```csharp
public const string DEBUG_LOG_FILE_PATH =
    "Library/SymphonyFrameWork/Cache/Log.txt";
```

プロジェクトルートからの相対パスを公開する。`public`とする根拠は、Editor自動化や診断コードが
ログの場所を一意に参照できる汎用Editor定数であり、既存の`FRAMEWORK_PATH`や
`PROJCET_SETTING_FILE_PATH`と同じ役割だからである。

絶対パスへの変換はEditor内部の実装詳細とし、テスト可能な
`internal static string ResolveDebugLogFileAbsolutePath(string assetsPath)`を追加する。

### MCPログ取得

`SymphonyFrameWork.Editor.Debugger.SymphonyMcpTools`へ次を追加する。

```csharp
public static string GetLogFileJson(int maxLines = 200)
```

戻り値は必ず有効なJSON文字列とし、次のフィールドを返す。

| フィールド | 内容 |
| --- | --- |
| `exists` | ログファイルが存在するか |
| `path` | ログファイルの絶対パス |
| `totalLineCount` | ファイル全体の行数 |
| `returnedLineCount` | `lines`へ含めた行数 |
| `truncated` | 古い行を省略したか |
| `lines` | 時系列を維持した直近のログ行 |
| `error` | 読み取りに失敗した場合の理由。成功時は含めない |

`maxLines`は1以上1000以下だけを受け付ける。範囲外では例外を投げず、
`error`を含むJSONを返す。既定200行はMCPレスポンスを過度に大きくせず、
直近の調査に十分な量とするためである。

これは既存の`SymphonyMcpTools`と同じEditor専用の公開診断入口であり、
Runtime公開APIやPlayerビルドには含めない。

## ファイル構成

| パス | 名前空間 | 変更内容 |
| --- | --- | --- |
| `Assets/SymphonyFrameWork/Core/Editor/EditorSymphonyConstant.cs` | `SymphonyFrameWork.Core` | ログ相対パス定数と絶対パス解決を追加 |
| `Assets/SymphonyFrameWork/Core/Internal/CoreLogRelay.cs` | `SymphonyFrameWork.Core` | Editorログ出力先を説明するXMLコメントを更新 |
| `Assets/SymphonyFrameWork/Editor/Debug/SymphonyDebugLogFileWriter.cs` | `SymphonyFrameWork.Editor.Debugger.Logger` | 共通パスへ出力し、MCP読み取り前に呼べる内部フラッシュを提供 |
| `Assets/SymphonyFrameWork/Editor/Debug/SymphonyMcpTools.cs` | `SymphonyFrameWork.Editor.Debugger` | `GetLogFileJson`とファイル読み取り処理を追加 |
| `Assets/SymphonyFrameWork/Tests/Editor/SymphonyDebugLogFileWriterTests.cs` | `SymphonyFrameWork.Tests` | 共通ログパスのEditModeテストを追加 |
| `Assets/SymphonyFrameWork/Tests/Editor/SymphonyMcpToolsTests.cs` | `SymphonyFrameWork.Tests` | MCPログ取得のEditModeテストを追加 |
| `Assets/SymphonyFrameWork/Documentation~/Modules/Debug.md` | なし | 出力先とMCPツールの契約を更新 |
| `Assets/SymphonyFrameWork/Documentation~/AgentVerification.md` | なし | MCPからログを取得する例を追加 |
| `Assets/SymphonyFrameWork/Documentation~/EditorTools.md` | なし | 生成ファイル一覧の出力先を更新 |
| `Assets/SymphonyFrameWork/Documentation~/Html/**` | なし | 上記Markdownから再生成 |
| `Assets/SymphonyFrameWork/Samples~/Runtime/DebuggerSample/Scripts/DebuggerSample_Controller.cs` | `SymphonyFrameWork.Samples.DebuggerSample` | Sample画面に表示するログ出力先を更新 |
| `Assets/SymphonyFrameWork/AGENTS.md` | なし | 生成ログを編集・コミットしない常時ルールのパスを更新 |
| `Assets/SymphonyFrameWork/.gitignore` | なし | パッケージ直下に生成しなくなる`/Cache/`と`/Cache.meta`の専用除外規則を削除 |

既存型を移動せず、名前空間も変更しない。今回の修正は保存先と診断入口に限定し、
既存の`Editor/Debug/`配置を現在の`Internal/`規約へ合わせるリファクタリングは含めない。

`SymphonyMcpTools`はEditor専用でRuntime Sampleから参照できないため、MCP公開APIの利用例は
`Documentation~/AgentVerification.md`へ置く。既存Debugger Sampleはファイル出力先を画面へ表示しているため、
その文言だけを新しいパスへ追従させる。

## 依存方向

`EditorSymphonyConstant`はEditorから共有するパス定義、`SymphonyDebugLogFileWriter`は
Editor用ファイルI/O、`SymphonyMcpTools`は外部自動化向けのEditor診断入口を担当する。
既存の`SymphonyFrameWork.Editor`から`SymphonyFrameWork.Core`への参照だけを使い、
RuntimeやCoreからEditorアセンブリへの逆参照は追加しない。

パス解決は`System.IO.Path`だけを使う純粋処理とし、`Application.dataPath`の読み取りは
`SymphonyEditorOrchestrator`から呼ばれる`SymphonyDebugLogFileWriter.Initialize()`、または
MCPメソッドの呼び出し時に行う。static field initializerからUnity APIやI/Oを開始しない。

MCP読み取りはファイル出力待機バッファを同期的にフラッシュしてから行う。
既存のファイルロックを利用し、書き込み途中の内容を読み取らない。

## アクセス手段の検証

| 経路 | 確認結果 |
| --- | --- |
| Editor初期化 | `SymphonyEditorOrchestrator.InitializeModules()`が`SymphonyDebugLogFileWriter.Initialize()`を呼ぶ |
| プロジェクトルート | Unity Editorでは`Application.dataPath`が`<Project>/Assets`の絶対パスを返すため、その親から公開相対パスの`Library/...`へ到達できる |
| WriterからCore定数 | `SymphonyFrameWork.Editor.asmdef`は`SymphonyFrameWork.Core`を参照済み |
| MCPからWriter | 両方とも`SymphonyFrameWork.Editor`アセンブリにあり、`internal`なフラッシュ処理へ直接到達できる |
| テストから内部型 | `Core/AssemblyInfo.cs`と`Editor/AssemblyInfo.cs`に`InternalsVisibleTo("SymphonyFrameWork.Tests.Editor")`があり、Editorテストasmdefは両アセンブリを参照済み |
| uLoopMCPから公開ツール | 既存文書と実装が、`execute-dynamic-code`からpublic staticな`SymphonyMcpTools`メソッドを呼ぶ経路を採用済み |
| 既存の回避策 | 現在は`SymphonyConstant.GetFrameworkAbsolutePath()`でUPMとAssets直置きを分岐するが、これはログをFramework直下へ置くための処理であり、他のログパス解決やMCPログ取得はない |

## エラー処理

`ResolveDebugLogFileAbsolutePath`は有効な`Application.dataPath`を受け取る前提とする。
通常のEditor実行では発生しない空文字などに独自フォールバックを追加せず、
`Path` APIの例外を不変条件違反として扱う。

ログのディレクトリ作成と追記に失敗した場合は、既存どおりConsoleへ警告を出し、
待機バッファを解放して同じログを再試行しない。

MCP境界では例外を外へ出さない。ファイル未作成は正常状態として`exists: false`と空の`lines`を返し、
パス不正、権限、読み取り競合などは`error`を含む有効なJSONへ変換する。
ログ行そのものは文字列配列としてJSONエスケープし、ログ内の引用符や改行表現でJSONを壊さない。

## 影響範囲

ログ内容、フラッシュ間隔、バッファ上限、Runtime公開API、シリアライズ形式は変わらない。
Editor向け公開APIとして定数とMCPメソッドを追加し、出力先を次のように変更する。

| 導入形態 | 変更前 | 変更後 |
| --- | --- | --- |
| Symphony Workspace / Assets直置き | `<Project>/Assets/SymphonyFrameWork/Cache/Log.txt` | `<Project>/Library/SymphonyFrameWork/Cache/Log.txt` |
| UPM | `<Project>/Library/PackageCache/<package>/Cache/Log.txt`など`resolvedPath`直下 | `<Project>/Library/SymphonyFrameWork/Cache/Log.txt` |

旧`Assets/SymphonyFrameWork/Cache/Log.txt`は自動移行・削除しない。生成物であり、
削除は利用者の既存ログを不可逆に失わせるためである。修正後は新しい追記だけが新パスへ入る。
パッケージ直下の`Cache/`は今後生成しないため、パッケージ固有の`.gitignore`から
`/Cache/`と`/Cache.meta`の除外規則を削除する。新しい出力先はプロジェクト側で既に除外される
`Library/`配下であり、追加の除外規則は不要である。

MCPは既定で直近200行だけを返す。ログ全体が200行以下なら全行を返し、
それを超える場合は`truncated: true`で省略を明示する。

## テストの置き場と種別

EditModeテストで実ファイルI/Oと純粋なパス解決を検証する。ファイルI/Oテストは
`Path.GetTempPath()`配下へ専用ディレクトリを作り、`finally`でそのディレクトリだけを削除する。

| テスト名 | 検証内容と実装方法 |
| --- | --- |
| `ResolveDebugLogFileAbsolutePath_ProjectAssetsPath_ReturnsLibraryLogPath` | 一時プロジェクト相当の`<root>/Assets`を渡し、公開定数をプロジェクトルートへ結合した正規化済み絶対パスと一致することを比較する |
| `ResolveDebugLogFileAbsolutePath_ProjectAssetsPath_DoesNotReturnPathUnderAssets` | 得られたパスがAssetsディレクトリ配下ではないことを、末尾区切りを付けた正規化パスのprefix比較で検証する |
| `ReadLogFileJson_MissingFile_ReturnsEmptyResult` | 存在しない一時パスを内部読み取り処理へ渡し、`exists: false`、件数0、空配列の有効なJSONを検証する |
| `ReadLogFileJson_MoreLinesThanLimit_ReturnsLatestLinesInOrder` | 5行の一時ログを上限3行で読み、末尾3行が元の時系列順で返り、総数5、`truncated: true`になることを検証する |
| `ReadLogFileJson_LimitOutsideRange_ReturnsErrorJson` | 0と1001を渡し、例外を投げず`error`を含む有効なJSONを返すことを検証する |
| `ReadLogFileJson_LogContainingQuotes_ReturnsValidJson` | 引用符を含む一時ログを読み、JSON解析と元文字列の復元が成功することを検証する |

## 動作確認手順

1. `python scripts/verify_round.py`でコンパイル、Console、EditMode、PlayMode 2往復を一括確認する。
2. `SymphonyDebugLogger.LogDirect`を実行し、5秒以上待って
   `<Project>/Library/SymphonyFrameWork/Cache/Log.txt`へ行が追記されることを確認する。
3. 実行前後で`Assets/SymphonyFrameWork/Cache/Log.txt`の更新日時が変わらないことを確認する。
4. uLoopMCPの`execute-dynamic-code`から`SymphonyMcpTools.GetLogFileJson()`を呼び、
   手順2の行、絶対パス、件数を含む有効なJSONが返ることを確認する。
5. `Library`配下のため、新しいログや`.meta`が`git status`へ現れないことを確認する。

GUI操作は不要であり、すべて自動コードまたはシェルで実測する。

## バージョン判断

Editor向け公開定数と公開メソッドの追加は、既存APIを壊さない機能追加として
`6.2.1`から`6.3.0`へのマイナー更新とする。Issue #193のログ出力先修正は
`6.3.0`から`6.3.1`へのパッチ更新として分け、Round完了時の配布版を`6.3.1`とする。

## この Round で触るバージョン関連ファイル

| パス | 更新内容 |
| --- | --- |
| `Assets/SymphonyFrameWork/package.json` | `version`を最終版`6.3.1`へ更新 |
| `Assets/SymphonyFrameWork/Core/SymphonyConstant.cs` | `VERSION`を最終版`6.3.1`へ同期 |
| `Assets/SymphonyFrameWork/README.md` | 「現在のバージョン」を最終版`6.3.1`へ更新。機能索引は既にDebug文書を指すため変更しない |
| `Assets/SymphonyFrameWork/CHANGELOG.md` | `6.3.0`へMCP公開API追加、`6.3.1`へIssue #193の修正を分けて追加 |

`Documentation~/Architecture.md`はアセンブリ関係、初期化、公開型の関係が変わらないため更新しない。
`AGENTS.md`はログ生成物に関する常時ルールのパスだけを更新する。
`Documentation~/AgentUsage.md`は導線やモジュール索引が変わらないため更新しない。
