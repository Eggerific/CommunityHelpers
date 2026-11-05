using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace CommunityHelpers.EditorTools
{
    /// <summary>
    /// Places Kenney models in the scene at real-world sizes.
    /// Each kit has its own units, so every kit is scaled by one factor measured from a reference model.
    /// Every model is wrapped in a container whose origin is the model's bottom-center and whose +Z is the
    /// direction the model faces, so placing and turning things is predictable.
    /// </summary>
    public static class KenneyKit
    {
        public const string Car = "car-kit";
        public const string Commercial = "city-kit-commercial";
        public const string Suburban = "city-kit-suburban";
        public const string Furniture = "furniture-kit";
        public const string Nature = "nature-kit";

        /// <summary>
        /// Yaw (degrees) that turns each kit's model so its front faces the container's +Z.
        /// If a kit shows up backwards in the headset, flip its value between 0 and 180 and rebuild.
        /// </summary>
        public static readonly Dictionary<string, float> FrontYaw = new()
        {
            // Checked in the headset 2026-11-05: Kenney models face -Z, so every kit turns 180.
            { Car, 180f },
            { Commercial, 180f },
            { Suburban, 180f },
            { Furniture, 180f },
            { Nature, 180f },
        };

        private static readonly Dictionary<string, float> KitScales = new();
        private static readonly Dictionary<string, Material> TintCache = new();

        public static void ClearCache()
        {
            KitScales.Clear();
            TintCache.Clear();
        }

        public static GameObject Load(string kit, string model)
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>($"{KenneyImportSettings.Root}/{kit}/{model}.fbx");
            if (go == null) Debug.LogWarning($"[KenneyKit] Missing model {kit}/{model}.fbx");
            return go;
        }

        /// <summary>Sets a kit's scale so that the reference model's size on the given axis equals targetMeters.</summary>
        public static void SetKitScale(string kit, string referenceModel, int axis, float targetMeters)
        {
            Vector3 size = Measure(kit, referenceModel);
            float s = size[axis] > 0.0001f ? targetMeters / size[axis] : 1f;
            KitScales[kit] = s;
        }

        public static float KitScale(string kit) => KitScales.TryGetValue(kit, out var s) ? s : 1f;

        /// <summary>Size of a model at kit scale 1, front-aligned (x = width, y = height, z = depth).</summary>
        public static Vector3 Measure(string kit, string model)
        {
            var asset = Load(kit, model);
            if (asset == null) return Vector3.zero;
            var temp = new GameObject("_measure");
            var pivot = new GameObject("pivot").transform;
            pivot.SetParent(temp.transform, false);
            pivot.localRotation = Quaternion.Euler(0, FrontYaw[kit], 0);
            Object.Instantiate(asset, pivot, false);
            var b = LocalBounds(temp.transform);
            Object.DestroyImmediate(temp);
            return b.size;
        }

        /// <summary>
        /// Places a model. pos is where its bottom-center goes (parent space); yaw turns its front.
        /// extraScale multiplies the kit scale. Returns the container; size gives its final size in meters.
        /// </summary>
        public static Transform Place(string kit, string model, Transform parent, Vector3 pos, float yaw,
            out Vector3 size, float extraScale = 1f, string name = null)
        {
            size = Vector3.zero;
            var asset = Load(kit, model);
            var container = new GameObject(name ?? model).transform;
            container.SetParent(parent, false);
            if (asset == null) return container;

            // container (placement) -> pivot (front yaw, scale, centering offset) -> model (untouched import transform)
            var pivot = new GameObject("pivot").transform;
            pivot.SetParent(container, false);
            pivot.localRotation = Quaternion.Euler(0, FrontYaw[kit], 0);
            pivot.localScale = Vector3.one * (KitScale(kit) * extraScale);
            PrefabUtility.InstantiatePrefab(asset, pivot);

            var b = LocalBounds(container);
            pivot.localPosition = -new Vector3(b.center.x, b.min.y, b.center.z);
            size = b.size;

            foreach (var r in container.GetComponentsInChildren<Renderer>(true))
            {
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
            }

            container.localPosition = pos;
            container.localRotation = Quaternion.Euler(0, yaw, 0);
            return container;
        }

        public static Transform Place(string kit, string model, Transform parent, Vector3 pos, float yaw = 0f,
            float extraScale = 1f, string name = null)
            => Place(kit, model, parent, pos, yaw, out _, extraScale, name);

        /// <summary>Tints every material on a placed model toward a color (saved as material assets).</summary>
        public static void Tint(Transform container, Color32 color, float amount, string key)
        {
            Color tint = Color.Lerp(Color.white, color, amount);
            foreach (var r in container.GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    if (mats[i] == null) continue;
                    string id = $"K_{key}_{mats[i].name}";
                    if (!TintCache.TryGetValue(id, out var tinted) || tinted == null)
                    {
                        string dir = BuildKit.MaterialDir + "/Tinted";
                        BuildKit.EnsureFolder(dir);
                        string path = $"{dir}/M_{id}.mat";
                        tinted = AssetDatabase.LoadAssetAtPath<Material>(path);
                        if (tinted == null)
                        {
                            tinted = new Material(mats[i]);
                            AssetDatabase.CreateAsset(tinted, path);
                        }
                        else
                        {
                            tinted.CopyPropertiesFromMaterial(mats[i]);
                        }
                        tinted.color = mats[i].color * tint;
                        EditorUtility.SetDirty(tinted);
                        TintCache[id] = tinted;
                    }
                    mats[i] = tinted;
                }
                r.sharedMaterials = mats;
            }
        }

        /// <summary>
        /// Height of the top of a placed model's front wall (container space), looking only at vertices
        /// within frontBand meters of the front face. Rooftop boxes set back from the front are ignored.
        /// Needs readable meshes (see KenneyImportSettings). Returns false if nothing was found.
        /// </summary>
        public static bool FrontTop(Transform container, float frontBand, out float topY, out float frontZ)
        {
            topY = 0f;
            frontZ = float.MinValue;
            var toRoot = container.worldToLocalMatrix;
            var points = new List<Vector3>();
            float maxY = float.MinValue;
            foreach (var mf in container.GetComponentsInChildren<MeshFilter>(true))
            {
                var mesh = mf.sharedMesh;
                if (mesh == null || !mesh.isReadable) continue;
                var m = toRoot * mf.transform.localToWorldMatrix;
                foreach (var v in mesh.vertices)
                {
                    var p = m.MultiplyPoint3x4(v);
                    points.Add(p);
                    if (p.y > maxY) maxY = p.y;
                }
            }
            if (points.Count == 0) return false;

            // Front face of the upper half only, so street-level awnings and overhangs don't count.
            foreach (var p in points)
                if (p.y > maxY * 0.5f && p.z > frontZ) frontZ = p.z;

            bool found = false;
            foreach (var p in points)
            {
                if (p.z < frontZ - frontBand) continue;
                if (!found || p.y > topY) { topY = p.y; found = true; }
            }
            return found;
        }

        /// <summary>Exact bounds of all meshes under root, in root's local space.</summary>
        public static Bounds LocalBounds(Transform root)
        {
            bool any = false;
            var bounds = new Bounds();
            var toRoot = root.worldToLocalMatrix;
            foreach (var mf in root.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh == null) continue;
                var mb = mf.sharedMesh.bounds;
                var m = toRoot * mf.transform.localToWorldMatrix;
                for (int i = 0; i < 8; i++)
                {
                    var corner = mb.center + Vector3.Scale(mb.extents,
                        new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    var p = m.MultiplyPoint3x4(corner);
                    if (!any) { bounds = new Bounds(p, Vector3.zero); any = true; }
                    else bounds.Encapsulate(p);
                }
            }
            foreach (var smr in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var p = toRoot.MultiplyPoint3x4(smr.bounds.center);
                if (!any) { bounds = new Bounds(p, smr.bounds.size); any = true; }
                else bounds.Encapsulate(new Bounds(p, smr.bounds.size));
            }
            return bounds;
        }
    }
}
