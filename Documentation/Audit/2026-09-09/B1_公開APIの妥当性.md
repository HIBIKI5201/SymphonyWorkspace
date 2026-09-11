# B1. 公開APIの妥当性

`public`型を公開範囲の列挙と突き合わせた。

## 【設計指摘】IGameObjectの公開理由を本体から確認できない

**場所**: [IGameObject.cs:9](../../../Assets/SymphonyFrameWork/Runtime/Interface/IGameObject.cs#L9)

利用側が実装する契約には該当し得るが、Framework・Sampleとも実装0件で利用例がない。削除は破壊的変更なので、用途の文書化か段階的廃止の判断が必要。

前回のSampleクラス出荷は`Samples~/`移動により解消済み。

