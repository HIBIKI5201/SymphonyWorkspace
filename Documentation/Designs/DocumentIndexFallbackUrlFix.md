# DocumentIndexFallbackUrlFix

Issue: [#215](https://github.com/HIBIKI5201/SymphonyFramework/issues/215) 再オープン分（索引のGitHubフォールバックが古い前提のまま）

## 目的

`SymphonyDocumentPathResolver.ResolveFallbackUrl(SymphonyDocumentPageEnum.Index)`は、「索引はHTML専用で正本のMarkdownが無い」という前提のコメントとともに、`{REPOSITORY_URL}/tree/{FALLBACK_BRANCH}/{MARKDOWN_DIRECTORY}/Modules`（モジュール文書のディレクトリ一覧）を返す特別扱いをしている（[SymphonyDocumentPathResolver.cs:74-80](../../Assets/SymphonyFrameWork/Editor/Documentation/Internal/SymphonyDocumentPathResolver.cs)）。

Issue #215本体の対応（PR #216）で`Documentation~/index.md`を追加し、`DOCUMENT_NAMES`辞書には既に`[SymphonyDocumentPageEnum.Index] = "index"`が登録済みである（同ファイル112行目）。**この特別扱いの前提が既に成立しなくなっている。** 同梱HTMLを解決できない環境（Package未導入、HTML再生成前など）で索引を開こうとすると、`index.md`を直接指さず、ディレクトリ一覧へ飛んでしまう。

## 公開API

変更なし。`SymphonyDocumentPathResolver`は`internal`。

## ファイル構成

- `Editor/Documentation/Internal/SymphonyDocumentPathResolver.cs`（変更）: `ResolveFallbackUrl`の`Index`特別扱い（74-80行目）を削除する
- `Tests/Editor/SymphonyDocumentPathResolverTests.cs`（変更）: `ResolveFallbackUrl_IndexPage_PointsToModulesDirectory`を、`index.md`を指すことを検証するテストへ書き換える

## 修正方針

`DOCUMENT_NAMES`に`Index => "index"`が既に登録されているため、特別扱いを消すだけで、既存の汎用ロジック（`TryGetDocumentName`が成功した場合に`{REPOSITORY_URL}/blob/{FALLBACK_BRANCH}/{MARKDOWN_DIRECTORY}/{documentName}.md`を返す）がそのまま`{REPOSITORY_URL}/blob/main/Documentation~/index.md`を返す。**新しいロジックを足す必要はない。**

```csharp
internal static string ResolveFallbackUrl(SymphonyDocumentPageEnum page)
{
    // 未定義のページでも利用者が文書を探せるよう、リポジトリのトップへ戻す。
    if (!TryGetDocumentName(page, out string documentName)) { return REPOSITORY_URL; }

    return $"{REPOSITORY_URL}/blob/{FALLBACK_BRANCH}/{MARKDOWN_DIRECTORY}/{documentName}.md";
}
```

## エラー処理

変更なし。既存の「未定義のページはリポジトリのトップへ戻す」という契約は維持する。

## 影響範囲

- 公開APIへの影響なし（`internal`）
- 挙動の変化は、同梱HTMLが無い環境で索引を開いたときのフォールバック先だけ（ディレクトリ一覧 → `index.md`）
- ドキュメント文書側の追加更新は不要（Issue本文・#215の他のコメントは既にこの状態を前提に書かれている）

## テストの置き場と種別

`Tests/Editor/SymphonyDocumentPathResolverTests.cs`（EditMode、既存ファイルを変更）。

- `ResolveFallbackUrl_IndexPage_PointsToModulesDirectory`を`ResolveFallbackUrl_IndexPage_PointsToIndexMarkdownOnMain`へ改名し、`SymphonyDocumentPathResolver.ResolveFallbackUrl(SymphonyDocumentPageEnum.Index)`が`REPOSITORY_URL + "/blob/main/Documentation~/index.md"`と一致することを`Assert.That(url, Is.EqualTo(...))`で検証する

既存の`ResolveFallbackUrl_KnownPage_PointsToMarkdownOnMain`（`SceneLoader`ページ）はそのまま通ることを確認する（変更していないため）。

## 動作確認手順

自動テストのみで検証できる（Unity APIへ触れない純粋ロジックのため）。人による確認は不要。

## バージョン判断

パッチ（実装のみ・不具合修正、公開APIへの影響なし）。

## この Round で触るバージョン関連ファイル

- `package.json` の `version`: パッチを1つ上げる
- `CHANGELOG.md`: `### Fix` 見出しへ本件を追記

## 実施レポート

実施日: 2026-09-06 / バージョン: 6.14.5 / PR: [#220](https://github.com/HIBIKI5201/SymphonyFramework/pull/220)

### 実装した内容

設計どおり、`SymphonyDocumentPathResolver.ResolveFallbackUrl`の`Index`専用の特別扱い（5行）を削除した。`DOCUMENT_NAMES`辞書に既に`Index => "index"`が登録済みだったため、既存の汎用ロジックがそのまま`{REPOSITORY_URL}/blob/main/Documentation~/index.md`を返すようになった。`Tests/Editor/SymphonyDocumentPathResolverTests.cs`の`ResolveFallbackUrl_IndexPage_PointsToModulesDirectory`を`ResolveFallbackUrl_IndexPage_PointsToIndexMarkdownOnMain`へ改名し、新しい期待値へ書き換えた。

実装はCodex CLIワーカーへ委譲した。差分は自分で確認し、設計書どおり2ファイルのみの変更であることを確認した。

### 設計から変えた点

無し。設計書どおりに実装された。

### 検証結果

`python scripts/verify_round.py --json` を自分で実行した実測値:

- compile: 0 errors / 0 warnings
- EditMode: 737 / 737 成功
- PlayMode: 21 / 21 成功（2往復とも）

`python scripts/release_round.py preflight`は全項目OK（`docs`同期を含む）。

### 未実施の確認

無し。Unity APIへ触れない純粋ロジックの変更で、自動テストのみで完結する。

### 振り返り

気づきは無し。
