using System.Collections.Generic;
using UnityEditor;

namespace SymphonyFrameWork.Editor.SettingProvider
{
    /// <summary> Asset Store Tools PackagerのProject Settings画面を提供する。 </summary>
    public sealed class AssetStoreToolsPackagerProvider
    {
        /// <summary> Project Settingsに表示する設定項目名。 </summary>
        public const string LABEL = "Asset Store Tools Packager";

        /// <summary> SettingsProviderの完全な設定パス。 </summary>
        public const string SELF_PATH = SymphonySettingProvider.PROVIDER_PATH + LABEL;

        /// <summary> Packager設定用のSettingsProviderを生成する。 </summary>
        [SettingsProvider]
        public static SettingsProvider CreateCustomSettingsProvider()
        {
            // SettingsScope.Projectを指定することでProject Settingsに項目を追加できる
            var provider = new SettingsProvider(SELF_PATH, SettingsScope.Project)
            {
                // 項目のタイトル
                label = LABEL,

                // どのように描画するか(IMGUI)
                guiHandler = IMGUI,

                // 検索するときのキーワード
                keywords = new HashSet<string>(new[] { "asset", "store", "tools", "packager" }),
            };

            return provider;
        }

        /// <summary> Packagerが使用する入出力パス設定を描画する。 </summary>
        private static void IMGUI(string searchContext)
        {
            string assetStoreToolsPath = AssetStoreToolsPackagerData.AssetStoreToolsPath;
            assetStoreToolsPath = EditorGUILayout.TextField("Asset Store Tools Path", assetStoreToolsPath);
            if (assetStoreToolsPath != AssetStoreToolsPackagerData.AssetStoreToolsPath)
            {
                AssetStoreToolsPackagerData.SetAssetStoreToolsPath(assetStoreToolsPath);
            }

            string exportedPackagesPath = AssetStoreToolsPackagerData.ExportedPackagesPath;
            exportedPackagesPath = EditorGUILayout.TextField("Exported Packages Path", exportedPackagesPath);
            if (exportedPackagesPath != AssetStoreToolsPackagerData.ExportedPackagesPath)
            {
                AssetStoreToolsPackagerData.SetExportedPackagesPath(exportedPackagesPath);
            }
        }
    }
}
