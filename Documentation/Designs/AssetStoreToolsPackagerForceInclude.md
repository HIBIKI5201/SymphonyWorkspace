# Asset Store Tools Packager の設定ファイル化と出力確認ウィンドウ

Issue: [#133](https://github.com/HIBIKI5201/SymphonyFramework/issues/133)

## 目的

`AssetStoreToolsPackager` の「Used Dependencies」出力で、パッケージへ入るべきアセットが落ちる。

原因は `GetUsedAssetsInDirectory` の1箇所である。

```csharp
return allFiles
    .Where(files => usedAssetPaths.Contains(files)
                    || files.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
    .ToArray();
```

`usedAssetPaths` は `AssetDatabase.GetDependencies` で組み立てているが、**`.asmdef` / `.asmref` は Unity の依存関係グラフに載らない**。強制包含は `.cs` のハードコード1種類しかないため、どちらの条件にも一致せず落ちる。結果、消費者プロジェクトでは `.cs` だけが `Assembly-CSharp` へ取り込まれ、CRI SDK の asmdef 参照が解決できずコンパイルエラーになる。ネイティブプラグイン（`.dll` / `.aar` / `.bundle` 等）も同じ理由で落ちる。

現状の回避策（「Used Dependencies」をオフにして丸ごと出力する）はパッケージサイズが増える。本設計はこれを不要にする。

あわせて、パッケージ化の設定が2か所へ散っている状態を整理する。

| 現状 | 置き場 | 形式 |
| --- | --- | --- |
| 除外フォルダ名 | `Assets/AssetStoreTools/ignore.txt` | 1行1件のプレーンテキスト |
| 対象パス・出力先パス | `ProjectSettings/Packages/symphonyframework/AssetStoreToolsPackagerData.asset` | ScriptableSingleton |

ここへ強制包含拡張子が加わるため、**「何を詰めるか」の設定を `PackagerConfig.json` へ集約する**。消費者プロジェクトが自分のリポジトリで設定を管理・変更できる形にする。

## Round 分割

2つの Round に分ける。Round 1 だけで Issue のコンパイルエラーは解消し、単独でリリースできる。

| Round | 内容 | バージョン |
| --- | --- | --- |
| **Round 1** | `PackagerConfig.json` の導入（除外フォルダ＋強制包含拡張子）、`ignore.txt` からの移行、収集ロジックの修正 | 3.1.0 |
| **Round 2** | 出力計画（Plan）への分離と、エクスポート前の確認ウィンドウ（階層表示） | 3.2.0 |

Round 1 が「何が入るか」を正しくし、Round 2 が「何が入るかを事前に見せる」。順序は入れ替えられない（確認ウィンドウは正しい収集結果を前提にする）。Round 1 のコミットが終わってから Round 2 へ進む。

以降、各節は Round ごとに分けて書く。

## 前提の確認

着手時にコードで確認した事実を記録する。

| 前提 | 確認結果 |
| --- | --- |
| `AssetStoreToolsPackagerData` と `AssetStoreToolsPackagerProvider` が同一アセンブリか | **同一。** `Editor/` 配下の asmdef は `Editor/SymphonyFrameWork.Editor.asmdef` の1つだけ |
| Editor アセンブリから Newtonsoft.Json を使えるか | **使える。** `Editor/Debug/SymphonyMcpTools.cs` が `using Newtonsoft.Json;` を `#if` 無しで使っている。`package.json` の hard dependency でもある |
| `AssetDatabase.FindAssets("", new[]{ dir })` の利用実績 | **あり。** 同ファイルの `GetProjectUsedDependencies` が同じ形で使っている |
| `SymphonyFrameWork.Editor` の `internal` がテストから見えるか | **見えない。** `InternalsVisibleTo` を持つのは `Core/AssemblyInfo.cs` と `Runtime/AssemblyInfo.cs` だけで、Editor 側に `AssemblyInfo.cs` は存在しない |
| `AssetStoreToolsPackageContext` を確認ウィンドウが保持できるか | **できない。** `readonly ref struct` のためクラスのフィールドへ置けない。Round 2 で渡す計画データは別の参照型にする |
| `ignore.txt` のパスの組み立て | **既存バグあり。** `EditorSymphonyConstant.ASSET_STORE_TOOLS_IGNORE_FILE` は `const` の既定パスから組まれており、`AssetStoreToolsPackagerData.AssetStoreToolsPath` を変更しても追従しない。ディレクトリ列挙側は設定値を使っているため、パスを変えると除外設定だけ読まれなくなる |
| `ignore.txt` の参照箇所 | `EditorSymphonyConstant.cs` の定数定義1件と `AssetStoreToolsPackager.GetIgnoredNames` の3行のみ。他のコード・文書からの参照は無い |
| 利用者向け文書に Packager の記述があるか | **無い。** `README.md` の「主な設定場所」、`Documentation~/AgentUsage.md`、`AGENTS.md` のいずれにも Asset Store Tools Packager は登場しない。更新対象は `CHANGELOG.md` のみ |

---

# Round 1: PackagerConfig.json

## 設定の置き場の分担

2か所に残すが、**分担の基準を明確にする**。

| 置き場 | 何を持つか | 理由 |
| --- | --- | --- |
| `ProjectSettings/Packages/symphonyframework/AssetStoreToolsPackagerData.asset` | `AssetStoreToolsPath`、`ExportedPackagesPath` | **どこにあるか**。`AssetStoreToolsPath` を設定ファイル側へ置くことは原理的にできない（設定ファイルの場所がそのパス配下にあるため）。出力先パスもプロジェクトの構造に属するため同居させる |
| `<AssetStoreToolsPath>/PackagerConfig.json` | `ignoredDirectories`、`forceIncludeExtensions` | **何を詰めるか**。パッケージ化する内容そのものに属する設定であり、対象フォルダと一緒に版管理されるべきもの |

`PackagerConfig.json` は `Assets/` 配下にあるため消費者プロジェクトのリポジトリへコミットされる。手で編集でき、差分も読める。

**ファイルパスは `AssetStoreToolsPackagerData.AssetStoreToolsPath` から組み立てる。** 定数は既定のファイル名だけを持つ。これで前提確認に挙げた既存バグ（パス設定に追従しない）も直る。

## ファイル形式

**JSON を採る。YAML は採らない。**

- Unity には任意のオブジェクトを扱う YAML の API が無く、外部パッケージの追加が必要になる。設定ファイル1つのために依存を増やす価値は無い
- Newtonsoft.Json は既に `package.json` の依存であり、Editor アセンブリでも使用実績がある。追加依存はゼロ
- Newtonsoft は読み込み時に `//` 形式のコメントを許容するため、手で注釈を書いても壊れない。ただし**Project Settings から保存し直すとコメントは失われる**。この点は既定ファイルのフィールド名を自己説明的にすることで補う

`JsonUtility` ではなく Newtonsoft を使うのは、コメント許容に加えて、壊れた JSON に対する例外メッセージが行番号を含むためである。

### 既定の内容

```json
{
  "ignoredDirectories": [],
  "forceIncludeExtensions": [
    ".cs",
    ".asmdef",
    ".asmref",
    ".dll",
    ".so",
    ".a",
    ".dylib",
    ".aar",
    ".bundle",
    ".framework",
    ".jslib"
  ]
}
```

`.cs` は既存のハードコードの引き継ぎ。`.asmdef` / `.asmref` が本 Issue の直接原因。残りはネイティブプラグインで、CRI SDK は asmdef だけでは解消しない可能性が高いため既定へ入れる。

### 正規化

読み込み時に正規化する。

- `forceIncludeExtensions`: 前後の空白を落とす。先頭に `.` が無ければ補う。空文字は捨てる。比較は既存コードと同じく `StringComparison.OrdinalIgnoreCase`
- `ignoredDirectories`: 前後の空白を落とす。空文字は捨てる。比較は `StringComparer.OrdinalIgnoreCase` にする（現行は既定の序数比較で大文字小文字を区別するが、Windows のファイルシステムは区別しないため、`Demigiant` と `demigiant` が食い違う。**現行からの挙動変更である**）
- どちらも `null` の場合は空一覧として扱う（フィールドを消して保存された JSON への耐性）

`forceIncludeExtensions` を空にした場合は強制包含が無効になる（依存関係のみで絞る、現状の `.cs` すら入らない状態）。これは設定として認める。

## ignore.txt からの移行

`PackagerConfig.json` が存在しないときの読み込みで、次を行う。

1. 同じフォルダに `ignore.txt` があれば、コメント（`#` 始まり）と空行を除いた各行を `ignoredDirectories` として取り込む
2. `forceIncludeExtensions` は既定値を使う
3. `PackagerConfig.json` を書き出し、`AssetDatabase.Refresh()` する
4. `Debug.Log` で「`ignore.txt` から移行した。以降 `ignore.txt` は読まれないため削除してよい」と通知する

`ignore.txt` を無い場合は既定の `PackagerConfig.json` を書き出す（現行の `ignore.txt` 自動生成と同じ挙動）。

**`ignore.txt` を自動削除しない。** 利用者のファイルであり、移行が意図どおりか確認する余地を残す。

`EditorSymphonyConstant.ASSET_STORE_TOOLS_IGNORE_FILE` は `public const` のため削除すると破壊的変更になる。`[Obsolete("PackagerConfig.jsonへ移行しました。", error: false)]` を付けて残し、次のメジャー更新で削除する。

## 公開API

**追加する公開APIは無い。** 設定は JSON ファイルへ移るため、`AssetStoreToolsPackagerData` は変更しない。

追加・変更する型はすべて `internal`。`SymphonyFrameWork.Editor` の外から使う必要が無い。

```csharp
namespace SymphonyFrameWork.Editor
{
    /// <summary> AssetStoreToolsフォルダのパッケージ化設定。 </summary>
    internal sealed class AssetStoreToolsPackagerConfig
    {
        /// <summary> パッケージ対象から除外するフォルダ名。 </summary>
        public List<string> IgnoredDirectories;

        /// <summary> 依存関係に含まれなくても強制的に含める拡張子。 </summary>
        public List<string> ForceIncludeExtensions;

        /// <summary> 既定値の設定を生成する。 </summary>
        internal static AssetStoreToolsPackagerConfig CreateDefault();

        /// <summary> 空白や欠落を取り除いた正規化済みの設定を返す。 </summary>
        internal AssetStoreToolsPackagerConfig Normalize();
    }

    /// <summary> PackagerConfig.jsonの読み書きとignore.txtからの移行を担う。 </summary>
    internal static class AssetStoreToolsPackagerConfigStore
    {
        /// <summary> 現在の対象パス設定から設定ファイルのパスを組み立てる。 </summary>
        internal static string GetConfigFilePath();

        /// <summary> 設定を読み込む。存在しない場合は生成し、ignore.txtがあれば移行する。 </summary>
        /// <returns> 読み込みに失敗した場合はnull。 </returns>
        internal static AssetStoreToolsPackagerConfig Load();

        /// <summary> 設定をJSONへ保存する。 </summary>
        internal static void Save(AssetStoreToolsPackagerConfig config);
    }
}
```

`EditorSymphonyConstant` へ既定ファイル名の定数を1件追加する。`public const` だが、既存の `ASSET_STORE_TOOLS_IGNORE_FILE` と同じ位置づけであり、置き換えである。

```csharp
/// <summary> パッケージ化設定ファイルの名前。 </summary>
public const string ASSET_STORE_TOOLS_CONFIG_FILE_NAME = "PackagerConfig.json";
```

## ファイル構成

| 区分 | パス | 名前空間 |
| --- | --- | --- |
| 変更 | `Core/Editor/EditorSymphonyConstant.cs` | `SymphonyFrameWork.Core` |
| 変更 | `Editor/Generator/AssetStoreToolsPackager/AssetStoreToolsPackager.cs` | `SymphonyFrameWork.Editor` |
| 変更 | `Editor/SettingProvider/AssetStoreToolsPackagerProvider.cs` | `SymphonyFrameWork.Editor.SettingProvider` |
| 新規 | `Editor/Generator/AssetStoreToolsPackager/AssetStoreToolsPackagerConfig.cs` | `SymphonyFrameWork.Editor` |
| 新規 | `Editor/Generator/AssetStoreToolsPackager/AssetStoreToolsPackagerConfigStore.cs` | `SymphonyFrameWork.Editor` |
| 新規 | `Editor/AssemblyInfo.cs` | （属性のみ） |
| 新規 | `Tests/Editor/AssetStoreToolsPackagerConfigTests.cs` | `SymphonyFrameWork.Tests` |

`Editor/AssemblyInfo.cs` は `[assembly: InternalsVisibleTo("SymphonyFrameWork.Tests.Editor")]` の1行のみ。`Core` / `Runtime` と同じ形で、Editor アセンブリの `internal` をテストから検証できるようにする。これが無いと本 Round のテストは書けない。

`Internal/` フォルダは使わない。`Editor/` 配下は既存も分けておらず、Editor アセンブリ自体が利用側から参照されない前提のため。

## 依存方向

Editor 層の内部で完結する。Runtime / Core への追加参照は無い（`EditorSymphonyConstant` は既に `Core/Editor/` にあり、Editor から参照済み）。

`AssetStoreToolsPackager`（収集・出力） → `AssetStoreToolsPackagerConfigStore`（IO） → `AssetStoreToolsPackagerConfig`（データ）の一方向。`AssetStoreToolsPackagerProvider` は Store を通してのみ設定へ触る。Config はファイル IO を知らない。

## 変更内容

### 1. 除外フォルダの読み込みを設定ファイルへ移す

`AssetStoreToolsPackager.GetIgnoredNames()` を削除し、`GetPackageDirectories()` は `AssetStoreToolsPackagerConfigStore.Load()` の `IgnoredDirectories` を使う。

### 2. 収集を `Directory.GetFiles` から `AssetDatabase.FindAssets` へ変える

現行は `Directory.GetFiles(dir, "*.*", SearchOption.AllDirectories)` でファイルシステムを走査している。これには2つの問題がある。

- `.bundle` / `.framework` のような**フォルダ形式のネイティブプラグイン**は、Unity が単一アセットとして扱う。`Directory.GetFiles` はフォルダ自体を列挙せず、代わりに中身のファイルを個別に返す。中身のファイルは AssetDatabase 上のアセットではないため、`ExportPackage` へ渡しても出力されない
- Unity がインポートしないファイルも拾う

`AssetDatabase.FindAssets(string.Empty, new[] { dir })` に変えると、Unity がアセットとして認識している単位で列挙されるため、フォルダ形式のプラグインもそのパス1件として返る。

```csharp
private static string[] CollectExportAssets(
    string dir,
    HashSet<string> usedAssetPaths,
    IReadOnlyList<string> forceIncludeExtensions)
{
    return AssetDatabase.FindAssets(string.Empty, new[] { dir })
        .Select(AssetDatabase.GUIDToAssetPath)
        .Where(path => !string.IsNullOrEmpty(path))
        .Distinct()
        .Where(path => IsExportTarget(path, usedAssetPaths, forceIncludeExtensions))
        .OrderBy(path => path, StringComparer.Ordinal)
        .ToArray();
}

private static bool IsExportTarget(
    string path,
    HashSet<string> usedAssetPaths,
    IReadOnlyList<string> forceIncludeExtensions)
{
    bool isForceIncluded = HasForceIncludeExtension(path, forceIncludeExtensions);

    // 通常のフォルダは出力対象にしない。.bundle等のフォルダ形式アセットは拡張子一致で残す。
    if (!isForceIncluded && AssetDatabase.IsValidFolder(path))
    {
        return false;
    }

    return isForceIncluded || usedAssetPaths.Contains(path);
}
```

`IsValidFolder` の判定を「強制包含に一致しないときだけ」効かせているのは、`.bundle` に対して `IsValidFolder` が何を返すかへ結果を依存させないためである。`false`（プラグインとして単一アセット）でも `true`（ただのフォルダ）でも `.bundle` は残る。後者の場合は中身のアセットも個別に列挙されるが、いずれも出力されるべきものなので実害は無い。

`.meta` は `FindAssets` が返さないため、除外処理は不要になる。

### 3. 設定 UI

`AssetStoreToolsPackagerProvider` へ設定ファイルの編集欄を追加する。現行のパス2件はそのまま残す。

- `activateHandler` で `AssetStoreToolsPackagerConfigStore.Load()` を呼び、結果をフィールドへ保持する
- `Ignored Directories` と `Force Include Extensions` を、要素ごとの `TextField` ＋ 削除ボタン、末尾に追加ボタンで描く
- **保存は明示的な `Save` ボタンで行う。** 編集はメモリ上の複製に対して行い、`Save` でファイルへ書く。`Reload` でファイルから読み直す。未保存の変更がある間は `HelpBox` で知らせる
- 設定ファイルのパスを読み取り専用で表示する
- **`Asset Store Tools Path` の変更では設定を読み直さない。** `TextField` は1キーストロークごとに値が変わるため、変更のたびに `Load()` を呼ぶと入力途中のパスごとに設定ファイルを生成してしまう。代わりに、読み込み済みのパスと現在のパスが食い違っている間は `HelpBox` で警告し、`Save` を無効化して `Reload` を促す

`SerializedObject` は使わない。編集対象が `UnityEngine.Object` ではなくプレーンな JSON だからである。ファイルへ書くタイミングを1キーストロークごとにしないため、自動保存にはしない。

## エラー処理

例外は追加しない。既存の方針（`Debug.LogWarning` / `Debug.LogError` で通知して続行）を維持する。

| 状況 | 扱い |
| --- | --- |
| 設定ファイルが存在しない | `ignore.txt` があれば移行、無ければ既定値で新規作成。`Debug.Log` で通知 |
| 設定ファイルが壊れている（JSON パース失敗） | `Debug.LogError` にファイルパスと例外メッセージを出し、`Load` は `null` を返す。**既定値へフォールバックしない**（除外設定が無視されて意図しないフォルダが出力されるため）。`GetPackageDirectories` は空一覧を返し、`Export` は何もせず戻る |
| 対象フォルダ自体が無い | 現行どおり `GetPackageDirectories` が空一覧を返す |
| 対象ディレクトリが0件 | 現行どおり `Debug.LogWarning` して何もしない |
| あるディレクトリのアセットが0件 | 現行どおり `Debug.LogWarning` してそのディレクトリを飛ばす |
| `ExportPackage` の失敗 | 現行どおり `try`/`catch` で `Debug.LogError` |
| 拡張子一覧が空 | 例外にしない。依存関係のみで絞る |
| 一覧の要素が空文字や空白のみ | 正規化時に捨てる |

## 影響範囲

- **Runtime の公開APIとシリアライズ形式への影響は無い。** 変更は Editor アセンブリに閉じる
- **`Assets/AssetStoreTools/ignore.txt` は読まれなくなる。** 初回起動時に内容が `PackagerConfig.json` へ自動移行され、ログで通知される。ファイル自体は残るため、利用者が確認してから削除できる
- `EditorSymphonyConstant.ASSET_STORE_TOOLS_IGNORE_FILE` が `[Obsolete]` になる。参照している利用側コードがあれば警告が出るが、コンパイルは通る
- **除外フォルダ名の比較が大文字小文字を区別しなくなる。** 現行と挙動が変わるが、Windows のファイルシステムに合わせた修正である
- **「Used Dependencies」出力の内容が変わる。** これが本 Issue の修正そのもので、従来落ちていた `.asmdef` / `.asmref` / ネイティブプラグインが入る。パッケージサイズは増える
- 収集経路が `Directory.GetFiles` から `AssetDatabase.FindAssets` へ変わるため、**Unity がインポートしないファイルは出力対象から外れる。** これらは元々 `ExportPackage` で出力できなかったため、実質的な欠落は生じない
- `AssetStoreToolsPath` を変更したときに設定ファイルが追従するようになる（既存バグの修正）。変更後のフォルダに設定ファイルが無ければ新規作成される

## テストの置き場と種別

`Tests/Editor/AssetStoreToolsPackagerConfigTests.cs`（EditMode）へ置く。`Editor/AssemblyInfo.cs` の `InternalsVisibleTo` により `internal` な型を検証できる。

`AssetDatabase` とファイル IO に依存する `CollectExportAssets` と `AssetStoreToolsPackagerConfigStore` は、検証用アセットの生成・削除が必要で EditMode テストとしては壊れやすいため**自動テストを書かない**。手動の動作確認手順（後述）で担保する。テスト対象は Unity API とファイル IO へ触れない純粋なロジックに限定する。

テストのメソッド名は既存のテスト（`PauseInfoTests` など）に合わせて英語の `対象_条件_期待` 形式にする。

| テスト | 何を検証するか | どう書くか |
| --- | --- | --- |
| `Normalize_ExtensionWithoutDot_AddsDot` | 正規化 | `ForceIncludeExtensions = { "cs" }` の設定を `Normalize()` し、結果が `{ ".cs" }` であることを `CollectionAssert.AreEqual` |
| `Normalize_ExtensionWithSpaces_TrimsSpaces` | 正規化 | `{ " .dll " }` を `Normalize()` し `{ ".dll" }` |
| `Normalize_EmptyElements_AreRemoved` | 正規化 | `{ ".cs", "", "  " }` を `Normalize()` し `{ ".cs" }` |
| `Normalize_NullLists_BecomeEmptyLists` | 欠落耐性 | 両フィールドが `null` の設定を `Normalize()` し、どちらも `Is.Empty` |
| `Normalize_IgnoredDirectories_TrimsAndRemovesEmpty` | 正規化 | `IgnoredDirectories = { " Demigiant ", "" }` を `Normalize()` し `{ "Demigiant" }` |
| `Normalize_DoesNotMutateSource` | 非破壊 | `Normalize()` の呼び出し後、元のインスタンスの一覧が変わっていないことを `CollectionAssert.AreEqual` |
| `CreateDefault_HasElevenExtensions` | 既定値 | `CreateDefault()` の `ForceIncludeExtensions` が `.cs` / `.asmdef` / `.asmref` を含み、`Count` が 11 |
| `HasForceIncludeExtension_MatchingExtension_IsTrue` | 一致判定 | `AssetStoreToolsPackager.HasForceIncludeExtension("Assets/A/B.asmdef", new[]{ ".asmdef" })` が `true` |
| `HasForceIncludeExtension_DifferentCase_IsTrue` | `OrdinalIgnoreCase` | 同メソッドへ `"B.ASMDEF"` と `new[]{ ".asmdef" }` を渡して `true` |
| `HasForceIncludeExtension_OtherExtension_IsFalse` | 非一致 | 同メソッドへ `"Icon.png"` と `new[]{ ".asmdef" }` を渡して `false` |
| `HasForceIncludeExtension_NoExtension_IsFalse` | 拡張子なし | 同メソッドへ拡張子を持たないパスを渡して `false` |
| `HasForceIncludeExtension_EmptyList_IsFalse` | 無効化 | 同メソッドへ空配列を渡して `false` |

テストのために `HasForceIncludeExtension` は `internal static` にする（`private` のままでは検証できない）。`Normalize` は元のインスタンスを変更せず新しい設定を返すため、事後状態の織り込みは不要である。

## 動作確認手順

Play Mode は不要。Editor 上の操作で確認する。

1. `uloop compile` で Error 0・Warning 0
2. EditMode テストが全数成功（既存分＋本 Round の12件）
3. `Assets/AssetStoreTools/PackagerConfig.json` を削除した状態で `Tools > SymphonyFrameWork > ExportAssetStoreToolsFolder` を開く。`ignore.txt` の内容を引き継いだ `PackagerConfig.json` が生成され、移行のログが出ること。`ignore.txt` が残っていること
4. `PackagerConfig.json` の `ignoredDirectories` へ `Demigiant` を書いて保存し、Packager ウィンドウの `Refresh` を押す。`Demigiant (Ignored)` になること。`demigiant` と小文字で書いても同じ結果になること
5. `Project Settings > SymphonyFrameWork > Asset Store Tools Packager` を開き、両方の一覧が表示されること。要素を追加して `Save` を押すと JSON へ反映され、`Reload` でファイルの内容へ戻ること
6. `PackagerConfig.json` を壊れた JSON にして Packager ウィンドウを開く。`Debug.LogError` が出て、ディレクトリ一覧が空になり、出力が行われないこと
7. `AssetStoreToolsPath` を別のフォルダへ変更し、そのフォルダ配下に `PackagerConfig.json` が新規作成されること（既存バグの修正確認）
8. CRI を含むディレクトリを選択、**Used Dependencies をオン**にして Export する。出力された `.unitypackage` に `.asmdef` とネイティブプラグインが含まれていること
9. 出力した `.unitypackage` を空のプロジェクトへ導入し、**CRI 由来のコンパイルエラーが出ないこと。** これが本 Issue の受け入れ条件
10. Used Dependencies を**オフ**にして Export し、出力結果が修正前と同じ（丸ごと出力）であること
11. `forceIncludeExtensions` を空にして Used Dependencies オンで Export し、`.cs` すら含まれないこと（設定が効いていることの確認）

手順9は消費者プロジェクトが必要になる。ここまで実施できない場合は、手順8の出力内容をもって代替とし、その旨を報告する。

## バージョン判断

**マイナー（3.1.0）。** 公開APIの削除・シグネチャ変更は無い。`EditorSymphonyConstant` へ定数を1件追加し、`ASSET_STORE_TOOLS_IGNORE_FILE` を `[Obsolete]` にする。設定の保存形式が変わるが、`ignore.txt` から自動移行するため利用者の作業は不要である。

`ignore.txt` の廃止は破壊的に見えるが、移行が自動で行われ、旧ファイルも残るため、メジャーとしない。次のメジャー更新で `ASSET_STORE_TOOLS_IGNORE_FILE` と移行処理を削除する。

## この Round で触るバージョン関連ファイル

| ファイル | 変更 |
| --- | --- |
| `Assets/SymphonyFrameWork/package.json` | `3.0.0` → `3.1.0` |
| `Assets/SymphonyFrameWork/CHANGELOG.md` | `[3.1.0]` の節を追加。Changed（設定の `PackagerConfig.json` 化と移行）、Fixed（取りこぼしの修正、パス設定に追従しないバグ）、Deprecated（`ASSET_STORE_TOOLS_IGNORE_FILE`） |
| `Assets/SymphonyFrameWork/README.md` | 「現在のバージョン」を `3.1.0` へ |

`README.md` の「主な設定場所」、`Documentation~/`、`AGENTS.md` は更新しない。Asset Store Tools Packager は開発者側のツールで、いずれの文書にも記載が無く、利用者の使い方に影響しないため。

---

# Round 2: 出力確認ウィンドウ

Round 1 のコミットが終わってから着手する。

## 目的

エクスポート実行前に、実際にパッケージへ入るアセットの一覧を階層表示で確認できるようにする。Issue #133 のような取りこぼしを、出力前に気づけるようにする。

## 公開API

**追加する公開APIは無い。** すべて `internal`。

```csharp
internal sealed class AssetStoreToolsPackagePlan          // 出力計画
internal sealed class AssetStoreToolsPackagePlanEntry     // ディレクトリ1件分の出力内容
internal sealed class AssetStoreToolsPackageConfirmWindow // 確認ウィンドウ
internal sealed class AssetPathTreeNode                   // パス一覧から組む階層ツリー
```

`AssetStoreToolsPackager` の既存 `public` メソッド（`ExportAssetStoreToolsFolder`、`GetPackageDirectories`、`Export(string[], PackageModeEnum, bool, bool)`）はシグネチャを変えない。`Export` は内部で計画を組んで実行する形になるが、外から見た挙動は変わらない。

## ファイル構成

| 区分 | パス | 名前空間 |
| --- | --- | --- |
| 変更 | `Editor/Generator/AssetStoreToolsPackager/AssetStoreToolsPackager.cs` | `SymphonyFrameWork.Editor` |
| 変更 | `Editor/Generator/AssetStoreToolsPackager/AssetStoreToolsPackageWindow.cs` | `SymphonyFrameWork.Editor` |
| 新規 | `Editor/Generator/AssetStoreToolsPackager/AssetStoreToolsPackagePlan.cs` | `SymphonyFrameWork.Editor` |
| 新規 | `Editor/Generator/AssetStoreToolsPackager/AssetStoreToolsPackageConfirmWindow.cs` | `SymphonyFrameWork.Editor` |
| 新規 | `Editor/Generator/AssetStoreToolsPackager/AssetPathTreeNode.cs` | `SymphonyFrameWork.Editor` |
| 新規 | `Tests/Editor/AssetPathTreeNodeTests.cs` | `SymphonyFrameWork.Tests` |

## 依存方向

`AssetStoreToolsPackageWindow`（操作） → `AssetStoreToolsPackager`（計画の生成と実行）の一方向。確認ウィンドウは `AssetStoreToolsPackageWindow` から開き、確定時のコールバックで `AssetStoreToolsPackager.Export(plan)` を呼ぶ。**`AssetStoreToolsPackager` から確認ウィンドウを参照しない**（出力処理が UI を知らない状態を保つ）。

## 変更内容

### 1. 出力計画（Plan）を組んでから実行する

確認ウィンドウで「何が出るか」を見せるには、出力前に内容が確定している必要がある。`Export` を「計画の生成」と「計画の実行」へ分ける。

```csharp
internal sealed class AssetStoreToolsPackagePlan
{
    public PackageModeEnum Mode;
    public bool CreateZip;
    public bool UsedDependencies;
    public IReadOnlyList<AssetStoreToolsPackagePlanEntry> Entries;
}

internal sealed class AssetStoreToolsPackagePlanEntry
{
    /// <summary> 出力単位となるディレクトリのパス。 </summary>
    public string DirectoryPath;

    /// <summary> パッケージ名と表示に使うディレクトリ名。 </summary>
    public string Name;

    /// <summary> このディレクトリから出力されるアセットのパス一覧。 </summary>
    public IReadOnlyList<string> AssetPaths;
}
```

`AssetPaths` は「実際にパッケージへ入るアセット」を表す。`UsedDependencies` の値で意味は変わらず、`ExportPackage` への渡し方だけが変わる。

| `UsedDependencies` | `ExportPackage` へ渡すパス | オプション | `AssetPaths` の内容 |
| --- | --- | --- | --- |
| `true` | `AssetPaths` | `Default` | 依存関係またはホワイトリストに一致したアセット |
| `false` | `DirectoryPath` の1件 | `Recurse` | ディレクトリ配下の全アセット（表示用に列挙する） |

`false` の場合に実行時のパスを `DirectoryPath` のままにするのは、現行の挙動（丸ごと出力）を変えないためである。`AssetPaths` は確認ウィンドウの表示にのみ使う。

`AssetStoreToolsPackageContext` の生成は計画の生成時ではなく実行時に行う。出力先フォルダ名へ日時が入るため、確認ウィンドウで待っている間に日時が進んでよい。

既存の `public static void Export(string[] directories, PackageModeEnum mode, bool createZip, bool usedDependencies)` は残し、`Export(CreatePlan(...))` を呼ぶだけにする。

### 2. 確認ウィンドウ

`AssetStoreToolsPackageWindow` の Export ボタンは、直接出力せず次を行う。

1. `AssetStoreToolsPackager.CreatePlan(...)` で計画を組む
2. `AssetStoreToolsPackageConfirmWindow.Open(plan, onConfirmed)` で確認ウィンドウを開く
3. 確認ウィンドウの「Export」で `onConfirmed`（＝`AssetStoreToolsPackager.Export(plan)`）を呼び、ウィンドウを閉じる。「Cancel」は何もせず閉じる

**モーダル（`ShowModalUtility`）は使わない。** 別ウィンドウの `OnGUI` の中からモーダルを開くのは入れ子 GUI になるため、`ShowUtility` の非モーダル＋コールバックにする。

表示内容:

- ヘッダー: 出力モード、ZIP 作成の有無、Used Dependencies の有無、総アセット数
- ディレクトリごとにルートノードを持つ階層ツリー。フォルダノードは折りたたみ（既定は展開）、末尾に配下のアセット数を出す
- アセットが0件のエントリは「（対象アセットなし）」と出し、出力時に警告になることが分かるようにする
- 下部に「Export」「Cancel」

ツリーは `AssetPathTreeNode` が担う。パス一覧を `/` で分割して親子へ組み、子は名前の昇順で並べる。フォルダとファイルの区別は「子を持つか」で行う。**この型は Unity API へ触れない純粋なロジックにして、テスト対象にする。**

## エラー処理

Round 1 から変更しない。確認ウィンドウで Cancel した場合は何も出力せず、ログも出さない。

計画の生成中に例外が出た場合は `Debug.LogError` して確認ウィンドウを開かない。

## 影響範囲

- Export ボタンを押してから出力が始まるまでに確認の1ステップが挟まる
- `Export(string[], PackageModeEnum, bool, bool)` を直接呼ぶ経路は確認ウィンドウを通さない。既存の公開APIの挙動は変わらない
- Runtime への影響は無い

## テストの置き場と種別

`Tests/Editor/AssetPathTreeNodeTests.cs`（EditMode）。

Round 1 と同じく、メソッド名は英語の `対象_条件_期待` 形式にする。

| テスト | 何を検証するか | どう書くか |
| --- | --- | --- |
| `Build_NestedPaths_CreatesHierarchy` | ツリー構築 | `DOTween/Modules/DOTweenModuleUI.cs` と `DOTween/DOTween.dll` から `Build` し、ルートの子1件・その子2件を名前で辿って `Assert` |
| `Build_UnorderedPaths_SortsChildrenByName` | 並び順 | 逆順に与えたパスから `Build` し、`Children` の `Name` 列が昇順であることを `CollectionAssert.AreEqual` |
| `Build_CountsAssetsPerNode` | 件数表示 | 3件のパスから `Build` し、ルートの `AssetCount` が3、途中フォルダが2、末端が1であることを `Assert` |
| `Build_EmptyPaths_ReturnsEmptyRoot` | 空入力 | 空配列から `Build` し、`Children` が空・`AssetCount` が 0 |
| `Build_NullPaths_ReturnsEmptyRoot` | null耐性 | `null` から `Build` し、例外を出さず空のルートを返す |

確認ウィンドウ自体は `EditorWindow` であり、UI の描画を自動テストで検証しない。手動の動作確認手順で担保する。

**確認ウィンドウのボタン操作は自動で検証できない。** `EditorWindow.SendEvent` に合成した `Event` を渡しても GUILayout のボタンは反応せず、Editor ウィンドウを叩く手段が uloop にも無い。dynamic-code のサンドボックスは `Type.GetType` と `Assembly.GetType` を禁止しているため、`internal` な `Open` をリフレクションで呼ぶこともできない。**下記の動作確認手順のうち、確認ウィンドウの表示と Export / Cancel の押下は人の操作で確認する。**

## 動作確認手順

1〜2 は自動で確認する。3 以降は**人の操作で確認する**（上記の理由による）。

1. `uloop compile` で Error 0・Warning 0
2. EditMode テストが全数成功（Round 1 の12件＋本 Round の5件）。あわせて公開 `Export` 経由で出力したパッケージの中身が Round 1 と一致すること（計画への分離で出力が変わっていないことの確認）
3. Packager ウィンドウで Used Dependencies オン、Export を押す。確認ウィンドウが開き、階層ツリーに `.dll` などの強制包含アセットが含まれていること
4. 折りたたみの開閉が機能すること。各フォルダのアセット数が実際の件数と一致すること
5. Cancel で何も出力されないこと（`ExportedPackages/` に新しいフォルダができない）
6. Export で `.unitypackage` が出力され、コンソールに出力パスのログが出ること。出力内容が確認ウィンドウの一覧と一致すること
7. Used Dependencies をオフにして Export し、確認ウィンドウにディレクトリ配下の全アセットが並ぶこと。**フォルダ自体は一覧に出ない**（`ExportPackageOptions.Recurse` の出力にはフォルダのエントリも含まれるが、提示するのはアセットに限る）
8. 対象アセットが0件になる設定（拡張子一覧を空にし、どこからも参照されていないディレクトリを選ぶ）で、「（対象アセットなし）」と表示されること
9. 確認ウィンドウを開いたまま Packager ウィンドウの選択を変えても、確認ウィンドウの内容は開いた時点の計画のままであること

## バージョン判断

**マイナー（3.2.0）。** 公開APIの追加・削除は無いが、ツールの操作手順が1ステップ増える利用者向けの機能追加である。パッチとするには挙動の変化が大きい。

## この Round で触るバージョン関連ファイル

| ファイル | 変更 |
| --- | --- |
| `Assets/SymphonyFrameWork/package.json` | `3.1.0` → `3.2.0` |
| `Assets/SymphonyFrameWork/CHANGELOG.md` | `[3.2.0]` の節を追加。Added（出力確認ウィンドウ） |
| `Assets/SymphonyFrameWork/README.md` | 「現在のバージョン」を `3.2.0` へ |

`README.md` の「現在のバージョン」行は Round 1 と Round 2 の両方が触る。**Round 1 のコミットが終わってから Round 2 が同じ行を更新する**ため、作業ツリー上で競合しない。

---

## ブランチ

submodule の `develop` から `feature/133-packager-force-include` を作成済み。Round 1・Round 2 とも同じブランチで進め、Round ごとにコミットを分ける。PR は `develop` をベースにし、本文へ `Issue: #133` を記載する。
