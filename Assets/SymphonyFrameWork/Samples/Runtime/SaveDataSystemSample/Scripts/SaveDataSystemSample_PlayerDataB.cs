using SymphonyFrameWork.System.SaveSystem;
using System;

namespace SymphonyFrameWork.Samples.SaveDataSystemSample
{
    /// <summary> 所持アイテムID一覧を保存するサンプルデータ。 </summary>
    [Serializable]
    public sealed class SaveDataSystemSample_PlayerDataB : SaveDataContent
    {
        /// <summary> 保存対象となる所持アイテムID一覧。 </summary>
        public int[] ItemIDs = Array.Empty<int>();
    }
}
