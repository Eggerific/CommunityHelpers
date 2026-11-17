using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using static CommunityHelpers.EditorTools.BuildKit;
using B = CommunityHelpers.EditorTools.MenuBoardBuilder;

namespace CommunityHelpers.EditorTools
{
    /// <summary>
    /// The people outside the Game scene's window (Kenney Mini Characters) and the hint system (HintGiver).
    /// Builds, under World/TownPeople:
    /// - a garden path outside the window (4.3 to 5.8 m from the seat; the street starts at 8.1 m),
    /// - one walker per helper (only this game's helpers show up at runtime), plus any townsfolk listed,
    /// - a speech bubble on each,
    /// - HintGiver, wired to CardGame, CardTable, Robo and the HintBook asset.
    /// Also makes the Animator controller (Idle, Walk, Gesture, Nod) and the HintBook (first drafts from
    /// Docs/Hints.md; never overwritten once it exists, so the team's edits are kept).
    /// Called by GameTableBuilder (Community Helpers > Update Game Table).
    /// Space: World's local space = meters from the seat (x right, y up from the floor, z toward the window).
    /// </summary>
    public static class TownPeopleBuilder
    {
        public const string GroupName = "TownPeople";
        private const string AnimDir = "Assets/_Project/Animation";
        private const string DataDir = "Assets/_Project/Data";
        public const string HintBookPath = DataDir + "/HintBook.asset";
        private const string Kit = KenneyKit.Characters;
        private const string BaseModel = "character-male-c";

        // ---- Path (meters from the seat). The back wall's outside face is at z = 3.05. At ~5 m the window sill only
        //      hides their legs (the sight line over the sill is ~0.45 m up there), and they look human-sized. ----
        private const float PathNearZ = 4.3f, PathFarZ = 5.8f, PathHalfWidth = 8f;
        private const float PathTop = 0.3f; // a low raised terrace, so the window sill and its plants hide less of them
        private const float TalkZ = 4.55f;                       // where a helper stands to give a hint
        private static readonly float[] Lanes = { 4.9f, 5.2f, 5.5f };
        // They stroll well past the window's edges (it shows about x = ±3.8 m at the path), so only about half
        // of them are in view at a time and the window doesn't feel crowded.
        private static readonly Vector2 PathX = new(-7.5f, 7.5f);
        private const float PersonHeight = 1.15f;                 // chunky cartoon people: big heads, short legs

        // Animator states → clips (names from the Mini Characters pack).
        private static readonly (string state, string clip)[] States =
        {
            (TownWalker.Idle, "idle"), (TownWalker.Walk, "walk"), (TownWalker.Gesture, "interact-right"), (TownWalker.Nod, "emote-yes"),
        };

        // Who plays each helper (helper names match CardDesignsBuilder.Catalog and the card categories).
        private static readonly (string helper, string model)[] Cast =
        {
            ("Firefighter", "character-worker"),   // hard hat, recolored red
            ("Doctor", "character-female-e"),      // white coat
            ("Librarian", "character-male-e"),     // glasses and cardigan
            ("PoliceOfficer", "character-male-c"), // blue cap and uniform
            ("Teacher", "character-female-d"),     // blazer
            ("MailCarrier", "character-female-c"), // blue shirt, red bag strap
        };
        // No extra townsfolk for now (team call, Nov 17: too crowded). Add models here to bring some back.
        private static readonly string[] Townsfolk = { };

        [MenuItem("Community Helpers/Setup/Reimport Mini Characters")]
        public static void ReimportCharacters() =>
            AssetDatabase.ImportAsset($"{KenneyImportSettings.Root}/{Kit}", ImportAssetOptions.ImportRecursive | ImportAssetOptions.ForceUpdate);

        // The Main Menu just has a few helpers strolling past (no hints there).
        private static readonly string[] MenuHelpers = { "Firefighter", "Doctor", "PoliceOfficer" };

        /// <param name="table">World/Table (CardGame and Robo live under it); its parent is World.</param>
        /// <param name="game">
        /// Game scene: every helper, plus the hint system. Main Menu (false): only <see cref="MenuHelpers"/>
        /// strolling, nothing else.
        /// </param>
        public static void Build(Transform table, bool game = true)
        {
            var world = table.parent;
            var old = world.Find(GroupName);
            if (old != null) Object.DestroyImmediate(old.gameObject);

            if (KenneyKit.Load(Kit, BaseModel) == null)
            {
                Debug.LogError($"[TownPeopleBuilder] No Mini Characters in {KenneyImportSettings.Root}/{Kit}. Hints are off.");
                return;
            }
            // Finding each hand (for the props) needs readable meshes; older imports aren't.
            if (AssetImporter.GetAtPath($"{KenneyImportSettings.Root}/{Kit}/{BaseModel}.fbx") is ModelImporter mi && !mi.isReadable)
                ReimportCharacters();

            // Scale is set per person by FitHeight (the kit-wide measurement can't read skinned meshes).
            var controller = BuildController(out var baseClips);
            var hints = game ? LoadOrCreateHintBook() : null;

            var group = Group(GroupName, world);
            // A grassy terrace with the path along its top.
            Box("Terrace", group, new Vector3(0, PathTop * 0.5f - 0.02f, (PathNearZ + PathFarZ) * 0.5f),
                new Vector3(PathHalfWidth * 2f, PathTop, PathFarZ - PathNearZ + 0.4f), Mat("Grass", UIColors.Grass));
            Box("GardenPath", group, new Vector3(0, PathTop - 0.015f, (PathNearZ + PathFarZ) * 0.5f),
                new Vector3(PathHalfWidth * 2f, 0.03f, PathFarZ - PathNearZ), Mat("GardenPath", UIColors.GardenPath));

            var cast = game ? Cast : Cast.Where(c => MenuHelpers.Contains(c.helper)).ToArray();
            var helperWalkers = new List<HintGiver.HelperWalker>();
            for (int i = 0; i < cast.Length; i++)
            {
                float x = cast.Length > 1 ? Mathf.Lerp(PathX.x + 0.5f, PathX.y - 0.5f, i / (float)(cast.Length - 1)) : 0f;
                var walker = Person(group, cast[i].model, cast[i].helper, new Vector3(x, PathTop, Lanes[i % Lanes.Length]),
                    controller, baseClips);
                if (walker == null) continue;
                if (cast[i].helper == "Firefighter") RecolorHelmet(walker.transform.Find("pivot")); // the model only, not the bubble
                GiveProp(walker.transform, cast[i].helper);
                helperWalkers.Add(new HintGiver.HelperWalker { helper = cast[i].helper, walker = walker });
            }
            if (!game)
            {
                Debug.Log($"[TownPeopleBuilder] Main Menu: {helperWalkers.Count} helpers strolling outside the window.");
                return;
            }
            var folks = new List<TownWalker>();
            for (int i = 0; i < Townsfolk.Length; i++)
            {
                var pos = new Vector3(i == 0 ? 3.6f : -3.2f, PathTop, Lanes[(i + 1) % Lanes.Length]);
                var walker = Person(group, Townsfolk[i], "Townsperson_" + (i + 1), pos, controller, baseClips);
                if (walker != null) folks.Add(walker);
            }

            var giver = group.gameObject.AddComponent<HintGiver>();
            var robo = table.Find("Robo");
            giver.EditorSetup(table.GetComponentInChildren<CardGame>(true), table.GetComponentInChildren<CardTable>(true),
                robo != null ? robo.GetComponent<RoboController>() : null, hints, helperWalkers.ToArray(), folks.ToArray(), TalkZ);
            EditorUtility.SetDirty(giver);
            Debug.Log($"[TownPeopleBuilder] {helperWalkers.Count} helpers and {folks.Count} townsfolk outside the window.");
        }

        // ------------------------------------------------------------------ People

        private static TownWalker Person(Transform group, string model, string name, Vector3 pos,
            AnimatorController controller, Dictionary<string, AnimationClip> baseClips)
        {
            // Facing along the path, alternating directions so they don't all set off the same way.
            float yaw = (pos.x * 10f) % 2f < 1f ? 90f : -90f;
            var c = KenneyKit.Place(Kit, model, group, pos, yaw, out _, 1f, name);
            var pivot = c.Find("pivot");
            if (pivot == null || pivot.childCount == 0) return null;
            FitHeight(c, pivot, PersonHeight);

            var instance = pivot.GetChild(0).gameObject;
            var animator = instance.GetComponent<Animator>();
            if (animator == null) animator = instance.AddComponent<Animator>();
            animator.runtimeAnimatorController = OverrideFor(model, controller, baseClips);
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;

            var head = Group("Head", c, new Vector3(0, PersonHeight * 0.8f, 0));
            var bubble = BuildBubble(c, PersonHeight, out var text);
            var walker = c.gameObject.AddComponent<TownWalker>();
            walker.EditorSetup(animator, bubble, text, head, PathX, pos.z);
            EditorUtility.SetDirty(walker);
            return walker;
        }

        // ------------------------------------------------------------------ Props (what each helper carries)

        /// <summary>
        /// Puts the helper's thing in their right hand, so they're easy to tell apart from the seat.
        /// The prop hangs from the hand (its grip is its origin) and swings with the arm as they walk.
        /// </summary>
        private static void GiveProp(Transform person, string helper)
        {
            Transform prop = helper switch
            {
                "Doctor" => KitProp(Kit, "aid-defibrillator-red", 0.26f),             // red medical case
                "Librarian" => KitProp(KenneyKit.Furniture, "books", 0.24f),          // stack of books
                "MailCarrier" => KitProp(KenneyKit.Furniture, "cardboardBoxClosed", 0.26f), // parcel
                "Firefighter" => Extinguisher(),
                "PoliceOfficer" => StopSign(),
                "Teacher" => Apple(),
                _ => null,
            };
            if (prop == null) return;

            var arm = FindDeep(person, "arm-right");
            var animator = person.GetComponentInChildren<Animator>();
            if (arm == null || animator == null)
            {
                Object.DestroyImmediate(prop.gameObject);
                return;
            }
            // Pose the arm as it hangs in Idle, so the prop is set straight relative to a lowered arm.
            var idle = animator.runtimeAnimatorController.animationClips.FirstOrDefault(c => c.name == "idle");
            if (idle != null) idle.SampleAnimation(animator.gameObject, 0f);

            Vector3 hand = HandPoint(person, arm);
            Vector3 outward = Vector3.ProjectOnPlane(hand - person.position, Vector3.up).normalized;
            prop.SetPositionAndRotation(hand + outward * 0.03f, person.rotation); // a little out from the leg
            prop.SetParent(arm, true);
            prop.name = "Prop";
        }

        /// <summary>The end of the arm: the arm vertex farthest from its shoulder joint (from the skin weights).</summary>
        private static Vector3 HandPoint(Transform person, Transform arm)
        {
            foreach (var smr in person.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                int bi = System.Array.IndexOf(smr.bones, arm);
                var mesh = smr.sharedMesh;
                if (bi < 0 || mesh == null || !mesh.isReadable) continue;
                var weights = mesh.boneWeights;
                var verts = mesh.vertices;
                var bind = mesh.bindposes[bi];
                Vector3 best = Vector3.zero;
                float bestD = -1f;
                for (int i = 0; i < verts.Length && i < weights.Length; i++)
                {
                    var w = weights[i];
                    float wt = w.boneIndex0 == bi ? w.weight0 : w.boneIndex1 == bi ? w.weight1
                             : w.boneIndex2 == bi ? w.weight2 : w.boneIndex3 == bi ? w.weight3 : 0f;
                    if (wt < 0.5f) continue;
                    var p = bind.MultiplyPoint3x4(verts[i]); // in the arm bone's own space
                    if (p.sqrMagnitude > bestD) { bestD = p.sqrMagnitude; best = p; }
                }
                if (bestD >= 0f) return arm.TransformPoint(best);
            }
            Debug.LogWarning($"[TownPeopleBuilder] Couldn't find the hand on {person.name}; the prop sits at the shoulder.");
            return arm.position;
        }

        private static Transform FindDeep(Transform root, string name)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == name) return t;
            return null;
        }

        /// <summary>A kit model scaled so its largest side is <paramref name="size"/> m, hanging below its grip (the origin).</summary>
        private static Transform KitProp(string kit, string model, float size)
        {
            var grip = new GameObject("Prop_" + model).transform;
            var c = KenneyKit.Place(kit, model, grip, Vector3.zero, 0f, out var s);
            float k = size / Mathf.Max(0.0001f, Mathf.Max(s.x, Mathf.Max(s.y, s.z)));
            c.localScale = Vector3.one * k;
            c.localPosition = new Vector3(0f, -s.y * k, 0f); // top of the model at the hand
            return grip;
        }

        /// <summary>Small red fire extinguisher: body, black top and handle, a short hose.</summary>
        private static Transform Extinguisher()
        {
            var grip = new GameObject("Prop_Extinguisher").transform;
            var red = Mat("Prop_Red", UIColors.FireRed, 0.4f);
            var dark = Mat("Prop_Dark", UIColors.Ink);
            Cyl("Body", grip, new Vector3(0, -0.15f, 0), 0.045f, 0.2f, red);
            Sphere("Shoulder", grip, new Vector3(0, -0.05f, 0), new Vector3(0.09f, 0.04f, 0.09f), red);
            Cyl("Top", grip, new Vector3(0, -0.02f, 0), 0.016f, 0.03f, dark);
            Box("Handle", grip, new Vector3(0, -0.002f, 0.01f), new Vector3(0.016f, 0.012f, 0.06f), dark);
            Box("Hose", grip, new Vector3(0, -0.09f, 0.05f), new Vector3(0.012f, 0.1f, 0.012f), dark, new Vector3(-12f, 0, 0));
            return grip;
        }

        /// <summary>Crossing-guard stop sign on a pole, held partway up the pole, the sign facing out to the sides.</summary>
        private static Transform StopSign()
        {
            var grip = new GameObject("Prop_StopSign").transform;
            Cyl("Pole", grip, new Vector3(0, 0.1f, 0), 0.009f, 0.5f, Mat("Prop_Pole", UIColors.Mist));
            // The sign faces sideways (along x): people walk along the path, so their side faces the window.
            var sign = Group("Sign", grip, new Vector3(0, 0.42f, 0), new Vector3(0, 90f, 0));
            var oct = new Vector2[8];
            for (int i = 0; i < 8; i++)
            {
                float a = Mathf.PI / 8f + i * Mathf.PI / 4f;
                oct[i] = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 0.09f;
            }
            MeshKit.Show("Octagon", sign, MeshKit.Extrude("Prop_StopOctagon", oct, 0.01f), Mat("Prop_Red", UIColors.FireRed, 0.4f),
                new Vector3(0, 0, 0.005f));
            var bold = FontSetup.Load(FontSetup.FredokaBold);
            // Both faces say STOP.
            Text3D("Front", sign, "STOP", bold, 0.045f, UIColors.Cream, new Vector3(0, 0, -0.0062f), new Vector2(0.15f, 0.06f));
            Text3D("Back", sign, "STOP", bold, 0.045f, UIColors.Cream, new Vector3(0, 0, 0.0062f), new Vector2(0.15f, 0.06f),
                new Vector3(0, 180f, 0));
            return grip;
        }

        /// <summary>Shiny red apple with a stem and a leaf (the teacher's).</summary>
        private static Transform Apple()
        {
            var grip = new GameObject("Prop_Apple").transform;
            Sphere("Apple", grip, new Vector3(0, -0.055f, 0), new Vector3(0.1f, 0.09f, 0.1f), Mat("Prop_Red", UIColors.FireRed, 0.4f));
            Cyl("Stem", grip, new Vector3(0, -0.005f, 0), 0.005f, 0.025f, Mat("WoodDark", UIColors.WoodDark));
            Box("Leaf", grip, new Vector3(0.016f, -0.002f, 0), new Vector3(0.028f, 0.004f, 0.014f), Mat("Prop_Leaf", UIColors.Leaf), new Vector3(0, 0, 25f));
            return grip;
        }

        /// <summary>
        /// Scales the model so it stands <paramref name="height"/> tall with its feet on the container's origin.
        /// KenneyKit's bounds can't read skinned meshes before they're drawn, so this bakes them in their rest pose.
        /// </summary>
        private static void FitHeight(Transform container, Transform pivot, float height)
        {
            for (int pass = 0; pass < 2; pass++) // pass 0: scale; pass 1: put the feet on the ground, centered
            {
                var b = BakedBounds(container);
                if (b.size.y < 0.0001f) return;
                if (pass == 0) pivot.localScale *= height / b.size.y;
                else pivot.localPosition -= new Vector3(b.center.x, b.min.y, b.center.z);
            }
        }

        private static Bounds BakedBounds(Transform root)
        {
            bool any = false;
            var bounds = new Bounds();
            var toRoot = root.worldToLocalMatrix;
            var mesh = new Mesh();
            foreach (var smr in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                smr.BakeMesh(mesh, true); // vertices in the renderer's space, its scale included
                var m = toRoot * Matrix4x4.TRS(smr.transform.position, smr.transform.rotation, Vector3.one);
                foreach (var v in mesh.vertices)
                {
                    var p = m.MultiplyPoint3x4(v);
                    if (!any) { bounds = new Bounds(p, Vector3.zero); any = true; }
                    else bounds.Encapsulate(p);
                }
            }
            Object.DestroyImmediate(mesh);
            return bounds;
        }

        /// <summary>
        /// Cream speech bubble with an ink outline and a tail, over the person's head. Built facing -Z;
        /// TownWalker turns it to face the player. Sized to read from the seat (about 3.5 m away).
        /// </summary>
        private static Transform BuildBubble(Transform person, float height, out TMPro.TMP_Text text)
        {
            var bubble = Group("Bubble", person, new Vector3(0, height + 0.32f, 0));
            var size = new Vector2(0.62f, 0.34f);
            const float edge = 0.025f;
            var ink = Mat("BubbleEdge", UIColors.BubbleEdge);
            var cream = Mat("Cream", UIColors.Cream);
            MeshKit.Show("Edge", bubble, MeshKit.RoundedSlab("Bubble_Edge", size.x + edge * 2, size.y + edge * 2, 0.02f, 0.14f), ink);
            MeshKit.Show("Fill", bubble, MeshKit.RoundedSlab("Bubble_Fill", size.x, size.y, 0.02f, 0.12f), cream, new Vector3(0, 0, -0.01f));

            // Tail pointing down toward the speaker's head (outline triangle behind a cream one).
            var tail = Group("Tail", bubble, new Vector3(-0.1f, -size.y * 0.5f + 0.01f, -0.012f));
            B.Icon("BubbleTail_Edge", tail, new[] { new Vector2(-0.6f, 0.4f), new Vector2(0.6f, 0.4f), new Vector2(-0.25f, -0.75f) }, 0.12f, UIColors.BubbleEdge, Vector2.zero);
            var tailFill = Group("Fill", tail, new Vector3(0, 0.012f, -0.005f));
            B.Icon("BubbleTail_Fill", tailFill, new[] { new Vector2(-0.45f, 0.4f), new Vector2(0.45f, 0.4f), new Vector2(-0.22f, -0.55f) }, 0.12f, UIColors.Cream, Vector2.zero);

            var tmp = Text3D("Text", bubble, "...", FontSetup.Load(FontSetup.FredokaBold), 0.16f, UIColors.Ink,
                new Vector3(0, 0.01f, -0.035f), new Vector2(size.x - 0.06f, size.y - 0.04f));
            tmp.enableAutoSizing = true; // short "..." is big; a full hint line shrinks to fit
            tmp.fontSizeMin = 0.5f;
            tmp.fontSizeMax = 3f;   // big "..."
            text = tmp;

            bubble.gameObject.SetActive(false);
            return bubble;
        }

        /// <summary>
        /// The pack has no firefighter, so the worker's yellow hard hat becomes a red helmet: a copy of the
        /// color map with its yellows turned red, on a material only this character uses.
        /// </summary>
        private static void RecolorHelmet(Transform person)
        {
            string src = $"{KenneyImportSettings.Root}/{Kit}/Textures/colormap.png";
            string dir = MaterialDir + "/Characters";
            EnsureFolder(dir);
            string texPath = $"{dir}/colormap_firefighter.png";
            if (!File.Exists(src)) return;

            var tex = new Texture2D(2, 2);
            tex.LoadImage(File.ReadAllBytes(src));
            var px = tex.GetPixels32();
            Color.RGBToHSV(UIColors.FireRed, out float redH, out _, out _);
            for (int i = 0; i < px.Length; i++)
            {
                Color.RGBToHSV(px[i], out float h, out float s, out float v);
                if (h >= 0.09f && h <= 0.18f && s > 0.45f && v > 0.5f) // yellows and golds
                {
                    var c = (Color32)Color.HSVToRGB(redH, Mathf.Max(s, 0.7f), v * 0.92f);
                    c.a = px[i].a;
                    px[i] = c;
                }
            }
            tex.SetPixels32(px);
            File.WriteAllBytes(texPath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(texPath);
            if (AssetImporter.GetAtPath(texPath) is TextureImporter ti && ti.filterMode != FilterMode.Point)
            {
                ti.filterMode = FilterMode.Point; // a palette: no blending between color cells
                ti.mipmapEnabled = false;
                ti.SaveAndReimport();
            }
            var redTex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);

            foreach (var r in person.GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    if (mats[i] == null) continue;
                    string matPath = $"{dir}/M_Firefighter_{mats[i].name}.mat";
                    var m = AssetDatabase.LoadAssetAtPath<Material>(matPath);
                    if (m == null)
                    {
                        m = new Material(mats[i]);
                        AssetDatabase.CreateAsset(m, matPath);
                    }
                    else m.CopyPropertiesFromMaterial(mats[i]);
                    m.mainTexture = redTex;
                    EditorUtility.SetDirty(m);
                    mats[i] = m;
                }
                r.sharedMaterials = mats;
            }
        }

        // ------------------------------------------------------------------ Animation

        private static AnimationClip Clip(string model, string name) =>
            AssetDatabase.LoadAllAssetsAtPath($"{KenneyImportSettings.Root}/{Kit}/{model}.fbx")
                .OfType<AnimationClip>()
                .FirstOrDefault(c => !c.name.StartsWith("__preview__") && KenneyImportSettings.ClipName(c.name) == name);

        /// <summary>One controller (from the base model's clips); each other character gets an override with its own clips.</summary>
        private static AnimatorController BuildController(out Dictionary<string, AnimationClip> baseClips)
        {
            EnsureFolder(AnimDir);
            string path = $"{AnimDir}/MiniCharacter.controller";
            AssetDatabase.DeleteAsset(path);
            var ctrl = AnimatorController.CreateAnimatorControllerAtPath(path);
            var sm = ctrl.layers[0].stateMachine;

            baseClips = new Dictionary<string, AnimationClip>();
            AnimatorState idle = null;
            var oneShots = new List<AnimatorState>();
            foreach (var (state, clipName) in States)
            {
                var clip = Clip(BaseModel, clipName);
                if (clip == null) Debug.LogWarning($"[TownPeopleBuilder] {BaseModel} has no \"{clipName}\" animation.");
                baseClips[clipName] = clip;
                var st = sm.AddState(state);
                st.motion = clip;
                if (state == TownWalker.Idle) idle = st;
                if (state == TownWalker.Gesture || state == TownWalker.Nod) oneShots.Add(st);
            }
            sm.defaultState = idle;
            // Gesture and Nod play once, then settle back into Idle.
            foreach (var st in oneShots)
            {
                var back = st.AddTransition(idle);
                back.hasExitTime = true;
                back.exitTime = 0.95f;
                back.duration = 0.15f;
            }
            EditorUtility.SetDirty(ctrl);
            return ctrl;
        }

        private static RuntimeAnimatorController OverrideFor(string model, AnimatorController ctrl, Dictionary<string, AnimationClip> baseClips)
        {
            if (model == BaseModel) return ctrl;
            string path = $"{AnimDir}/MiniCharacter_{model.Replace("character-", "")}.overrideController";
            AssetDatabase.DeleteAsset(path);
            var ov = new AnimatorOverrideController(ctrl);
            var pairs = new List<KeyValuePair<AnimationClip, AnimationClip>>();
            foreach (var (_, clipName) in States)
            {
                var from = baseClips[clipName];
                var to = Clip(model, clipName);
                if (from != null && to != null) pairs.Add(new KeyValuePair<AnimationClip, AnimationClip>(from, to));
            }
            ov.ApplyOverrides(pairs);
            AssetDatabase.CreateAsset(ov, path);
            return ov;
        }

        // ------------------------------------------------------------------ Hint lines

        /// <summary>The HintBook asset; made with the first drafts (Docs/Hints.md) if it doesn't exist yet.</summary>
        private static HintBook LoadOrCreateHintBook()
        {
            var book = AssetDatabase.LoadAssetAtPath<HintBook>(HintBookPath);
            if (book != null) return book;
            EnsureFolder(DataDir);
            book = ScriptableObject.CreateInstance<HintBook>();
            book.showText = false; // team call (Nov 17): "..." in the bubble until the voice-overs exist
            foreach (var (card, clue, help) in Drafts)
                book.entries.Add(new HintBook.Entry { card = card, clue = clue, helpingHand = help });
            AssetDatabase.CreateAsset(book, HintBookPath);
            return book;
        }

        private static readonly (string card, string clue, string help)[] Drafts =
        {
            ("Firefighter/Tool", "I spray water on fires with this!", "That's my hose! Put it with me, the firefighter."),
            ("Firefighter/Workplace", "My big red fire truck waits here!", "That's my fire station! Put it with the firefighter."),
            ("Doctor/Tool", "I listen to your heartbeat with this!", "That's my stethoscope! Put it with me, the doctor."),
            ("Doctor/Workplace", "I help sick people feel better here!", "That's my hospital! Put it with the doctor."),
            ("Librarian/Tool", "I help kids find stories like this!", "That's my book! Put it with me, the librarian."),
            ("Librarian/Workplace", "I work where the shelves are full of books!", "That's my library! Put it with the librarian."),
            ("PoliceOfficer/Tool", "I wear this to show I keep you safe!", "That's my badge! Put it with me, the police officer."),
            ("PoliceOfficer/Workplace", "I go here to start my day keeping people safe!", "That's my police station! Put it with the police officer."),
            ("Teacher/Tool", "I write lessons on this for my class!", "That's my whiteboard! Put it with me, the teacher."),
            ("Teacher/Workplace", "Kids come here to learn with me!", "That's my school! Put it with the teacher."),
            ("MailCarrier/Tool", "I carry letters to your house in this!", "That's my mailbag! Put it with me, the mail carrier."),
            ("MailCarrier/Workplace", "All the letters get sorted here!", "That's my post office! Put it with the mail carrier."),
        };
    }
}
