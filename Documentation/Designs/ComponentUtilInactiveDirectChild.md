# ComponentUtilの非アクティブな直下の子の除外

## 目的

Issue #179 に対応し、`SymphonyComponentUtil.GetComponentInChildrenExcludeSelf<T>` が `includeInactive: false` のとき、直下の非アクティブな子が持つComponentを返す不具合を修正する。XMLドキュメントが定める「非アクティブな子を検索しない」という既存契約へ実装を一致させる。

## 公開API

シグネチャとXMLドキュメントは変更しない。

```csharp
public static T GetComponentInChildrenExcludeSelf<T>(
    this Transform self,
    bool includeInactive = false)
    where T : Component
```

`SymphonyComponentUtil` は特定サブシステムに依存しない公開Utilityであり、`DesignPhilosophy.md` の公開範囲に適合している。今回変更するのは既定引数が既に表す契約に反していた挙動だけで、新しい公開型やメンバーは追加しない。

## ファイル構成

| ファイル | 変更内容 |
| --- | --- |
| `Assets/SymphonyFrameWork/Runtime/Utility/SymphonyComponentUtil.cs` | `includeInactive` が `false` で `child.gameObject.activeInHierarchy` が `false` の直下の子を検索起点から除外し、Issue #179 のTODOを削除する |
| `Assets/SymphonyFrameWork/Tests/Editor/SymphonyComponentUtilTests.cs` | 現在の不具合を固定しているテストを、非アクティブな直下の子を返さない契約のテストへ変更する |
| `Assets/SymphonyFrameWork/Documentation~/Modules/Utility.md` | `includeInactive` の既定挙動と、`true` のときだけ非アクティブな子を含めることを明記する |
| `Assets/SymphonyFrameWork/Documentation~/Html/**` | 利用者向けMarkdownから再生成する |
| `Assets/SymphonyFrameWork/CHANGELOG.md` | Issue #179 の修正をパッチ版の `Fix` として記録する |
| `Assets/SymphonyFrameWork/package.json` | パッチ版へ更新する |
| `Assets/SymphonyFrameWork/README.md` | 現在のバージョンをパッチ版へ更新する |
| `Assets/SymphonyFrameWork/Core/SymphonyConstant.cs` | 実行時バージョン定数をパッチ版へ更新する |

既存ファイルだけを変更するため、新しい `.meta` は発生しない。名前空間は既存の `SymphonyFrameWork` のままとし、配置も変更しない。

## 依存方向

公開Utilityが既に参照しているUnity Runtime APIだけを使用する。`Runtime` から `Editor` への依存、新しいアセンブリ参照、リフレクションは追加しない。

## エラー処理

例外、ログ、戻り値の契約は変更しない。検索対象が無い場合は従来どおり `null` を返す。`self` が `null` の場合の既存Unity例外挙動も変更しない。

## 影響範囲

`includeInactive: false` または既定値で呼び出したとき、非アクティブな直下の子が持つComponentを誤って返していた挙動だけが変わる。アクティブな子、アクティブな子の下にある孫、`includeInactive: true` の検索順と結果は変えない。

これはXMLドキュメントと既定引数の意味へ実装を戻す修正であり、公開シグネチャやシリアライズ形式の破壊的変更ではない。誤った挙動へ依存している利用側には結果の変化があるが、契約違反の是正としてパッチ更新で扱う。

## テストの置き場と種別

EditMode の既存 `Assets/SymphonyFrameWork/Tests/Editor/SymphonyComponentUtilTests.cs` を変更する。

| テスト | 実装方法 |
| --- | --- |
| `GetComponentInChildrenExcludeSelf_InactiveDirectChild_IsSkippedByDefault` | 直下の子へ `BoxCollider` を追加して非アクティブ化し、既定呼び出しが `null` を返すことを検証する |
| `GetComponentInChildrenExcludeSelf_IncludeInactive_FindsInactiveChild` | 既存テストを維持し、同じ構成で `includeInactive: true` がComponentを返すことを回帰確認する |
| 既存の子・孫・非アクティブな孫のテスト | 全数実行し、直下以外の検索契約が変わらないことを確認する |

準備は公開APIとUnityの通常APIだけで行い、内部状態へ直接触れない。事後状態は検索結果そのものを比較するため、準備操作による通知回数などの差分比較は不要である。

## 動作確認手順

1. `python scripts/verify_round.py` を実行し、コンパイルがエラー0・警告0、EditModeとPlayModeが全数成功することを確認する。
2. Domain Reload無効のPlay Mode開始・終了が2往復とも成功し、Consoleに新しいエラーや警告が無いことを確認する。
3. `python scripts/build_module_docs.py --check` と `python scripts/release_round.py preflight` を実行し、生成文書、テスト差分、BOM、依存方向、版表記の整合を確認する。

Editor UI、Scene、Prefab、シリアライズ形式は変更しないため、スクリーンショットや人によるGUI操作確認は不要である。

## バージョン判断

パッチ更新。公開APIの追加やシグネチャ変更はなく、既存のXMLドキュメント契約に反する検索結果を修正するため。着手時点の `6.3.1` から `6.3.2` へ更新する。

## この Round で触るバージョン関連ファイル

| ファイル | 変更内容 |
| --- | --- |
| `Assets/SymphonyFrameWork/package.json` | `6.3.2` |
| `Assets/SymphonyFrameWork/CHANGELOG.md` | `6.3.2` の `Fix` を先頭へ追加 |
| `Assets/SymphonyFrameWork/README.md` | 現在のバージョンを `6.3.2` へ更新 |
| `Assets/SymphonyFrameWork/Core/SymphonyConstant.cs` | `VERSION` を `6.3.2` へ更新 |

公開型索引、メニューパス、Sample、`AGENTS.md`、`EditorTools.md`、`Deprecations.md` は変化しないため更新しない。

## Round分割

1 Roundで完了する。実装1ファイル、テスト1ファイル、利用者向け文書と生成物、4つのバージョン関連ファイルだけを扱い、他のIssueやUtilityのリファクタリングは含めない。
