# Open Issue実装ロードマップ

この文書は、2026-08-24時点で`HIBIKI5201/SymphonyFramework`に残るOpen Issue 6件を、依存関係と実装リスクに基づいて並べた実装順序です。日付による期限ではなく、単独で検証・リリースできるRoundを進捗単位にします。

## 推奨順序

| 順位 | Issue | 規模 | 先に行う理由 |
| --- | --- | --- | --- |
| 1 | #113 Packagerの出力先をExplorerで開く | 1 Round / 低 | 独立したEditor改善で、既存パイプラインの完了結果を明示する小さな変更。後続の基盤変更と競合しない |
| 2 | #129 Roslynによる自動生成 | 3 Round / 高 | #109のコンストラクタ注入と#168の型別Pauseカテゴリー生成に共通する基盤。先に境界を決めないと反射実装と生成実装が二重化する |
| 3 | #109 Injectorのコンストラクタ注入 | 2 Round / 中〜高 | #129で決めた生成方式と診断方式を最初の利用機能として完成させる |
| 4 | #168 IPausableの型別カテゴリー | 3 Round / 高 | カテゴリーinterface生成を#129へ載せ、既存の単一Pauseを互換カテゴリーとして維持できる |
| 5 | #110 Scene Block | 4 Round / 高 | 既存SceneLoaderの並行ロードを再利用できるが、DAG検証、失敗、キャンセル、進捗を独立して設計する必要がある |
| 6 | #115 独自SymphonyTask型 | 調査1 + 実装4以上 / 最高 | 要求が「Awaitableだけでは不足する用途」の段階で、契約が未確定。全非同期APIへ波及し得るため最後に独立RFCとして扱う |

推奨する直列経路は次のとおりです。

```text
#113
  ↓
#129 ──→ #109 ──→ #168
                    ↓
                  #110
                    ↓
                  #115
```

#110は#129〜#168とコード上の直接依存がないため、優先度を上げたい場合は#113の次へ移動できます。ただし、同時に複数Issueの差分を作業ツリーへ載せず、Issue専用ブランチとRoundを1つずつ閉じます。

## Phase 1: 小さなEditor改善

### #113 Packagerの完了ログから出力先をExplorerで開く

現状の`AssetStoreToolsPackagePipelineRunner.Export`は、出力完了時に`context.ExportLocalPath`を通常ログとして記録します。ログ文字列そのものをクリック可能にするより、完了通知の出力先を値として保持し、Editor UIから明示的に開く構造を推奨します。

| Round | 内容 | 完了条件 |
| --- | --- | --- |
| 113-1 | Export結果に出力先を持たせ、完了UIまたはログ操作から`EditorUtility.RevealInFinder`相当を呼ぶ。パス正規化と存在しない出力先のテスト、Packager文書とEditorTools索引を更新する | WindowsでExplorerが開く。macOS向けAPIをハードコードせずUnityのEditor APIへ委譲する。既存Exportテストが全数成功する |

設計時には「Consoleのログ本文をクリック可能にする」のか「Packager Windowへ完了操作を出す」のかを先に決めます。Unity Consoleの内部型への反射は採用しません。

## Phase 2: コード生成と依存注入

### #129 Roslynによる自動生成

現在の主対象は`ServiceInjector.TryAutoInject`です。実装は対象型のinterface列挙、公開`Inject`メソッドの探索、`MakeGenericMethod().Invoke`をRuntimeで行っています。Unity 6はSource Generatorをサポートしますが、Generator DLLを.NET Standard 2.0で構築し、`RoslynAnalyzer`ラベルを設定する配布形態が必要です（[Unity 6公式マニュアル](https://docs.unity3d.com/ja/current/Manual/create-source-generator.html)）。

| Round | 内容 | 完了条件 |
| --- | --- | --- |
| 129-1 | Generator DLLの配置、asmdefへの適用範囲、Unity/UPM配布、生成物のデバッグ方法を検証する最小プロトタイプ。固定入力から決定的なコードを生成し、PlayerビルドへGenerator本体を含めない | clean clone相当でUnityコンパイルが成功し、生成コードがRuntimeから利用できる。導入手順が手作業のAsset Label設定へ依存しない |
| 129-2 | `IInjectable<T...>`実装型ごとの注入ディスパッチを生成し、`ServiceInjector`のRuntime反射を置き換える。未対応アリティ、重複interface、未登録サービスをcompile診断または既存例外へ割り当てる | 既存注入テスト、Sceneロード時の自動注入、IL2CPP/AOTを想定したコードパスが反射なしで通る |
| 129-3 | 生成時間、Runtime割り当て、初回注入時間を現行と比較し、適用範囲を確定する。EnumGeneratorやSaveData型カタログなど、別責務は測定結果に基づいて個別Issueへ分離する | 「反射処理をすべて生成へ移す」という無制限な範囲を残さず、採用対象と非対象が文書化される |

GeneratorのNuGet依存をRuntimeパッケージへ混ぜません。Unity公式手順上、Generatorは通常コードとは異なるPlugin/Analyzerとして配布されるため、package構成と`.meta`をRound 129-1で先に確定します。

### #109 Injectorのコンストラクタ注入

任意の`T`へRuntime反射でコンストラクタを探す方式は、#129の目的とAOT適合性に反します。#129で確立したGeneratorが、利用側型の選択されたコンストラクタと必要サービスをcompile時に確定する方式を第一候補にします。

| Round | 内容 | 完了条件 |
| --- | --- | --- |
| 109-1 | 対象型の指定方法、コンストラクタ選択規則、複数候補・循環依存・未登録・非公開コンストラクタの診断契約を定義し、生成factoryを実装する | 正常系だけでなく曖昧なコンストラクタと循環依存が決定的に拒否される。利用側型ごとの生成コードをテストできる |
| 109-2 | 公開入口、XML文書、Service Locatorモジュール文書、Sampleを追加し、既存`IInjectable`方式との使い分けと移行方針を示す | 既存注入APIを壊さず、生成可能な型だけをコンストラクタ注入できる。Player環境の動作確認手順が残る |

API名はIssue本文の`Constructor<T>()`を確定事項にしません。生成コードが属するアセンブリ境界を検証してから、`ServiceInjector.Create<T>()`、生成factory、属性付きconstructorなどの候補を比較します。

## Phase 3: 型別Pauseカテゴリー

### #168 IPausableに多態性を持たせる

現状は`PauseStateEntity`がboolを1つ持ち、`PausableRegistry`も`IPausable`を単一辞書で管理します。待機API、Tween、Administrator、MCP診断も同じグローバル状態を参照するため、Dictionaryを1つ置き換えるだけでは完了しません。

| Round | 内容 | 完了条件 |
| --- | --- | --- |
| 168-1 | 型キーのPauseカテゴリー、状態Entity、購読Registry、Queryを内部層へ追加する。既存`IPausable`と`PauseManager.Pause`は既定カテゴリーへ割り当てる | 既存利用側コードの挙動が変わらず、複数カテゴリーの状態と購読が相互に干渉しない |
| 168-2 | `PauseManager`の型引数付き操作・待機APIと、SettingsProviderで定義したカテゴリーinterfaceの生成を追加する。生成は#129の基盤を再利用する | カテゴリー名の重複、無効な識別子、削除・改名時の扱いが診断され、Domain Reloadなしでも生成状態が残らない |
| 168-3 | Administrator、PauseViewModel、MCP JSON、Debug HUD、Sample、Pause Manager文書をカテゴリー一覧へ対応させる | 開いたままのEditor UIがカテゴリー追加・状態変更へ追従し、既定カテゴリーと追加カテゴリーを個別に操作・診断できる |

既存APIを削除せず「既定カテゴリー」として残せるならマイナー更新です。既存`IPausable`や`PauseManager.Pause`を置換する案はメジャー更新として別途合意を取ります。

## Phase 4: Scene Block

### #110 複数シーンの依存関係を管理するScene Block

既存`SceneLoadService.LoadScenes`は複数Requestを同時開始できます。Scene Blockではこれを直接呼ぶ前に、依存グラフを検証し、ロード可能になった層だけを並行実行するPlanner/Schedulerが必要です。

| Round | 内容 | 完了条件 |
| --- | --- | --- |
| 110-1 | シーン識別子と依存辺を表す不変なDomainモデル、重複、自己依存、循環、欠落参照を検出する純粋なDAG Plannerを実装する | Unity APIなしのEditModeテストで、トポロジカル層と全異常系を固定する |
| 110-2 | `SceneBlock` ScriptableObjectとEditor検証UIを追加し、AssetからDomainモデルへ変換するInfrastructureを実装する | Assetの並び順に依存せず同じ実行計画になり、循環などをロード開始前にInspector/ログで確認できる |
| 110-3 | 層ごとの並行ロード、進捗集約、キャンセル、部分失敗時の停止・巻き戻し方針をSceneLoadServiceへ統合する | `A→B,C`、`B→D,E`の例が意図した層順で動き、失敗後にRegistryへLoading状態が残らない |
| 110-4 | 公開API、Info/Dto、Administrator、MCP診断、Sample、Scene Loader文書を追加する | 公開APIだけでBlockをロードでき、Play Mode 2往復とSampleで再現可能な確認手順がある |

初版では条件分岐、重み付き辺、動的グラフ変更を扱いません。依存DAGとロード順の契約を固めてから拡張Issueへ分離します。

## Phase 5: 非同期基盤の再検討

### #115 独自SymphonyTask型

旧`SymphonyTask`は2.6.0で非推奨化され、`SymphonyAwaitable`への移行後に削除済みです。同じ名前を再導入すると「旧Utilityの復活」なのか「新しいtask-like型」なのか判別できないため、名前を先に確定しません。

| Round | 内容 | 完了条件 |
| --- | --- | --- |
| 115-0 | Awaitableで不足する具体的ユースケース、割り当て、複数await、保存、キャンセル、例外、PlayerLoop、AOTの要求をRFCとbenchmarkで確定する | 少なくとも2つの実利用ケースと、既存`Awaitable`/`Task`/`SymphonyAwaitable`では満たせない測定済みの差がある |
| 115-1 | awaiter、完了source、結果型、例外・キャンセル伝播の最小prototypeをCoreへ実装する | 単一await、同期完了、非同期完了、例外、キャンセルをEditModeで検証できる |
| 115-2 | PlayerLoop連携、pooling、世代token、複数await・再利用禁止などの所有権契約を実装する | Domain Reloadなしの2往復、誤再利用、二重完了、継続例外で状態が汚染されない |
| 115-3 | `Task`/`Awaitable`とのadapterと一つの内部サブシステムでpilot移行を行い、性能と可読性を比較する | 既存APIを一括置換せず、移行価値とコストを数値で判断できる |
| 115-4以降 | 公開API化と段階的移行。既存非同期APIの戻り値を変える場合は互換APIと非推奨期間を設ける | 破壊的変更を含む場合はメジャー版の移行表、Sample、全モジュール文書が揃う |

Round 115-0で明確な優位性を確認できなければ実装を止め、`SymphonyAwaitable`の不足API追加へIssueを縮小します。

## 各Issue共通の完了条件

1. Issue専用の`feature/*`または`fix/*`ブランチを`develop`から作る。
2. Roundごとに設計書を提示し、合意後に実装する。
3. 実装中は`release_round.py checkpoint`で未検証の途中成果を小刻みにcommit/pushする。
4. Round完成時は`verify_round.py`、生成文書同期、`release_round.py preflight`を通す。
5. PRを`develop`へマージし、Issueまたは該当Roundを閉じ、設計書へ実施レポートを追記する。
6. `develop`から`main`へのリリースは人が行う。

## 見直し条件

次のいずれかが起きたら、この順序とRound分割を更新します。

- 新しいbug Issueが追加され、利用側のデータ破損、ビルド不能、契約違反を起こす。
- #129のprototypeでUnity/UPM配布時にGeneratorを安定適用できない。
- #115のRFCで、#110または#168より先に解決すべき非同期契約の欠陥が実測される。
- 1 Roundが20ファイルを大きく超え、単独レビューまたは単独リリースが成立しない。
