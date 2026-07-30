using Newtonsoft.Json;
using System;

namespace SymphonyFrameWork.System.SaveSystem
{
    /// <summary>
    ///     Newtonsoft.Json と PlayerPrefs を利用するセーブデータローダーです。
    /// </summary>
    [Serializable]
    internal sealed class NewtonsoftSaveDataLoader : PlayerPrefsSaveDataLoader
    {
        /// <summary> Newtonsoft.JsonでセーブデータをJSONへ変換する。 </summary>
        protected override string SerializeToJson(Type dataType, SaveDataContent data)
        {
            return JsonConvert.SerializeObject(data, Formatting.Indented);
        }

        /// <summary> Newtonsoft.JsonでJSONを既存インスタンスへ上書きする。 </summary>
        protected override void OverwriteFromJson(Type dataType, string json, SaveDataContent data)
        {
            JsonConvert.PopulateObject(json, data, _settings);
        }

        private static readonly JsonSerializerSettings _settings = new()
        {
            ObjectCreationHandling = ObjectCreationHandling.Replace
        };
    }
}
