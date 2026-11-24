using UnityEngine;
using UnityEngine.UI;

// Small four-point glints in the same coordinate space as the handwritten strokes.
public sealed class AfterglowGlitter : Graphic
{
    private struct Fleck
    {
        public Vector2 position;
        public float radius, period, phase;
    }

    private readonly Fleck[] flecks = new Fleck[34];
    private float elapsed;

    protected override void Awake()
    {
        base.Awake();
        var random = new System.Random(505);
        float Next() => (float)random.NextDouble();
        for (int i = 0; i < flecks.Length; i++)
        {
            float angle = (i + Next()) * (2f * Mathf.PI / flecks.Length);
            float spread = Mathf.Lerp(0.7f, 1.1f, Next());
            flecks[i] = new Fleck
            {
                position = new Vector2(154f * Mathf.Cos(angle), 46f * Mathf.Sin(angle)) * spread,
                radius = Mathf.Lerp(0.8f, 2.25f, Next()),
                period = Mathf.Lerp(1.4f, 2.9f, Next()),
                phase = Next()
            };
        }
    }

    private void Update()
    {
        elapsed += Time.unscaledDeltaTime;
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        Rect rect = rectTransform.rect;
        float scale = AfterglowLettering.LetterScale(rect);
        float onset = Mathf.SmoothStep(0f, 1f, (elapsed - 0.35f) / 1.4f);
        foreach (var fleck in flecks)
        {
            float phase = Mathf.Repeat(elapsed / fleck.period + fleck.phase, 1f);
            if (phase >= 0.5f) continue;
            float pulse = Mathf.Sin(phase * 2f * Mathf.PI);
            float alpha = onset * pulse * pulse;
            var center = rect.center + (fleck.position + Vector2.up * (phase * 3f)) * scale;
            float radius = scale * fleck.radius * (0.55f + 0.45f * pulse);
            var core = new Color(1f, 0.91f, 0.75f, alpha);
            var tip = new Color(1f, 0.76f, 0.51f, alpha * 0.2f);
            int start = mesh.currentVertCount;
            mesh.AddVert(center, core, Vector2.zero);
            for (int i = 0; i < 8; i++)
            {
                float angle = i * Mathf.PI / 4f;
                bool isTip = i % 2 == 0;
                var direction = new Vector2(Mathf.Cos(angle) * 0.78f, Mathf.Sin(angle));
                mesh.AddVert(center + direction * radius * (isTip ? 1f : 0.25f),
                    isTip ? tip : core, Vector2.zero);
            }
            for (int i = 0; i < 8; i++)
                mesh.AddTriangle(start, start + 1 + (i + 1) % 8, start + 1 + i);
        }
    }
}
