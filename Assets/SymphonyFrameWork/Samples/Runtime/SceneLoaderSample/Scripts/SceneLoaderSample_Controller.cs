using SymphonyFrameWork.System.SceneLoad;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SymphonyFrameWork.Samples.SceneLoaderSample
{
    /// <summary> SceneLoaderによる追加ロード、優先度に基づくActive Scene切り替え、ロード完了待機を実演する。 </summary>
    public sealed class SceneLoaderSample_Controller : MonoBehaviour
    {
        private const string SCENE_A = "SceneLoaderSample_SceneA";
        private const string SCENE_B = "SceneLoaderSample_SceneB";
        private const int PRIORITY_A = 5;
        private const int PRIORITY_B = 10;

        private readonly Queue<string> _commentaryLogs = new();
        private Vector2 _scrollPosition;
        private bool _isBusy;
        private string _waitResult = "(not run)";

        /// <summary> ロード完了通知を登録し、実況ログを開始する。 </summary>
        private void Start()
        {
            AddCommentary("サンプルを開始しました。SceneA/SceneBはまだロードされていません。");
            AddCommentary("事前にSceneA/SceneBをBuild SettingsのScene Listへ追加してください。");

            SceneLoader.RegisterAfterSceneLoad(SCENE_A, () => AddCommentary($"{SCENE_A} のロード完了通知(RegisterAfterSceneLoad)を受信しました。"));
            SceneLoader.RegisterAfterSceneLoad(SCENE_B, () => AddCommentary($"{SCENE_B} のロード完了通知(RegisterAfterSceneLoad)を受信しました。"));
        }

        /// <summary> SceneAを優先度5で追加ロードする。 </summary>
        [ContextMenu("Load Scene A (priority 5)")]
        public async void LoadSceneA()
        {
            if (_isBusy)
            {
                return;
            }

            _isBusy = true;
            AddCommentary($"{SCENE_A} を優先度{PRIORITY_A}で追加ロードします。");

            bool succeeded = await SceneLoader.LoadScene(
                SCENE_A,
                mode: LoadSceneMode.Additive,
                priority: PRIORITY_A,
                token: destroyCancellationToken);

            AddCommentary(succeeded
                ? $"{SCENE_A} のロードに成功しました。"
                : $"{SCENE_A} のロードに失敗しました。Build SettingsにSceneが登録されているか確認してください。");
            _isBusy = false;
        }

        /// <summary> SceneBを優先度10で追加ロードする。優先度が現在のActive以上であればActiveも切り替わる。 </summary>
        [ContextMenu("Load Scene B (priority 10)")]
        public async void LoadSceneB()
        {
            if (_isBusy)
            {
                return;
            }

            _isBusy = true;
            AddCommentary($"{SCENE_B} を優先度{PRIORITY_B}で追加ロードします。優先度が現在のActive以上のため、ロード後にActiveへ切り替わります。");

            bool succeeded = await SceneLoader.LoadScene(
                SCENE_B,
                mode: LoadSceneMode.Additive,
                priority: PRIORITY_B,
                token: destroyCancellationToken);

            AddCommentary(succeeded
                ? $"{SCENE_B} のロードに成功しました。"
                : $"{SCENE_B} のロードに失敗しました。Build SettingsにSceneが登録されているか確認してください。");
            _isBusy = false;
        }

        /// <summary> SceneAをアンロードする。 </summary>
        [ContextMenu("Unload Scene A")]
        public async void UnloadSceneA()
        {
            if (_isBusy)
            {
                return;
            }

            _isBusy = true;
            AddCommentary($"{SCENE_A} をアンロードします。Activeだった場合は次に優先度が高いシーンへ切り替わります。");
            await SceneLoader.UnloadScene(SCENE_A, token: destroyCancellationToken);
            AddCommentary($"{SCENE_A} のアンロードが完了しました。");
            _isBusy = false;
        }

        /// <summary> SceneBをアンロードする。 </summary>
        [ContextMenu("Unload Scene B")]
        public async void UnloadSceneB()
        {
            if (_isBusy)
            {
                return;
            }

            _isBusy = true;
            AddCommentary($"{SCENE_B} をアンロードします。Activeだった場合は次に優先度が高いシーンへ切り替わります。");
            await SceneLoader.UnloadScene(SCENE_B, token: destroyCancellationToken);
            AddCommentary($"{SCENE_B} のアンロードが完了しました。");
            _isBusy = false;
        }

        /// <summary> ロード済みのSceneAを明示的にActiveへ切り替える。 </summary>
        [ContextMenu("Set Active: Scene A")]
        public void SetActiveSceneA()
        {
            bool succeeded = SceneLoader.SetActiveScene(SCENE_A);
            AddCommentary(succeeded
                ? $"{SCENE_A} をActiveに切り替えました。"
                : $"{SCENE_A} は未ロードのため切り替えできません。");
        }

        /// <summary> ロード済みのSceneBを明示的にActiveへ切り替える。 </summary>
        [ContextMenu("Set Active: Scene B")]
        public void SetActiveSceneB()
        {
            bool succeeded = SceneLoader.SetActiveScene(SCENE_B);
            AddCommentary(succeeded
                ? $"{SCENE_B} をActiveに切り替えました。"
                : $"{SCENE_B} は未ロードのため切り替えできません。");
        }

        /// <summary> SceneAのロード完了をWaitForLoadSceneAsyncで待機する。 </summary>
        [ContextMenu("Wait For Scene A Load")]
        public async void WaitForSceneALoad()
        {
            _waitResult = "waiting...";
            AddCommentary($"{SCENE_A} のロード完了をWaitForLoadSceneAsyncで待機します。");

            try
            {
                await SceneLoader.WaitForLoadSceneAsync(SCENE_A, destroyCancellationToken);
                _waitResult = "done";
                AddCommentary($"{SCENE_A} のロード完了を確認しました。");
            }
            catch (OperationCanceledException)
            {
                _waitResult = "(not run)";
            }
        }

        /// <summary> シーン状態、操作ボタン、実況ログをIMGUIで描画する。 </summary>
        private void OnGUI()
        {
            float margin = Mathf.Max(12f, Screen.width * 0.025f);
            float width = Screen.width - (margin * 2f);
            float height = Screen.height - (margin * 2f);
            Rect outerRect = new(margin, margin, width, height);
            Rect innerRect = new(
                outerRect.x + 12f,
                outerRect.y + 28f,
                outerRect.width - 24f,
                outerRect.height - 40f);

            GUI.Box(outerRect, "Scene Loader Sample");

            GUILayout.BeginArea(innerRect);
            GUILayout.Label("実況解説");
            GUILayout.Label("1. Load Scene BはSceneAより優先度が高いため、両方ロード済みだとBがActiveになります。");
            GUILayout.Label("2. Active Sceneをアンロードすると、残りの中で最も優先度が高いシーンへ自動的に切り替わります。");
            GUILayout.Label("3. Wait For Scene A LoadはWaitForLoadSceneAsyncでロード完了だけを待ちます。");
            GUILayout.Space(8f);

            GUILayout.Label($"Active Scene : {SceneManager.GetActiveScene().name}");
            GUILayout.Label($"Wait Result  : {_waitResult}");
            GUILayout.Label($"Busy         : {_isBusy}");
            GUILayout.Space(8f);

            DrawSceneStatus(SCENE_A);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Load A")) { LoadSceneA(); }
            if (GUILayout.Button("Unload A")) { UnloadSceneA(); }
            if (GUILayout.Button("Set Active A")) { SetActiveSceneA(); }
            if (GUILayout.Button("Wait A")) { WaitForSceneALoad(); }
            GUILayout.EndHorizontal();

            GUILayout.Space(6f);

            DrawSceneStatus(SCENE_B);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Load B")) { LoadSceneB(); }
            if (GUILayout.Button("Unload B")) { UnloadSceneB(); }
            if (GUILayout.Button("Set Active B")) { SetActiveSceneB(); }
            GUILayout.EndHorizontal();

            GUILayout.Space(10f);
            GUILayout.Label("Commentary Log");
            float logHeight = Mathf.Max(160f, innerRect.height * 0.32f);
            _scrollPosition = GUILayout.BeginScrollView(_scrollPosition, GUILayout.Height(logHeight));
            GUILayout.TextArea(BuildCommentaryText(), GUILayout.ExpandHeight(true));
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        /// <summary> 指定シーンの存在有無とロード状態をラベル表示する。 </summary>
        private void DrawSceneStatus(string sceneName)
        {
            bool exists = SceneLoader.IsExist(sceneName);
            string state = SceneLoader.TryGetState(sceneName, out SceneLoadState loadState) ? loadState.ToString() : "Untracked";
            GUILayout.Label($"{sceneName} | Exists: {exists} | State: {state}");
        }

        /// <summary> 表示上限を維持しながら実況ログを末尾へ追加する。 </summary>
        private void AddCommentary(string message)
        {
            if (_commentaryLogs.Count >= 12)
            {
                _commentaryLogs.Dequeue();
            }

            _commentaryLogs.Enqueue(message);
        }

        /// <summary> 現在の実況ログを改行区切りの表示文字列へまとめる。 </summary>
        private string BuildCommentaryText()
        {
            StringBuilder builder = new();
            foreach (string log in _commentaryLogs)
            {
                builder.AppendLine(log);
            }

            return builder.ToString();
        }
    }
}
