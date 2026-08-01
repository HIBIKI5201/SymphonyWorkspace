using DG.Tweening;
using UnityEngine;

namespace LibraryResearch.DOTweenSample
{
    [CreateAssetMenu(menuName = "Library Research/DOTween Sample Settings")]
    public sealed class DOTweenSampleSettings : ScriptableObject
    {
        [SerializeField] private Vector3 movement = new Vector3(4f, 0f, 0f);
        [SerializeField, Min(0.1f)] private float duration = 1.5f;
        [SerializeField] private Ease ease = Ease.InOutSine;

        public Vector3 Movement => movement;
        public float Duration => duration;
        public Ease Ease => ease;
    }
}
