# AssetProtection

## 目的

`SymphonyAssetProtector` は現在、`Assets/SymphonyFrameWork/` 配下のアセット移動を一律で差し戻すか、ダイアログで確認するかの2択しかなく、切り替えは `Tools/SymphonyFrameWork/Settings/Symphony Asset Lock` のメニュートグルに隠れている。開発中に意図してファイルを動かしたい場面（テストフォルダの移設など）で毎回メニューを探すことになり、設定の所在も分かりにくい。

保護の強さを **有効化／警告／無効化** の3段階にし、`Project Settings > SymphonyFrameWork` から選べるようにする。

あわせて、この機会に既存の `EditorPrefs` 実装をすべて廃止する。理由は2つ。

1. **Runtime コードが `UnityEditor` を参照している。** `ServiceLocator.cs` と `ServiceLocateManager.cs` が `#if UNITY_EDITOR` 内で `EditorPrefs` を読んでおり、`CodeGuidelines.md` の「RuntimeコードからEditor APIを参照しない。`#if UNITY_EDITOR` で囲んでも例外を作らない」に違反している。
2. **設定の所在が不透明。** `EditorPrefs` はレジストリ／plist に入るため、値の確認・リセット・共有ができない。

設定は `UserSettings/SymphonyFrameWork/` 配下の `ScriptableSingleton` へ集約する。`UserSettings/` は `.gitignore` 済みなので、`EditorPrefs` と同じ「開発者ごとの設定」という性質を保ったまま、ファイルとして可視化・削除できるようになる。

## 公開API

利用側から新たに呼べる API は追加しない。設定は Project Settings の UI からのみ変更する。

新設する型はすべて Editor アセンブリに置き、`public` にするのは Unity が発見する必要がある型だけに限る。

| 型 | 公開範囲 | 根拠 |
| --- | --- | --- |
| `AssetProtectionModeEnum` | `public` | 設定アセットのシリアライズ対象であり、Inspector と SettingsProvider から参照される |
| `SymphonyUserSettingConfig` | `public sealed`（`ScriptableSingleton<T>`） | `AutoEnumGeneratorConfig` と同じ扱い。Unity が型を発見して生成する |

`DesignPhilosophy.md` の「公開範囲」では Config は `internal` が原則だが、`ScriptableSingleton` は Unity がインスタンス生成を担うため、既存の `AutoEnumGeneratorConfig`・`AssetStoreToolsPackagerData` と同様に Editor アセンブリ内の `public` とする。Editor アセンブリは利用側のビルドに含まれないため、パッケージ利用者へ公開APIが増えるわけではない。

```csharp
/// <summary> Framework配下のアセット移動に対する保護の強さ。 </summary>
public enum AssetProtectionModeEnum
{
    /// <summary> 移動を常に差し戻す。 </summary>
    Enabled,

    /// <summary> ダイアログで続行するか差し戻すかを選ばせる。 </summary>
    Warning,

    /// <summary> 移動を通し、Consoleへ通常ログを出す。 </summary>
    Disabled,
}
```

命名は更新済み `CodeGuidelines.md` の「enum は PascalCase + `Enum`」に従う。

## ファイル構成

### 新規

| パス | 名前空間 | 内容 |
| --- | --- | --- |
| `Editor/Configs/ConfigData/AssetProtectionModeEnum.cs` | `SymphonyFrameWork.Editor` | 上記 enum |
| `Editor/Configs/ConfigData/SymphonyUserSettingConfig.cs` | `SymphonyFrameWork.Editor` | `ScriptableSingleton<SymphonyUserSettingConfig>`。`[FilePath(EditorSymphonyConstant.USER_SETTING_FILE_PATH + nameof(SymphonyUserSettingConfig) + ".asset", FilePathAttribute.Location.ProjectFolder)]`。保持する値は保護モードと ServiceLocator の3つのログ切り替え |

`AutoEnumGeneratorConfig` と同じ構成にする（`[SerializeField] private` ＋ setter で `Save(true)`）。

### 変更

| パス | 変更内容 |
| --- | --- |
| `Core/Editor/EditorSymphonyConstant.cs` | `USER_SETTING_FILE_PATH = "UserSettings/" + SymphonyConstant.SYMPHONY_FRAMEWORK + "/"` を追加。「ウィンドウのコンフィグ」節の `EditorPrefs` キー3つと既定値3つを削除 |
| `Editor/SymphonyAssetProtector.cs` | 3分岐化。`[MenuItem]` トグル2つと static constructor の `EditorApplication.delayCall` を削除。フックを `OnWillMoveAsset` へ変更 |
| `Editor/SettingProvider/SymphonySettingProvider.cs` | 保護モードのポップアップと ServiceLocator ログ3項目を描画 |
| `Editor/Configs/SymphonyConfigManager.cs` | `UserSettings/SymphonyFrameWork/` フォルダが無ければ生成する |
| `Editor/Administrator/UITK/CS/ServiceLocatorWindow.cs` | Toggle の保存先を `EditorPrefs` から新 Config へ |
| `Editor/PackageInitializer.cs` | Editor 起動時に Config を読み、Runtime のログフラグへ注入する |
| `Runtime/System/ServiceLocator/ServiceLocator.cs` | `EditorPrefs` 参照を除去 |
| `Runtime/System/ServiceLocator/Internal/ServiceLocateManager.cs` | 同上 |
| 新規 `Runtime/System/ServiceLocator/Internal/ServiceLocateLogOption.cs` | Runtime 側が持つログ可否フラグ。`internal static`。既定値は現行と同じ（登録 `true` / 取得 `false` / 破棄 `true`） |

## 依存方向

Runtime から Editor への参照を作らない。設定の流れは `DesignPhilosophy.md` の「Composition が Config を読み、必要な値だけを各層へ注入する」に従う。

```text
SymphonyUserSettingConfig（Editor / Infrastructure）
        │ 読み取り
        v
PackageInitializer（Editor / Composition）
        │ internal setter で注入
        v
ServiceLocateLogOption（Runtime / internal）
        │ 参照
        v
ServiceLocator, ServiceLocateManager（Runtime）
```

`ServiceLocateLogOption` は `internal` なので、Editor アセンブリからの書き込みは既存の `[assembly: InternalsVisibleTo("SymphonyFrameWork.Editor")]`（`Runtime/AssemblyInfo.cs`）で解決する。

ログ呼び出し自体は `#if UNITY_EDITOR` で囲んだままにする。これは `UnityEditor` への**参照**ではなくコンパイルシンボルなので、ガイドラインの禁止対象ではなく、Player ビルドに不要な処理を含めないという要求を満たす。

## 各モードの挙動

フックは `OnWillMoveAsset(string sourcePath, string destinationPath)` へ変更する。戻り値で移動を止められるため、現行の「移動させてから `AssetDatabase.MoveAsset` で戻す」方式より副作用が少なく、`AssetDatabase.Refresh()` も不要になる。

| モード | 戻り値 | 通知 |
| --- | --- | --- |
| `Enabled` | `AssetMoveResult.FailedMove` | `EditorUtility.DisplayDialog`（OK のみ）で移動できない旨を表示 |
| `Warning` | ダイアログの選択に従い `DidNotMove` または `FailedMove` | `EditorUtility.DisplayDialog` の2択（「移動する」／「元に戻す」） |
| `Disabled` | `AssetMoveResult.DidNotMove` | `Debug.Log` のみ |

`AssetMoveResult.DidNotMove` は「このポストプロセッサは何もしない＝Unity の通常処理に任せる」という意味であり、移動は成立する。

### ダイアログを1回の移動操作につき1回にする

`OnWillMoveAsset` は移動対象ごとに呼ばれるため、複数ファイルやフォルダを一括で動かすと `Warning` モードでダイアログが連続して出る。これを避けるため、**1回の移動操作の中では最初の選択を記憶して再利用する**。

- `Warning` モードで選択を得たら、その結果を static フィールドへ保持する
- `OnPostprocessAllAssets` が呼ばれた時点で1回の操作が終わったとみなし、保持した選択を破棄する
- `Enabled` モードでも同様に、1操作につきダイアログは1回に抑える

## エラー処理

- 保護判定は失敗し得ない。異常系は例外ではなく戻り値で表現する。
- `SymphonyUserSettingConfig.instance` は `ScriptableSingleton` が必ず値を返すため null 検査は不要。ファイルが無ければ既定値でインスタンスが作られる。
- `UserSettings/SymphonyFrameWork/` の生成に失敗した場合（権限等）は `Debug.LogWarning` を出し、設定は既定値のまま動作を継続する。保護機能が使えないだけで Editor の動作は止めない。

## 影響範囲

**破壊的変更ではない。** 公開APIのシグネチャは変わらず、シリアライズ形式にも影響しない。

利用側から観測できる変更は次の3点。

1. **`EditorPrefs` に保存されていた既存の設定値は引き継がれない。** 保護モードは既定の `Enabled`、ServiceLocator のログ設定も既定値から始まる。移行スクリプトは書かない（開発者ごとのローカル設定であり、再設定コストが低いため）。
2. **`Tools/SymphonyFrameWork/Settings/Symphony Asset Lock` メニューが無くなる。** Project Settings へ移動する。
3. **`UserSettings/SymphonyFrameWork/` が新しく生成される。** `.gitignore` 済みのためコミット対象にはならない。

## テストの置き場と種別

自動テストは追加しない。理由は、この機能の中核が `AssetPostprocessor` のコールバックと `EditorUtility.DisplayDialog`（モーダル）であり、テストランナーから再現するとダイアログで停止するため。

代わりに、下記「動作確認手順」を手動で実施する。将来 `OnWillMoveAsset` の判定ロジックを純粋関数として切り出せた場合は、EditMode テストの対象にする。

## 動作確認手順

`uloop-compile` がエラー0・警告0であることを確認したうえで、Unity Editor で次を実施する。

1. **Enabled（既定）** — `Project Settings > SymphonyFrameWork` で保護モードが `Enabled` であることを確認し、`Assets/SymphonyFrameWork/` 配下の任意のファイルを別フォルダへドラッグする。ダイアログが1回出て、移動が成立しないこと。
2. **Warning** — モードを `Warning` に変更し、同じ操作を行う。2択ダイアログが出る。「移動する」を選ぶと移動が成立し、「元に戻す」を選ぶと元の位置に留まること。
3. **Warning の一括移動** — `Warning` のまま複数ファイルを選択して一度に移動する。**ダイアログが1回だけ**出て、その選択が全ファイルへ適用されること。
4. **Disabled** — モードを `Disabled` に変更し、同じ操作を行う。ダイアログは出ず、移動が成立し、Console に `Debug.Log` が1件出ること。
5. **永続化** — Editor を再起動し、選んだモードが保持されていること。`UserSettings/SymphonyFrameWork/SymphonyUserSettingConfig.asset` が存在すること。
6. **ServiceLocator ログ** — Project Settings でログ3項目を切り替え、Play Mode でサービスを登録・取得・破棄し、Console へのログ出力が設定どおり変わること。Editor を再起動しても設定が保持されること。
7. **Runtime の Editor 参照除去** — `Runtime/` 配下を全文検索し、`EditorPrefs` と `using UnityEditor` が1件も残っていないこと。

## バージョン判断

**マイナー（2.5.0）。**

公開APIの追加も削除も無く、既存のシグネチャと挙動は変わらない。設定の保存先変更とメニュー廃止は Editor の操作方法の変更であり、利用側のコードをコンパイルエラーにしない。`EditorPrefs` の値が引き継がれない点は利用側に見える変更なので、CHANGELOG の `Change` へ影響を明記する。
