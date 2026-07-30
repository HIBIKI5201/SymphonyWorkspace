# セーブデータシステム 不具合調査レポート

対象: `Runtime/System/SaveSystem/*`, `Editor/Administrator/UITK/CS/SaveDataRegistryWindow.cs`, `Samples/Runtime/SaveDataSystemSample/*`
作成日: 2026-07-17
ブランチ: `feature/save-system`

---

## 1. 質問1: クライアントがレジストリのインスタンスを持ち、全セーブデータが常に最新であるようにしたい → サンプルで実現できているか

### 結論: できています

現在の `SaveDataRegistry`（[SaveDataRegistry.cs](../SymphonyFrameWork/Runtime/System/SaveSystem/SaveDataRegistry.cs)）は、型ごとに **ただ1つの `SaveDataContent` インスタンスをキャッシュし続け、常にそのインスタンス自身を返す** 設計になっています。

```csharp
public static T Get<T>() where T : SaveDataContent, new()
{
    return (T)Get(typeof(T));
}

public static SaveDataContent Get(Type dataType)
{
    lock (_lock)
    {
        if (_cache.TryGetValue(dataType, out SaveDataContent cached) && cached != null)
        {
            return cached; // 既存インスタンスをそのまま返す（コピーしない）
        }

        SaveDataContent created = (SaveDataContent)Activator.CreateInstance(dataType);
        _cache[dataType] = created;
        return created;
    }
}
```

`LoadAsync` / `SaveAsync` / `DeleteAsync` はいずれも `Get(dataType)` で取得した **同じ参照** をローダーに渡し、ローダー側も新しいインスタンスを作って返すのではなく、`JsonUtility.FromJsonOverwrite` / `JsonConvert.PopulateObject` で **既存インスタンスのフィールドを上書き** します（[JsonUtilitySaveDataLoader.cs](../SymphonyFrameWork/Runtime/System/SaveSystem/Template/JsonUtilitySaveDataLoader.cs), [NewtonsoftSaveDataLoader.cs](../SymphonyFrameWork/Runtime/System/SaveSystem/Template/NewtonsoftSaveDataLoader.cs)）。

つまり:

- どこで `SaveDataRegistry.Get<T>()` を呼んでも、同じ型なら必ず同一インスタンスの参照が返る
- そのインスタンスのフィールドを直接書き換えれば、他のどのコード（別スクリプト、Administratorウィンドウのインスペクタ）から見ても即座に反映される
- `SaveAsync<T>()` は引数を取らず、レジストリが保持している「今の」インスタンスをそのまま永続化する

現在の [SaveDataSystemSample_Controller.cs](../SymphonyFrameWork/Samples/Runtime/SaveDataSystemSample/Scripts/SaveDataSystemSample_Controller.cs) は、この設計どおりに書けています。

```csharp
private void IncreaseLevel()
{
    SaveDataSystemSample_PlayerData data = SaveDataRegistry.Get<SaveDataSystemSample_PlayerData>();
    data.Level++; // レジストリ上のインスタンスを直接書き換える
}

public async void SaveSampleData()
{
    await SaveDataRegistry.SaveAsync<SaveDataSystemSample_PlayerData>(); // 引数なし＝今のインスタンスを保存
}
```

ローカル変数にコピーして、Save 時に `new PlayerData(...)` で別インスタンスを作る、といった「コピーイン・コピーアウト」は行われていません。Editor 側の `SaveDataRegistryWindow` のデバッグインスペクタも `[SerializeReference]` で同じインスタンスを直接参照しているため、Runtime / Editor 双方で単一の実体を共有できています。

### 注意点だった項目 → 4章で対応済み

初出時点では「`Get<T>()` はディスクから自動ロードせず、`LoadAsync<T>()` を先に呼んでいないと空インスタンスが返る」という呼び出し順序の暗黙の契約が残っていました。この点はフォローアップ要望を受けて 4章 のとおり解消しています。

---

## 2. 質問2: Administrator ウィンドウがボタンを押すまで Registry の情報を取得しないバグ

### 原因

[SaveDataRegistryWindow.cs](../SymphonyFrameWork/Editor/Administrator/UITK/CS/SaveDataRegistryWindow.cs) にはすでに `StartAutoRefresh` という自動更新の仕組みが実装されていました。

```csharp
// 修正前
private void StartAutoRefresh(VisualElement root)
{
    _autoRefreshItem?.Pause();
    _autoRefreshItem = root.schedule.Execute(() =>
    {
        if (panel == null) { return; }

        EnsureTypeListCurrent(); // ← 問題箇所
        RefreshView();
    }).Every(0); // ← 実質毎フレーム
}
```

問題は `EnsureTypeListCurrent()` の中身です。

```csharp
private void EnsureTypeListCurrent()
{
    List<Type> latestTypes = AppDomain.CurrentDomain.GetAssemblies()
        .SelectMany(GetTypesSafe)
        .Where(IsSupportedSaveDataType)
        .OrderBy(type => type.FullName, StringComparer.Ordinal)
        .ToList();
    ...
}
```

これは **ロード済み全アセンブリの全型を毎回列挙してフィルタする** 非常に重い処理です。それを `Every(0)`（毎フレーム相当）で回していたため、ウィンドウを開いている間エディタに継続的な負荷がかかり、体感的に「更新が遅い／反映されていないように見える」状態になっていました。

しかし、セーブデータ型（`SaveDataContent` を継承したクラス）の一覧はスクリプトのドメインリロード（再コンパイル）でしか変化しません。ドメインリロードが起きると `SaveDataRegistryWindow` 自体が UXML から再構築され `Initialize_S` が最初から呼び直されるため、**毎フレームの型スキャンは実質不要** でした。一方、`RefreshView()` はキャッシュ済みの `_saveDataTypes` と `SaveDataRegistry` の現在状態を読むだけの軽い処理で、こちらは頻繁に呼んでも問題ありません。

### 適用した修正

`EnsureTypeListCurrent()` は初期化時（`RefreshTypeList()` 経由、[Initialize_S](../SymphonyFrameWork/Editor/Administrator/UITK/CS/SaveDataRegistryWindow.cs) 内で1回）にのみ呼び出すようにし、定期実行からは外しました。定期実行の間隔も `Every(0)` から `Every(200)`（0.2秒ごと）に変更しています。

```csharp
// 修正後
private const long AUTO_REFRESH_INTERVAL_MS = 200;

private void StartAutoRefresh(VisualElement root)
{
    _autoRefreshItem?.Pause();
    _autoRefreshItem = root.schedule.Execute(() =>
    {
        if (panel == null) { return; }

        RefreshView();
    }).Every(AUTO_REFRESH_INTERVAL_MS);
}
```

これにより:

- ウィンドウを開いている間、ボタン操作なしで Registry の状態（Loaded / Saved / Empty、SaveDate、選択中インスタンスの中身）が自動的に反映され続ける
- 重い型スキャンは初期化時とドメインリロード後のみ実行され、エディタへの継続負荷がなくなる

### 変更ファイル

- [Editor/Administrator/UITK/CS/SaveDataRegistryWindow.cs](../SymphonyFrameWork/Editor/Administrator/UITK/CS/SaveDataRegistryWindow.cs)
  - `StartAutoRefresh` から `EnsureTypeListCurrent()` 呼び出しを削除
  - 定期実行間隔を `Every(0)` → `Every(200)`（`AUTO_REFRESH_INTERVAL_MS` 定数化）に変更

> **追記**: この修正はパフォーマンス上の問題点としては正しいものでしたが、実際には自動更新そのものが動いていませんでした。根本原因と本当の修正は 5章 を参照してください。

---

## 3. 未対応・要検討事項（参考）

- `SaveDataRegistry.GetEntries()` は一度でも `Get()` された型を常に「キャッシュ済み」として扱うため、Administrator ウィンドウの一覧で "Loaded" 表示が「実際にディスクからロード済み」を意味しなくなっています（ウィンドウを開いて型を選択しただけでも `Get()` が走るため）。実害はありませんが、表示ラベルの意味合いが変わっている点は認識しておくとよいです。
- `SaveAsync<T>()` / `DeleteAsync<T>()` を、そのセッションで一度も `Get`/`LoadAsync` していない型に対していきなり呼んだ場合は、4章のオートロードの対象外です（内部的にロードを介さず既定値インスタンスを直接生成して保存/削除処理に渡すため）。既存の永続化データがある状態で最初の呼び出しが `SaveAsync` だと、意図せず初期値で上書きされる可能性があります。通常の利用（`Get`/`LoadAsync` を先に経由する）では発生しません。

---

## 4. 追加修正: `Get()` の初回アクセス時オートロード

### 要望

「`Get` 時にキャッシュが無ければ自動でロードしてほしい」という追加要望に対応しました。

### 実装方針

- キャッシュの有無を判定する処理と、ロードを伴わずにキャッシュを作る処理を分離する必要がありました。`LoadAsync` / `SaveAsync` / `DeleteAsync` は内部で「今のインスタンスを取得する」ために `Get` を呼んでいたため、`Get` 自身がロードを行うようにすると **`Get` → `LoadAsync` → `Get` → ...** の循環呼び出しになってしまいます。
- そこで、ロードを発生させない内部専用のキャッシュ取得メソッド `GetOrCreateCache(dataType, out bool isFirstAccess)` を新設し、`LoadAsync` / `SaveAsync` / `DeleteAsync` はこちらを使うように変更しました。
- 公開 API の `Get(Type dataType)` だけが「初回アクセスかどうか」を見て、初回であれば `LoadAsync(dataType).GetAwaiter().GetResult()` で同期的にロードしてから返すようにしています。

```csharp
public static SaveDataContent Get(Type dataType)
{
    ValidateDataType(dataType);

    SaveDataContent data = GetOrCreateCache(dataType, out bool isFirstAccess);

    if (isFirstAccess)
    {
        // キャッシュが無い＝初回アクセスの場合のみ自動でロードする
        LoadAsync(dataType).GetAwaiter().GetResult();
    }

    return data;
}

private static SaveDataContent GetOrCreateCache(Type dataType, out bool isFirstAccess)
{
    lock (_lock)
    {
        if (_cache.TryGetValue(dataType, out SaveDataContent cached) && cached != null)
        {
            isFirstAccess = false;
            return cached;
        }

        SaveDataContent created = (SaveDataContent)Activator.CreateInstance(dataType);
        _cache[dataType] = created;
        isFirstAccess = true;
        return created;
    }
}
```

`LoadAsync(Type dataType, ...)` 側も `Get` ではなく `GetOrCreateCache` を使うよう変更したため、`Get` が内部で `LoadAsync` を呼んでも二重ロードは発生しません（`LoadAsync` 内の `GetOrCreateCache` はすでにキャッシュ済みのインスタンスを返すだけになるため）。

### 挙動

- 一度もアクセスしていない型に対して初めて `Get<T>()` / `Get(Type)` を呼ぶと、その場で同期的に永続化データがロードされます（保存データが無ければ既定値のまま）。以降は通常どおりキャッシュされたインスタンスをそのまま返し、再ロードは行いません（Save 前の編集内容が消えることはありません）。
- 既存の `LoadAsync<T>()` を明示的に呼ぶフローとの二重ロードは発生しません。
- 同期ブロッキング（`GetAwaiter().GetResult()`）を使っていますが、既定のローダー（`JsonUtilitySaveDataLoader` / `NewtonsoftSaveDataLoader`）は `PlayerPrefs` ベースで実質同期処理のため、デッドロックや体感的な待ち時間は発生しません。将来的に本当に非同期な I/O を行うカスタムローダー（クラウドセーブ等）を実装する場合は、この同期ブロッキングが問題にならないか個別に確認してください。

### 変更ファイル

- [Runtime/System/SaveSystem/SaveDataRegistry.cs](../SymphonyFrameWork/Runtime/System/SaveSystem/SaveDataRegistry.cs)
  - `GetOrCreateCache(Type, out bool)` を追加（ロードを発生させない内部専用アクセサ）
  - `Get(Type)` が初回アクセス時に自動で `LoadAsync` を実行するように変更
  - `LoadAsync` / `SaveAsync` / `DeleteAsync` の内部実装を `Get` から `GetOrCreateCache` に切り替え（循環呼び出し回避）

---

## 5. Administrator ウィンドウ自動更新バグ: 本当の原因と修正、SaveDataSystem 全体のリファクタリング所見

2章の修正（`Every(0)` → `Every(200)`、型スキャンの間引き）はパフォーマンス上正しい改善でしたが、**自動更新が実際には一度も動いていなかった** という、より根本的な設計ミスが別に存在していました。

### 5-1. 本当の原因: 「自パネル駆動」と「親駆動」の2方式が混在していた

`SymphonyAdministrator` パネルには5つの子ウィンドウ（`PauseWindow` / `ServiceLocatorWindow` / `SceneLoaderWindow` / `AutoEnumGeneratorWindow` / `SaveDataRegistryWindow`）がありますが、更新方式が統一されていませんでした。

```csharp
// SymphonyAdministrator.cs（修正前）
private void Update()
{
    _pauseWindow?.Update();
    _serviceLocatorWindow?.Update();
    _sceneLoaderWindow?.Update();
    // _saveDataRegistryWindow?.Update(); ← 呼ばれていない
}
```

`PauseWindow` と `ServiceLocatorWindow` は「毎フレーム親（`SymphonyAdministrator`）の `Update()` から呼ばれる `public void Update()` を自分で持つ」という共通パターンで実装されており、これは `EditorApplication.update` に直結しているため確実に動作します。

一方 `SaveDataRegistryWindow` だけは `Update()` メソッドを持たず、代わりに自分自身の `VisualElement.schedule.Execute(...).Every(...)` で自走する別方式を採用していました。この方式には2つの問題がありました。

1. **他のウィンドウと方式が統一されていない** — 同じ Administrator パネル内で2つの異なる更新の仕組みが混在しているのは設計として不自然で、片方だけ結線を忘れる／片方だけ動かないという事故の温床になっていました。
2. **`schedule.Execute` の呼び出しタイミングが早すぎた** — `StartAutoRefresh` は `Initialize_S` から呼ばれますが、`Initialize_S` は UXML の `Instantiate()` 中（＝ `SaveDataRegistryWindow` がまだどの `Panel` にも Add されていない、コンストラクタ実行中に近いタイミング）に同期的に完走します。`VisualElement` がパネルに未アタッチの状態で `schedule.Execute(...).Every(...)` を予約しても、後から `rootVisualElement.Add(windowElement)` でパネルにアタッチされた際に確実にスケジューラへ引き継がれる保証がなく、実際にコールバックが一度も発火しない状態になっていたと考えられます。

これが「ロード/セーブなどのボタンを押すまで画面が更新されない」という報告と一致します。ボタンのクリックハンドラ（`ExecuteAction` 経由）は明示的に `RefreshView()` を呼んでいるので手動操作時だけ画面が更新され、自動更新の仕組みは実質機能していませんでした。

### 5-2. 修正: 実績のある「親駆動」方式に統一

自パネル駆動 (`VisualElement.schedule`) をやめ、`PauseWindow` / `ServiceLocatorWindow` と同じ「親から呼ばれる `public void Update()`」方式に統一しました。

```csharp
// SaveDataRegistryWindow.cs（修正後）
public void Update()
{
    RefreshView();
}
```

```csharp
// SymphonyAdministrator.cs（修正後）
private void Update()
{
    _pauseWindow?.Update();
    _serviceLocatorWindow?.Update();
    _sceneLoaderWindow?.Update();
    _saveDataRegistryWindow?.Update();
}
```

`EnsureTypeListCurrent()`（重い型スキャン）は 2章の判断どおり毎フレーム呼ぶ必要が無いため、`Initialize_S` 内の `RefreshTypeList()` 一回のみで維持しています。これにより、`SymphonyAdministrator` が確実に踏んでいる `EditorApplication.update` ループにそのまま乗るため、パネルへのアタッチ有無に依存せず、ボタン操作なしで Registry の状態が継続的に反映されます。

### 変更ファイル

- [Editor/Administrator/UITK/CS/SaveDataRegistryWindow.cs](../SymphonyFrameWork/Editor/Administrator/UITK/CS/SaveDataRegistryWindow.cs)
  - `_autoRefreshItem` フィールドと `StartAutoRefresh`（`VisualElement.schedule` 方式）を削除
  - `public void Update() { RefreshView(); }` を追加（`PauseWindow` / `ServiceLocatorWindow` と同じ方式）
- [Editor/Administrator/SymphonyAdministrator.cs](../SymphonyFrameWork/Editor/Administrator/SymphonyAdministrator.cs)
  - `Update()` に `_saveDataRegistryWindow?.Update();` を追加

### 5-3. SaveDataSystem 全体のリファクタリング所見

今回のバグ調査のついでに `Runtime/System/SaveSystem` 全体を見直し、他に不自然な点がないか確認しました。

| 項目 | 内容 | 対応 |
| --- | --- | --- |
| `SaveDataRegistry.ValidateDataInstance` が未使用 | `SaveAsync(Type)` が引数なしの現行シグネチャに変わった際の名残で、`SaveDataRegistry.cs` 内で定義されているだけで一度も呼ばれていませんでした（`JsonUtilitySaveDataLoader` / `NewtonsoftSaveDataLoader` にはそれぞれ独立した同名メソッドがあり、そちらは使われています）。 | **削除しました**（本レポートの修正に含む） |
| `SaveSystem<TData, TLoader>`（互換API）が `SaveDataRegistry` と別のキャッシュを持つ | `SaveSystem<TData, TLoader>` は自分専用の `static TData _saveData` を持ち、`SaveDataRegistry._cache` とは別物です。`LoadFromRegistry()` / `SaveToRegistry()` で明示的に橋渡しをしない限り、`SaveSystem<T,L>.Get()` と `SaveDataRegistry.Get<T>()` は違うインスタンスを指す可能性があり、「レジストリが唯一の真実の源」という設計方針と矛盾します。実際に KillChord 側の実験コードはこの互換APIを直接使っています。 | **未対応（要相談）**。フレームワーク内では他に参照箇所がなく、外部プロジェクト互換のためだけに残っている状態です。段階的に `SaveDataRegistry` 直接利用へ寄せて、将来的に削除する方針を検討することを推奨します。 |
| `Obsolute` フォルダ名・名前空間が `Obsolete` のタイポ | `[Obsolete(...)]` 属性のスペルは正しい一方、フォルダ名 `Runtime/System/SaveSystem/Obsolute/`自体が誤字になっています。 | **未対応**。命名の修正はフォルダ移動（`.meta` の GUID 維持含む）を伴うため影響範囲を確認してから対応することを推奨し、今回は見送りました。 |
| `SaveDataRegistry.GetEntries()` の "Loaded" 表示の意味変化 | 3章に記載済み。`Get()` が呼ばれた時点でキャッシュされるため、「実際にディスクからロードした」を意味しなくなっています。 | **未対応（表示ラベルの見直しを推奨）** |

上記のうち、明確にデッドコードだった `ValidateDataInstance` は本レポートの修正で削除済みです。残り3件は挙動を変える設計判断を伴うため、今回は対応せずここに記録するに留めています。

---

## 6. 重大バグ: Administrator ウィンドウで Load しても保存時のデータに戻らない

### 再現手順

Administrator ウィンドウで「編集 → Save → さらに編集 → Load」を行うと、Load 後に表示されるデータが **Save した時点の値ではなく、Load 直前の「編集中（未保存）」の値のまま** になる。

### 原因

`SaveDataRegistryWindow` のデバッグインスペクタは、`SaveDataContent`（プレーンな C# クラス）を編集可能にするために `SaveDataDebugState`（`ScriptableObject`）の `[SerializeReference] SaveDataContent _data` フィールド経由で `SerializedObject` / `PropertyField` にバインドしています。

通常は、`_debugState._data` は `SaveDataRegistry` が保持している正本インスタンスと同一参照になるよう `SetData()` で揃えていますが、`[SerializeReference]` フィールドは Unity の内部シリアライズ処理（`SerializedObject.ApplyModifiedProperties()`）を経由するため、**インスペクタでの編集後、`_debugState._data` が Registry 側の正本と別インスタンスになってしまう可能性があります。**

この状態で `SaveSelected()` を見ると、致命的な問題がありました。

```csharp
// 修正前
private void SaveSelected()
{
    SaveDataContent data = _debugState.GetData(); // ← 編集中のインスタンスを取得しているが…
    if (data == null)
    {
        BindCurrentSelection();
    }

    SaveDataRegistry.SaveAsync(_selectedType).GetAwaiter().GetResult(); // ← data を一切使わず、Registry 側の正本をそのまま保存
    ...
}
```

`_debugState.GetData()` で取得した「インスペクタ上で編集中のインスタンス」を **一度も使わずに** 捨てており、実際に保存されるのは `SaveDataRegistry` 側の（編集前のままかもしれない）正本インスタンスです。`_debugState._data` と Registry の正本が食い違っている場合、**画面に表示されている編集内容とは異なるデータが保存される** ことになります。

その後 Load すると、`LoadSelected()` は Registry の正本を正しくディスクの内容で上書きしますが、保存されていたデータ自体が「本来 Save したかった値」ではなかったため、結果として「現在（未保存）の値のように見える古い値」がそのまま出てくる、という現象になります。

### 適用した修正

1. **`SaveSelected()`**: 保存直前に、編集中のインスタンス (`_debugState.GetData()`) と Registry の正本が別インスタンスになっていないか `ReferenceEquals` で確認し、食い違っていれば `JsonUtility` 経由で編集内容を正本へ同期してから `SaveAsync` するようにしました。

```csharp
// 修正後
private void SaveSelected()
{
    SaveDataContent editingData = _debugState.GetData();
    if (editingData == null)
    {
        BindCurrentSelection();
        editingData = _debugState.GetData();
    }

    SaveDataContent canonical = SaveDataRegistry.Get(_selectedType);
    if (!ReferenceEquals(canonical, editingData))
    {
        JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(editingData), canonical);
    }

    SaveDataRegistry.SaveAsync(_selectedType).GetAwaiter().GetResult();
    ...
}
```

2. **`BindCurrentSelection` / `LoadSelected` / `SaveSelected` / `DeleteSelected` 共通**: これまで `_debugState.SetData(...)` の後に `_debugSerializedObject.Update()` だけを呼んでいましたが、`[SerializeReference]` の参照先切り替えを `Update()` だけでは確実に検知できないケースに備え、`SerializedObject` 自体を作り直す `RebindDebugState()` に統一しました。

```csharp
private void RebindDebugState(SaveDataContent data)
{
    _debugState.SetData(data, data?.SaveDate);
    _debugSerializedObject = new SerializedObject(_debugState);
}
```

これにより、インスペクタでの編集内容が確実に Registry の正本に反映されてから保存されるようになり、また Load / Delete 後の表示も確実に最新の正本インスタンスへ再バインドされます。

### 変更ファイル

- [Editor/Administrator/UITK/CS/SaveDataRegistryWindow.cs](../SymphonyFrameWork/Editor/Administrator/UITK/CS/SaveDataRegistryWindow.cs)
  - `SaveSelected()` に、編集中インスタンスと Registry 正本が分岐している場合の同期処理を追加
  - `BindCurrentSelection` / `LoadSelected` / `SaveSelected` / `DeleteSelected` / `EnsureTypeListCurrent` の再バインド処理を `RebindDebugState()` に統一（`SerializedObject` を作り直すように変更）

### 補足

Runtime 側（`SaveDataRegistry` 本体、Sample）はこの問題の影響を受けません。`SaveDataSystemSample_Controller` は `SaveDataRegistry.Get<T>()` で取得した参照を直接書き換えているだけで、`[SerializeReference]` / `SerializedObject` を経由しないため、Registry の正本と表示中のインスタンスが分岐する余地がないためです。

> **追記**: 上記の「補足」は誤りでした。実際には Sample（Runtime）でも同じ症状が再現しています。本当の原因は 7章 を参照してください。

---

## 7. 真の重大バグ: `LoadAsync` の重複排除キャッシュが完了済みタスクを永久に握り続ける

### 再現ログ（Sample / Runtime）

```
Registry が保持している現在インスタンスを永続化します。
保存完了: Symphony / Level 2 / Gold 100
Level を 1 増やしました。まだ保存はされていません。
Level を 1 増やしました。まだ保存はされていません。
永続化済みデータを読み込み、Registry 上の現在インスタンスを差し替えます。
ロード完了: Symphony / Level 4 / Gold 100   ← Level 2（保存時）に戻るべきが、Level 4（現在値）のまま
```

`[SerializeReference]` を一切使わない純粋な Runtime コード（`SaveDataSystemSample_Controller`）でも再現したため、6章の「Editor 限定の問題」という切り分けは誤りでした。原因は `SaveDataRegistry` 本体にありました。

### 原因

`LoadAsync(Type dataType, ...)` は、同じ型への同時多発的な Load リクエストをまとめるために `_loadingTasks` という辞書で「実行中のロードタスク」を重複排除しています。

```csharp
// 修正前
lock (_lock)
{
    if (_loadingTasks.TryGetValue(dataType, out Task loadingTask))
    {
        return new ValueTask(loadingTask);
    }

    Task loadTask = LoadInternalAsync(dataType, current, token);
    _loadingTasks[dataType] = loadTask; // ← ここが問題
    return new ValueTask(loadTask);
}
```

既定のローダー（`JsonUtilitySaveDataLoader` / `NewtonsoftSaveDataLoader`）は `PlayerPrefs` を使うため **同期的に完了** します。つまり `LoadInternalAsync(...)` の呼び出しが戻ってきた時点で、その中の `finally`（`_loadingTasks.Remove(dataType)`）は **すでに実行済み** です（ただしこの時点ではまだ辞書に登録していないので、実質何もしない Remove です）。

その直後の `_loadingTasks[dataType] = loadTask;` で、**完了済みのタスクを辞書に登録してしまいます。** このエントリを削除するタイミングはもう存在しません（`finally` は一度しか実行されず、しかも既に実行済み）。結果、`_loadingTasks[dataType]` に「完了済みの幽霊タスク」が **永久に** 残り続けます。

これ以降、同じ型に対する `LoadAsync` 呼び出しはすべて次の分岐に入ります。

```csharp
if (_loadingTasks.TryGetValue(dataType, out Task loadingTask))
{
    return new ValueTask(loadingTask); // 既に完了しているタスクをそのまま返すだけ
}
```

**つまり、実際にはローダーが二度と呼ばれず、PlayerPrefs からの再読み込みが一切行われません。** `Get()`/`LoadAsync()` はキャッシュ済みインスタンスをそのまま返すだけになるため、画面には「現在（未保存）の値」がそのまま表示され続けます。

サンプルは `Start()` で最初に必ず `LoadAsync<T>()` を呼ぶため、**そのゲーム内で最初の1回のロードで即座にこの幽霊タスクが生成され、それ以降の Load ボタンは事実上何もしなくなっていました。**

### 修正

`LoadInternalAsync` が同期的に完了しているタスクは、そもそも `_loadingTasks` に登録しないようにしました。

```csharp
// 修正後
Task loadTask = LoadInternalAsync(dataType, current, token);

if (!loadTask.IsCompleted)
{
    _loadingTasks[dataType] = loadTask;
}

return new ValueTask(loadTask);
```

同期的に完了する既定ローダーでは辞書に何も残らないため、毎回正しく `LoadInternalAsync` → `GetLoader().LoadAsync(...)` が実行され、PlayerPrefs から再読み込みされます。将来的に本当に非同期な I/O を行うカスタムローダー（クラウドセーブ等）を実装した場合は、そのロードが完了する前に `loadTask.IsCompleted` が `false` のまま辞書に登録されるため、重複リクエストの排除は引き続き機能します。

### 変更ファイル

- [Runtime/System/SaveSystem/SaveDataRegistry.cs](../SymphonyFrameWork/Runtime/System/SaveSystem/SaveDataRegistry.cs)
  - `LoadAsync(Type dataType, ...)` で、`loadTask.IsCompleted` が `false` の場合のみ `_loadingTasks` に登録するように修正

### 6章の記載について

6章で報告した Administrator ウィンドウの `SaveSelected()` の問題（編集中インスタンスと Registry 正本の分岐対策）自体は妥当な防御的修正であり、そのまま残しています。ただし「Runtime 側は影響を受けない」という補足は誤りで、本章の `LoadAsync` の幽霊タスク問題が両方のケースで観測された症状の主因でした。

---

## 8. ドメインリロードで選択中セーブデータの表示がリセットされる問題

### 要望

「最初に選択しているセーブデータを常に確認可能な状態にしておきたい」「ドメインリロードすると Window のデータ表示がリセットされ、Load や Save を実行しないと表示されない」という要望・報告。

### 原因

`SaveDataRegistryWindow` は `VisualElement`（`UnityEngine.Object` ではない、ただの C# オブジェクト）です。`SymphonyAdministrator.OnEnable()` はウィンドウが開かれた時だけでなく **ドメインリロード後にも呼ばれ**、`LoadWindow()` が毎回 `rootVisualElement.Clear()` → UXML から再インスタンス化を行います。

```csharp
private TemplateContainer LoadWindow()
{
    rootVisualElement.Clear();
    var windowTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(...);
    var windowElement = windowTree.Instantiate(); // ← SaveDataRegistryWindow が新規インスタンスとして作り直される
    rootVisualElement.Add(windowElement);
    return windowElement;
}
```

つまりドメインリロードのたびに `SaveDataRegistryWindow` は **完全に新しいインスタンス** になり、`_selectedType`（どの型を選んでいたか）はフィールドの初期値である `null` に戻ります。`EnsureTypeListCurrent()` は `_selectedType == null` の場合、常にアルファベット順で最初の型（`index = 0`）を選び直していたため、直前まで確認していたセーブデータとは別の型が表示される（あるいは選択操作をやり直すまで実質何も表示されていないように見える）状態になっていました。

なお `SaveDataRegistry` 側のキャッシュ（`_cache`）は static フィールドなのでドメインリロードで消えますが、7章の修正により `Get()` は初回アクセス時に自動でロードするため、**型さえ正しく選択されれば** データは即座に表示されます。問題は「型の選択そのものがドメインリロードで失われること」でした。

### 修正

Unity Editor でドメインリロードをまたいで軽量な状態を保持する標準的な仕組み `SessionState`（`UnityEditor.SessionState`。Editor プロセスを終了すると消えるが、スクリプト再コンパイルは生き残る）を使い、選択中の型を記憶・復元するようにしました。

```csharp
private void SetSelectedType(Type type)
{
    _selectedType = type;
    SessionState.SetString(SELECTED_TYPE_SESSION_KEY, type?.AssemblyQualifiedName ?? string.Empty);
}

private static Type RestoreSelectedTypeFromSession()
{
    string typeName = SessionState.GetString(SELECTED_TYPE_SESSION_KEY, string.Empty);
    return string.IsNullOrEmpty(typeName) ? null : Type.GetType(typeName);
}
```

`EnsureTypeListCurrent()` で `_selectedType` が `null`（＝ドメインリロード直後、もしくは初回起動）の場合、まず `SessionState` から前回選択していた型の復元を試みるようにしました。

```csharp
Type typeToSelect = _selectedType ?? RestoreSelectedTypeFromSession();

int index = typeToSelect == null
    ? 0
    : _saveDataTypes.FindIndex(type => type == typeToSelect);

if (index < 0) { index = 0; }

_selectedIndex = index;
SetSelectedType(_saveDataTypes[index]);
...
BindCurrentSelection(false); // Registry.Get() が初回アクセスなら自動でロードするので、ここで即座にデータが表示される
```

型が見つからない場合（対象クラスが削除された等）はこれまで通りアルファベット順で先頭の型にフォールバックします。ドロップダウンで型を手動選択した場合（`OnTypeChanged`）も同様に `SetSelectedType()` 経由で記憶するようにしています。

### 変更ファイル

- [Editor/Administrator/UITK/CS/SaveDataRegistryWindow.cs](../SymphonyFrameWork/Editor/Administrator/UITK/CS/SaveDataRegistryWindow.cs)
  - `SELECTED_TYPE_SESSION_KEY` を追加
  - `SetSelectedType(Type)` / `RestoreSelectedTypeFromSession()` を追加
  - `EnsureTypeListCurrent()` / `OnTypeChanged()` で選択の記憶・復元を行うように変更

### 補足

`SessionState` は Editor プロセスを終了すると消えます（`EditorPrefs` とは異なり、マシン全体やユーザー設定として永続化はされません）。「スクリプト再コンパイル中は選択を保持したいが、Editor を閉じたらリセットされてよい」という今回の要件に対しては `SessionState` が適切な選択です。Editor 再起動をまたいで記憶したい場合は `EditorPrefs` への切り替えを検討してください。

---

## 9. 「New または Load を押してください」等、旧設計時代の UI 文言の残骸

### 報告内容

Administrator ウィンドウで、型は選択されデータも Registry 上は "Loaded" 状態なのに、デバッグインスペクタ欄に「現在編集中のデータはありません。New または Load を押してください。」という HelpBox が表示されている。「New」ボタンは既に存在しないため、この文言は現在の設計と矛盾しているはず、という指摘。

### 確認結果

ご指摘の通りです。この HelpBox は `dataProperty.managedReferenceValue == null`（＝ `_debugState._data` が null）のときだけ表示されますが、現在の設計では:

- `SaveDataRegistry.Get(Type)` は **必ず何らかのインスタンスを返し**、null を返すことはありません（3章・4章）。
- `BindCurrentSelection()` / `LoadSelected()` / `SaveSelected()` / `DeleteSelected()` はすべて `SaveDataRegistry.Get(...)` の結果を `RebindDebugState()` 経由でバインドするため、型が選択されていれば `_data` が null になることはありません。
- 「New」ボタンは古い設計時代のもので、現行 UXML には存在しません（Load / Save / Delete の3つのみ）。

つまり、この分岐に到達するのは **プロジェクト内に `SaveDataContent` を継承したセーブデータ型が1つも存在しない場合のみ**（`_saveDataTypes.Count <= 0`）です。それ以外のケースで表示されることは設計上あり得ません。

また同じスクリーンショットで、Status 欄に「Type を選択して Load してください。」という初期プレースホルダー文言がそのまま残っていた点も同根の問題でした。`EnsureTypeListCurrent()` が型を自動選択・自動ロードした際に `BindCurrentSelection(false)` を呼んでおり、`updateStatus=false` によってステータス文言の更新がスキップされていたため、実際にはロード済みにもかかわらず「まだ選択されていない」ように見える表示になっていました。

### 修正

- `DrawEditorInspector()` の HelpBox 文言を、実際に到達しうる唯一のケース（型が1つも存在しない）に即した内容に変更。
- `EnsureTypeListCurrent()` の型なし分岐で `_statusMessage` も適切に設定。
- `BindCurrentSelection()` の呼び出しをすべて `updateStatus=true`（デフォルト）に統一し、自動選択時も正しいステータス文言（`"{型名} の現在インスタンスを表示しています。"`）が表示されるようにした。もう使われなくなった `updateStatus` パラメータ自体も削除。
- 初期プレースホルダーの `_statusMessage` も `"初期化中です…"` に変更（`Initialize_S` が同期的に完走するため実際に表示されることはほぼ無いが、旧文言のまま残すのは誤解を招くため）。

### 変更ファイル

- [Editor/Administrator/UITK/CS/SaveDataRegistryWindow.cs](../SymphonyFrameWork/Editor/Administrator/UITK/CS/SaveDataRegistryWindow.cs)
  - `DrawEditorInspector()` の HelpBox 文言を修正
  - `EnsureTypeListCurrent()` の「型が見つからない」分岐に `_statusMessage` 設定を追加
  - `BindCurrentSelection(bool updateStatus = true)` → `BindCurrentSelection()`（パラメータ削除、常にステータス更新）
  - 初期 `_statusMessage` を変更

---

## 10. 「わざわざインスタンス化されていないデータをインスタンス化させる必要はない」という設計指摘

### 報告内容

9章の修正を確認しようとしたところ、以下の矛盾した表示になった。

- Status: `初期化中です…`（プレースホルダーのまま更新されていない）
- HelpBox: `プロジェクト内に SaveDataContent を継承したセーブデータ型が見つかりません。`
- 一方で `Visible Entries: 1`、Registry Cache には `State: Loaded` のエントリが実在

「Registry に既に入っているデータの先頭を表示すればよく、まだインスタンス化されていないデータをわざわざインスタンス化させる必要はない」という設計上の指摘。

### 原因の分析

9章までの実装は、`EnsureTypeListCurrent()` が **リフレクションで見つけた「セーブデータになり得る全ての型」のうち、アルファベット順で先頭の型を自動選択し、`SaveDataRegistry.Get()` で強制的にインスタンス化** していました。

```csharp
// 修正前
Type typeToSelect = _selectedType ?? RestoreSelectedTypeFromSession();
int index = typeToSelect == null ? 0 : _saveDataTypes.FindIndex(...);
if (index < 0) { index = 0; } // ← 何も手がかりが無ければ強制的に先頭を選ぶ
...
BindCurrentSelection(); // → SaveDataRegistry.Get() で未使用の型を新規インスタンス化
```

この「触られてもいない型を Window が勝手にインスタンス化する」動作自体が、そもそも古い設計思想（New ボタンで明示的に作る前提が崩れた後も、"何かは表示する" ために自動でインスタンス化していた名残）でした。この自動インスタンス化の過程（`Get()` 内の初回ロード処理）で何らかの例外が発生すると、`BindCurrentSelection()` の後半（`RebindDebugState` によるバインドや `_statusMessage` の更新）に到達できず、結果として「Registry には既にデータがある（例外発生前にキャッシュへは登録済みのため）のに、Window 側の表示は初期状態のまま」という、まさに報告いただいた矛盾した表示になっていたと考えられます。

### 修正

自動選択のロジックを、**Registry に既に乗っている（＝どこかで実際にロード／セーブ／使用された）データを優先する** 方式に変更しました。何もインスタンス化されていない場合は、自動選択・自動インスタンス化を行わず、ユーザーの明示操作（Type 選択 or Load）を待つようにしています。

```csharp
// 修正後
private static Type ResolveAutoSelectType()
{
    IReadOnlyList<SaveDataRegistryEntryInfo> cachedEntries = SaveDataRegistry.GetEntries();
    if (cachedEntries.Count <= 0)
    {
        return null; // 何もインスタンス化されていないなら自動選択しない
    }

    Type sessionType = RestoreSelectedTypeFromSession();
    if (sessionType != null && cachedEntries.Any(entry => entry.DataType == sessionType))
    {
        return sessionType; // 前回選択していた型が今も Registry に乗っていればそれを優先
    }

    return cachedEntries
        .Select(entry => entry.DataType)
        .OrderBy(type => type.FullName, StringComparer.Ordinal)
        .First(); // Registry の先頭（＝既に使われているデータ）を表示するだけ
}
```

`typeToSelect` が null（Registry が空）の場合は、`_selectedType` を null のままにし、ドロップダウンも空表示にして、`Get()` を一切呼ばずに待機します。

```csharp
if (index < 0)
{
    _selectedIndex = -1;
    _selectedType = null;
    _typeDropdown.SetValueWithoutNotify(string.Empty);
    RebindDebugState(null);
    _statusMessage = "Type を選択するか、既存のセーブデータを Load してください。";
    return;
}
```

ドロップダウン自体（`_saveDataTypes`、選択肢一覧）は引き続きリフレクションで全ての `SaveDataContent` 継承型を列挙します。これは「ユーザーが明示的に選んで確認・作成したい」というケースまで塞ぐ必要はないためです。ユーザーが手動でドロップダウンから型を選んだ場合（`OnTypeChanged`）は、これまで通り `Get()` によるインスタンス化を行います（＝明示的な操作なので許容）。

また、HelpBox の文言も状況に応じた `_statusMessage` をそのまま表示するように変更し、「型が存在しない」場合と「型はあるが何も選択されていない」場合を正しく区別できるようにしました。

### 変更ファイル

- [Editor/Administrator/UITK/CS/SaveDataRegistryWindow.cs](../SymphonyFrameWork/Editor/Administrator/UITK/CS/SaveDataRegistryWindow.cs)
  - `EnsureTypeListCurrent()`: 自動選択ロジックを `ResolveAutoSelectType()`（Registry 優先、無ければ未選択のまま）に変更
  - `ResolveAutoSelectType()` を新規追加
  - 何も選択できない場合に `Get()` を呼ばず `_selectedType = null` のまま待機するように変更
  - `DrawEditorInspector()` の HelpBox 文言を `_statusMessage` を表示するように変更（状況に応じた文言を一元化）

---

## 11. 未選択のまま Registry に後からデータが乗った場合の自動選択

### 要望

10章の修正で「未インスタンス化のデータを強制的にインスタンス化しない」ようになったが、その後 **Registry に要素が1つ以上乗った時点で自動的にそれを選択してほしい**（例: Window は選択なしで開いたまま、Play Mode で Sample がロード/セーブしてデータが Registry に乗った場合など）。

### 修正

自動選択の実行タイミングを、初期化時（`EnsureTypeListCurrent` / `Initialize_S` 内で1回）だけでなく、**選択が無い間は毎フレームの `Update()` でも試みる** ように変更しました。選択ロジック自体（`ResolveAutoSelectType()` = Registry を優先し、無ければ選択しない）は10章のものをそのまま再利用し、新規にインスタンス化する処理は一切追加していません。

```csharp
public void Update()
{
    TryAutoSelectFromRegistry();
    RefreshView();
}

private void TryAutoSelectFromRegistry()
{
    if (_selectedType != null)
    {
        return; // 既に何か選択されていれば何もしない
    }

    Type typeToSelect = ResolveAutoSelectType();
    if (typeToSelect == null)
    {
        return; // Registry がまだ空なら何もしない
    }

    ApplyAutoSelection(typeToSelect);
}
```

`EnsureTypeListCurrent()` 側の選択反映処理も `ApplyAutoSelection(Type)` として共通化し、初期化時のパスと `Update()` からの自動選択パスで同じロジックを使うようにしています。`_selectedType != null` の間は `SaveDataRegistry.GetEntries()` の軽い読み取りだけで即 return するため、毎フレーム呼んでもコストはほぼありません。

### 変更ファイル

- [Editor/Administrator/UITK/CS/SaveDataRegistryWindow.cs](../SymphonyFrameWork/Editor/Administrator/UITK/CS/SaveDataRegistryWindow.cs)
  - `Update()` から `TryAutoSelectFromRegistry()` を呼ぶように変更
  - `TryAutoSelectFromRegistry()` を新規追加
  - `EnsureTypeListCurrent()` の選択反映処理を `ApplyAutoSelection(Type)` として抽出・共通化
