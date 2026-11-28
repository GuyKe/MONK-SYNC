using UnityEngine;

namespace Monk.EditorTools
{
    /// <summary>Procedural boxes whose UVs come from world position, so textures tile continuously across neighbouring pieces.</summary>
    public static class MeshKit
    {
        /// <param name="worldCenter">Where the box will sit in the world (used only to compute UVs).</param>
        /// <param name="size">Full size of the box.</param>
        /// <param name="metersPerTile">World size of one texture repeat.</param>
        public static Mesh Box(Vector3 worldCenter, Vector3 size, float metersPerTile)
        {
            Vector3 half = size * 0.5f;
            var verts = new Vector3[24];
            var norms = new Vector3[24];
            var uvs = new Vector2[24];
            var tris = new int[36];

            Vector3[] normals = { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };
            for (int f = 0; f < 6; f++)
            {
                Vector3 n = normals[f];
                // v axis is world up, except on the horizontal faces where it is +Z.
                Vector3 b = (Mathf.Abs(n.y) > 0.5f) ? Vector3.forward : Vector3.up;
                Vector3 t = Vector3.Cross(n, b);

                Vector3 c = Vector3.Scale(n, half);
                float hx = Mathf.Abs(t.x) * half.x + Mathf.Abs(t.y) * half.y + Mathf.Abs(t.z) * half.z;
                float hy = Mathf.Abs(b.x) * half.x + Mathf.Abs(b.y) * half.y + Mathf.Abs(b.z) * half.z;

                Vector3[] corner =
                {
                    c - t * hx - b * hy,
                    c - t * hx + b * hy,
                    c + t * hx + b * hy,
                    c + t * hx - b * hy,
                };

                for (int i = 0; i < 4; i++)
                {
                    int idx = f * 4 + i;
                    verts[idx] = corner[i];
                    norms[idx] = n;
                    Vector3 w = worldCenter + corner[i];
                    Vector2 uv;
                    if (Mathf.Abs(n.x) > 0.5f) uv = new Vector2(w.z, w.y);
                    else if (Mathf.Abs(n.y) > 0.5f) uv = new Vector2(w.x, w.z);
                    else uv = new Vector2(w.x, w.y);
                    uvs[idx] = uv / metersPerTile;
                }

                int ti = f * 6, vi = f * 4;
                tris[ti] = vi; tris[ti + 1] = vi + 1; tris[ti + 2] = vi + 2;
                tris[ti + 3] = vi; tris[ti + 4] = vi + 2; tris[ti + 5] = vi + 3;
            }

            var mesh = new Mesh { name = "Box" };
            mesh.vertices = verts;
            mesh.normals = norms;
            mesh.uv = uvs;
            mesh.triangles = tris;
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
