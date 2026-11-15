using System.Collections.Generic;
using System.IO;
using System.Linq;
using Oculus.Interaction;
using Oculus.Interaction.Grab;
using Oculus.Interaction.HandGrab;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using static CommunityHelpers.EditorTools.BuildKit;

namespace CommunityHelpers.EditorTools
{
    /// <summary>
    /// Turns the card models in Assets/CardS into working, grabbable card prefabs
    /// (Assets/_Project/Prefabs/Cards) and lays all 18 out face-up on the Game scene table.
    ///
    /// Each prefab: root tagged "Card" with BoxCollider, Rigidbody, Grabbable and CardScript;
    /// "Visual" child = the FBX, auto-oriented (art face-up, top of the art pointing away from the seat)
    /// and scaled to <see cref="CardLength"/>; "Grab" child = controller + hand grab interactables.
    /// Safe to re-run: prefabs are overwritten in place (same GUIDs) and the CardDesigns group is replaced.
    /// </summary>
    public static class CardDesignsBuilder
    {
        public const string PrefabDir = "Assets/_Project/Prefabs/Cards";
        private const string CardMaterialDir = MaterialDir + "/Cards";
        private const string GroupName = "CardDesigns";

        // ---- Card size (meters) ----
        // 12 cm: ~1.4x a real playing card, so K-2 hands can grab them and the art reads from the seat.
        private const float CardLength = 0.12f;    // long side; width follows the art's own aspect
        private const float MinThickness = 0.004f; // collider never thinner than this, so physics stays stable

        // ---- Layout on the table (table space: origin = table-top surface, -z = toward the player) ----
        // Two rows of nine centered in front of the seat, as close to the player's edge as fits (within reach).
        // Each row holds three helpers; each helper's helper / tool / workplace cards sit side by side.
        private const float EdgeMargin = 0.02f;    // gap between the near row and the table's near edge
        private const float RowGap = 0.02f;
        private const float ColumnGap = 0.015f, GroupGap = 0.04f;

        private static float NearRowZ => -MainMenuSceneBuilder.TableSize.y * 0.5f + EdgeMargin + CardLength * 0.5f;
        private static float[] RowZ => new[] { NearRowZ, NearRowZ + CardLength + RowGap };

        // ---- Texture sharpness (cards lie flat, so they're seen at a steep angle) ----
        private const int AnisoLevel = 16;

        private enum Kind { Helper, Tool, Workplace }

        // One entry per community helper: its helper, tool and workplace card models.
        private static readonly (string helper, string[] fbx)[] Catalog =
        {
            ("Doctor", new[] { "Assets/CardS/Doctor/DR card.fbx", "Assets/CardS/Tool for DR/DR_tool.fbx", "Assets/CardS/WP DR/WP DR.fbx" }),
            ("Firefighter", new[] { "Assets/CardS/FireFighter/Firefighter_card.fbx", "Assets/CardS/Tool for firefighter/Tool for firefighter.fbx", "Assets/CardS/WP Firefighter/WP Firefighter.fbx" }),
            ("Librarian", new[] { "Assets/CardS/Librarian/Librarian_card.fbx", "Assets/CardS/Tool for Lib/Tool for Lib.fbx", "Assets/CardS/WP Libririan/WP Libririan.fbx" }),
            ("MailCarrier", new[] { "Assets/CardS/Mail Carrier/Mailcarrier_card.fbx", "Assets/CardS/Tool for mail carrier/Tool for mail carrier.fbx", "Assets/CardS/WP MAIL/WP MAIL.fbx" }),
            ("PoliceOfficer", new[] { "Assets/CardS/Police officer/Police_card.fbx", "Assets/CardS/Tool for Police/Tool for Police.fbx", "Assets/CardS/WP Police Station/Workplace_police.fbx" }),
            ("Teacher", new[] { "Assets/CardS/Teacher/Teacher_card.fbx", "Assets/CardS/Tool for Teacher/Tool for Teacher.fbx", "Assets/CardS/WP TEACHER/WP TEACHER.fbx" }),
        };

        private struct BuiltCard
        {
            public int helperIndex;
            public Kind kind;
            public GameObject prefab;
        }

        // ------------------------------------------------------------------ Menu items

        // Placing the cards in the Game scene: Community Helpers > Update Game Table (see GameTableBuilder).

        [MenuItem("Community Helpers/Cards/Rebuild Card Prefabs", priority = 21)]
        public static void RebuildPrefabsMenu() => BuildAllPrefabs();

        // ------------------------------------------------------------------ Scene placement

        /// <summary>Builds the prefabs and lays them out on <paramref name="table"/>. Also called by the Game scene build.</summary>
        public static Transform PlaceCards(Transform table)
        {
            var cards = BuildAllPrefabs();
            if (cards.Count == 0) return null;

            var old = table.Find(GroupName);
            if (old != null) Object.DestroyImmediate(old.gameObject);
            EnsureTableCollider(table);

            var group = Group(GroupName, table);
            float w = cards.Max(c => c.prefab.GetComponent<BoxCollider>().size.x);
            float rowWidth = 9 * w + 6 * ColumnGap + 2 * GroupGap;
            float x0 = -rowWidth * 0.5f + w * 0.5f; // centered on the seat
            var rowZ = RowZ;

            foreach (var c in cards)
            {
                int row = c.helperIndex / 3, g = c.helperIndex % 3, col = g * 3 + (int)c.kind;
                float x = x0 + col * (w + ColumnGap) + g * (GroupGap - ColumnGap);
                float halfThick = c.prefab.GetComponent<BoxCollider>().size.y * 0.5f;

                var card = (GameObject)PrefabUtility.InstantiatePrefab(c.prefab, group);
                card.transform.localPosition = new Vector3(x, halfThick + 0.001f, rowZ[row]);
                card.transform.localRotation = Quaternion.identity;
            }

            if (rowWidth > MainMenuSceneBuilder.TableSize.x)
                Debug.LogWarning("[CardDesignsBuilder] The card rows are wider than the table; lower CardLength.");
            Debug.Log($"[CardDesignsBuilder] Placed {cards.Count} cards on the table.");
            return group;
        }

        /// <summary>The generated table top is decor-only (no collider), so physics cards would fall through it.</summary>
        private static void EnsureTableCollider(Transform table)
        {
            var top = table.Find("Top");
            if (top != null && top.GetComponent<Collider>() == null) top.gameObject.AddComponent<BoxCollider>();
        }

        // ------------------------------------------------------------------ Prefabs

        private static List<BuiltCard> BuildAllPrefabs()
        {
            EnsureFolder(PrefabDir);
            var built = new List<BuiltCard>();
            for (int h = 0; h < Catalog.Length; h++)
            for (int k = 0; k < 3; k++)
            {
                var prefab = BuildCardPrefab(Catalog[h].helper, (Kind)k, Catalog[h].fbx[k]);
                if (prefab != null) built.Add(new BuiltCard { helperIndex = h, kind = (Kind)k, prefab = prefab });
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"[CardDesignsBuilder] Built {built.Count} card prefabs in {PrefabDir}");
            return built;
        }

        private static GameObject BuildCardPrefab(string helper, Kind kind, string fbxPath)
        {
            // Orientation is worked out from the mesh's vertices, so the mesh must be readable.
            if (AssetImporter.GetAtPath(fbxPath) is ModelImporter importer && !importer.isReadable)
            {
                importer.isReadable = true;
                importer.SaveAndReimport();
            }
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
            if (model == null)
            {
                Debug.LogWarning("[CardDesignsBuilder] Missing card model: " + fbxPath);
                return null;
            }
            SharpenTextures(Path.GetDirectoryName(fbxPath).Replace('\\', '/'));

            string name = $"Card_{helper}_{kind}";
            var root = new GameObject(name); // at the world origin, so world space = card space while building
            try
            {
                root.tag = "Card";

                var visual = (GameObject)PrefabUtility.InstantiatePrefab(model);
                visual.name = "Visual";
                visual.transform.SetParent(root.transform, false);
                visual.transform.localPosition = Vector3.zero;
                visual.transform.localRotation = Quaternion.identity;
                visual.transform.localScale = Vector3.one;
                FixMissingTextures(visual, fbxPath);
                Vector3 size = FitVisual(visual.transform, fbxPath);

                var col = root.AddComponent<BoxCollider>();
                col.size = new Vector3(size.x, Mathf.Max(size.y, MinThickness), size.z);

                var rb = root.AddComponent<Rigidbody>();
                rb.mass = 0.05f;
                // No interpolation: SeatAnchor moves the room (and raises the table) at startup, and an
                // interpolated body ignores its parent moving, so the cards would be left behind and fall.
                rb.interpolation = RigidbodyInterpolation.None;
                rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

                var grabbable = root.AddComponent<Grabbable>();
                grabbable.InjectOptionalTargetTransform(root.transform);
                grabbable.InjectOptionalRigidbody(rb);
                grabbable.InjectOptionalKinematicWhileSelected(true);
                grabbable.InjectOptionalThrowWhenUnselected(true);

                root.AddComponent<CardScript>();

                // Same setup the HandGrab building block gives Van's test card: controller + hand grabs.
                var grab = new GameObject("Grab");
                grab.transform.SetParent(root.transform, false);
                var controllerGrab = grab.AddComponent<GrabInteractable>();
                controllerGrab.InjectRigidbody(rb);
                controllerGrab.InjectOptionalPointableElement(grabbable);
                var handGrab = grab.AddComponent<HandGrabInteractable>();
                handGrab.InjectRigidbody(rb);
                handGrab.InjectOptionalPointableElement(grabbable);
                handGrab.InjectSupportedGrabTypes(GrabTypeFlags.Pinch | GrabTypeFlags.Palm);

                return PrefabUtility.SaveAsPrefabAsset(root, $"{PrefabDir}/{name}.prefab");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        // ------------------------------------------------------------------ Orientation + size

        /// <summary>
        /// Rotates, scales and centers the model so the card lies flat in its parent's space:
        /// front art facing +Y, top of the art toward +Z, long side = <see cref="CardLength"/>.
        /// Returns the fitted card size (x = width, y = thickness, z = length).
        /// </summary>
        private static Vector3 FitVisual(Transform visual, string fbxPath)
        {
            var points = new List<Vector3>();
            Vector3 frontNormal = Vector3.zero, backNormal = Vector3.zero, artUp = Vector3.zero;

            foreach (var mf in visual.GetComponentsInChildren<MeshFilter>(true))
            {
                var mesh = mf.sharedMesh;
                if (mesh == null) continue;
                var toVisual = visual.worldToLocalMatrix * mf.transform.localToWorldMatrix;
                var verts = mesh.vertices.Select(v => toVisual.MultiplyPoint3x4(v)).ToArray();
                var uvs = mesh.uv;
                var renderer = mf.GetComponent<Renderer>();
                var mats = renderer != null ? renderer.sharedMaterials : new Material[0];
                points.AddRange(verts);

                for (int s = 0; s < mesh.subMeshCount; s++)
                {
                    var tris = mesh.GetTriangles(s);
                    Vector3 n = Vector3.zero; // area-weighted face normal of this material's faces
                    for (int t = 0; t + 2 < tris.Length; t += 3)
                        n += Vector3.Cross(verts[tris[t + 1]] - verts[tris[t]], verts[tris[t + 2]] - verts[tris[t]]);

                    if (IsBack(s < mats.Length ? mats[s] : null)) backNormal += n;
                    else
                    {
                        frontNormal += n;
                        if (uvs.Length == verts.Length) artUp += UvUpDirection(verts, uvs, tris);
                    }
                }
            }
            if (points.Count == 0)
            {
                Debug.LogWarning("[CardDesignsBuilder] No mesh in " + fbxPath);
                return new Vector3(CardLength * 0.65f, MinThickness, CardLength);
            }

            // Face-up direction: front faces' normal (and the back faces' opposite), snapped to the card's thin axis.
            var bounds = BoundsOf(points);
            Vector3 thinAxis = SmallestAxis(bounds.size);
            Vector3 up = frontNormal.normalized - backNormal.normalized;
            float sign = Vector3.Dot(up, thinAxis);
            up = thinAxis * (sign < 0f ? -1f : 1f);

            // Top of the art: the direction the front texture's V coordinate increases, snapped to an axis.
            Vector3 forward = Vector3.ProjectOnPlane(artUp, up);
            if (forward.sqrMagnitude < 1e-20f)
            {
                forward = LargestAxisPerpendicular(bounds.size, up);
                Debug.LogWarning($"[CardDesignsBuilder] Couldn't read the art direction of {fbxPath}; it may be upside down.");
            }
            forward = SnapToAxis(forward);

            var rotation = Quaternion.Inverse(Quaternion.LookRotation(forward, up));
            var fitted = BoundsOf(points.Select(p => rotation * p));
            float longSide = Mathf.Max(fitted.size.x, fitted.size.z);
            float scale = longSide > 0f ? CardLength / longSide : 1f;

            visual.localRotation = rotation;
            visual.localScale = Vector3.one * scale;
            visual.localPosition = -fitted.center * scale;
            return fitted.size * scale;
        }

        /// <summary>Direction (in mesh space) along which the texture's V coordinate grows, i.e. "up" on the art.</summary>
        private static Vector3 UvUpDirection(Vector3[] verts, Vector2[] uvs, int[] tris)
        {
            var idx = new HashSet<int>(tris);
            if (idx.Count == 0) return Vector3.zero;
            Vector3 meanP = Vector3.zero;
            float meanV = 0f;
            foreach (int i in idx) { meanP += verts[i]; meanV += uvs[i].y; }
            meanP /= idx.Count;
            meanV /= idx.Count;
            Vector3 dir = Vector3.zero;
            foreach (int i in idx) dir += (verts[i] - meanP) * (uvs[i].y - meanV);
            return dir;
        }

        private static bool IsBack(Material m) =>
            m != null && (m.name.ToLowerInvariant().Contains("back") ||
                          (m.mainTexture != null && m.mainTexture.name.ToLowerInvariant().Contains("back")));

        private static Bounds BoundsOf(IEnumerable<Vector3> points)
        {
            bool first = true;
            var b = new Bounds();
            foreach (var p in points)
            {
                if (first) { b = new Bounds(p, Vector3.zero); first = false; }
                else b.Encapsulate(p);
            }
            return b;
        }

        private static Vector3 SmallestAxis(Vector3 size) =>
            size.x <= size.y && size.x <= size.z ? Vector3.right : size.y <= size.z ? Vector3.up : Vector3.forward;

        private static Vector3 LargestAxisPerpendicular(Vector3 size, Vector3 normal)
        {
            Vector3 best = Vector3.forward;
            float bestLen = -1f;
            foreach (var (axis, len) in new[] { (Vector3.right, size.x), (Vector3.up, size.y), (Vector3.forward, size.z) })
            {
                if (Mathf.Abs(Vector3.Dot(axis, normal)) > 0.5f || len <= bestLen) continue;
                best = axis;
                bestLen = len;
            }
            return best;
        }

        private static Vector3 SnapToAxis(Vector3 v)
        {
            Vector3 a = new(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));
            if (a.x >= a.y && a.x >= a.z) return new Vector3(Mathf.Sign(v.x), 0, 0);
            if (a.y >= a.z) return new Vector3(0, Mathf.Sign(v.y), 0);
            return new Vector3(0, 0, Mathf.Sign(v.z));
        }

        // ------------------------------------------------------------------ Texture sharpness

        /// <summary>
        /// Import settings for card art seen flat on a table in VR. Unity's defaults (anisotropic level 1,
        /// normal-quality compression) smear the art badly at a steep viewing angle.
        /// Full-quality compression (BC7 on PC, ASTC 4x4 on Quest), 16x anisotropic, trilinear, Kaiser mips.
        /// </summary>
        private static void SharpenTextures(string folder)
        {
            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { folder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (AssetImporter.GetAtPath(path) is not TextureImporter ti) continue;
                if (ti.anisoLevel == AnisoLevel && ti.filterMode == FilterMode.Trilinear &&
                    ti.mipmapFilter == TextureImporterMipFilter.KaiserFilter &&
                    ti.textureCompression == TextureImporterCompression.CompressedHQ) continue;

                ti.anisoLevel = AnisoLevel;
                ti.filterMode = FilterMode.Trilinear;
                ti.mipmapEnabled = true;
                ti.mipmapFilter = TextureImporterMipFilter.KaiserFilter;
                ti.textureCompression = TextureImporterCompression.CompressedHQ;
                ti.SaveAndReimport();
            }
        }

        // ------------------------------------------------------------------ Materials

        /// <summary>
        /// The FBX files point at textures on the artist's machine (A:\UNCW\CARDS\...). Unity normally finds
        /// them next to the FBX by name; if it didn't, use a project material with the right texture instead.
        /// </summary>
        private static void FixMissingTextures(GameObject visual, string fbxPath)
        {
            string dir = Path.GetDirectoryName(fbxPath).Replace('\\', '/');
            var textures = AssetDatabase.FindAssets("t:Texture2D", new[] { dir })
                .Select(g => AssetDatabase.LoadAssetAtPath<Texture2D>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(t => t != null).ToList();
            var backTex = textures.FirstOrDefault(t => t.name.ToLowerInvariant().Contains("back"));
            var frontTex = textures.FirstOrDefault(t => t != backTex);

            foreach (var r in visual.GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < mats.Length; i++)
                {
                    if (mats[i] != null && mats[i].mainTexture != null) continue;
                    var tex = IsBack(mats[i]) ? backTex : frontTex;
                    if (tex == null) continue;
                    string matName = $"{Path.GetFileNameWithoutExtension(fbxPath)}_{(mats[i] != null ? mats[i].name : "Mat" + i)}";
                    mats[i] = CardMaterial(matName, tex);
                    changed = true;
                }
                if (changed)
                {
                    r.sharedMaterials = mats;
                    Debug.Log($"[CardDesignsBuilder] {fbxPath}: textures weren't linked on import; assigned them from {dir}.");
                }
            }
        }

        private static Material CardMaterial(string name, Texture2D tex)
        {
            EnsureFolder(CardMaterialDir);
            string path = $"{CardMaterialDir}/{name.Replace(' ', '_')}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Standard"));
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.mainTexture = tex;
            mat.SetFloat("_Glossiness", 0.1f);
            EditorUtility.SetDirty(mat);
            return mat;
        }
    }
}
