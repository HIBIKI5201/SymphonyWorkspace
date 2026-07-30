using SymphonyFrameWork.Config;
using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SymphonyFrameWork.System.SceneLoad
{
    /// <summary> 起動時のシーン整理と初期シーンロードを実行する。 </summary>
    internal static class SceneResetter
    {
        /// <summary> 指定した除外シーン以外をすべてアンロードする。 </summary>
        public static ValueTask ResetScene(SceneLoadManager manager, ReadOnlySpan<string> ignores)
        {
            int sceneCount = SceneManager.sceneCount;

            Span<Scene> allScenes = stackalloc Scene[sceneCount];
            Span<Scene> unloadScenes = stackalloc Scene[sceneCount];

            for (int i = 0; i < sceneCount; i++)
            {
                allScenes[i] = SceneManager.GetSceneAt(i);
            }

            int index = 0;
            for (int i = 0; i < sceneCount; i++)
            {
                Scene scene = allScenes[i];
                if (ignores.Contain(scene.name)) { continue; }

                unloadScenes[index++] = scene;
            }

            string[] unloadSceneNames = new string[index];
            for (int i = 0; i < index; i++)
            {
                unloadSceneNames[i] = unloadScenes[i].name;
            }
            
            return ConvertTask(manager.UnloadScenes(unloadSceneNames));
        }


        /// <summary> Configに設定された初期シーンをロードする。 </summary>
        public static ValueTask LoadScene(SceneLoadManager manager, SceneManagerConfig config)
        {
            return ConvertTask(manager.LoadScenes(config.InitializeSceneList));
        }

        /// <summary>
        ///     入れられたシーンを全てアンロードする。
        /// </summary>
        /// <param name="scenes"> アンロードするUnityシーンの一覧。 </param>
        /// <returns> 全アンロード処理を表すValueTask。 </returns>
        private static async ValueTask UnloadScenes(Scene[] scenes)
        {
            AsyncOperation[] tasks = new AsyncOperation[scenes.Length];

            for (int i = 0; i < scenes.Length; i++)
            {
                Scene scene = scenes[i];
                tasks[i] = SceneManager.UnloadSceneAsync(scene);
            }

            foreach (var task in tasks)
            {
                await task;
            }
        }

        /// <summary>
        ///     文字列スパンに特定の文字が含まれているか。
        /// </summary>
        /// <param name="span"> 検索対象の文字列Span。 </param>
        /// <param name="element"> 検索する文字列。 </param>
        /// <returns> 一致する要素が含まれる場合はtrue。 </returns>
        private static bool Contain(this ReadOnlySpan<string> span, string element)
        {
            for (int i = 0; i < span.Length; i++)
            {
                string s = span[i];
                if (s == element) { return true; }
            }

            return false;
        }

        /// <summary> 結果付きValueTaskを結果なしValueTaskとして待機する。 </summary>
        private static async ValueTask ConvertTask<T>(ValueTask<T> task) => await task;
    }
}
