using System;
using UnityEngine;

namespace SymphonyFrameWork.System.SaveSystem
{
    /// <summary>
    ///     JsonUtility と PlayerPrefs を利用するセーブデータローダーです。
    /// </summary>
    [Serializable]
    internal sealed class JsonUtilitySaveDataLoader : PlayerPrefsSaveDataLoader
    {
        /// <summary> Unity JsonUtilityでセーブデータをJSONへ変換する。 </summary>
        protected override string SerializeToJson(Type dataType, SaveDataContent data)
        {
            return JsonUtility.ToJson(data, true);
        }

        /// <summary> Unity JsonUtilityでJSONを既存インスタンスへ上書きする。 </summary>
        protected override void OverwriteFromJson(Type dataType, string json, SaveDataContent data)
        {
            JsonUtility.FromJsonOverwrite(json, data);
        }
    }
}
