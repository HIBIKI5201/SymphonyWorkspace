using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

namespace SymphonyFrameWork
{
    /// <summary> AudioManagerが使用するミキサーとグループ設定を保持する。 </summary>
    internal sealed class AudioManagerConfig : ScriptableObject
    {
        /// <summary> 再生と音量制御に使用するAudioMixer。 </summary>
        public AudioMixer AudioMixer => _audioMixer;

        /// <summary> AudioMixerグループごとの再生設定。 </summary>
        public List<AudioGroupSettings> AudioGroupSettingList => _audioGroupSettingList;

        [SerializeField, Tooltip("再生と音量制御に使用するAudioMixer。")]
        private AudioMixer _audioMixer;

        [SerializeField, Tooltip("AudioMixerグループごとの再生設定。")]
        private List<AudioGroupSettings> _audioGroupSettingList;

        /// <summary> AudioMixerグループに対応する再生と音量制御の設定を保持する。 </summary>
        [Serializable]
        internal sealed class AudioGroupSettings
        {
            /// <summary> AudioMixer内のグループ名。 </summary>
            public string AudioGroupName => _audioGroupName;

            /// <summary> 音量変更に使用する公開パラメーター名。 </summary>
            public string ExposedVolumeParameterName => _exposedParameterName;

            /// <summary> AudioSourceをループ再生するかを示す。 </summary>
            public bool IsLoop => _isLoop;

            [SerializeField, Tooltip("AudioMixer内のグループ名。")]
            private string _audioGroupName = string.Empty;

            [SerializeField, Tooltip("音量変更に使用する公開パラメーター名。")]
            private string _exposedParameterName = string.Empty;

            [SerializeField, Tooltip("このグループのAudioSourceをループ再生するか。")]
            private bool _isLoop = false;
        }
    }
}
