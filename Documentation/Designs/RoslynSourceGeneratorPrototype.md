# Roslyn Source Generator 導入プロトタイプ（Round 129-1）

Issue: [#129](https://github.com/HIBIKI5201/SymphonyFramework/issues/129)

ロードマップ: [Documentation/IssueImplementationRoadmap.md](../IssueImplementationRoadmap.md) Phase 2 / Round 129-1

## 目的

`ServiceInjector.TryAutoInject`（[ServiceInjector.cs](../../Assets/SymphonyFrameWork/Runtime/Service/ServiceLocator/ServiceInjector.cs)）は、対象型が実装する`IInjectable<T...>`をRuntimeで`GetInterfaces()`と`MakeGenericMethod().Invoke()`により反射解決している。Roslynによる自動生成へ置き換える計画（#129 → #109 → #168）を進める前に、**Unity 6 + UPMパッケージという配布形態でRoslyn Source Generatorが実際に機能するか**を、対象ロジックに触れない最小プロトタイプで検証する。

現状の`ServiceInjector`を書き換えるのは Round 129-2 の範囲であり、本 Round では行わない。本 Round が確定すべきは次の3点である。

1. Generator用の別アセンブリ（.NET Standard 2.0、`Microsoft.CodeAnalysis.CSharp`参照）を、`dotnet build`/`msbuild`を使わずに用意する手段があるか、無ければ`dotnet`を使う以外の選択肢が無いことの確認
2. Generator DLLをUPMパッケージ（git submodule配布）へ含めたとき、**手作業のAsset Label設定を利用者に要求せず**、`SymphonyFrameWork`アセンブリ（Runtime）にだけ生成コードが適用されること
3. 生成コードがRuntimeから利用でき、Playerビルドへ Generator 本体（DLL・依存パッケージ）が含まれないこと

## AGENTS.md §7 との関係（要確認事項）

[AGENTS.md](../../AGENTS.md) §7は「`dotnet build`/`msbuild`/`csc`を使う。コンパイル可否の判断はUnity（uloop）に委ねます」を禁止事項として挙げている。この規則は、**Unityが管理するC#（`Assets/`配下のRuntime/Editorスクリプト）のコンパイル可否を、Unity以外の手段で判定しない**という意図であり、根拠は[CONTRIBUTING.md](../CONTRIBUTING.md)の検証方針にある。

Roslyn Source Generatorは、Unityのスクリプトコンパイラではなく**Roslynコンパイラの拡張機能**であり、Unity 6公式手順（[Create a source generator](https://docs.unity3d.com/6000.3/Documentation/Manual/create-source-generator.html)）でも「.NET Standard 2.0のクラスライブラリをUnity外でビルドし、DLLをAssetsへ配置する」という手順が前提になっている。Unity自身はGenerator DLLをソースからビルドする機能を持たない。

**そのため本Roundは、Generator本体（`Assets/SymphonyFrameWork/Generators/Source~/`に置く別プロジェクト）のビルドに限り`dotnet build`を使う。** これはUnityプロジェクト本体（`Assets/`配下のゲームコード）のコンパイル可否判定を代替するものではなく、Unityの外側にある独立した配布物（Generator DLL）を作る手段が他に無いために生じる例外である。この解釈と例外の適用範囲は、**実装へ進む前にユーザーの確認を得る。**

### 代替案（承認が得られない場合）

自律実行（スケジュールタスク）ではユーザー確認を得られないため、本Roundは実装へ進まず、承認待ちのまま以下の代替案を選択肢として残す。次回ユーザーが確認できるタイミングで、いずれかを選んでから実装へ進む。

| 案 | 内容 | 長所 | 短所 |
| --- | --- | --- | --- |
| A. CI外部ビルド | `Assets/SymphonyFrameWork/Generators/Source~/`のGeneratorプロジェクトを、ワークスペース/エージェントのローカル実行では一切ビルドせず、`HIBIKI5201/SymphonyFramework`側のGitHub Actions（ホストランナー）で`dotnet build`する。ビルド済みDLLを成果物としてPRへ含める運用とし、DOTweenのような外部プラグインDLLの扱いに準じる | このワークスペース内で`dotnet`/`msbuild`/`csc`を一切実行しないため、AGENTS.md §7の文言に抵触しない。既存の`deploy-docs.yml`と同様にCIへ委譲できる | 新規GitHub Actionsワークフローの追加・secrets要否の検討が別途必要。DLL更新のたびにPRとCI実行が要る運用コストが増える |
| B. 本Round見送り | #129を一旦保留し、`ServiceInjector.TryAutoInject`の反射実装を維持する。ロードマップの#129〜#168を「Generator前提」から「反射実装の改善」へ縮小するか、AGENTS.md §7自体の改定をユーザーへ別途諮る | AGENTS.md §7に一切触れない。判断を先送りしない | #109・#168が反射実装の上に積み上がり、後日Generatorへ移行する際の手戻りが大きい（ロードマップの前提そのものが崩れる） |
| C. AGENTS.md §7の明文改定 | 「Unity外の独立した配布物（Source~配下、Unityの非インポート対象）のビルドに限り例外」という一文をAGENTS.md §7へユーザー承認のもと追記し、以後のRoundで迷わず適用できるようにする | 一度の意思決定で129-1以降のRoundが滞らない | AGENTS.md本文の変更はこの設計書の範囲を超え、ユーザーの明示的な合意が必須 |

自律実行では**A案が最もAGENTS.md §7の文言と整合する**候補だが、新規CIワークフローの追加はそれ自体が「共有システムに影響する変更」であり、ユーザー確認なしに着手しない。次回人が確認できるタイミングで、A/B/Cのいずれかを選択してから129-1の実装（ワーカー呼び出し）へ進む。

## 公開API

公開APIは追加・変更しない。本Roundで追加する型・ファイルはすべて次のいずれかである。

- Generator本体のソース（Unity非対象、`~`により非インポート）
- ビルド済みGenerator DLLと、それを検証するための最小限のテスト対象コード（`internal`、プロトタイプ専用）

プロトタイプの生成コードは実運用APIではないため、Round終了後に129-2で置き換える前提で`internal`かつ`SymphonyFrameWork.Generators.Prototype`名前空間へ隔離し、他のRuntimeコードから参照しない。

## ファイル構成

| パス | 内容 |
| --- | --- |
| `Assets/SymphonyFrameWork/Generators/Source~/SymphonyFrameWork.Generators/SymphonyFrameWork.Generators.csproj` | netstandard2.0、`Microsoft.CodeAnalysis.CSharp` 4.3.0 参照。Unity非対象（`Source~`） |
| `Assets/SymphonyFrameWork/Generators/Source~/SymphonyFrameWork.Generators/PrototypeMarkerGenerator.cs` | `IIncrementalGenerator`実装。`[GeneratePrototypeMarker]`が付いた`partial class`へ、固定文字列を返す`partial`メソッド1つだけを生成する最小実装 |
| `Assets/SymphonyFrameWork/Generators/Source~/SymphonyFrameWork.Generators/README.md` | ビルド手順（`dotnet build -c Release`）、DLLの配置先、更新時の手順 |
| `Assets/SymphonyFrameWork/Generators/SymphonyFrameWork.Generators.dll`（+`.meta`） | ビルド済みGenerator DLL。`.meta`に`RoslynAnalyzer`ラベルを事前設定してコミットする |
| `Assets/SymphonyFrameWork/Runtime/Internal/Generated/GeneratePrototypeMarkerAttribute.cs` | Generatorが探す`[GeneratePrototypeMarker]`属性の定義。`internal` |
| `Assets/SymphonyFrameWork/Tests/Editor/RoslynGeneratorPrototypeTests.cs` | 生成コードが実際に存在し、Runtimeから呼べることを検証するEditModeテスト |
| `Assets/SymphonyFrameWork/Documentation~/EditorTools.md` | 「Generatorの一覧」に本プロトタイプの検証結果を短く追記（採用可否が決まる129-3まで暫定表記） |

新しいasmdefは作らない。Generator DLLはどのasmdefにも属さない`Plugin`（`PluginImporter`）として扱う。

## 依存方向とスコープの契約

- Generator DLLは`SymphonyFrameWork.asmdef`（Runtime）の`precompiledReferences`へ明示的に追加し、`overrideReferences: true`にする。これにより生成対象を`SymphonyFrameWork`アセンブリのコンパイル時だけへ限定し、ホスト側`Assets/Scripts/`や他パッケージのコンパイルへ波及させない。
- `SymphonyFrameWork.Core`、`SymphonyFrameWork.Editor`、テストアセンブリへは参照を追加しない。スコープを絞れることの確認が本Roundの目的の一つであるため。
- Generator DLLの`.meta`（`PluginImporter`）は、`Any Platform`を無効化し、Editor/Standaloneを含む全プラットフォームの実行時ロードを無効にする。Player実行時に本体を含めない。

## エラー処理

Generator自体がクラッシュした場合はUnityのコンパイルエラーとして表面化する（Roslyn Generatorの例外はコンパイラ診断へ変換される）。プロトタイプの生成ロジックは分岐を持たず、対象属性が無ければ何も生成しない。異常系の診断（重複、循環依存など）は129-2以降で設計する。

## 影響範囲

- 既存の`ServiceInjector`、`IInjectable<T...>`の挙動は変更しない。
- 既存の公開API、シリアライズ形式への影響は無い。
- Generator DLLと`Microsoft.CodeAnalysis.CSharp`への依存はEditor限定であり、Playerビルド、IL2CPP/AOTには影響しない（本Roundの検証項目そのもの）。
- プロトタイプの型・属性は129-2で削除する前提であり、公開APIとして扱わない。

## テストの置き場と種別

`Assets/SymphonyFrameWork/Tests/Editor/RoslynGeneratorPrototypeTests.cs`へEditModeテストを追加する。

| テスト | 検証内容 | 書き方 |
| --- | --- | --- |
| `GeneratePrototypeMarkerAttribute_AppliedType_HasGeneratedMethod` | `[GeneratePrototypeMarker]`を付けた`internal partial class`に、Generatorが生成した`partial`メソッドが実在し、固定文字列を返す | 属性を付けたテスト専用の`internal partial class`をテストアセンブリ内に定義し、コンパイル時点で生成メソッドを直接呼び出して戻り値を`Assert.AreEqual`で比較する。反射は使わない（生成が失敗していればこのテストファイル自体がコンパイルエラーになるため、テストの成否そのものがGeneratorの動作を証明する） |
| `GeneratedAssembly_DoesNotReferenceEditorOnlyAnalyzer` | `SymphonyFrameWork.dll`のビルド成果物にGenerator DLL自体への実行時参照が含まれない | `AssetImporter.GetAtPath`で`SymphonyFrameWork.Generators.dll`の`PluginImporter`を取得し、`GetCompatibleWithAnyPlatform()`が`false`であることと、Editor/Standalone実行時互換設定が全て`false`であることを確認する |

Editor UIの操作を伴わないため、GUI起因の人手確認は無い。ただし次はテストで検証できないため「動作確認手順」の人手確認へ回す。

- Generator DLLの`.meta`に`RoslynAnalyzer`ラベルが**追加の手作業無しに**適用されること自体（clean cloneでの確認）
- Playerビルド後の出力にGenerator関連ファイルが含まれないこと（ビルドサイズ・Player Data Managerでの確認）

## 動作確認手順

### 自動確認

1. `Assets/SymphonyFrameWork/Generators/Source~/SymphonyFrameWork.Generators`で`dotnet build -c Release`を実行し、`SymphonyFrameWork.Generators.dll`を`Assets/SymphonyFrameWork/Generators/`へ配置する。
2. `python scripts/verify_round.py`でUnityコンパイル、Console、EditMode、PlayMode 2往復を確認する。
3. コンパイルがエラー0・警告0、EditModeが全数成功することを確認する。

### 人が操作して確認する項目

1. clean clone相当（`Library/`を削除した再インポート、または新規clone）でUnityを開き、`RoslynAnalyzer`ラベルを手で設定し直さなくても生成コードが機能することを確認する。
2. `File > Build Settings`でStandaloneビルドを実行し、出力に`SymphonyFrameWork.Generators.dll`または`Microsoft.CodeAnalysis.CSharp.dll`相当が含まれないことを確認する。
3. Generatorのソース（`PrototypeMarkerGenerator.cs`）を変更してDLLを再ビルド・再配置した際、Unity側で追加の手順（DLLの再インポート以外）が不要なことを確認する。

## バージョン判断

利用者から見える公開APIの追加・変更が無いプロトタイプ検証であるため、**パッケージバージョンは更新しない**（`--no-tests-reason`ではなく通常テストを実装するが、公開契約は変わらないため）。129-3で採用が確定した時点で、129-1〜129-3をまとめた変更としてマイナー更新を検討する。

## この Round で触るバージョン関連ファイル

無し。`package.json`、`CHANGELOG.md`、`README.md`は129-3で採用可否が確定してからまとめて更新する。プロトタイプ段階のファイルはCHANGELOGに書かない。

## Round分割の確認（rounds.md の検索結果）

削除・改名するメンバーは無い（新規追加のみ）ため、参照元の機械的検索は該当なし。ワークスペース側`Documentation/`の記述更新は次の検索で確認した。

```text
rg -n "README\.md|Documentation~/|EditorTools\.md|AgentUsage\.md" Documentation/ AGENTS.md
```

本Roundで触れるのは`Assets/SymphonyFrameWork/Documentation~/EditorTools.md`の「Generatorの一覧」暫定追記のみで、`AGENTS.md`・`CONTRIBUTING.md`側の記述変更は無い（Generator配布方式が129-3で確定してから、必要な節を追加・修正する）。
