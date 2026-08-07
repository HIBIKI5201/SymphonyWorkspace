# 02. Null参照リスクとガード漏れ

**指摘なし。**

このフレームワークには `SymphonyDebugLogger.LogAndCheckComponentNull` /
`IsComponentNotNull` という自前ガードがあるため、観点を
「ガードが無い箇所を探す」ではなく**「自前ガードを使うべき箇所で使っていない」**へ読み替えて確認した。

## 確認したこと

| 項目 | 結果 |
| --- | --- |
| Unityオブジェクトに対する `??` / `??=` | **0件** |
| `SerializeField` を持つファイル | 3ファイル・13フィールド |
| `LogError` / `LogWarning` の直後に `return` が無い箇所 | **0件** |

### `??=` 7件はすべてローカル変数への適用

| 場所 | 対象 |
| --- | --- |
| `Runtime/Orchestrator/Internal/SymphonyOrchestrator.cs:128,140` | `List<Exception> exceptions` |
| `Runtime/Utility/SymphonyAwaitable.cs:128,132,177,181` | `Exception firstCancellation` / `firstException` |
| `Core/Internal/ReactiveProperty.cs:67` | `List<Exception> exceptions` |

いずれも `System.Exception` と `System.Collections.Generic.List<T>` に対するもので、
**Unityの `==` オーバーロードが関係しない純粋な.NET型である**。
破棄済みUnityオブジェクトを非nullと誤判定する問題は起きない。

7件すべてが「例外を集約するための遅延生成」という同じ用途で、書き方も統一されている。

### `SerializeField` は3ファイルのみ

| 場所 | 件数 |
| --- | --- |
| `Runtime/Component/ServiceLocateComponent.cs` | 5 |
| `Runtime/Configs/Internal/AudioConfig.cs` | 5 |
| `Runtime/Configs/Internal/SceneLoadConfig.cs` | 3 |

フレームワークはMonoBehaviourをほとんど持たないため、Inspector経由の未設定による
NullReferenceのリスク面が構造的に小さい。`AudioConfig` / `SceneLoadConfig` は
`SymphonyConfigManager` が自動生成する `internal` な設定アセットで、
利用側がフィールドを空のまま放置する経路は Project Settings のUIに限られる。

### エラー検知後の処理継続は0件

`Docs/RuntimeAudit` で「`LogError` の後に `return` が無くNullReferenceへ進む」という
確定指摘があった型の欠陥を探したが、当フレームワークには該当が無かった。

`UnitySceneLoader.cs:74,100` の `LogError` はいずれも直後に `return` している。

## この観点の限界

**静的解析では「実行時にnullになりうるか」は判断できない。**
上記は「典型的な誤りパターンが無い」ことの確認であって、
null安全性が証明されたわけではない。

`SymphonyDebugLogger.LogAndCheckComponentNull` の利用箇所が適切かどうかは、
PlayModeテスト（→ [B6](B6_テストとサンプルの追随.md)）の充実でしか担保できない。
