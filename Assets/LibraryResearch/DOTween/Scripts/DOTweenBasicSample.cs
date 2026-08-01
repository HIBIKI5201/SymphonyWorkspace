using DG.Tweening;
using UnityEngine;

namespace LibraryResearch.DOTweenSample
{
    public sealed class DOTweenBasicSample : MonoBehaviour
    {
        [SerializeField] private DOTweenSampleSettings settings;
        [SerializeField] private Transform target;

        private Vector3 startPosition;
        private Tween tween;

        private void Start()
        {
            startPosition = target.position;
            Play();
        }

        private void OnDestroy()
        {
            tween?.Kill();
        }

        private void Play()
        {
            tween?.Kill();
            target.position = startPosition;
            tween = target
                .DOMove(startPosition + settings.Movement, settings.Duration)
                .SetEase(settings.Ease)
                .SetLoops(-1, LoopType.Yoyo);
        }

        private void OnGUI()
        {
            GUILayout.BeginArea(new Rect(24f, 24f, 420f, 250f), GUI.skin.box);
            GUILayout.Label("DOTween Basic Sample");
            GUILayout.Label($"Position: {target.position}");
            GUILayout.Label($"Duration: {settings.Duration:0.00}s / Ease: {settings.Ease}");

            if (GUILayout.Button("Restart looping tween"))
            {
                Play();
            }

            if (GUILayout.Button(tween != null && tween.IsPlaying() ? "Pause" : "Resume"))
            {
                if (tween != null && tween.IsPlaying()) tween.Pause();
                else tween?.Play();
            }

            if (GUILayout.Button("Complete and kill"))
            {
                tween?.Kill(true);
            }

            GUILayout.EndArea();
        }
    }
}
