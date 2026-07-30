using SymphonyFrameWork.System.SaveSystem;
using UnityEngine;

namespace SymphonyFrameWork.Editor
{
    /// <summary> SerializedObjectでセーブデータを編集するための一時コンテナ。 </summary>
    public sealed class SaveDataDebugState : ScriptableObject
    {
        [SerializeReference, Tooltip("管理パネルで表示・編集するセーブデータの一時参照。")]
        private SaveDataContent _data;

        /// <summary> 現在保持しているデバッグ対象データを取得する。 </summary>
        public SaveDataContent GetData() => _data;

        /// <summary> 管理パネルで表示・編集するデータを設定する。 </summary>
        public void SetData(SaveDataContent data)
        {
            _data = data;
        }
    }
}
