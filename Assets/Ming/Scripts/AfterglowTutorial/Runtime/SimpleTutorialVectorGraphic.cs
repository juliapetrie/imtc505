using UnityEngine;
using UnityEngine.UI;

namespace Afterglow.SimpleTutorial
{
    // Keep the existing serialized fields and public API so saved prefabs remain compatible.
    [ExecuteAlways]
    [RequireComponent(typeof(CanvasRenderer))]
    [AddComponentMenu("UI/Afterglow Simple Tutorial Vector Graphic")]
    public sealed class SimpleTutorialVectorGraphic : MaskableGraphic
    {
        [SerializeField] private Vector2[] vertices;
        [SerializeField] private int[] triangles;

        public override Texture mainTexture => Texture2D.whiteTexture;

        public void SetGeometry(Vector2[] points, int[] indices)
        {
            vertices = points;
            triangles = indices;
            SetAllDirty();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            SetAllDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (vertices == null || triangles == null || vertices.Length < 3)
                return;

            // Use local RectTransform coordinates, including for a World Space Canvas.
            Rect r = rectTransform.rect;
            if (r.width <= 0f || r.height <= 0f)
                return;

            UIVertex v = UIVertex.simpleVert;
            v.color = color;
            v.uv0 = new Vector4(0.5f, 0.5f, 0f, 0f);
            for (int i = 0; i < vertices.Length; ++i)
            {
                Vector2 p = vertices[i];
                v.position = new Vector3(r.xMin + r.width * p.x,
                                         r.yMin + r.height * p.y, 0f);
                vh.AddVert(v);
            }

            for (int i = 0; i + 2 < triangles.Length; i += 3)
            {
                int a = triangles[i], b = triangles[i + 1], c = triangles[i + 2];
                if (a < 0 || b < 0 || c < 0 || a >= vertices.Length ||
                    b >= vertices.Length || c >= vertices.Length)
                    continue;
                Vector2 ab = vertices[b] - vertices[a];
                Vector2 ac = vertices[c] - vertices[a];
                float cross = ab.x * ac.y - ab.y * ac.x;
                // Match the clockwise winding used by standard uGUI graphics.
                if (cross > 0f)
                    vh.AddTriangle(a, c, b);
                else if (cross < 0f)
                    vh.AddTriangle(a, b, c);
            }
        }
    }
}
