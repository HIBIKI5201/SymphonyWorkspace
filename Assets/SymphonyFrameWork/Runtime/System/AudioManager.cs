using System;
using System.Collections.Generic;
using System.Linq;

using SymphonyFrameWork.Debugger.Logger;
using SymphonyFrameWork.Exceptions;
using SymphonyFrameWork.Orchestrator;

using UnityEngine;
using UnityEngine.Audio;

namespace SymphonyFrameWork.System
{
    /// <summary>
    /// オーディオ再生
    /// </summary>
    public static class AudioManager
    {
        private static AudioManagerConfig _config;
        private static GameObject _instance;
        private static bool _isInitialized;

        private static
            Dictionary<string, AudioSettingData> _audioDict = new();

        private struct AudioSettingData
        {
            /// <summary> 対応するAudioMixerGroup。 </summary>
            public readonly AudioMixerGroup Group;

            /// <summary> グループの再生を担当するAudioSource。 </summary>
            public readonly AudioSource Source;

            /// <summary> 音量制御に使用する公開パラメーター名。 </summary>
            public readonly string ExposedName;

            /// <summary> AudioMixerから取得した初期音量。 </summary>
            public readonly float? OriginalVolume;

            /// <summary> ミキサーグループに対応する再生元と初期音量を保持する。 </summary>
            public AudioSettingData(AudioMixerGroup group, AudioSource source, string exposedName, float? originalVolume)
            {
                Group = group;
                Source = source;
                ExposedName = exposedName;
                OriginalVolume = originalVolume;
            }
        }

        /// <summary> Configを受け取り、遅延生成されるランタイム状態を初期化する。 </summary>
        /// <param name="config"> オーディオミキサーとグループ設定。 </param>
        internal static void Initialize(AudioManagerConfig config)
        {
            _isInitialized = false;
            _instance = null;
            _audioDict = null;
            _config = config;
            _isInitialized = true;
        }

        /// <summary> AudioSourceを所有するシステムオブジェクトを必要な場合だけ生成する。 </summary>
        private static void CreateInstance()
        {
            if (_instance is not null) return;

            var instance = new GameObject(nameof(AudioManager));

            SymphonyOrchestrator.PreserveObject(instance);
            _instance = instance;
        }

        /// <summary> Configに定義されたミキサーグループごとのAudioSourceを遅延生成する。 </summary>
        private static void AudioSourceInitialize()
        {
            EnsureInitialized();

            if (_audioDict != null)
            {
                return;
            }

            _audioDict = new();

            CreateInstance();

            AudioMixer mixer = _config?.AudioMixer;

            if (!mixer)
            {
                Debug.LogWarning("オーディオミキサーがアサインされていません");
                return;
            }

            SymphonyDebugLogger.AddText("Audio Managerを初期化しました。");

            foreach (string name in _config.AudioGroupSettingList.Select(s => s.AudioGroupName))


            {
                if (string.IsNullOrEmpty(name))
                {
                    continue;
                }

                //グループ名からデータを取得
                var data = _config.AudioGroupSettingList.Find(s => s.AudioGroupName == name);

                if (data == null)
                {
                    Debug.LogWarning($"{name}のデータがありません。");
                    continue;
                }

                //ミキサーグループを取得する
                AudioMixerGroup group = mixer.FindMatchingGroups(name).FirstOrDefault();
                if (group)
                {
                    AudioSource source = _instance.AddComponent<AudioSource>();
                    source.outputAudioMixerGroup = group;
                    source.playOnAwake = false;
                    if (data.IsLoop) source.loop = true;

                    //初期のボリュームを取得
                    float? volume = null;
                    if (!string.IsNullOrEmpty(data.ExposedVolumeParameterName) &&
                        mixer.GetFloat(data.ExposedVolumeParameterName, out var value))
                    {
                        volume = value;
                        SymphonyDebugLogger.AddText($"{name}は正常に追加されました。volume : {volume}");
                    }
                    else
                    {
                        SymphonyDebugLogger.AddText($"{name}のVolumeParameterが見つかりませんでした");
                    }

                    //各情報を追加
                    _audioDict.Add(name, new AudioSettingData(group, source, data.ExposedVolumeParameterName, volume ?? 0));
                }
                else
                {
                    SymphonyDebugLogger.AddText($"{name} is not a valid AudioMixerGroup.");
                }
            }

            SymphonyDebugLogger.LogText();
        }

        /// <summary>
        ///     指定したミキサーグループの音量を割合で変更する。
        /// </summary>
        /// <param name="name"> Configに登録されたオーディオグループ名。 </param>
        /// <param name="value"> 0から1までの音量割合。 </param>
        public static void VolumeSliderChanged(string name, float value)
        {
            EnsureInitialized();

            if (string.IsNullOrEmpty(name))
            {
                throw new ArgumentException("オーディオグループ名を指定してください。", nameof(name));
            }

            if (value < 0 || 1 < value)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(value),
                    value,
                    "音量割合は0から1の範囲で指定してください。");
            }

            AudioSourceInitialize();

            if (!_audioDict.TryGetValue(name, out var data)) return;

            if (data.OriginalVolume == null)
            {
                Debug.LogWarning($"{name}のボリュームがありません");
                return;
            }

            //デシベルで音量を割合変更
            float db = value * (data.OriginalVolume.Value + 80) - 80;

            _config?.AudioMixer.SetFloat(data.ExposedName, db);
        }

        /// <summary>
        ///     指定されたAudioSourceを取得する。
        /// </summary>
        /// <param name="name"> Configに登録されたオーディオグループ名。 </param>
        /// <returns> 対応するAudioSource。未登録の場合はnull。 </returns>
        public static AudioSource GetAudioSource(string name)
        {
            EnsureInitialized();

            if (string.IsNullOrEmpty(name))
            {
                throw new ArgumentException("オーディオグループ名を指定してください。", nameof(name));
            }

            AudioSourceInitialize();

            return _audioDict.TryGetValue(name, out var data) ? data.Source : null;
        }

        /// <summary> Audio Managerが利用可能な状態か検証する。 </summary>
        private static void EnsureInitialized()
        {
            if (!_isInitialized)
            {
                throw new SymphonyNotInitializedException(typeof(AudioManager));
            }
        }
    }
}
