using System.Collections.Generic;
using UnityEngine;

namespace SymphonyFrameWork.Config
{
    /// <summary>
    ///     シーンマネージャーのコンフィグを格納する
    /// </summary>
    internal sealed class SceneManagerConfig : ScriptableObject
    {
        /// <summary> 再生開始時にシーンを整理して初期シーンをロードするかを示す。 </summary>
        public bool IsResetAndLoadOnPlay => _isResetAndLoadOnPlay;

        /// <summary> 起動時にロードするシーン名の一覧。 </summary>
        public string[] InitializeSceneList => _initializeSceneList;

        /// <summary> 起動時のシーン整理でアンロードしないシーン名の一覧。 </summary>
        public string[] ResetIgnoreSceneList => _resetIgnoreSceneList;

        [SerializeField, Tooltip("エディタでの再生時にシーンをリセットしてロードを実行するか")]
        private bool _isResetAndLoadOnPlay;

        [SerializeField, Tooltip("初期化時にロードするシーン")]
        private string[] _initializeSceneList;
        [SerializeField, Tooltip("リセットしてもアンロードされないシーン")]
        private string[] _resetIgnoreSceneList;
    }
}
