# 利用者向けモジュール文書

## 目的

`Documentation~/Modules/` のモジュール文書を、利用者が使うための情報だけをまとめた形にする。

6.15.0 までのモジュール文書は「入口 → クイックスタート → 実装時の注意 → Editor機能 → 内部構造 → 関連」の並びだった。次の問題があった。

| 問題 | 内容 |
| --- | --- |
| 内部構造が利用者向けの文書にある | Service、Registry、Entity、Query、ViewModelのクラス構成図は、利用側のコードを書くのに要らない。AIが読むときのコンテキストも増やしていた |
| 失敗時の動きが散らばっている | 例外の型、`false`・`null`を返す条件、決まりごとが「実装時の注意」の1つの箇条書きに混ざり、「何が起きたらどうなるか」を引きにくい |
| 範囲が書かれていない | そのモジュールが何をしないか（例: Pause Managerは`Time.timeScale`を変えない）が書かれておらず、利用者が試して確かめるしかなかった |
| 機能を指す名前が無い | 機能の一覧が無く、Issueや会話で「どの機能か」を言い表せない |

## 決定

- モジュール文書の並びを「概要 → 目的と範囲 → 入口 → 仕組みの要点 → 機能一覧 → クイックスタート → 状態 → うまくいかないとき → 守ること → Editor機能 → 確かめ方 → 関連」にする。各節の書き方は [DocumentationGuidelines.md §2.1](../DocumentationGuidelines.md#21-モジュール文書の各節) に置く。
- 機能一覧にはモジュールごとの接頭辞を持つIDを付ける。

  | モジュール | 接頭辞 |
  | --- | --- |
  | Service Locator | `SL` |
  | Scene Loader | `SCN` |
  | Scene Block | `BLK` |
  | Save Data System | `SAVE` |
  | Audio Manager | `AUD` |
  | Pause Manager | `PAUSE` |
  | Debug | `DBG` |
  | Utility | `UTIL` |
  | Inspector属性 | `ATTR` |
  | AutoEnumGenerator | `ENUM` |
  | Asset Store Tools Packager | `ASTP` |
  | Project Structure Tools | `PST` |
  | Build Validation | `BV` |

- 内部構造の節とmermaidのクラス図は、ワークスペースの [ModuleInternals.md](../ModuleInternals.md) へ移す。`Documentation~/Architecture.md` も、アセンブリ・公開の入口・起動順を表と番号付きリストで示す形にする。
- 内部構造に書かれていた事実のうち、利用者の判断に要るもの（AudioSourceを作る時点、同じ値の再設定で通知しないこと）は「仕組みの要点」か「うまくいかないとき」へ言い換えて残す。

この並びは、SymphonyKillChord のプロジェクトドキュメント（システムリスト）で採ったモジュールの書き方を、利用者向けに絞ったものである。開発側の情報（構成、処理フロー、この形にした理由）は利用者向けの文書から外し、ワークスペース側へ置く。

## 採らなかった案

| 案 | 採らなかった理由 |
| --- | --- |
| 内部構造の節をモジュール文書の末尾に残す | 「読み飛ばせる位置にある」だけでは、AIが文書を丸ごと読むときのコンテキストは減らない |
| 内部構造を削除する | 本体開発者が層の分け方を確かめる手段が無くなる。設計書（Designs）はRoundごとに分かれていて、今の構成を1か所で読めない |
| 「この形にした理由」の節をモジュール文書に置く | 設計判断はDesignsとCHANGELOGに既にある。利用者が使うときに要らない |

## 版

- パッケージ: `6.15.1`（パッチ）。公開APIとシリアライズ形式は変えない。
