using System.IO;
using SymphonyFrameWork.Config;
using SymphonyFrameWork.Core;
using SymphonyFrameWork.System.SaveSystem;
using UnityEditor;
using UnityEngine;

namespace SymphonyFrameWork.Editor
{
    /// <summary>
    ///     起動時に初期化する
    /// </summary>
    [InitializeOnLoad]
    public static class PackageInitializer
    {
        /// <summary> Config、生成enum、Editor用セーブローダーをEditor起動時に整備する。 </summary>
        static PackageInitializer()
        {
            SymphonyConfigManager.AllConfigCheck();
            SaveDataRegistry.ConfigureLoaderResolver(ResolveSaveDataLoader);
            EnumInitialize();
            
            AssetDatabase.Refresh();
            
            Debug.Log("Symphony Framework Initialized");
        }

        /// <summary> 自動生成enumとAssembly Definitionの配置・参照を整備する。 </summary>
        private static void EnumInitialize()
        {
            //Enumファイルが無ければ生成する
            if (!Directory.Exists(EditorSymphonyConstant.ENUM_PATH))
            {
                AutoEnumGenerator.SceneListEnumGenerate();
                AutoEnumGenerator.TagsEnumGenerate();
                AutoEnumGenerator.LayersEnumGenerate();
                AutoEnumGenerator.AudioEnumGenerate();
            }
            
            //パッケージ内のEnumを消す
            var path = $"Packages/{SymphonyConstant.SYMPHONY_PACKAGE}/Enum"; //パッケージ内のEnumフォルダ
            if (Directory.Exists(path))
            {
                FileUtil.DeleteFileOrDirectory(path);
                FileUtil.DeleteFileOrDirectory(path + ".meta");
                AssetDatabase.Refresh();
            }
            
            var enumAsmdefPath = EditorSymphonyConstant.ENUM_PATH + "/SymphonyFrameWork.Enum.asmdef";
            var mainAsmdefPath = EditorSymphonyConstant.FRAMEWORK_PATH + "/SymphonyFrameWork.asmdef";
            AssemblyGenerator.AddAsssemblyReference(mainAsmdefPath, enumAsmdefPath);
        }

        /// <summary>
        ///     Editor上の現在のConfigからセーブデータローダーを解決する。
        /// </summary>
        /// <returns> Configで選択されたローダー。未設定の場合は既定のローダー。 </returns>
        private static SaveDataLoader ResolveSaveDataLoader()
        {
            SaveSystemConfig config =
                SymphonyConfigLocator.GetConfig<SaveSystemConfig>();
            return config?.Loader ?? new JsonUtilitySaveDataLoader();
        }
    }
}
