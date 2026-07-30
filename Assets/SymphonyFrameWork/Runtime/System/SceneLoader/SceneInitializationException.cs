using System;

namespace SymphonyFrameWork.System.SceneLoad
{
    /// <summary> ロードしたシーンのルートオブジェクト初期化に失敗した場合に発生する例外。 </summary>
    public sealed class SceneInitializationException : Exception
    {
        /// <summary> 初期化に失敗したシーンと対象を指定して例外を生成する。 </summary>
        /// <param name="sceneName"> 初期化中だったシーン名。 </param>
        /// <param name="gameObjectName"> 初期化対象のルートGameObject名。 </param>
        /// <param name="initializerType"> DIまたは非同期初期化を実行していた型。 </param>
        /// <param name="innerException"> 原因となった例外。 </param>
        public SceneInitializationException(
            string sceneName,
            string gameObjectName,
            Type initializerType,
            Exception innerException)
            : base(
                $"[{nameof(SceneLoader)}] シーン {sceneName} のルートオブジェクト {gameObjectName} を初期化できませんでした。"
                + $" Initializer: {initializerType?.FullName ?? "(null)"}",
                innerException)
        {
            SceneName = sceneName;
            GameObjectName = gameObjectName;
            InitializerType = initializerType;
        }

        /// <summary> 初期化中だったシーン名。 </summary>
        public string SceneName { get; }

        /// <summary> 初期化対象のルートGameObject名。 </summary>
        public string GameObjectName { get; }

        /// <summary> DIまたは非同期初期化を実行していた型。 </summary>
        public Type InitializerType { get; }
    }
}
