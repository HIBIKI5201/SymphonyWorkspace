using UnityEditor;

namespace SymphonyFrameWork.Editor
{
    /// <summary> Editor用ScriptableSingleton設定へのアクセスを提供する。 </summary>
    public static class SymphonyEditorConfigLocator
    {
        /// <summary>
        ///     指定した型のアセットを取得する
        /// </summary>
        /// <typeparam name="T"> 取得するEditor設定の型。 </typeparam>
        /// <returns> 対象型の共有設定インスタンス。 </returns>
        public static T GetConfig<T>() where T : ScriptableSingleton<T>
        {
            return ScriptableSingleton<T>.instance;
        }
    }
}
