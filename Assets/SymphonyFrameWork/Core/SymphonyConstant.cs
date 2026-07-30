namespace SymphonyFrameWork.Core
{
    /// <summary>
    ///     ランタイム用の定数値を持つ
    /// </summary>
    public static class SymphonyConstant
    {
        /// <summary> Unity Package Managerで使用するパッケージ名。 </summary>
        public const string SYMPHONY_PACKAGE = "symphonyframework";

        /// <summary> Frameworkの表示名およびAssets配下のルート名。 </summary>
        public const string SYMPHONY_FRAMEWORK = "SymphonyFrameWork";

#if UNITY_EDITOR
        /// <summary>
        ///     Frameworkが実際に配置されている絶対パス（実ファイルパス）を取得する。
        ///     UPM経由（Library/PackageCache等）とAssets直置きの両方に対応する。
        /// </summary>
        /// <returns> Frameworkルートフォルダの絶対パス。 </returns>
        public static string GetFrameworkAbsolutePath()
        {
            var packageInfo = UnityEditor.PackageManager.PackageInfo
                .FindForAssembly(typeof(SymphonyConstant).Assembly);

            if (packageInfo != null) return packageInfo.resolvedPath;

            // パッケージとして認識されない場合はAssets直下に配置されているとみなす。
            return System.IO.Path.Combine(UnityEngine.Application.dataPath, SYMPHONY_FRAMEWORK);
        }
#endif

        /// <summary> Runtime設定アセットを配置するResourcesディレクトリ。 </summary>
        public const string RESOURCES_RUNTIME_PATH = "Assets/Resources/" + SYMPHONY_FRAMEWORK;

        /// <summary> FrameworkのToolsメニュー基準パス。 </summary>
        public const string TOOL_MENU_PATH = "Tools/" + SYMPHONY_FRAMEWORK + "/";

        /// <summary> Framework設定用Toolsメニューの基準パス。 </summary>
        public const string TOOL_MENU_SETTING_PATH = TOOL_MENU_PATH + "Settings/";

        /// <summary> FrameworkのEditorWindowメニュー基準パス。 </summary>
        public const string WINDOW_MENU_PATH = "Window/" + SYMPHONY_FRAMEWORK + "/";
    }
}
