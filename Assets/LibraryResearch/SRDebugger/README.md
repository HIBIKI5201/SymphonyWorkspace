# SRDebugger 基本サンプル

SRDebuggerの導入状態を確認し、デバッグパネルを開いたりUnity Consoleへログを出したりするサンプルです。SRDebuggerは有料アセットのため、このリポジトリには本体を含めていません。

## 実行方法

1. SRDebuggerをUnity Asset StoreのMy Assetsからプロジェクトへインポートします。
2. `Scene/SRDebuggerBasicSample.unity`を開きます。
3. Play Modeを開始します。
4. `Open SRDebugger panel`でデバッグパネルを表示します。
5. `Write a sample log`で設定済みメッセージをUnity Consoleへ出力します。

`Data/SRDebuggerSampleSettings.asset`では、起動時にパネルを開くかどうかと、サンプルログの内容を変更できます。

## 未導入時の動作

サンプルはReflectionで`SRDebug`型を検索するため、SRDebugger本体がない状態でもコンパイルできます。未導入時は画面に案内を表示し、パネル表示ボタンを押しても例外を発生させません。

この方式は有料アセットを含まない公開可能な調査サンプルにするためのものです。実プロジェクトでSRDebuggerを必須依存にする場合は、導入後に専用asmdef参照と直接API呼び出しへ切り替える方法もあります。

## 主なファイル

- `Scripts/SRDebuggerBasicSample.cs`：導入検出、パネル表示、ログ出力
- `Scripts/SRDebuggerSampleSettings.cs`：サンプル設定用ScriptableObject
- `Data/SRDebuggerSampleSettings.asset`：シーンから参照する設定データ
- `Scripts/LibraryResearch.SRDebugger.asmdef`：他のサンプルから隔離するアセンブリ

## リポジトリ上の注意

SRDebugger本体は`.gitignore`の対象です。README、サンプルコード、シーン、設定データにはSRDebugger本体のファイルやライセンス対象コードを含めていません。

