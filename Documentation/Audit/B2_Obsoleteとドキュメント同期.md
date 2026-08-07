# B2. `[Obsolete]` と Deprecations.md の同期

**指摘なし。**

`AGENTS.md` §0.1 は「`[Obsolete]` を付けたら同じ変更で `Deprecations.md` へ行を追加する。
記載漏れはバグとして扱う」と定めている。全 `[Obsolete]` を機械的に列挙し、
`Assets/SymphonyFrameWork/Documentation~/Deprecations.md` と突き合わせた。

## 調査サマリ

| 分類 | 件数 |
| --- | --- |
| `[Obsolete]` の総数 | 11 |
| `Deprecations.md` に記載あり | **11** |
| **記載漏れ（コード → ドキュメント）** | **0** |
| `Deprecations.md` の `削除予定の一覧` の行数 | 11 |
| **移し忘れ（ドキュメント → コード）** | **0** |

**11件が双方向で過不足なく一致している。** `AGENTS.md` の規約が実際に守られている。

---

## 付録A: `[Obsolete]` 全件（全11件）

| 場所 | シンボル | 記載 |
| --- | --- | --- |
| `Assets/SymphonyFrameWork/Runtime/Debug/SymphonyDebugLogger.cs:200` | `DirectLog` | あり |
| `Assets/SymphonyFrameWork/Runtime/Debug/SymphonyDebugLogger.cs:212` | `TextLog` | あり |
| `Assets/SymphonyFrameWork/Runtime/Debug/SymphonyDebugLogger.cs:227` | `CheckComponentNull` | あり |
| `Assets/SymphonyFrameWork/Runtime/Debug/SymphonyDebugLogger.cs:237` | `IsComponentNotNull` | あり |
| `Assets/SymphonyFrameWork/Runtime/Utility/SymphonyTween.cs:116` | `TweeningLerp` | あり |
| `Assets/SymphonyFrameWork/Runtime/Utility/SymphonyTween.cs:181` | `TweeningCurve` | あり |
| `Assets/SymphonyFrameWork/Core/Editor/EditorSymphonyConstant.cs:50` | `ASSET_STORE_TOOLS_IGNORE_FILE` | あり |
| `Assets/SymphonyFrameWork/Editor/Generator/AssetStoreToolsPackager/AssetStoreToolsPackageContext.cs:12` | `AssetStoreToolsPackageContext` | あり |
| `Assets/SymphonyFrameWork/Editor/Generator/AssetStoreToolsPackager/AssetStoreToolsPackager.cs:34` | `PackageModeEnum` | あり |
| `Assets/SymphonyFrameWork/Editor/Generator/AssetStoreToolsPackager/AssetStoreToolsPackager.cs:52` | `Combine` | あり |
| `Assets/SymphonyFrameWork/Editor/Generator/AssetStoreToolsPackager/AssetStoreToolsPackager.cs:129` | `Export` | あり |

## 逆方向の検査を追加した

**当初の走査は「コード → ドキュメント」の一方向しか見ていなかった。**
これでは「削除が済んだのに `Deprecations.md` の行が `## 削除済み` へ移されていない」
ケースを拾えない。シンボルがコードから消えているため、
**コード側には手掛かりが一切残らない**。

`scripts/audit_scan.py` へ `B_deprecation_stale` を追加した。

| 分類 | 向き | 拾えるもの |
| --- | --- | --- |
| `B_obsolete_undocumented` | コード → ドキュメント | `[Obsolete]` を付けたのに記載していない |
| `B_deprecation_stale` | ドキュメント → コード | 削除済みなのに行が残っている |

`## 削除予定の一覧` 節の表から11シンボルを抽出し、
コード側の `[Obsolete]` 11件と突き合わせる。**両者は過不足なく一致した。**

```text
listed but not in code: (none)
in code but not listed: (none)
```

`### Combineの削除手順` 節にも同じシンボルを含む補足表があるが、
`###` 以下は対象外としている（削除手順の記述であって一覧ではないため）。

`## 削除済み` 節は現時点で「削除済みの項目はありません」であり、
検査結果と整合している。

## 確認できていないこと

- **各行の削除予定が妥当か。** 11件中6件が「未定」で、
  `Deprecations.md` 自身が「CHANGELOGへ非推奨化の記録が無い」ことを理由として記載している。
  **判断が必要な項目として可視化されており、記載漏れではない**
- **移行先として書かれたAPIが実在するか。** 文字列としては書かれているが、
  そのシンボルがコードに存在するかは検査していない。次回の検査候補になる

## 併せて提案する `[Obsolete]` の追加

本監査では、次の2件に `[Obsolete]` を付けることを提案している。
実施する場合は同じ変更で `Deprecations.md` へ行を追加すること。

| 対象 | 理由 |
| --- | --- |
| [SymphonyAwaitable.BackGroundThreadAction](../../Assets/SymphonyFrameWork/Runtime/Utility/SymphonyAwaitable.cs) | `async void` で例外契約が成立しない（→ [11](11_非同期処理の不純点.md)） |
| [SymphonyConstant.GetFrameworkAbsolutePath](../../Assets/SymphonyFrameWork/Core/SymphonyConstant.cs) | Core から `UnityEditor` を参照している（→ [08](08_アセンブリ境界とレイヤー違反.md)）。移設する場合のみ |

再生成:

```bash
python scripts/audit_scan.py --category B_obsolete --category B_obsolete_undocumented --category B_deprecation_stale
```
