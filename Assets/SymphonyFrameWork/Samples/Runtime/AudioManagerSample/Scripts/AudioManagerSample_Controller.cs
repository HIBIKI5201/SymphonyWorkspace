using SymphonyFrameWork.System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace SymphonyFrameWork.Samples.AudioManagerSample
{
    /// <summary> AudioManagerによるAudioSource取得と音量制御を、実行時生成したトーンで実演する。 </summary>
    public sealed class AudioManagerSample_Controller : MonoBehaviour
    {
        private const int TONE_SAMPLE_RATE = 44100;
        private const float TONE_DURATION_SECONDS = 1.5f;
        private const float TONE_FREQUENCY = 440f;

        [SerializeField, Tooltip("Project Settings > Symphony Framework で設定したAudioMixerグループ名と一致させてください。")]
        private string _audioGroupName = "BGM";

        private readonly Queue<string> _commentaryLogs = new();
        private Vector2 _scrollPosition;
        private AudioClip _toneClip;
        private float _volume = 1f;

        /// <summary> 再生確認用のサイン波トーンを実行時に生成する。 </summary>
        private void Awake()
        {
            _toneClip = CreateToneClip();
        }

        /// <summary> サンプルの使い方と前提条件を実況ログへ出力する。 </summary>
        private void Start()
        {
            AddCommentary("サンプルを開始しました。Project Settings > Symphony Framework でAudioMixerとグループを設定してください。");
            AddCommentary($"既定のグループ名は \"{_audioGroupName}\" です。自分のAudioMixerグループ名に合わせて書き換えてください。");
        }

        /// <summary> 指定グループのAudioSourceでトーンを再生する。 </summary>
        [ContextMenu("Play Tone")]
        public void PlayTone()
        {
            AudioSource source = AudioManager.GetAudioSource(_audioGroupName);
            if (source == null)
            {
                AddCommentary($"\"{_audioGroupName}\" のAudioSourceを取得できません。AudioMixerとAudioManagerConfigの設定を確認してください。");
                return;
            }

            source.PlayOneShot(_toneClip);
            AddCommentary($"\"{_audioGroupName}\" でトーンを再生しました。");
        }

        /// <summary> 指定グループの再生を停止する。 </summary>
        [ContextMenu("Stop Tone")]
        public void StopTone()
        {
            AudioSource source = AudioManager.GetAudioSource(_audioGroupName);
            if (source == null)
            {
                AddCommentary($"\"{_audioGroupName}\" のAudioSourceを取得できません。");
                return;
            }

            source.Stop();
            AddCommentary($"\"{_audioGroupName}\" の再生を停止しました。");
        }

        /// <summary> グループ名、音量スライダー、操作ボタン、実況ログをIMGUIで描画する。 </summary>
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

            GUI.Box(outerRect, "Audio Manager Sample");

            GUILayout.BeginArea(innerRect);
            GUILayout.Label("実況解説");
            GUILayout.Label("1. AudioSourceが取得できない場合は、AudioMixerとAudioManagerConfigのグループ名を確認してください。");
            GUILayout.Label("2. Volumeスライダーは AudioManager.VolumeSliderChanged を呼び出します。");
            GUILayout.Label("3. Play/StopはAudioManager.GetAudioSourceで取得したAudioSourceを直接操作します。");
            GUILayout.Space(8f);

            GUILayout.Label("Audio Group Name");
            _audioGroupName = GUILayout.TextField(_audioGroupName);
            GUILayout.Space(8f);

            AudioSource resolvedSource = AudioManager.GetAudioSource(_audioGroupName);
            GUILayout.Label($"AudioSource Resolved : {resolvedSource != null}");
            GUILayout.Label($"Is Playing           : {(resolvedSource != null && resolvedSource.isPlaying)}");
            GUILayout.Space(8f);

            GUILayout.Label($"Volume : {_volume:F2}");
            float newVolume = GUILayout.HorizontalSlider(_volume, 0f, 1f);
            if (!Mathf.Approximately(newVolume, _volume))
            {
                _volume = newVolume;
                AudioManager.VolumeSliderChanged(_audioGroupName, _volume);
            }

            GUILayout.Space(8f);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Play Tone")) { PlayTone(); }
            if (GUILayout.Button("Stop Tone")) { StopTone(); }
            GUILayout.EndHorizontal();

            GUILayout.Space(10f);
            GUILayout.Label("Commentary Log");
            float logHeight = Mathf.Max(160f, innerRect.height * 0.32f);
            _scrollPosition = GUILayout.BeginScrollView(_scrollPosition, GUILayout.Height(logHeight));
            GUILayout.TextArea(BuildCommentaryText(), GUILayout.ExpandHeight(true));
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        /// <summary> 指定秒数、周波数のサイン波AudioClipをフェード付きで生成する。 </summary>
        private static AudioClip CreateToneClip()
        {
            int sampleCount = Mathf.CeilToInt(TONE_SAMPLE_RATE * TONE_DURATION_SECONDS);
            AudioClip clip = AudioClip.Create("AudioManagerSample_Tone", sampleCount, 1, TONE_SAMPLE_RATE, false);

            float[] samples = new float[sampleCount];
            for (int i = 0; i < sampleCount; i++)
            {
                float time = (float)i / TONE_SAMPLE_RATE;
                float fadeSeconds = Mathf.Min(time, TONE_DURATION_SECONDS - time);
                float fade = Mathf.Clamp01(fadeSeconds * 20f);
                samples[i] = Mathf.Sin(2f * Mathf.PI * TONE_FREQUENCY * time) * 0.3f * fade;
            }

            clip.SetData(samples, 0);
            return clip;
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
