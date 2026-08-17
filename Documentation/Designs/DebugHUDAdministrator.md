# Debug HUD Administratorパネル

## 目的

既存の `Window > SymphonyFrameWork > Symphony Administrator` へDebug HUDパネルを追加し、HUDの初期化・表示状態と登録テキスト数を確認しながらShow / Hideを操作できるようにする。Round 1のInput Action編集修正が `6.0.1` として完了した後に着手する。

## Round分割

この文書をRound 2とし、`DebugHUDInputFix.md` のRound 1をマージ・finalizeしてから、更新済み `develop` から別featureブランチを作る。Runtimeの状態通知、Editorパネル、文書、テストを1つのminorリリースとして完結させる。

## 公開API

利用側向けRuntime APIは変更しない。UI ToolkitがUXMLから生成するため、既存パネルと同じ根拠で次のEditor型だけをpublicにする。

```csharp
[UxmlElement]
public sealed partial class DebugHUDWindow : SymphonyVisualElement, IDisposable
```

表示用の `DebugHUDDto`、`DebugHUDViewModel`、`SymphonyDebugHUD.CurrentViewModel` はすべてinternalとする。

## ファイル構成

| パス | 名前空間 | 変更内容 |
| --- | --- | --- |
| `Runtime/Debug/DebugHUD/Internal/DebugHUDDto.cs` | `SymphonyFrameWork.Debugger.HUD` | 初期化可否、表示状態、登録数の不変スナップショット |
| `Runtime/Debug/DebugHUD/Internal/DebugHUDViewModel.cs` | 同上 | `ReactiveProperty<DebugHUDDto>` を所有してEditor Viewへ通知 |
| `Runtime/Debug/DebugHUD/SymphonyDebugHUD.cs` | 同上 | 各状態変更後にViewModelを更新し、Compositionから現在のViewModelを取得可能にする |
| `Runtime/Orchestrator/Internal/SymphonyOrchestrator.cs` | `SymphonyFrameWork.Orchestrator` | ViewModelを生成してDebug HUDへ注入する |
| `Editor/Administrator/UITK/CS/DebugHUDWindow.cs` | `SymphonyFrameWork.Editor` | ViewModel購読、Play Mode再接続、Show / Hide操作 |
| `Editor/Administrator/UITK/UXML/DebugHUDWindow.uxml` | UXML | 状態ラベル、登録数、Show / Hideボタン、文書ボタン |
| `Editor/Administrator/UITK/SymphonyWindow.uxml` | UXML | `DebugHUDWindow` を管理画面へ追加 |
| `Editor/Administrator/SymphonyAdministrator.cs` | `SymphonyFrameWork.Editor` | パネルを取得し、Window終了時にDispose |
| `Tests/Editor/DebugHUDViewModelTests.cs` | `SymphonyFrameWork.Tests` | 同値抑制、状態更新、Disposeを検証 |
| `Tests/Editor/DebugHUDWindowTests.cs` | 同上 | UXML要素と未接続表示を検証 |
| `Tests/Editor/SymphonyAdministratorUxmlTests.cs` | 同上 | 6番目のカスタムパネルが生成されることを検証 |

新規 `.cs` と `.uxml` はUnity生成の `.meta` と対で含める。`DebugHUDWindow` は既存Administratorの同一モジュールへ置くため、新しいEditorディレクトリは作らない。

## 依存方向

Debug HUDはView専用機能であり、Runtime側のViewModelがFacadeの表示状態をDtoへ変換し、Editor側のWindowがReactivePropertyを購読する。Windowのボタンは状態を独自変更せず、既存公開エントリポイント `SymphonyDebugHUD.Show()` / `Hide()` へ委譲する。

```text
SymphonyOrchestrator ──生成──> DebugHUDViewModel
          └─注入────────────> SymphonyDebugHUD
                                  ├─状態更新──> DebugHUDViewModel.State
                                  │                    │
                                  │                    v
                                  │             DebugHUDWindow
                                  └<──Show/Hide────────┘
```

RuntimeからEditorへの参照は追加しない。Editor asmdefは既にRuntimeとCoreを参照し、`InternalsVisibleTo` によりinternal ViewModelへ到達できる。既存のPause / Scene Load / Service Locate / Save Dataパネルと同じ再接続経路を使う。

## 状態とライフサイクル

- Dtoは `IsInitialized`、`IsAvailable`、`IsVisible`、`RegisteredTextCount` を持ち、値等価を実装する。
- `Initialize`、`Show`、`Hide`、`AddText`、`RemoveText`、一時テキスト解除、`ResetRuntimeState` の確定後に最新DtoをViewModelへ設定する。同値ならReactivePropertyが通知を抑止する。
- `SymphonyOrchestrator` がViewModelを生成し、Debug HUDのResetでDisposeする。Domain Reload無効でも前回購読を残さない。
- WindowはPlay Mode開始後に `CurrentViewModel` へ接続し、終了時に購読を解除して未接続表示へ戻す。
- 未接続時は状態を `-`、登録数を `-` とし、Show / Hideボタンを無効化する。
- 接続中は現在の表示状態と登録数を即時反映し、Shortcutやメニュー経由の切替にも開いたまま追従する。

## エラー処理

- Edit Mode、通常ビルド条件、Composition初期化前はボタンを無効にし、未初期化例外を発生させない。
- ViewModelまたはUXML要素を取得できない場合は既存のAdministrator初期化・ログ経路に従い、不完全な操作を開始しない。
- `Dispose` は多重呼出し可能とし、Editor callbackとReactiveProperty購読を必ず解除する。

## 影響範囲

- Symphony AdministratorへDebug HUDパネルが1つ増える。
- `DebugHUDWindow` はEditor専用public型だが、Runtime利用側APIとシリアライズ形式は変わらない。
- Debug HUDの状態変更時にinternalな表示通知が発生する。HUD描画、Shortcut、Development Build限定契約は変えない。
- `Documentation~/Modules/Debug.md` にパネル操作を追加し、`Documentation~/EditorTools.md` のAdministrator一覧を更新する。

## テストの置き場と種別

EditModeへ追加する。

| テスト名 | 検証内容と書き方 |
| --- | --- |
| `SetState_ChangedDto_NotifiesSubscriberOnce` | 初期通知後の回数を記録し、異なるDto設定による差分が1回であることを比較する |
| `SetState_EquivalentDto_DoesNotNotifyAgain` | 準備直後の通知回数を記録し、同値設定後に増えないことを比較する |
| `Dispose_AfterSubscription_StopsNotifications` | Dispose後の更新を拒否または通知しない契約を例外型と通知差分で固定する |
| `InitializeAndShow_UpdatesCurrentViewModelState` | Fake Factoryで初期化し、Show後のDtoがinitialized / available / visibleであることを購読値で確認する |
| `AddAndRemoveText_UpdatesRegisteredTextCount` | 公開Facade経由で追加・解除し、Dtoの件数が1→0になることを確認する |
| `Instantiate_DebugHUDPanel_ContainsStatusAndButtons` | UXMLをInstantiateし、名称付きLabelとShow / Hide ButtonをQueryして非null確認する |
| `Instantiate_AllAdministratorPanels_IncludesDebugHUDWindow` | 既存UXMLテストへ `DebugHUDWindow` を追加し、6パネルすべてを確認する |

EditorWindowのクリック操作とPlay Mode遷移中の見た目は自動操作できないため、人の確認へ残す。

## 動作確認手順

### 自動確認

1. `python scripts/verify_round.py` でコンパイル、全EditMode、PlayMode 2往復を通す。
2. Facade操作からDto / ViewModelまでの状態通知をEditModeテストで確認する。
3. Administrator UXMLをInstantiateし、6パネルとDebug HUD内の全要素を確認する。
4. `python scripts/release_round.py preflight` と文書生成同期を通す。

### 人が操作する確認

1. Symphony Administratorを開き、Debug HUDパネルが既存5パネルと同じレイアウトで表示されることを確認する。
2. Edit Modeでは未接続表示でShow / Hideが無効であることを確認する。
3. Windowを開いたままPlay Modeへ入り、状態と登録数が接続表示へ変わることを確認する。
4. Show / Hideボタン、Shortcut、Toolsメニューの各経路で切り替え、開いたままのパネルが毎回追従することを確認する。
5. AddText / RemoveTextで登録数が追従することを確認する。
6. Play Mode開始・終了を2回繰り返し、表示の二重更新や古い状態が残らないことを確認する。

## バージョン判断

`6.1.0` のマイナー更新とする。既存Runtime APIを壊さず、新しいEditor管理パネルを追加する後方互換な機能追加である。

## この Round で触るバージョン関連ファイル

- `Assets/SymphonyFrameWork/package.json`: `6.1.0`
- `Assets/SymphonyFrameWork/Core/SymphonyConstant.cs`: `6.1.0`
- `Assets/SymphonyFrameWork/CHANGELOG.md`: `6.1.0` の `Add` / `Change`
- `Assets/SymphonyFrameWork/README.md`: 現在のバージョンだけ更新
- `Assets/SymphonyFrameWork/Documentation~/Modules/Debug.md`: Administratorパネルの入口・状態・操作
- `Assets/SymphonyFrameWork/Documentation~/EditorTools.md`: Administrator一覧へDebug HUDを追加
- `Assets/SymphonyFrameWork/Documentation~/Html/**`: Markdown正本から再生成

`AGENTS.md`、`Documentation~/AgentUsage.md`、`Architecture.md` は公開Runtime API、AI向け利用判断、全体レイヤー構成を変えないため更新しない。

## 実施レポート

- 実装コミット: `1af76b2`（`[add]AdministratorにDebug HUDパネルを追加`）
- Pull Request: [SymphonyFramework #186](https://github.com/HIBIKI5201/SymphonyFramework/pull/186)
- Unity compile: Framework Error 0件
- 集中EditMode: 15件すべて成功
- 全EditMode: 458件すべて成功
- PlayMode: 21件すべて成功を2回連続で確認
- Unity Console: Error 0件
- `python scripts/release_round.py preflight`: 全項目成功
- Symphony Administratorを実際に開き、Debug HUDカード、Edit Modeの未接続表示、無効なShow / Hideをスクリーンショットで確認

`verify_round.py` はUnity/uLoopの待機ハンドシェイクが応答を返さず中断した。Unityの増分コンパイルと、同スクリプトが呼ぶEditMode・PlayMode・Console確認は個別に実行して上記結果を得た。Play Mode中のボタンクリックとShortcut・メニューを跨ぐ見た目の追従は、人の確認事項として残す。
