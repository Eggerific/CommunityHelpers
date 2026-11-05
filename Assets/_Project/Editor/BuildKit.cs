using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace CommunityHelpers.EditorTools
{
    /// <summary>Small helpers for building blockout geometry, materials and 3D text from editor scripts.</summary>
    public static class BuildKit
    {
        public const string MaterialDir = "Assets/_Project/Materials";
        private static readonly Dictionary<string, Material> Cache = new();

        public static void ClearCache() => Cache.Clear();

        /// <summary>Gets or creates a shared lit material asset with this color.</summary>
        public static Material Mat(string name, Color32 color, float smoothness = 0.15f)
        {
            if (Cache.TryGetValue(name, out var cached) && cached != null) return cached;

            EnsureFolder(MaterialDir);
            string path = $"{MaterialDir}/M_{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Standard"));
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.color = color;
            mat.SetFloat("_Glossiness", smoothness);
            mat.SetFloat("_Metallic", 0f);
            mat.enableInstancing = true;
            EditorUtility.SetDirty(mat);
            Cache[name] = mat;
            return mat;
        }

        /// <summary>Gets or creates an unlit color material (for the sky backdrop).</summary>
        public static Material Unlit(string name, Color32 color)
        {
            if (Cache.TryGetValue(name, out var cached) && cached != null) return cached;
            EnsureFolder(MaterialDir);
            string path = $"{MaterialDir}/M_{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Unlit/Color"));
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.color = color;
            EditorUtility.SetDirty(mat);
            Cache[name] = mat;
            return mat;
        }

        public static Transform Group(string name, Transform parent, Vector3 localPos = default, Vector3 localEuler = default)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localEulerAngles = localEuler;
            return go.transform;
        }

        public static GameObject Prim(PrimitiveType type, string name, Transform parent, Vector3 localPos,
            Vector3 localScale, Material mat, Vector3 localEuler = default)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>()); // decor only; ISDK surfaces don't need colliders
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localEulerAngles = localEuler;
            go.transform.localScale = localScale;
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return go;
        }

        public static GameObject Box(string name, Transform parent, Vector3 pos, Vector3 size, Material mat, Vector3 euler = default)
            => Prim(PrimitiveType.Cube, name, parent, pos, size, mat, euler);

        /// <summary>Box defined by min/max corners (handy for walls with holes).</summary>
        public static GameObject BoxMinMax(string name, Transform parent, Vector3 min, Vector3 max, Material mat)
            => Box(name, parent, (min + max) * 0.5f, max - min, mat);

        /// <summary>Cylinder with real radius and height (Unity's cylinder is 2 units tall).</summary>
        public static GameObject Cyl(string name, Transform parent, Vector3 pos, float radius, float height, Material mat, Vector3 euler = default)
            => Prim(PrimitiveType.Cylinder, name, parent, pos, new Vector3(radius * 2f, height * 0.5f, radius * 2f), mat, euler);

        public static GameObject Sphere(string name, Transform parent, Vector3 pos, Vector3 diameter, Material mat)
            => Prim(PrimitiveType.Sphere, name, parent, pos, diameter, mat);

        /// <summary>
        /// World-space TextMeshPro. emMeters is roughly the height of a line in meters
        /// (TMP 3D text uses 0.1 world units per point).
        /// </summary>
        public static TextMeshPro Text3D(string name, Transform parent, string text, TMP_FontAsset font,
            float emMeters, Color32 color, Vector3 localPos, Vector2 boxMeters, Vector3 localEuler = default,
            TextAlignmentOptions align = TextAlignmentOptions.Center)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var tmp = go.AddComponent<TextMeshPro>();
            tmp.text = text;
            if (font != null) tmp.font = font;
            tmp.fontSize = emMeters * 10f;
            tmp.color = color;
            tmp.alignment = align;
            tmp.textWrappingMode = TextWrappingModes.Normal;
            tmp.overflowMode = TextOverflowModes.Overflow;
            var rt = (RectTransform)go.transform;
            rt.sizeDelta = boxMeters;
            rt.localPosition = localPos;
            rt.localEulerAngles = localEuler;
            var r = go.GetComponent<MeshRenderer>();
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return tmp;
        }

        public static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
        }
    }
}
