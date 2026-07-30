using SymphonyFrameWork.Core;
using System.Collections.Generic;
using UnityEditor;

namespace SymphonyFrameWork.Editor.SettingProvider
{
    /// <summary> Symphony FrameworkのルートProject Settings画面を提供する。 </summary>
    public sealed class SymphonySettingProvider
    {
        /// <summary> Project Settingsに表示するルート設定項目名。 </summary>
        public const string LABEL = SymphonyConstant.SYMPHONY_FRAMEWORK;

        /// <summary> ルートSettingsProviderの完全な設定パス。 </summary>
        public const string SELF_PATH = EditorSymphonyConstant.PROJECT_SETTING_PATH + LABEL;

        /// <summary> 子SettingsProviderが使用する設定パスの接頭辞。 </summary>
        public const string PROVIDER_PATH = EditorSymphonyConstant.PROJECT_SETTING_PATH + LABEL + "/";

        /// <summary> Framework設定用のSettingsProviderを生成する。 </summary>
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
                keywords = new HashSet<string>(new[] { "symphony", "framework", "symphony framework" }),
            };

            return provider;
        }

        /// <summary> Framework設定画面の案内を描画する。 </summary>
        private static void IMGUI(string searchContext)
        {
            EditorGUILayout.LabelField("これはSettingsProviderにより追加した独自項目です。");
        }
    }
}
