using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// A pen path in lettering coordinates. Each group of three points forms a cubic Bezier.
public sealed class AfterglowLettering : Graphic
{
    private static readonly Vector2[][] Strokes =
    {
        new Vector2[]
        {
            new(-35,-2), new(-18,-5), new(-4,10), new(11,15),
            // a
            new(23,24), new(24,7), new(16,13),
            new(4,22), new(-7,-9), new(6,-5),
            new(14,-3), new(18,8), new(21,15),
            new(17,3), new(13,-13), new(28,-3),
            // f
            new(38,6), new(64,52), new(54,53),
            new(44,54), new(32,4), new(29,-22),
            new(26,-40), new(44,-22), new(42,-12),
            new(40,-1), new(33,1), new(35,2),
            new(44,0), new(52,3), new(57,8),
            // t
            new(65,20), new(72,34), new(71,33),
            new(66,20), new(52,-10), new(63,-6),
            // e
            new(71,-4), new(87,6), new(84,12),
            new(79,20), new(66,5), new(72,-3),
            new(77,-12), new(90,-3), new(95,6),
            // r
            new(98,13), new(100,19), new(98,18),
            new(93,10), new(101,11), new(107,13),
            new(112,15), new(95,-8), new(106,-5),
            new(112,-3), new(119,8), new(125,11),
            // g
            new(134,18), new(135,9), new(132,11),
            new(120,20), new(109,-6), new(120,-5),
            new(126,-5), new(133,8), new(137,15),
            new(131,-1), new(125,-31), new(113,-34),
            new(99,-37), new(110,-20), new(131,-8),
            // l
            new(142,-1), new(172,44), new(164,47),
            new(154,51), new(135,0), new(143,-5),
            new(148,-9), new(156,-1), new(161,7),
            // o
            new(172,21), new(183,12), new(177,11),
            new(169,18), new(153,2), new(160,-5),
            new(168,-13), new(179,5), new(176,11),
            new(170,18), new(177,1), new(188,12),
            // w and exit flourish
            new(184,3), new(178,-11), new(188,-5),
            new(195,-1), new(201,13), new(201,13),
            new(199,6), new(192,-8), new(200,-6),
            new(208,-4), new(217,12), new(213,14),
            new(205,18), new(209,1), new(221,5),
            new(231,8), new(242,18), new(256,16)
        },
        // Cross the t on the return pass.
        new Vector2[] { new(51,19), new(60,21), new(70,21), new(81,23) }
    };

    private readonly List<Vector2[]> paths = new();
    private float length;
    [SerializeField, Range(0f, 1f)] private float written;

    private void BuildPaths()
    {
        foreach (var stroke in Strokes)
        {
            var samples = new List<Vector2> { stroke[0] };
            for (int i = 0; i + 3 < stroke.Length; i += 3)
            {
                for (int j = 1; j <= 18; j++)
                {
                    float t = j / 18f;
                    float u = 1f - t;
                    var point = u*u*u*stroke[i] + 3*u*u*t*stroke[i+1]
                        + 3*u*t*t*stroke[i+2] + t*t*t*stroke[i+3];
                    float distance = Vector2.Distance(samples[samples.Count - 1], point);
                    if (distance < 0.001f) continue;
                    length += distance;
                    samples.Add(point);
                }
            }
            paths.Add(samples.ToArray());
        }
    }

    public void SetInk(float progress)
    {
        written = progress;
        SetVerticesDirty();
    }

    internal static float LetterScale(Rect rect) =>
        Mathf.Min(rect.width * 0.68f / 291f, rect.height * 0.32f / 90f);

    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        if (paths.Count == 0) BuildPaths();
        var rect = rectTransform.rect;
        float scale = LetterScale(rect);
        float remaining = written * length;
        foreach (var path in paths)
        {
            int start = mesh.currentVertCount;
            for (int i = 0; i < path.Length && remaining > 0f; i++)
            {
                var point = path[i];
                if (i > 0)
                {
                    float segment = Vector2.Distance(path[i-1], point);
                    point = Vector2.Lerp(path[i-1], point, remaining / segment);
                    remaining -= segment;
                }
                var tangent = (path[Mathf.Min(i+1, path.Length-1)] - path[Mathf.Max(0, i-1)]).normalized;
                var normal = new Vector2(-tangent.y, tangent.x);
                float taper = Mathf.Clamp01(Mathf.Min(i + 1, path.Length - i) / 7f);
                float width = (0.62f + 1.15f * Mathf.Clamp01(-tangent.y)) * taper;
                var ink = new Color(1f, 0.73f, 0.52f);
                var edge = new Color(1f, 0.48f, 0.23f, 0f);
                var halo = new Color(1f, 0.48f, 0.23f, 0.18f);
                var center = rect.center + (point - new Vector2(110.5f, 9f)) * scale;
                var step = normal * scale;
                mesh.AddVert(center - step*(width+2.6f), edge, Vector2.zero);
                mesh.AddVert(center - step*(width+0.35f), halo, Vector2.zero);
                mesh.AddVert(center - step*width, ink, Vector2.zero);
                mesh.AddVert(center + step*width, ink, Vector2.zero);
                mesh.AddVert(center + step*(width+0.35f), halo, Vector2.zero);
                mesh.AddVert(center + step*(width+2.6f), edge, Vector2.zero);
                if (i == 0) continue;
                int previous = start + (i-1)*6;
                for (int lane = previous; lane < previous+5; lane++)
                {
                    mesh.AddTriangle(lane, lane+1, lane+6);
                    mesh.AddTriangle(lane+1, lane+7, lane+6);
                }
            }
        }
    }
}
