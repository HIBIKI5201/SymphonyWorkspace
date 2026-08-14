# 監査の観点

`Docs/RuntimeAudit`（ゲームプロジェクト側の18観点）を出発点に、**配布物であるフレームワークに
合わせて取捨と読み替えを行ったもの**。A群は元の観点を引き継いだもの、B群はフレームワーク固有で
追加したもの、C群は当てはまらないので外したもの。

各観点には**検出手段**を明記している。`scan` は `scripts/audit_scan.py` の分類名、
`PA` は Project Auditor、`読解` は人／AIがコードを読んで判断するものを指す。

---

## 観点一覧

| # | 観点 | 検出手段 | 優先度 |
| --- | --- | --- | --- |
| 01 | イベント購読解除漏れ | scan | 高 |
| 02 | Null参照リスクとガード漏れ | 読解 | 中 |
| 03 | ライフサイクルと static 状態のリセット | scan + Play Mode | **最高** |
| 04 | GC Alloc・メモリリーク | PA | 中 |
| 05 | 不必要な繰り返し処理 | scan + PA | 中 |
| 06 | 非効率なデータ構造 | scan + PA | 中 |
| 07 | 外部依存の妥当性 | 読解 | 中 |
| 08 | アセンブリ境界とレイヤー違反 | scan | 高 |
| 09 | マジックナンバーと定数化 | scan | 低 |
| 10 | コード重複と過剰抽象化 | 読解 | 中 |
| 11 | 非同期処理の不純点 | scan | 高 |
| 12 | ログ運用とビルド影響 | scan | 高 |
| 13 | DRY | 読解 | 中 |
| 14 | KISS / YAGNI | scan + 読解 | 中 |
| 15 | クラス種別と責務の一致 | 読解 | 中 |
| 16 | SOLID（特に SRP） | scan + 読解 | 中 |
| 17 | デメテルの法則と CQS | 読解 | 低 |
| 18 | 命名一貫性と可読性 | scan + 読解 | 低 |
| B1 | 公開APIの妥当性 | scan + 読解 | **最高** |
| B2 | `[Obsolete]` と Deprecations.md の同期 | scan（双方向） | 高 |
| B3 | Editor機能と EditorTools.md の同期 | 読解 | 高 |
| B4 | 利用側への非侵襲性 | 読解 | 高 |
| B5 | 自動生成物との境界 | 読解 | 中 |
| B6 | テストとサンプルの追随 | scan + 読解 | 高 |
| B7 | XMLドキュメントの網羅 | scan | 中 |
| B8 | 文字コード・改行・`.meta` | scan | 低 |
| B9 | コード内 `TODO` の棚卸し | scan + 読解 | 中 |

---

## A群 — RuntimeAudit から引き継いだ観点

### 01 イベント購読解除漏れ

**検出**: `scan` の `01_subscribe_imbalance` / `01_lambda_subscribe`

`+=` と `-=` をファイル単位で突き合わせ、差分のあるファイルだけを読む。

判断基準:

- **`static event` は最優先。** インスタンスの `event` はオブジェクトごと GC されるが、
  static は Domain Reload 無効下でエディタセッションを跨いで生き残る
- **`OnDestroy` / `Dispose` の早期 return より前で解除されているか。** 早期 return の後ろに
  解除を置くと、参照が先に破棄されたときだけ到達しなくなる
- **ラムダ購読は `-=` が書けない。** フィールドへ退避してから購読しているか
- 解除が非対称なのが**意図的なら、その理由がコメントかXMLドキュメントに書かれているか**

### 02 Null参照リスクとガード漏れ

**検出**: 読解

このフレームワークには `SymphonyDebugLogger.LogAndCheckComponentNull` /
`IsComponentNotNull` という自前ガードがある。よって観点は「ガードが無い箇所を探す」ではなく
**「自前ガードを使うべき箇所で使っていない」**へ読み替える。

- `LogError` / `LogWarning` の直後に `return` が無く、そのまま NullReference へ進む経路
- Unity オブジェクトに対する `??` / `??=`（Unity の `==` オーバーロードが効かず、
  破棄済みオブジェクトを非 null と判定する）
- `SerializeField` に対する起動時の存在確認

### 03 ライフサイクルと static 状態のリセット

**検出**: `scan` の `03_static_without_reset` / `03_reset_not_registered` + Play Mode 実測

**この観点がこのフレームワークで最も重要。** `ProjectSettings/EditorSettings.asset` の
`m_EnterPlayModeOptions: 3` により **Domain Reload と Scene Reload の両方が無効**であり、
static 状態は Play Mode 終了時にリセットされない。各 Facade の `ResetRuntimeState()` は
この前提のために存在する。

判断基準:

- static な可変状態（フィールド、自動プロパティ、`event`）を持つ型に `ResetRuntimeState()` があるか
- その `ResetRuntimeState()` が `SymphonyOrchestrator` の初期化列へ登録されているか
- **`ResetRuntimeState()` が、その型の static 状態を*全部*戻しているか。**
  フィールドを1つ足したときに戻し忘れるのがこの観点で最も多い欠陥
- **Play Mode の開始・終了を2回繰り返して、ゴースト参照が残らないこと**（`uloop-control-play-mode`）

### 04 GC Alloc・メモリリーク

**検出**: `PA`（`IssueCategory.Code`）

Project Auditor がボクシング、`string` 連結、`params` 配列、クロージャ確保を IL レベルで拾う。
grep では取れないのでここはツールに任せる。

- フレームワークは毎フレーム実行されるコードが薄いので、**ホットパスにあるものだけを「確定」にする**
- マテリアルの暗黙クローン（`.material` アクセス）、`Texture2D` / `RenderTexture` の破棄漏れ
- `IReadOnlyList<T>` に対する `foreach` の列挙子ボクシング

### 05 不必要な繰り返し処理

**検出**: `scan` の `05_linq_in_runtime` + `PA`

- Runtime / Core での `System.Linq`。1回きりの初期化なら許容、ループ内なら指摘
- ループ内で不変の値を毎回取得している箇所
- 文字列によるシェーダプロパティ／アニメーションパラメータの毎回解決

### 06 非効率なデータ構造

**検出**: `scan` の `06_struct_without_iequatable` + `PA`

- `IEquatable<T>` 未宣言の `struct`（型付き `Equals` を実装済みなら**宣言を足すだけでボクシングが消える**）
- `List.Contains` による線形探索が、要素数が増えうる場所にあるか
- レジストリ／ロケータの探索方式が `Dictionary` か線形か

### 07 外部依存の妥当性

**検出**: 読解

**ゲーム側の「ライブラリ未活用」を反転させた観点。** フレームワークは利用側へ依存を強制するため、
増やす側ではなく**減らす側**で見る。

- `package.json` の `dependencies` と、実際に使っている API の対応
- Unity 標準機能（`Awaitable`、`UnityEngine.Pool`、`Physics.RaycastNonAlloc`）で置換できる自前実装
- `asmdef` の `references` に、実際には参照していないアセンブリが残っていないか

### 08 アセンブリ境界とレイヤー違反

**検出**: `scan` の `B_unity_editor_in_runtime` + `asmdef` の読解

ゲーム側の7レイヤー構成は当てはまらないので、**アセンブリ境界と `Internal/` の使い分け**へ置き換える。

```text
SymphonyFrameWork.Editor ──> SymphonyFrameWork ──> SymphonyFrameWork.Core
```

- Runtime / Core から `UnityEditor` / `EditorPrefs` / `AssetDatabase` への参照。
  **`#if UNITY_EDITOR` で囲んであっても違反として扱う**（`Documentation/CONTRIBUTING.md`）
- `Internal/` 配下の型が `public` になっていないか
- 名前空間とディレクトリの一致（`Documentation/CodeGuidelines.md` の「名前空間」節）
- テスト用 asmdef の `defineConstraints` に `UNITY_INCLUDE_TESTS` があるか

### 09 マジックナンバーと定数化

**検出**: `scan` の `09_magic_number`

`Core/SymphonyConstant.cs` が既にあるので、判断は**「散在しているか」ではなく
「SymphonyConstant へ集約すべきものが実装内に残っていないか」**。

- 2箇所以上に同じ数値が出るなら定数化の対象
- 1箇所しか出ない数値は、**名前が付いていないことが問題なので `const` ローカルで足りる**

### 10 コード重複と過剰抽象化 / 13 DRY

**検出**: 読解

- 同じ処理のコピペ（構造の重複）
- **同じ「知識」が複数箇所に書かれていないか**（デフォルト値、パス、閾値）。
  コードの見た目が違っても知識が重複していれば DRY 違反
- 逆に、1箇所でしか使わない抽象化が入っていないか（過剰抽象化）

### 11 非同期処理の不純点

**検出**: `scan` の `11_async_void` / `11_missing_cancellation_token`

- **`async void`** — 例外が呼び出し元へ伝播せず握り潰される。
  Editor の UI コールバックなど**意図的な場合はその理由がコメントにあるか**を見る
- **`CancellationToken` 引数の欠落** — 破棄後も待機が続く。`scan` は引数の有無しか見ないので、
  「トークンを受け取っているが下流へ渡していない」は読解で拾う
- `Documentation/CodeGuidelines.md` の「非同期処理」節、
  `DesignPhilosophy.md` の「非同期処理の型」節が正本。`Task` ではなく `Awaitable` へ寄せる方針

### 12 ログ運用とビルド影響

**検出**: `scan` の `12_raw_debug_log`

`SymphonyDebugLogger` に `[Conditional("UNITY_EDITOR")]` 付きの API が揃っているので、
**素の `Debug.Log` はビルドへ残る**という明快な基準で判断できる。

- Runtime / Core での素の `Debug.Log*`。文字列補間を伴うものは**条件を満たさなくても文字列が構築される**
- エラーを検知した後に処理を継続している箇所（`LogError` の後に `return` が無い）
- 利用側のコンソールを汚さないか。**フレームワークのログは既定で静かであるべき**

### 14 KISS / YAGNI

**検出**: `scan` の `14_single_implementation_interface` + 読解

- 実装が1件しかないインターフェース。**ただしテストのモック用、
  および公開APIの契約として置いているものは正当**。区別せずに指摘しない
- 使われていない拡張点、到達しない分岐
- `typeof` / リフレクションによる投機的な汎用化

### 15 クラス種別と責務の一致

**検出**: 読解

ゲーム側の GRASP 観点（`*Controller` の濫用）を、**`DesignPhilosophy.md` が定義する
クラス種別**（Orchestrator / Service / Strategy / Query / Registry / Entity / Info /
ViewModel / Component / DTO / Loader / Factory / Config / Initializer / Debugger）へ置き換える。

- 型名の接尾辞が、`DesignPhilosophy.md` の定義どおりの責務になっているか
- 情報を持っている型が、その情報を使う処理も持っているか（Information Expert）

### 16 SOLID（特に SRP）

**検出**: `scan` の `16_long_file` / `16_long_method` + 読解

- 300行超のファイル、60行超のメソッド。**行数は責務過多の兆候であって、それ自体は欠陥ではない。**
  必ず「何と何を抱えているか」を書く
- 既定で `true` を返す基底クラス（LSP）
- `enum` に対する `switch` が複数箇所に散っていないか（OCP）

### 17 デメテルの法則と CQS

**検出**: 読解

- 3段以上のメンバーチェーン
- 引数が多すぎる `Initialize`
- 値を返しつつ状態を変えるメソッド、`ref` と `out` の併用

### 18 命名一貫性と可読性

**検出**: `scan` の `18_todo` / `18_commented_code` + 読解

- 綴り誤り、表記ゆれ（`Usecase` / `UseCase` のような）
- `TODO` / `FIXME` / `HACK`、コメントアウトされたコード
- `Documentation/CodeGuidelines.md` の「命名規則」「メンバーの記述順」への適合

---

## B群 — フレームワーク固有で追加した観点

**ここがゲームプロジェクト向け監査との本質的な差分。** 配布物であることに起因する。

### B1 公開APIの妥当性

**検出**: `scan` の `B_public_in_internal` + 読解

**最も重要な観点。一度 `public` にした API は SemVer 上、外すのにメジャーバージョンを要求する。**

- `public` である必要があるか。`internal` で足りないか。
  `InternalsVisibleTo` によりテストからは `internal` も検証できるので、
  **「テストのために public」は理由にならない**
- `Internal/` 配下に `public` 型が漏れていないか
- 公開範囲の判断は `Documentation/DesignPhilosophy.md` の「公開範囲」節が正本

**指摘するときは、破壊的変更になるかどうかを必ず明記する。**
なる場合は `[Obsolete]` を経由する段階的な削除計画まで書く。

### B2 `[Obsolete]` と Deprecations.md の同期

**検出**: `scan` の `B_obsolete` / `B_obsolete_undocumented` / `B_deprecation_stale`

`Assets/SymphonyFrameWork/Documentation~/Deprecations.md` が正本。
`AGENTS.md` §0.1 が「記載漏れはバグ」と規定している。

**検査は双方向に行う。片方向だけでは片方の不整合しか拾えない。**

| 向き | 拾えるもの | 分類 |
| --- | --- | --- |
| コード → ドキュメント | `[Obsolete]` を付けたのに記載していない | `B_obsolete_undocumented` |
| ドキュメント → コード | 削除が済んだのに `## 削除済み` へ移していない | `B_deprecation_stale` |

後者は**コード側からは原理的に検出できない**（シンボルが消えているため手掛かりが無い）。

読解で確認するもの:

- 各行に削除予定が書かれているか（未定なら「未定」と明記されているか）
- 移行先として書かれたAPIが実在するか

### B3 Editor機能と EditorTools.md の同期

**検出**: 読解

`Assets/SymphonyFrameWork/Documentation~/EditorTools.md` が正本。

- `Editor/` 配下の各ディレクトリに対応する節があるか
- メニューパス、Project Settings の位置が実装と一致しているか。
  **実際に、廃止済みのメニューへの言及が残っていた前例がある**

**この観点は機械化を試みて失敗している。** `EditorTools.md` は実装ディレクトリではなく
機能名で節を立てるため（`SettingProvider` → 「Framework設定」「Save System設定」…）、
ディレクトリ名の文字列一致では誤検出率100%になった。**読解に委ねること。**
`audit_scan.py` へ同種の検査を再び足さない。

### B4 利用側への非侵襲性

**検出**: 読解

- 利用側のシーン、Build Settings、Project Settings を勝手に書き換えていないか
- グローバルな状態（`Time.timeScale`、`Application.targetFrameRate`、`QualitySettings`）を
  無断で変更していないか。変更するなら**元へ戻す責務が明示されているか**
- `[InitializeOnLoad]` / `[RuntimeInitializeOnLoadMethod]` の副作用が、
  利用側に説明されているか
- `Resources/` へアセットを生成する挙動が、利用側から制御できるか

### B5 自動生成物との境界

**検出**: 読解

- `EnumGenerator` / `AutoEnumGenerator` の生成物（`Assets/Scripts/SymphonyFrameWork/*Enum.cs`）が
  手編集されていないか
- `SymphonyConfigManager.AllConfigCheck()` が生成する `Assets/Resources/SymphonyFrameWork/*.asset` の
  型が `internal` に保たれているか
- **生成コードの再生成で壊れる依存が無いか**（生成物の中身に依存した実装）

### B6 テストとサンプルの追随

**検出**: `scan`（テスト数の集計）+ 読解

- **PlayMode テストが薄い箇所**。`Tests/Runtime/` の対象が、
  Play Mode 依存の機能（シーンロード、ポーズ、オーディオ）を覆えているか
- `LogAssert.ignoreFailingMessages` を使っていないか（ログを消費しないためテスト間へ漏れる）
- テストで再現できない範囲（モーダルダイアログ、Play Mode の往復）が
  `Samples/Runtime/*Sample/` のサンプルシーンで担保されているか

### B7 XMLドキュメントの網羅

**検出**: `scan` の `B_public_without_xmldoc`

`Documentation/CodeGuidelines.md` の「XMLドキュメントとコメント」節が正本。

- `public` メンバーに `<summary>` があるか
- 引数・戻り値・例外の記述があるか
- **説明が型名の言い換えになっていないか**（`/// <summary> シーンをロードする </summary>` が
  `LoadScene` に付いているだけ、のような無情報な記述）

### B8 文字コード・改行・`.meta`

**検出**: `scan` の `B_missing_bom` / `B_line_ending`

- `.cs` が UTF-8 BOM 付きか（`Documentation/CONTRIBUTING.md` §3）。
  **BOM が無くてもコンパイルは通るため、検索しない限り気づけない**
- リポジトリ側の改行コードがLFか。**ワーキングツリーを見ても判定できない。**
  `core.autocrlf=true` 前提でWindows側はCRLFになるため、
  `git ls-files --eol` でindexを見る必要がある
- `.cs` と `.meta` が対で揃っているか

### B9 コード内 `TODO` の棚卸し

**検出**: `rg -n "TODO|FIXME|HACK" Assets/SymphonyFrameWork -g '*.cs'`

`TODO` は「後で直す」という意思表示だが、**書いた時点で追随先が決まっておらず、検出手段も無ければ棚卸しされずに残り続ける。** 監査のたびに全件を出し、次のどれかへ分類する。

- **Issue 番号付き**（`// TODO(#161):` 形式）— 対応する Issue が open か確認する。**closed なのに TODO が残っていれば、対応漏れか消し忘れのどちらかで、必ずどちらかの指摘になる**
- **番号なし**（`// TODO:`）— 追随用の Issue が立っていない。Issue 化するか、その場で直せる規模なら実装フローへ載せる
- **記述が古い** — 参照しているファイル・型・メソッドが既に存在しない。削除する

**指摘の粒度は TODO 1件につき1行**にし、放置年数ではなく「今の設計と食い違っているか」で優先度を付ける。

**新しく `TODO` を書く側の規則も同時に見る。** 規約違反を承知で残す場合は、何を・なぜ保留したかと、揃える対象を書く。「あとで直す」だけの TODO は、次の監査で意図が復元できないため指摘対象にする。

---

## C群 — 当てはまらないので外した観点

RuntimeAudit にあるが、このフレームワークでは監査しない。**外した理由をレポートにも書く。**

| 元の観点 | 外す理由 | 代替 |
| --- | --- | --- |
| 07 ライブラリ未活用 | フレームワークは依存を増やせない | B群の考え方で反転させた観点07 |
| 08 緊密結合とレイヤー違反（7レイヤー） | ゲーム側の `1.Domain`〜`6.Composition` 構成が前提 | 観点08（アセンブリ境界） |
| `FindObjectsByType` の棚卸し | パッケージ内に該当なし | — |
| 15 GRASP（`*Controller` の分類） | ゲーム固有の命名が前提 | 観点15（クラス種別と責務の一致） |
