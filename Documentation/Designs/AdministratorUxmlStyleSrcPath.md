# Symphony Administrator UXML の Style 参照パス修正

## 目的

Symphony Administrator を構成する UXML のうち2つが、`<Style src="...">` に**実在しないフォルダのパス**を書いている。

| ファイル | 現在の `src` のパス部分 |
| --- | --- |
| `Editor/Administrator/UITK/UXML/PauseWindow.uxml` | `Assets/Scripts/SymphonyFrameWork/SymphonyEditor/Editor/Administrator/UITK/SymphonyWIndow.uss` |
| `Editor/Administrator/UITK/UXML/AutoEnumGeneratorWindow.uxml` | `Assets/SymphonyFrameWork/SymphonyEditor/Editor/Administrator/UITK/SymphonyWIndow.uss` |

正しいパスは、残る4つ（`SymphonyWindow.uxml`、`SaveDataWindow.uxml`、`SceneLoadWindow.uxml`、`ServiceLocateWindow.uxml`）と同じ `Assets/SymphonyFrameWork/Editor/Administrator/UITK/SymphonyWIndow.uss` である。`Assets/Scripts/SymphonyFrameWork/` も `SymphonyEditor/` も現在のリポジトリには存在せず、過去のフォルダ構成の残骸である。

現状で表示が壊れていないのは、`src` のクエリ文字列 `guid=16c3f611d15eda644b63e276b16e6925`（`SymphonyWIndow.uss.meta` の GUID と一致）で Unity が解決しているため。**壊れるのは GUID が失われた場合と、パス側で解決する経路を通った場合**であり、読む人にも「そういうフォルダがある」という誤解を与える。

これは表示の不具合の修正ではなく、**参照の一貫性を戻す修正**である。

## Round 分割

1 Round で完了する。UXML 2ファイルのパス修正、再発検出テストの追加、CHANGELOG とパッチバージョンの更新を同時に行う。公開 API、パネルの表示・操作ロジック、USS の中身、`SymphonyWindow.uxml` の名前空間宣言は変更しない。

Issue は無いため、submodule の `develop` から `feature/administrator-uxml-style-path` を切る。

## 公開API

追加・変更・削除なし。UXML の属性値だけを変更する。

## ファイル構成

- 変更: `Assets/SymphonyFrameWork/Editor/Administrator/UITK/UXML/PauseWindow.uxml`
  - `<Style src>` のパス部分を `Assets/SymphonyFrameWork/Editor/Administrator/UITK/SymphonyWIndow.uss` へ直す。`fileID`・`guid`・`type`・フラグメント（`#SymphonyWIndow`）は現状のまま残す。
- 変更: `Assets/SymphonyFrameWork/Editor/Administrator/UITK/UXML/AutoEnumGeneratorWindow.uxml`
  - 同上。
- 新規: `Assets/SymphonyFrameWork/Tests/Editor/AdministratorUxmlStyleSrcTests.cs`
  - 名前空間: `SymphonyFrameWork.Tests`
  - 再発検出用の EditMode テスト。`.meta` は Unity に生成させる。
- 変更: `Assets/SymphonyFrameWork/CHANGELOG.md`、`package.json`、`README.md`（版）
- 設計記録: `Documentation/Designs/AdministratorUxmlStyleSrcPath.md`

既存の `Tests/Editor/SymphonyAdministratorUxmlTests.cs` は変更しない。あれは UXML からカスタム要素**型**が解決できることを検証しており、検証対象が違う。同じファイルへ混ぜると、どちらが落ちたのか名前から読めなくなる。

新しい Runtime / Editor 型は作らない。

## 依存方向

Editor 配下の UXML アセットと EditMode テストだけを変更する。テストアセンブリ `SymphonyFrameWork.Tests.Editor` は `SymphonyFrameWork.Editor` と `SymphonyFrameWork.Core` を参照済みで、`Editor -> Runtime -> Core` の向きは変わらない。Runtime / Core から `UnityEditor` への参照は追加しない。

## エラー処理

実行時の分岐・例外は追加しない。テスト側では次を別々の assertion にする。

- UXML が1つも見つからない（検索対象のパスが変わって検証が空振りしている状態）
- `<Style src>` から GUID を取り出せない
- パスと GUID の解決先が食い違う

## 影響範囲

- 表示・動作は変わらない。現状も GUID で解決できているため、修正前後で Symphony Administrator の見た目は同じである。
- 公開 API、シリアライズ形式、メニューパス、設定保存先への影響はない。
- USS の GUID が失われた場合、または UXML をパスで解決する経路を通った場合に、正しいスタイルへ到達できるようになる。

### アクセス手段の検証（確定前に確認済み）

- `SymphonyAdministrator.UITK_UXML_PATH`（`public static`）と `EditorSymphonyConstant.UITK_PATH`（`public static`）へテストアセンブリから到達できる。asmdef が両アセンブリを参照している。
- `EditorSymphonyConstant.IsPackage()` は `public static`。`[CallerFilePath]` はテスト自身のソースパスを取るため、テストが置かれた場所（`Assets/` 配下か `Packages/` 配下か）を正しく判定する。
- `SymphonyWIndow.uss.meta` の `guid` は `16c3f611d15eda644b63e276b16e6925` で、6つの UXML が書いている `guid=` と一致する。GUID からパスを引く経路が成立する。

## テストの置き場と種別

EditMode テストを `Assets/SymphonyFrameWork/Tests/Editor/AdministratorUxmlStyleSrcTests.cs` へ追加する。

- `StyleSrc_AllAdministratorUxml_PathAgreesWithGuid`
  - **どう書くか**: `AssetDatabase.FindAssets("t:VisualTreeAsset", new[] { EditorSymphonyConstant.UITK_PATH.TrimEnd('/') })` で UITK 配下の UXML を再帰的に列挙する（現在は6件。`UXML/` サブフォルダも含む）。件数が0なら「検証が空振りしている」として失敗させる。各 UXML を `File.ReadAllText`（AssetDatabase のパスはプロジェクトルート相対でそのまま開ける）で読み、`Regex` で `src="project://database/<パス>?...guid=<32桁hex>..."` からパス部分と GUID を取り出す。パス部分が `AssetDatabase.GUIDToAssetPath(guid)` と一致することを、ファイル名付きのメッセージで assert する。
  - **なぜ GUID との突き合わせか**: 「パスが実在するか」だけを見ると、たまたま別の USS を指していても通る。GUID は Unity が実際に解決に使っている値なので、**パスと GUID が同じアセットを指すこと**が本来の不変条件である。
  - **UPM 配置での扱い**: `src` の literal は `Assets/...` 形式しか書けないのに対し、`GUIDToAssetPath` は `Packages/` 配下なら `Packages/...` を返す。`EditorSymphonyConstant.IsPackage()` が真なら `Assert.Ignore` で理由付きスキップする。既存の `SymphonyAdministratorUxmlTests` も `UITK_PATH` 経由で Assets 配置を前提にしており、前提を揃える。
  - 現状の UXML 2ファイルで失敗し、修正後に成功する。

`LogAssert` は不要（ログを伴う経路を通らない）。GUI 操作も伴わないため、人の確認は表示の目視だけで足りる。

## 動作確認手順

自動で確認する項目:

1. Unity Console をクリアし、`uloop-compile` でエラー0・警告0を確認する。
2. EditMode / PlayMode テストを全数実行し、全件成功を確認する。特に既存の `SymphonyAdministratorUxmlTests.Instantiate_AllAdministratorPanels_UsesRegisteredCustomElements` と、新規の `StyleSrc_AllAdministratorUxml_PathAgreesWithGuid` の成功を確認する。
3. 新規テストが修正前の UXML で失敗することを、修正を当てる前に一度実行して確認する（検出力の実測。ここを飛ばすと常に通るだけのテストを足しかねない）。
4. `python scripts/release_round.py preflight` を通す。
5. 新規 `.cs` に `.meta` が対で存在することを確認する。

人が確認する項目:

1. `Window > SymphonyFrameWork > Symphony Administrator` を開き、Pause / Service Locate / Scene Load / Save Data / Auto Enum Generator の5パネルが、修正前と同じスタイル（`base` / `title` / `text` / `button` クラスの見た目）で表示されることを確認する。**この修正は見た目を変えないので、「変わっていないこと」が期待値である。**
2. ウィンドウを開いたまま Play Mode の開始・終了を2回繰り返し、Console に新しいエラー・警告が出ないことを確認する。

## バージョン判断

**パッチ更新**とする。公開 API とシリアライズ形式を変えず、アセット内の参照記述の誤りだけを正すため。`### Fix` は他の見出しと同じ版へ混ぜない規則に従い、この Round は Fix のみの単独の版として出す。

版番号は `release_round.py bump --level patch` で develop の現在値から1つ上げる。着手時点の develop の版に依存するため、設計書には固定値を書かない（PR #155 のマージで develop の版が動く）。

## この Round で触るバージョン関連ファイル

- `Assets/SymphonyFrameWork/package.json`: `version`
- `Assets/SymphonyFrameWork/CHANGELOG.md`: 新しいパッチ版の `### Fix`。何が誤っていたか、なぜ表示は壊れていなかったか、いつ壊れるかを書く
- `Assets/SymphonyFrameWork/README.md`: 「現在のバージョン」

上記3つは `bump` が同時に書き換える。

`AGENTS.md`、`Documentation~/EditorTools.md`、`Documentation~/Architecture.md`、Sample は更新しない。公開 API、利用手順、アセンブリ構成、初期化構成のいずれも変わらず、UXML の内部的な参照記述はこれらの文書の記述対象ではないため。

## この Round に含めないもの

- **`noNamespaceSchemaLocation` の相対階層のずれ**。`UXML/` 配下の5ファイルは `../` が8段だが、プロジェクトルートの `UIElementsSchema/` へ届くのは6段。`SymphonyWindow.uxml` は6段だが正しくは5段。6ファイル全部がずれており、`Style src` とは別の原因・別の影響範囲（UXML エディタのスキーマ補完のみ）なので、別 Round とする。
- USS の中身、ファイル名の `SymphonyWIndow.uss`（`I` が大文字）の改名。改名は GUID を保ったままでも参照記述の全書き換えを伴うため、別 Round が要る。
