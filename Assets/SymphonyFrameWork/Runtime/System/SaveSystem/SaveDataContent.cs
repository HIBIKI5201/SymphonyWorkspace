using System;
using Newtonsoft.Json;
using SymphonyFrameWork.Attribute;
using UnityEngine;

namespace SymphonyFrameWork.System.SaveSystem
{
    /// <summary> 保存日時を含むセーブデータの基底型。 </summary>
    [Serializable]
    public abstract class SaveDataContent : IDisposable
    {
        /// <summary> ISO 8601形式で記録された最終保存日時。 </summary>
        [ReadOnly]
        public string SaveDate;

        /// <summary> セーブデータが保持する基底状態を破棄する。 </summary>
        public virtual void Dispose()
        {
            SaveDate = null;
        }

        /// <summary>
        ///     最終保存日時を指定値または現在日時で更新する。
        ///     保存日時は<see cref="SaveDataLoader" />がライフサイクルとして管理するため、利用側からは更新できない。
        /// </summary>
        /// <param name="saveDate"> 記録する日時。既定値の場合は現在日時。 </param>
        internal void UpdateSaveDate(DateTime saveDate = default)
        {
            if (saveDate == default)
            {
                saveDate = DateTime.Now;
            }

            SaveDate = saveDate.ToString("O");
        }

        /// <summary> 記録されている保存日時を消去する。 </summary>
        internal void ClearSaveDate()
        {
            SaveDate = null;
        }
    }
}
