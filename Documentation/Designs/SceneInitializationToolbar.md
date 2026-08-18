# 初期化シーンツールバー

## 目的

`SceneLoadConfig._isResetAndLoadOnPlay` はプロジェクト共有の設定アセットに保存されており、デバッグ対象のシーンをそのまま再生したい場合も設定アセットを選択して Inspector から変更する必要がある。Unity Editor のメインツールバーへ同じ設定を表すトグルを追加し、再生前に短い操作で初期シーン処理を有効・無効にできるようにする。

Issue #189 のコメントにある今後の拡張性は、Unity 6000.3 で追加された公式の `MainToolbarElementAttribute` を登録口として満たす。独自の反射、外部の ToolbarExtender パッケージ、汎用 Registry は追加せず、今後の項目は属性付き factory メソッドを追加して同じグループへ登録する。

## Round 分割

1 Round で実施する。公式 API による登録基盤、初期化シーントグル、設定アセットへの保存、テスト、利用者向け文書を合わせても差分は約 12 ファイルで、単独で検証・リリースできるため分割しない。

この Round に含めるもの:

| 対象 | 内容 |
| --- | --- |
| メインツールバー | `Symphony Framework` グループへ `Scene Init` トグルを登録する |
| 設定変更 | `SceneLoadConfig.IsResetAndLoadOnPlay` と同じシリアライズ済みフィールドを Undo、dirty、保存へ反映する |
| 追従 | ツールバー操作後と Inspector 操作後に公式 `MainToolbar.Refresh` で表示を更新する |
| 互換性 | Unity 6000.3 以降だけを `UNITY_6000_3_OR_NEWER` でコンパイルし、6000.0〜6000.2 では機能を提供しない |
| 文書 | Scene Loader の Editor 機能と Editor 機能索引へ入口・制約を記載する |

この Round に含めないもの:

| 対象 | 理由 |
| --- | --- |
| 外部 ToolbarExtender パッケージ | Unity 6000.3 の公式 API で目的を満たし、反射と追加依存が不要なため |
| 6000.0〜6000.2 の反射 fallback | Unity 内部実装への依存と保守分岐を増やすため |
| 利用側から任意要素を登録する Symphony Framework 独自公開 API | Unity 自身の属性が拡張契約を提供しており、重複する公開面を追加する根拠がないため |
| 実行中のシーン初期化処理の再実行 | トグルは次回の Play Mode 開始時に読む設定だけを変更するため |

パッケージの構造や公開 API の入口は変えない。`rg -n "README\.md|Documentation~/|EditorTools\.md|AgentUsage\.md" Documentation/ AGENTS.md` の結果では、今回古くなるワークスペース側の記述はない。

## 公開 API

新しい Symphony Framework の公開 API は追加しない。ツールバー要素は `SymphonyFrameWork.Editor` アセンブリ内の `internal` factory として Unity に発見させる。

利用する Unity Editor API:

```csharp
[MainToolbarElement(
    "Symphony Framework/Scene Init",
    defaultDockPosition = MainToolbarDockPosition.Right)]
internal static MainToolbarElement CreateSceneInitializationToggle()
```

`MainToolbarElementAttribute` は属性付き static メソッドを探索し、`MainToolbarElement` またはその列挙をツールバーへ登録する公式拡張点である。`Scene Init` は `MainToolbarToggle` とし、factory が呼ばれるたびに `SceneLoadConfig` の現在値から表示を再構築する。

参考:

- [Unity MainToolbar](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Toolbars.MainToolbar.html)
- [Unity MainToolbarElementAttribute](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Toolbars.MainToolbarElementAttribute.html)
- [Unity MainToolbarToggle](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Toolbars.MainToolbarToggle.html)

## ファイル構成

| 種別 | パス | 名前空間・責務 |
| --- | --- | --- |
| 新規 | `Assets/SymphonyFrameWork/Editor/Toolbar/Internal/SymphonyMainToolbar.cs` | `SymphonyFrameWork.Editor`。公式属性による要素登録、Config の取得、トグル操作の保存、表示更新 |
| 変更 | `Assets/SymphonyFrameWork/Editor/Configs/Drawer/SceneLoadConfigDrawer.cs` | Inspector から対象フラグを変更した場合にツールバーを更新 |
| 新規 | `Assets/SymphonyFrameWork/Tests/Editor/SymphonyMainToolbarTests.cs` | 登録属性と Config 変更 helper の EditMode テスト |
| 変更 | `Assets/SymphonyFrameWork/Documentation~/Modules/SceneLoader.md` | `Scene Init` の入口、保存先、反映時点を記載 |
| 変更 | `Assets/SymphonyFrameWork/Documentation~/EditorTools.md` | `## 一覧` とメインツールバー拡張の横断説明を追加 |
| 自動生成 | `Assets/SymphonyFrameWork/Documentation~/Html/` | Markdown 更新後に `scripts/build_module_docs.py` で再生成 |
| 変更 | `Assets/SymphonyFrameWork/CHANGELOG.md` | Editor 機能追加と Unity 6000.3 条件を記載 |
| 変更 | `Assets/SymphonyFrameWork/package.json` | バージョンを更新 |
| 変更 | `Assets/SymphonyFrameWork/README.md` | 現在バージョンと Editor 機能索引へ追記 |

新しい `.cs` と同じ場所に Unity が生成する `.meta` を含める。`Editor/Toolbar/Internal/` は内部 Editor View の配置であり、名前空間に `Internal` は含めない。

## 依存方向

`SymphonyMainToolbar` は Editor の View であり、Runtime Infrastructure の `SceneLoadConfig` と `SymphonyConfigLocator`、UnityEditor の Toolbar／AssetDatabase APIを参照する。依存方向は `Editor -> Runtime -> Core` のままで、Runtime または Core から `UnityEditor` を参照しない。

状態変更は新しい Runtime Service を作らず、既存のプロジェクト共有 Config のシリアライズ済みフィールドへ委譲する。この機能はゲーム実行中の Scene Loader 状態を変更する Command ではなく、次回起動用の Editor 設定操作であるためである。

`[MainToolbarElement]` は `MenuItem` や `SettingsProvider` と同じ Unity の discovery callback として扱う。package-wide な初期化、購読、ポーリングは持たせず、`SymphonyEditorOrchestrator` の所有権を迂回しない。

## アクセス手段の検証

| 経路 | 確認結果 |
| --- | --- |
| Editor から `SceneLoadConfig` へ到達 | 型は `internal` だが `Runtime/AssemblyInfo.cs` が `SymphonyFrameWork.Editor` へ `InternalsVisibleTo` を付与している |
| Config の取得 | `SymphonyConfigLocator.GetConfig<SceneLoadConfig>()` は `Resources/SymphonyFrameWork/SceneLoadConfig` を読み込む既存経路で、Runtime の `SymphonyOrchestrator` と同じ設定へ到達する |
| 対象値の読取 | `SceneLoadConfig.IsResetAndLoadOnPlay` が `_isResetAndLoadOnPlay` を返す |
| 対象値の変更 | `SerializedObject.FindProperty("_isResetAndLoadOnPlay")` は既存 `SceneLoadConfigDrawer` が使用中で、フィールドは `private` でも Editor から変更できる |
| 永続化 | `Undo.RecordObject`、`SerializedObject.ApplyModifiedProperties`、`EditorUtility.SetDirty`、`AssetDatabase.SaveAssets` を使用できる |
| 表示更新 | `MainToolbar.Refresh("Symphony Framework/Scene Init")` が同じパスの factory を再評価する公式経路である |
| Unity 版 | ワークスペースは 6000.3.10f1。公式 Main Toolbar API は 6000.3 以降のため、条件コンパイルが必要である |

既存コードに Toolbar 用の反射、`#if`、コピー実装はなく、`Packages/manifest.json` と `package.json` に ToolbarExtender 依存もない。したがって維持すべき既存回避策はない。

## エラー処理

`SceneLoadConfig` が未生成またはロード不能の場合、factory は false 表示かつ操作不能なトグルを返し、例外を投げない。通常は `SymphonyEditorOrchestrator` が設定を生成するが、導入直後やアセット削除直後でもメインツールバー全体の構築を壊さないためである。

操作 callback から Config が取得できなかった場合も保存せず、ツールバーを再評価する。Config が存在する場合は Undo 登録後に対象の `SerializedProperty` だけを変更し、dirty と保存を行う。`AssetDatabase.Refresh` は不要であり呼ばない。

保存時の Unity API 例外は握りつぶさない。通常の Editor 操作として Console へ到達させ、トグル表示だけ成功したように見える状態を避ける。

## 影響範囲

- 公開 Runtime API、名前空間、asmdef 参照、シリアライズフィールド名は変更しない。
- 既存の `SceneLoadConfig.asset` をそのまま使用するため移行は不要である。
- トグル変更はプロジェクト共有アセットの差分になる。開発者個人だけの一時設定ではなく、Inspector で同じ値を変える既存挙動と一致する。
- Play Mode 開始後に値を変えても、すでに完了した初期シーン処理は巻き戻さない。変更は次の Play Mode 開始から反映する。
- Unity 6000.0〜6000.2 ではパッケージの既存機能はコンパイル・動作するが、メインツールバー項目は表示されない。
- ユーザーがメインツールバーのカスタムレイアウトから項目を非表示にした場合は、ツールバーのコンテキストメニューから `Symphony Framework/Scene Init` を再表示する。

## テストの置き場と種別

`Assets/SymphonyFrameWork/Tests/Editor/SymphonyMainToolbarTests.cs` へ EditMode テストを追加する。テストは `UNITY_6000_3_OR_NEWER` 条件内に置く。

| テスト | 検証内容と方法 |
| --- | --- |
| `CreateSceneInitializationToggle_HasMainToolbarRegistration_UsesSymphonyGroup` | factory を reflection で取得し、`MainToolbarElementAttribute` の path と右側の既定 dock 位置を比較する |
| `ApplySceneInitializationValue_FalseToTrue_UpdatesSerializedConfig` | `ScriptableObject.CreateInstance<SceneLoadConfig>()` を作り、保存を伴わない internal helper で true を適用し、公開 getter と `SerializedProperty.boolValue` の両方を確認する |
| `ApplySceneInitializationValue_NullConfig_ReturnsFalse` | null を渡して例外を出さず false を返すことを確認し、未生成 Config 時の toolbar callback を再現する |

準備には実プロジェクトの `SceneLoadConfig.asset` を使わず、テスト終了時に一時 `ScriptableObject` を `DestroyImmediate` する。プロジェクト共有設定をテストから書き換えない。

## 動作確認手順

自動確認:

1. `python scripts/verify_round.py` で compile の Error 0・Warning 0、EditMode／PlayMode 全数成功、Play Mode 2往復、Enter Play Mode Options を確認する。
2. Unity Editor のメインツールバーをスクリーンショットし、`Scene Init` が `Symphony Framework` グループの項目として表示できることを確認する。
3. `SceneLoadConfig.asset` の値を Editor API 経由で false／true に変更し、それぞれで `MainToolbar.Refresh` 後に factory が同じ値を読むことを EditMode テストで裏付ける。
4. `python scripts/build_module_docs.py --check` と `python scripts/release_round.py preflight` を通す。

人の操作が必要な確認:

1. ツールバーの `Scene Init` をクリックし、`Assets/Resources/SymphonyFrameWork/SceneLoadConfig.asset` の対象チェックが同じ値へ変わり、Undo で戻せることを確認する。
2. `Scene Init` を off にしてデバッグ対象シーンから Play Mode を開始し、設定済みの初期シーンへ置き換わらないことを確認する。終了後に on へ戻し、次の開始では初期シーン処理が走ることを確認する。
3. `SceneLoadConfig.asset` を Inspector で開いたまま対象チェックを変更し、ツールバーを作り直したり Editor を再起動したりせず表示が追従することを確認する。
4. カスタム項目を非表示にしてから、ツールバーのコンテキストメニューで再表示できることを確認する。

Editor のメインツールバーは PlayMode Game View 用の `uloop simulate-mouse-*` では操作できないため、クリック、Undo、レイアウトの表示切り替えは人の確認へ残す。スクリーンショットによる表示と、同じ callback/helper の状態変更は自動で確認する。

## バージョン判断

`6.2.0` のマイナー更新とする。公開 Runtime API と既存シリアライズ形式は維持するが、利用者が操作できる後方互換な Editor 機能を追加するためである。

## この Round で触るバージョン関連ファイル

| ファイル | 変更 |
| --- | --- |
| `Assets/SymphonyFrameWork/package.json` | `version` を `6.2.0` へ更新 |
| `Assets/SymphonyFrameWork/CHANGELOG.md` | `6.2.0` の `Add` と Unity 6000.3 条件を追加 |
| `Assets/SymphonyFrameWork/README.md` | 「現在のバージョン」を `6.2.0` へ更新し、Editor・デバッグ支援へメインツールバー項目を追加 |
| `Assets/SymphonyFrameWork/Documentation~/Modules/SceneLoader.md` | Editor 機能へ `Scene Init` を追加 |
| `Assets/SymphonyFrameWork/Documentation~/EditorTools.md` | `## 一覧` とメインツールバー拡張節を追加 |
| `Assets/SymphonyFrameWork/Documentation~/Html/` | 上記 Markdown から再生成 |

`AGENTS.md`、`Documentation~/AgentUsage.md`、`Documentation~/Architecture.md`、Sample は更新しない。公開 API、AI の利用側コード契約、アセンブリ構成、Runtime 初期化構成、サンプル操作は変わらないためである。
