# ReactiveProperty

## 目的

`ArchitectureRevision.md` の Phase 3 で導入する ViewModel が、表示状態の変更を View と Editor Window へ通知するための最小基盤を用意する。

Round G〜J（Scene Load / Service Locate / Save Data / Audio+Pause のレイヤー分割）で作る各 ViewModel がこれに依存するため、**先に単独の Round として片付ける**。

現在、Editor Window 4枚は `EditorApplication.update` から毎フレーム内部状態をポーリングしている。ViewModel と ReactiveProperty を入れることで、変更があったときだけ反映する形へ移行できる。

外部ライブラリ（R3 / UniRx）は導入せず、必要最小限を自前で持つ。`package.json` の依存を増やさないため。

## 公開API

**増えない。** どちらも `Core` アセンブリの `internal` である。

`Core/AssemblyInfo.cs` は既に `SymphonyFrameWork`（Runtime）と `SymphonyFrameWork.Editor` へ `InternalsVisibleTo` を与えているため、両方から利用できる。**この Round で `InternalsVisibleTo` を広げない。**

```csharp
internal interface IReadOnlyReactiveProperty<out T>
{
    T Value { get; }
    IDisposable Subscribe(Action<T> observer, bool notifyCurrent = true);
}

internal sealed class ReactiveProperty<T> : IReadOnlyReactiveProperty<T>, IDisposable
{
    public ReactiveProperty(T initialValue, IEqualityComparer<T> comparer = null);
    public T Value { get; }
    public bool SetValue(T value);
    public IDisposable Subscribe(Action<T> observer, bool notifyCurrent = true);
    public void Dispose();
}
```

`ArchitectureRevision.md` の「ViewModelとReactiveProperty」節の仕様をそのまま実装する。

## 満たすべき契約

`DesignPhilosophy.md` の「### ReactiveProperty」に列挙されている条件を実装する。

- コンストラクタで `IEqualityComparer<T>` を任意に受け取る。**未指定時だけ** `EqualityComparer<T>.Default` を使用し、同値なら通知しない
- `SetValue` は値が実際に変わったかを `bool` で返す
- `Subscribe` は解除用 `IDisposable` を返す。同じ `IDisposable` を複数回 Dispose しても安全にする
- `notifyCurrent` で購読開始時に現在値を通知するかを明示する
- **通知と値の更新はメインスレッドに限定する。** バックグラウンドから呼ばれた場合は例外を送出し、黙って壊れないようにする
- `Dispose` 後は購読をすべて解除し、更新と新規購読を拒否する
- 通知中に購読者が `Subscribe` / `Dispose` を呼んでも、コレクション変更例外を起こさない（通知はスナップショットに対して行う）
- 1人の購読者が例外を投げても、残りの購読者へ通知を続ける。例外はまとめて記録する
- Unity のシリアライズ、永続化、グローバルな Event Bus として使用しない（用途をXMLドキュメントに明記）

配列や `List` を値にする場合は内容比較用の comparer を渡す必要がある。これは**利用側の責務**であり、`ReactiveProperty` 自身は既定で参照比較になることをXMLドキュメントへ明記する。

## ファイル構成

| パス | 名前空間 | 内容 |
| --- | --- | --- |
| `Core/Internal/IReadOnlyReactiveProperty.cs`（新規） | `SymphonyFrameWork.Core` | 読み取り専用契約 |
| `Core/Internal/ReactiveProperty.cs`（新規） | `SymphonyFrameWork.Core` | 実装 |

`CodeGuidelines.md` の「1ファイルには1つの公開型だけを定義し、ファイル名を型名と一致させる」に従い2ファイルに分ける。`Internal` は名前空間へ含めない（`Core/Internal/SymphonyLazyObject.cs` と同じ扱い）。

## 依存方向

`Core` は Runtime・Editor・Samples へ依存しない。`ReactiveProperty` は純粋な C# と `UnityEngine` のスレッド判定のみに依存する。

この Round では**利用側を作らない**。ViewModel は Round G 以降で追加する。

## エラー処理

| 状況 | 挙動 |
| --- | --- |
| メインスレッド外からの `SetValue` / `Subscribe` / `Dispose` | `InvalidOperationException`。黙って壊れるより即座に失敗させる |
| `Dispose` 後の `SetValue` | `ObjectDisposedException` |
| `Dispose` 後の `Subscribe` | `ObjectDisposedException` |
| `Subscribe(null)` | `ArgumentNullException` |
| 購読者が例外を投げた | 残りへ通知を続け、最後にまとめて記録する |
| 同じ購読解除 `IDisposable` の多重 Dispose | 無害（2回目以降は何もしない） |

## テストの置き場と種別

**この Round に自動テストは追加できない。**

`ReactiveProperty<T>` は `Core` の `internal` であり、テストアセンブリからは参照できない。テストは公開APIの範囲に留める方針のため、`Core/AssemblyInfo.cs` へテストアセンブリを追加しない。

**この判断のリスクを明記しておく。** `ReactiveProperty<T>` は等値比較、購読解除、Dispose 後の拒否、通知中の購読変更、例外の分離といった**純粋なロジックの塊**であり、単体テストの費用対効果が最も高い種類の型である。かつ Round G〜J の全 ViewModel がこれに依存するため、ここに欠陥があると後続すべてに波及する。

自動テストを持たない代わりに、次で担保する。

- 実装後に私（レビュー担当）が全行を読み、上記「満たすべき契約」の各項目が満たされているか1つずつ照合する
- Round G で最初の ViewModel を作った時点で、Editor Window の実挙動から間接的に検証する

`Core/AssemblyInfo.cs` にだけテストアセンブリを許可する案も検討したが、**自動テスト無しで進めることを選択した**（ユーザー決定）。テストを公開APIの範囲に留める方針を、Core についても例外なく適用する。

したがって**レビューでの全行照合が唯一の品質保証になる。** ステップ3では「満たすべき契約」の各項目を1つずつコードと突き合わせること。通常のレビューより踏み込んだ確認が必要になる。

## 動作確認手順

1. `uloop-compile` がエラー0・警告0
2. 既存テストが全数成功（EditMode 43 / PlayMode 4）
3. **この Round では利用側を作らないため、実行時の挙動確認は無い。** Round G で最初の ViewModel を組んだ時点で確認する

## バージョン判断

**マイナー（2.10.0）。** `internal` 型の追加のみで、公開APIに変更が無い。

`CONTRIBUTING.md` §6 の表では「内部実装のみの変更は原則不要」だが、後続 Round の基盤として意味のある追加であり、CHANGELOG に残す価値があるためマイナーとして刻む。

## この Round で触るバージョン関連ファイル

| ファイル | 触る箇所 |
| --- | --- |
| `Assets/SymphonyFrameWork/package.json` | `version` を `2.9.0` → `2.10.0` |
| `Assets/SymphonyFrameWork/CHANGELOG.md` | `## [2.10.0]` の見出しと本文 |
| `Assets/SymphonyFrameWork/README.md` | 「現在のバージョン」を `2.10.0` へ |

`AGENTS.md` は触らない（利用側から見える API が増えないため）。
