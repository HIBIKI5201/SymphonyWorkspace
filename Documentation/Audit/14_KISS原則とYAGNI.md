# 14. KISS原則とYAGNI

インターフェースの実装数、`typeof` / リフレクションによる汎用化、到達しない拡張点を確認した。

## 調査サマリ

| 分類 | 件数 |
| --- | --- |
| インターフェース総数（Runtime / Core） | 8 |
| └ 実装が1件以下のもの | 8 |
| └ **読解の結果、YAGNI違反だったもの** | **0** |
| `typeof` の使用 | 78 |
| リフレクションAPIの使用 | 55 |

`Docs/RuntimeAudit` ではゲーム側にインターフェース135件があり、
うち78件が単一実装として棚卸し対象になっていた。
**当フレームワークは8件しか無く、すべてに存在理由がある。**

---

## 【設計指摘】実装0件のインターフェース2件に、利用例が無い

**場所**: [IGameObject.cs:7](../../Assets/SymphonyFrameWork/Runtime/Interface/IGameObject.cs)、
[PauseManager.cs:231](../../Assets/SymphonyFrameWork/Runtime/System/Pause/PauseManager.cs)

この2つはフレームワーク内に実装が無い。**利用側が実装するための契約**だからである。
YAGNI違反ではない。

```csharp
public interface IGameObject
{
    GameObject gameObject { get; }
    Transform transform { get; }
    CancellationToken destroyCancellationToken { get; }
}
```

`gameObject` / `transform` が小文字始まりなのは、**MonoBehaviour のメンバー名と一致させて
MonoBehaviour が暗黙にこの契約を満たすようにするため**である。
C#の命名規則から外れているが、意図が明確な例外であり指摘しない。

### 何を指摘するか

`PauseManager.IPausable` には
[PauseManagerSample_Mover](../../Assets/SymphonyFrameWork/Samples/Runtime/PauseManagerSample/Scripts/PauseManagerSample_Mover.cs)
という実装例がサンプルにある。**一方 `IGameObject` には実装例がどこにも無い。**

- サンプルに無い
- テストに無い
- `Documentation~/` に用例が無い

**実装0件で用例も無いインターフェースは、利用者から見て「何のために在るのか」が分からない。**
XMLドキュメントは「GameObjectを所有するUnityオブジェクトの共通参照を公開する」と
書いてあるが、**誰がこれを受け取るのか**が書かれていない。

**修正方針**: いずれか。

1. `IGameObject` を引数に取るフレームワークAPIがあるなら、
   XMLドキュメントの `<remarks>` にそれを明記する
2. 無いなら、**この型が実際に必要かを再検討する**。
   利用側が実装しても、フレームワーク側に受け手が無ければ意味を持たない
3. 用例をサンプルへ足す

---

## 検証したが問題が無かった項目

### 単一実装インターフェース6件はすべて正当

| 場所 | 実装 | 存在理由 |
| --- | --- | --- |
| [IInitializeAsync.cs:8](../../Assets/SymphonyFrameWork/Runtime/Interface/IInitializeAsync.cs) | 1 | 利用側が実装する契約 |
| [IAudioSourceHost.cs:9](../../Assets/SymphonyFrameWork/Runtime/System/Audio/Internal/Application/IAudioSourceHost.cs) | 1 | `Internal/` — テスト用の差し替え点 |
| [ISceneLoader.cs:11](../../Assets/SymphonyFrameWork/Runtime/System/SceneLoader/Internal/Application/ISceneLoader.cs) | 1 | `Internal/` — Unity APIとの境界（DIP） |
| [IServiceHost.cs:4](../../Assets/SymphonyFrameWork/Runtime/System/ServiceLocator/Internal/Application/IServiceHost.cs) | 1 | `Internal/` — テスト用の差し替え点 |
| [IReadOnlyReactiveProperty.cs:13](../../Assets/SymphonyFrameWork/Core/Internal/IReadOnlyReactiveProperty.cs) | 1 | 読み取り専用ビューの公開（ISP） |
| [ISystemObjectFactory.cs:8](../../Assets/SymphonyFrameWork/Core/Internal/ISystemObjectFactory.cs) | 1 | `Internal/` — テスト用の差し替え点 |

**`Internal/` 配下の4件は、`InternalsVisibleTo` によるテストから実装を差し替えるための
境界である。** 実装が1件なのは正常で、2件目はテストコード側に存在する。
`Documentation/DesignPhilosophy.md` の「依存性逆転」節の方針どおりである。

`ISceneLoader` は特に、**Unity の `SceneManager` API との境界を切る**役割を持つ。
`SceneLoadService`（Application層）が Unity API を直接触らずに済むのはこの型のおかげである。

### `typeof` 78件 / リフレクション55件は設計上必然

`ServiceLocator` は型をキーにしたサービス解決、`SaveSystem` は型をキーにした
セーブデータ解決という設計であり、**型情報の実行時利用が機能の中核**である。
投機的な汎用化ではない。

ただし解決結果をキャッシュしているかは本監査で追い切れていない
（→ [04](04_GCAllocとメモリリーク.md) の「リフレクションがランタイム側に36箇所ある」）。

### 到達しない拡張点は0件

定数 `switch` で分岐が死んでいる箇所、使われない仮想メソッド、
`TODO` として残された未実装の拡張点は見つからなかった（`TODO` / `FIXME` は0件）。

---

## 付録A: 実装が1件以下のインターフェース（全8件）

| 場所 | 実装数 | 判定 |
| --- | --- | --- |
| `Assets/SymphonyFrameWork/Runtime/Interface/IGameObject.cs:7` | 0 | **指摘**（用例が無い） |
| `Assets/SymphonyFrameWork/Runtime/System/Pause/PauseManager.cs:231` `IPausable` | 0 | 正当（サンプルに用例あり） |
| `Assets/SymphonyFrameWork/Runtime/Interface/IInitializeAsync.cs:8` | 1 | 正当（利用側の契約） |
| `Assets/SymphonyFrameWork/Runtime/System/Audio/Internal/Application/IAudioSourceHost.cs:9` | 1 | 正当（テスト差し替え） |
| `Assets/SymphonyFrameWork/Runtime/System/SceneLoader/Internal/Application/ISceneLoader.cs:11` | 1 | 正当（Unity API境界） |
| `Assets/SymphonyFrameWork/Runtime/System/ServiceLocator/Internal/Application/IServiceHost.cs:4` | 1 | 正当（テスト差し替え） |
| `Assets/SymphonyFrameWork/Core/Internal/IReadOnlyReactiveProperty.cs:13` | 1 | 正当（ISP） |
| `Assets/SymphonyFrameWork/Core/Internal/ISystemObjectFactory.cs:8` | 1 | 正当（テスト差し替え） |

再生成:

```bash
python scripts/audit_scan.py --category 14_single_implementation_interface
```

**走査は実装数を文字列一致で数えているため、過小・過大の両方に振れる。**
上表の判定は読解によるものである。
