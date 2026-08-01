# R3 基本サンプル

R3の`ReactiveProperty`と基本的なオペレーターを、カウンターを使って確認するサンプルです。値が変化すると、購読している表示内容が自動的に更新されます。

## 実行方法

1. `Scene/R3BasicSample.unity`を開きます。
2. Play Modeを開始します。
3. `Publish next value`で`ReactiveProperty<int>`の値を更新します。
4. 設定したマイルストーン以上になると、条件付き購読のメッセージが表示されます。
5. `Reset`で初期値へ戻します。

`Data/R3SampleSettings.asset`から初期値、加算量、マイルストーンを変更できます。

## データの流れ

- `ReactiveProperty<int>`が現在のカウントを保持します。
- `Select`が値を2倍した表示文字列へ変換します。
- `Where`がマイルストーン以上の値だけを通します。
- `Subscribe`が変換・抽出された値を受け取ります。
- `AddTo(this)`が購読の寿命をGameObjectに結び付けます。

## 主なファイル

- `Scripts/R3BasicSample.cs`：ReactiveProperty、購読、簡易UI
- `Scripts/R3SampleSettings.cs`：サンプル設定用ScriptableObject
- `Data/R3SampleSettings.asset`：シーンから参照する設定データ
- `Scripts/LibraryResearch.R3.asmdef`：`R3.Unity`を参照する隔離アセンブリ

## 確認できる要素

- `ReactiveProperty<T>`による状態管理
- `Select`による値の変換
- `Where`によるイベントの絞り込み
- `Subscribe`による変更通知の受信
- GameObjectの破棄に合わせた購読の終了

