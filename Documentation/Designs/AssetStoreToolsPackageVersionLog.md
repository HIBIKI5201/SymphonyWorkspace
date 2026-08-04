# Asset Store Tools Packager のバージョンログと差分インポート

Issue: [#119](https://github.com/HIBIKI5201/SymphonyFramework/issues/119)

## 目的

`AssetStoreToolsPackager` が出力した `.unitypackage` を別プロジェクトへ取り込むとき、**どのパッケージが前回から変わったのかを機械的に判定する手段が無い**。結果として、変更の無いパッケージまで毎回インポートすることになる。Asset Store 由来のディレクトリはネイティブプラグインや大量のテクスチャを含むため、1つのインポートで数十秒かかることも珍しくない。

現状で「変わったか」を知る手段は、出力日時とファイルサイズを目で見るしかない。日時は「出力した日時」であって「中身が変わった日時」ではないため判断材料にならない。

そこで **パッケージ単位のリビジョン番号**を導入する。

| 置き場 | ファイル | 意味 | 誰が書くか |
| --- | --- | --- | --- |
| AST ルート直下 | `PackageVersions.json` | このプロジェクトでの各ディレクトリの現在リビジョン | `AssetPostprocessor` が変更を検知して加算 |
| AST の各ディレクトリ直下 | `ExportedVersion.json` | **そのパッケージが出力された時点**のリビジョン | パッケージ出力時に生成し、パッケージへ同梱する |
| 出力先フォルダ | `PackageManifest.json` | その回に出力したパッケージ名とリビジョンの一覧 | パッケージ出力時に生成する |

2種類のログを分けるのが設計の中心である。**ルートログは「編集の履歴」、ディレクトリ内ログは「出荷時点のスナップショット」**であり、意味が違う。ディレクトリ内ログはパッケージへ同梱されるため、インポート先プロジェクトでは「今そこに入っているパッケージのリビジョン」を表す。これと出力先のマニフェストを突き合わせれば、インポートが必要なパッケージだけを選び出せる。

Issue のコメントにあるとおり、差分インポートには専用の操作面が要る。ウィンドウを増やさず、現在の Packager ウィンドウを **Export / Import の2タブ**にする。

### 差分インポートは Combine 出力と両立しない

**統合パッケージ（`PackageModeEnum.Combine`）は全ディレクトリを1つの `.unitypackage` へまとめるため、ディレクトリ単位で取り出せない。** 差分インポートの単位にならない。

つまり本 Issue の目的を達成した時点で、実用上の出力形式は個別出力（`Singles`）だけになる。`Combine` を残すと「差分インポートできない出力形式」が選択肢として残り続け、利用者がそれを選ぶと Import タブが機能しない。**`Combine` と `PackageModeEnum` は非推奨にする。**

**ただし削除はこの Issue では行わない。** `PackageModeEnum` は `public` であり、削除はメジャー更新（4.0.0）を要求する。Editor 専用ツール1つの整理のためにメジャーを消費せず、他の破壊的変更とまとめて出すほうがよい。`[Obsolete]` を付けた状態で置き、削除は次のメジャーへ繰り越す。

### 削除予定を記録する場所が無い

`[Obsolete]` を付けて放置すると、**それが「いつ・何と引き換えに消えるのか」がどこにも残らない。** 現在の正本は CHANGELOG である（`Documentation~/AgentUsage.md` が「非推奨APIの期限と移行先は CHANGELOG.md を正本とします」と明記している）。しかし CHANGELOG は時系列の記録であり、**「今なお非推奨で、まだ消えていないもの」を一覧できない。** 非推奨化した版まで遡って読む必要があり、その後で削除されたのかどうかも分からない。

実際、パッケージには既に7件の `[Obsolete]` メンバーが残っている。着手時に全数を検索して確認した。

| 対象 | 場所 | 移行先 |
| --- | --- | --- |
| `SymphonyDebugLogger.DirectLog(string, LogKindEnum)` | `Runtime/Debug/SymphonyDebugLogger.cs:202` | `LogDirect` |
| `SymphonyDebugLogger.TextLog(LogKindEnum, bool)` | `Runtime/Debug/SymphonyDebugLogger.cs:215` | `LogText` |
| `SymphonyDebugLogger.CheckComponentNull<T>(T)` | `Runtime/Debug/SymphonyDebugLogger.cs:229` | `LogAndCheckComponentNull` |
| `SymphonyDebugLogger.IsComponentNotNull<T>(T)` | `Runtime/Debug/SymphonyDebugLogger.cs:238` | `LogAndCheckComponentNull` |
| `SymphonyTween.TweeningLerp<T>(...)` | `Runtime/Utility/SymphonyTween.cs:117` | `Tweening` |
| `SymphonyTween.TweeningCurve<T>(...)` | `Runtime/Utility/SymphonyTween.cs:182` | `Tweening` |
| `EditorSymphonyConstant.ASSET_STORE_TOOLS_IGNORE_FILE` | `Core/Editor/EditorSymphonyConstant.cs:51` | `ASSET_STORE_TOOLS_CONFIG_FILE_NAME` |

これらが**いつ消えるのかは、どこにも書かれていない。** `ASSET_STORE_TOOLS_IGNORE_FILE` だけは CHANGELOG 3.1.0 に「次のメジャー更新で削除します」とあるが、他の6件には期限の記載が無い。`Combine` を同じ状態で追加すると8件目になる。

そこで `Documentation~/Deprecations.md` を新設し、**非推奨APIと削除予定の一覧の正本**にする。`Combine` の削除予定もここへ書き、この Issue はそこで閉じる。

### Editor 機能のドキュメントが無い

`README.md` の `## Editor・デバッグ支援` は10行の箇条書きで、**`AssetStoreToolsPackager` は1行も書かれていない。** 同様に `SymphonyPackageLoader`、`SymphonyAssetProtector`、`SymphonyMcpTools`、Project Settings の各 Setting Provider も未記載である。

Runtime の各サブシステムには README のクイックスタートと `Documentation~/AgentUsage.md` があるのに、Editor 機能には対応する正本が無い。本 Issue で Packager の挙動が変わり、出力物が増え、最終的に公開APIが減る。**書き足す先が存在しない状態のまま挙動を変えると、変更が誰にも伝わらない。**

そこで `Documentation~/EditorTools.md` を新設し、Editor 機能の正本にする。あわせて、**記載の無いモジュールを見つけたら追記する**という常時ルールを開発側の `AGENTS.md` と `CONTRIBUTING.md` へ入れる。

## Round 分割

3つの Round に分ける。各 Round は単独で検証でき、単独でリリースできる。

| Round | 内容 | バージョン |
| --- | --- | --- |
| **1** | `Documentation~/EditorTools.md` と `Documentation~/Deprecations.md` の新設、記載漏れを埋めさせる常時ルールの追加 | 3.2.1 |
| **2** | バージョンログの基盤。ルートログ、`AssetPostprocessor` によるリビジョン加算、出力時のディレクトリ内ログとマニフェストの生成 | 3.3.0 |
| **3** | Packager ウィンドウの2タブ化と差分インポート。`PackageModeEnum.Combine` を `[Obsolete]` にし、削除予定を `Deprecations.md` へ記録する | 3.4.0 |

順序の理由:

- **Round 1 が先。** 以降の Round が挙動を変えるたびに書き足す先を先に用意する。ここを後回しにすると、Round 2〜3 の変更が README の箇条書き1行に押し込まれるか、どこにも書かれない。`Deprecations.md` も同じで、Round 3 で `[Obsolete]` を付ける時点で書き込む先が要る
- **Round 2 → Round 3。** Round 2 が「比較できる情報を作る」、Round 3 が「その情報で比較して選ぶ」。入れ替えられない

**`Combine` の削除（4.0.0）はこの Issue の範囲外とする。** Round 3 で `[Obsolete]` を付け、`Deprecations.md` へ「次のメジャー更新で削除」と記録したところで Issue #119 を閉じる。実際の削除は、他の破壊的変更とまとめて次のメジャー更新で行う。

前の Round がコミットまで終わってから次へ進む。**Round 2 以降の内容は着手時にもう一度コードを見て確認する。** ここに書いてあるのは Round 1 着手時点の理解であって、事実の確認ではない。

## 前提の確認

着手時にコードで確認した事実を記録する。

| 前提 | 確認結果 |
| --- | --- |
| `AssetStoreToolsPackagerData.AssetStoreToolsPath` を Packager 以外から読めるか | **読める。** `public static` プロパティ（`AssetStoreToolsPackagerData.cs:13`）で、Editor アセンブリ内から到達できる |
| `SymphonyEditorOrchestrator` へ新しいモジュールを登録できるか | **できる。** `internal static`（`SymphonyEditorOrchestrator.cs:16`）で、`Editor/` の asmdef は `SymphonyFrameWork.Editor` の1つだけ。同一アセンブリ |
| `AssetPostprocessor` から Orchestrator へ通知する既存の型があるか | **ある。** `TagsAndLayersPostProcessor` と `SymphonyAssetProtector` が `internal static event Action OnHostChangesPending` を持ち、Orchestrator が `SubscribeHostCallbacks` で購読している（`SymphonyEditorOrchestrator.cs:39-42`） |
| Refresh 要否を Orchestrator へ返す既存の形があるか | **ある。** `AutoEnumGenerator.ConsumeAssetChanges()` が `bool` を返し、`_requiresAssetDatabaseRefresh` へ OR される（`SymphonyEditorOrchestrator.cs:200`） |
| Editor アセンブリから Newtonsoft.Json を使えるか | **使える。** `AssetStoreToolsPackagerConfigStore` が `#if` 無しで使用しており、`package.json` の hard dependency でもある |
| テストアセンブリから Editor の `internal` を検証できるか | **できる。** `Editor/AssemblyInfo.cs` が `SymphonyFrameWork.Tests.Editor` へ `InternalsVisibleTo` を与えている。既存の `AssetStoreToolsPackagerConfigTests` が `internal` な `AssetStoreToolsPackagerConfig` を直接使っている |
| `README.md` / `AGENTS.md` に Packager の記述があるか | **無い。** 両ファイルを検索して0件。だから Round 1 でドキュメントを新設する |
| `PackageModeEnum` の参照箇所はどこか | **パッケージ内は Packager フォルダの3ファイルだけ**（`AssetStoreToolsPackager.cs`、`AssetStoreToolsPackagePlan.cs`、`AssetStoreToolsPackageWindow.cs`）。ワークスペース側は `Documentation/DesignPhilosophy.md:346` が enum 命名の実例として挙げている。将来の削除時はこの行も直す必要がある。**`Deprecations.md` の該当エントリへこの参照箇所を書き残す** |
| パッケージに残っている `[Obsolete]` は何件か | **7件。** `SymphonyDebugLogger` に4件、`SymphonyTween` に2件、`EditorSymphonyConstant` に1件。削除期限が書かれているのは `ASSET_STORE_TOOLS_IGNORE_FILE`（CHANGELOG 3.1.0）だけ |
| `.meta` は `OnPostprocessAllAssets` の引数に含まれるか | **含まれない。** Unity はアセットパスを渡す。ただし除外判定は拡張子ではなくファイル名で行い、将来この前提が変わっても壊れないようにする |

### なぜハッシュではなくリビジョン番号か

内容ハッシュで比較すれば「本当に変わったか」を厳密に判定できる。それを採らない理由は2つある。

1. Issue が `AssetPostProcessor` による更新を指定している。イベント駆動の検知にハッシュは要らない
2. AST 配下は Asset Store 由来のディレクトリで、1つが数百 MB になる。インポート判定のたびに全ファイルをハッシュすると、避けたかったコスト（時間）がそのまま判定側へ移る

**リビジョン番号の限界は「実際には変わっていないのに増える」ことである。** Unity が再インポートしただけでも加算される。これは「不要なインポートが1回だけ余分に起きる」方向の誤りであり、**「変わったのに増えない」という逆方向の誤りは起きない**（Unity がインポートしないファイルはそもそもパッケージに入らないため）。安全側に倒れるので許容する。

---

# Round 1: Editor 機能ドキュメントと削除予定一覧の新設

## 目的

2つの正本を作り、以降の Round が書き足せる場所を用意する。あわせて、記載漏れが再発しない仕組みを入れる。

- `Documentation~/EditorTools.md` — Editor 機能の正本。Round 2・3 が Packager の挙動を書き足す
- `Documentation~/Deprecations.md` — 非推奨APIと削除予定の正本。Round 3 が `Combine` を書き足す

**この Round はドキュメントだけを変更する。C# には一切触れない。**

## 成果物

### `Assets/SymphonyFrameWork/Documentation~/EditorTools.md`（新規）

パッケージ利用者向けの Editor 機能の正本。`Documentation~/` に置く理由は、Unity の Asset Import 対象外であり、`.meta` を必要としないためである（CONTRIBUTING §2 の例外に該当する）。

構成:

```markdown
# Editor機能

## 一覧
（メニューパス・Project Settings のパス・機能名の対応表）

## Symphony Administrator
## Project Settings
### Symphony Framework
### Save Data
### Asset Store Tools Packager
## 自動生成
### AutoEnumGenerator
### AssemblyGenerator
### FolderGenerator
## Asset Store Tools Packager
## パッケージ導入支援（SymphonyPackageLoader）
## アセット保護（SymphonyAssetProtector）
## デバッグ支援
### SymphonyDebugHUD
### SymphonyDebugLogger と Cache/Log.txt
### SymphonyStopWatch
### SymphonyMcpTools
## Inspector属性
## 初期化の仕組み（SymphonyEditorOrchestrator）
```

各節に書く内容を統一する。

- **何をするものか**（1〜2文）
- **どこから開くか**（メニューパス、Project Settings のパス、または自動実行である旨）
- **設定ファイルの置き場**（あれば。版管理へ含めるかどうかも書く）
- **注意点**（自動生成物を手で編集しない、など）

コード例は書かない。Editor 機能は GUI 操作が入口であり、API を直接呼ぶ利用は想定しないためである。`AssetStoreToolsPackager.Export` のような `public` メソッドは存在するが、README のクイックスタートと同じ扱いにはしない。

**網羅する対象は `Assets/SymphonyFrameWork/Editor/` 配下の全モジュールとする。** 着手時にディレクトリを列挙して確認した現在の対象:

| モジュール | 現在の記載状況 |
| --- | --- |
| `SymphonyAdministrator` | README に1行 |
| `AttributeDrawer/`（ReadOnly / DisplayText / SceneNameSelector / TagSelector） | README に1行 |
| `SymphonyConfigManager` / `SymphonyEditorConfigLocator` | 記載なし |
| `SymphonyDebugHUDMenu` | README に1行（HUD 本体の説明のみ） |
| `SymphonyDebugLogFileWriter` | README に1行 |
| `SymphonyMcpTools` | **記載なし** |
| `AssemblyGenerator` | README に1行 |
| `AssetStoreToolsPackager` | **記載なし** |
| `AutoEnumGenerator` | README に1行 |
| `FolderGenerator` | README に1行 |
| `SymphonyEditorOrchestrator` | `Architecture.md` の起動と終了に記載あり。EditorTools からはそちらへリンクする |
| `SymphonyPackageLoader` / `PackageList.txt` | **記載なし** |
| `AssetStoreToolsPackagerProvider` / `SaveDataSettingProvider` / `SymphonySettingProvider` | **記載なし** |
| `PackageInitializer` | 記載なし |
| `SymphonyAssetProtector` | ワークスペース側 `AGENTS.md` にのみ記載。利用者向けには**記載なし** |
| `TagsAndLayersPostProcessor` | 記載なし（`AutoEnumGenerator` の節に内部の仕組みとして書く） |

**この Round では現在の挙動をそのまま書く。** `Combine` も現状のとおり記載する。Round 3 で非推奨として更新する。

### `Assets/SymphonyFrameWork/Documentation~/Deprecations.md`（新規）

非推奨APIと削除予定の一覧の正本。`Documentation~/` に置く理由は `EditorTools.md` と同じ。

構成:

```markdown
# 非推奨APIと削除予定

（この文書の役割。CHANGELOGとの分担）

## 削除予定の一覧
（表。1行1メンバー）

## 削除の方針
（いつ消すか、[Obsolete]を付ける手順、CHANGELOGとの書き分け）

## 削除済み
（削除が完了したものを、削除した版とともに移す）
```

`## 削除予定の一覧` の表の列:

| 列 | 内容 |
| --- | --- |
| 対象 | `public` なメンバーの完全な名前とシグネチャ |
| 場所 | パッケージルートからの相対パス |
| 移行先 | 代替API。無い場合は「代替なし」と理由 |
| 非推奨にした版 | CHANGELOG の該当見出し |
| 削除予定 | `次のメジャー更新` など。未定なら「未定」と書き、未定であること自体を記録する |
| 備考 | 削除時に一緒に直す必要があるもの（ワークスペース側のドキュメント、移行処理など） |

**着手時に `[Obsolete]` を全数検索して初期の7件を埋める。** 「前提の確認」の表に挙げたものが現時点の全数だが、実装時にもう一度検索して確認する。削除予定が判明していない6件は「未定」と書く。**未定と書くこと自体が目的である。** 期限の無い非推奨が6件あるという事実が見えるようになる。

`ASSET_STORE_TOOLS_IGNORE_FILE` は CHANGELOG 3.1.0 に「次のメジャー更新で、旧定数と `ignore.txt` からの移行処理をあわせて削除します」とあるため、備考へ `AssetStoreToolsPackagerConfigStore` の移行処理も一緒に消す旨を書く。

**CHANGELOG との分担を文書内に明記する。** CHANGELOG は「いつ非推奨にしたか」という時系列の記録、`Deprecations.md` は「今なお非推奨で残っているもの」の現在形の一覧。非推奨化のたびに両方へ書く。

### 既存ドキュメントからの導線（変更）

| ファイル | 変更内容 |
| --- | --- |
| `Assets/SymphonyFrameWork/README.md` | `## Editor・デバッグ支援` の箇条書きは残し、冒頭へ `詳細は [Editor機能](./Documentation~/EditorTools.md) にあります。` を追加する。`## ドキュメント` の一覧へ `EditorTools.md` と `Deprecations.md` を追加する |
| `Assets/SymphonyFrameWork/AGENTS.md` | §0 の表の「Editor・デバッグ機能を使う」の参照先を `README.md#editorデバッグ支援` から `Documentation~/EditorTools.md` へ変える。「バージョン差分、非推奨化、移行方法を調べる」の行を分割し、非推奨と移行方法は `Documentation~/Deprecations.md` を参照先にする |
| `Assets/SymphonyFrameWork/Documentation~/AgentUsage.md` | `## 非推奨APIと移行` の「非推奨APIの期限と移行先は CHANGELOG.md を正本とします」を `Deprecations.md` を正本とする記述へ変える。**この1文が現在の正本を CHANGELOG と定めているため、放置すると正本が2つになる** |

README の箇条書きを消さないのは、README が「何ができるか」の索引、EditorTools が「どう使うか」の正本、という分担にするためである。

### 記載漏れを埋めさせる常時ルール（変更）

| ファイル | 変更内容 |
| --- | --- |
| `AGENTS.md`（ワークスペース側） | §0 の表の下へ、Editor 機能ドキュメントの正本と追記義務を書いた節を追加する |
| `Documentation/CONTRIBUTING.md` | §6 の表へ「Editor 機能の追加・変更」の行を追加する |

`AGENTS.md` へ入れる文面（案）:

```markdown
## Editor機能と削除予定のドキュメント

次の2つは、記載漏れが起きやすいわりに参照されるドキュメントです。**該当する変更をしたら、同じ変更の中で必ず更新してください。**

| 正本 | 対象 |
| --- | --- |
| [Documentation~/EditorTools.md](./Assets/SymphonyFrameWork/Documentation~/EditorTools.md) | Editor機能（ウィンドウ、メニュー、Project Settings、自動生成、AssetPostprocessor） |
| [Documentation~/Deprecations.md](./Assets/SymphonyFrameWork/Documentation~/Deprecations.md) | 非推奨API（`[Obsolete]`）と、その削除予定 |

- **作業中に、EditorTools.md へ記載の無いEditorモジュールを見つけたら、そのモジュールの節を追加してください。** 今回の変更で触っていないモジュールでも構いません。記載漏れは、その機能が存在しないのと同じです。記載の有無は `Assets/SymphonyFrameWork/Editor/` 配下のディレクトリと照らして判断します。
- **`[Obsolete]` を付けたら、同じ変更で Deprecations.md へ行を追加してください。** 削除予定が決まっていない場合は「未定」と書きます。書かずに済ませないでください。
- **`[Obsolete]` なメンバーを削除したら、Deprecations.md の行を `## 削除済み` へ移してください。**

コードとドキュメントの乖離はバグとして扱います（[Documentation/CONTRIBUTING.md](./Documentation/CONTRIBUTING.md) §6）。
```

`CONTRIBUTING.md` §6 の表へ追加・変更する行（案）:

| 変更の種類 | 同時に更新するもの |
| --- | --- |
| Editor機能（ウィンドウ、メニュー、Project Settings、生成物）の追加・変更 | `Assets/SymphonyFrameWork/Documentation~/EditorTools.md`、CHANGELOG.md。索引が変わる場合は README の `Editor・デバッグ支援` |
| 非推奨化（既存行を変更） | `[Obsolete("代替APIの案内", error: false)]`、**`Documentation~/Deprecations.md` への行追加（削除予定が未定なら「未定」と書く）**、CHANGELOG の `### Deprecated`（移行方法を明記）、READMEまたはSampleの旧API利用箇所 |
| 非推奨APIの削除（新規行） | `Documentation~/Deprecations.md` の行を `## 削除済み` へ移す、CHANGELOG の `### Breaking`（移行方法を明記）、`package.json` の `version`（メジャー更新） |

**「提案にとどめる」規則との関係。** `implement` スキルのステップ6は、振り返りで気づいたドキュメント変更を勝手に反映せず提案にとどめるよう定めている。今回はユーザーからの明示的な指示であり、この Round の成果物そのものであるため、提案ではなく実装として扱う。

## 影響範囲

- **コードの変更は無い。** 公開API、シリアライズ形式、挙動のいずれも変わらない
- パッケージへ `Documentation~/EditorTools.md` と `Documentation~/Deprecations.md` が2ファイル増える。`Documentation~/` は Unity の Asset Import 対象外のため、利用側プロジェクトのアセットは増えない
- `.meta` は不要（`Documentation~/` は CONTRIBUTING §2 の例外）
- **非推奨APIの正本が CHANGELOG から `Deprecations.md` へ移る。** 既存の CHANGELOG の記述は消さず、そのまま残す

## テストの置き場と種別

**自動テストを書かない。** ドキュメントのみの変更であり、検証対象になるコードが無いためである。

## 動作確認手順

自動で確認できる項目は無い。次を人が確認する。

1. `uloop-compile` がエラー0・警告0で通る（ドキュメントのみの変更なので、既存の状態から変わらないことの確認）
2. `EditorTools.md` の全メニューパスを Unity のメニューから実際に開き、書いてあるパスで到達できる
3. Project Settings の3項目（`SymphonyFrameWork` / `Save System` / `Asset Store Tools Packager`）が書いてある場所にある。ラベルはコード上の定数から確認済み（`SymphonySettingProvider.LABEL` / `SaveDataSettingProvider.LABEL` / `AssetStoreToolsPackagerProvider.LABEL`）
4. `Deprecations.md` の7件が、実際に `[Obsolete]` が付いているメンバーと過不足なく一致する（`rg "\[Obsolete" Assets/SymphonyFrameWork -g '*.cs'` の結果と突き合わせる）
5. `README.md`、両 `AGENTS.md`、`AgentUsage.md` のリンクが切れていない（相対パスが Package 導入と Assets 直置きの両方で解決すること）

## バージョン判断

**3.2.1（パッチ）。** 公開APIとシリアライズ形式は変わらず、実装にも触れない。CHANGELOG には `### Add` として、Editor 機能のドキュメントと削除予定一覧を追加したことを記載する。

## この Round で触るバージョン関連ファイル

| ファイル | 触る箇所 |
| --- | --- |
| `Assets/SymphonyFrameWork/package.json` | `version` を `3.2.0` → `3.2.1` |
| `Assets/SymphonyFrameWork/CHANGELOG.md` | 先頭へ `## [3.2.1] - <日付>` を追加。`### Add` のみ |

---

# Round 2: バージョンログの基盤

## 公開API

**新しい `public` 型は追加しない。** 追加する公開メンバーは `EditorSymphonyConstant` の定数3件だけである。

```csharp
namespace SymphonyFrameWork.Core
{
    public static class EditorSymphonyConstant
    {
        /// <summary> 対象フォルダ直下に置くバージョンログの名前。 </summary>
        public const string ASSET_STORE_TOOLS_VERSION_LOG_FILE_NAME = "PackageVersions.json";

        /// <summary> パッケージへ同梱する出力時バージョンの名前。各ディレクトリ直下に置かれる。 </summary>
        public const string ASSET_STORE_TOOLS_EXPORTED_VERSION_FILE_NAME = "ExportedVersion.json";

        /// <summary> 出力先フォルダに置くパッケージ一覧の名前。 </summary>
        public const string ASSET_STORE_TOOLS_MANIFEST_FILE_NAME = "PackageManifest.json";
    }
}
```

`public` にする根拠は、既存の `ASSET_STORE_TOOLS_CONFIG_FILE_NAME` と同じ扱いにするためである。利用側プロジェクトが自分のリポジトリでこれらの JSON を版管理し、`.gitignore` やビルドスクリプトから名前で参照する。DesignPhilosophy の「公開範囲」では「サブシステムに依存しない汎用ユーティリティ」に相当する。

それ以外の型はすべて `internal` にする。`AssetStoreToolsPackager.Export` のシグネチャは変えない。

## ファイル構成

新規（すべて `namespace SymphonyFrameWork.Editor`）:

| パス | 型 | 公開範囲 | 責務 |
| --- | --- | --- | --- |
| `Editor/Generator/AssetStoreToolsPackager/AssetStoreToolsVersionLog.cs` | `AssetStoreToolsVersionLog`<br>`AssetStoreToolsVersionEntry` | `internal sealed` | ルートログの JSON 表現と、リビジョンの取得・加算・正規化 |
| `Editor/Generator/AssetStoreToolsPackager/AssetStoreToolsVersionLogStore.cs` | `AssetStoreToolsVersionLogStore` | `internal static` | ルートログの読み書き。既定値の生成 |
| `Editor/Generator/AssetStoreToolsPackager/AssetStoreToolsExportedVersion.cs` | `AssetStoreToolsExportedVersion` | `internal sealed` | ディレクトリ内ログの JSON 表現 |
| `Editor/Generator/AssetStoreToolsPackager/AssetStoreToolsPackageManifest.cs` | `AssetStoreToolsPackageManifest`<br>`AssetStoreToolsPackageManifestEntry` | `internal sealed` | 出力先マニフェストの JSON 表現 |
| `Editor/Generator/AssetStoreToolsPackager/AssetStoreToolsVersionPathResolver.cs` | `AssetStoreToolsVersionPathResolver` | `internal static` | アセットパスから対象ディレクトリ名を解決し、除外対象を弾く。**Unity API へ触れない純粋なロジック** |
| `Editor/Generator/AssetStoreToolsPackager/AssetStoreToolsVersionPostProcessor.cs` | `AssetStoreToolsVersionPostProcessor` | `internal sealed : AssetPostprocessor` | 変更を検知して処理待ちへ積み、Orchestrator の指示で1回だけ書き出す |
| `Tests/Editor/AssetStoreToolsVersionLogTests.cs` | — | — | 純粋ロジックの EditMode テスト |

変更:

| パス | 変更内容 |
| --- | --- |
| `Core/Editor/EditorSymphonyConstant.cs` | 上記の定数3件を「自動生成物のパス」リージョンへ追加 |
| `Editor/Generator/AssetStoreToolsPackager/AssetStoreToolsPackager.cs` | 計画へリビジョンとバージョンファイルのパスを載せ、出力時にバージョンファイルとマニフェストを書く |
| `Editor/Generator/AssetStoreToolsPackager/AssetStoreToolsPackagePlan.cs` | `AssetStoreToolsPackagePlanEntry` へ `Version` を追加 |
| `Editor/Orchestrator/Internal/SymphonyEditorOrchestrator.cs` | 新しいモジュールの購読・初期化・終了・処理待ち消費を登録 |
| `Documentation~/EditorTools.md` | Packager の節へバージョンログ3種の説明を追記。**Round 1 で作った正本を、同じ変更の中で更新する** |

**`Internal/` フォルダを新設しない。** `Editor/Generator/AssetStoreToolsPackager/` は既に `internal` な `AssetStoreToolsPackagerConfig` と `AssetStoreToolsPackagePlan` を直置きしており、フォルダ全体が Packager の内部実装である。既存の凝集をそのまま使う。

**`AssetPostprocessor` を `Editor/` 直下ではなく Packager フォルダへ置く。** 既存の `TagsAndLayersPostProcessor` と `SymphonyAssetProtector` が `Editor/` 直下にあるのは、それらがフレームワーク全体を対象にするためである。今回のものは Packager 機能だけを対象にするため、機能フォルダへ置く。

**型名 `AssetStoreToolsVersionPathResolver` について。** `Resolver` は CodeGuidelines の命名表に無い。表は `Manager` と `Data` だけで終わる名前を禁止し、それ以外は「実際の役割を表すサフィックス」を求めている。純粋ロジックの補助型には既に `AssetPathTreeNode` という表外の例がある。役割（パスの解決）を表しているためこの名前を採る。

## 依存方向

すべて Editor 層に閉じる。`Core/Editor/` の定数だけを参照し、Runtime と Core の実装へは触れない。`AssetStoreToolsVersionPathResolver` は `UnityEditor` も `UnityEngine` も参照しない純粋な C# にする。

```text
SymphonyEditorOrchestrator（Composition）
   │ Initialize / Shutdown / ProcessPendingChanges
   v
AssetStoreToolsVersionPostProcessor ──> AssetStoreToolsVersionLogStore ──> AssetStoreToolsVersionLog
   │                                            ^                                  ^
   │ 判定を委譲                                  │ 読み書き                          │
   v                                            │                                  │
AssetStoreToolsVersionPathResolver（純粋）    AssetStoreToolsPackager ──────────────┘
```

## データ形式

`Assets/AssetStoreTools/PackageVersions.json`:

```json
{
  "directories": [
    { "name": "Demigiant", "version": 3, "updatedAt": "2026-08-05T09:12:44.1234567Z" },
    { "name": "TestPackage", "version": 1, "updatedAt": "2026-08-05T09:10:02.0000000Z" }
  ]
}
```

`Dictionary` ではなく配列にするのは、手で読み書きしたときに差分が行単位で出るようにするためである。`PackagerConfig.json` と同じく利用側のリポジトリで版管理される。

`Assets/AssetStoreTools/<Name>/ExportedVersion.json`:

```json
{ "name": "TestPackage", "version": 1, "exportedAt": "2026-08-05T09:15:00.0000000Z" }
```

`<ExportedPackagesPath>/Export_AssetStoreToolsPackage_yyyyMMdd_HHmmss/PackageManifest.json`:

```json
{
  "exportedAt": "2026-08-05T09:15:00.0000000Z",
  "packages": [
    { "name": "TestPackage", "version": 1, "fileName": "TestPackage.unitypackage" }
  ]
}
```

- `version` は `int`。ルートログに登録の無いディレクトリは `0` として扱い、最初の加算で `1` になる
- `updatedAt` / `exportedAt` は `DateTime.UtcNow.ToString("o")`。**記録のためだけに持ち、比較には使わない。** 比較は `version` だけで行う
- ルートログを新規生成するときは、その時点で AST 配下に存在する全ディレクトリを `version = 1` で登録する。これが基準になる

## 変更検知の流れ

```text
Unity がアセットをインポート
   v
AssetStoreToolsVersionPostProcessor.OnPostprocessAllAssets
   │ imported / deleted / moved / movedFrom の全パスを
   │ AssetStoreToolsVersionPathResolver で対象ディレクトリ名へ解決
   │ 解決できたものを HashSet<string> _pendingDirectoryNames へ積む
   │ 1件でも積んだら OnHostChangesPending を発火（ファイルは書かない）
   v
SymphonyEditorOrchestrator.HostChangesPendingHandler
   v
SymphonyEditorOrchestrator.ProcessPendingHostChanges
   │ _requiresAssetDatabaseRefresh |=
   │     AssetStoreToolsVersionPostProcessor.ProcessPendingChanges()
   │        └ 処理待ちのディレクトリ名ぶんリビジョンを加算してルートログを1回書く
   v
RefreshAssetDatabaseIfRequired（既存。1回だけ Refresh）
```

`AssetPostprocessor` 側は**判定と記録だけを行い、ファイル書き込み・`AssetDatabase.Refresh`・購読を持たない**。DesignPhilosophy の「`AssetPostprocessor` は変更種別を Orchestrator へ通知して coalesce するだけにする」に従う。

**自己ループしないことがこの設計の要点である。** ルートログ `PackageVersions.json` は AST 直下にあり、サブディレクトリ配下ではないため `TryResolveDirectoryName` が `false` を返す。ディレクトリ内の `ExportedVersion.json` は、ファイル名一致で明示的に除外する。したがって Orchestrator の `Refresh` が再び `OnPostprocessAllAssets` を呼んでも、処理待ちは1件も積まれず、そこで止まる。

`AssetStoreToolsVersionPathResolver.TryResolveDirectoryName(assetPath, rootPath, out name)` の判定順:

1. `assetPath` と `rootPath` の `\` を `/` へ正規化し、`rootPath` の末尾 `/` を落とす
2. `assetPath` が `rootPath + "/"` で始まらなければ `false`
3. 残りに `/` が1つも無ければ `false`（AST 直下のファイルとディレクトリ自身は対象外）
4. 残りの先頭セグメントを `name` とする
5. 残りが `name + "/" + ExportedVersion.json` と一致すれば `false`（`.meta` 付きも同様に弾く）
6. それ以外は `true`

ディレクトリを削除した場合、ルートログのエントリは残る。存在しないディレクトリのリビジョンは出力にも比較にも使われないため害が無い。**掃除処理は入れない**（使われない状態を消すためだけのコードを増やさない）。

## 出力時の流れ

`CreatePlan` と `Export` の役割分担は 3.2.0 で決めた「計画は出力しない」を維持する。

`CreatePlan`（ファイルを書かない）:

1. ルートログを読み、各エントリの `Version` を確定して `AssetStoreToolsPackagePlanEntry.Version` へ入れる
2. 各エントリの `AssetPaths` へ `<DirectoryPath>/ExportedVersion.json` を追加する（未収集の場合のみ）

  **まだ存在しないファイルのパスを計画へ載せる。** これは意図的である。確認ウィンドウが提示する内容と、実際に出力される内容を一致させるため。`Used Dependencies` を有効にした経路では `AssetPaths` がそのまま `ExportPackage` の引数になるので、ここへ入れないとバージョンファイルがパッケージへ入らない

`Export(plan)`（ファイルを書く）:

1. 各エントリのディレクトリへ `ExportedVersion.json` を書く
2. `AssetDatabase.Refresh()` を1回呼ぶ。**これを省くと新規ファイルが AssetDatabase に載らず、`ExportPackageOptions.Recurse` でも `ExportPackage` の明示指定でも出力されない**
3. 既存の `ExportPackage` / `CreateCombinedPackage` を実行する
4. `Singles` が指定されている場合、出力フォルダへ `PackageManifest.json` を書く
5. 既存の `CreateZip` を実行する（マニフェストを含めるため、必ず4の後）

**`Combine` だけを指定した場合、マニフェストは書かない。** 統合パッケージはディレクトリ単位で取り出せないため、差分インポートの単位にならない。その場合は「統合パッケージだけの出力は差分インポートの対象になりません」と `Debug.LogWarning` で伝える。この警告は Round 3 の `[Obsolete]` 化への布石でもある。

`CreatePlan` から `Export` までの間にユーザーがファイルを編集すると、計画のリビジョンが実際より古くなる。確認ウィンドウを挟む数秒の話であり、ずれても「次回のインポートで1回余分に取り込む」だけなので追従しない。

## エラー処理

| 失敗 | 扱い |
| --- | --- |
| ルートログが壊れている（JSON 解析失敗） | `Debug.LogError` して**リビジョン加算を中止する**。既定値へフォールバックすると全ディレクトリのリビジョンが巻き戻り、インポート先が「更新なし」と誤判定する。`AssetStoreToolsPackagerConfigStore.Load` が壊れたファイルを `null` として扱うのと同じ方針 |
| ルートログが存在しない | 現在のディレクトリ一覧を `version = 1` で登録して生成する |
| ルートログの書き込みに失敗（`IOException`） | `Debug.LogError` して処理待ちを破棄する。次の変更で再度加算される |
| `ExportedVersion.json` の書き込みに失敗 | `Debug.LogError` して**パッケージ出力は続行する**。バージョンファイルの無いパッケージはインポート側で常に「新規」と判定されるだけで、既存の出力機能は損なわれない |
| マニフェストの書き込みに失敗 | `Debug.LogError` して出力は続行する。理由は上と同じ |
| AST パスが未設定 | 既存の `AssetStoreToolsPackagerConfigStore` と同じく `Debug.LogError` して何もしない |

ログは `[{nameof(型名)}]\nメッセージ` の形式で、対象のパスを必ず含める。

## 影響範囲

- **公開APIの破壊は無い。** `AssetStoreToolsPackager.Export(string[], PackageModeEnum, bool, bool)` のシグネチャと戻り値は変えない
- **公開挙動は変わる。** `Export` が AST 配下へ `ExportedVersion.json` を、出力先へ `PackageManifest.json` を書くようになる。利用側のリポジトリに新しい JSON が現れ、出力パッケージの中身が1ファイル増える
- **AST 配下を編集すると `PackageVersions.json` が更新される。** 利用側が意図しない差分と受け取らないよう、CHANGELOG と `EditorTools.md` で「版管理へ含めるファイル」として明示する
- シリアライズ済みデータ（`ScriptableObject`、セーブデータ）への影響は無い

## テストの置き場と種別

`Assets/SymphonyFrameWork/Tests/Editor/AssetStoreToolsVersionLogTests.cs`（EditMode）。

**ファイル I/O と `AssetDatabase` に触れない純粋ロジックだけを対象にする。** `AssetStoreToolsVersionLogStore` は実ファイルを触るため対象外、`AssetStoreToolsVersionPostProcessor` は Unity がインポートを起こさないと呼ばれないため対象外とする。この2つは下の「動作確認手順」で人が確認する。

メソッド名は既存の `AssetStoreToolsPackagerConfigTests` に合わせて英語の `対象_条件_期待` 形式にする。

| テスト | どう書くか |
| --- | --- |
| `TryResolveDirectoryName_AssetUnderDirectory_ReturnsDirectoryName` | `"Assets/AssetStoreTools/Demigiant/DOTween/DOTween.dll"` と `"Assets/AssetStoreTools"` を渡し、`out` が `"Demigiant"`、戻り値が `true` |
| `TryResolveDirectoryName_FileAtRoot_ReturnsFalse` | `"Assets/AssetStoreTools/PackagerConfig.json"` を渡し、戻り値が `false` |
| `TryResolveDirectoryName_DirectoryItself_ReturnsFalse` | `"Assets/AssetStoreTools/Demigiant"` を渡し、戻り値が `false` |
| `TryResolveDirectoryName_ExportedVersionFile_ReturnsFalse` | `"Assets/AssetStoreTools/Demigiant/ExportedVersion.json"` を渡し、戻り値が `false`。**自己ループしないことの回帰テスト** |
| `TryResolveDirectoryName_ExportedVersionMetaFile_ReturnsFalse` | 上に `.meta` を付けたパスで `false` |
| `TryResolveDirectoryName_OutsideRoot_ReturnsFalse` | `"Assets/Scripts/Foo.cs"` を渡し、`false` |
| `TryResolveDirectoryName_BackslashPath_ReturnsDirectoryName` | `"Assets\\AssetStoreTools\\Demigiant\\Foo.cs"` を渡して `"Demigiant"`。Windows のパス区切りで落ちないこと |
| `TryResolveDirectoryName_RootWithTrailingSlash_ReturnsDirectoryName` | `rootPath` に `"Assets/AssetStoreTools/"` を渡しても解決できる |
| `GetVersion_UnknownName_ReturnsZero` | 空の `AssetStoreToolsVersionLog` に対し `GetVersion("Foo")` が `0` |
| `IncrementVersion_UnknownName_BecomesOne` | 空のログへ `IncrementVersion("Foo")` して `GetVersion("Foo")` が `1` |
| `IncrementVersion_KnownName_AddsOne` | `version = 3` のエントリを持つログへ加算して `4` |
| `IncrementVersion_UpdatesTimestamp` | 加算前の `updatedAt` を記録し、加算後がそれと異なる |
| `Normalize_NullEntries_BecomesEmptyList` | `Directories = null` のログを `Normalize()` して空リスト |
| `Normalize_EntryWithEmptyName_IsRemoved` | 名前が空白のエントリが落ちる |
| `Normalize_NegativeVersion_BecomesZero` | 手で編集して負値になったリビジョンが `0` へ丸められる |

`IncrementVersion_UpdatesTimestamp` は、**加算前の値を記録して差分で比較する**。`DateTime.UtcNow` の絶対値と比較しない。

## 動作確認手順

**自動で確認できるのは上の EditMode テストだけである。** `AssetPostprocessor` の発火と `EditorWindow` の操作は自動化できない（`EditorWindow.SendEvent` で GUILayout は反応せず、`uloop` の `simulate-mouse-*` は PlayMode の Game View 専用）。以下は人が Unity 上で操作して確認する。

1. `Assets/AssetStoreTools/TestPackage/NewMonoBehaviourScript.cs` へコメントを1行足して保存する
   → `Assets/AssetStoreTools/PackageVersions.json` の `TestPackage` の `version` が 1 増える
2. 何も編集せずに Unity へフォーカスを当て直す
   → `version` が増えない
3. Packager ウィンドウ（`Tools/SymphonyFrameWork/ExportAssetStoreToolsFolder`）で `TestPackage` だけを選び、`Export Mode = Singles` で `Export Selected Directories` を押す
   → 確認ウィンドウのツリーに `ExportedVersion.json` が現れる
4. `Export` を押す
   → `Assets/AssetStoreTools/TestPackage/ExportedVersion.json` が生成され、`version` が手順1の値と一致する
   → `ExportedPackages/Export_AssetStoreToolsPackage_*/PackageManifest.json` に `TestPackage` と同じ `version` が記録される
   → **`PackageVersions.json` の `version` が手順1から増えていない**（バージョンファイルの生成で自己ループしない）
5. 出力された `TestPackage.unitypackage` を展開する、または別プロジェクトへ取り込む
   → `ExportedVersion.json` がパッケージへ含まれている
6. `Used Dependencies` を有効にして手順3〜4を繰り返す
   → こちらの経路でも `ExportedVersion.json` がパッケージへ含まれている
7. `Export Mode = Combine` だけで出力する
   → マニフェストが作られず、差分インポートの対象にならない旨の警告が Console に出る
8. Play Mode の開始・終了を2回繰り返す
   → Console に新しいエラーと意図しない警告が出ない（Domain Reload が無効なため、`AssetPostprocessor` の static な処理待ちが残らないことの確認）

## バージョン判断

**3.3.0（マイナー）。** 後方互換な追加である。

- `EditorSymphonyConstant` へ `public const` を3件追加する → 公開API の追加
- `Export` の公開挙動が変わる（出力物が増える）が、既存のシグネチャ・戻り値・出力パッケージの既存内容は変わらない
- 破壊的変更は無いのでメジャーではない。公開APIが増えるのでパッチでもない

## この Round で触るバージョン関連ファイル

| ファイル | 触る箇所 |
| --- | --- |
| `Assets/SymphonyFrameWork/package.json` | `version` を `3.2.1` → `3.3.0` |
| `Assets/SymphonyFrameWork/CHANGELOG.md` | 先頭へ `## [3.3.0] - <日付>` を追加。`### Add` と `### Change` |
| `Assets/SymphonyFrameWork/Documentation~/EditorTools.md` | Packager の節。**Round 1 が作った節を書き換える** |

`README.md` と `AGENTS.md` は、Round 1 で作った導線が変わらないため触らない。

---

# Round 3: 2タブ化と差分インポート

**この節は Round 1 着手時点の計画である。着手時にもう一度コードを見て確認し、そのうえで設計を確定する。**

## 想定する内容

- `AssetStoreToolsPackageWindow` を `Export` / `Import` の2タブにする。既存の描画は `Export` タブへそのまま移す
- `Import` タブ:
  - `ExportedPackagesPath` 配下の `Export_*` フォルダを新しい順に列挙し、1つ選ぶ
  - 選んだフォルダの `PackageManifest.json` を読む
  - 各エントリについて、ローカルの `<AST>/<Name>/ExportedVersion.json` と `version` を比較する
  - 状態を `New`（ローカルに無い）/ `Updated`（ローカルが古い）/ `UpToDate`（同じ）/ `Newer`（ローカルの方が新しい）へ分類して表示する
  - 既定で `New` と `Updated` だけを選択済みにする。`Newer` は既定で選ばない（意図しない巻き戻しを避ける）
  - `Import Selected` で `AssetDatabase.ImportPackage(path, interactive: false)` を順に呼ぶ
- 状態の分類は Unity API へ触れない純粋ロジック（`AssetStoreToolsImportStateEnum` を返す判定）へ切り出し、そこを EditMode テストの対象にする。タブ切り替えとボタン操作は人が確認する

### `Combine` の非推奨化

- `PackageModeEnum.Combine` へ `[Obsolete("差分インポートに対応しないため廃止予定です。Singlesを使用してください。", error: false)]` を付ける
- Export タブでは選べないようにするか、選んだ場合に警告を出す。どちらにするかは着手時に決める
- **`Deprecations.md` へ行を追加する。** 削除予定は「次のメジャー更新」、備考へ削除時に一緒に直すものを書く

  | 項目 | 内容 |
  | --- | --- |
  | 対象 | `AssetStoreToolsPackager.PackageModeEnum`（`Combine` を含む enum 全体） |
  | 場所 | `Editor/Generator/AssetStoreToolsPackager/AssetStoreToolsPackager.cs` |
  | 移行先 | 代替なし。個別出力（旧 `Singles`）が唯一の形式になり、`Export` から `mode` 引数が消える |
  | 非推奨にした版 | 3.4.0 |
  | 削除予定 | 次のメジャー更新（4.0.0） |
  | 備考 | 削除時は `CreateCombinedPackage`、`AssetStoreToolsPackagePlan.Mode`、`AssetStoreToolsPackageWindow` の `Export Mode`、`AssetStoreToolsPackageConfirmWindow` の `Export Mode` 行、ワークスペース側 `Documentation/DesignPhilosophy.md:346` の enum 実例も一緒に直す |

**削除はこの Issue では行わない。** `PackageModeEnum` は `public` なので削除には 4.0.0 が要る。Editor 専用ツール1つのためにメジャーを消費せず、他の破壊的変更とまとめて出す。`Deprecations.md` へ記録した時点で Issue #119 を閉じる。

削除時に `[Obsolete]` のシムを残せないことも、いま記録しておく。**`PackageModeEnum` 自体が消えるため、旧シグネチャは型として成立しない。** DesignPhilosophy の「名前、シグネチャ、意味が同時に変わり旧契約を安全に維持できない API は、メジャー更新で直接削除し、CHANGELOG の Breaking 項目と移行ガイドへ明記する」に該当する。

## 想定するバージョン

**3.4.0（マイナー）。** `EditorWindow` の表示が変わり、`Combine` が非推奨になるが、削除はしないため破壊的ではない。CHANGELOG には `### Add` と `### Deprecated`（移行方法を明記）を書く。

## 想定するバージョン関連ファイル

| ファイル | 触る箇所 |
| --- | --- |
| `Assets/SymphonyFrameWork/package.json` | `version` を `3.3.0` → `3.4.0` |
| `Assets/SymphonyFrameWork/CHANGELOG.md` | 先頭へ `## [3.4.0]`。`### Add` と `### Deprecated` |
| `Assets/SymphonyFrameWork/Documentation~/EditorTools.md` | Packager の節へ Import タブと `Combine` 非推奨の記載を追加 |
| `Assets/SymphonyFrameWork/Documentation~/Deprecations.md` | `PackageModeEnum` の行を追加 |

---

# この Issue の範囲外

次は `Deprecations.md` へ記録し、この Issue では実施しない。

| やらないこと | 記録先 | 実施時期 |
| --- | --- | --- |
| `PackageModeEnum` と `Export` の `mode` 引数、`CreateCombinedPackage` の削除 | `Deprecations.md`（Round 3 で記載） | 次のメジャー更新（4.0.0）。他の破壊的変更とまとめる |
| 期限未定の既存 `[Obsolete]` 6件の削除時期の決定 | `Deprecations.md`（Round 1 で「未定」として記載） | 別途判断する。**「未定」と書き出すことで、判断が必要であること自体を見えるようにする** |
