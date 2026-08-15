# B1. 公開APIの妥当性

`public` メンバー327件・`public` 型98件を、`Documentation/DesignPhilosophy.md` の「公開範囲」節と
照合した。`Internal/` 配下49ファイルに `public` 型が漏れていないかも機械的に確認した。

## 調査サマリ

| 分類 | 件数 |
| --- | --- |
| `Runtime` / `Core` の `public` メンバー | 327 |
| `Runtime` / `Core` の `public` 型 | 98 |
| `Internal/` 配下の `public` 型 | **0** |
| XMLドキュメントの無い `public` メンバー | **0** |
| **意図せず公開されているサンプルの `public` 型** | **13** |

---

## 【確定・最優先】サンプル13クラスが公開APIとして出荷されている

**場所**: [Samples/Runtime/](../../Assets/SymphonyFrameWork/Samples/Runtime/) 配下13ファイル

`Assets/SymphonyFrameWork/Samples/` に asmdef が無いため、**サンプルスクリプトはルートの
`SymphonyFrameWork.asmdef` に取り込まれる**。Project Auditor の解析結果が、これを裏付けている。

```text
Assets/SymphonyFrameWork/Samples/Runtime/SaveDataSystemSample/Scripts/
    SaveDataSystemSample_Controller.cs   → アセンブリ: SymphonyFrameWork
Assets/SymphonyFrameWork/Samples/Runtime/SceneLoaderSample/Scripts/
    SceneLoaderSample_Controller.cs      → アセンブリ: SymphonyFrameWork
```

対象は13クラスすべてが `public sealed class` である（全件は[付録A](#付録a-出荷されているサンプル型全13件)）。

```csharp
namespace SymphonyFrameWork.Samples.SaveDataSystemSample
{
    public sealed class SaveDataSystemSample_Controller : MonoBehaviour
```

### 何が起きるか

1. **利用側の全ビルドにサンプルのMonoBehaviourが含まれる。** サンプルを使う・使わないに関わらず、
   `SymphonyFrameWork` アセンブリの一部としてリンクされる
2. **`package.json` の `samples[]` からImportすると、同じクラスが二重定義になる。**
   `package.json` は6つのサンプルを `path: "Samples/Runtime/..."` として宣言している。
   Package Manager はこのパスの内容を `Assets/Samples/Symphony Framework/<version>/` へ**コピー**する。
   コピー先も同じ `SymphonyFrameWork.Samples.*` 名前空間・同じクラス名でコンパイルされるため、
   **利用側プロジェクトが CS0101（名前空間に同じ名前の定義が複数存在する）でコンパイル不能になる**
3. **公開APIの表面積が13クラス分ふくらむ。** SemVer上、サンプルクラスの変更が破壊的変更にあたるか
   という判断が毎回必要になる

### なぜ気づきにくいか

**このワークスペースでは再現しない。** パッケージが `Assets/` 直下に置かれている開発構成では
Package Manager のImport操作自体が発生しないため、二重定義が起きない。
UPM経由で導入した利用側プロジェクトでのみ顕在化する。

### 修正方針

Unityのパッケージレイアウト規約では、**サンプルは `Samples~`（末尾チルダ）に置く**。
チルダ付きフォルダはUnityのアセットパイプラインから不可視となり、コンパイル対象から外れる。
`package.json` の `samples[].path` もチルダ付きの `Samples~/...` へ変更する。

> **訂正（2026-08-15、Issue #167 の対応時）**
> この節は当初「`samples[].path` はチルダ無しの `Samples/...` のまま記述する（Package Manager が
> 内部で読み替える）」としていたが、**誤りだった。** `Library/PackageCache/` の実パッケージを確認した
> ところ、Addressables・Input System・Cinemachine・Behavior・Animation Rigging・AI Navigation・
> App UI・SerializeReference Extensions のすべてが `"path": "Samples~/..."` と書いている。
> あわせて「配下の全 `.meta` を削除することになる」も誤りで、**実パッケージは `Samples~` 配下の
> `.meta` を保持している**（Input System 180件、Cinemachine 142件）。インポート時にGUIDごと
> 利用側へコピーするためである。実際の対応では `git mv` で `.meta` を保持したまま移動した。

```text
Assets/SymphonyFrameWork/Samples/   →   Assets/SymphonyFrameWork/Samples~/
```

**ただしこの変更には確認事項が2つある。**

1. **`AGENTS.md` §3 の `SymphonyAssetProtector` がフォルダ移動を差し戻す。**
   `Project Settings > SymphonyFrameWork` の `Asset Protection Mode` を一時的に
   `Warning` または `Disabled` にする必要がある
2. **チルダ付きフォルダは `.meta` を持たない。** 移動時に `Samples.meta` および配下の全 `.meta` を
   削除することになる。サンプルシーンがGUIDでスクリプトを参照しているため、
   **移動前にサンプルシーンの参照が壊れないことを確認する**

**これは破壊的変更にあたる。** 現在サンプルクラスを直接参照している利用者がいれば、
その参照は解決できなくなる。メジャーバージョンを上げるか、
1バージョン `[Obsolete]` を挟んでから移動する。

---

## 検証したが問題が無かった項目

- **`Internal/` 配下の `public` 型は0件。** 49ファイルすべてが `internal` に保たれている。
  `Documentation/DesignPhilosophy.md` の「公開範囲」節が実際に守られている
- **`InternalsVisibleTo` によりテストから `internal` を検証できている。**
  「テストのために `public` にした」型は見つからなかった
- **XMLドキュメントの無い `public` メンバーは0件**（→ [B7](B7_XMLドキュメントの網羅.md)）

---

## 付録A: 出荷されているサンプル型（全13件）

すべて `public sealed class`。アセンブリはいずれも `SymphonyFrameWork`。

| 場所 | 型 |
| --- | --- |
| `Assets/SymphonyFrameWork/Samples/Runtime/AudioManagerSample/Scripts/AudioManagerSample_Controller.cs:9` | `AudioManagerSample_Controller : MonoBehaviour` |
| `Assets/SymphonyFrameWork/Samples/Runtime/DebuggerSample/Scripts/DebuggerSample_Controller.cs:13` | `DebuggerSample_Controller : MonoBehaviour` |
| `Assets/SymphonyFrameWork/Samples/Runtime/DebuggerSample/Scripts/DebuggerSample_HudProbe.cs:10` | `DebuggerSample_HudProbe : MonoBehaviour` |
| `Assets/SymphonyFrameWork/Samples/Runtime/PauseManagerSample/Scripts/PauseManagerSample_Controller.cs:10` | `PauseManagerSample_Controller : MonoBehaviour` |
| `Assets/SymphonyFrameWork/Samples/Runtime/PauseManagerSample/Scripts/PauseManagerSample_Mover.cs:7` | `PauseManagerSample_Mover : MonoBehaviour, PauseManager.IPausable` |
| `Assets/SymphonyFrameWork/Samples/Runtime/SaveDataSystemSample/Scripts/SaveDataSystemSample_Controller.cs:11` | `SaveDataSystemSample_Controller : MonoBehaviour` |
| `Assets/SymphonyFrameWork/Samples/Runtime/SaveDataSystemSample/Scripts/SaveDataSystemSample_PlayerDataA.cs:8` | `SaveDataSystemSample_PlayerDataA : SaveDataContent` |
| `Assets/SymphonyFrameWork/Samples/Runtime/SaveDataSystemSample/Scripts/SaveDataSystemSample_PlayerDataB.cs:8` | `SaveDataSystemSample_PlayerDataB : SaveDataContent` |
| `Assets/SymphonyFrameWork/Samples/Runtime/SceneLoaderSample/Scripts/SceneLoaderSample_Controller.cs:11` | `SceneLoaderSample_Controller : MonoBehaviour` |
| `Assets/SymphonyFrameWork/Samples/Runtime/SceneLoaderSample/Scripts/SceneLoaderSample_SceneMarker.cs:6` | `SceneLoaderSample_SceneMarker : MonoBehaviour` |
| `Assets/SymphonyFrameWork/Samples/Runtime/ServiceLocatorSample/Scripts/ServiceLocatorSample_1.cs:7` | `ServiceLocatorSample_1 : MonoBehaviour` |
| `Assets/SymphonyFrameWork/Samples/Runtime/ServiceLocatorSample/Scripts/ServiceLocatorSample_2.cs:8` | `ServiceLocatorSample_2 : MonoBehaviour` |
| `Assets/SymphonyFrameWork/Samples/Runtime/ServiceLocatorSample/Scripts/ServiceLocatorSample_Sequences.cs:11` | `ServiceLocatorSample_Sequences : MonoBehaviour` |
