namespace SymphonyFrameWork.System.SaveSystem
{
    /// <summary> セーブデータに対して実行した操作の種類。 </summary>
    public enum SaveDataOperation
    {
        /// <summary> 永続化データの存在確認。 </summary>
        Exists,

        /// <summary> 永続化データの読み込み。 </summary>
        Load,

        /// <summary> 永続化データの保存。 </summary>
        Save,

        /// <summary> 永続化データの削除。 </summary>
        Delete
    }
}
