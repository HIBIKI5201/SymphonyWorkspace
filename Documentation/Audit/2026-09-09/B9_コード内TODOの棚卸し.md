# B9. コード内TODOの棚卸し

`TODO|FIXME|HACK`を全C#から検索した。

## 【確定・最優先】型だけを確認して別インスタンスを解除し得る

**場所**: [ServiceLocateComponent.cs:79](../../../Assets/SymphonyFrameWork/Runtime/Service/ServiceLocator/ServiceLocateComponent.cs#L79)

`OnDisable`は`IsExistInstance(_targetType)`後に`UnregisterInstance(_targetType)`を呼ぶ。自分が登録した対象Aが先に解除され、同型Bが登録された場合でもBを解除する。コメント自身も誤判定を認識しているがIssue番号がなく、現行TODO規約にも違反する。

**修正方針**: `UnregisterInstance(Type, object)`相当の内部APIで登録payloadとの参照同一性を確認し、一致した場合だけ解除する。対応Issueを作り、A→B差し替え後の`OnDisable`回帰テストを追加する。

## 付録A: TODO全件（1件）

| 場所 | 内容 |
| --- | --- |
| [ServiceLocateComponent.cs:79](../../../Assets/SymphonyFrameWork/Runtime/Service/ServiceLocator/ServiceLocateComponent.cs#L79) | 同型の別インスタンスを誤判定し得る。Issue番号なし |

