

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Codice.Client.BaseCommands;
using UnityEditor;
using UnityEngine;

public class VersionLogGenerator : EditorWindow
{
    private const string LOG_PATH = "Assets/SymphonyFramework/CHANGELOG.md";
    private const string PACKAGE_PATH = "Assets/SymphonyFramework/package.json";

    private static List<LogData> logs = new();

    private static LogData _newData = new();
    

    [MenuItem("Tools/" + nameof(VersionLogGenerator))]
    public static void ShowWindow()
    {
        // ウィンドウを表示
        var window = GetWindow<VersionLogGenerator>();
        window.titleContent = new GUIContent(nameof(VersionLogGenerator));
        window.Show();

        var log = ReadChangelog();
        ConvertLogData(log);

        #region バージョンの最新を生成

        _newData = new();
        
        var version = logs[0].version.Split('.');
        version[2] = (int.Parse(version[2]) + 1).ToString();
        _newData.version = string.Join(".", version);

        #endregion
    }

    #region ウィンドウ描画
    
    // ウィンドウのGUIを描画
    private void OnGUI()
    {
        DateTime date = DateTime.Now;
        _newData.date = $"{date.Year.ToString("0000")}-{date.Month.ToString("00")}-{date.Day.ToString("00")}";
        GUILayout.Label("date: " + _newData.date);

        if (GUILayout.Button("Add Log"))
        {
            AddLog();
        }

        // テキストフィールドを追加
        _newData.version = EditorGUILayout.TextField("version", _newData.version);

        // リストの編集
        ListGUI("AddText", ref _newData.addText);
        ListGUI("UpdateText", ref _newData.updateText);
        ListGUI("FixText", ref _newData.fixText);
    }

    
    private void ListGUI(string label, ref List<string> list)
    {
        EditorGUILayout.Space(10);
        GUILayout.Label(label);

        for (int i = 0; i < list.Count; i++)
        {
            // 各要素の編集フィールド
            list[i] = EditorGUILayout.TextField("Item " + i, list[i]);

            // 要素を削除するボタン
            if (GUILayout.Button("Remove Item " + i))
            {
                list.RemoveAt(i);
            }
        }

        // 新しいアイテムを追加するボタン
        if (GUILayout.Button("Add Item"))
        {
            list.Add(string.Empty);
        }
    }
    
    #endregion

    private static string ReadChangelog()
    {
        if (File.Exists(LOG_PATH))
        {
            return File.ReadAllText(LOG_PATH);
        }
        else
        {
            Debug.LogError("CHANGELOG.md が見つかりません");
            return string.Empty;
        }
    }

    private static void ConvertLogData(string text)
    {
        logs.Clear();

        var lines = text.Split("\n").Select(l => l.TrimEnd('\r')).ToArray();

        for (int i = 0; i < lines.Length; i++)
        {
            if (lines[i].StartsWith("## "))
            {
                var data = new LogData();

                data.version = lines[i].Substring(lines[i].IndexOf("[") + 1, lines[i].IndexOf("]") - lines[i].IndexOf("[") - 1);
                data.date = lines[i].Substring(lines[i].IndexOf("- ") + 2);

                string title = string.Empty;
                i += 1;
                while (!string.IsNullOrEmpty(lines[i]))
                {
                    if (lines[i].StartsWith("### "))
                    {
                        title = lines[i].Substring(4);
                    }
                    else if (lines[i].StartsWith("- "))
                    {
                        switch (title)
                        {
                            case "Add":
                                data.addText.Add(lines[i].Substring(2));
                                break;
                            
                            case "Update":
                                data.updateText.Add(lines[i].Substring(2));
                                break;
                            
                            case "Fix":
                                data.fixText.Add(lines[i].Substring(2));
                                break;
                        }
                    }
                    i++;
                }

                logs.Add(data);
            }
        }
    }
    
    private void AddLog()
    {
        // すでに同じバージョンが存在していないか確認
        string[] versions = logs.Select(l => l.version).ToArray();

        if (versions.Contains(_newData.version))
        {
            Debug.LogWarning($"version :{_newData.version}は既に存在します");
            return;
        }

        logs = new List<LogData> { _newData }.Concat(logs).ToList(); // ログ追加

        string text = $"# Changelog\n\n" +
                      string.Join("\n\n", logs) + "\n";

        File.WriteAllText(LOG_PATH, text);
        UpdatePackageLog(_newData.version);

        Debug.Log($"ログを追加\n{_newData}");

        Close();
    }

    private void UpdatePackageLog(string version)
    {
        if (!File.Exists(PACKAGE_PATH))
        {
            Debug.LogError("CHANGELOG.md が見つかりません");
        } 
        
        string text = File.ReadAllText(PACKAGE_PATH);
        
        var lines = text.Split('\n').Select(l => l.TrimEnd('\r')).ToArray();
        for (int i = 0; i < lines.Length; i++)
        {
            if (lines[i].TrimStart().StartsWith("\"version\":"))
            {
                // 行のインデントを維持
                string indent = lines[i].Substring(0, lines[i].IndexOf('"'));
                lines[i] = $"{indent}\"version\": \"{version}\",";
                break; // 一つだけ書き換えるならループを抜けてもOK
            }
        }

        string newText = string.Join("\n", lines);
        File.WriteAllText(PACKAGE_PATH, newText);
    }

    [Serializable]
    private class LogData
    {
        public string version;
        public string date;
        public List<string> addText;
        public List<string> updateText;
        public List<string> fixText;

        public LogData()
        {
            version = string.Empty;
            date = "2000-01-01";
            addText = new();
            updateText = new();
            fixText = new();
        }

        public override string ToString() =>
            $"## [{version}] - {date}" +
            (addText.Count > 0 ? "\n### Add" + $"\n- {string.Join("\n- ", addText)}" : string.Empty)+
            (updateText.Count > 0 ? "\n### Update" + $"\n- {string.Join("\n- ", updateText)}" : string.Empty) +
            (fixText.Count > 0 ? "\n### Fix" + $"\n- {string.Join("\n- ", fixText)}" : string.Empty);
    }

    private enum LogType
    {
        Add,
        Update,
        Fix,
        Other,
    }
}