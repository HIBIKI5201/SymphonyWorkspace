# SceneManagerConfig から SceneLoadConfig への改名 — 3.0.0 へ延期

> **状態: 未実施。2.x では行わない。**
> Round L3 として着手したが、**利用側の設定値が失われることを実測したため中断した。**
> 破壊的変更として 3.0.0（Phase 6）で扱い、CHANGELOG で手動での改名を案内する。
> この文書は、そのときに同じ調査を繰り返さないための記録である。

## 目的

`SceneManagerConfig` を `SceneLoadConfig` へ改名する。サブシステム名は `SceneLoad`（名前空間 `SymphonyFrameWork.System.SceneLoad`、Facade は `SceneLoader`）であり、`SceneManager` という名前は Unity の `UnityEngine.SceneManagement.SceneManager` を連想させて紛らわしい。

## なぜ 2.x で自動移行できないのか

### 設定アセットは型名でファイルを引いている

```csharp
// SymphonyConfigLocator
Resources.Load<T>($"SymphonyFrameWork/{typeof(T).Name}");
```

型を改名すると `SceneLoadConfig.asset` を探しに行く。存在しないため `SymphonyConfigManager.FileCheck<T>()` が**既定値で新しいアセットを作り**、既存の `SceneManagerConfig.asset` は誰からも参照されなくなる。**エラーもログも出ない。**

`[MovedFrom]` はこの経路に効かない。あれは型の解決を救う属性であり、`Resources.Load` のパス解決には関与しない。

### 実測した障害

`Editor/Configs/SymphonyConfigMigrator.cs` を作り、`FileCheck` より前に `AssetDatabase.RenameAsset` で旧アセットを改名する方式を実装して検証した。結果は次のとおり。

**1. 移行処理は動いたが、設定値が失われた。**

```
[SymphonyConfigMigrator] 設定型の改名に伴い 'SceneManagerConfig.asset' を
'SceneLoadConfig.asset' へ改名しました。設定値は引き継がれています。
```

このログが出た一方で、改名後のアセットは `_isResetAndLoadOnPlay: 0` だった。元の値は `1`（git のコミット済み内容で確認）。**移行処理は自分の失敗を検知できていない。**

**2. クラス改名後、旧アセットをそもそもロードできない。**

`m_Script` の GUID が完全に一致しているにもかかわらず、`AssetDatabase.LoadAssetAtPath<ScriptableObject>` が `null` を返す。

| | 値 |
| --- | --- |
| アセットが参照する GUID | `798a2583624225c4fa38a13aba398a0e` |
| 改名後の `.cs.meta` の GUID | `798a2583624225c4fa38a13aba398a0e` |
| ロード結果 | `null` |

**「`.cs` と `.meta` を一緒に改名して GUID を維持すれば `m_Script` で解決できる」という前提が成立していない。** ロードできなければ、移行処理は改名対象を見つけられないか、見つけても中身が空になる。

### 未確定なこと

次のどちらかを確定できていない。**再開時にはここから調べる。**

- Unity が `ScriptableObject` の型解決に、GUID に加えてクラス名の一致を要求しているのか
- 段階的にファイルを編集したことで AssetDatabase が中途半端な状態になっていただけなのか

後者の可能性は無視できない。**同じセッションで「増分コンパイルの結果を実体と取り違える」誤りを2度起こしている**（Round L1 の非推奨警告0件、Round L2 直前の警告件数）。再開時は**クリーンな状態から全ファイルを一度に適用し、Unity を1回だけ再読み込みさせて判定する**こと。

## 3.0.0 での進め方

自動移行を書かず、破壊的変更として扱う。

1. `Runtime/Configs/Internal/SceneManagerConfig.cs` を `SceneLoadConfig.cs` へ改名（`.meta` も一緒に動かす）
2. `Editor/Configs/Drawer/SceneManagerConfigDrawer.cs` を `SceneLoadConfigDrawer.cs` へ改名
3. 参照箇所を更新する（`SymphonyOrchestrator` 2箇所、`SceneLoader.AfterSceneLoad` 1箇所、`SymphonyConfigManager` 1箇所、Drawer 1箇所）
4. シリアライズ済みフィールド名（`_isResetAndLoadOnPlay`、`_initializeSceneList`、`_resetIgnoreSceneList`）は**変更しない**
5. CHANGELOG の Breaking へ**手動での改名手順**を書く

### CHANGELOG へ書く移行手順の案

> `Assets/Resources/SymphonyFrameWork/SceneManagerConfig.asset` を
> `SceneLoadConfig.asset` へリネームしてください。Unity のプロジェクトウィンドウ上で
> 名前を変更すれば、設定値はそのまま引き継がれます。
> リネームしないまま起動すると、既定値の `SceneLoadConfig.asset` が新しく作られ、
> 起動時にロードするシーンの設定が反映されません。

**「リネームしないとどうなるか」を必ず書く。** 沈黙して既定値になる種類の失敗であり、書かなければ利用側は原因にたどり着けない。

### 自動移行を再検討する場合

`SymphonyConfigMigrator` の実装方針自体は妥当だった。再挑戦するなら次を守る。

- `FileCheck` より**前**に実行する。順序が逆だと既定値のアセットが正本になる
- 旧が存在し、新が存在しない場合だけ改名する
- **改名後に設定値が引き継がれたかを検証し、失われていたら復元する。** 今回の実装は「引き継がれています」と無条件にログへ出していた。**検証しない移行処理は、失敗を成功として報告する**
- 利用側のファイルを動かすため、必ずログで知らせる

## 検討したが採らなかった案

**型名とファイル名を切り離す**（属性でファイル名を指定し、`SceneLoadConfig` が `SceneManagerConfig.asset` を読む）。利用側のアセットに一切触れずに済む。

採らない理由は、型名とアセット名の食い違いが恒久的に残ること。この改名が解消しようとしている「名前が実態と合っていない」問題を、ファイル名側へ移し替えるだけである。加えて設定解決に新しい仕組みを増やすことになる。

**改名しない**案も検討したが、`SceneManager` という名前が Unity の型と紛らわしい問題は残る。3.0.0 という破壊的変更を出す機会があるため、そこで解消する。

## 参照

- 同じ Phase の完了済み Round: [SaveStoreRename.md](./SaveStoreRename.md)、[SaveDataLoaderStrategyRename.md](./SaveDataLoaderStrategyRename.md)
- `[MovedFrom]` が `[SerializeReference]` の旧型名を救えることは Round L2 で実証済み。**ただし本件の `Resources.Load` 経路には効かない**
