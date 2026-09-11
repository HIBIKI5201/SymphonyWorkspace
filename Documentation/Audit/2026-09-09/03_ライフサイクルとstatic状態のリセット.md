# 03. ライフサイクルとstatic状態のリセット

static可変状態7候補をOrchestratorの`Shutdown`、Editorの`PackageInitializer.Shutdown`と突き合わせ、Play Modeを2往復した。

## 【確定・高】Loggerの蓄積バッファがRuntime終了時にリセットされない

**場所**: [SymphonyDebugLogger.cs:337](../../../Assets/SymphonyFrameWork/Runtime/Debug/SymphonyDebugLogger.cs#L337)

`_logTextBuilder`はstaticだが`ResetRuntimeState`を持たない。Domain Reload無効下では、`LogText(clearText: true)`へ到達しなかった内容が次のPlay Modeへ残る。

**修正方針**: Loggerに多重呼び出し安全な内部リセットを設け、Runtime Orchestratorの逆順終了へ登録する。`OnLogDirect`はEditor購読者の所有権を壊さないよう別扱いにする。

