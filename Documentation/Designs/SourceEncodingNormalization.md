# ソース文字コードの統一 — Round K

## 目的

`Documentation/CONTRIBUTING.md` の「3. 文字コードと改行」は `.cs` を **UTF-8 BOM付き**と定めているが、パッケージ内に BOM 無しの `.cs` が 17 件残っている。これを規約どおりへ揃える。

**BOM が無くてもコンパイルは通る。** 日本語コメントを含むファイルを BOM 無しで保存した場合、環境によってはシステム既定のコードページで解釈されて文字化けする。この規約はコミット `ccac325` で一度統一されたものだが、その後に追加されたファイルで崩れている。

Round I2 のレビューで発見した。原因は、ファイル書き込みツールの多くが BOM を付けないことであり、ワーカーの成果物でも手作業の実装でも起こる。**再発防止は `.agents/skills/implement/references/review.md` の機械チェックで既に入れてある**（コミット `8ca6c81`）。本 Round は既存分の後始末である。

コードの挙動は 1 バイトも変えない。単独で検証・リリース可能な **2.17.1** とする。

## 前提の確認

着手時にパッケージ全体を走査して確認した事実を記録する。

| 前提 | 確認結果 |
| --- | --- |
| BOM 無しの `.cs` の件数 | **17 件**（下記の一覧） |
| 逆方向の違反（`.md` / `.json` / `.asmdef` / `.uxml` / `.uss` に BOM が付いている） | **0 件**。本 Round では扱わない |
| 改行コードの扱い | `.gitattributes` は存在せず、`core.autocrlf=true` 前提。リポジトリには LF で格納される |
| BOM 付与が改行差分を生むか | 生まない。バイト列の先頭へ 3 バイトを足すだけで、既存の改行はそのまま残る |

## 対象ファイル

| 区分 | パス |
| --- | --- |
| Runtime | `Runtime/AssemblyInfo.cs` |
| Runtime | `Runtime/Configs/Internal/SaveSystemConfig.cs` |
| Runtime | `Runtime/Orchestrator/Internal/SymphonyOrchestratorObject.cs` |
| Runtime（Save Data） | `Runtime/System/SaveSystem/SaveDataLoader.cs` |
| Runtime（Save Data） | `Runtime/System/SaveSystem/SaveDataRegistry.cs` |
| Runtime（Save Data） | `Runtime/System/SaveSystem/SaveDataRegistryEntryInfo.cs` |
| Runtime（Save Data） | `Runtime/System/SaveSystem/Internal/Infrastructure/JsonUtilitySaveDataLoader.cs` |
| Runtime（Save Data） | `Runtime/System/SaveSystem/Internal/Infrastructure/NewtonsoftSaveDataLoader.cs` |
| Runtime（Save Data） | `Runtime/System/SaveSystem/Template/PlayerPrefsSaveDataLoader.cs` |
| Editor | `Editor/Administrator/UITK/CS/SaveDataDebugState.cs` |
| Editor | `Editor/Configs/SymphonyEditorConfigLocator.cs` |
| Editor | `Editor/SettingProvider/SaveSystemSettingProvider.cs` |
| Tests | `Tests/Editor/ReactivePropertyTests.cs` |
| Samples | `Samples/Runtime/AudioManagerSample/Scripts/AudioManagerSample_Controller.cs` |
| Samples | `Samples/Runtime/PauseManagerSample/Scripts/PauseManagerSample_Controller.cs` |
| Samples | `Samples/Runtime/PauseManagerSample/Scripts/PauseManagerSample_Mover.cs` |
| Samples | `Samples/Runtime/SceneLoaderSample/Scripts/SceneLoaderSample_SceneMarker.cs` |

Save Data 系が多いのは、Round I1 / I2 で私が追加・書き換えたファイル群がこの規約を外していたためである。I1 / I2 で新規に作った 9 件は I2 のコミット `2ba21c4` で既に修正済みで、ここに残るのはそれ以前から存在するファイルである。

## 変更内容

各ファイルの先頭へ UTF-8 BOM（`EF BB BF`）を付与する。**それ以外は 1 バイトも変更しない。**

```bash
printf '\xef\xbb\xbf' | cat - "$f" > "$f.tmp" && mv "$f.tmp" "$f"
```

`cat` はバイト列をそのまま連結するため、既存の改行コードは保たれる。エディタで開いて保存し直す方法は、改行や末尾空白を巻き込む可能性があるため採らない。

`.meta` は変更しない。BOM の有無は `.meta` に記録されない。

## 公開API

変更しない。

## ファイル構成

新規・削除は無い。上記 17 件の内容変更のみ。

## 依存方向

変更しない。

## エラー処理

変更しない。

## 影響範囲

- **コードの挙動、公開 API、シリアライズ形式のいずれも変わらない**
- git の差分は各ファイル 1 行（先頭行）のみ。`git diff --stat` が 17 files changed, 17 insertions(+), 17 deletions(-) になることを確認する
- `Samples/` の 4 件は Package Manager 経由で利用側のプロジェクトへ取り込まれるが、文字コードの変更に留まる
- `git blame` の先頭行だけが本コミットへ移る。それ以外の行の履歴は保たれる

## テストの置き場と種別

**自動テストは追加しない。** 検証対象は「リポジトリ上のファイルのバイト列」であり、実行時の振る舞いではない。テストアセンブリからソースファイルのバイト列を読む検証は、テストのためだけにパス依存の処理をテストコードへ持ち込むことになる。

代わりに、`references/review.md` へ追加済みの機械チェック（コミット `8ca6c81`）を全ファイルへ広げた形で実行し、0 件になることを確認する。これは CI ではなくレビュー手順として運用する。

既存の EditMode 174 件・PlayMode 4 件が引き続き全数成功することを確認する。**BOM の付与でコンパイル結果やテスト結果が変わってはいけない**ので、これは変化が無いことの確認である。

## 動作確認手順

1. 17 件へ BOM を付与する
2. `find` による全走査で、BOM 無しの `.cs` が **0 件**になったことを確認する
3. 逆方向（`.md` / `.json` / `.asmdef` / `.uxml` / `.uss` に BOM）が引き続き 0 件であることを確認する
4. `git diff --stat` が 17 files changed, 17 insertions(+), 17 deletions(-) であることを確認する。これ以外の行が動いていたら、改行コードを巻き込んでいる
5. `uloop compile` を実行し、Error 0・Warning 0 を確認する
6. EditMode 174 件と PlayMode 4 件が全数成功することを確認する
7. 日本語コメントを含むファイル（`SaveDataRegistry.cs` など）を開き、文字化けしていないことを確認する

Play Mode を伴う確認は不要である。実行時の振る舞いを変えないため。

## バージョン判断

**パッチ（2.17.1）。** 公開 API の追加も変更も無く、実装の挙動も変わらない。ソースファイルの文字コードを規約へ揃えるだけである。

## この Round で触るバージョン関連ファイル

| ファイル | 変更 |
| --- | --- |
| `Assets/SymphonyFrameWork/package.json` | `2.17.0` → `2.17.1` |
| `Assets/SymphonyFrameWork/CHANGELOG.md` | 2.17.1 へ文字コードの統一を記録 |
| `Assets/SymphonyFrameWork/README.md` | 現在のバージョン |

`Documentation~/` は変更しない。利用側の使い方に影響しないため。

## ブランチ

`develop` から `fix/source-encoding-normalization` を作成する。
