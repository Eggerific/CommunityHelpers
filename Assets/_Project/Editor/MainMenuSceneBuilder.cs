using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using static CommunityHelpers.EditorTools.BuildKit;

namespace CommunityHelpers.EditorTools
{
    /// <summary>
    /// Builds Assets/Scenes/MainMenu.unity from scratch. Safe to re-run: it overwrites the scene,
    /// so put hand-made changes in prefabs or tell Claude to add them here.
    /// </summary>
    public static class MainMenuSceneBuilder
    {
        public const string ScenePath = "Assets/Scenes/MainMenu.unity";

        // Meta Interaction SDK rig: OVR camera rig + hands + controllers with ray and poke.
        private const string RigPrefabGuid = "0a7d2469f24041c4284c66706f84c45e";       // OVRComprehensiveInteractionRig
        private const string CameraRigPrefabGuid = "126d619cf4daa52469682f85c1378b4a"; // Meta XR Core OVRCameraRig

        // Anything with these words in its name is turned off: seated game, no moving around.
        private static readonly string[] LocomotionKeywords =
            { "Locomot", "Teleport", "Turner", "ControllerSlide", "ControllerStep", "Tunneling" };

        // ---- Layout (meters, relative to the seat: x right, y up from floor, z forward) ----
        internal static readonly Vector2 TableSize = new(1.8f, 1.1f); // width (x) by depth (z), square corners
        internal const float TableCenterZ = 0.80f;   // near edge 0.25 m in front of the player
        internal const float BackWallZ = 3.0f;
        internal const float SideWallX = 2.8f;
        internal const float BehindWallZ = -2.2f;
        internal const float WallHeight = 3.0f;
        internal const float WainscotHeight = 0.95f;
        internal static readonly Vector2 WindowX = new(-2.3f, 2.3f);
        internal static readonly Vector2 WindowY = new(0.75f, 2.35f);
        private const float SillLift = 0.02f; // sill top sits just above the wall's opening edge (avoids z-fighting)

        public const string GameScenePath = "Assets/Scenes/Game.unity";

        [MenuItem("Community Helpers/Build All Scenes", priority = 0)]
        public static void BuildAll()
        {
            if (!Prepare("Rebuild all scenes", $"This replaces {ScenePath} and {GameScenePath} with fresh builds.")) return;
            BuildMenuScene();
            BuildGameScene();
            EditorSceneManager.OpenScene(ScenePath);
        }

        [MenuItem("Community Helpers/Build Main Menu Scene", priority = 1)]
        public static void Build()
        {
            if (!Prepare("Rebuild Main Menu", $"This replaces {ScenePath} with a fresh build.")) return;
            BuildMenuScene();
        }

        [MenuItem("Community Helpers/Build Game Scene (placeholder)", priority = 2)]
        public static void BuildGame()
        {
            if (!Prepare("Rebuild Game scene", $"This replaces {GameScenePath} with a fresh build.")) return;
            BuildGameScene();
        }

        /// <summary>Checks, confirmation and one-time setup shared by every build.</summary>
        private static bool Prepare(string title, string message)
        {
            if (!FontSetup.EnsureTmpEssentials()) return false;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return false;
            if (!EditorUtility.DisplayDialog(title, message + " Continue?", "Rebuild", "Cancel")) return false;
            FontSetup.CreateAll(force: false);
            KenneyImportSettings.EnsureApplied();
            return true;
        }

        private static void BuildMenuScene()
        {
            var scene = NewSceneWithWorld(out var rig, out var world, out var table, out var robo, out var titleSign);

            var menu = MenuBoardBuilder.Build(table, TableCenterZ);
            var mso = new SerializedObject(menu);
            mso.FindProperty("robo").objectReferenceValue = robo;
            mso.FindProperty("titleSign").objectReferenceValue = titleSign;
            mso.ApplyModifiedPropertiesWithoutUndo();

            // Confetti belongs to the game starting, not the menu.
            var tso = new SerializedObject(titleSign);
            tso.FindProperty("confettiAtStart").floatValue = -1f;
            tso.ApplyModifiedPropertiesWithoutUndo();

            Save(scene, ScenePath, rig);
        }

        private static void BuildGameScene()
        {
            var scene = NewSceneWithWorld(out var rig, out var world, out var table, out var robo, out _);
            GameBoardBuilder.Build(table, TableCenterZ, robo);
            Save(scene, GameScenePath, rig); // the title sign pops confetti when the game loads
        }

        /// <summary>
        /// Everything both scenes share: lighting, the XR rig (with screen fader), the room, Helper Town, props,
        /// table, Robo and the title sign, all under a World that SeatAnchor places around the player.
        /// </summary>
        private static UnityEngine.SceneManagement.Scene NewSceneWithWorld(out GameObject rig, out Transform world,
            out Transform table, out RoboController robo, out TitleSignAnimator titleSign)
        {
            ClearCache();
            KenneyKit.ClearCache();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            SetupLighting();
            rig = SpawnRig(out var head, out var trackingSpace);
            AddScreenFader(head);

            world = new GameObject("World").transform;
            var anchor = world.gameObject.AddComponent<SeatAnchor>();
            BuildRoom(world);
            BuildHelperTown(world);
            BuildClassroomProps(world);
            table = BuildTable(world);
            robo = BuildRobo(table);
            titleSign = TitleSignBuilder.Build(world);

            var so = new SerializedObject(anchor);
            so.FindProperty("head").objectReferenceValue = head;
            so.FindProperty("trackingSpace").objectReferenceValue = trackingSpace;
            so.FindProperty("table").objectReferenceValue = table;
            so.ApplyModifiedPropertiesWithoutUndo();
            return scene;
        }

        private static void Save(UnityEngine.SceneManagement.Scene scene, string path, GameObject rig)
        {
            EditorSceneManager.SaveScene(scene, path);
            UpdateBuildSettings();
            AssetDatabase.SaveAssets();
            Selection.activeObject = rig;
            Debug.Log("[MainMenuSceneBuilder] Built " + path);
        }

        /// <summary>Soft cream fade quad just in front of the headset camera (see ScreenFader).</summary>
        private static void AddScreenFader(Transform head)
        {
            if (head == null) return;
            EnsureFolder(MaterialDir);
            string path = $"{MaterialDir}/M_ScreenFade.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Sprites/Default"));
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.color = UIColors.Cream;
            mat.renderQueue = 4000; // overlay: drawn after everything else
            EditorUtility.SetDirty(mat);

            var quad = Prim(PrimitiveType.Quad, "ScreenFader", head, new Vector3(0, 0, 0.1f), new Vector3(1.2f, 1.2f, 1f), mat);
            var fader = quad.AddComponent<ScreenFader>();
            var so = new SerializedObject(fader);
            so.FindProperty("quad").objectReferenceValue = quad.GetComponent<Renderer>();
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ------------------------------------------------------------------ Rig

        private static GameObject SpawnRig(out Transform head, out Transform trackingSpace)
        {
            head = null;
            trackingSpace = null;

            // 1) The core camera rig (headset camera, controller and hand anchors, OVRManager).
            var cameraRigPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(CameraRigPrefabGuid));
            var interactionPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(RigPrefabGuid));
            if (cameraRigPrefab == null || interactionPrefab == null)
            {
                Debug.LogError("[MainMenuSceneBuilder] Could not find OVRCameraRig or OVRComprehensiveInteractionRig. Are the Meta XR Core and Interaction SDKs installed?");
                return null;
            }

            var cameraRigGo = (GameObject)PrefabUtility.InstantiatePrefab(cameraRigPrefab);
            cameraRigGo.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            var cameraRig = cameraRigGo.GetComponent<OVRCameraRig>();

            // 2) The Interaction SDK rig goes under the camera rig (same as Meta's setup wizard) and points at it.
            var rig = (GameObject)PrefabUtility.InstantiatePrefab(interactionPrefab, cameraRigGo.transform);
            rig.transform.localPosition = Vector3.zero;
            rig.transform.localRotation = Quaternion.identity;
            foreach (var rigRef in rig.GetComponentsInChildren<Oculus.Interaction.Input.OVRCameraRigRef>(true))
            {
                var rso = new SerializedObject(rigRef);
                rso.FindProperty("_ovrCameraRig").objectReferenceValue = cameraRig;
                rso.ApplyModifiedPropertiesWithoutUndo();
            }
            // The interaction rig draws its own hands, so hide the camera rig's built-in ones.
            foreach (var hand in cameraRigGo.GetComponentsInChildren<OVRHand>(true))
            {
                if (hand.transform.IsChildOf(rig.transform)) continue;
                if (hand.TryGetComponent<OVRSkeletonRenderer>(out var sr)) sr.enabled = false;
                if (hand.TryGetComponent<OVRMesh>(out var m)) m.enabled = false;
                if (hand.TryGetComponent<OVRMeshRenderer>(out var mr)) mr.enabled = false;
                if (hand.TryGetComponent<SkinnedMeshRenderer>(out var smr)) smr.enabled = false;
                foreach (var c in hand.GetComponents<Component>())
                    PrefabUtility.RecordPrefabInstancePropertyModifications(c);
            }

            // Seated, no locomotion.
            var disabled = new List<string>();
            foreach (var t in rig.GetComponentsInChildren<Transform>(true))
            {
                if (t == rig.transform || !t.gameObject.activeSelf) continue;
                if (LocomotionKeywords.Any(k => t.name.Contains(k)))
                {
                    t.gameObject.SetActive(false);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(t.gameObject);
                    disabled.Add(t.name);
                }
            }
            Debug.Log("[MainMenuSceneBuilder] Turned off locomotion objects: " +
                      (disabled.Count > 0 ? string.Join(", ", disabled) : "(none found)"));

            // Floor-level tracking so the room's floor matches the real floor.
            var manager = cameraRigGo.GetComponent<OVRManager>();
            if (manager == null) manager = cameraRigGo.AddComponent<OVRManager>();
            var mso = new SerializedObject(manager);
            var prop = mso.FindProperty("_trackingOriginType");
            if (prop != null) prop.intValue = (int)OVRManager.TrackingOrigin.FloorLevel;
            // Hands follow the controllers when they're held (what Meta's wizard sets).
            var handPoses = mso.FindProperty("controllerDrivenHandPosesType");
            if (handPoses != null) handPoses.intValue = (int)OVRManager.ControllerDrivenHandPosesType.ConformingToController;
            mso.ApplyModifiedPropertiesWithoutUndo();

            trackingSpace = cameraRigGo.transform.Find("TrackingSpace");
            head = trackingSpace != null ? trackingSpace.Find("CenterEyeAnchor") : null;
            if (head == null) Debug.LogError("[MainMenuSceneBuilder] CenterEyeAnchor not found under OVRCameraRig.");

            if (head != null && head.TryGetComponent<Camera>(out var cam))
            {
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = UIColors.SkyBlue;
                cam.nearClipPlane = 0.03f;
                cam.farClipPlane = 120f; // skyline sits about 45 m out, sky backdrop at 73 m
                PrefabUtility.RecordPrefabInstancePropertyModifications(cam);
            }

            return cameraRigGo;
        }

        // ------------------------------------------------------------------ Lighting

        private static void SetupLighting()
        {
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color32(0xF2, 0xEE, 0xE6, 255);
            RenderSettings.ambientEquatorColor = new Color32(0xC9, 0xC4, 0xBA, 255);
            RenderSettings.ambientGroundColor = new Color32(0x8C, 0x80, 0x72, 255);
            RenderSettings.fog = false;

            var lightGo = new GameObject("Sun Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color32(0xFF, 0xF1, 0xDC, 255);
            light.intensity = 0.9f;
            light.shadows = LightShadows.None;
            lightGo.transform.rotation = Quaternion.Euler(50f, -25f, 0f);
        }

        // ------------------------------------------------------------------ Room

        private static void BuildRoom(Transform world)
        {
            var room = Group("Room", world);
            var floor = Mat("FloorWood", UIColors.FloorWood);
            var upper = Mat("WallUpper", UIColors.WallUpper);
            var lower = Mat("WallLower", UIColors.WallLower);
            var trim = Mat("WoodDark", UIColors.WoodDark);

            const float t = 0.1f; // wall thickness

            Box("Floor", room, new Vector3(0, -0.05f, (BackWallZ + BehindWallZ) * 0.5f),
                new Vector3(SideWallX * 2, 0.1f, BackWallZ - BehindWallZ), floor);
            // Ceiling (the title sign and bunting hang from it).
            Box("Ceiling", room, new Vector3(0, WallHeight + 0.05f, (BackWallZ + BehindWallZ) * 0.5f),
                new Vector3(SideWallX * 2 + 0.2f, 0.1f, BackWallZ - BehindWallZ + 0.2f), Mat("Ceiling", new Color32(0xFB, 0xF5, 0xEA, 255)));

            // Side and back (behind the player) walls: lower teal band, cream above, wood trim between.
            WallStrip("Wall_Left", room, new Vector3(-SideWallX, 0, (BackWallZ + BehindWallZ) * 0.5f),
                new Vector3(t, 0, BackWallZ - BehindWallZ), lower, upper, trim);
            WallStrip("Wall_Right", room, new Vector3(SideWallX, 0, (BackWallZ + BehindWallZ) * 0.5f),
                new Vector3(t, 0, BackWallZ - BehindWallZ), lower, upper, trim);
            WallStrip("Wall_Behind", room, new Vector3(0, 0, BehindWallZ),
                new Vector3(SideWallX * 2, 0, t), lower, upper, trim);

            // Front wall with the big window onto Helper Town.
            var front = Group("Wall_Front", room);
            float z0 = BackWallZ - t * 0.5f, z1 = BackWallZ + t * 0.5f;
            float wl = WindowX.x, wr = WindowX.y, wb = WindowY.x, wt = WindowY.y;
            // below window
            BoxMinMax("Lower_L", front, new Vector3(-SideWallX, 0, z0), new Vector3(wl, WainscotHeight, z1), lower);
            BoxMinMax("Lower_R", front, new Vector3(wr, 0, z0), new Vector3(SideWallX, WainscotHeight, z1), lower);
            BoxMinMax("Lower_Mid", front, new Vector3(wl, 0, z0), new Vector3(wr, wb, z1), lower);
            // sides and top
            BoxMinMax("Upper_L", front, new Vector3(-SideWallX, WainscotHeight, z0), new Vector3(wl, WallHeight, z1), upper);
            BoxMinMax("Upper_R", front, new Vector3(wr, WainscotHeight, z0), new Vector3(SideWallX, WallHeight, z1), upper);
            BoxMinMax("Upper_Top", front, new Vector3(wl, wt, z0), new Vector3(wr, WallHeight, z1), upper);
            BoxMinMax("Trim_L", front, new Vector3(-SideWallX, WainscotHeight - 0.022f, z0 - 0.02f), new Vector3(wl, WainscotHeight + 0.022f, z0), trim);
            BoxMinMax("Trim_R", front, new Vector3(wr, WainscotHeight - 0.022f, z0 - 0.02f), new Vector3(SideWallX, WainscotHeight + 0.022f, z0), trim);

            // Window frame: chunky wood, one center post, a deep sill.
            var frame = Group("WindowFrame", front);
            const float f = 0.08f;
            // The frame straddles the hole's edges (half on the wall, half in the opening) and each piece has
            // its own front/back depth, so no frame face shares a plane with a wall face or another piece.
            // Shared planes are what made the frame flicker.
            BoxMinMax("Frame_Top", frame, new Vector3(wl - f, wt - f * 0.5f, z0 - 0.05f), new Vector3(wr + f, wt + f, z1 + 0.03f), trim);
            BoxMinMax("Frame_L", frame, new Vector3(wl - f, wb, z0 - 0.04f), new Vector3(wl + f * 0.5f, wt, z1 + 0.02f), trim);
            BoxMinMax("Frame_R", frame, new Vector3(wr - f * 0.5f, wb, z0 - 0.04f), new Vector3(wr + f, wt, z1 + 0.02f), trim);
            BoxMinMax("Frame_Mid", frame, new Vector3(-0.03f, wb, z0 - 0.025f), new Vector3(0.03f, wt, z1 + 0.01f), trim);
            BoxMinMax("Sill", frame, new Vector3(wl - f - 0.05f, wb - 0.05f, z0 - 0.18f), new Vector3(wr + f + 0.05f, wb + SillLift, z1 + 0.04f), trim);
        }

        private static void WallStrip(string name, Transform parent, Vector3 basePos, Vector3 footprint,
            Material lower, Material upper, Material trim)
        {
            var g = Group(name, parent);
            Box("Lower", g, basePos + Vector3.up * WainscotHeight * 0.5f,
                new Vector3(footprint.x, WainscotHeight, footprint.z), lower);
            Box("Upper", g, basePos + Vector3.up * (WainscotHeight + (WallHeight - WainscotHeight) * 0.5f),
                new Vector3(footprint.x, WallHeight - WainscotHeight, footprint.z), upper);
            Box("Trim", g, basePos + Vector3.up * WainscotHeight,
                new Vector3(footprint.x + 0.03f, 0.04f, footprint.z + 0.03f), trim);
        }

        // ------------------------------------------------------------------ Helper Town (seen through the window)

        private struct HelperPlace
        {
            public string Label, Building, Vehicle;
            public Color32 Color;
            public HelperPlace(string label, Color32 color, string building, string vehicle)
            { Label = label; Color = color; Building = building; Vehicle = vehicle; }
        }

        // Street outside the window (meters from the seat, z forward).
        // Street is 3.2 m wide: a driving lane (traffic, right to left) and a parking lane (helper vehicles).
        private const float SidewalkNearZ = 8.1f, StreetNearZ = 8.9f, StreetFarZ = 12.1f, BuildingFrontZ = 13.4f;
        private const float DrivingLaneZ = 9.7f, ParkingLaneZ = 11.3f;

        private static void BuildHelperTown(Transform world)
        {
            var town = Group("HelperTown", world);
            var signFont = FontSetup.Load(FontSetup.FredokaBold);

            // One scale per kit, from a reference model. Toy-town scale: buildings and vehicles match each other.
            KenneyKit.SetKitScale(KenneyKit.Commercial, "building-a", 0, 2.6f);   // small shop 2.6 m wide
            KenneyKit.SetKitScale(KenneyKit.Car, "firetruck", 2, 3.4f);           // fire truck 3.4 m long
            KenneyKit.SetKitScale(KenneyKit.Suburban, "building-type-a", 0, 5.5f);
            KenneyKit.SetKitScale(KenneyKit.Nature, "tree_oak", 1, 4.2f);

            // Ground, street, sidewalks, crosswalk, sky.
            Box("Ground", town, new Vector3(0, -0.03f, BackWallZ + 35f), new Vector3(160f, 0.04f, 70f), Mat("Grass", UIColors.Grass));
            var sidewalk = Mat("Sidewalk", new Color32(0xD8, 0xD3, 0xC8, 255));
            var asphalt = Mat("Asphalt", new Color32(0x5E, 0x63, 0x6E, 255));
            var paint = Mat("RoadPaint", new Color32(0xF7, 0xF1, 0xDE, 255));
            BoxMinMax("Sidewalk_Near", town, new Vector3(-40, 0, SidewalkNearZ), new Vector3(40, 0.06f, StreetNearZ), sidewalk);
            BoxMinMax("Street", town, new Vector3(-40, 0, StreetNearZ), new Vector3(40, 0.02f, StreetFarZ), asphalt);
            BoxMinMax("Sidewalk_Far", town, new Vector3(-40, 0, StreetFarZ), new Vector3(40, 0.06f, BuildingFrontZ + 1.5f), sidewalk);
            float midZ = (StreetNearZ + StreetFarZ) * 0.5f;
            for (float x = -38f; x <= 38f; x += 2.5f)
                if (Mathf.Abs(x) > 1.6f)
                    Box("LaneDash", town, new Vector3(x, 0.025f, midZ), new Vector3(1.1f, 0.01f, 0.1f), paint);
            for (int i = 0; i < 5; i++)
                Box("Crosswalk", town, new Vector3(-1.2f + i * 0.6f, 0.025f, midZ), new Vector3(0.35f, 0.01f, StreetFarZ - StreetNearZ - 0.2f), paint);
            Prim(PrimitiveType.Quad, "SkyBackdrop", town, new Vector3(0, 18f, BackWallZ + 70f), new Vector3(240f, 60f, 1f),
                Unlit("Sky", UIColors.SkyBlue));

            // The six helper buildings, left to right, each with its color, a rooftop sign and its vehicle.
            var places = new[]
            {
                new HelperPlace("FIRE STATION", UIColors.Coral,    "building-c", "firetruck"),
                new HelperPlace("HOSPITAL",     UIColors.Teal,     "building-i", "ambulance"),
                new HelperPlace("LIBRARY",      UIColors.Violet,   "building-h", null),
                new HelperPlace("POLICE",       UIColors.Sky,      "building-a", "police"),
                new HelperPlace("SCHOOL",       UIColors.Sun,      "building-e", null),
                new HelperPlace("POST OFFICE",  UIColors.TryAgain, "building-f", "delivery"),
            };

            const float gap = 1.2f;
            var sizes = places.Select(p => KenneyKit.Measure(KenneyKit.Commercial, p.Building) * KenneyKit.KitScale(KenneyKit.Commercial)).ToArray();
            float total = sizes.Sum(s => s.x) + gap * (places.Length - 1);
            float x0 = -total * 0.5f;

            var signPost = Mat("SignPost", UIColors.Ink);
            for (int i = 0; i < places.Length; i++)
            {
                var p = places[i];
                float cx = x0 + sizes[i].x * 0.5f;
                x0 += sizes[i].x + gap;

                // yaw 180: building front faces the classroom window.
                var b = KenneyKit.Place(KenneyKit.Commercial, p.Building, town,
                    new Vector3(cx, 0.06f, BuildingFrontZ + sizes[i].z * 0.5f), 180f, out var size, name: $"Building_{p.Label}");
                KenneyKit.Tint(b, p.Color, 0.55f, p.Label.Replace(' ', '_'));

                // Sign in the helper's color, standing on the top edge of the front wall (container +Z = front).
                // The front wall top is measured from the mesh so rooftop boxes don't lift the sign into the air.
                if (!KenneyKit.FrontTop(b, 0.35f, out float wallTop, out float frontZ))
                {
                    wallTop = size.y;
                    frontZ = size.z * 0.5f;
                }
                float signW = Mathf.Min(size.x * 0.95f, 3.4f);
                // Bottom of the board sinks 4 cm behind the front wall's top edge so it reads as attached.
                var sign = Group("RoofSign", b, new Vector3(0, wallTop - 0.04f, frontZ - 0.12f));
                Box("Base", sign, new Vector3(0, 0.03f, 0), new Vector3(signW + 0.06f, 0.06f, 0.14f), signPost);
                Box("Board", sign, new Vector3(0, 0.41f, 0), new Vector3(signW, 0.7f, 0.08f), Mat("Sign_" + p.Label.Replace(' ', '_'), p.Color));
                // Dark text on light signs (yellow, orange), cream text on the darker ones.
                Color c = p.Color;
                bool lightSign = 0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b > 0.6f;
                var txt = Text3D("Label", sign, p.Label, signFont, 0.42f, lightSign ? UIColors.Ink : UIColors.Cream,
                    new Vector3(0, 0.41f, 0.045f), new Vector2(signW - 0.15f, 0.6f), new Vector3(0, 180f, 0));
                txt.enableAutoSizing = true;
                txt.fontSizeMin = 1.5f;
                txt.fontSizeMax = 4.2f;

                // The helper's vehicle parked along the far curb, in front of its building.
                if (p.Vehicle != null)
                    KenneyKit.Place(KenneyKit.Car, p.Vehicle, town, new Vector3(cx, 0.02f, ParkingLaneZ), 90f + (i % 2) * 180f);

                // Bushes either side of the entrance.
                KenneyKit.Place(KenneyKit.Nature, "plant_bushLarge", town, new Vector3(cx - size.x * 0.5f + 0.3f, 0.06f, BuildingFrontZ - 0.35f), 0f, 0.8f);
                KenneyKit.Place(KenneyKit.Nature, "plant_bushLarge", town, new Vector3(cx + size.x * 0.5f - 0.3f, 0.06f, BuildingFrontZ - 0.35f), 40f, 0.8f);
            }

            // Trees: one behind each gap, plus some along the near sidewalk at the edges of the view.
            string[] treeModels = { "tree_oak", "tree_default", "tree_detailed", "tree_fat" };
            float gx = -total * 0.5f;
            for (int i = 0; i < places.Length - 1; i++)
            {
                gx += sizes[i].x + gap;
                KenneyKit.Place(KenneyKit.Nature, treeModels[i % treeModels.Length], town,
                    new Vector3(gx - gap * 0.5f, 0.06f, BuildingFrontZ + 2.5f), i * 47f);
            }
            float[] edgeX = { -total * 0.5f - 3f, -total * 0.5f - 6f, total * 0.5f + 3f, total * 0.5f + 6.5f };
            for (int i = 0; i < edgeX.Length; i++)
                KenneyKit.Place(KenneyKit.Nature, treeModels[(i + 1) % treeModels.Length], town,
                    new Vector3(edgeX[i], 0.06f, BuildingFrontZ + 0.5f + (i % 2) * 1.5f), i * 80f, 1.1f);

            // Flower beds along the near sidewalk.
            string[] flowers = { "flower_redA", "flower_yellowA", "flower_purpleA" };
            for (int i = 0; i < 14; i++)
            {
                float fx = -9f + i * 1.4f;
                if (Mathf.Abs(fx) < 1.7f) continue; // keep the crosswalk clear
                KenneyKit.Place(KenneyKit.Nature, flowers[i % flowers.Length], town, new Vector3(fx, 0f, SidewalkNearZ - 0.3f), i * 33f, 0.45f);
            }

            // Back row: suburban houses peeking over the helper buildings.
            string[] houses = { "building-type-a", "building-type-c", "building-type-e", "building-type-h",
                                "building-type-k", "building-type-n", "building-type-q", "building-type-t" };
            int h = 0;
            for (float x = -27f; x <= 27f; x += 7.5f, h++)
                KenneyKit.Place(KenneyKit.Suburban, houses[h % houses.Length], town, new Vector3(x, 0f, BuildingFrontZ + 11f + (h % 2) * 2f), 180f);

            // Far skyline.
            string[] towers = { "building-skyscraper-a", "building-skyscraper-b", "building-skyscraper-c",
                                "building-skyscraper-d", "building-skyscraper-e" };
            int t = 0;
            for (float x = -36f; x <= 36f; x += 9f, t++)
                KenneyKit.Place(KenneyKit.Commercial, towers[t % towers.Length], town, new Vector3(x + (t % 2) * 2f, 0f, BuildingFrontZ + 30f + (t % 3) * 4f), 180f, 1.4f);

            BuildTraffic(town);
            BuildPlane(town);
        }

        // ------------------------------------------------------------------ Moving life outside the window

        /// <summary>A small pool of cars that StreetTraffic sends along the driving lane, right to left.</summary>
        private static void BuildTraffic(Transform town)
        {
            var traffic = Group("Traffic", town, new Vector3(0, 0.02f, DrivingLaneZ));
            string[] models = { "sedan", "taxi", "suv", "van", "hatchback-sports", "truck" };
            var cars = new List<Transform>();
            foreach (var m in models)
            {
                // yaw 90: checked in the headset, this points the car's front to -X (the direction it drives).
                var car = KenneyKit.Place(KenneyKit.Car, m, traffic, Vector3.zero, 90f, name: "Car_" + m);
                car.gameObject.SetActive(false);
                cars.Add(car);
            }
            var tr = traffic.gameObject.AddComponent<StreetTraffic>();
            var so = new SerializedObject(tr);
            var arr = so.FindProperty("cars");
            arr.arraySize = cars.Count;
            for (int i = 0; i < cars.Count; i++) arr.GetArrayElementAtIndex(i).objectReferenceValue = cars[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>A cute propeller plane that PlaneFlyby flies back and forth across the sky behind the town.</summary>
        private static void BuildPlane(Transform town)
        {
            // Between the back-row houses (z ~25-31, under 6 m tall) and the skyline (front ~41 m), above everything.
            // The loop's far pass is 6 m farther (z ~35), still well in front of the towers.
            var sky = Group("SkyLife", town, new Vector3(0, 10f, BuildingFrontZ + 16f));
            var plane = Group("Plane", sky);
            plane.localScale = Vector3.one * 1.4f;

            var white = Mat("PlaneBody", new Color32(0xFA, 0xFA, 0xF5, 255), 0.4f);
            var coral = Mat("Coral", UIColors.Coral);
            var sun = Mat("Sun", UIColors.Sun, 0.4f);
            var glass = Mat("PlaneWindow", UIColors.Sky, 0.7f);

            // Nose points +X. Wings span Z. PlaneFlyby turns the plane to face its direction of travel.
            Prim(PrimitiveType.Capsule, "Fuselage", plane, Vector3.zero, new Vector3(0.7f, 1.6f, 0.7f), white, new Vector3(0, 0, 90));
            Box("Wing", plane, new Vector3(0.2f, -0.08f, 0), new Vector3(0.9f, 0.08f, 3.8f), coral);
            Box("TailFin", plane, new Vector3(-1.35f, 0.45f, 0), new Vector3(0.6f, 0.75f, 0.07f), coral);
            Box("TailWing", plane, new Vector3(-1.4f, 0.12f, 0), new Vector3(0.5f, 0.06f, 1.5f), coral);
            for (int i = 0; i < 3; i++)
            {
                // Windows on both sides: the player sees one side going right and the other coming back.
                Sphere("Window", plane, new Vector3(0.75f - i * 0.42f, 0.1f, -0.31f), new Vector3(0.24f, 0.24f, 0.08f), glass);
                Sphere("Window", plane, new Vector3(0.75f - i * 0.42f, 0.1f, 0.31f), new Vector3(0.24f, 0.24f, 0.08f), glass);
            }
            Sphere("Hub", plane, new Vector3(1.62f, 0, 0), Vector3.one * 0.26f, sun);
            var prop = Group("Propeller", plane, new Vector3(1.68f, 0, 0));
            Box("BladeA", prop, Vector3.zero, new Vector3(0.05f, 1.3f, 0.12f), Mat("Ink", UIColors.Ink));
            Box("BladeB", prop, Vector3.zero, new Vector3(0.05f, 0.12f, 1.3f), Mat("Ink", UIColors.Ink));

            var fly = sky.gameObject.AddComponent<PlaneFlyby>();
            var so = new SerializedObject(fly);
            so.FindProperty("plane").objectReferenceValue = plane;
            so.FindProperty("propeller").objectReferenceValue = prop;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ------------------------------------------------------------------ Classroom props

        private static void BuildClassroomProps(Transform world)
        {
            var props = Group("ClassroomProps", world);
            const string F = KenneyKit.Furniture;
            KenneyKit.SetKitScale(F, "desk", 1, 0.72f); // a desk is 72 cm tall

            // Container +Z is each prop's front. Yaw 90 faces +X (into the room from the left wall), -90 from the right wall.
            float leftWall = -SideWallX + 0.05f, rightWall = SideWallX - 0.05f;

            // Left wall: two tall bookcases with books on top, a toy box and a floor lamp.
            for (int i = 0; i < 2; i++)
            {
                var bc = KenneyKit.Place(F, "bookcaseOpen", props, Vector3.zero, 90f, out var bcSize);
                bc.localPosition = new Vector3(leftWall + bcSize.z * 0.5f, 0, 0.1f + i * (bcSize.x + 0.1f));
                KenneyKit.Place(F, "books", props, bc.localPosition + new Vector3(0, bcSize.y, 0), 90f + i * 25f);
            }
            KenneyKit.Place(F, "cardboardBoxOpen", props, new Vector3(leftWall + 0.35f, 0, 2.2f), 70f);
            KenneyKit.Place(F, "lampRoundFloor", props, new Vector3(leftWall + 0.3f, 0, -1.2f), 90f);
            KenneyKit.Place(F, "coatRackStanding", props, new Vector3(leftWall + 0.35f, 0, -1.85f), 0f);

            // Right wall: low bookcase with books and a plant, then the teacher's desk by the window.
            // (The Kenney teddy bear read as a lion in the headset, so it was removed.)
            var low = KenneyKit.Place(F, "bookcaseOpenLow", props, Vector3.zero, -90f, out var lowSize);
            low.localPosition = new Vector3(rightWall - lowSize.z * 0.5f, 0, 0.4f);
            KenneyKit.Place(F, "books", props, low.localPosition + new Vector3(0, lowSize.y, -0.3f), -80f);
            KenneyKit.Place(F, "pottedPlant", props, new Vector3(rightWall - 0.3f, 0, -0.6f), 0f);
            KenneyKit.Place(F, "trashcan", props, new Vector3(rightWall - 0.25f, 0, 1.4f), 0f);

            var desk = KenneyKit.Place(F, "desk", props, Vector3.zero, 180f, out var deskSize);
            desk.localPosition = new Vector3(rightWall - deskSize.x * 0.5f - 0.05f, 0, BackWallZ - 0.1f - deskSize.z * 0.5f - 0.45f);
            KenneyKit.Place(F, "chairDesk", props, desk.localPosition + new Vector3(0, 0, deskSize.z * 0.5f + 0.3f), 180f);
            KenneyKit.Place(F, "books", props, desk.localPosition + new Vector3(-0.3f, deskSize.y, 0), 160f);
            KenneyKit.Place(F, "lampSquareTable", props, desk.localPosition + new Vector3(0.4f, deskSize.y, 0.05f), 200f);

            // Corner plant and window-sill plants.
            KenneyKit.Place(F, "pottedPlant", props, new Vector3(leftWall + 0.35f, 0, BackWallZ - 0.4f), 30f, 1.2f);
            float sillTop = WindowY.x + SillLift, sillZ = BackWallZ - 0.12f;
            string[] small = { "plantSmall1", "plantSmall2", "plantSmall3", "plantSmall1" };
            float[] sillX = { -1.9f, -1.2f, 1.25f, 1.95f };
            for (int i = 0; i < sillX.Length; i++)
                KenneyKit.Place(F, small[i], props, new Vector3(sillX[i], sillTop, sillZ), i * 60f);

            // Rug under the table and two empty kid chairs at the table ends.
            KenneyKit.Place(F, "rugRectangle", props, new Vector3(0, 0.002f, TableCenterZ), 0f, 1.05f);
            KenneyKit.Place(F, "chairRounded", props, new Vector3(-TableSize.x * 0.5f - 0.3f, 0, TableCenterZ), 90f);
            KenneyKit.Place(F, "chairRounded", props, new Vector3(TableSize.x * 0.5f + 0.3f, 0, TableCenterZ), -90f);
        }

        // ------------------------------------------------------------------ Table

        private static Transform BuildTable(Transform world)
        {
            // The Table transform sits at the table-top surface; SeatAnchor sets its height from the player's eyes.
            var table = Group("Table", world, new Vector3(0, 0.65f, TableCenterZ));
            var wood = Mat("TableWood", UIColors.TableWood);
            var dark = Mat("WoodDark", UIColors.WoodDark);
            float w = TableSize.x, d = TableSize.y;
            Box("Top", table, new Vector3(0, -0.025f, 0), new Vector3(w, 0.05f, d), wood);
            Box("Apron", table, new Vector3(0, -0.08f, 0), new Vector3(w - 0.1f, 0.06f, d - 0.1f), dark);
            // Four legs, inset from the corners. They extend below the floor on low tables, which is hidden.
            const float inset = 0.09f, legLen = 1.1f;
            for (int sx = -1; sx <= 1; sx += 2)
            for (int sz = -1; sz <= 1; sz += 2)
                Box($"Leg_{(sx < 0 ? "L" : "R")}{(sz < 0 ? "Front" : "Back")}", table,
                    new Vector3(sx * (w * 0.5f - inset), -0.05f - legLen * 0.5f, sz * (d * 0.5f - inset)),
                    new Vector3(0.06f, legLen, 0.06f), dark);
            return table;
        }

        // ------------------------------------------------------------------ Step 1 placeholders

        /// <summary>
        /// Robo stands right of the menu board (the board is off-center to the left), closer to the player than
        /// the board, turned to face the seat. Built from parts by RoboBuilder.
        /// </summary>
        private static RoboController BuildRobo(Transform table)
        {
            var roboPos = new Vector3(0.4f, 0, -0.28f);
            return RoboBuilder.Build(table, roboPos, MenuBoardBuilder.YawTowardSeat(roboPos, TableCenterZ));
        }

        // ------------------------------------------------------------------ Build settings

        /// <summary>MainMenu is scene 0 (the app starts there), Game is scene 1, then anything else already listed.</summary>
        private static void UpdateBuildSettings()
        {
            var ordered = new[] { ScenePath, GameScenePath }.Where(System.IO.File.Exists).ToList();
            var scenes = ordered.Select(p => new EditorBuildSettingsScene(p, true)).ToList();
            scenes.AddRange(EditorBuildSettings.scenes.Where(s => !ordered.Contains(s.path)));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
