# 03. ライフサイクルとstatic状態のリセット

`ProjectSettings/EditorSettings.asset` の `m_EnterPlayModeOptions: 3` により、
**Domain Reload と Scene Reload の両方が無効**である。static 状態は Play Mode 終了時に
リセットされない。各Facadeの `ResetRuntimeState()` はこの前提のために存在する。

static な可変状態（フィールド・自動プロパティ・`event`）を持つ型を列挙し、
`ResetRuntimeState()` の有無と `SymphonyOrchestrator` への登録を照合した。

## 調査サマリ

| 分類 | 件数 |
| --- | --- |
| `SymphonyOrchestrator` へ登録済みのサブシステム | 6 |
| static 可変状態を持ち `ResetRuntimeState` が無い型 | 5 |
| └ 読解の結果、リセットが必要だったもの | **0** |
| └ 別経路で確実に再設定されるもの | 5 |

**確定の指摘は無い。** この観点は当フレームワークで最も事故りやすい箇所だが、
実装は行き届いている。

---

## 登録済みの6サブシステム

[SymphonyOrchestrator.cs:47-67](../../Assets/SymphonyFrameWork/Runtime/Orchestrator/Internal/SymphonyOrchestrator.cs)

```csharp
RecordInitializedSubsystem(SaveStore.ResetRuntimeState);
RecordInitializedSubsystem(PauseManager.ResetRuntimeState);
RecordInitializedSubsystem(ServiceLocator.ResetRuntimeState);
RecordInitializedSubsystem(SceneLoader.ResetRuntimeState);
RecordInitializedSubsystem(AudioManager.ResetRuntimeState);
RecordInitializedSubsystem(SymphonyDebugHUD.ResetRuntimeState);
```

**初期化とリセットを同じ場所へ1行で並べる形になっており、新規サブシステムの登録忘れが
起きにくい。** 良い設計である。

---

## 【設計指摘】リセット不要な理由がコードから読み取れない

5件のstatic状態は、いずれも別経路で再設定されるためリセット不要である。
ただし**そのことがコードから分からない**。次の監査でも同じ5件を1つずつ追うことになる。

| 場所 | static 状態 | リセット不要な理由 |
| --- | --- | --- |
| [ServiceLocateLogOption.cs:7,10,13](../../Assets/SymphonyFrameWork/Runtime/System/ServiceLocator/Internal/ServiceLocateLogOption.cs) | ログ出力フラグ3件 | [PackageInitializer.ApplyServiceLocateLogOptions()](../../Assets/SymphonyFrameWork/Editor/PackageInitializer.cs) がProject Settingsから毎回押し込む。Play Mode中に変化しない |
| [SymphonyVisualElement.cs:55](../../Assets/SymphonyFrameWork/Runtime/Utility/SymphonyVisualElement.cs) | `EditorAssetLoader` | [PackageInitializer.cs:27,38](../../Assets/SymphonyFrameWork/Editor/PackageInitializer.cs) が設定と `null` 化の両方を行う |
| [SymphonyDebugLogger.cs:52](../../Assets/SymphonyFrameWork/Runtime/Debug/SymphonyDebugLogger.cs) | `OnLogDirect` | 購読側 [SymphonyDebugLogFileWriter.cs:43](../../Assets/SymphonyFrameWork/Editor/Debug/SymphonyDebugLogFileWriter.cs) が `-=` → `+=` の冪等購読で二重登録を防ぐ |
| [SymphonyOrchestrator.cs:24](../../Assets/SymphonyFrameWork/Runtime/Orchestrator/Internal/SymphonyOrchestrator.cs) | 初期化済みリスト | オーケストレータ自身がリセットの主体。自分をリセット対象にはできない |
| [EditorSymphonyConstant.cs:41](../../Assets/SymphonyFrameWork/Core/Editor/EditorSymphonyConstant.cs) | 定数キャッシュ3件 | Editor専用。Play Modeの影響を受けない |

**修正方針**: 各型のXMLドキュメントへ「`ResetRuntimeState` を持たない理由」を1行足す。
たとえば `ServiceLocateLogOption` なら次のようになる。

```csharp
/// <summary>
///     Service LocatorのEditor向けログ出力可否を保持する。
///     値はPackageInitializerがProject Settingsから押し込むため、
///     Domain Reload無効下でもResetRuntimeStateは不要である。
/// </summary>
internal static class ServiceLocateLogOption
```

これは動作を変えない純粋なドキュメント修正で、
**次回以降の監査でこの5件が自動的に「確認済み」として扱えるようになる**。

---

## 未実施: Play Mode 2往復による実測

**静的解析では「`ResetRuntimeState` が状態を*全部*戻しているか」を判断できない。**
フィールドを1つ追加したときに戻し忘れる、というのがこの観点で最も多い欠陥である。

本監査ではPlay Modeの実行を行っていない。次の手順での確認を勧める。

```text
uloop-clear-console
uloop-control-play-mode   （開始 → 終了 → 開始 → 終了）
uloop-get-logs            （2周目でゴースト参照の例外が出ないこと）
```

`AGENTS.md` §6 が定番のチェックとして挙げているものである。

---

## 検証したが問題が無かった項目

- **6サブシステムすべてが `ResetRuntimeState()` を `internal static` で持ち、
  自身の初期化メソッドからも呼んでいる**（例: [SceneLoader.cs:305,313](../../Assets/SymphonyFrameWork/Runtime/System/SceneLoader/SceneLoader.cs)）。
  初期化時とリセット時で同じコードを通るため、経路の食い違いが起きない
- **`DontDestroyOnLoad` は2件のみ**で、いずれもフレームワーク自身のシステムGameObject
  （[SymphonyOrchestrator.cs:44](../../Assets/SymphonyFrameWork/Runtime/Orchestrator/Internal/SymphonyOrchestrator.cs)、
  [SystemObjectFactory.cs:16](../../Assets/SymphonyFrameWork/Runtime/Orchestrator/Internal/SystemObjectFactory.cs)）
- **`OnDestroy` の早期returnによる解除スキップは0件**

---

## 付録A: static 可変状態を持ち `ResetRuntimeState` が無い型（全5件）

| 場所 | 内容 |
| --- | --- |
| `Assets/SymphonyFrameWork/Runtime/Debug/SymphonyDebugLogger.cs:52` | static な可変状態 2件 |
| `Assets/SymphonyFrameWork/Runtime/Orchestrator/Internal/SymphonyOrchestrator.cs:24` | static な可変状態 3件 |
| `Assets/SymphonyFrameWork/Runtime/System/ServiceLocator/Internal/ServiceLocateLogOption.cs:7` | static な可変状態 3件 |
| `Assets/SymphonyFrameWork/Runtime/Utility/SymphonyVisualElement.cs:55` | static な可変状態 1件 |
| `Assets/SymphonyFrameWork/Core/Editor/EditorSymphonyConstant.cs:41` | static な可変状態 3件 |

再生成:

```bash
python scripts/audit_scan.py --category 03_static_without_reset --category 03_reset_not_registered
```
