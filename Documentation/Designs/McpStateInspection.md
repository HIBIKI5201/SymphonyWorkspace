# McpStateInspection

## 目的

MCP（uLoopMCP の `execute-dynamic-code`）や自動化スクリプトから、Symphony の各サブシステムが今どういう状態かを**列挙**できるようにする。

現状、公開されているのは点検索だけで、列挙する手段が無い。

| サブシステム | 既存の公開API | 列挙 |
| --- | --- | --- |
| Save Data | `SaveDataRegistry.GetEntries()` | **可能** |
| Service Locate | `IsExistInstance<T>()`、`TryGetInstance<T>(out)` | 不可（型を知っている必要がある） |
| Scene Load | `IsExist(name)`、`TryGetState(name, out)` | 不可（シーン名を知っている必要がある） |
| Pause | `Pause` プロパティのみ | 不可 |

そのため Editor Window 4枚（`ServiceLocatorWindow`・`SceneLoaderWindow`・`PauseWindow`・`SaveDataRegistryWindow`）は `GetField(..., BindingFlags.Static | BindingFlags.NonPublic)` でプライベートフィールドを覗いている。エージェントには同じ手段しか無く、「何が登録されているか」を調べるだけでリフレクションを書くことになる。

## この設計は暫定である

`Documentation/Designs/ArchitectureRevision.md` の Phase 3 は、この領域を作り直す。`ServiceLocateData` は `ServiceLocateRegistry` + `ServiceRegistrationEntity` へ分割され、Editor Window の状態取得は ViewModel と ReactiveProperty の購読に置き換わり、Adaptor の `Query` が `Info` と `Dto` を生成するようになる。

したがって本機能は **Phase 3 の Query/Info が入るまでの橋渡し**と位置づけ、次の方針で最小限にとどめる。

- 型付きの列挙構造を新設しない。JSON 文字列だけを返す
- **Editor Window 4枚のリフレクションは今回触らない**（Phase 3 でまとめて消える）
- XML ドキュメントに暫定である旨と置き換え先を明記する

## 公開API

Editor アセンブリに `public static` クラスを1つ置き、**サブシステムごとに別メソッド**とする。

```csharp
public static string GetServiceLocatorJson();
public static string GetSceneLoaderJson();
public static string GetSaveDataJson();
public static string GetPauseJson();
```

メソッド名に `Json` を含めるのは、戻り値の形式が呼び出し側の扱いを決めるため（`CodeGuidelines.md` の「単位を持つ値は名前に単位を含める」と同じ考え方）。

`public` にする根拠: MCP が生成するコードは uLoop 側のアセンブリでコンパイルされるため、`internal` では到達できない。到達させるには外部ツールのアセンブリ名を `InternalsVisibleTo` へ書くことになり、フレームワークが外部ツールの実装詳細へ依存する。Editor アセンブリは Player ビルドに含まれないため、`public` にしてもランタイムの公開API面は増えない。既存の Editor 型（`SymphonyAdministrator`、`SymphonyConfigManager`、各 Drawer）もすべて `public` であり、慣行とも一致する。

### 型名

**`SymphonyMcpTools`。**

`Tools` は当初 `CodeGuidelines.md` の役割サフィックス一覧に無かったため、「Editor 専用の診断・操作ツール群」を表すサフィックスとして追加済み。`Utility` は「特定サブシステムに紐づかない汎用の再利用可能なもの」という定義であり、診断ツール群という性質を表せていないため採らなかった。

名前に `Mcp` を含めることについて: 本来この API は MCP 専用ではなく、CI や独自 Editor スクリプトからも使える「状態の JSON ダンプ」である。将来 uLoopMCP 以外へ乗り換えると名前が実態とずれる。それでも、エージェントが `AGENTS.md` を読んで最初に探す語が `MCP` である以上、発見しやすさの利得が上回ると判断する。

## ファイル構成

| パス | 名前空間 | 内容 |
| --- | --- | --- |
| `Editor/Debug/SymphonyMcpTools.cs`（新規） | `SymphonyFrameWork.Editor.Debugger` | 上記4メソッド。既存の `Editor/Debug/SymphonyDebugLogFileWriter.cs` と同じフォルダ・名前空間へ置く |

`Editor/Debug/` は既に診断系の置き場になっているため、新しいフォルダは作らない。

### Runtime への変更（`internal` アクセサの追加）

| パス | 追加するもの |
| --- | --- |
| `Runtime/System/ServiceLocator/ServiceLocator.cs` | 初期化状態と登録テーブルを読むための get 専用 `internal static` アクセサ |
| `Runtime/System/SceneLoader/SceneLoader.cs` | 同上（追跡中シーンと Active Scene） |
| `Runtime/System/PauseManager.cs` | 初期化状態と `IPausable` 購読数を読む get 専用 `internal static` アクセサ |
| `Runtime/System/SaveSystem/SaveDataRegistry.cs` | 初期化状態を読む get 専用 `internal static` アクセサ（エントリ列挙は既存の `public GetEntries()` を使う） |

いずれも公開APIは増えない。setter と副作用を持たせない。

## 依存方向

### 前提の訂正: `InternalsVisibleTo` だけでは届かない

当初この設計書は「`InternalsVisibleTo` があるので Editor から Runtime の内部状態へ直接アクセスできる」としていたが、**誤りだった。**

`ServiceLocateData` や `SceneLoadData` という**型**は `internal` だが、それを保持する**フィールドは `private static`** である。

| Facade | 状態フィールド | 可視性 |
| --- | --- | --- |
| `ServiceLocator` | `_data` | `private static` |
| `SceneLoader` | `_data` | `private static` |
| `PauseManager` | `_pause`、`_isInitialized`、`_pauseEventDictionary` | `private static` |
| `SaveDataRegistry` | `_loaderResolver` | `private static` |

既存の `internal` 読み取りアクセサも無い。既存の Editor Window 4枚がリフレクションを使っているのは、まさにこの理由による。

### 採る方針: Runtime へ最小限の `internal` 読み取りアクセサを追加する

`DesignPhilosophy.md` の「公開範囲」は、この用途を明示的に認めている。

> Editor拡張やCompositionのためだけに存在するメンバーは、Facade上にあっても`public`にしない。ローダーやManagerなど**内部実装を取り出すアクセサ**、状態のリセット、初期化・注入のフックは`internal`にし、`[assembly: InternalsVisibleTo("SymphonyFrameWork.Editor")]` など明示的なアセンブリ間許可で参照する。

したがって、各 Facade へ **get のみの `internal static` アクセサ**を最小限追加する。リフレクションは使わない。

- **get 専用。** setter を作らない。Editor から状態を書き換える経路は作らない
- **副作用を持たない。** 遅延初期化や生成を起こさない
- **公開APIは1つも増えない。** すべて `internal`
- `SaveDataRegistry.GetEntries()` は既に `public` なので、Save Data は初期化判定のアクセサだけで足りる

```text
SymphonyMcpTools（Editor / View層のDebugger）
        │ InternalsVisibleTo 経由で internal アクセサを参照
        v
ServiceLocator, SceneLoader, PauseManager, SaveDataRegistry（Runtime）
        │ private フィールド
        v
ServiceLocateData, SceneLoadData 等（Runtime / internal）
```

この追加も Phase 3 で Adaptor の `Query` / `Info` に置き換わる暫定措置である。

```text
SymphonyMcpTools（Editor / View層のDebugger）
        │ InternalsVisibleTo 経由の直接参照
        v
ServiceLocateData, SceneLoadData, PauseManager, SaveDataRegistry（Runtime / internal）
```

`DesignPhilosophy.md` の「Debugger は View に属し、Editor または Development ビルドでだけ診断情報と操作を提供する」に合致する。**状態を変更するメソッドは置かない。読み取りのみ。**

Runtime 側へ新しい公開APIも `internal` メンバーも追加しない。既存の `internal` をそのまま読む。

## JSON の生成

`JsonUtility` は Dictionary とトップレベル配列を扱えないため使えない。`Newtonsoft.Json`（`com.unity.nuget.newtonsoft-json`）を使う。`package.json` の `dependencies` に既にあり、`NewtonsoftSaveDataLoader` が Runtime で使用している。

**実装前に、Editor asmdef から Newtonsoft が解決できることを確認すること。** 解決できない場合は Editor asmdef へ参照を追加する。

各メソッドの JSON には、最低限次を含める。

| メソッド | 含める内容 |
| --- | --- |
| `GetServiceLocatorJson` | 初期化状態、登録件数、各登録の型名・`LocateType`・インスタンス名（Component ならその GameObject 名） |
| `GetSceneLoaderJson` | 初期化状態、追跡中シーンの名前・`SceneLoadState`・優先度、Active Scene 名 |
| `GetSaveDataJson` | 登録エントリの型名・`SaveDate`・ロード済みかどうか。**`SaveDataContent` の中身は含めない**（セーブデータに機微な値が入りうるため） |
| `GetPauseJson` | 初期化状態、現在のポーズ状態、`IPausable` の購読件数 |

## エラー処理

**例外を投げない。** MCP から呼ばれる前提なので、Play Mode 外や未初期化時に例外を投げると呼び出し側が扱いにくい。

- 未初期化のサブシステムは、例外ではなく `"initialized": false` を含む JSON を返す
- 内部状態の読み取りに失敗した場合も、`"error"` フィールドにメッセージを入れた JSON を返す
- どのメソッドも必ず有効な JSON 文字列を返す

## 影響範囲

**破壊的変更ではない。** ランタイムの公開API、シリアライズ形式、既存の挙動はいずれも変わらない。Editor アセンブリに `public static` クラスが1つ増えるだけ。

Editor Window 4枚のリフレクションは残る（Phase 3 で解消）。

## テストの置き場と種別

`Assets/Tests/Editor/`（ワークスペース側）に EditMode テストを追加する。検証内容:

- 4メソッドすべてが、未初期化状態（Play Mode 外）でも例外を投げずに**パース可能な JSON** を返すこと
- 未初期化時の JSON に `"initialized": false` が含まれること

Play Mode での実データ検証は手動確認に回す。EditMode ではサブシステムが初期化されないため。

## 動作確認手順

1. `uloop-compile` がエラー0・警告0
2. EditMode テストが成功
3. Play Mode に入り、サービスを2件登録・シーンを1件追加ロードした状態で、`uloop-execute-dynamic-code` から4メソッドを呼ぶ。返った文字列が JSON としてパースでき、登録した内容が反映されていること
4. Play Mode を抜けた状態で同じ4メソッドを呼び、例外なく `"initialized": false` の JSON が返ること
5. `GetSaveDataJson` の出力に `SaveDataContent` の中身が含まれていないこと

## ドキュメントへの記載

パッケージ側の `AGENTS.md` に「エージェントが状態を確認する方法」の節を追加する。パッケージ利用者も uLoopMCP + Claude Code で自分のゲームをデバッグするため、利用者向け情報として実用性がある。

暫定 API である旨と、Phase 3 で置き換わる予定も併記する。

## バージョン判断

**マイナー。** 後方互換な公開API追加（Editor アセンブリ）であり、既存のシグネチャと挙動を変えない。
