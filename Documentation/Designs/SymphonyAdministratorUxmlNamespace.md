# Symphony Administrator UXML 名前空間修正

## 目的

`Symphony Administrator` を開いたとき、`PauseWindow`、`ServiceLocateWindow`、`SceneLoadWindow`、`SaveDataWindow`、`AutoEnumGeneratorWindow` が `UxmlElementAttribute` または factory を持たないという警告が出て、管理パネルを UXML から生成できない問題を修正する。

対象の5型はすでに `public partial`、`[UxmlElement]`、引数なしコンストラクタを持ち、コンパイル済みの `SymphonyFrameWork.Editor.dll` にも `UxmlElementAttribute` と生成済み `UxmlSerializedData` が存在する。原因は C# 側の登録不足ではなく、`SymphonyWindow.uxml` が `SymphonyFrameWork.Editor` 名前空間を宣言せず、完全修飾名を名前空間なしのタグとして記述していることにある。

## Round 分割

1 Round で完了する。UXML の名前空間参照修正、再発防止テスト、Editor 機能ドキュメント、CHANGELOG、パッチバージョン更新を同時に行う。公開 API、各パネル固有の表示・操作ロジック、個別パネル UXML は変更しない。

## 公開API

追加・変更・削除なし。既存の5つの `public` な `VisualElement` 型のシグネチャ、可視性、UXML 要素名は変更しない。UXML が既存型へ到達する名前空間参照だけを正す。

## ファイル構成

- 変更: `Assets/SymphonyFrameWork/Editor/Administrator/UITK/SymphonyWindow.uxml`
  - ルートへ `SymphonyFrameWork.Editor` の名前空間宣言を追加する。
  - 5つのカスタム要素を名前空間 prefix 付きの型名で参照する。
- 新規: `Assets/SymphonyFrameWork/Tests/Editor/SymphonyAdministratorUxmlTests.cs`
  - 名前空間: `SymphonyFrameWork.Tests`
  - `SymphonyWindow.uxml` を実際にインスタンス化し、5型が生成されることを検証する。
- 変更: `Assets/SymphonyFrameWork/Documentation~/EditorTools.md`
  - Symphony Administrator の5パネルが登録済み UXML カスタム要素として構築されることを明記する。
- 変更: `Assets/SymphonyFrameWork/CHANGELOG.md`
- 変更: `Assets/SymphonyFrameWork/package.json`
- 設計記録: `Documentation/Designs/SymphonyAdministratorUxmlNamespace.md`

新しい Runtime / Editor 型や名前空間は作らない。テストファイルの `.meta` は Unity に生成させる。

## 依存方向

Editor の UXML と Editor 用 EditMode テストだけを変更する。テストアセンブリは既存の `SymphonyFrameWork.Tests.Editor.asmdef` から `SymphonyFrameWork.Editor` を参照済みであり、`Editor -> Runtime -> Core` の製品コードの依存方向は変わらない。Runtime / Core から `UnityEditor` への参照は追加しない。

## エラー処理

実行時の新しい分岐や例外処理は追加しない。誤った名前空間参照を正し、UI Toolkit の標準 UXML 登録経路で既存型を生成させる。テストでは UXML アセット自体を取得できない場合と、各カスタム型を生成できない場合を別の assertion として示す。

## 影響範囲

- `Symphony Administrator` の構築時に5パネルが正しい型として生成され、添付の警告が出なくなる。
- 公開 API、シリアライズ形式、個別パネルの操作、メニューパス、設定保存先への影響はない。
- UXML 内の要素名は既存 C# 型名のままで、型名変更や移行作業はない。
- パッケージの Assets 直置き環境を対象に自動検証する。パス解決は既存の `EditorSymphonyConstant.UITK_PATH` を利用し、UPM 配置でも同じ既存解決経路を維持する。

## テストの置き場と種別

EditMode テストを `Assets/SymphonyFrameWork/Tests/Editor/SymphonyAdministratorUxmlTests.cs` へ追加する。

- `Instantiate_AllAdministratorPanels_UsesRegisteredCustomElements`
  - `AssetDatabase.LoadAssetAtPath<VisualTreeAsset>` で `SymphonyWindow.uxml` を読み、`Instantiate()` したコンテナを `Q<T>()` で検索して5型すべてが存在することを検証する。生成された `IDisposable` パネルは `finally` で破棄し、Editor callback の購読を残さない。

このテストは GUI のクリックや見た目を検証せず、今回壊れていた UXML から C# 型への解決経路だけを検証する。現状の UXML では5型が生成されず失敗し、名前空間修正後に成功する形にする。

## 動作確認手順

自動確認:

1. Unity Console をクリアして再コンパイルし、エラー0・警告0を確認する。
2. EditMode / PlayMode テストを全数実行し、全件成功を確認する。
3. `Symphony Administrator` の UXML をコードからインスタンス化し、5型が取得でき、添付の警告が出ないことを確認する。
4. Play Mode の開始・終了を2回繰り返し、Consoleに新しいエラー・警告がなく、パネルの購読が残らないことを確認する。
5. `git diff --check`、Runtime / Core の `UnityEditor` 参照検索、テスト asmdef の `UNITY_INCLUDE_TESTS` を確認する。

人の確認:

1. `Window > SymphonyFrameWork > Symphony Administrator` を開き、Pause / Service Locate / Scene Load / Save Data / Auto Enum Generator の5パネルが表示されることを確認する。
2. ウィンドウを開いたまま Play Mode を開始・終了し、表示の接続・切断が追従することを確認する。

## バージョン判断

`3.8.3` から `3.8.4` へのパッチ更新とする。公開 API とシリアライズ形式を変えず、既存 Editor ウィンドウの UXML 解決不具合だけを修正するため。

## この Round で触るバージョン関連ファイル

- `Assets/SymphonyFrameWork/package.json`: `version` を `3.8.4` へ更新する。
- `Assets/SymphonyFrameWork/CHANGELOG.md`: `3.8.4` の `Fix` を追加し、原因、修正内容、公開 API とシリアライズ形式への影響がないことを記載する。
- `Assets/SymphonyFrameWork/README.md`: 「現在のバージョン」を `3.8.4` へ更新する。API説明は変更しない。
- `Assets/SymphonyFrameWork/Documentation~/EditorTools.md`: Symphony Administrator の UXML カスタム要素構築を追記する。

Sample、`AGENTS.md`、`Documentation~/Architecture.md` は、公開 API、利用手順、アセンブリ構成、初期化構成が変わらないため更新しない。
