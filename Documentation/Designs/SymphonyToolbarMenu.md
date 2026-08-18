# Symphony Toolbar Menu

## 目的

Issue #189で追加した単独の`Scene Init`トグルを、メインツールバー上の`Symphony Framework`プルダウンへ置き換える。今後Framework本体へEditor操作を追加するとき、中央のツールバーfactoryを編集せず、機能ごとの項目クラスを追加してメニューへ参加できる構造にする。

現在の`SymphonyMainToolbar`は、ツールバー登録、Scene Load設定の読み書き、表示更新を1型に持つ。公式`MainToolbarElementAttribute`によってツールバー項目自体は増やせるが、項目ごとにメインツールバーの表示領域を消費し、Framework内でメニュー項目を登録する契約もない。本Roundでは公式`MainToolbarDropdown`を固定入口にし、Scene Load固有処理を独立した項目へ移す。

## Round

1 Roundで実施する。対象はメインツールバーの表示方式、内部登録契約、`Scene Init`の移設、テスト、利用者向け文書、バージョン更新である。RuntimeのScene Load処理、設定アセットの形式、Symphony Administrator、利用側が独自項目を登録する公開拡張APIは含めない。

## 表示と操作

メインツールバー右側へ音符アイコン付きの`Symphony Framework`プルダウンを1つ登録する。開いたメニューには次の項目を表示する。

| パス | 状態 | 操作 |
| --- | --- | --- |
| `Scene Init` | `SceneLoadConfig.IsResetAndLoadOnPlay`がtrueならチェック付き | 選択時に現在値を反転し、Undo対象として保存する |

メニューは開くたびに項目を構築し直す。Project SettingsまたはInspectorから設定が変わっても、ツールバー要素の再登録やポーリングをせず、次に開いたメニューへ現在値を反映する。Configが取得できない場合は`Scene Init`を未チェックかつ操作不能で表示する。

## アイコンとライセンス

プルダウンにはLucideの`music-2`アイコンを使用する。LucideはISCライセンスで商用利用、改変、再配布が認められている。上流のライセンス全文にはFeather由来アイコン用のMITライセンスも含まれるため、取得時の全文をパッケージ直下の`Third Party Notices.md`へ転載する。

| アセット | 用途 |
| --- | --- |
| `music-2-light.png` | Unityの明色テーマ用。暗色の線を透明背景へ描画 |
| `music-2-dark.png` | Unityの暗色テーマ用。明色の線を透明背景へ描画 |

どちらもLucide公式SVGを32×32ピクセルのPNGへ変換した派生物とし、形状は変更しない。`EditorGUIUtility.isProSkin`で選択し、`EditorSymphonyConstant.FRAMEWORK_PATH`を基準に`AssetDatabase.LoadAssetAtPath<Texture2D>`で読み込む。これによりPackage Manager導入と`Assets/SymphonyFrameWork`直置きの両方で同じアセットへ到達する。

アイコンを読み込めない場合も、`Symphony Framework`というテキストだけでプルダウンを生成する。アイコン欠落によってメインツールバー全体を構築不能にしない。

出典とライセンスは[Lucide公式リポジトリ](https://github.com/lucide-icons/lucide)および[公式LICENSE](https://github.com/lucide-icons/lucide/blob/main/LICENSE)を正本として確認した。Unityパッケージへ第三者通知を含める形式は、Unity Manualの`Third Party Notices.md`に従う。

## 拡張契約

Framework本体のメニュー項目は、`[SymphonyToolbarMenuItem]`を付けた`ISymphonyToolbarMenuItem`実装として追加する。どちらもEditorアセンブリ内の`internal`型とし、利用側プロジェクトへ公開しない。

```csharp
[SymphonyToolbarMenuItem]
internal sealed class SceneInitializationToolbarMenuItem
    : ISymphonyToolbarMenuItem
{
    public string Path => "Scene Init";
    public int Priority => 100;
    public bool IsChecked { get; }
    public bool IsEnabled { get; }
    public void Execute();
}
```

`SymphonyToolbarMenuCatalog`は`TypeCache.GetTypesWithAttribute<SymphonyToolbarMenuItemAttribute>()`で候補を取得し、interface実装、非abstract、引数なしコンストラクタを満たす型だけを生成する。生成した項目は`Priority`昇順、同値なら`Path`のordinal昇順で並べる。

マーカー属性を要求するのは、interfaceをテスト用fakeや内部helperが実装しても実際のメニューへ混入させないためである。型探索と生成はメニューを開いた時だけ実行し、static constructor、`InitializeOnLoad`、常時購読は追加しない。

## 公開API

追加・変更しない。新設する属性、interface、catalog、Scene Init項目はすべて`internal`である。

この拡張契約は、今後のSymphony Framework本体のEditor項目を分離するためのものに限定する。利用側による拡張を公開契約にすると、メニュー項目の状態、例外、順序、互換性をSemVer対象として固定する必要があるが、Issue #189はそこまで要求していないため公開しない。

## ファイル構成

| 種別 | パス | 内容 |
| --- | --- | --- |
| 変更 | `Assets/SymphonyFrameWork/Editor/Toolbar/Internal/SymphonyMainToolbar.cs` | 単独トグルを廃止し、`Symphony Framework`プルダウンだけを公式Main Toolbarへ登録 |
| 新規 | `Assets/SymphonyFrameWork/Editor/Toolbar/Internal/ISymphonyToolbarMenuItem.cs` | 項目のパス、優先度、状態、操作を表す内部契約 |
| 新規 | `Assets/SymphonyFrameWork/Editor/Toolbar/Internal/SymphonyToolbarMenuItemAttribute.cs` | TypeCache探索対象を明示する内部マーカー |
| 新規 | `Assets/SymphonyFrameWork/Editor/Toolbar/Internal/SymphonyToolbarMenuCatalog.cs` | 項目の探索、検証、生成、並べ替え |
| 新規 | `Assets/SymphonyFrameWork/Editor/Toolbar/Internal/SceneInitializationToolbarMenuItem.cs` | Scene Load設定の表示、反転、保存 |
| 新規 | `Assets/SymphonyFrameWork/Editor/Toolbar/Icons/music-2-light.png` | 明色テーマ用のLucide音符アイコン |
| 新規 | `Assets/SymphonyFrameWork/Editor/Toolbar/Icons/music-2-dark.png` | 暗色テーマ用のLucide音符アイコン |
| 変更 | `Assets/SymphonyFrameWork/Editor/Configs/Drawer/SceneLoadConfigDrawer.cs` | 単独トグル用Refresh呼び出しを削除 |
| 変更 | `Assets/SymphonyFrameWork/Tests/Editor/SymphonyMainToolbarTests.cs` | プルダウン登録、catalog、Scene Init項目のテストへ更新 |
| 変更 | `Assets/SymphonyFrameWork/Documentation~/Modules/SceneLoader.md` | `Scene Init`の入口をプルダウン階層へ更新 |
| 変更 | `Assets/SymphonyFrameWork/Documentation~/EditorTools.md` | 一覧とメインツールバー節をプルダウン表示へ更新 |
| 変更 | `Assets/SymphonyFrameWork/README.md` | 現在バージョンとEditor機能索引を更新 |
| 自動生成 | `Assets/SymphonyFrameWork/Documentation~/Html/` | Markdown正本から再生成 |
| 変更 | `Assets/SymphonyFrameWork/package.json` | パッチバージョン更新 |
| 変更 | `Assets/SymphonyFrameWork/CHANGELOG.md` | 表示方式と内部拡張構造を記録 |
| 新規 | `Assets/SymphonyFrameWork/Third Party Notices.md` | Lucideの出典、ISC／MITライセンス全文 |

新規の`Assets/`配下ファイルにはUnityが生成する`.meta`を対にする。名前空間は既存と同じ`SymphonyFrameWork.Editor`とし、`Internal`は名前空間へ含めない。

## 依存方向

すべて`SymphonyFrameWork.Editor`アセンブリのView／Editor Infrastructureに属する。`SymphonyMainToolbar`はUnityの`MainToolbarDropdown`と`GenericMenu`、catalogだけを参照する。`SceneInitializationToolbarMenuItem`だけがRuntime Infrastructureの`SceneLoadConfig`とEditor用`SymphonyConfigLocator`、Undo／AssetDatabase APIを参照する。

依存方向は`Editor -> Runtime -> Core`のままである。Runtime／CoreへEditor用契約や`UnityEditor`参照を追加しない。

## アクセス手段の確認

| 経路 | 確認結果 |
| --- | --- |
| メインツールバーのプルダウン | Unity 6000.3.10f1の`MainToolbarDropdown(MainToolbarContent, Action<Rect>)`をEditor DLLから確認済み |
| ドロップダウン表示 | `GenericMenu.DropDown(Rect)`で公式callbackの位置へ表示できる |
| 項目探索 | `UnityEditor.TypeCache`はEditorアセンブリから利用でき、属性付き型を取得できる |
| アイコン | `MainToolbarContent(string, Texture2D, string)`を確認済み。`EditorSymphonyConstant.FRAMEWORK_PATH`はPackage／Assets双方の基準パスを返す |
| Scene Load設定 | 既存の`SymphonyConfigLocator.GetConfig<SceneLoadConfig>()`はEditorコードから参照済み |
| 設定の変更 | 既存の`SerializedObject`、`Undo.RecordObject`、`EditorUtility.SetDirty`、`AssetDatabase.SaveAssets`経路を維持できる |
| テストからの参照 | Editorテストasmdefは既存の`SymphonyMainToolbar`と`SceneLoadConfig`へ到達済みで、internal型も参照できる |

既存の`RefreshSceneInitializationToggle()`は`SceneLoadConfigDrawer`からだけ呼ばれている。プルダウンのチェック状態は開くたびに読み直すため、このRefresh経路は削除できる。

## エラー処理

| 状況 | 扱い |
| --- | --- |
| Configが未生成 | `Scene Init`を未チェック、操作不能で表示する |
| Configのシリアライズ済みフィールドが見つからない | 変更せずfalseを返す |
| テーマ用アイコンを読み込めない | テキストだけのプルダウンを表示する |
| 登録型がinterfaceを実装しない、abstract、引数なしで生成できない | その型を除外し、他の項目は表示する |
| 項目生成中に例外 | 該当型を除外して例外をConsoleへ記録し、メニュー全体は開く |

catalogの障害隔離はEditor UI全体を壊さないために行う。通常の登録型はコンパイルとテストで条件を満たすことを確認し、無効な型を黙って正常扱いしない。

## 影響範囲

公開API、Runtime挙動、`SceneLoadConfig`のシリアライズ形式、保存先は変わらない。`Scene Init`の入口だけが、メインツールバー上の単独トグルから`Symphony Framework > Scene Init`へ変わる。

Unity 6000.3未満では既存と同じくファイル全体を`UNITY_6000_3_OR_NEWER`で除外する。外部Toolbar拡張パッケージとUnity内部型への反射は追加しない。`TypeCache`と`MainToolbarDropdown`はいずれもUnityEditorの公開APIを使用する。

## テストの置き場と種別

`Assets/SymphonyFrameWork/Tests/Editor/SymphonyMainToolbarTests.cs`のEditModeテストを更新する。

| テスト | 方法 |
| --- | --- |
| `CreateToolbarMenu_HasMainToolbarRegistration_UsesRightDock` | factoryの属性をreflectionで取得し、固定パスと右側dockを比較する |
| `CreateToolbarMenu_ReturnsDropdownWithFrameworkLabelAndIcon` | factoryの戻り値を`MainToolbarDropdown`として取得し、contentの表示名と現在テーマ用Textureを比較する |
| `ToolbarIcons_PackageAssets_ExistAndAreReadable` | `EditorSymphonyConstant.FRAMEWORK_PATH`から明暗両PNGを`Texture2D`として読み込む |
| `ThirdPartyNotices_LucideLicense_ContainsRequiredNotices` | 通知ファイルを読み、Lucideの著作権表示とISC／MITの許諾文が含まれることを確認する |
| `CreateItems_RegisteredSceneItem_DiscoversSceneInitializationItem` | catalogへ実際のTypeCache候補を渡し、`Scene Init`項目が1件含まれることを確認する |
| `OrderItems_MixedPriority_SortsByPriorityThenPath` | マーカーを持たないテストfakeを明示的に並べ替え処理へ渡し、優先度とパス順を比較する |
| `SceneInitializationItem_ConfigEnabled_IsCheckedAndEnabled` | 一時`SceneLoadConfig`を項目へ注入できる内部factory経路で生成し、状態を比較する |
| `SceneInitializationItem_NullConfig_IsUncheckedAndDisabled` | null Configで生成し、操作不能の状態を比較する |
| `Execute_SceneInitializationDisabled_EnablesSerializedConfig` | 一時Configで操作を実行し、公開getterとシリアライズ済みフィールドがtrueになることを確認する |

GUIのクリック自体は自動テスト対象にしない。項目の発見、順序、表示状態、実行結果をUnity UI APIの外側で確認できる形に分ける。

## 動作確認手順

### 自動確認

1. `python scripts/verify_round.py`でコンパイルのError 0／Warning 0、EditMode／PlayMode全数成功、Play Mode 2往復を確認する。
2. Unityのメインツールバーをスクリーンショットし、右側に音符アイコン付きの`Symphony Framework`が単独表示されることを確認する。
3. `python scripts/build_module_docs.py --check`と`python scripts/release_round.py preflight`を通す。

### 人の操作が必要な確認

1. `Symphony Framework`を押すと、`Scene Init`が表示される。
2. `Scene Init`を選ぶたびチェックが反転し、`SceneLoadConfig.asset`の値と一致する。
3. Project SettingsまたはInspector側で値を変更し、ツールバーを開いたままにせず次に開いたメニューへ新しい状態が反映される。
4. ツールバーのコンテキストメニューから項目を非表示／再表示でき、Editor再起動後も表示が崩れない。
5. 現在のUnityテーマで、アイコンが背景へ埋もれず、文字と同じ高さに収まる。

## バージョン判断

`6.2.0`から`6.2.1`へのパッチ更新とする。公開APIとシリアライズ形式を変更せず、6.2.0で追加したEditor機能の表示構造と内部拡張性を修正するためである。

## このRoundで触るバージョン関連ファイル

| ファイル | 変更 |
| --- | --- |
| `Assets/SymphonyFrameWork/package.json` | `version`を`6.2.1`へ更新 |
| `Assets/SymphonyFrameWork/CHANGELOG.md` | `6.2.1`の`Change`としてプルダウン化と内部登録契約を追加 |
| `Assets/SymphonyFrameWork/README.md` | 現在バージョンを`6.2.1`へ更新し、索引の入口を`Symphony Framework > Scene Init`へ変更 |

`Third Party Notices.md`はバージョン値を持たないが、Lucide派生アセットと同じRoundへ含める。アイコンだけを転載して通知を欠落させない。

`AGENTS.md`、`Documentation~/AgentUsage.md`、`Documentation~/Architecture.md`、Sampleは変更しない。利用側コードの契約、公開型、アセンブリ構成、Runtime初期化、Sample操作を変えないためである。ワークスペース側の`Documentation/CONTRIBUTING.md`と`AGENTS.md`にも、今回の表示パスや内部型を正本として記載した箇所はない。
