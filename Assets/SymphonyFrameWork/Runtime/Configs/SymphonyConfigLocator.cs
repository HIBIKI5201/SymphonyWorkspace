using SymphonyFrameWork.Core;
using UnityEngine;

namespace SymphonyFrameWork.Config
{
    /// <summary> Symphony Frameworkの設定アセットに対するパス解決とロードを提供する。 </summary>
    public static class SymphonyConfigLocator
    {
        /// <summary> 指定した設定型のResources内ファイル名を取得する。 </summary>
        public static string GetConfigPathInResources<T>() where T : ScriptableObject =>
            $"{typeof(T).Name}.asset";

        /// <summary>
        ///     それぞれのパスを取得する
        /// </summary>
        /// <typeparam name="T"> パスを取得する設定アセットの型。 </typeparam>
        /// <returns> Framework内の設定アセットパス。 </returns>
        public static string GetFullPath<T>() where T : ScriptableObject => 
            $"{SymphonyConstant.RESOURCES_RUNTIME_PATH}/{GetConfigPathInResources<T>()}";
        
        /// <summary>
        ///     指定した型のアセットを取得する
        /// </summary>
        /// <typeparam name="T"> 取得する設定アセットの型。 </typeparam>
        /// <returns> ロードした設定アセット。見つからない場合はnull。 </returns>
        public static T GetConfig<T>() where T : ScriptableObject
        {
            var path = $"{SymphonyConstant.SYMPHONY_FRAMEWORK}/{typeof(T).Name}";
            if (path == null) return null;
            
            return Resources.Load<T>(path);
        }
    }
}
