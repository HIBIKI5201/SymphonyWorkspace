using System.Threading.Tasks;
using System;

namespace SymphonyFrameWork.System.SaveSystem
{
    /// <summary>
    ///     旧来の型ごとローダーAPIです。
    /// </summary>
    [Obsolete("Use SaveDataLoader instead.")]
    public interface ISaveDataLoader<T>
        where T : SaveDataContent, new()
    {
        /// <summary> 保存先からセーブデータを読み込む。 </summary>
        ValueTask<T> Load();

        /// <summary> 指定したセーブデータを保存する。 </summary>
        ValueTask<T> Save(T data);
    }
}
