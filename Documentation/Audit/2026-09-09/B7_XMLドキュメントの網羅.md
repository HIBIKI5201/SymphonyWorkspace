# B7. XMLドキュメントの網羅

機械検出1件を属性とevent accessorを含めて読解した。

## 調査結果

該当なし。[PauseManager.OnPauseChanged](../../../Assets/SymphonyFrameWork/Runtime/Service/Pause/PauseManager.cs#L24)にはXML文書があり、長い`Obsolete`属性により走査の12行窓から外れた誤検出だった。

