using SymphonyFrameWork.System.SaveSystem;
using SymphonyFrameWork.Utility;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace SymphonyFrameWork.Editor
{
    /// <summary> SaveDataRegistryのキャッシュ確認、編集、保存操作を提供する管理パネル。 </summary>
    [UxmlElement]
    public sealed partial class SaveDataRegistryWindow : SymphonyVisualElement, IDisposable
    {
        private const string SELECTED_TYPE_SESSION_KEY = "SymphonyFrameWork.SaveDataRegistryWindow.SelectedTypeName";

        private readonly SaveDataDebugState _debugState;
        private SerializedObject _debugSerializedObject;
        private List<Type> _saveDataTypes = new();
        private Type _selectedType;
        private string _statusMessage = "初期化中です…";
        private Vector2 _editorScrollPosition;

        private Label _currentLoaderLabel;
        private Label _loadedEntriesCountLabel;
        private Label _statusLabel;
        private IMGUIContainer _editorContainer;
        private ListView _cacheListView;
        private string _lastViewSignature;
        private IReadOnlyList<SaveDataRegistryEntryInfo> _registryEntriesSnapshot;
        private List<SaveDataRegistryEntryInfo> _sortedEntries = new();
        private bool _disposed;

        /// <summary> 管理パネル用UXMLと一時編集状態の初期化を開始する。 </summary>
        public SaveDataRegistryWindow() : base(
            SymphonyAdministrator.UITK_UXML_PATH + "SaveDataRegistryWindow.uxml",
            InitializeType.None,
            LoadType.AssetDataBase)
        {
            _debugState = ScriptableObject.CreateInstance<SaveDataDebugState>();
            // HideAndDontSave には NotEditable も含まれ、SerializedProperty がすべて
            // 読み取り専用になる。永続化だけを防ぎ、デバッグ編集は許可する。
            _debugState.hideFlags = HideFlags.HideInHierarchy | HideFlags.DontSave;
            _debugSerializedObject = new SerializedObject(_debugState);
        }

        /// <summary> レジストリ操作ボタン、一覧、データInspectorを構成する。 </summary>
        protected override ValueTask Initialize_S(VisualElement root)
        {
            _currentLoaderLabel = root.Q<Label>("save-current-loader");
            _loadedEntriesCountLabel = root.Q<Label>("save-loaded-entries");
            _statusLabel = root.Q<Label>("save-status");
            _editorContainer = root.Q<IMGUIContainer>("save-editor");
            _cacheListView = root.Q<ListView>("save-cache-list");

            root.Q<Button>("save-load").clicked += () => ExecuteAction(LoadSelected);
            root.Q<Button>("save-save").clicked += () => ExecuteAction(SaveSelected);
            root.Q<Button>("save-delete").clicked += () => ExecuteAction(DeleteSelected);

            _editorContainer.onGUIHandler = DrawEditorInspector;

            ConfigureCacheList();
            RefreshTypeList();
            RefreshView(true);

            return default;
        }

        /// <summary>
        ///     SymphonyAdministrator.Update() から毎フレーム呼び出され、Registry の最新状態を表示に反映します。
        ///     PauseWindow / ServiceLocatorWindow と同じ「親から駆動される」方式に合わせています。
        /// </summary>
        public void Update()
        {
            if (_disposed)
            {
                return;
            }

            TryAutoSelectFromRegistry();
            RefreshView(false);
        }

        /// <summary> UIコールバックと一時編集用Unityオブジェクトを破棄する。 </summary>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            if (_editorContainer != null)
            {
                _editorContainer.onGUIHandler = null;
            }

            _debugSerializedObject?.Dispose();
            _debugSerializedObject = null;

            if (_debugState != null)
            {
                UnityEngine.Object.DestroyImmediate(_debugState);
            }
        }

        /// <summary>
        ///     未選択のまま Registry に新たにデータが乗った場合（他のコードが Get/Load/Save した場合など）、
        ///     ここで初めて自動選択します。Get() を呼んで新規インスタンス化することはしません。
        /// </summary>
        private void TryAutoSelectFromRegistry()
        {
            if (_selectedType != null)
            {
                return;
            }

            Type typeToSelect = ResolveAutoSelectType();
            if (typeToSelect == null)
            {
                return;
            }

            ApplyAutoSelection(typeToSelect);
        }

        /// <summary> AppDomain内の対応セーブデータ型一覧を最新状態へ同期する。 </summary>
        private void EnsureTypeListCurrent()
        {
            List<Type> latestTypes = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(GetTypesSafe)
                .Where(IsSupportedSaveDataType)
                .OrderBy(type => type.FullName, StringComparer.Ordinal)
                .ToList();

            bool changed = latestTypes.Count != _saveDataTypes.Count
                || !latestTypes.SequenceEqual(_saveDataTypes);

            if (!changed)
            {
                return;
            }

            _saveDataTypes = latestTypes;
            _registryEntriesSnapshot = null;

            if (_saveDataTypes.Count <= 0)
            {
                _selectedType = null;
                RebindDebugState(null);
                _statusMessage = "プロジェクト内に SaveDataContent を継承したセーブデータ型が見つかりません。";
                _lastViewSignature = null;
                return;
            }

            // _selectedType はドメインリロードで作り直されると null に戻る。その場合、
            // 「まだ誰もインスタンス化していない型」を自動選択して Get() で無理やり
            // インスタンス化させることはしない。Registry に既に乗っているデータ
            // （＝どこかで実際に使われているデータ）があればそれを優先して表示するだけに留める。
            ApplyAutoSelection(_selectedType ?? ResolveAutoSelectType());
        }

        /// <summary>
        ///     解決済みの型（null の場合は「まだ何も選べない」）を実際に選択状態へ反映します。
        ///     Registry にも SessionState にも手がかりが無い場合は、先頭の型を勝手に選んで
        ///     Get() を呼ぶ（＝触られてもいないデータを新規インスタンス化する）ことはせず、
        ///     Registry Cache にデータが現れるまで選択を行いません。
        /// </summary>
        private void ApplyAutoSelection(Type typeToSelect)
        {
            if (typeToSelect == null || !_saveDataTypes.Contains(typeToSelect))
            {
                _selectedType = null;
                RebindDebugState(null);
                _statusMessage = "Registry Cache からセーブデータを選択してください。";
                _lastViewSignature = null;
                return;
            }

            SetSelectedType(typeToSelect);
            BindCurrentSelection();
        }

        /// <summary>
        ///     自動選択の対象を決定します。Registry に既にインスタンス化済みのデータがあれば
        ///     （SessionState に前回選択が残っていればそれを優先しつつ）それを使い、
        ///     何もインスタンス化されていなければ null（自動選択しない）を返します。
        /// </summary>
        private static Type ResolveAutoSelectType()
        {
            IReadOnlyList<SaveDataRegistryEntryInfo> cachedEntries = SaveDataRegistry.GetEntries();
            if (cachedEntries.Count <= 0)
            {
                return null;
            }

            Type sessionType = RestoreSelectedTypeFromSession();
            if (sessionType != null && cachedEntries.Any(entry => entry.DataType == sessionType))
            {
                return sessionType;
            }

            return cachedEntries
                .Select(entry => entry.DataType)
                .OrderBy(type => type.FullName, StringComparer.Ordinal)
                .First();
        }

        /// <summary> 選択型を更新し、ドメインリロード後に復元できるようSessionStateへ保存する。 </summary>
        private void SetSelectedType(Type type)
        {
            if (_selectedType != type)
            {
                _editorScrollPosition = Vector2.zero;
            }

            _selectedType = type;
            SessionState.SetString(SELECTED_TYPE_SESSION_KEY, type?.AssemblyQualifiedName ?? string.Empty);
        }

        /// <summary> SessionStateから前回選択していたセーブデータ型を復元する。 </summary>
        private static Type RestoreSelectedTypeFromSession()
        {
            string typeName = SessionState.GetString(SELECTED_TYPE_SESSION_KEY, string.Empty);
            return string.IsNullOrEmpty(typeName) ? null : Type.GetType(typeName);
        }

        /// <summary> キャッシュ一覧の要素生成、表示内容、選択イベントを構成する。 </summary>
        private void ConfigureCacheList()
        {
            _cacheListView.makeItem = () => new Label();
            _cacheListView.bindItem = (element, index) =>
            {
                SaveDataRegistryEntryInfo entry = (SaveDataRegistryEntryInfo)_cacheListView.itemsSource[index];
                bool isLoaded = entry.Data != null;
                bool isSaved = SaveDataRegistry.Exists(entry.DataType);
                string state = isLoaded
                    ? "Loaded"
                    : isSaved
                        ? "Saved"
                        : "Empty";

                ((Label)element).text = $"{entry.DataType.FullName}\nState: {state} / Date: {entry.SaveDate ?? "(unknown)"}";
            };
            _cacheListView.selectionType = SelectionType.Single;
            _cacheListView.selectionChanged += OnCacheSelectionChanged;
        }

        /// <summary> キャッシュ一覧で選択されたセーブデータ型を編集対象へ反映する。 </summary>
        private void OnCacheSelectionChanged(IEnumerable<object> selectedItems)
        {
            foreach (object selectedItem in selectedItems)
            {
                if (selectedItem is SaveDataRegistryEntryInfo entry)
                {
                    SelectType(entry.DataType);
                }

                return;
            }
        }

        /// <summary> 有効なセーブデータ型を選択して現在のキャッシュへバインドする。 </summary>
        private void SelectType(Type type)
        {
            if (type == null || !_saveDataTypes.Contains(type) || type == _selectedType)
            {
                return;
            }

            SetSelectedType(type);
            BindCurrentSelection();
            RefreshView(true);
        }

        /// <summary> 選択中セーブデータをスクロール可能なInspectorとして描画する。 </summary>
        private void DrawEditorInspector()
        {
            _debugSerializedObject.Update();
            _editorScrollPosition = EditorGUILayout.BeginScrollView(
                _editorScrollPosition,
                GUILayout.ExpandWidth(true),
                GUILayout.ExpandHeight(true));

            try
            {
                SerializedProperty dataProperty = _debugSerializedObject.FindProperty("_data");
                if (dataProperty.managedReferenceValue == null)
                {
                    // SaveDataRegistry.Get() は必ず何らかのインスタンスを返すため、型が選択されていれば
                    // ここには到達しない。到達するのは (a) プロジェクトに SaveDataContent を継承した型が
                    // 一つも無い、または (b) まだ何もインスタンス化されておらず自動選択もしていない場合のみ。
                    // どちらの理由かは _statusMessage 側で出し分けているので、そのままここに表示する。
                    EditorGUILayout.HelpBox(_statusMessage, MessageType.Info);
                }
                else
                {
                    // SaveDataContent.SaveDate は [ReadOnly] なので、再帰描画の中で
                    // SaveDate だけが読み取り専用になり、派生クラスのフィールドは編集できる。
                    EditorGUILayout.PropertyField(dataProperty, new GUIContent("Data"), true);
                }
            }
            finally
            {
                EditorGUILayout.EndScrollView();
            }

            _debugSerializedObject.ApplyModifiedProperties();
        }

        /// <summary> 対応型一覧と管理パネル表示を強制更新する。 </summary>
        private void RefreshTypeList()
        {
            EnsureTypeListCurrent();
            RefreshView(true);
        }

        /// <summary> 選択型のレジストリ正本を一時編集状態へバインドする。 </summary>
        private void BindCurrentSelection()
        {
            if (_selectedType == null)
            {
                return;
            }

            SaveDataContent data = SaveDataRegistry.Get(_selectedType);
            RebindDebugState(data);
            _statusMessage = $"{_selectedType.FullName} の現在インスタンスを表示しています。";
            _lastViewSignature = null;
        }

        /// <summary> 選択中の型を保存先から再ロードして編集状態へ反映する。 </summary>
        private void LoadSelected()
        {
            SaveDataRegistry.LoadAsync(_selectedType).GetAwaiter().GetResult();
            SaveDataContent saveData = SaveDataRegistry.Get(_selectedType);

            RebindDebugState(saveData);
            _statusMessage = $"{_selectedType.FullName} をロードしました。";
            RefreshView(true);
        }

        /// <summary> Inspectorの編集内容をレジストリ正本へ同期して保存する。 </summary>
        private void SaveSelected()
        {
            SaveDataContent editingData = _debugState.GetData();
            if (editingData == null)
            {
                BindCurrentSelection();
                editingData = _debugState.GetData();
            }

            // インスペクタで編集中のインスタンスが [SerializeReference] の再構築などで
            // Registry のキャッシュ本体と別インスタンスになっている可能性があるため、
            // 保存前に編集内容を Registry 側の正本へ同期する（食い違ったまま Save してしまう事故を防ぐ）。
            SaveDataContent canonical = SaveDataRegistry.Get(_selectedType);
            if (!ReferenceEquals(canonical, editingData))
            {
                JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(editingData), canonical);
            }

            SaveDataRegistry.SaveAsync(_selectedType).GetAwaiter().GetResult();
            SaveDataContent saveData = SaveDataRegistry.Get(_selectedType);
            RebindDebugState(saveData);
            _statusMessage = $"{_selectedType.FullName} を保存しました。";
            RefreshView(true);
        }

        /// <summary> 確認後に選択型の保存データを削除し、現在インスタンスを初期化する。 </summary>
        private void DeleteSelected()
        {
            if (!EditorUtility.DisplayDialog(
                    "Delete Save Data",
                    $"{_selectedType.FullName} の保存データを削除しますか？",
                    "Delete",
                    "Cancel"))
            {
                return;
            }

            SaveDataRegistry.DeleteAsync(_selectedType).GetAwaiter().GetResult();
            SaveDataContent regenerated = SaveDataRegistry.Get(_selectedType);
            RebindDebugState(regenerated);
            _statusMessage = $"{_selectedType.FullName} の保存データを削除し、現在インスタンスを初期化しました。";
            RefreshView(true);
        }

        /// <summary>
        ///     デバッグインスペクタが表示するインスタンスを Registry の正本に合わせて再バインドします。
        ///     [SerializeReference] フィールドは参照先の入れ替えを SerializedObject.Update() だけでは
        ///     確実に検知できない場合があるため、SerializedObject 自体を作り直して確実に反映します。
        /// </summary>
        private void RebindDebugState(SaveDataContent data)
        {
            if (_disposed)
            {
                return;
            }

            _debugSerializedObject?.Dispose();
            _debugState.SetData(data);
            _debugSerializedObject = new SerializedObject(_debugState);
        }

        /// <summary> 選択状態を検証し、管理パネル操作中の例外をステータス表示へ変換する。 </summary>
        private void ExecuteAction(Action action)
        {
            if (_selectedType == null)
            {
                _statusMessage = "Registry Cache からセーブデータを選択してください。";
                RefreshView(false);
                return;
            }

            try
            {
                action();
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                _statusMessage = ex.Message;
                RefreshView(true);
            }
        }

        /// <summary> 表示署名が変わった場合、または強制指定時に管理パネルを更新する。 </summary>
        private void RefreshView(bool forceEditorRepaint)
        {
            List<SaveDataRegistryEntryInfo> entries = GetSortedEntries();
            string currentLoaderText = $"Current Loader: {SaveDataRegistry.GetCurrentLoader().GetType().Name}";
            string loadedEntriesText = $"Visible Entries: {entries.Count}";
            string signature = BuildViewSignature(entries, currentLoaderText, loadedEntriesText, _statusMessage);

            if (!forceEditorRepaint && signature == _lastViewSignature)
            {
                return;
            }

            _lastViewSignature = signature;
            _currentLoaderLabel.text = currentLoaderText;
            _loadedEntriesCountLabel.text = loadedEntriesText;
            _statusLabel.text = _statusMessage;
            _cacheListView.itemsSource = entries;
            _cacheListView.Rebuild();
            SyncCacheSelection(entries);

            if (forceEditorRepaint)
            {
                _editorContainer.MarkDirtyRepaint();
            }
        }

        /// <summary> 現在の選択型に対応する一覧行を通知なしで選択状態へ同期する。 </summary>
        private void SyncCacheSelection(IReadOnlyList<SaveDataRegistryEntryInfo> entries)
        {
            int selectedEntryIndex = -1;
            for (int index = 0; index < entries.Count; index++)
            {
                if (entries[index].DataType == _selectedType)
                {
                    selectedEntryIndex = index;
                    break;
                }
            }

            if (selectedEntryIndex < 0)
            {
                _cacheListView.SetSelectionWithoutNotify(Array.Empty<int>());
                return;
            }

            _cacheListView.SetSelectionWithoutNotify(new[] { selectedEntryIndex });
        }

        /// <summary> 不要なUI再構築を避けるため、現在の表示内容を表す署名を生成する。 </summary>
        private static string BuildViewSignature(
            IReadOnlyList<SaveDataRegistryEntryInfo> entries,
            string currentLoaderText,
            string loadedEntriesText,
            string statusMessage)
        {
            StringBuilder builder = new();
            builder.Append(currentLoaderText)
                .Append('|')
                .Append(loadedEntriesText)
                .Append('|')
                .Append(statusMessage);

            foreach (SaveDataRegistryEntryInfo entry in entries)
            {
                builder.Append('|')
                    .Append(entry.DataType.AssemblyQualifiedName)
                    .Append(':')
                    .Append(entry.SaveDate)
                    .Append(':')
                    .Append(entry.Data == null ? 0 : RuntimeHelpers.GetHashCode(entry.Data));
            }

            return builder.ToString();
        }

        /// <summary> 対応型の順序に揃えた保存済みまたはキャッシュ済みエントリ一覧を取得する。 </summary>
        private List<SaveDataRegistryEntryInfo> GetSortedEntries()
        {
            IReadOnlyList<SaveDataRegistryEntryInfo> registryEntries = SaveDataRegistry.GetEntries();
            if (ReferenceEquals(registryEntries, _registryEntriesSnapshot))
            {
                return _sortedEntries;
            }

            _registryEntriesSnapshot = registryEntries;
            Dictionary<Type, SaveDataRegistryEntryInfo> loadedEntries = registryEntries
                .ToDictionary(entry => entry.DataType);

            List<SaveDataRegistryEntryInfo> entries = new(_saveDataTypes.Count);
            foreach (Type saveDataType in _saveDataTypes)
            {
                if (loadedEntries.TryGetValue(saveDataType, out SaveDataRegistryEntryInfo loadedEntry))
                {
                    entries.Add(loadedEntry);
                    continue;
                }

                if (SaveDataRegistry.Exists(saveDataType))
                {
                    entries.Add(new SaveDataRegistryEntryInfo(saveDataType, null));
                    continue;
                }
            }

            _sortedEntries = entries;
            return _sortedEntries;
        }

        /// <summary> 一部の型をロードできないAssemblyからも取得可能な型だけを列挙する。 </summary>
        private static IEnumerable<Type> GetTypesSafe(Assembly assembly)
        {
            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                return ex.Types.Where(type => type != null);
            }
        }

        /// <summary> 管理パネルで生成・編集できるセーブデータ具象型か検証する。 </summary>
        private static bool IsSupportedSaveDataType(Type type)
        {
            if (type == null
                || !type.IsClass
                || type.IsAbstract
                || type.IsGenericTypeDefinition
                || typeof(UnityEngine.Object).IsAssignableFrom(type))
            {
                return false;
            }

            if (type.GetConstructor(Type.EmptyTypes) == null)
            {
                return false;
            }

            if (!typeof(SaveDataContent).IsAssignableFrom(type))
            {
                return false;
            }

            return type.IsDefined(typeof(SerializableAttribute), false);
        }

    }
}
