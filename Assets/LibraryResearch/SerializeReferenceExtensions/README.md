# SerializeReferenceExtensions 基本サンプル

Unityの`SerializeReference`で保持するポリモーフィックなデータを、SerializeReferenceExtensionsの型選択UIで編集するサンプルです。共通インターフェースに対して、加算・乗算・クランプの処理をInspectorから切り替えられます。

## 実行方法

1. `Data/SerializeReferenceSampleData.asset`をInspectorで選択します。
2. `Operation`の型選択メニューから処理を選びます。
3. 選択した処理固有の値を設定します。
4. `Scene/SerializeReferenceBasicSample.unity`を開いてPlay Modeを開始します。
5. スライダーで入力値を変え、実行ボタンで結果を更新します。

## 用意している処理

- `AddOperation`：入力値に指定値を加算
- `MultiplyOperation`：入力値に指定倍率を乗算
- `ClampOperation`：入力値を最小値と最大値の範囲に制限

各処理の`AddTypeMenu`属性が、Inspectorの型選択メニューに表示するパスを定義します。`SerializeReferenceSampleData`のフィールドには`SerializeReference`と`SubclassSelector`を指定しています。

## 主なファイル

- `Scripts/INumberOperation.cs`：すべての数値処理が実装する契約
- `Scripts/AddOperation.cs`：加算処理
- `Scripts/MultiplyOperation.cs`：乗算処理
- `Scripts/ClampOperation.cs`：範囲制限処理
- `Scripts/SerializeReferenceSampleData.cs`：managed referenceを保持するScriptableObject
- `Scripts/SerializeReferenceBasicSample.cs`：選択した処理を実行する簡易UI
- `Data/SerializeReferenceSampleData.asset`：処理の型と値を保存するデータ
- `Scripts/LibraryResearch.SerializeReferenceExtensions.asmdef`：拡張ライブラリを参照する隔離アセンブリ

## 処理を追加する方法

1. `INumberOperation`を実装するSerializableなクラスを、クラス名と同じ名前のファイルに作成します。
2. 型に`[Serializable]`を付けます。
3. 必要に応じて`[AddTypeMenu("任意のメニュー/表示名")]`を付けます。
4. Unityへ戻ると、既存データの型選択メニューから新しい処理を選べます。

