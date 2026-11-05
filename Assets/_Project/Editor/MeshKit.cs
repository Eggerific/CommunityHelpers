using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace CommunityHelpers.EditorTools
{
    /// <summary>
    /// Generates simple meshes (rounded slabs, extruded icons) and saves them as assets.
    /// Convention: the front face points toward -Z (toward the player), the back sits at z = 0,
    /// so a panel's forward axis points "into" the board like a Unity UI canvas.
    /// </summary>
    public static class MeshKit
    {
        public const string MeshDir = "Assets/_Project/Meshes/Generated";

        /// <summary>Rounded-rectangle slab: width x height, depth toward -Z, corner radius r.</summary>
        public static Mesh RoundedSlab(string name, float width, float height, float depth, float radius, int cornerSegments = 8)
        {
            radius = Mathf.Clamp(radius, 0.0005f, Mathf.Min(width, height) * 0.5f - 0.0001f);
            float hx = width * 0.5f - radius, hy = height * 0.5f - radius;
            var outline = new List<Vector2>();
            var normals2D = new List<Vector2>();
            // Counter-clockwise, starting at the bottom-right corner arc.
            Vector2[] centers = { new(hx, -hy), new(hx, hy), new(-hx, hy), new(-hx, -hy) };
            for (int c = 0; c < 4; c++)
            {
                float start = -90f + c * 90f;
                for (int s = 0; s <= cornerSegments; s++)
                {
                    float a = (start + 90f * s / cornerSegments) * Mathf.Deg2Rad;
                    var n = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                    outline.Add(centers[c] + n * radius);
                    normals2D.Add(n);
                }
            }
            return Save(name, BuildSlab(outline, normals2D, depth, smoothSides: true));
        }

        /// <summary>Flat-sided extrusion of any simple polygon (concave is fine). Polygon is in meters.</summary>
        public static Mesh Extrude(string name, IList<Vector2> polygon, float depth)
        {
            var pts = new List<Vector2>(polygon);
            if (SignedArea(pts) < 0) pts.Reverse(); // make counter-clockwise
            return Save(name, BuildSlab(pts, null, depth, smoothSides: false));
        }

        // ------------------------------------------------------------------ building

        private static Mesh BuildSlab(List<Vector2> outline, List<Vector2> sideNormals, float depth, bool smoothSides)
        {
            var verts = new List<Vector3>();
            var norms = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();
            int n = outline.Count;

            var capTris = Triangulate(outline); // counter-clockwise triangles

            // Front cap (z = -depth, faces -Z). Unity front faces are clockwise as seen by the viewer.
            int f0 = verts.Count;
            foreach (var p in outline) { verts.Add(new Vector3(p.x, p.y, -depth)); norms.Add(Vector3.back); uvs.Add(p); }
            for (int i = 0; i < capTris.Count; i += 3)
            { tris.Add(f0 + capTris[i]); tris.Add(f0 + capTris[i + 2]); tris.Add(f0 + capTris[i + 1]); }

            // Back cap (z = 0, faces +Z).
            int b0 = verts.Count;
            foreach (var p in outline) { verts.Add(new Vector3(p.x, p.y, 0)); norms.Add(Vector3.forward); uvs.Add(p); }
            for (int i = 0; i < capTris.Count; i += 3)
            { tris.Add(b0 + capTris[i]); tris.Add(b0 + capTris[i + 1]); tris.Add(b0 + capTris[i + 2]); }

            // Sides.
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                Vector2 a = outline[i], b = outline[j];
                Vector3 na, nb;
                if (smoothSides && sideNormals != null)
                {
                    na = sideNormals[i];
                    nb = sideNormals[j];
                }
                else
                {
                    var e = (b - a).normalized;
                    na = nb = new Vector3(e.y, -e.x, 0); // outward for a counter-clockwise outline
                }
                int s = verts.Count;
                verts.Add(new Vector3(a.x, a.y, -depth)); norms.Add(na); uvs.Add(new Vector2(0, 0)); // F_i
                verts.Add(new Vector3(a.x, a.y, 0));      norms.Add(na); uvs.Add(new Vector2(0, 1)); // B_i
                verts.Add(new Vector3(b.x, b.y, -depth)); norms.Add(nb); uvs.Add(new Vector2(1, 0)); // F_j
                verts.Add(new Vector3(b.x, b.y, 0));      norms.Add(nb); uvs.Add(new Vector2(1, 1)); // B_j
                tris.Add(s); tris.Add(s + 2); tris.Add(s + 1);
                tris.Add(s + 1); tris.Add(s + 2); tris.Add(s + 3);
            }

            var mesh = new Mesh();
            mesh.SetVertices(verts);
            mesh.SetNormals(norms);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            return mesh;
        }

        private static float SignedArea(IList<Vector2> p)
        {
            float a = 0;
            for (int i = 0; i < p.Count; i++)
            {
                var q = p[(i + 1) % p.Count];
                a += p[i].x * q.y - q.x * p[i].y;
            }
            return a * 0.5f;
        }

        /// <summary>Ear-clipping triangulation of a counter-clockwise simple polygon. Returns CCW index triples.</summary>
        private static List<int> Triangulate(List<Vector2> poly)
        {
            var result = new List<int>();
            var idx = new List<int>();
            for (int i = 0; i < poly.Count; i++) idx.Add(i);

            int guard = 0;
            while (idx.Count > 3 && guard++ < 10000)
            {
                bool clipped = false;
                for (int i = 0; i < idx.Count; i++)
                {
                    int ia = idx[(i + idx.Count - 1) % idx.Count], ib = idx[i], ic = idx[(i + 1) % idx.Count];
                    Vector2 a = poly[ia], b = poly[ib], c = poly[ic];
                    if (Cross(b - a, c - b) <= 1e-9f) continue; // reflex or degenerate
                    bool contains = false;
                    foreach (int k in idx)
                    {
                        if (k == ia || k == ib || k == ic) continue;
                        if (PointInTri(poly[k], a, b, c)) { contains = true; break; }
                    }
                    if (contains) continue;
                    result.Add(ia); result.Add(ib); result.Add(ic);
                    idx.RemoveAt(i);
                    clipped = true;
                    break;
                }
                if (!clipped) break; // bad polygon; give up on the remainder
            }
            if (idx.Count == 3) { result.Add(idx[0]); result.Add(idx[1]); result.Add(idx[2]); }
            return result;
        }

        private static float Cross(Vector2 u, Vector2 v) => u.x * v.y - u.y * v.x;

        private static bool PointInTri(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            float d1 = Cross(b - a, p - a), d2 = Cross(c - b, p - b), d3 = Cross(a - c, p - c);
            return d1 >= 0 && d2 >= 0 && d3 >= 0;
        }

        // ------------------------------------------------------------------ assets

        private static Mesh Save(string name, Mesh mesh)
        {
            BuildKit.EnsureFolder(MeshDir);
            mesh.name = name;
            string path = $"{MeshDir}/{name}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null)
            {
                // Overwrite in place so scenes that reference the asset keep working.
                EditorUtility.CopySerialized(mesh, existing);
                existing.name = name;
                EditorUtility.SetDirty(existing);
                Object.DestroyImmediate(mesh);
                return existing;
            }
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

        /// <summary>Creates a GameObject showing a mesh.</summary>
        public static GameObject Show(string name, Transform parent, Mesh mesh, Material mat, Vector3 localPos = default, Vector3 localEuler = default)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localEulerAngles = localEuler;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return go;
        }
    }
}
