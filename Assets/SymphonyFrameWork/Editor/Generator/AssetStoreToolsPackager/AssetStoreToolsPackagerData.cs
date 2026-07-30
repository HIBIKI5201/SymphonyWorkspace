using SymphonyFrameWork.Core;
using UnityEditor;
using UnityEngine;
namespace SymphonyFrameWork.Editor
{
    /// <summary> Asset Store Tools Packagerのプロジェクト共有パス設定を保持する。 </summary>
    [FilePath(EditorSymphonyConstant.PROJCET_SETTING_FILE_PATH
        + nameof(AssetStoreToolsPackagerData) + ".asset",
        FilePathAttribute.Location.ProjectFolder)]
    public sealed class AssetStoreToolsPackagerData : ScriptableSingleton<AssetStoreToolsPackagerData>
    {
        /// <summary> パッケージ対象となるAsset Store Toolsフォルダのパス。 </summary>
        public static string AssetStoreToolsPath => instance._assetStoreToolsPath;

        /// <summary> 生成したパッケージを保存するフォルダのパス。 </summary>
        public static string ExportedPackagesPath => instance.exportedPackagesPath;

        /// <summary> パッケージ対象フォルダのパスを保存する。 </summary>
        public static void SetAssetStoreToolsPath(string path)
        {
            if (instance._assetStoreToolsPath != path)
            {
                instance._assetStoreToolsPath = path;
                EditorUtility.SetDirty(instance);
                AssetDatabase.SaveAssets();
                Save();
            }
        }
        /// <summary> パッケージ出力先フォルダのパスを保存する。 </summary>
        public static void SetExportedPackagesPath(string path)
        {
            if (instance.exportedPackagesPath != path)
            {
                instance.exportedPackagesPath = path;
                EditorUtility.SetDirty(instance);
                AssetDatabase.SaveAssets();
                Save();
            }
        }

        [SerializeField, Tooltip("生成したパッケージを保存するフォルダのパス。")]
        private string exportedPackagesPath = "ExportedPackages";

        [SerializeField, Tooltip("パッケージ対象となるAsset Store Toolsフォルダのパス。")]
        private string _assetStoreToolsPath = EditorSymphonyConstant.ASSET_STORE_TOOLS_PATH;

        /// <summary> 現在の設定値をProjectSettingsへ保存する。 </summary>
        private static void Save() => instance.Save(true);
    }
}
