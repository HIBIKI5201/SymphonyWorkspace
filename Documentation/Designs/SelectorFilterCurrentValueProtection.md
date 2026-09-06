# SelectorFilterCurrentValueProtection

Issue: [#199](https://github.com/HIBIKI5201/SymphonyFramework/issues/199) 再オープン分（[P1] フィルターで現在値が除外されると、描画だけで保存値が変わる）

## 目的

`SceneNameSelectorDrawer` / `TagSelectorDrawer` は、保存済みの値がフィルター後の候補から外れているとき、Popup表示用に補正したindex（`0`）を**そのまま** `property.stringValue` へ代入している。これは「表示するために選んだ既定index」と「ユーザーが実際に選び直した結果」を区別していないためで、Inspectorを開いて何も操作しないだけで保存値が書き換わる。

`SubclassSelectorDrawer` は同じ状況を「選択が変わった場合だけ代入する」（`currentTypeIndex != selectedTypeIndex`）で回避しているが、これは元のindexが常に候補内にあるため成立する比較であり、`-1`を`0`へ補正する2つのDrawerではそのまま使えない（補正後のindexとPopupの初期選択indexが一致し、変化なしと誤判定する）。

対応するのは #199 再オープン分の[P1]1件のみ。#202側の[P1]2件は別Issueの対応であり、本設計の範囲外。

## 公開API

変更なし。`SceneNameSelectorDrawer` / `TagSelectorDrawer` はいずれも `CustomPropertyDrawer` で、公開シグネチャに影響する変更を行わない。

`SelectorFilterUtility`（`internal static`）へ次の関数を追加する。

```csharp
/// <summary>
///     保存済みの値が候補から外れている場合に使う、Popup表示用の既定indexを解決する。
/// </summary>
/// <param name="candidates"> Popupへ並べる候補。 </param>
/// <param name="currentValue"> シリアライズ済みの値。 </param>
/// <returns> 候補内に見つかった場合はそのindex、見つからない場合は0。 </returns>
internal static int ResolveDisplayIndex(string[] candidates, string currentValue)
```

## ファイル構成

- `Editor/AttributeDrawer/Internal/SelectorFilterUtility.cs`（変更）: `ResolveDisplayIndex` を追加
- `Editor/AttributeDrawer/SceneNameSelectorDrawer.cs`（変更）: `OnGUI` の index解決と代入ロジックを修正
- `Editor/AttributeDrawer/TagSelectorDrawer.cs`（変更）: 同上
- `Tests/Editor/SelectorFilterUtilityTests.cs`（変更）: `ResolveDisplayIndex` のテストを追加

`SubclassSelectorDrawer.cs` は対象外（元々 `currentTypeIndex != selectedTypeIndex` で保護済みで、今回のバグを持たない）。

## 依存方向

`SelectorFilterUtility` は既存どおり `Editor` 層のみに閉じ、Unity APIへ触れない（既存の `remarks` のとおり）。`Drawer` 側だけが `EditorGUI` を扱う。レイヤー構造・依存方向に変更はない。

## 修正方針

### 1. 表示用indexの解決を関数へ切り出す

現状:

```csharp
int index = Array.IndexOf(selectableScenes, property.stringValue);
if (index < 0) { index = 0; }
```

これを `SelectorFilterUtility.ResolveDisplayIndex(selectableScenes, property.stringValue)` へ切り出す。ロジック自体は変えず、Unity APIへ触れない関数として独立させ、テスト可能にする。

### 2. 代入をユーザー操作があった場合だけに限定する

`EditorGUI.BeginChangeCheck()` / `EditorGUI.EndChangeCheck()` で `EditorGUI.Popup` の呼び出しを囲み、`EndChangeCheck()` が `true` を返した場合だけ `property.stringValue` を書き換える。

```csharp
int displayIndex = SelectorFilterUtility.ResolveDisplayIndex(selectableScenes, property.stringValue);

EditorGUI.BeginChangeCheck();
int selectedIndex = EditorGUI.Popup(position, label.text, displayIndex, selectableScenes);
if (EditorGUI.EndChangeCheck())
{
    property.stringValue = selectableScenes[selectedIndex];
}
```

`EditorGUI.BeginChangeCheck` / `EndChangeCheck` はUnityが提供する「この区間で描画したコントロールへ実際にユーザー操作があったか」を判定する標準APIで、`GUI.changed` を区間内だけに限定して読む。表示用に補正したindexをそのままPopupへ渡しても、ユーザーが触らない限り `EndChangeCheck()` は `false` のままなので、保存値は変わらない。

`TagSelectorDrawer` も同じ形へ変更する。

### 3. 「候補外として判別できる表示」は今回の対応範囲に含めない

修正方針の提案に「現在値を候補外として判別できる表示にする」があるが、再オープンの受け入れ条件（下記）はいずれも**保存値が変わらないこと／明示的な選択で更新されること**を要求しており、表示上の区別（候補一覧に元の値を追加する、ラベルを変えるなど）までは求めていない。Popupの候補配列を変えると `SubclassSelectorDrawer` の `<null>` 相当の特別枠を新設する必要があり、今回のRoundの範囲を超える。**見送る。**

## エラー処理

既存のフィルターエラー・候補ゼロの早期returnはそのまま維持する（変更なし）。`ResolveDisplayIndex` は不変条件違反を持たない純粋関数で、`candidates` が空配列でも `0` を返す（呼び出し側は候補ゼロのとき既に早期returnしており到達しない）。

## 影響範囲

公開APIへの影響なし。シリアライズ形式への影響なし。挙動の変化は「フィルターで現在値が除外された状態でInspectorを開いただけでは値が変わらなくなる」ことのみで、これは意図した修正そのもの。

## テストの置き場と種別

`Tests/Editor/SelectorFilterUtilityTests.cs`（EditMode）に追加する。`ResolveDisplayIndex` は文字列配列を返すだけの純粋関数なので、EditModeで完全に検証できる。

- `ResolveDisplayIndex_ValueInCandidates_ReturnsItsIndex`: 候補 `["A","B","C"]`、現在値`"B"` → `1`
- `ResolveDisplayIndex_ValueNotInCandidates_ReturnsZero`: 候補 `["A","B"]`、現在値`"Z"` → `0`
- `ResolveDisplayIndex_EmptyCandidates_ReturnsZero`: 候補 `Array.Empty<string>()`、現在値`"A"` → `0`

`EditorGUI.BeginChangeCheck` / `Popup` を含む `OnGUI` 側の分岐（実際にPopupが変更を検知したときだけ代入する経路）はUnity APIの `GUI.changed` に依存するため、EditModeテストからは検証できない。既存の `SceneNameSelectorAttributeTests.cs` / `TagSelectorAttributeTests.cs` と同様、Drawerの `OnGUI` 自体は自動検証の対象外とする。

**`--no-tests-reason`**: 本Roundは `Drawer.OnGUI` 内の `EditorGUI.BeginChangeCheck`/`Popup` 呼び出し順の変更を含むが、これはEditor GUIのユーザー操作検知に依存する分岐であり、既存のDrawerテストの方針（Attributeの契約のみをテスト対象とする）を維持する。ロジック部分（`ResolveDisplayIndex`）は上記のとおりテストを追加するため、`.cs` 変更全体に対してテスト0件にはならない。

## 動作確認手順

Unity Editor上で以下を確認する（自動検証の対象外、人による確認）。

1. `[SceneNameSelector]` を付けたフィールドに、Build Settingsのシーン一覧のうち1つを保存した状態でPrefab/GameObjectを用意する。
2. フィールドへ `[SceneNameSelector(nameof(Filter))]` のようにフィルターを追加し、保存済みの値を除外する条件にする。
3. 対象のInspectorを開き、**何も操作せずに** `property.stringValue`（シリアライズ済みの値。Inspectorを閉じて別オブジェクトを選択し、再度開いて表示される値、または `SerializedObject` をコード側から読む）が変化していないことを確認する。
4. Popupを開いて表示されている候補（先頭の値）を明示的に選択し、保存値がその候補へ更新されることを確認する。
5. `TagSelector` でも同じ手順を繰り返す（プロジェクト設定のタグを使う）。
6. フィルター未指定の既存フィールドで、通常の選択・保存が従来どおり動作することを確認する（回帰確認）。

## バージョン判断

パッチ（実装のみ・不具合修正、公開APIへの影響なし）。

## この Round で触るバージョン関連ファイル

- `package.json` の `version`: パッチを1つ上げる
- `CHANGELOG.md`: `### Fix` 見出しへ本件を追記

## 実施レポート

実施日: 2026-09-06 / バージョン: 6.14.3 / PR: [#218](https://github.com/HIBIKI5201/SymphonyFramework/pull/218)

### 実装した内容

設計どおり4ファイルを変更した。

- `SelectorFilterUtility.cs`: `ResolveDisplayIndex(string[] candidates, string currentValue)` を追加（純粋関数）
- `SceneNameSelectorDrawer.cs` / `TagSelectorDrawer.cs`: `Array.IndexOf` による直接補正を `ResolveDisplayIndex` 呼び出しへ置き換え、`EditorGUI.BeginChangeCheck()` / `EndChangeCheck()` でPopupを囲み、`EndChangeCheck()` が `true` の場合だけ `property.stringValue` を書き換えるよう変更した。未使用になった `using System;` も削除されている
- `SelectorFilterUtilityTests.cs`: `ResolveDisplayIndex` のテスト3件（候補内・候補外・空配列）を追加

実装はCodex CLIワーカー（`scripts/codex_runner.py`）へ委譲し、差分は自分で全件読んでレビューした。設計書からの逸脱、余分なファイル変更、`.meta` の新規追加はいずれも無かった。

### 設計から変えた点

無し。「候補外として判別できる表示」を見送る判断も含め、設計書どおりに実装された。

### 検証結果

`python scripts/verify_round.py --json` を自分で実行した実測値:

- compile: 0 errors / 0 warnings
- EditMode: 726 / 726 成功
- PlayMode: 21 / 21 成功（2往復とも）
- Enter Play Mode Options: Domain Reload / Scene Reload とも無効を維持（`verify_round.py` が実行後に復元）

`python scripts/release_round.py preflight` は全項目OK（`docs`同期を含む）。

### 未実施の確認

「動作確認手順」1〜6はいずれもUnity Editorでの人による目視確認が必要で、今回のセッションでは実施していない。特に次の2点は自動テストではカバーしていない。

- Inspectorを開いただけ（Popupを操作しない）で `property.stringValue` が変化しないこと
- 候補外状態から別候補を明示的に選択すると保存値が更新されること

`EditorGUI.BeginChangeCheck` / `EndChangeCheck` はUnity標準APIの既知の挙動として上記を満たす設計だが、実機での確認は次回に持ち越す。

### 振り返り

- Codexワーカーは設計書の指示どおりに実装し、差し戻しは無かった
- ワーカーの実行環境（sandbox）はuLoopの名前付きパイプを拒否するため、Unity検証はワーカー側で完結せず、必ずステップ3を別途自分で実行する必要がある。これは既存の`worker.md`の注記どおりで、新たな仕組み化の提案は無し
- 気づきは無し
