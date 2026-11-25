using UnityEngine;

namespace Afterglow.Tutorial
{
    public sealed class TutorialVisualPulse : MonoBehaviour
    {
        [SerializeField] private Renderer[] targetRenderers;
        [SerializeField] private string colorProperty = "_Color";
        [SerializeField] private Color idleColor = new Color(0.77f, 0.63f, 0.48f, 1f);
        [SerializeField] private Color completeColor = new Color(0.55f, 0.86f, 0.63f, 1f);
        [SerializeField] private float pulseSpeed = 2.4f;
        [SerializeField] private float pulseScale = 0.08f;

        private bool isCompleted;
        private Vector3 baseScale;

        private void Awake()
        {
            baseScale = transform.localScale;
        }

        private void Update()
        {
            if (isCompleted) return;
            float t = 1f + Mathf.Sin(Time.time * pulseSpeed) * pulseScale;
            transform.localScale = baseScale * t;
            ApplyColor(idleColor);
        }

        public void MarkCompleted(bool completed)
        {
            isCompleted = completed;
            transform.localScale = baseScale;
            ApplyColor(completed ? completeColor : idleColor);
        }

        private void ApplyColor(Color color)
        {
            if (targetRenderers == null) return;
            foreach (var r in targetRenderers)
            {
                if (r == null || r.sharedMaterial == null) continue;
                var block = new MaterialPropertyBlock();
                r.GetPropertyBlock(block);
                block.SetColor(colorProperty, color);
                r.SetPropertyBlock(block);
            }
        }
    }
}
