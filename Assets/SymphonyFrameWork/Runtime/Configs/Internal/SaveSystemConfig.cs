using SymphonyFrameWork.Attribute;
using SymphonyFrameWork.System.SaveSystem;
using System;
using UnityEngine;

namespace SymphonyFrameWork
{
    /// <summary> セーブデータのシリアライズ方式と保存先を選択する。 </summary>
    [Serializable]
    internal sealed class SaveSystemConfig : ScriptableObject
    {
        /// <summary> 現在選択されているセーブデータローダー。 </summary>
        public SaveDataLoader Loader => _loader;

        [SerializeReference, SubclassSelector, Tooltip("セーブデータの変換と永続化を担当するローダー。")]
        private SaveDataLoader _loader = new JsonUtilitySaveDataLoader();
    }
}
