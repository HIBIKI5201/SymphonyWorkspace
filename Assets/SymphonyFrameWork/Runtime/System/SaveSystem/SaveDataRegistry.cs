using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using SymphonyFrameWork.Exceptions;

namespace SymphonyFrameWork.System.SaveSystem
{
    /// <summary>
    ///     プロジェクト設定に従ってセーブデータを一括管理するレジストリです。
    /// </summary>
    public static class SaveDataRegistry
    {
        /// <summary> 指定型の永続化データが存在するか確認する。 </summary>
        /// <exception cref="SaveDataOperationException"> ローダーまたは保存先で存在確認に失敗した場合。 </exception>
        public static bool Exists<T>() where T : SaveDataContent, new()
        {
            return Exists(typeof(T));
        }

        /// <summary> 指定型の永続化データが存在するか確認する。 </summary>
        /// <exception cref="SaveDataOperationException"> ローダーまたは保存先で存在確認に失敗した場合。 </exception>
        public static bool Exists(Type dataType)
        {
            ValidateDataType(dataType);
            SaveDataLoader loader = GetLoader();

            try
            {
                return loader.Exists(dataType);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (SaveDataOperationException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new SaveDataOperationException(
                    SaveDataOperation.Exists,
                    dataType,
                    loader.GetType(),
                    ex);
            }
        }

        /// <summary> 指定型のキャッシュまたは保存済みデータを同期的に取得する。 </summary>
        /// <exception cref="SaveDataOperationException"> 初回の同期読み込みに失敗した場合。 </exception>
        public static T Get<T>() where T : SaveDataContent, new()
        {
            return (T)Get(typeof(T));
        }

        /// <summary>
        ///     Registry が保持している現在のインスタンスを取得します。
        ///     キャッシュが無い初回アクセス時は、自動的に永続化データをロードします。
        /// </summary>
        /// <exception cref="SaveDataOperationException"> 初回の同期読み込みに失敗した場合。 </exception>
        public static SaveDataContent Get(Type dataType)
        {
            ValidateDataType(dataType);

            SaveDataContent data = GetOrCreateCache(dataType);

            if (!IsLoaded(dataType))
            {
                LoadAsync(dataType).GetAwaiter().GetResult();
            }

            return data;
        }

        /// <summary> 指定型の保存済みデータをキャッシュへ非同期に読み込む。 </summary>
        /// <exception cref="SaveDataOperationException"> ローダーまたは保存先で読み込みに失敗した場合。 </exception>
        /// <exception cref="OperationCanceledException"> 呼び出し側から処理が中断された場合。 </exception>
        public static async ValueTask<T> LoadAsync<T>(CancellationToken token = default) where T : SaveDataContent, new()
        {
            await LoadAsync(typeof(T), token);
            return Get<T>();
        }

        /// <summary> 指定型の保存済みデータをキャッシュへ非同期に読み込む。 </summary>
        /// <exception cref="SaveDataOperationException"> ローダーまたは保存先で読み込みに失敗した場合。 </exception>
        /// <exception cref="OperationCanceledException"> 呼び出し側から処理が中断された場合。 </exception>
        public static ValueTask LoadAsync(Type dataType, CancellationToken token = default)
        {
            ValidateDataType(dataType);
            SaveDataContent current = GetOrCreateCache(dataType);

            lock (_lock)
            {
                if (_loadingTasks.TryGetValue(dataType, out Task loadingTask))
                {
                    return new ValueTask(loadingTask);
                }

                Task loadTask = LoadInternalAsync(dataType, current, token);

                // 既定のローダー（PlayerPrefs ベース）は同期的に完了するため、この時点で
                // loadTask は既に完了しており、LoadInternalAsync の finally による
                // 自己解除もすでに実行済みである。ここで無条件に登録すると、完了済みの
                // 古いタスクが _loadingTasks に残り続け、以降の LoadAsync 呼び出しが
                // すべて「重複リクエスト」とみなされて実際のロードが二度と走らなくなる
                // （＝ Load しても保存済みデータが読み込まれない）バグになる。
                // 未完了（本当に非同期I/Oを行うローダー）の場合のみ重複排除用に登録する。
                if (!loadTask.IsCompleted)
                {
                    _loadingTasks[dataType] = loadTask;
                }

                return new ValueTask(loadTask);
            }
        }

        /// <summary> 指定型のキャッシュを保存先へ非同期に書き込む。 </summary>
        /// <exception cref="SaveDataOperationException"> ローダーまたは保存先で保存に失敗した場合。 </exception>
        /// <exception cref="OperationCanceledException"> 呼び出し側から処理が中断された場合。 </exception>
        public static ValueTask SaveAsync<T>(CancellationToken token = default) where T : SaveDataContent, new()
        {
            return SaveAsync(typeof(T), token);
        }

        /// <summary> 指定型のキャッシュを保存先へ非同期に書き込む。 </summary>
        /// <exception cref="SaveDataOperationException"> ローダーまたは保存先で保存に失敗した場合。 </exception>
        /// <exception cref="OperationCanceledException"> 呼び出し側から処理が中断された場合。 </exception>
        public static async ValueTask SaveAsync(Type dataType, CancellationToken token = default)
        {
            ValidateDataType(dataType);
            SaveDataContent data = GetOrCreateCache(dataType);
            SaveDataLoader loader = GetLoader();
            await ExecuteLoaderOperationAsync(
                SaveDataOperation.Save,
                dataType,
                loader,
                () => loader.SaveAsync(dataType, data, token));
            MarkLoaded(dataType, data);
        }

        /// <summary> 指定型の保存済みデータを削除し、キャッシュを既定値へ戻す。 </summary>
        /// <exception cref="SaveDataOperationException"> ローダーまたは保存先で削除または再読み込みに失敗した場合。 </exception>
        /// <exception cref="OperationCanceledException"> 呼び出し側から処理が中断された場合。 </exception>
        public static async ValueTask DeleteAsync<T>(CancellationToken token = default) where T : SaveDataContent, new()
        {
            await DeleteAsync(typeof(T), token);
        }

        /// <summary> 指定型の保存済みデータを削除し、キャッシュを既定値へ戻す。 </summary>
        /// <exception cref="SaveDataOperationException"> ローダーまたは保存先で削除または再読み込みに失敗した場合。 </exception>
        /// <exception cref="OperationCanceledException"> 呼び出し側から処理が中断された場合。 </exception>
        public static async ValueTask DeleteAsync(Type dataType, CancellationToken token = default)
        {
            ValidateDataType(dataType);
            SaveDataContent current = GetOrCreateCache(dataType);

            lock (_lock)
            {
                _loadedTypes.Remove(dataType);
            }

            SaveDataLoader loader = GetLoader();
            await ExecuteLoaderOperationAsync(
                SaveDataOperation.Delete,
                dataType,
                loader,
                () => loader.DeleteAsync(dataType, token));
            await ExecuteLoaderOperationAsync(
                SaveDataOperation.Load,
                dataType,
                loader,
                () => loader.LoadAsync(dataType, current, token));
            MarkLoaded(dataType, current);
        }

        /// <summary> 現在キャッシュされている全エントリの読み取り専用スナップショットを取得する。 </summary>
        public static IReadOnlyList<SaveDataRegistryEntryInfo> GetEntries()
        {
            lock (_lock)
            {
                if (!_entrySnapshotDirty)
                {
                    return _entrySnapshot;
                }

                List<SaveDataRegistryEntryInfo> entries = new(_cache.Count);
                foreach ((Type type, SaveDataContent saveData) in _cache)
                {
                    entries.Add(new SaveDataRegistryEntryInfo(type, saveData));
                }

                _entrySnapshot = entries.AsReadOnly();
                _entrySnapshotDirty = false;
                return _entrySnapshot;
            }
        }

        /// <summary>
        ///     ローダーとキャッシュを破棄し、次回アクセス時にConfigから再解決する。
        ///     Configを編集するEditorのProject Settings画面から呼び出す。
        /// </summary>
        internal static void RefreshLoader()
        {
            ResetRuntimeState();
        }

        /// <summary>
        ///     Compositionが解決したローダーの取得処理を設定する。
        /// </summary>
        /// <param name="loaderResolver"> 現在のConfigに対応するローダーを返す処理。 </param>
        internal static void ConfigureLoaderResolver(
            Func<SaveDataLoader> loaderResolver)
        {
            _loaderResolver = loaderResolver
                ?? throw new ArgumentNullException(nameof(loaderResolver));
            ResetRuntimeState();
        }

        /// <summary> Domain Reloadの有無に依存しないようランタイム状態を初期化する。 </summary>
        internal static void ResetRuntimeState()
        {
            _cachedLoader = null;
            ClearCache();
        }

        /// <summary>
        ///     現在選択されているローダーを取得する。
        ///     Adaptorが選んだ実装は公開APIへ出さず、状態を表示するEditorウィンドウからのみ参照する。
        /// </summary>
        internal static SaveDataLoader GetCurrentLoader() => GetLoader();

        /// <summary>
        ///     ロードを発生させずにキャッシュ済みインスタンスを取得します。無ければ既定値で作成します。
        /// </summary>
        private static SaveDataContent GetOrCreateCache(Type dataType)
        {
            lock (_lock)
            {
                if (_cache.TryGetValue(dataType, out SaveDataContent cached) && cached != null)
                {
                    return cached;
                }

                SaveDataContent created = (SaveDataContent)Activator.CreateInstance(dataType);
                _cache[dataType] = created;
                _entrySnapshotDirty = true;
                return created;
            }
        }

        /// <summary> 指定型がキャッシュへ読み込み済みか確認する。 </summary>
        private static bool IsLoaded(Type dataType)
        {
            lock (_lock)
            {
                return _loadedTypes.Contains(dataType);
            }
        }

        /// <summary> キャッシュが同一インスタンスの場合だけ指定型を読み込み済みとして記録する。 </summary>
        private static void MarkLoaded(Type dataType, SaveDataContent data)
        {
            lock (_lock)
            {
                if (_cache.TryGetValue(dataType, out SaveDataContent cached)
                    && ReferenceEquals(cached, data))
                {
                    _loadedTypes.Add(dataType);
                }
            }
        }

        /// <summary> 重複ロード管理の後始末を保証しながら対象データを読み込む。 </summary>
        private static async Task LoadInternalAsync(Type dataType, SaveDataContent current, CancellationToken token)
        {
            try
            {
                SaveDataLoader loader = GetLoader();
                await ExecuteLoaderOperationAsync(
                    SaveDataOperation.Load,
                    dataType,
                    loader,
                    () => loader.LoadAsync(dataType, current, token));
                MarkLoaded(dataType, current);
            }
            finally
            {
                lock (_lock)
                {
                    _loadingTasks.Remove(dataType);
                }
            }
        }

        /// <summary> ローダー操作へセーブデータ型とローダー型の文脈を付けて実行する。 </summary>
        /// <param name="operation"> 実行する操作。 </param>
        /// <param name="dataType"> 操作対象のセーブデータ型。 </param>
        /// <param name="loader"> 操作に使用するローダー。 </param>
        /// <param name="execute"> 実行するローダー処理。 </param>
        private static async ValueTask ExecuteLoaderOperationAsync(
            SaveDataOperation operation,
            Type dataType,
            SaveDataLoader loader,
            Func<ValueTask> execute)
        {
            try
            {
                await execute();
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (SaveDataOperationException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new SaveDataOperationException(
                    operation,
                    dataType,
                    loader.GetType(),
                    ex);
            }
        }

        /// <summary> キャッシュ済みローダーを返し、未解決の場合はCompositionのresolverから取得する。 </summary>
        private static SaveDataLoader GetLoader()
        {
            if (_cachedLoader != null)
            {
                return _cachedLoader;
            }

            if (_loaderResolver == null)
            {
                throw new SymphonyNotInitializedException(typeof(SaveDataRegistry));
            }

            _cachedLoader = _loaderResolver();
            if (_cachedLoader == null)
            {
                throw new InvalidOperationException(
                    $"[{nameof(SaveDataRegistry)}] ローダーの解決結果がnullです。");
            }

            return _cachedLoader;
        }

        /// <summary> レジストリで扱えるデフォルトコンストラクタ付き具象型か検証する。 </summary>
        private static void ValidateDataType(Type dataType)
        {
            if (dataType == null)
            {
                throw new ArgumentNullException(nameof(dataType));
            }

            if (!dataType.IsClass || dataType.IsAbstract || dataType.IsGenericTypeDefinition)
            {
                throw new ArgumentException("セーブ対象は new() 可能な具象クラスにしてください。", nameof(dataType));
            }

            if (dataType.GetConstructor(Type.EmptyTypes) == null)
            {
                throw new ArgumentException("デフォルトコンストラクタが必要です。", nameof(dataType));
            }

            if (!typeof(SaveDataContent).IsAssignableFrom(dataType))
            {
                throw new ArgumentException($"{nameof(SaveDataContent)} を継承した型を指定してください。", nameof(dataType));
            }
        }

        /// <summary> キャッシュされたデータを破棄し、ロード管理状態を消去する。 </summary>
        private static void ClearCache()
        {
            lock (_lock)
            {
                foreach (SaveDataContent saveData in _cache.Values)
                {
                    saveData?.Dispose();
                }

                _cache.Clear();
                _loadedTypes.Clear();
                _loadingTasks.Clear();
                _entrySnapshotDirty = true;
            }
        }

        private static readonly object _lock = new();
        private static readonly Dictionary<Type, SaveDataContent> _cache = new();
        private static readonly HashSet<Type> _loadedTypes = new();
        private static readonly Dictionary<Type, Task> _loadingTasks = new();

        private static IReadOnlyList<SaveDataRegistryEntryInfo> _entrySnapshot = Array.Empty<SaveDataRegistryEntryInfo>();
        private static bool _entrySnapshotDirty = true;

        private static Func<SaveDataLoader> _loaderResolver;
        private static SaveDataLoader _cachedLoader;
    }
}
