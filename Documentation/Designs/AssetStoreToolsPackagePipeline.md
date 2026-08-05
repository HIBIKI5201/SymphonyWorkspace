# AssetStoreToolsPackagePipeline

Issue: [#124](https://github.com/HIBIKI5201/SymphonyFramework/issues/124)

## 目的

Asset Store Tools Packager の出力内容は、現在 3 つの固定オプション（`Export Mode` の `PackageModeEnum`、`Create ZIP File`、`Used Dependencies`）の組み合わせでしか表現できません。次の 2 つが困っています。

1. **出力の組み合わせを名前を付けて保存できません。** 「Singles + Used Dependencies + ZIP」で出したい場面と、「全部入りで ZIP 無し」で出したい場面があり、ウィンドウを開くたびに手でトグルを合わせ直しています。
2. **出力手順を利用側から拡張できません。** ZIP 化の代わりに独自の圧縮をかけたい、出力後に社内ツールへ通知したい、といった要求へ、フレームワークを書き換えずに応える手段がありません。

そこで、**出力手順を順序付きの Strategy 列（パイプライン）として `ScriptableObject` へ保存し、ウィンドウではその名前で選ぶ**形に変えます。

同時に、[#119](https://github.com/HIBIKI5201/SymphonyFramework/issues/119) で非推奨にした `PackageModeEnum.Combine` の扱いを決めます。**統合パッケージ出力そのものは機能として残します。** 差分インポートの単位にならないという 3.4.0 の判断は変わらないため、**既定テンプレートには含めず、必要な人が自分でパイプラインへ追加する**位置付けにします。「enum の選択肢としては消すが、Strategy としては残す」ということです。

### 既存の何では足りないか

| 既存 | 足りない点 |
| --- | --- |
| `PackageModeEnum`（`Nothing` / `Singles` / `Combine`） | フラグの組み合わせしか表現できず、順序も名前も持てない。`Combine` を非推奨にすると `Singles` と `Nothing` しか残らず、enum 自体が意味を失う |
| `createZip` / `usedDependencies` の `bool` 引数 | 増えるたびに `Export` のシグネチャが伸びる。利用側が独自の手順を差し込めない |
| `PackagerConfig.json` | 「何を詰めるか」（除外・強制包含）の設定であり、「どう出すか」の手順ではない |

## 全体像

```text
AssetStoreToolsPackagerData（ProjectSettings）
  └─ Pipelines : AssetStoreToolsPackagePipeline[]   ← Project Settings で配列としてアサイン
                   └─ Steps : List<AssetStoreToolsPackageStepStrategy>  ← [SerializeReference, SubclassSelector]
                        ├─ AssetStoreToolsSinglePackageStrategy
                        ├─ AssetStoreToolsUsedDependenciesStrategy
                        └─ AssetStoreToolsCreateZipStrategy
```

Packager ウィンドウの `Export Mode` は、この配列から 1 つを選ぶポップアップになります。選択肢の表示名は `ScriptableObject` の名前（アセット名）です。

### 2 段階実行

**Strategy は `Plan` と `Execute` の 2 段階を持ち、パイプライン全体で「全 Strategy の `Plan` → 全 Strategy の `Execute`」の順に走ります。** 各段階の中では一覧の順序どおりに実行します。

| 段階 | 役割 | この段階を実装する既定 Strategy |
| --- | --- | --- |
| `Plan` | 出力対象アセットを絞り込む。ファイルは書かない | `AssetStoreToolsUsedDependenciesStrategy` |
| `Execute` | 実際にファイルを出力する | `AssetStoreToolsSinglePackageStrategy`、`AssetStoreToolsCombinePackageStrategy`、`AssetStoreToolsCreateZipStrategy` |

2 段階にする理由は 2 つあります。

- **確認ウィンドウが「何が出力されるか」を実行前に提示するため。** 現在の `CreatePlan` → 確認 → `Export(plan)` の流れを維持します。絞り込みが `Execute` の中で起きると、確認ウィンドウに出す一覧が実物と食い違います。
- **Issue が指定したテンプレート順（Single → Used Dependence → Create Zip）を、そのまま正しく動く順序にするため。** 単一段階だと「絞り込みは出力より前でなければならない」という暗黙の制約が生まれ、テンプレートの順序が誤りになります。2 段階なら、`Used Dependencies` が一覧上どこにあっても絞り込みが先に走ります。

`Execute` 段階の順序は利用者の責任です。`Create Zip` を先頭に置けば空のフォルダを圧縮します。**ウィンドウと確認ウィンドウが順序をそのまま表示するため、誤りは目視できます。** 実行順の自動並べ替えは行いません。順序をフレームワークが勝手に決めると、独自 Strategy を差し込む位置を利用側が制御できなくなるためです。

### 出力時バージョンの書き出しは Strategy にしない

`ExportedVersion.json` の書き出しと `AssetDatabase.Refresh` は、**パイプラインの前段としてランナーが必ず実行します。** Strategy にしません。

理由は、これが「出力の一種」ではなく、**すべての出力 Strategy が前提にする準備**だからです。`AssetStoreToolsSinglePackageStrategy` はこのファイルをパッケージへ含め、差分インポートはこのファイルのリビジョンを見ます。パイプラインから外せてしまうと、外した瞬間に 3.3.0 以降のバージョンログ機能が静かに壊れます。

## 公開API

利用側が自作 Strategy を実装できることが Issue の要件なので、Strategy の契約と、そこから触る型は `public` にします。`DesignPhilosophy.md` の「公開範囲」で `public` を認めている **「利用側が実装・継承する契約」** と **「公開エントリポイントの引数・戻り値として境界を越える Info、Value Object」** に該当します。

### AssetStoreToolsPackageStepStrategy（新規・public abstract）

```csharp
namespace SymphonyFrameWork.Editor
{
    /// <summary> パッケージ出力の1手順を表す拡張点。 </summary>
    [Serializable]
    public abstract class AssetStoreToolsPackageStepStrategy
    {
        /// <summary> ウィンドウと確認ウィンドウへ表示する名前。 </summary>
        public virtual string DisplayName => GetType().Name;

        /// <summary> 出力対象を絞り込む段階。既定では何もしない。 </summary>
        protected internal virtual void Plan(AssetStoreToolsPackagePlan plan) { }

        /// <summary> 出力を行う段階。既定では何もしない。 </summary>
        protected internal virtual void Execute(AssetStoreToolsPackageExportContext context) { }
    }
}
```

`protected internal` にするのは、**派生型（別アセンブリを含む）から `override` でき、かつランナー（同一アセンブリ）から呼べる**組み合わせがこれだけだからです。`protected` だけではランナーが呼べず、`public` にすると利用側がパイプラインを介さずに直接叩けてしまいます。

> 検証済み: `protected internal` は「同一アセンブリ **または** 派生型」からのアクセスを許可します（C# 言語仕様の `protected internal`。`private protected` ではありません）。ランナーは `SymphonyFrameWork.Editor` に置くため呼び出せます。
>
> **別アセンブリで `override` する場合、オーバーライド側は `protected` として宣言する必要があります**（C# の規則。`protected internal` のままでは `CS0507` になります）。同一アセンブリ内の既定 Strategy は `protected internal` のまま書けます。この差は `Documentation~/EditorTools.md` の自作 Strategy の書き方へ明記します。

### 既定の Strategy（新規・public sealed）

| 型 | 段階 | 役割 | 既定テンプレート |
| --- | --- | --- | --- |
| `AssetStoreToolsSinglePackageStrategy` | `Execute` | ディレクトリごとに `.unitypackage` を出力し、`PackageManifest.json` を書く | **含む** |
| `AssetStoreToolsUsedDependenciesStrategy` | `Plan` | 使用中アセットと強制包含拡張子だけへ絞る | **含む** |
| `AssetStoreToolsCreateZipStrategy` | `Execute` | 出力フォルダを ZIP 化する | **含む** |
| `AssetStoreToolsCombinePackageStrategy` | `Execute` | 全ディレクトリを 1 つの `.unitypackage` へまとめる | **含まない** |

`AssetStoreToolsCombinePackageStrategy` に `[Obsolete]` は付けません。**利用者が明示的に選んだときだけ動く拡張オプションとして残す**のがこの Issue での判断です。代わりに次の 2 つで注意を促します。

- 既定テンプレートへ含めない
- XML ドキュメントと確認ウィンドウで「差分インポートの対象にならない」ことを示す。パイプラインに `AssetStoreToolsSinglePackageStrategy` が無く `AssetStoreToolsCombinePackageStrategy` だけがある場合、実行時に警告ログを出す（現行の `Export` と同じ文面を維持）

### AssetStoreToolsPackagePlan / PlanEntry（internal → public へ変更）

自作 Strategy が絞り込みを行うために必要なので公開します。**フィールドを公開プロパティへ変え、変更手段は「絞り込み」だけに限定します。**

```csharp
public sealed class AssetStoreToolsPackagePlan
{
    /// <summary> 計画を組み立てたパイプラインの名前。 </summary>
    public string PipelineName { get; }

    /// <summary> 実行する手順。null要素は除去済み。 </summary>
    public IReadOnlyList<AssetStoreToolsPackageStepStrategy> Steps { get; }

    /// <summary> ディレクトリごとの出力内容。 </summary>
    public IReadOnlyList<AssetStoreToolsPackagePlanEntry> Entries { get; }

    /// <summary> 依存関係に関わらず含める拡張子。PackagerConfig.jsonの値。 </summary>
    public IReadOnlyList<string> ForceIncludeExtensions { get; }

    /// <summary> 出力に含まれるアセットの総数。ディレクトリ間の重複は数えない。 </summary>
    public int TotalAssetCount { get; }

    /// <summary> いずれかのディレクトリでPlan段階の絞り込みが行われたか。 </summary>
    public bool IsFiltered { get; }

    internal AssetStoreToolsPackagePlan(...);
}

public sealed class AssetStoreToolsPackagePlanEntry
{
    public string DirectoryPath { get; }
    public string Name { get; }
    public int Version { get; }

    /// <summary> このディレクトリから出力されるアセットのパス一覧。 </summary>
    public IReadOnlyList<string> AssetPaths { get; }

    /// <summary> Plan段階で絞り込みが行われたかを示す。 </summary>
    public bool IsFiltered { get; }

    /// <summary> 条件に合わないアセットを出力対象から外す。追加はできない。 </summary>
    public void FilterAssetPaths(Func<string, bool> predicate);

    internal AssetStoreToolsPackagePlanEntry(...);
}
```

**`FilterAssetPaths` が「絞る」ことしかできないのは意図的です。** Strategy がアセットを追加できると、確認ウィンドウで提示した内容より多くのものが出力され得ます。コンストラクタを `internal` にするのも同じ理由で、計画はランナーだけが組み立てます。

`Steps` と `PipelineName` を計画が持つのは、**確認ウィンドウが手順を表示するためと、`AssetStoreToolsCombinePackageStrategy` が「同じパイプラインに個別出力があるか」を判定するため**です。パイプラインの `ScriptableObject` そのものではなく手順のリストを持たせています。旧APIの互換経路ではメモリ上に手順を組み立てるだけでアセットが存在せず、確認ウィンドウは Domain Reload をまたいで計画を保持するためです。

`AssetStoreToolsPackagePlan.IsFiltered` は「1件でも絞り込まれていれば true」です。計画全体で1つの出力を作る `AssetStoreToolsCombinePackageStrategy` が、ディレクトリ丸ごとの出力と明示指定の出力を切り替えるのに使います。**丸ごと出力側へ倒すと、確認ウィンドウで提示した内容より多くのアセットが出力され得るため、明示指定側へ倒します。**

`Mode` / `CreateZip` / `UsedDependencies` の 3 フィールドは削除します。パイプラインの構成そのものが同じ情報を持つためです。**`AssetStoreToolsPackagePlan` は 3.5.0 時点で `internal` なので、この削除は公開APIの破壊ではありません。**

### AssetStoreToolsPackageExportContext（新規・public sealed）

```csharp
public sealed class AssetStoreToolsPackageExportContext
{
    public AssetStoreToolsPackagePlan Plan { get; }

    /// <summary> 日時を含む出力パッケージ名。 </summary>
    public string PackageName { get; }

    /// <summary> パッケージ出力先の絶対ルートパス。 </summary>
    public string ExportRoot { get; }

    /// <summary> AssetDatabaseから扱えるパッケージ出力先パス。 </summary>
    public string ExportLocalPath { get; }

    /// <summary> 今回のパッケージ出力先となる絶対パス。 </summary>
    public string ExportFullPath { get; }

    /// <summary> パッケージ処理を開始した日時。 </summary>
    public DateTime DateTime { get; }

    internal AssetStoreToolsPackageExportContext(...);
}
```

既存の `AssetStoreToolsPackageContext`（`public readonly ref struct`）を置き換えます。**`ref struct` は `Execute` へ渡せてもフィールドへ保持できず、拡張点の引数として使いにくいためです。**

既存型は `public` なので、その場では削除せず `[Obsolete]` を付けて `Deprecations.md` へ登録し、次のメジャー更新で削除します。

### AssetStoreToolsPackagePipeline（新規・public sealed ScriptableObject）

```csharp
[CreateAssetMenu(
    fileName = "AssetStoreToolsPackagePipeline",
    menuName = "SymphonyFrameWork/Asset Store Tools Package Pipeline")]
public sealed class AssetStoreToolsPackagePipeline : ScriptableObject
{
    /// <summary> 実行する手順。Plan段階とExecute段階の中では、この順序で実行される。 </summary>
    public IReadOnlyList<AssetStoreToolsPackageStepStrategy> Steps => _steps;

    [SerializeReference, SubclassSelector]
    [Tooltip("実行する手順。Plan段階とExecute段階のそれぞれで、この順序どおりに実行される。")]
    private List<AssetStoreToolsPackageStepStrategy> _steps = new();

    /// <summary> 既定テンプレートの手順を持つインスタンスを生成する。 </summary>
    internal static AssetStoreToolsPackagePipeline CreateTemplate();
}
```

利用側が自分のプロジェクトでアセットを作るため `public` にします（`DesignPhilosophy.md` の「Config は `internal`」は Runtime の設定アセットを対象にした規約で、ここは Editor の拡張点にあたります。この判断は下の「懸念」へ記録します）。

### AssetStoreToolsPackager（既存・変更）

```csharp
// 追加
public static void Export(string[] directories, AssetStoreToolsPackagePipeline pipeline);

// 非推奨化（削除はしない）
[Obsolete("パイプラインへ移行しました。Export(string[], AssetStoreToolsPackagePipeline)を使用してください。", error: false)]
public static void Export(string[] directories, PackageModeEnum mode, bool createZip = false, bool usedDependencies = false);

[Obsolete("パイプラインへ移行しました。AssetStoreToolsPackagePipelineを使用してください。", error: false)]
public enum PackageModeEnum : byte { ... }
```

旧 `Export` はフラグからパイプラインをメモリ上に組み立てて新経路へ委譲します。**動作は変えません。**

`PackageModeEnum` 全体を非推奨にするのは、`Combine` を外すと `Singles` と `Nothing` しか残らず enum が意味を失う、という 3.4.0 時点の判断（`Deprecations.md` の「Combine の削除手順」）をそのまま引き継ぐためです。

## ファイル構成

すべて `SymphonyFrameWork.Editor` 名前空間、`Assets/SymphonyFrameWork/Editor/Generator/AssetStoreToolsPackager/` 配下です。既存 18 ファイルと同じ `AssetStoreTools` 接頭辞に揃えます。Editor 配下には `Internal/` の規約が無く、既存ファイルも直下へ並んでいるため、パイプライン関連は `Pipeline/` サブフォルダへまとめます（名前空間は変えません。`CodeGuidelines.md` の「概念レイヤー名は名前空間へ含めない」に従います）。

| パス | 区分 | 内容 |
| --- | --- | --- |
| `Pipeline/AssetStoreToolsPackageStepStrategy.cs` | 新規 | 拡張点の抽象基底 |
| `Pipeline/AssetStoreToolsSinglePackageStrategy.cs` | 新規 | 個別出力 + マニフェスト |
| `Pipeline/AssetStoreToolsCombinePackageStrategy.cs` | 新規 | 統合出力（テンプレート外） |
| `Pipeline/AssetStoreToolsUsedDependenciesStrategy.cs` | 新規 | 使用中アセットへの絞り込み |
| `Pipeline/AssetStoreToolsCreateZipStrategy.cs` | 新規 | ZIP 化 |
| `Pipeline/AssetStoreToolsPackagePipeline.cs` | 新規 | `ScriptableObject` 本体とテンプレート生成 |
| `Pipeline/AssetStoreToolsPackagePipelineRunner.cs` | 新規 | `internal static`。計画組み立てと 2 段階実行 |
| `Pipeline/AssetStoreToolsPackageExportContext.cs` | 新規 | `Execute` 段階のコンテキスト |
| `AssetStoreToolsPackagePlan.cs` | 変更 | `public` 化、`Mode`/`CreateZip`/`UsedDependencies` 削除、`FilterAssetPaths` 追加 |
| `AssetStoreToolsPackager.cs` | 変更 | 出力処理をランナーと Strategy へ移し、旧APIを非推奨化 |
| `AssetStoreToolsPackageContext.cs` | 変更 | `[Obsolete]` を付ける |
| `AssetStoreToolsPackageWindow.cs` | 変更 | `Export Mode` をパイプラインのポップアップへ |
| `AssetStoreToolsPackageConfirmWindow.cs` | 変更 | 表示をパイプライン名と手順一覧へ |
| `AssetStoreToolsPackagerData.cs` | 変更 | パイプライン配列を保持 |
| `../../SettingProvider/AssetStoreToolsPackagerProvider.cs` | 変更 | 配列の編集とテンプレート生成ボタン |
| `../../../Tests/Editor/AssetStoreToolsPackagePipelineTests.cs` | 新規 | 下記「テストの置き場と種別」 |

## 依存方向

すべて Editor レイヤーに閉じます。`Editor → Runtime → Core` の向きは崩しません。

- Strategy と Pipeline は `SymphonyFrameWork.Editor` アセンブリへ置きます。`UnityEditor`（`AssetDatabase.ExportPackage`）に依存するため、Runtime へは置けません。
- `[SerializeReference, SubclassSelector]` の `SubclassSelectorAttribute` は `SymphonyFrameWork.Attribute`（`SymphonyFrameWork` アセンブリ、`Runtime/Attribute/`）にあります。**`SymphonyFrameWork.Editor.asmdef` は `GUID:68a532f43eeefd5408e4e8871b769ee4`（= `SymphonyFrameWork`）を参照済みで、この経路は成立しています。** 確認済み。
- `SubclassSelectorDrawer`（`Editor/Configs/Drawer/`）は `AppDomain.CurrentDomain.GetAssemblies()` を走査するため、`SymphonyFrameWork.Editor` アセンブリで定義された派生型も候補に含みます。`List<T>` フィールドにも対応済みです（`GetType` が `List<>` のジェネリック引数を返す実装になっています）。確認済み。
- 利用側が自作 Strategy を置くアセンブリは、`SymphonyFrameWork.Editor` を参照する Editor 用 asmdef である必要があります。README には書かず、`Documentation~/EditorTools.md` へ記載します。

## エラー処理

このモジュールは Editor ツールであり、既存の方針（**失敗しても `Debug.LogError` / `Debug.LogWarning` を出して続行できる範囲は続行する**）を維持します。例外は投げません。

| 状況 | 扱い |
| --- | --- |
| パイプラインが未選択 / 配列が空 | ウィンドウに `HelpBox` を出し、Export ボタンを無効化する。ログは出さない |
| パイプラインの `Steps` が空 | 実行前に `Debug.LogWarning`。`ExportedVersion.json` の書き出しだけが走る |
| `Steps` に `null` 要素がある（`SubclassSelector` の `<null>`） | **スキップする。** `NullReferenceException` を起こさない |
| 個別 Strategy の `Execute` が例外を投げた | ランナーが `try`/`catch` し、`Debug.LogError` へ Strategy 名と例外を出して**次の Strategy へ進む** |
| `PackagerConfig.json` を読めない | 従来どおり計画を組まずに `null` を返し、出力しない |
| 出力対象ディレクトリが 0 件 | 従来どおり `Debug.LogWarning` して何もしない |

**1 つの Strategy の失敗で全体を止めないのは、既存の `ExportPackage` / `CreateZip` が個別に `try`/`catch` して続行していた挙動を保つためです。** 利用側の自作 Strategy が投げた例外でフレームワーク側の出力まで巻き添えにしません。

## 影響範囲

### 公開API

| 対象 | 変更 | 互換性 |
| --- | --- | --- |
| `AssetStoreToolsPackager.Export(string[], PackageModeEnum, bool, bool)` | `[Obsolete]`（警告） | **維持。** 動作も変えない |
| `AssetStoreToolsPackager.PackageModeEnum` | `[Obsolete]`（警告） | **維持** |
| `AssetStoreToolsPackageContext` | `[Obsolete]`（警告） | **維持。** ランナーからは使わなくなる |
| `AssetStoreToolsPackager.GetPackageDirectories()` | 変更なし | 維持 |
| `AssetStoreToolsPackageWindow.ShowWindow()` | 変更なし | 維持 |
| 上記の新規 `public` 型 | 追加 | 後方互換な追加 |

**破壊的変更はありません。** 既存の呼び出しはコンパイル警告が出るだけで動きます。

### 出力対象の収集を1本化したことによる表示の変化

3.5.0 では、絞り込みの有無で収集方法が 2 つに分かれていました（`CollectExportAssets` と `CollectAllAssets`）。パイプライン化では **絞り込み前の収集を 1 本にし、絞り込みは Plan 段階の Strategy が行う**形へ変えます。

```text
収集（常時）  : 強制包含拡張子に一致する ∨ 通常のフォルダでない
絞り込み（任意）: 強制包含拡張子に一致する ∨ 使用中アセットである
```

この 2 つを合成した結果は 3.5.0 の `CollectExportAssets` と一致します。**絞り込みを行わない場合だけ、確認ウィンドウの一覧へ `.bundle` や `.framework` などフォルダ形式のアセットが増えます。** 3.5.0 の `CollectAllAssets` はこれらを落としていましたが、**その経路の実際の出力はディレクトリ単位の `Recurse` なので、もともと出力には含まれていました。** 提示内容が実物へ近づく方向の変化で、出力される `.unitypackage` の中身は変わりません。

### シリアライズ形式

- `ProjectSettings/Packages/symphonyframework/AssetStoreToolsPackagerData.asset` へ `_pipelines` フィールドが増えます。**既存ファイルには無いフィールドなので、読み込み時は空配列になります。** 破壊はありません。
- `PackagerConfig.json`、`PackageVersions.json`、`ExportedVersion.json`、`PackageManifest.json` の形式は変えません。
- パイプラインアセットは新規のため既存データはありません。

> **検証済み**: `ScriptableSingleton` の保存経路（`InternalEditorUtility.SaveToSerializedFileAndForget`）が `UnityEngine.Object` 参照を GUID 付きで書けるかを、実際に Editor 上で確認しました。`Material` を `Temp/refcheck.asset` へ保存すると `m_Shader: {fileID: 4800000, guid: 933532a4..., type: 3}` と外部参照が書かれ、`LoadSerializedFileAndForget` で読み直しても参照が解決されました。**`AssetStoreToolsPackagerData` が `AssetStoreToolsPackagePipeline` の配列を直接保持できます。** GUID 文字列で持ち回る回避策は不要です。

### 利用側の移行

コード変更は不要です。ウィンドウを使っている場合だけ、**初回に Project Settings でパイプラインを 1 つ作る**必要があります。Project Settings に `Create Default Pipeline` ボタンを置き、1 クリックで既定テンプレート（Singles → Used Dependencies → Create Zip）のアセットを `Assets/Editor/SymphonyFrameWork/Configs/` へ生成します。

**自動生成はしません。** `Assets/` 配下へアセットを勝手に作ると、利用側のリポジトリへ意図しないファイルが増えます。既存の Runtime Config が自動生成されるのは Framework の動作に必須だからで、パイプラインは「使う人が構成するもの」です。

## Round 分割

**2 つの Round に分けます。** 1 Round あたりの差分を自分で読める規模に保つためです。

### Round 1: パイプライン基盤（3.6.0）

**含むもの**

- `AssetStoreToolsPackageStepStrategy` と既定 Strategy 4 種
- `AssetStoreToolsPackagePipeline`（`ScriptableObject`）と `CreateTemplate`
- `AssetStoreToolsPackagePipelineRunner`
- `AssetStoreToolsPackageExportContext` の追加と `AssetStoreToolsPackageContext` の `[Obsolete]` 化
- `AssetStoreToolsPackagePlan` / `PlanEntry` の `public` 化と `FilterAssetPaths` 追加
- `AssetStoreToolsPackager.Export(string[], AssetStoreToolsPackagePipeline)` の追加
- **既存の出力処理をランナー経由へ置き換える。** 旧 `Export(string[], PackageModeEnum, bool, bool)` はフラグからパイプラインを組み立てて委譲する
- **確認ウィンドウの表示をパイプライン名と手順一覧へ差し替える**（当初 Round 2 の予定だったが、`AssetStoreToolsPackagePlan` から `Mode` / `CreateZip` / `UsedDependencies` を削除する時点で表示元が無くなるため、Round 1 で行う）
- テスト

**含まないもの**

- `AssetStoreToolsPackagerData` のパイプライン配列
- Project Settings と Packager ウィンドウ（Export タブ）の UI 変更
- 旧 API の `[Obsolete]` 化

**この Round 単独で成立する理由**: ウィンドウの見た目と操作は 3.5.0 と同一で、内部の実行経路だけがパイプラインへ移ります。公開APIは追加のみです。

### Round 2: 設定とUI（3.7.0）

**含むもの**

- `AssetStoreToolsPackagerData` へ `_pipelines` を追加
- Project Settings での配列編集と `Create Default Pipeline` ボタン
- Packager ウィンドウの `Export Mode` をパイプラインのポップアップへ差し替え、`Create ZIP File` / `Used Dependencies` のトグルを削除
- `Export(string[], PackageModeEnum, bool, bool)` と `PackageModeEnum` の `[Obsolete]` 化
- `Documentation~/EditorTools.md` と `Documentation~/Deprecations.md` の更新

**依存順**: Round 1 が完了しコミットされてから着手します。

**実装時の追加判断**:

- **`internal CreatePlan(string[], PackageModeEnum, bool, bool)` は削除しました。** ウィンドウがパイプラインへ移った時点で唯一の呼び出し元が非推奨の公開 `Export` だけになったため、そちらへ直接インライン展開しています。`internal` なのでバージョニング上の制約はありません。
- **Export タブへ切り替えたときにパイプラインの一覧を読み直します。** Project Settings の配列は、Packager ウィンドウを開いたまま変更され得るためです。Import タブが既に同じ形（タブ入場時に読み直す）だったため、それに揃えています。実測でも、`OnEnable` と `Refresh` ボタンだけでは配列の変更が反映されないことを確認しました。

## テストの置き場と種別

EditMode テストを `Assets/SymphonyFrameWork/Tests/Editor/AssetStoreToolsPackagePipelineTests.cs` へ置きます。メソッド名は既存 `AssetStoreToolsPackagerConfigTests` に合わせて英語の `対象_条件_期待` 形式です。

**Editor ウィンドウの GUI 操作は自動検証できません**（`EditorWindow.SendEvent` も `uloop` も EditorWindow のコントロールを叩けないことが確認済み）。そのため、**ロジックを Unity API へ触れない範囲へ切り出し、そこをテスト対象にします。**

| テスト | 何を検証するか | どう書くか | Round |
| --- | --- | --- | --- |
| `FilterAssetPaths_Predicate_KeepsOnlyMatching` | 述語に合わないパスが出力対象から外れる | `internal` コンストラクタで `PlanEntry` を 3 パスで作り、`FilterAssetPaths(p => p.EndsWith(".cs"))` 後の `AssetPaths` を `CollectionAssert.AreEqual` で比較 | 1 |
| `FilterAssetPaths_CannotAddPaths` | 絞り込みでパスが増えない | 全件 `true` を返す述語を 2 回適用し、件数が初期値と等しいことを確認 | 1 |
| `FilterAssetPaths_SetsIsFiltered` | 絞り込み後に `IsFiltered` が立つ | 適用前が `false`、適用後が `true` であることを 2 回の `Assert.That` で確認 | 1 |
| `Plan_UsedDependenciesStrategy_KeepsForceIncludedExtension` | 使用中でなくても強制包含拡張子は残る | 使用中パス集合を空にした計画へ `AssetStoreToolsUsedDependenciesStrategy` の絞り込み判定（`internal static` に切り出す）を適用し、`.asmdef` が残ることを確認 | 1 |
| `CreateTemplate_ContainsSinglesUsedDependenciesAndZipInOrder` | テンプレートの中身と順序 | `AssetStoreToolsPackagePipeline.CreateTemplate()` の `Steps` の型を順に `Assert.That(..., Is.InstanceOf<T>())` で確認。件数も確認 | 1 |
| `CreateTemplate_DoesNotContainCombineStrategy` | **Combine がテンプレートに入らない** | `Steps.Any(s => s is AssetStoreToolsCombinePackageStrategy)` が `false` | 1 |
| `Run_NullStep_IsSkipped` | `<null>` 要素で落ちない | `null` を含む `Steps` を持つパイプラインでランナーの `Plan` 段階を呼び、例外が出ないことを `Assert.DoesNotThrow` で確認 | 1 |
| `Run_StepThrows_ContinuesToNextStep` | 1 つの失敗が全体を止めない | 例外を投げるテスト用 Strategy と、実行フラグを立てるテスト用 Strategy を並べ、後者が実行されることを確認。`LogAssert.ignoreFailingMessages` で `LogError` を許容する | 1 |

**`AssetStoreToolsPackagePlan` の `internal` コンストラクタは `InternalsVisibleTo("SymphonyFrameWork.Tests.Editor")`（`Editor/AssemblyInfo.cs` に記載済み）でテストから触れます。** 確認済み。

Round 2 で追加する UI は自動検証できないため、テストを追加しません。「動作確認手順」の人手項目で確認します。

### 実装時に追加した検証の足場

`AssetStoreToolsPackagePipelineRunner` の 3 メソッドを `internal` として切り出しています。**そうしないと「null 手順のスキップ」と「例外が出ても後続へ進む」を、`AssetDatabase` とファイル出力を伴わずに検証できないためです。**

| メソッド | 検証できること |
| --- | --- |
| `SelectValidSteps` | `<null>` 要素の除去 |
| `RunPlanSteps` | Plan 段階の順次実行、例外の隔離 |
| `RunExecuteSteps` | Execute 段階の順次実行、例外の隔離（Round 1 ではテスト対象外。ファイル出力を伴うため） |

`RunPlanSteps_StepThrows_ContinuesToNextStep` では `LogAssert.ignoreFailingMessages` ではなく `LogAssert.Expect(LogType.Error, Regex)` を使います。`ignoreFailingMessages` はログを無視するだけで消費せず、遅れて届いたログが後続テストへ漏れるためです（`references/review.md`）。

## 動作確認手順

### 自動で確認する項目

1. `uloop-compile` が**エラー 0・警告 0**。旧 API を内部から呼んでいないことの確認も兼ねます（`[Obsolete]` 呼び出しが残っていれば警告として出ます）
2. `uloop-run-tests` の EditMode が全数成功

### 人が操作して確認する項目（Round 1）

1. `Tools > SymphonyFrameWork > ExportAssetStoreToolsFolder` でウィンドウを開く
2. `Export Mode = Singles`、`Used Dependencies` オン、`Create ZIP File` オンで出力する
3. 期待する結果:
   - `ExportedPackages/Export_AssetStoreToolsPackage_<日時>/` に、選択したディレクトリ数と同じ `.unitypackage`
   - 同フォルダに `PackageManifest.json`
   - `ExportedPackages/Export_AssetStoreToolsPackage_<日時>.zip`
   - 各対象ディレクトリ直下に `ExportedVersion.json`
   - Console に `[AssetStoreToolsPackager] パッケージを出力しました` の 1 行。**エラーと警告が出ないこと**
4. **3.5.0 の出力結果と比較して、生成物の一覧が一致すること。** Round 1 は内部経路の置き換えなので、出力が変われば回帰です

### 人が操作して確認する項目（Round 2）

1. `Project Settings > SymphonyFrameWork > Asset Store Tools Packager` を開き、`Create Default Pipeline` を押す
2. `Assets/Editor/SymphonyFrameWork/Configs/AssetStoreToolsPackagePipeline.asset` が生成され、Inspector で `Steps` に 3 件（Single / UsedDependencies / CreateZip）が順に並ぶこと
3. `Steps` の要素のポップアップ（SubclassSelector）を開き、**`AssetStoreToolsCombinePackageStrategy` が候補に出ること**。テンプレートには入っていないこと
4. Project Settings の `Pipelines` 配列へアセットが追加されていること。Unity を再起動しても参照が保持されていること（`ProjectSettings/Packages/symphonyframework/AssetStoreToolsPackagerData.asset` に `guid` 付きの参照が書かれる）
5. Packager ウィンドウの `Export Mode` がポップアップになり、**アセット名がそのまま選択肢として並ぶこと**
6. Export を押すと確認ウィンドウにパイプライン名と手順一覧が表示され、実行結果が Round 1 と同じであること
7. パイプラインを複製して `AssetStoreToolsCombinePackageStrategy` だけを持つものを作り、選択して出力する。**統合パッケージが 1 つ出力され、`PackageManifest.json` が作られず、差分インポートの対象外である旨の警告が出ること**
8. Project Settings の `Pipelines` を空にすると、ウィンドウに案内の `HelpBox` が出て Export ボタンが押せないこと

Play Mode を使う機能ではないため、Play Mode の開始・終了によるゴースト参照の確認は該当しません。**ただし Domain Reload はまたぎます。** 確認ウィンドウは既に「ドメインリロードで計画を失った場合は閉じる」実装になっており、パイプライン参照も `ScriptableObject` なのでリロードを越えます。手順 6 の途中でスクリプトを再コンパイルさせ、確認ウィンドウが閉じるだけで例外が出ないことを確認します。

## バージョン判断

| Round | 版 | 理由 |
| --- | --- | --- |
| 1 | **3.6.0**（マイナー） | 公開API（Strategy 契約、Pipeline、Plan の公開化、新 `Export` 多重定義）の**後方互換な追加**。既存APIの削除・シグネチャ変更なし |
| 2 | **3.7.0**（マイナー） | `[Obsolete]` の付与は破壊的変更ではない（警告のみ）。設定ファイルへのフィールド追加も既存データを壊さない。UI の変更は公開APIではない |

**メジャーにしない根拠**: `DesignPhilosophy.md` の「バージョニング」で、破壊的変更はメジャー、後方互換な公開API追加はマイナー、と定めています。この Issue の範囲では `public` メンバーの削除もシグネチャ変更も行いません。`PackageModeEnum` と `AssetStoreToolsPackageContext` の**削除は次のメジャー更新**へ回し、`Deprecations.md` へ登録します。

## この Round で触るバージョン関連ファイル

**同じファイルを両 Round が触るため、どの Round がどこを触るかを明示します。**

| ファイル | Round 1 | Round 2 |
| --- | --- | --- |
| `Assets/SymphonyFrameWork/package.json` | `version` を `3.6.0` へ | `version` を `3.7.0` へ |
| `Assets/SymphonyFrameWork/CHANGELOG.md` | `## [3.6.0]` の節を**新規追加**（`### Add`、`### Deprecated`） | `## [3.7.0]` の節を**新規追加**（`### Add`、`### Change`、`### Deprecated`）。3.6.0 の節は編集しない |
| `Assets/SymphonyFrameWork/Documentation~/Deprecations.md` | `AssetStoreToolsPackageContext` の行を追加 | `PackageModeEnum` の行の**移行先を更新**（`Singles` → パイプライン）し、`Combine` の移行先を `AssetStoreToolsCombinePackageStrategy` へ。`Export(string[], PackageModeEnum, bool, bool)` の行を追加。「Combine の削除手順」表を新構成へ更新 | 
| `Assets/SymphonyFrameWork/Documentation~/EditorTools.md` | 「出力手順のパイプライン」の節を**新規追加**（`Assets > Create` のメニューが増えるため。入口一覧へも1行追加）。「Export タブ」の節は編集しない | 「Asset Store Tools Packager 設定」と「Export タブ」の節を更新。パイプラインの選択方法と配列の設定を追記 |
| `Assets/SymphonyFrameWork/README.md` | 触らない（Packager は README の対象外） | 同左 |
| `Assets/SymphonyFrameWork/AGENTS.md` | 触らない | 導線が変わらないため触らない |

## 懸念と判断の記録

### `ScriptableObject` の Config を `public` にすること

`DesignPhilosophy.md` は「Composition Root、Config、Domain Entity ... は `internal` にする」と定めています。`AssetStoreToolsPackagePipeline` はこれに反して `public` です。

**判断**: `public` にします。この規約が対象にしているのは Runtime の設定アセット（`SceneLoadConfig`、`AudioConfig`、`SaveDataConfig`）で、**利用側が中身を触らずフレームワークが読むもの**です。パイプラインは逆に、**利用側が自分で作り、自分で構成し、自分の Strategy を差し込むための拡張点**です。`internal` にすると `[CreateAssetMenu]` から作れず、Issue の要件（外部からの拡張）が満たせません。

同じ理由で、`SaveDataConfig` の `SaveDataLoaderStrategy` が `public abstract` で公開されている前例に揃います。

### `Combine` を残すことと 3.4.0 の判断の関係

3.4.0 では「差分インポートの単位にならない」ことを理由に `Combine` を非推奨にしました。**この技術的な判断は変わりません。** 変えるのは「だから機能ごと消す」という結論の方で、Issue #124 のコメントにあるとおり、統合パッケージを使いたい場面が実在するためです。

パイプライン化によって、**「既定では使われないが、選べば使える」という中間の位置**が表現できるようになりました。enum のフラグには無かった選択肢です。`Deprecations.md` の `Combine` 行は削除せず、移行先を `AssetStoreToolsCombinePackageStrategy` へ書き換えます。**「非推奨の enum 値」から「テンプレート外の Strategy」への移行であり、機能の削除ではないことを文書に残します。**
