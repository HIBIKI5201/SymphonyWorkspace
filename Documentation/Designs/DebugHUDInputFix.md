# Debug HUD Input Action編集修正

## 目的

`Project Settings > SymphonyFrameWork > Debug HUD Shortcut` のInput Actionをクリックしても展開・選択状態が維持されない不具合と、編集後または設定画面を開いた状態でPlay Modeへ入ると `For singleton action, bindings array must match that of the action` が出る不具合を修正する。

原因は `SymphonySettingProvider` がIMGUIフレームごとに新しい `SerializedObject` を生成してInput SystemのPropertyDrawerが持つTreeView状態を失わせていること、および変更がないフレームでも `ApplyModifiedPropertiesWithoutUndo()` を呼んでいることにある。Domain Reload無効環境では、Runtimeが既に生成したsingleton actionの非シリアライズActionMapが旧Binding配列を保持したまま、シリアライズ対象の `m_SingletonActionBindings` だけが置き換わり、参照不一致になる。

## Round分割

この修正をRound 1として単独で `6.0.1` にする。後続の `DebugHUDAdministrator.md` は、この修正版を基点にSymphony Administratorへパネルを追加するRound 2（`6.1.0`）とする。修正と機能追加を別コミット・別PRへ分け、Round 1だけでも設定編集とRuntime初期化が正常になる状態にする。

## 公開API

公開APIの追加・変更はない。既存の `SymphonyDebugHUD`、`DebugHUDConfig` のシリアライズフィールド名 `_toggleAction` と保存先を維持する。

## ファイル構成

| パス | 変更内容 |
| --- | --- |
| `Editor/SettingProvider/SymphonySettingProvider.cs` | Configごとに同じ `SerializedObject` を保持し、SettingsProvider非アクティブ化時に解放する。実変更時だけ適用・保存する |
| `Runtime/Configs/Internal/DebugHUDConfig.cs` | 保存後のInput ActionをJSONで再構築し、シリアライズされないActionMapキャッシュを破棄するinternal処理を追加する |
| `Tests/Editor/DebugHUDConfigTests.cs` | Runtime読取後のSerializedProperty編集と再構築を再現し、BindingとIDが壊れないことを検証する |
| `Tests/Editor/SymphonySettingProviderTests.cs` | 同じConfigでは同じSerializedObjectを再利用し、解放後は作り直すことを検証する |

## 依存方向

ConfigはInfrastructureとしてInput SystemとUnity JSONだけを参照する。SettingsProviderはEditor ViewとしてConfigの保存処理を呼ぶ。RuntimeからEditorへの参照は追加しない。

```text
SettingsProvider ──SerializedObjectで編集──> DebugHUDConfig
       └─実変更時だけ適用・保存─────────────┘
DebugHUDConfig ──JsonUtility round-trip──> 新しいInputAction
                                              └─ActionMap cacheは未生成
```

Unity Editor上の一時Input Actionで、JSON再構築後もAction ID、`ButtonWithTwoModifiers`、4 Binding、`Shift / D / P` のpathが維持されることを実測済みである。

## エラー処理

- ConfigのActionがnullの場合は既存どおりListener側で既定Actionへフォールバックする。
- JSON再構築に失敗した場合は例外を握りつぶさず設定保存を失敗として表面化させる。壊れたActionをRuntimeへ渡さない。
- SettingsProviderの対象Configが変わった場合は旧SerializedObjectをDisposeし、新しい対象へ作り直す。

## 影響範囲

- `_toggleAction` の保存形式、既定Binding、公開APIは変わらない。
- 入力設定の実変更時だけConfigアセットをdirty化・保存する。
- 開いたままのSettingsProviderでTreeViewの展開、Bindingの選択、追加・削除操作が維持される。
- Runtime Listenerは引き続きConfig ActionのCloneだけを所有し、Config本体をEnableしない。

## テストの置き場と種別

EditModeへ追加する。

| テスト名 | 検証内容と書き方 |
| --- | --- |
| `RebuildToggleAction_AfterSerializedBindingChange_PreservesBindingsAndId` | 先に `bindings` を読みActionMapを生成し、SerializedObjectでBinding pathを変更後に再構築する。Action ID、件数、変更pathを比較し、Assertログが出ないこともTest Runnerの通常ログ検査で担保する |
| `RebuildToggleAction_DefaultComposite_PreservesCompositeParts` | 既定Actionを再構築し、Composite名と3 partを列挙比較する |
| `GetDebugHUDSerializedObject_SameConfig_ReusesInstance` | 同じConfigを2回渡して `AreSame` で比較し、PropertyDrawerの状態保持条件を固定する |
| `ReleaseDebugHUDSerializedObject_NextRequestCreatesNewInstance` | キャッシュ解放前後のインスタンスを `AreNotSame` で比較する |

EditorのInput Action TreeView自体へのクリックは自動操作できないため、GUI操作は人の確認へ残す。

## 動作確認手順

### 自動確認

1. `python scripts/verify_round.py` でコンパイル、EditMode、PlayMode 2往復を通す。
2. 再現テストで、ActionMap生成後のSerializedProperty変更から再初期化してもAssertログが出ず、Bindingを読めることを確認する。
3. `python scripts/release_round.py preflight` を通す。

### 人が操作する確認

1. Project Settingsを開き、Toggle Action、Composite、各Bindingをクリックして選択・展開できることを確認する。
2. Bindingを追加・変更・削除し、設定画面を閉じずにPlay Modeへ入り、singleton actionのAssertが出ないことを確認する。
3. Play Mode終了後も設定画面を開いたまま再編集でき、2回目のPlay Modeでも設定したShortcutが1回だけ発火することを確認する。

## バージョン判断

`6.0.1` のパッチ更新とする。6.0.0で追加した設定UIとInput Action初期化の実装不具合を直し、公開契約とシリアライズ形式を変えないため。

## この Round で触るバージョン関連ファイル

- `Assets/SymphonyFrameWork/package.json`: `6.0.1`
- `Assets/SymphonyFrameWork/Core/SymphonyConstant.cs`: `6.0.1`
- `Assets/SymphonyFrameWork/CHANGELOG.md`: `6.0.1` の `Fix` だけを追加
- `Assets/SymphonyFrameWork/README.md`: 現在のバージョンだけ更新
- `Assets/SymphonyFrameWork/Documentation~/Html/**`: CHANGELOGとREADMEの再生成

利用方法と設定場所は変わらないため、`Documentation~/Modules/Debug.md` と `EditorTools.md` の本文は変更しない。

## 実装結果

- 実装コミット: `28bc62f`（`[fix]Debug HUDのInput Action編集を修正`）
- Pull Request: [SymphonyFramework #185](https://github.com/HIBIKI5201/SymphonyFramework/pull/185)
- EditMode: 452件すべて成功
- PlayMode: 21件すべて成功を2回連続で確認
- Unity Console: Error 0件
- `python scripts/release_round.py preflight`: 全項目成功

`verify_round.py` は本プロジェクトのフルコンパイルとDomain ReloadがuLoopの180秒応答上限を超えたため完走通知を取得できなかった。Unity再起動後のConsoleがError 0件であることを確認し、同スクリプトが呼ぶテストをuLoopから直接実行して上記結果を得た。Input Action TreeViewの実クリック確認は設計どおり人の確認事項として残す。
