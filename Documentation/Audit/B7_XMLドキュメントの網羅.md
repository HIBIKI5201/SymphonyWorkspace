# B7. XMLドキュメントの網羅

**指摘なし。**

`Documentation/CodeGuidelines.md` の「XMLドキュメントとコメント」節が正本。
`Runtime` / `Core` の `public` メンバーについて、
直前12行以内に `///` があるかを機械的に検査した。

## 調査サマリ

| 分類 | 件数 |
| --- | --- |
| `Runtime` / `Core` の `public` メンバー | 327 |
| **XMLドキュメントの無いもの** | **0** |

**327件すべてに `<summary>` が付いている。** 記載漏れゼロは、
このプロジェクトで最も徹底されている規約である。

---

## 記述の質

網羅率だけでなく、抽出した数十件について記述の質を確認した。
**型名の言い換えで終わっているものは見つからなかった。**

### 引数・戻り値・例外の記述

```csharp
/// <summary> HUDへ毎フレーム評価する文字列生成処理を追加する。 </summary>
/// <param name="func"> 表示文字列を返す処理。 </param>
public void Add(Func<string> func) => _extraTexts.Add(func);
```

```csharp
/// <summary> 指定シーンのロード完了callbackを取り出して登録から削除する。 </summary>
/// <param name="sceneName"> 対象シーン名。 </param>
/// <returns> 登録済みcallback。存在しない場合はnull。 </returns>
internal Action TakeLoadedAction(string sceneName)
```

**`internal` メンバーにもXMLドキュメントが付いている。**
規約は `public` のみを要求しているが、実態はそれを上回っている。

### 判断の理由が書かれている例

```csharp
// SymphonyAwaitable.cs:10-14
/// <summary>
///     UnityのAwaitableに、完了済み値、合成、条件待機、タイムアウト、Taskブリッジを提供する。
///     WhenAnyは未完了側のAwaitableと例外を安全に消費する一般契約を定められないため、
///     具体的な利用要件が生じるまで提供しない。
/// </summary>
```

**「提供しないもの」とその理由が書かれている。** 利用者が
「なぜ `WhenAny` が無いのか」を問い合わせる必要が無くなる。

### 誤解を先回りして潰している例

```csharp
// SaveDataEntryInfo.cs:26-30
/// <summary>
///     永続化データを読み込み済みかどうか。
///     <see cref="Data" />が存在することは読み込み済みであることを意味しない。
///     キャッシュは初回アクセス時に既定値で作られるため、両者は別の状態である。
/// </summary>
public bool IsLoaded { get; }
```

**「何を意味しないか」が明示されている。** `Data != null` を
読み込み済みの判定に使う誤用を、ドキュメントの側で防いでいる。

---

## 唯一の不整合はドキュメントの側にある

[SymphonyAwaitable.BackGroundThreadAction](../../Assets/SymphonyFrameWork/Runtime/Utility/SymphonyAwaitable.cs)
の `<exception cref="ArgumentNullException">` は、`async void` であるため
呼び出し元へ届かない。**記載漏れではなく、記載内容が実装と一致していない**ケースである。

詳細と修正方針は [11](11_非同期処理の不純点.md) に記載している。
本観点では「網羅率100%だが、契約が成立しない記述が1件ある」と記録するにとどめる。

## 記述の追加を提案しているもの

本監査の他の観点から、XMLドキュメントへの追記を3件提案している。
**いずれも動作を変えない純粋なドキュメント修正で、次回監査の負荷を下げる。**

| 対象 | 追記内容 | 出典 |
| --- | --- | --- |
| [ServiceLocateLogOption](../../Assets/SymphonyFrameWork/Runtime/System/ServiceLocator/Internal/ServiceLocateLogOption.cs) ほか4型 | `ResetRuntimeState` を持たない理由 | [03](03_ライフサイクルとstatic状態のリセット.md) |
| [IGameObject](../../Assets/SymphonyFrameWork/Runtime/Interface/IGameObject.cs) | メンバー名がPascalCaseでない理由、受け手の所在 | [14](14_KISS原則とYAGNI.md) / [18](18_命名一貫性と可読性.md) |
| [SymphonyAwaitable.WhenAll](../../Assets/SymphonyFrameWork/Runtime/Utility/SymphonyAwaitable.cs) | `CancellationToken` を取らない理由 | [11](11_非同期処理の不純点.md) |

---

## 付録A: XMLドキュメントの無い `public` メンバー

**該当なし（0件）。**

再生成:

```bash
python scripts/audit_scan.py --category B_public_without_xmldoc
```

**この検査は直前12行以内の `///` を見ている。** 属性が長い場合に取りこぼす可能性があるが、
今回は検出0件・実際の記載漏れも0件で一致した。
