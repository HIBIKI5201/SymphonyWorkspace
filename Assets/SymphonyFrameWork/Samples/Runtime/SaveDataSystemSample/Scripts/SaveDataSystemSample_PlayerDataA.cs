using SymphonyFrameWork.System.SaveSystem;
using System;

namespace SymphonyFrameWork.Samples.SaveDataSystemSample
{
    /// <summary> プレイヤー名、レベル、所持金を保存するサンプルデータ。 </summary>
    [Serializable]
    public sealed class SaveDataSystemSample_PlayerDataA : SaveDataContent
    {
        /// <summary> 編集可能なプレイヤー名。 </summary>
        public string PlayerName = "Symphony";

        /// <summary> 編集可能なプレイヤーレベル。 </summary>
        public int Level = 1;

        /// <summary> 編集可能な所持金。 </summary>
        public int Gold = 100;
    }
}
