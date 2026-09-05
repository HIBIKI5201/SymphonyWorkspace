using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Newtonsoft.Json;

using UnityEditor;
using UnityEngine;

/// <summary>
///     Symphony Framework の <c>Samples~</c> を、このワークスペースの <c>Assets/</c> へ取り込む。
/// </summary>
/// <remarks>
///     <para>
///         **これはワークスペース側の開発用ツールであり、パッケージには入れない。**
///         利用者はUPM経由でFrameworkを導入し、Package Manager の <c>Samples &gt; Import</c> から
///         サンプルを取り込む。その経路はUnityが持っているため、パッケージが取り込み機能を持つ必要が無い。
///     </para>
///     <para>
///         このワークスペースだけは Framework を <c>Assets/</c> 直下へ submodule として置いているため、
///         UPM のサンプル取り込みが現れない。**開発中に Samples~ を動かして確認する手段が無い**ので、
///         その穴だけをワークスペース側で埋める。
///     </para>
/// </remarks>
public sealed class SymphonySampleImporter : EditorWindow
{
    private const string FRAMEWORK_PATH = "Assets/SymphonyFrameWork";
    private const string PACKAGE_JSON_PATH = FRAMEWORK_PATH + "/package.json";
    private const string SAMPLES_ROOT = FRAMEWORK_PATH + "/Samples~";
    private const string DESTINATION_ROOT = "Assets/Samples/Symphony Framework";
    private const string AUTO_IMPORT_PREF_KEY = "SymphonySampleImporter.AutoImport";

    private List<SampleEntry> _samples = new();
    private Vector2 _scroll;

    [MenuItem("Tools/" + nameof(SymphonySampleImporter))]
    public static void ShowWindow()
    {
        SymphonySampleImporter window = GetWindow<SymphonySampleImporter>();
        window.titleContent = new GUIContent("Symphony Samples");
        window.Show();
    }

    private void OnEnable() => Reload();

    private void OnFocus() => Reload();

    private void OnGUI()
    {
        EditorGUILayout.HelpBox(
            "ワークスペース専用の開発用ツールです。"
            + "UPMで導入した利用者は Package Manager の Samples > Import を使います。",
            MessageType.Info);

        bool autoImport = EditorPrefs.GetBool(AUTO_IMPORT_PREF_KEY, false);
        bool newAutoImport = EditorGUILayout.ToggleLeft(
            "Editor起動時に未取り込みのサンプルを自動で取り込む", autoImport);
        if (newAutoImport != autoImport) { EditorPrefs.SetBool(AUTO_IMPORT_PREF_KEY, newAutoImport); }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("未取り込みを全て取り込む")) { ImportAll(); }
            if (GUILayout.Button("シーンをBuild Settingsへ登録")) { RegisterScenesWithDialog(); }
        }

        EditorGUILayout.Space();

        if (_samples.Count == 0)
        {
            EditorGUILayout.HelpBox($"{PACKAGE_JSON_PATH} からサンプル宣言を読めませんでした。", MessageType.Warning);
            return;
        }

        _scroll = EditorGUILayout.BeginScrollView(_scroll);
        foreach (SampleEntry sample in _samples)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField(sample.DisplayName, EditorStyles.boldLabel);
                EditorGUILayout.LabelField(sample.Description, EditorStyles.wordWrappedMiniLabel);
                EditorGUILayout.LabelField(sample.IsImported ? "取り込み済み" : "未取り込み");

                if (GUILayout.Button(sample.IsImported ? "上書きして取り込む" : "取り込む"))
                {
                    ImportWithConfirmation(sample);
                }
            }
        }

        EditorGUILayout.EndScrollView();
    }

    /// <summary>
    ///     package.json の宣言と、現在の取り込み状態を読み直す。
    /// </summary>
    private void Reload() => _samples = ReadSamples();

    /// <summary>
    ///     未取り込みのサンプルをまとめて取り込む。
    /// </summary>
    /// <remarks> 取り込み済みは上書きしない。編集した内容を黙って消さないため。 </remarks>
    private void ImportAll()
    {
        int imported = 0;
        foreach (SampleEntry sample in _samples.Where(sample => !sample.IsImported))
        {
            if (TryImport(sample, overwrite: false)) { imported++; }
        }

        AssetDatabase.Refresh();
        Reload();
        EditorUtility.DisplayDialog("Symphony Samples", $"{imported}件のサンプルを取り込みました。", "OK");
    }

    /// <summary>
    ///     1件を取り込む。取り込み済みの場合だけ上書きの可否を尋ねる。
    /// </summary>
    /// <param name="sample"> 取り込むサンプル。 </param>
    private void ImportWithConfirmation(SampleEntry sample)
    {
        // 取り込み済みフォルダは編集されている可能性があるため、確認なしに削除しない。
        if (sample.IsImported
            && !EditorUtility.DisplayDialog(
                "サンプルの上書き",
                $"'{sample.DisplayName}' の取り込み済みフォルダを削除して取り込み直しますか？",
                "上書き",
                "キャンセル"))
        {
            return;
        }

        if (TryImport(sample, overwrite: true)) { AssetDatabase.Refresh(); }

        Reload();
    }

    /// <summary>
    ///     取り込み済みサンプルのシーンをBuild Settingsへ追加する。
    /// </summary>
    /// <remarks>
    ///     **取り込みとは別の操作にしている。** Scene Loader と Scene Block のサンプルは
    ///     シーンが登録されていないと動かないが、Build Settings の変更は `SceneListEnum` の
    ///     再生成を伴うため、コピーの副作用にしない。
    /// </remarks>
    private static void RegisterScenesWithDialog()
    {
        if (!Directory.Exists(DESTINATION_ROOT))
        {
            EditorUtility.DisplayDialog("Symphony Samples", "取り込み済みのサンプルがありません。", "OK");
            return;
        }

        HashSet<string> registered = new(
            EditorBuildSettings.scenes.Select(scene => scene.path),
            StringComparer.Ordinal);
        List<EditorBuildSettingsScene> scenes = new(EditorBuildSettings.scenes);
        int added = 0;

        foreach (string scenePath in Directory
                     .GetFiles(DESTINATION_ROOT, "*.unity", SearchOption.AllDirectories)
                     .Select(path => path.Replace('\\', '/'))
                     .OrderBy(path => path, StringComparer.Ordinal))
        {
            if (!registered.Add(scenePath)) { continue; }

            scenes.Add(new EditorBuildSettingsScene(scenePath, enabled: true));
            added++;
        }

        if (added > 0) { EditorBuildSettings.scenes = scenes.ToArray(); }

        EditorUtility.DisplayDialog("Symphony Samples", $"{added}件のシーンを追加しました。", "OK");
    }

    /// <summary>
    ///     サンプル1件をコピーする。
    /// </summary>
    /// <param name="sample"> 取り込むサンプル。 </param>
    /// <param name="overwrite"> 取り込み済みフォルダを削除して置き換える場合はtrue。 </param>
    /// <returns> コピーした場合はtrue。 </returns>
    private static bool TryImport(SampleEntry sample, bool overwrite)
    {
        if (!Directory.Exists(sample.SourcePath))
        {
            Debug.LogWarning(
                $"[{nameof(SymphonySampleImporter)}] コピー元がありません。 path: '{sample.SourcePath}'");
            return false;
        }

        try
        {
            if (Directory.Exists(sample.DestinationPath))
            {
                if (!overwrite) { return false; }
                Directory.Delete(sample.DestinationPath, recursive: true);

                // フォルダのmetaが残るとUnityが空フォルダとして復元し、取り込み状態の判定が狂う。
                string folderMetaPath = sample.DestinationPath + ".meta";
                if (File.Exists(folderMetaPath)) { File.Delete(folderMetaPath); }
            }

            CopyDirectory(sample.SourcePath, sample.DestinationPath);
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogWarning(
                $"[{nameof(SymphonySampleImporter)}] コピーに失敗しました。 "
                + $"source: '{sample.SourcePath}', destination: '{sample.DestinationPath}', "
                + $"reason: '{exception.Message}'");
            return false;
        }
    }

    /// <summary>
    ///     ディレクトリを再帰コピーする。
    /// </summary>
    /// <param name="sourcePath"> コピー元。 </param>
    /// <param name="destinationPath"> コピー先。 </param>
    /// <remarks>
    ///     **`.meta` も一緒にコピーする。** `Samples~` はAssetDatabaseの管理外でGUIDが衝突しないため、
    ///     持ち込むことでサンプルシーン内のスクリプト参照とアセット参照がそのまま解決する。
    ///     捨てるとシーンの参照が全て切れる。
    /// </remarks>
    private static void CopyDirectory(string sourcePath, string destinationPath)
    {
        Directory.CreateDirectory(destinationPath);

        foreach (string filePath in Directory.GetFiles(sourcePath))
        {
            File.Copy(filePath, Path.Combine(destinationPath, Path.GetFileName(filePath)), overwrite: true);
        }

        foreach (string directoryPath in Directory.GetDirectories(sourcePath))
        {
            CopyDirectory(
                directoryPath,
                Path.Combine(destinationPath, Path.GetFileName(directoryPath)));
        }
    }

    /// <summary>
    ///     package.json の <c>samples</c> 宣言を読む。
    /// </summary>
    /// <returns> 宣言順のサンプル一覧。読めない場合は空。 </returns>
    private static List<SampleEntry> ReadSamples()
    {
        if (!File.Exists(PACKAGE_JSON_PATH)) { return new List<SampleEntry>(); }

        try
        {
            PackageManifest manifest =
                JsonConvert.DeserializeObject<PackageManifest>(File.ReadAllText(PACKAGE_JSON_PATH));
            if (manifest?.Samples == null) { return new List<SampleEntry>(); }

            string version = string.IsNullOrWhiteSpace(manifest.Version) ? "0.0.0" : manifest.Version;
            List<SampleEntry> samples = new(manifest.Samples.Count);

            foreach (SampleManifestEntry entry in manifest.Samples)
            {
                if (entry == null
                    || string.IsNullOrWhiteSpace(entry.DisplayName)
                    || string.IsNullOrWhiteSpace(entry.Path))
                {
                    continue;
                }

                // 宣言が Samples~ の外を指していたらコピー先を作らない。package.json は人が書くため。
                string sourcePath = Path.GetFullPath(Path.Combine(FRAMEWORK_PATH, entry.Path));
                string samplesRoot = Path.GetFullPath(SAMPLES_ROOT) + Path.DirectorySeparatorChar;
                if (!sourcePath.StartsWith(samplesRoot, StringComparison.OrdinalIgnoreCase)) { continue; }

                string destinationPath = $"{DESTINATION_ROOT}/{version}/{entry.DisplayName}";
                samples.Add(new SampleEntry(
                    entry.DisplayName,
                    entry.Description,
                    sourcePath,
                    destinationPath,
                    Directory.Exists(destinationPath)));
            }

            return samples;
        }
        catch (JsonException exception)
        {
            Debug.LogWarning(
                $"[{nameof(SymphonySampleImporter)}] package.jsonを解析できませんでした。 "
                + $"reason: '{exception.Message}'");
            return new List<SampleEntry>();
        }
    }

    /// <summary>
    ///     Editor起動時の自動取り込み。
    /// </summary>
    [InitializeOnLoad]
    private static class AutoImporter
    {
        static AutoImporter()
        {
            if (!EditorPrefs.GetBool(AUTO_IMPORT_PREF_KEY, false)) { return; }

            // AssetDatabaseが使える状態になってから走らせる。
            EditorApplication.delayCall += ImportMissing;
        }

        private static void ImportMissing()
        {
            int imported = 0;
            foreach (SampleEntry sample in ReadSamples().Where(sample => !sample.IsImported))
            {
                if (TryImport(sample, overwrite: false)) { imported++; }
            }

            if (imported == 0) { return; }

            AssetDatabase.Refresh();
            Debug.Log($"[{nameof(SymphonySampleImporter)}] {imported}件のサンプルを自動で取り込みました。");
        }
    }

    /// <summary> 取り込み対象1件。 </summary>
    private sealed class SampleEntry
    {
        public SampleEntry(
            string displayName,
            string description,
            string sourcePath,
            string destinationPath,
            bool isImported)
        {
            DisplayName = displayName;
            Description = description;
            SourcePath = sourcePath;
            DestinationPath = destinationPath;
            IsImported = isImported;
        }

        public string DisplayName { get; }
        public string Description { get; }
        public string SourcePath { get; }
        public string DestinationPath { get; }
        public bool IsImported { get; }
    }

    /// <summary> package.json のうち、このツールが読む項目。 </summary>
    private sealed class PackageManifest
    {
        [JsonProperty("version")]
        public string Version { get; private set; }

        [JsonProperty("samples")]
        public List<SampleManifestEntry> Samples { get; private set; }
    }

    /// <summary> package.json の samples 1件。 </summary>
    private sealed class SampleManifestEntry
    {
        [JsonProperty("displayName")]
        public string DisplayName { get; private set; }

        [JsonProperty("description")]
        public string Description { get; private set; }

        [JsonProperty("path")]
        public string Path { get; private set; }
    }
}
