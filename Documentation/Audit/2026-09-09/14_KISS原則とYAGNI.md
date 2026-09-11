# 14. KISS / YAGNI

単一実装interface 10候補をテスト差し替えと公開契約の用途まで確認した。

## 【設計指摘】IGameObjectが実装・利用とも0件の公開契約として残る

**場所**: [IGameObject.cs:9](../../../Assets/SymphonyFrameWork/Runtime/Interface/IGameObject.cs#L9)

他の単一実装interfaceはInfrastructure境界またはテスト差し替えに使われるが、`IGameObject`は本体・Sampleに実装がない。

**修正方針**: 将来用途だけで残しているなら`[Obsolete]`化し、メジャー更新で削除する。公開APIなので即時削除は破壊的変更。

