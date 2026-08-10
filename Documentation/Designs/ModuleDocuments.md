# モジュール別ドキュメント

Issue: [#101 Runtimeモジュールごとにドキュメントを分離する](https://github.com/HIBIKI5201/SymphonyFramework/issues/101)

## 目的

現在、利用者向けの説明がほぼ `README.md`（348行）と `Documentation~/EditorTools.md`（458行）へ集中している。1つのモジュールについて知りたいだけでも、AIは巨大なファイルを丸ごと読むことになり、人は目次を辿って該当節を探すことになる。

モジュール単位で1ファイルへ分け、`README.md` と `EditorTools.md` を索引へ縮める。あわせて、Editorから該当モジュールのドキュメントをブラウザで開けるようにする。

### 現状の重複

同じモジュールの説明が、目的別に3か所へ分かれている。

| ファイル | 内容 |
| --- | --- |
| `README.md` | クイックスタート（コード例） |
| `Documentation~/AgentUsage.md` | 実装時に誤りやすい判断（AI向け） |
| `Documentation~/EditorTools.md` | Editorの入口、設定の保存先、注意点 |

「Scene Loader を使う」ために3ファイルを開く必要がある。これを1ファイルへ集約する。

### 形式の決定

**Markdown を正本とし、HTMLは生成物とする。** Issueのコメントでは手書きHTMLが案として挙がっていたが、Issue本文の目的である「AIが参照するときのコンテキスト効率」はHTMLタグの分だけ悪化し、`README.md` や `AGENTS.md` からの相対リンクも Markdown 同士のようには繋がらない。

- 正本: `Documentation~/Modules/<Module>.md`
- 生成物: `Documentation~/Html/**/*.html`（`scripts/build_module_docs.py` が生成し、リポジトリへコミットする）
- 同期検証: `scripts/build_module_docs.py --check`。`release_round.py preflight` から呼ぶ

`.claude/skills` のロケーターを `sync_agent_skill_locators.py` で生成・検証しているのと同じ形にする。生成物をコミットするのは、利用側プロジェクトへ導入された状態でPythonを実行させないためである。

## Round 分割

5 Round に分ける。Round 1〜3 は Markdown と生成スクリプトだけを触り、C#を変更しない。Round 4〜5 で Editor からの入口を追加する。

| Round | 内容 | 触るファイル数 | バージョン |
| --- | --- | --- | --- |
| 1 | Runtime モジュール8本の分離。`README.md` / `AgentUsage.md` / `Architecture.md` を索引化 | 8新規 + 5変更 | `3.8.5`（パッチ） |
| 2 | Editor モジュール3本の分離。`EditorTools.md` を索引化 | 3新規 + 4変更 | `3.8.6`（パッチ） |
| 3 | `scripts/build_module_docs.py` と生成HTML、preflight への組み込み | 1新規（スクリプト）+ 19生成 + 4変更 | `3.8.7`（パッチ） |
| 4 | ドキュメントを開く公開API、Window メニュー、Project Settings のボタン | 3新規 + 6変更 | `3.9.0`（マイナー） |
| 5 | Symphony Administrator 各パネルのボタン | 0新規 + 12変更 | `3.9.1`（パッチ） |

Round 1 と 2 の順序は入れ替えられない。Round 2 の `EditorTools.md` 索引化が、Round 1 で作る Runtime モジュール文書（Editor節を含む）を参照するため。Round 3 は Round 1・2 の Markdown が確定してから走らせる。Round 4 は Round 3 の生成HTMLが存在することを前提にする。

**同じファイルを複数Roundで触る箇所**:

| ファイル | Round 1 | Round 2 | Round 3 | Round 4 | Round 5 |
| --- | --- | --- | --- | --- | --- |
| `README.md` | クイックスタート節を索引へ置換 | `Editor・デバッグ支援` 節を索引へ置換 | 「ドキュメント」節へHTMLの説明を追加 | 「現在のバージョン」のみ | 「現在のバージョン」のみ |
| `AGENTS.md`（パッケージ） | §0の表をモジュール文書へ差し替え | §0の表のEditor行 | 触らない | §0の表へEditorからの開き方を追加 | 触らない |
| `Documentation~/EditorTools.md` | 触らない | 全面再編 | 触らない | ドキュメント表示機能の節を追加 | Administrator節へボタンを追記 |
| `Documentation~/Architecture.md` | サブシステム内部図5点をモジュール文書へ移送 | 触らない | 触らない | 触らない | 触らない |
| `Documentation/CONTRIBUTING.md`（ワークスペース） | §6の表 | §6の表 | HTML再生成の手順 | §6の表 | 触らない |

---

## モジュールの分け方

`Documentation~/Modules/` 直下へ、Runtime 8本 + Editor 3本の計11本を置く。Runtime のサブシステムは `Runtime/System/` のフォルダに1対1で対応させる。

### Round 1: Runtime

| ファイル | 集約する内容 |
| --- | --- |
| `ServiceLocator.md` | `ServiceLocator` / `ServiceInjector` / `ServiceLocateComponent` / `IInjectable` / `LocateTypeEnum` / `ServiceRegistrationInfo`、Administrator の Service Locate パネル、`Project Settings > SymphonyFrameWork` のログ設定 |
| `SceneLoader.md` | `SceneLoader` / `SceneLoadRequest` / `SceneLoadInfo` / `IInitializeAsync`、`SceneLoadConfig`、Administrator の Scene Load パネル |
| `SaveDataSystem.md` | `SaveStore` / `SaveDataContent` / `SaveDataLoaderStrategy`、`Project Settings > SymphonyFrameWork > Save System`、Administrator の Save Data パネル |
| `AudioManager.md` | `AudioManager`、`AudioConfig` |
| `PauseManager.md` | `PauseManager` / `IPausable`、Administrator の Pause パネル |
| `Debug.md` | `SymphonyDebugHUD` / `SymphonyDebugLogger` / `SymphonyStopWatch` / `LogKindEnum`、ログのファイル出力、`SymphonyMcpTools` |
| `Utility.md` | `SymphonyAwaitable` / `SymphonyTween` / `SymphonyStringUtil` / `SymphonyComponentUtil` / `SymphonyVisualElement` |
| `InspectorAttributes.md` | `[ReadOnly]` / `[DisplayText]` / `[TagSelector]` / `[SceneNameSelector]` / `[SubclassSelector]` と対応するDrawer |

### Round 2: Editor

| ファイル | 集約する内容 |
| --- | --- |
| `AutoEnumGenerator.md` | enum生成、`AutoEnumGeneratorConfig`、変更検知、Administrator の Auto Enum Generator パネル |
| `AssetStoreToolsPackager.md` | Packager設定、Export / Import タブ、出力パイプライン、バージョンログ、出力全般の注意点 |
| `ProjectStructureTools.md` | `FolderGenerator` / `AssemblyGenerator` / `SymphonyPackageLoader` |

`EditorTools.md` には、単一モジュールへ属さない横断的な内容だけを残す。索引表、設定ファイルの置き場、Symphony Administrator 全体、アセット保護、設定アセットの自動生成、Editorの初期化。

### 各モジュール文書の構成

全11本で同じ見出し構成にする。存在しない節は省略せず「なし」と書かず、節ごと落とす。

```markdown
# <モジュール名>

1〜3行の概要。何を解決するか。

## 入口

namespace、主な型、メニューパス、設定の保存先の表。

## クイックスタート

`README.md` から移したコード例。

## 実装時の注意

`AgentUsage.md` から移した、誤りやすい判断。

## Editor機能

`EditorTools.md` から移した、このモジュールのEditor入口と設定。

## 内部構造

`Architecture.md` から移したサブシステム内部のmermaid図と、その説明。

## 関連

他モジュール文書、`Deprecations.md`、`CHANGELOG.md` へのリンク。
```

`Architecture.md` にはアセンブリ構成、ディレクトリ構成、起動と終了、公開Facade全体のclass図を残す。サブシステム個別の内部flowchart（Scene Load / Service Locate / Save Data / Pause / Audio の5点）は各モジュール文書の「内部構造」へ移す。

---

## 公開API

Round 1〜3 では追加・変更・削除なし。Round 4 で次を追加する。

```csharp
namespace SymphonyFrameWork.Editor
{
    /// <summary> Frameworkのドキュメントページを表す。 </summary>
    public enum SymphonyDocumentPageEnum
    {
        Index,
        ServiceLocator,
        SceneLoader,
        SaveDataSystem,
        AudioManager,
        PauseManager,
        Debug,
        Utility,
        InspectorAttributes,
        AutoEnumGenerator,
        AssetStoreToolsPackager,
        ProjectStructureTools,
        EditorTools,
    }

    /// <summary> Frameworkのドキュメントをブラウザで開く。 </summary>
    public static class SymphonyDocumentation
    {
        public static void Open(SymphonyDocumentPageEnum page);
    }
}
```

`public` にする根拠: `DesignPhilosophy.md` の「公開範囲」における **Adaptor層の公開エントリポイント**にあたる。既存の `SymphonyAdministrator`（`public sealed class` + `MenuItem`）と同じ扱いで、Editor機能の入口を1つの型へ集約する。`SymphonyDocumentPageEnum` は「公開エントリポイントの引数として境界を越えるEnum」に該当する。

enum の各値はモジュール文書のファイル名と1対1で対応させる。文書を増やすと enum が増えるため、その回はマイナー更新になる。

**実装時に `EditorTools` を追加した。** 当初はモジュール文書11本と索引だけを想定していたが、`Project Settings > SymphonyFrameWork` から開く先が `EditorTools.md`（アセット保護、設定アセットの自動生成、Editorの初期化）であり、モジュール文書のどれとも対応しないため。

パス解決とURL組み立ては Unity API へ触れない `internal static class SymphonyDocumentPathResolver` へ切り出し、EditModeテストの対象にする。

## ファイル構成

### Round 1

- 新規: `Assets/SymphonyFrameWork/Documentation~/Modules/{ServiceLocator,SceneLoader,SaveDataSystem,AudioManager,PauseManager,Debug,Utility,InspectorAttributes}.md`
- 変更: `Assets/SymphonyFrameWork/README.md`（「機能ごとの使い方」を索引表へ置換）
- 変更: `Assets/SymphonyFrameWork/AGENTS.md`（§0の参照先表）
- 変更: `Assets/SymphonyFrameWork/Documentation~/AgentUsage.md`（共通の前提と索引だけ残す）
- 変更: `Assets/SymphonyFrameWork/Documentation~/Architecture.md`（サブシステム内部図5点を移送）
- 変更: `Assets/SymphonyFrameWork/CHANGELOG.md` / `package.json`

### Round 2

- 新規: `Assets/SymphonyFrameWork/Documentation~/Modules/{AutoEnumGenerator,AssetStoreToolsPackager,ProjectStructureTools}.md`
- 変更: `Assets/SymphonyFrameWork/Documentation~/EditorTools.md`（横断的な内容だけ残す）
- 変更: `Assets/SymphonyFrameWork/README.md` / `AGENTS.md` / `CHANGELOG.md` / `package.json`

### Round 3

- 新規: `scripts/build_module_docs.py`（ワークスペース側）
- 新規: `Assets/SymphonyFrameWork/Documentation~/Html/**/*.html`（生成物、19ファイル）
- 変更: `scripts/release_round.py`（`run_preflight` へ `check_docs_html_sync` を追加）
- 変更: `Documentation/CONTRIBUTING.md` / `AGENTS.md`（ワークスペース側、再生成手順）
- 変更: `Assets/SymphonyFrameWork/README.md`（「ドキュメント」節）/ `CHANGELOG.md` / `package.json`

### Round 4

- 新規: `Assets/SymphonyFrameWork/Editor/Documentation/SymphonyDocumentation.cs`（namespace `SymphonyFrameWork.Editor`）
- 新規: `Assets/SymphonyFrameWork/Editor/Documentation/SymphonyDocumentPageEnum.cs`（同上）
- 新規: `Assets/SymphonyFrameWork/Editor/Documentation/Internal/SymphonyDocumentPathResolver.cs`（同上、`internal`）
- 新規: `Assets/SymphonyFrameWork/Tests/Editor/SymphonyDocumentPathResolverTests.cs`（namespace `SymphonyFrameWork.Tests`）
- 変更: `Editor/SettingProvider/{SymphonySettingProvider,SaveDataSettingProvider,AssetStoreToolsPackagerProvider}.cs`（ボタン追加）
- 変更: `Documentation~/EditorTools.md` / `README.md` / `AGENTS.md` / `CHANGELOG.md` / `package.json`

### Round 5

- 変更: `Editor/Administrator/UITK/UXML/{Pause,ServiceLocate,SceneLoad,SaveData,AutoEnumGenerator}Window.uxml`（ボタン要素の追加）
- 変更: `Editor/Administrator/UITK/CS/{Pause,ServiceLocate,SceneLoad,SaveData,AutoEnumGenerator}Window.cs`（クリックハンドラ）
- 変更: `Documentation~/EditorTools.md` / `CHANGELOG.md` / `package.json` / `README.md`

`Documentation~` は `~` 終わりのため Unity がインポートせず、`.meta` は不要。新規C#ファイルの `.meta` は Unity に生成させる。

## 依存方向

`Editor -> Runtime -> Core` を維持する。Round 4 で追加する型はすべて `SymphonyFrameWork.Editor` に属し、Runtime / Core からは参照しない。

`SymphonyDocumentPathResolver` は `System.IO.Path` と文字列操作だけを使い、`UnityEngine` / `UnityEditor` を参照しない。実ファイルパスの取得だけを呼び出し側（`SymphonyDocumentation`）が担当し、Resolver へ文字列として渡す。

### 検証済みのアクセス手段

- **Frameworkの実配置パス**: `SymphonyConstant.GetFrameworkAbsolutePath()` は `public static`、`SymphonyFrameWork.Core` アセンブリ、`#if UNITY_EDITOR` 内にある。`SymphonyFrameWork.Editor.asmdef` の `references` は `68a532f4...`（`SymphonyFrameWork`）と `b4e97826...`（`SymphonyFrameWork.Core`）で、Core を参照済みのため到達できる。UPM導入時は `PackageInfo.resolvedPath`、Assets直置き時は `Application.dataPath` 配下を返す。
- **`Documentation~` の実体**: `~` 終わりのフォルダは Unity がインポートしないだけで、ディスク上には存在する。既存の `Editor/PackageLoader/PackageList.txt` と `Cache/Log.txt` が同じ前提（Frameworkルート直下の実ファイル）で動いている。
- **ブラウザで開く手段**: `Application.OpenURL` を使う。既存コードに `OpenURL` / `Help.BrowseURL` / `documentationUrl` の利用は無く、回避策も存在しない（`grep` で確認済み）。
- **バージョン固定URLは作れない**: パッケージリポジトリにタグが1つも無い（`git ls-remote --tags origin` が空）。したがってフォールバック先のGitHub URLはバージョンではなく `main` を指す。

## エラー処理

`SymphonyDocumentation.Open` は例外を投げない。Editorの補助機能であり、ドキュメントが開けないことで利用側の作業を止めないため。

| 状況 | 扱い |
| --- | --- |
| 生成HTMLが存在する | `Application.OpenURL("file:///" + 絶対パス)` |
| 生成HTMLが無い（Frameworkルートを解決できない、`Documentation~` ごと除外された配布物など） | `Debug.LogWarning` を出し、GitHub上の該当Markdownへフォールバックする |
| enum に対応するファイル名が無い（将来の追加漏れ） | `Debug.LogError` を出して何も開かない |

`SymphonyDocumentPathResolver` は失敗を戻り値で表す（`TryResolveLocalPath(string frameworkRoot, SymphonyDocumentPageEnum page, out string path)`）。不変条件違反ではないため例外にしない。

`build_module_docs.py --check` は差分があれば標準エラーへ一覧を出し、終了コード1を返す。`sync_agent_skill_locators.py --check` と同じ形にする。

## HTML生成の仕様（Round 3）

対象は次のMarkdownで、出力は `Documentation~/Html/` 配下へソースの相対構造を保って書く。

| 入力 | 出力 |
| --- | --- |
| `README.md` | `Documentation~/Html/README.html` |
| `CHANGELOG.md` | `Documentation~/Html/CHANGELOG.html` |
| `Documentation~/*.md`（5本） | `Documentation~/Html/*.html` |
| `Documentation~/Modules/*.md`（11本） | `Documentation~/Html/Modules/*.html` |

加えて `Documentation~/Html/index.html` を生成する。全モジュールへのリンク一覧で、`SymphonyDocumentPageEnum.Index` の開き先になる。

- 対応する記法: 見出し、段落、順序付き／なしリスト、テーブル、フェンス付きコードブロック、インラインコード、強調、リンク、水平線、引用。**これはこのリポジトリの既存ドキュメントが実際に使っている記法の集合である。**
- **未対応の記法を見つけたらエラーで停止する。** 黙って素通しすると、生成HTMLだけが静かに壊れる。
- mermaid ブロックはレンダリングせず、コードブロックとして出力する。オフラインで開く前提のため外部スクリプトを読み込まない。既知の制限として `CONTRIBUTING.md` へ明記する。
- CSSは各HTMLへインラインで埋め込む。`file://` で開いたときに外部ファイルの解決へ依存しないため。ライトとダークの両方を `prefers-color-scheme` で扱う。
- 相対リンクの `.md` は、出力側の対応する `.html` へ書き換える。出力対象外へのリンク（`LICENSE.txt` など）と絶対URLはそのまま残す。
- 生成物の先頭へ「このファイルは生成物である。正本は `<相対パス>` である」というHTMLコメントを入れる。

## 影響範囲

- 公開API: Round 4 でのみ追加。既存シグネチャとシリアライズ形式は変えない。
- **既存ドキュメントへの外部リンクは壊れる。** `README.md` のアンカー（`#service-locator` など）を参照している外部の文書やブックマークは、対応するモジュール文書へ移る。パッケージ内からの参照は同じ変更内で全て張り替える。
- `AGENTS.md` の §0 が指す先が変わるため、AIエージェントの読み込み経路が変わる。1モジュールにつき1ファイルで完結するようになる。
- 生成HTMLがリポジトリへ入るため、パッケージのサイズが増える。11本+8本のHTMLで数百KBの見込み。
- Round 3 以降、**Markdownを直したらHTMLの再生成が必要になる。** 忘れると `release_round.py preflight` が落ちる。

## テストの置き場と種別

### Round 1〜3

自動テストを書かない。Markdownの再編とPythonスクリプトであり、Unityのテストアセンブリから検証する対象が無いため。代わりに次で担保する。

- `build_module_docs.py --check` を `release_round.py preflight` へ組み込み、正本と生成物の乖離を機械的に検出する（Round 3）
- リンク切れの検出をスクリプトへ含める。`Documentation~` と `README.md` の相対リンクを解決し、存在しない先を指していたらエラーにする

### Round 4

EditMode テストを `Assets/SymphonyFrameWork/Tests/Editor/SymphonyDocumentPathResolverTests.cs` へ追加する。メソッド名は既存の `対象_条件_期待` 形式に合わせる。

- `TryResolveLocalPath_KnownPage_ReturnsHtmlUnderDocumentationFolder`
  - 一時ディレクトリへ `Documentation~/Html/Modules/SceneLoader.html` を作り、Resolver がそのパスを返すことを検証する。`Path.GetFullPath` で正規化してから比較する。
- `TryResolveLocalPath_MissingFile_ReturnsFalse`
  - ファイルを作らない一時ディレクトリを渡し、`false` と `null` が返ることを検証する。
- `ResolveFallbackUrl_KnownPage_PointsToMarkdownOnMain`
  - GitHub URL が `blob/main/Documentation~/Modules/<Name>.md` の形になることを文字列比較で検証する。
- `AllPages_HaveDocumentFileName`
  - `Enum.GetValues(typeof(SymphonyDocumentPageEnum))` を回し、全要素がファイル名へ対応することを検証する。**enum を増やしてマッピングを足し忘れた場合をここで落とす。**

`Application.OpenURL` の呼び出し自体は検証しない。ブラウザを起動する副作用があり、自動テストで確認できないため。

### Round 5

**自動テストを書かない。** EditorWindow のボタンを押す手段が無いことは既に実測済みである（`references/design-doc.md` 参照）。ボタンのクリックハンドラが `SymphonyDocumentation.Open` を呼ぶだけであり、開く側の判定は Round 4 のテストで担保している。

`SymphonyWindow.uxml` から5パネルが生成できることは既存の `SymphonyAdministratorUxmlTests` が検証しており、UXMLへ要素を足して壊した場合はこのテストが落ちる。

## 動作確認手順

### Round 1〜2（自動）

1. `Documentation~` と `README.md` の相対リンクを走査し、リンク切れが無いことを確認する。
2. `git diff --check` を実行する。
3. Unity Console をクリアして再コンパイルし、エラー0・警告0を確認する。C#は変更しないが、`Documentation~` へのファイル追加がインポートを起こさないことを確認する意味がある。

### Round 1〜2（人の確認）

1. `Documentation~/Modules/` の各文書を開き、`README.md` / `AgentUsage.md` / `EditorTools.md` / `Architecture.md` から移した内容に欠落が無いことを確認する。**移送前後で節を突き合わせる。**
2. `README.md` と `EditorTools.md` が索引として成立していること（どのモジュールの説明がどこにあるか1画面で分かること）を確認する。

### Round 3（自動）

1. `python scripts/build_module_docs.py` を実行し、生成物が出ることを確認する。
2. 続けて `--check` を実行し、終了コード0を確認する。
3. Markdownを1行だけ変更して `--check` を実行し、終了コード1と差分の一覧が出ることを確認する。変更は元へ戻す。
4. `python scripts/release_round.py preflight` を実行し、HTML同期の検証が走ることを確認する。

### Round 3（人の確認）

1. 生成された `index.html` と `Modules/SceneLoader.html` をブラウザで開き、見出し、テーブル、コードブロックが読める形で表示されることを確認する。
2. HTML内の相対リンクを辿り、`.html` 同士で遷移できることを確認する。
3. ライトとダークの両方で読めることを確認する。

### Round 4（自動）

1. `uloop-clear-console` → `uloop-compile` でエラー0・警告0。
2. EditMode / PlayMode テストを全数実行し、全件成功。
3. Runtime / Core から `UnityEditor` への参照が増えていないことを検索で確認する。
4. 新規 `.cs` に `.meta` が対で揃っていることを確認する。
5. Play Mode の開始・終了を2回繰り返し、Consoleに新しいエラー・警告が出ないことを確認する。

### Round 4（人の確認）

1. `Window > SymphonyFrameWork > Documentation` を選び、`index.html` がブラウザで開くことを確認する。
2. `Project Settings > SymphonyFrameWork` / `> Save System` / `> Asset Store Tools Packager` の各画面のボタンから、対応するモジュール文書が開くことを確認する。
3. `Documentation~/Html/` をリネームして退避し、同じ操作でGitHubのMarkdownが開き、Consoleへ警告が出ることを確認する。確認後に戻す。

### Round 5（人の確認）

1. `Symphony Administrator` を開き、5パネルすべてのボタンから対応するモジュール文書が開くことを確認する。
2. **ウィンドウを開いたまま Play Mode を開始・終了し、ボタンが機能し続けることを確認する。** パネルは `Dispose` で購読を解除するため、再構築後にハンドラが外れていないかをここで見る。

## バージョン判断

| Round | バージョン | 理由 |
| --- | --- | --- |
| 1 | `3.8.5` パッチ | ドキュメントのみ。公開APIとシリアライズ形式は不変 |
| 2 | `3.8.6` パッチ | 同上 |
| 3 | `3.8.7` パッチ | 生成物とワークスペース側スクリプトの追加。公開APIは不変 |
| 4 | `3.9.0` マイナー | `SymphonyDocumentation` と `SymphonyDocumentPageEnum` の後方互換な公開API追加 |
| 5 | `3.9.1` パッチ | 既存Editorウィンドウへのボタン追加。公開APIは不変 |

## この Round で触るバージョン関連ファイル

各Roundで共通して次を更新する。

- `Assets/SymphonyFrameWork/package.json`: `version`
- `Assets/SymphonyFrameWork/CHANGELOG.md`: 見出しと節（Round 1〜3 は `Change`、Round 4 は `Add`、Round 5 は `Add`）
- `Assets/SymphonyFrameWork/README.md`: 「現在のバージョン」

`README.md` はどのRoundでも「現在のバージョン」行を触るが、本文の書き換えはRound 1〜3に限る。Round 4・5 はバージョン行だけを変更する。
