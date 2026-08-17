# Debug HUDショートカット

## 目的

Issue #103 の Debug HUD を、デバッグ情報の登録時に自動表示する挙動から、設定可能な Input Action で表示・非表示を切り替える挙動へ変更する。既定操作は `Shift + D + P` とし、キーボード以外を含む複数プラットフォーム向けのBindingを `Project Settings > SymphonyFrameWork` から追加・変更できるようにする。

HUDの初期化、入力監視、描画、公開APIはEditorとDevelopment Buildでだけ機能させ、通常のPlayerビルドではGameObject生成、入力購読、登録コールバックの保持を行わない。Issue #108 は #103 の完了後に行う指定のため、同じRoundでREADMEの参考ライブラリへSRDebuggerを追加する。

あわせて、Unityバージョンやプロジェクトテンプレートによる標準導入パッケージの差へ依存しないよう、Framework本体と同梱テストが直接使用するUnity提供パッケージを `package.json` の `dependencies` へすべて明示する。バージョンはこのRoundを検証するホストの `Packages/packages-lock.json` と一致させる。

## Round分割

1 Roundで実施する。Issue #108 はIssue #103の実装を前提とする同じDebug HUD文書変更であり、単独リリースする機能ではない。変更はDebug HUDのRuntime実装、設定画面、設定アセット生成、テスト、利用者向け文書、パッケージ依存、バージョン情報に閉じ、20ファイル前後に収まる。

## 公開API

既存の `SymphonyFrameWork.Debugger.HUD.SymphonyDebugHUD` と次のシグネチャは維持する。新しい公開型・公開メンバーは追加しない。

| API | Development Build / Editor | 通常のPlayerビルド |
| --- | --- | --- |
| `Show()` | HUDを明示表示する | 何もしない |
| `Hide()` | HUDを非表示にする | 何もしない |
| `AddText(Func<string>)` | 表示内容だけを登録し、HUDは表示しない | 何も保持しない |
| `RemoveText(Func<string>)` | 登録済みの表示内容を解除する | 何もしない |
| `AddText(string, float, Color, CancellationToken)` | 指定期間だけ表示内容を登録し、HUDは表示しない | 完了済みとして戻り、待機も登録もしない |

通常ビルドで型自体をコンパイルから除外すると利用側コードのビルドが失敗するため、公開面は残して無処理にする。`public` の根拠は既存のDebug HUD公開エントリポイントであり、今回その範囲を広げない。

## ファイル構成

| パス | 名前空間 | 変更内容 |
| --- | --- | --- |
| `Runtime/Debug/DebugHUD/SymphonyDebugHUD.cs` | `SymphonyFrameWork.Debugger.HUD` | 公開入口、表示内容の登録、遅延Drawer、Development Buildガードを管理 |
| `Runtime/Debug/DebugHUD/Internal/SymphonyHUDShortcutListener.cs` | `SymphonyFrameWork.Debugger.HUD` | Input Actionの複製・有効化・解除とトグル通知を所有する内部Component |
| `Runtime/Debug/DebugHUD/Internal/SymphonyHUDDrawer.cs` | `SymphonyFrameWork.Debugger.HUD` | 登録済み表示内容を受けて描画する既存View |
| `Runtime/Configs/Internal/DebugHUDConfig.cs` | `SymphonyFrameWork.Config` | プロジェクト共有のInput Actionを保持する内部Config |
| `Runtime/Orchestrator/Internal/SymphonyOrchestrator.cs` | `SymphonyFrameWork.Orchestrator` | `Debug.isDebugBuild`、Config、FactoryをDebug HUDへ注入 |
| `Editor/Configs/SymphonyConfigManager.cs` | `SymphonyFrameWork.Editor` | `DebugHUDConfig`の生成を既存の集約初期化へ追加 |
| `Editor/SettingProvider/SymphonySettingProvider.cs` | `SymphonyFrameWork.Editor.SettingProvider` | Input Actionを標準PropertyFieldで編集し、Configアセットへ保存 |
| `Tests/Editor/SymphonyDebugHUDTests.cs` | `SymphonyFrameWork.Tests` | Development Build判定、非自動表示、表示切替、リセットを検証 |
| `Tests/Editor/DebugHUDConfigTests.cs` | `SymphonyFrameWork.Tests` | 既定BindingとConfig生成を検証 |
| `Tests/Editor/PackageDependencyTests.cs` | `SymphonyFrameWork.Tests` | 直接使用するUnity提供パッケージがmanifestへ明示されていることを検証 |

新規の内部実装は既存のDebug HUD直下の `Internal/`、Configは規約どおり `Runtime/Configs/Internal/` に置き、`Internal` は名前空間へ含めない。新規 `.cs` にはUnityが生成する `.meta` を対で含める。

## 依存方向

`SymphonyDebugHUD`は既存のAdaptor兼View専用エントリポイント、`SymphonyHUDDrawer`と`SymphonyHUDShortcutListener`はView、`DebugHUDConfig`とInput System依存はInfrastructure、`SymphonyOrchestrator`はCompositionに属する。

```text
SymphonyOrchestrator
  ├─ DebugHUDConfig（Resources）
  ├─ Debug.isDebugBuild
  └─ ISystemObjectFactory
          ↓ 注入
SymphonyDebugHUD ──生成──> SymphonyHUDShortcutListener ──InputAction──> Toggle
       └─遅延生成──> SymphonyHUDDrawer
```

RuntimeからEditorへの参照は追加しない。Runtime asmdefから `Unity.InputSystem` へ直接参照を追加する。外部パッケージ固有の購読とCloneは `SymphonyHUDShortcutListener` に閉じ、CompositionがConfigの具象値を渡す。

## package.jsonの依存

`package.json` へ次の直接依存を明示する。既存2件はホストで実際に検証している版へ同期し、新規のInput System、同梱テスト、組み込みモジュールも固定する。

| パッケージ | バージョン | Framework内の直接利用 |
| --- | --- | --- |
| `com.unity.addressables` | `2.9.1` | `UnityEngine.AddressableAssets`、同パッケージ内のResource Manager |
| `com.unity.inputsystem` | `1.18.0` | Debug HUD Shortcutの `InputAction` |
| `com.unity.nuget.newtonsoft-json` | `3.2.2` | Save Data、MCP、Asset Store ToolsのJSON処理 |
| `com.unity.test-framework` | `1.6.0` | 同梱するEditMode / PlayModeテストのTest Runner |
| `com.unity.modules.audio` | `1.0.0` | `AudioSource`、`AudioMixer` |
| `com.unity.modules.imgui` | `1.0.0` | Runtime HUDの `GUI` とEditor GUI |
| `com.unity.modules.jsonserialize` | `1.0.0` | `JsonUtility` |
| `com.unity.modules.uielements` | `1.0.0` | `VisualElement`、UXML/UITK |

`UnityEngine.CoreModule`、`UnityEditor`、Scene Management、ProfilerはUnity Editor本体に属し、独立したUPM依存名を持たないため列挙対象外とする。Addressables内のResource ManagerやTest Framework内のNUnitなど、直接利用パッケージがmanifestで宣言する推移依存は重複追加しない。ただしUIElementsのようにFramework自身も直接型を使用するものは、別パッケージから推移導入される場合でも明示する。

## 状態とライフサイクル

- `SymphonyOrchestrator` は `Debug.isDebugBuild` がtrueの場合だけShortcut Listenerを生成する。Unity Editorではtrue、Development Buildではtrue、通常のPlayerビルドではfalseになる。
- Shortcut Listenerは注入された `InputAction` を `Clone()` して所有する。`OnEnable`または設定直後に有効化し、`OnDisable`でイベント解除・無効化、`OnDestroy`でDisposeする。
- HUDのDrawerは初回の明示 `Show()` またはShortcut発火まで生成しない。`AddText`だけでは生成しない。
- 表示内容のデリゲート一覧は `SymphonyDebugHUD` が保持し、Drawer再生成時に登録し直す。`Hide()`はDrawerだけを破棄するため、再表示後も登録内容を維持する。
- `ResetRuntimeState()` はShortcut Listener、Drawer、登録一覧、Factory、Development Build判定をすべて解放・初期化する。Domain Reload無効で2回Play Modeへ入っても前回のAction購読を残さない。
- 通常ビルドでは初期化済みフラグだけを確定し、Componentと登録一覧を作らない。公開APIは無処理で戻る。

## 設定と既定値

`DebugHUDConfig` は `Assets/Resources/SymphonyFrameWork/DebugHUDConfig.asset` に自動生成する。プロジェクトとプラットフォーム間で共有する入力設定であるため、個人用の `UserSettings` には置かない。

既定ActionはButton型で、`ButtonWithTwoModifiers` CompositeのModifier 1を `<Keyboard>/shift`、Modifier 2を `<Keyboard>/d`、Buttonを `<Keyboard>/p` とする。Input Action自体をSettings Providerへ描画するため、利用者はGamepadなどの通常Bindingや別Compositeを複数追加できる。

設定画面を開いた時点ではConfig生成を開始しない。Configが無い場合は既存Save System設定と同様に案内と「設定アセットを生成」ボタンを出し、押下時だけ `SymphonyEditorOrchestrator.RequestPackageSetup()` へ委譲する。

## アクセス手段の検証

| 経路 | 確認結果 |
| --- | --- |
| Runtime CompositionからConfig取得 | `SymphonyConfigLocator.GetConfig<T>()` は `internal ScriptableObject` をResourcesから取得でき、`SymphonyOrchestrator`と同じRuntime asmdefから参照できる |
| Config生成 | `SymphonyConfigManager.AllConfigCheck()` は `FileCheck<T>() where T : ScriptableObject` を持ち、既存3 Configと同じ経路へ `DebugHUDConfig` を追加できる |
| Editorから内部Config編集 | Runtime asmdefはEditor asmdefへ `InternalsVisibleTo` 済みで、既存 `SaveDataSettingProvider` が内部 `SaveDataConfig` を `SerializedObject` で編集している |
| テストから内部型へ到達 | EditorテストasmdefはRuntime/Core/Editorを参照し、パッケージの `InternalsVisibleTo` により `internal` APIを直接検証できる |
| Drawer生成有無 | `SymphonyLazyObject<T>` は `IsAlive` と `TryGetValue` を持ち、生成せずに現在の表示状態を判定できる |
| GameObject生成 | `ISystemObjectFactory.CreateComponent<T>()` はinternalだがRuntimeとテストから参照可能で、Fake実装により生成回数を測定できる |
| Input System | ホストには `com.unity.inputsystem` 1.18.0 と `Unity.InputSystem` asmdefが存在する。パッケージ側には未参照のため、asmdef参照とpackage依存の両方を追加する必要がある |
| Unity提供パッケージの版 | `Packages/packages-lock.json` でAddressables 2.9.1、Input System 1.18.0、Newtonsoft Json 3.2.2、Test Framework 1.6.0、使用する組み込みModule 1.0.0を確認した |
| 組み込みModuleの直接利用 | ソース検索でAudio、IMGUI、JsonUtility、UIElementsを確認した。Physics、UGUIなどはFramework本体から直接使用していない |

既存の `SymphonyDebugHUD` が `SymphonyLazyObject` を使う理由は、Unityで破棄済みになったObjectをDomain Reload無効時にも検知し、再生成できるようにするためである。この経路は残し、Input監視だけを常駐Componentへ分ける。

## エラー処理

- Development Build / EditorでOrchestrator初期化前に公開APIを呼んだ場合は、既存どおり `SymphonyNotInitializedException` を投げる。
- 通常ビルドでは機能を含めない契約を優先し、公開APIの引数検証も登録も行わず無処理にする。
- ConfigまたはActionがnull、ActionにBindingが無い場合は、既定ActionへフォールバックしてShortcutを失わないようにする。利用側の通常分岐であるため例外にはしない。
- Shortcut Listenerは自身がCloneしたActionだけを有効化・無効化・Disposeし、Configアセットが保持するActionの状態を変更しない。

## 影響範囲

- `AddText` 系がHUDを自動表示しなくなる。表示にはShortcutまたは明示 `Show()` が必要になる。
- `Hide()` 後も登録済みの継続テキストを保持し、次回表示時に復元する。`RemoveText`との対は維持する。
- 通常PlayerビルドではDebug HUDの公開APIが無処理になる。型とシグネチャは残るため、利用側コードのコンパイル互換性は保つ。
- `DebugHUDConfig`という新しいResources設定アセットとInput Systemパッケージ依存を追加する。
- 既存のシリアライズ済み型・フィールドは変更しない。

## テストの置き場と種別

EditModeテストへ追加する。Fake `ISystemObjectFactory` が実GameObjectへComponentを付与しつつ型別生成回数を記録し、各テストのTearDownで `ResetRuntimeState()` と生成Object破棄を行う。

| テスト名 | 検証内容と書き方 |
| --- | --- |
| `Initialize_NonDevelopmentBuild_DoesNotCreateComponents` | `isDebugBuild: false`で初期化し、Fakeの生成回数が0のままか測る |
| `AddText_NonDevelopmentBuild_DoesNotRetainCallback` | 通常ビルド条件で副作用を持つFuncを渡し、Show相当操作後も評価・Component生成されないことを測る |
| `Initialize_DevelopmentBuild_CreatesShortcutListenerOnly` | `isDebugBuild: true`直後はListenerだけ1件、Drawerは0件と型別回数で測る |
| `AddText_Hidden_DoesNotCreateDrawer` | 初期化直後に継続テキストを登録し、Drawer生成回数が増えないことを測る |
| `Show_AfterAddText_CreatesDrawerWithRegisteredText` | 登録後にShowし、Drawerが1件生成され登録数が一致することをinternal読み取り値で測る |
| `Hide_ThenShow_PreservesRegisteredText` | HideとShowを往復し、再生成Drawerへ同じ登録が戻ることを測る |
| `Toggle_VisibleState_SwitchesDrawer` | internalなToggle入口を2回呼び、Drawerの生存状態がtrue→falseになることを測る |
| `ResetRuntimeState_AfterInitialization_ReleasesOwnedState` | Reset後にListenerとDrawerが破棄対象になり、次回初期化で二重登録されないことを生成回数と状態で測る |
| `DefaultToggleAction_NewConfig_UsesShiftDPTwoModifierComposite` | `DebugHUDConfig`の既定ActionのComposite名と3つのpart binding pathを列挙して比較する |
| `RequestPackageSetup_AfterInitialization_CreatesDebugHUDConfig` | 既存の設定生成テストへ `SymphonyConfigLocator.GetConfig<DebugHUDConfig>()` の非null確認を追加する |
| `PackageManifest_DirectUnityDependencies_AreDeclaredAtTestedVersions` | `package.json`をJSONとして読み、上記8依存の存在とホストで検証した版を辞書比較する |

Input Actionの実デバイス入力とSettings ProviderのPropertyField操作はUnity Test Runnerから安定して操作できないため、Actionの既定Binding、ListenerのAction所有、Toggle後の状態を分けて自動検証する。GUIでBindingを変更する操作は人の確認項目とする。

## 動作確認手順

### 自動確認

1. `python scripts/verify_round.py` でConsoleクリア、コンパイルのエラー0・警告0、EditMode/PlayMode全件成功、Play Mode 2往復、Enter Play Mode Optionsを確認する。
2. `rg -n "UnityEditor" Assets/SymphonyFrameWork/Runtime Assets/SymphonyFrameWork/Core -g '*.cs'` で今回のRuntime変更にEditor参照が無いことを確認する。
3. `rg -n "UNITY_INCLUDE_TESTS" Assets/SymphonyFrameWork/Tests -g '*.asmdef'` でテストAssemblyのビルド除外を確認する。
4. Development Buildと通常ビルドの分岐は、Orchestratorから渡すboolを変えたEditModeテストでComponent生成と登録副作用の差を確認する。
5. `PackageDependencyTests` と `Packages/packages-lock.json` を照合し、Frameworkが直接使うUnity提供パッケージの宣言版と検証環境の解決版が一致することを確認する。

### 人が操作する確認

1. `Project Settings > SymphonyFrameWork` を開き、Debug HUD Shortcutに既定の `Shift + D + P` Compositeが表示されることを確認する。
2. Play Modeへ入り、Debug HUD利用コードが `AddText` を呼んでもHUDが自動表示されないことを確認する。
3. `Shift + D + P` を押すたびにHUDが表示・非表示へ切り替わることを確認する。
4. HUDを非表示にしている間も登録内容を更新し、再表示時に最新内容が出ることを確認する。
5. 設定画面でGamepadなど別Bindingを追加し、設定画面を開いたまま変更が保存され、次のPlay ModeでそのBindingが動くことを確認する。
6. Play Mode開始・終了を2回繰り返し、1回の入力で複数回切り替わらないことを確認する。
7. Development Build Playerでは動作し、Development Buildを外したPlayerではHUD用GameObjectが生成されず、Shortcutでも表示されないことを確認する。

## バージョン判断

`6.0.0` のメジャー更新とする。公開メソッドのシグネチャは維持するが、`AddText`の自動表示を廃止し、通常Playerビルドでは `Show` を含む公開操作を無処理へ変えるため、既存利用コードから観測できる契約変更である。設定追加だけならマイナーだが、Development Build限定化は後方互換な追加ではない。

## この Round で触るバージョン関連ファイル

| ファイル | 更新内容 |
| --- | --- |
| `Assets/SymphonyFrameWork/package.json` | versionを `6.0.0` にし、直接使用するUnity提供パッケージ8件を検証版で明示 |
| `Assets/SymphonyFrameWork/Core/SymphonyConstant.cs` | `VERSION`を `6.0.0` に同期 |
| `Assets/SymphonyFrameWork/CHANGELOG.md` | Issue #103 / #108、Development Build限定、移行方法、設定場所を先頭へ追加 |
| `Assets/SymphonyFrameWork/Documentation~/Modules/Debug.md` | Shortcut、設定、Build条件、AddTextと表示の分離を記載 |
| `Assets/SymphonyFrameWork/Documentation~/EditorTools.md` | Framework設定項目とDebugHUDConfigの保存先を更新 |
| `Assets/SymphonyFrameWork/README.md` | 必要なパッケージの説明と、参考・謝辞へのSRDebuggerを追加 |
| `Assets/SymphonyFrameWork/Documentation~/Html/**` | Markdown正本から再生成 |

公開型・namespaceの追加は無いため `Documentation~/AgentUsage.md` は変更しない。メニューパスも維持するためEditorToolsの一覧行は残し、設定項目とConfig一覧だけを更新する。`[Obsolete]` の追加・削除は無いため `Deprecations.md` は変更しない。

## 実施レポート

実施日: 2026-08-17 / バージョン: 6.0.0 / PR: [#183](https://github.com/HIBIKI5201/SymphonyFramework/pull/183)

### 実装した内容

- `DebugHUDConfig` と `SymphonyHUDShortcutListener` を追加し、既定値 `Shift + D + P` のInput Actionを複製して購読・解放するライフサイクルを実装した。
- `SymphonyDebugHUD` を表示内容の保持とDrawerの遅延生成へ分離した。`AddText` は自動表示せず、`Hide` 後も登録内容を維持し、再表示時に復元する。
- `SymphonyOrchestrator` から `Debug.isDebugBuild` を注入し、通常のPlayerビルドでは入力監視、Drawer、登録内容を生成しない無処理契約にした。
- `Project Settings > SymphonyFrameWork > Framework Settings` へInput Actionの編集欄を追加し、`DebugHUDConfig` の自動生成経路へ統合した。
- README、Debugモジュール文書、Editor Tools、サンプル、CHANGELOGと生成HTMLを実装内容へ同期し、SRDebuggerを参考ライブラリへ追加した。
- Frameworkと同梱テストが直接使うUnity提供パッケージ8件を `package.json` へ明記し、名前と検証版を `PackageDependencyTests` で固定した。
- Debug HUDとConfigのEditModeテストを追加し、公開型テスト網羅の既知残作業から `SymphonyDebugHUD` を除外した。

### 設計から変えた点

- Development Build条件でFactoryまたはListenerの初期化に失敗した場合、半初期化状態を残さず `ResetRuntimeState()` でロールバックしてから例外を再送出する処理を追加した。設計のエラー処理を強化する変更で、公開契約と依存方向は変えていない。
- それ以外の設計変更は無し。

### 検証結果

- `python scripts/verify_round.py`: コンパイル エラー0件・警告0件、EditMode 448/448、PlayMode 21/21を2回とも成功。Domain ReloadとScene Reloadが無効であること、および検証後のPlay Mode設定復元も確認した。
- `python scripts/release_round.py preflight`: branch、テスト差分、6.0.0の版整合、CHANGELOG、UTF-8 BOM 15件、`.meta` 5件、Runtime/CoreのEditor依存、asmdef 2件、Play Mode設定、生成文書同期の全項目を通過した。
- 全asmdefとRuntime / Core / EditorのUnity名前空間を再走査し、独立したUPMパッケージを持つ直接依存8件が `package.json` に揃っていることを確認した。
- Unity Editorで `Project Settings > SymphonyFrameWork` を開き、Debug HUD ShortcutのInput Action、既定Composite、Binding追加・削除UIにレイアウト崩れがないことをスクリーンショットで確認した。

### 未実施の確認

- 「人が操作する確認」の2〜7はPlayerまたは実デバイス入力を伴う手動確認として未実施。AddTextの非自動表示、Hide後の内容復元、2回のライフサイクル往復、通常ビルド条件の無生成は自動テストで検証したが、実際のキー入力、Gamepad Binding、Development Build Playerと通常Playerの実機操作は未確認である。
- 項目1は設定画面と既定Compositeの表示を確認済みだが、Compositeを展開して3つの個別Binding pathを目視する操作は未実施。各pathは `DebugHUDConfigTests` で検証した。

### 振り返り

- 実装レビューで初期化失敗時の半初期化状態を見つけ、コミット前にロールバック処理を追加して全検証を再実行した。
- `release_round.py commit --pr-body-file Temp/debug_hud_pr_body.md` は、呼び出し元ではなくsubmoduleを基準に相対パスを解決したため、コミットとpush後のPR作成だけが失敗した。今回は絶対パスで `gh pr create` を継続した。再発防止として、スクリプトが作業ディレクトリを切り替える前に `--pr-body-file` を絶対パスへ正規化する改善を提案するが、このRoundではスクリプトを変更していない。
