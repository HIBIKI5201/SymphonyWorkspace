# 04. GC Alloc・メモリリーク

Project AuditorのFramework該当1,513件を呼び出し頻度で絞った。

## 【確定・高】Service Locator取得ログが未出力のまま蓄積する

**場所**: [ServiceLocator.cs:292](../../../Assets/SymphonyFrameWork/Runtime/Service/ServiceLocator/ServiceLocator.cs#L292)、[SymphonyDebugLogger.cs:140](../../../Assets/SymphonyFrameWork/Runtime/Debug/SymphonyDebugLogger.cs#L140)

取得ログ有効時の`GetInstance<T>`は`AddText`だけを呼ぶ。取得の反復に比例してstatic `StringBuilder`が増え、無関係な`LogText`が呼ばれるまで解放されない。

**修正方針**: 取得ログは`LogDirectForEditor`で即時出力するか、呼び出し単位で所有するログバッチへ変更する。

## 【確定】HUDのメモリ表示が毎フレームボックス化する

**場所**: [SymphonyHUDDrawer.cs:127](../../../Assets/SymphonyFrameWork/Runtime/Debug/DebugHUD/Internal/SymphonyHUDDrawer.cs#L127)

補間書式付きの`long`／`double`4件をProject AuditorがMajorとして検出した。表示更新経路なので毎フレーム発生する。

**修正方針**: 同メソッドのFPS表示と同様に明示的な`ToString`結果を連結する。前回から残存。

