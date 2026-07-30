using System;

namespace SymphonyFrameWork.System.SaveSystem
{
    /// <summary> レジストリにキャッシュされたセーブデータと型の組を表す。 </summary>
    public readonly struct SaveDataRegistryEntryInfo
    {
        /// <summary>
        ///     セーブデータ型とキャッシュインスタンスから情報を生成する。
        ///     生成は<see cref="SaveDataRegistry" />の責務であり、利用側は<see cref="SaveDataRegistry.GetEntries" />で取得する。
        /// </summary>
        internal SaveDataRegistryEntryInfo(Type dataType, SaveDataContent data)
        {
            DataType = dataType;
            Data = data;
        }

        /// <summary> キャッシュされたセーブデータの型。 </summary>
        public Type DataType { get; }

        /// <summary> キャッシュされているセーブデータ。 </summary>
        public SaveDataContent Data { get; }

        /// <summary> キャッシュされているセーブデータの最終保存日時。 </summary>
        public string SaveDate => Data?.SaveDate;
    }
}
