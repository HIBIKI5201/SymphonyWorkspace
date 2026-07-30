using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace SymphonyFrameWork.System.SaveSystem
{
    /// <summary>
    ///     PlayerPrefsへのJSON入出力を提供するLoader基底クラスです。
    /// </summary>
    [Serializable]
    public abstract class PlayerPrefsSaveDataLoader : SaveDataLoader
    {
        /// <summary> PlayerPrefsに型固有のキーが存在するか確認する。 </summary>
        protected override bool ExistsCore(Type dataType)
        {
            return PlayerPrefs.HasKey(GetKey(dataType));
        }

        /// <summary> PlayerPrefsから型固有のJSONを読み込む。 </summary>
        protected override ValueTask<string> LoadJsonAsync(Type dataType, CancellationToken token)
        {
            return new ValueTask<string>(PlayerPrefs.GetString(GetKey(dataType)));
        }

        /// <summary> PlayerPrefsへ型固有のJSONを書き込む。 </summary>
        protected override ValueTask SaveJsonAsync(Type dataType, string json, CancellationToken token)
        {
            PlayerPrefs.SetString(GetKey(dataType), json);
            PlayerPrefs.Save();
            return default;
        }

        /// <summary> PlayerPrefsから型固有の保存値を削除する。 </summary>
        protected override ValueTask DeleteCoreAsync(Type dataType, CancellationToken token)
        {
            PlayerPrefs.DeleteKey(GetKey(dataType));
            PlayerPrefs.Save();
            return default;
        }

        /// <summary> 型の完全修飾名からPlayerPrefsキーを生成する。 </summary>
        private static string GetKey(Type dataType) => dataType.FullName;
    }
}
