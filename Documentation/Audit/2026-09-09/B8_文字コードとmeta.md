# B8. 文字コード・改行・`.meta`

BOM、index側改行、`.cs`と`.meta`の対応を機械確認した。

## 【確定】LifecycleLoopだけUTF-8 BOMがない

**場所**: [SymphonyFrameworkLifecycleLoop.cs:1](../../../Assets/SymphonyFrameWork/Runtime/Orchestrator/Internal/SymphonyFrameworkLifecycleLoop.cs#L1)

208ファイル中1件で、`Documentation/CodeGuidelines.md`のUTF-8規約と既存ファイルの統一から外れる。

**修正方針**: ロジックを変えずUTF-8 BOM付きで保存する。改行と`.meta`の欠落は検出されなかった。

