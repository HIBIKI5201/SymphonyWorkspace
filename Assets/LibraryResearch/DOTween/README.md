# DOTween 基本サンプル

DOTweenによるTransformの移動、Ease、ループ、再生制御を確認するサンプルです。サンプルGameObjectが指定距離を往復します。

## 実行方法

1. DOTween本体が`Assets/Plugins/Demigiant/`へ導入されていることを確認します。
2. `Scene/DOTweenBasicSample.unity`を開きます。
3. Play Modeを開始すると、GameObjectが自動的に往復移動します。
4. 画面上のボタンから再開始、一時停止・再開、完了・破棄を試せます。

`Data/DOTweenSampleSettings.asset`から移動量、所要時間、Easeを変更できます。

## Tweenの構成

- `DOMove`：開始位置から設定した移動量だけTransformを移動
- `SetEase`：補間カーブを設定
- `SetLoops(-1, LoopType.Yoyo)`：無限の往復ループ
- `Pause` / `Play`：一時停止と再開
- `Kill`：既存Tweenを破棄

GameObjectが破棄されたときは`OnDestroy()`でTweenを明示的に`Kill`しています。

## 主なファイル

- `Scripts/DOTweenBasicSample.cs`：Tweenの生成と再生制御
- `Scripts/DOTweenSampleSettings.cs`：サンプル設定用ScriptableObject
- `Data/DOTweenSampleSettings.asset`：シーンから参照する設定データ
- `Scripts/LibraryResearch.DOTween.asmdef`：`DOTween.dll`を参照する隔離アセンブリ

## リポジトリ上の注意

DOTween本体はライセンス管理されたローカルアセットとして`Assets/Plugins/`に置かれ、`.gitignore`の対象です。このREADME、サンプルコード、シーン、設定データは公開できますが、クローン直後はDOTween本体を別途導入しないとこのアセンブリをコンパイルできません。

