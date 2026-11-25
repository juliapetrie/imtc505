using UnityEngine;
using UnityEngine.UI;

namespace Afterglow.SimpleTutorial
{
    // Actual UI geometry: no texture, no SVG importer or third-party package required.
    [AddComponentMenu("UI/Afterglow Simple Tutorial Vector Graphic")]
    public sealed class SimpleTutorialVectorGraphic : MaskableGraphic
    {
        [SerializeField] private Vector2[] vertices;
        [SerializeField] private int[] triangles;
        public void SetGeometry(Vector2[] points, int[] indices)
        {
            vertices = points; triangles = indices; SetVerticesDirty();
        }
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (vertices == null || triangles == null) return;
            Rect r = GetPixelAdjustedRect();
            foreach (Vector2 p in vertices)
                vh.AddVert(new Vector3(r.xMin + r.width*p.x, r.yMin + r.height*p.y, 0), color, Vector2.zero);
            for (int i=0; i+2<triangles.Length; i+=3)
                vh.AddTriangle(triangles[i],triangles[i+1],triangles[i+2]);
        }
    }
}
