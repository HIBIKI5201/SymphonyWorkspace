using SymphonyFrameWork.Core;
using SymphonyFrameWork.Utility;
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEngine;

namespace SymphonyFrameWork.Editor
{
    /// <summary>
    ///     有用なパッケージを自動インストールするクラス
    /// </summary>
    public static class SymphonyPackageLoader
    {
        private static readonly string REQUIRE_PACKAGE_LIST_PATH = EditorSymphonyConstant.FRAMEWORK_PATH + "/Editor/PackageLoader/PackageList.txt";

        /// <summary>
        ///     パッケージがロードされているかチェックする
        /// </summary>
        [MenuItem(SymphonyConstant.TOOL_MENU_PATH + nameof(SymphonyPackageLoader), priority = 100)]
        private static void MenuExecution()
        {
            CheckAndInstallPackagesAsync(false);
        }

        /// <summary> 必須パッケージ一覧とインストール済み一覧を比較し、不足分の導入を確認する。 </summary>
        private static async void CheckAndInstallPackagesAsync(bool isEnterEditor)
        {
            //パッケージマネージャーの初期化が終わっているか。
            if (Client.List() == null) return;

            // パッケージリストを非同期で取得。
            var installedPackages = await GetInstalledPackagesAsync();

            if (installedPackages == null) return;

            // テキストファイルからインストールするパッケージリストを取得。
            string[] requirePackageList = AssetDatabase.LoadAssetAtPath<TextAsset>(REQUIRE_PACKAGE_LIST_PATH)
                ?.text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                ?.Select(line => line.Trim())
                ?.ToArray()
                ?? new string[0];

            string[] missingPackages = GetMissingPackages(requirePackageList, installedPackages);

            //パッケージがない場合は終了
            if (missingPackages.Length < 1)
            {
                if (!isEnterEditor && EditorUtility.DisplayDialog($"{nameof(SymphonyPackageLoader)}",
                        "全てのパッケージがインストールされています",
                        "OK"))
                {
                }
            }
            else
            {
                if (EditorUtility.DisplayDialog($"{nameof(SymphonyPackageLoader)}",
                        "以下のパッケージをインストールします\n" + string.Join('\n', missingPackages),
                        "OK", "Cancel"))
                {
                    await InstallPackageAsync(missingPackages);
                }
            }
        }

        /// <summary>
        ///     インストールされているパッケージを返す
        /// </summary>
        /// <returns> Package Managerから取得したインストール済みパッケージ一覧。 </returns>
        private static async Task<PackageCollection> GetInstalledPackagesAsync()
        {
            EditorUtility.DisplayProgressBar(nameof(SymphonyPackageLoader), "パッケージを確認中", 0);

            var listRequest = Client.List();

            var timer = Time.time;
            // IAsyncOperation を非同期タスクで待機
            await SymphonyTask.WaitUntil(() => listRequest.IsCompleted || timer + 60 < Time.time);

            EditorUtility.ClearProgressBar();

            if (timer + 60 < Time.time) EditorUtility.DisplayDialog(nameof(SymphonyPackageLoader), "タイムアウトしました", "OK");

            if (listRequest.Status == StatusCode.Failure)
            {
                Debug.LogError("Failed to fetch package list: " + listRequest.Error.message);
                return null;
            }

            return listRequest.Result;
        }

        /// <summary>
        ///     パッケージがロードされているかチェックする
        /// </summary>
        private static string[] GetMissingPackages(string[] required, PackageCollection installedPackages)
        {
            ConcurrentBag<string> missingPackages = new ();

            Parallel.ForEach(required, pkg =>
            {
                var fullPackageName = "com.unity." + pkg;

                if (!installedPackages.Any(installedPkg => installedPkg.name == fullPackageName))
                    missingPackages.Add(fullPackageName);
            });

            return missingPackages.ToArray();
        }

        /// <summary>
        ///     パッケージをロードする
        /// </summary>
        /// <param name="packageNames"> Package Managerへ追加する完全パッケージ名一覧。 </param>
        /// <returns> 全パッケージ追加処理を表すTask。 </returns>
        private static async Task InstallPackageAsync(string[] packageNames)
        {
            //ロードのタスクを一括で生成
            var tasks = packageNames.Select(async name =>
            {
                var addRequest = Client.Add(name);

                while (!addRequest.IsCompleted) await Task.Yield();

                if (addRequest.Status == StatusCode.Failure)
                    Debug.LogError("Failed to install package: " + addRequest.Error.message);
                else
                    Debug.Log("Package installed: " + name);
            });

            await Task.WhenAll(tasks);
        }
    }
}
