using System.Threading.Tasks;
using System;

namespace SymphonyFrameWork.System.SaveSystem
{
    /// <summary>
    ///     Json.NET を使ってデータをセーブする互換ラッパーです。
    /// </summary>
    /// <typeparam name="T"> 保存するセーブデータの型。 </typeparam>
    [Obsolete("Use NewtonsoftSaveDataLoader instead.")]
    public sealed class NugetDataLoader<T> : ISaveDataLoader<T>
        where T : SaveDataContent, new()
    {
        private static readonly NewtonsoftSaveDataLoader s_Loader = new();

        /// <summary> Newtonsoft.Json互換ローダーでデータを保存する。 </summary>
        public ValueTask<T> Save(T data)
        {
            return SaveInternalAsync(data);
        }

        /// <summary> Newtonsoft.Json互換ローダーでデータを読み込む。 </summary>
        public ValueTask<T> Load()
        {
            return LoadInternalAsync();
        }

        /// <summary> 共通ローダーAPIへ処理を委譲してデータを保存する。 </summary>
        private static async ValueTask<T> SaveInternalAsync(T data)
        {
            await s_Loader.SaveAsync(typeof(T), data);
            return data;
        }

        /// <summary> 新規インスタンスへ保存済みデータを復元する。 </summary>
        private static async ValueTask<T> LoadInternalAsync()
        {
            T data = new();
            await s_Loader.LoadAsync(typeof(T), data);
            return data;
        }
    }
}
