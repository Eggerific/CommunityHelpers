using Oculus.Interaction;
using Oculus.Interaction.Surfaces;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.Events;
using static CommunityHelpers.EditorTools.BuildKit;

namespace CommunityHelpers.EditorTools
{
    /// <summary>
    /// Step 2: the menu board on the table (a wooden easel) and the Main panel's four 3D buttons.
    /// Board space: x right, y up, +Z points into the board (away from the player), front faces -Z.
    /// All sizes in meters.
    /// </summary>
    public static class MenuBoardBuilder
    {
        // ---- Board placement (table space; table origin is the table-top surface) ----
        // Off-center to the left so the player has a clear view out the window; Robo stands to the right.
        // Height: just enough that the bottom edge clears the table at this tilt.
        private static readonly Vector3 BoardCenter = new(-0.2f, 0.28f, -0.18f);
        private const float BoardTilt = 25f;               // tipped back like a music stand

        // ---- Board size ----
        // The button layout is designed on a 0.62 x 0.42 m panel (DesignPanel) and scaled up to fit PanelSize,
        // so buttons, labels and spacing all grow together and stay inside the frame.
        private static readonly Vector2 DesignPanel = new(0.62f, 0.42f);
        private static readonly Vector2 PanelSize = new(0.75f, 0.508f);
        private const float FrameBorder = 0.025f;          // wood visible around the cream panel
        private static readonly Vector2 FrameSize = PanelSize + Vector2.one * (FrameBorder * 2f);
        // The buttons fill 90% of the panel, which leaves a clear cream border inside the wood frame.
        private const float ContentFill = 0.9f;
        private static float LayoutScale => Mathf.Min(PanelSize.x / DesignPanel.x, PanelSize.y / DesignPanel.y) * ContentFill;
        private const float FrameDepth = 0.025f, PanelDepth = 0.004f;

        // ---- Button construction ----
        private const float BaseDepth = 0.010f, FaceDepth = 0.010f, FaceInset = 0.004f;

        internal struct Helper
        {
            public Color32 Color; public string Kit, Model; public float Yaw;
            public Helper(Color32 c, string kit, string model, float yaw) { Color = c; Kit = kit; Model = model; Yaw = yaw; }
        }

        // Placeholder helper icons: a colored chip with a tiny 3D model. Teammates will replace these with card art.
        internal static readonly Helper Fire     = new(UIColors.Coral,    KenneyKit.Car,       "firetruck",          115f);
        internal static readonly Helper Doctor   = new(UIColors.Teal,     KenneyKit.Car,       "ambulance",          115f);
        internal static readonly Helper Librarian= new(UIColors.Violet,   KenneyKit.Furniture, "books",              200f);
        internal static readonly Helper Police   = new(UIColors.Sky,      KenneyKit.Car,       "police",             115f);
        internal static readonly Helper Teacher  = new(UIColors.Sun,      KenneyKit.Furniture, "desk",               200f);
        internal static readonly Helper Mail     = new(UIColors.TryAgain, KenneyKit.Furniture, "cardboardBoxClosed", 210f);

        /// <summary>Yaw (degrees) that turns something at tablePos to face the player's seat.</summary>
        public static float YawTowardSeat(Vector3 tablePos, float tableCenterZ) =>
            Mathf.Atan2(tablePos.x, tablePos.z + tableCenterZ) * Mathf.Rad2Deg;

        /// <param name="tableCenterZ">How far in front of the seat the table's center is (the seat is at z = -tableCenterZ in table space).</param>
        /// <summary>
        /// The wooden easel on the table: root (pops in at start) -> Board (tilted, turned toward the seat)
        /// with frame, cream panel, chalk ledge and legs. Panels go on the board via <see cref="NewPanel"/>.
        /// </summary>
        internal static Transform BuildEasel(Transform table, float tableCenterZ, string name, out Transform board)
        {
            var wood = Mat("WoodDark", UIColors.WoodDark);
            var root = Group(name, table);
            float yaw = YawTowardSeat(BoardCenter, tableCenterZ);
            board = Group("Board", root, BoardCenter, new Vector3(BoardTilt, yaw, 0));
            MeshKit.Show("Frame", board, MeshKit.RoundedSlab("Board_Frame", FrameSize.x, FrameSize.y, FrameDepth, 0.045f), wood);
            MeshKit.Show("Panel", board, MeshKit.RoundedSlab("Board_Panel", PanelSize.x, PanelSize.y, PanelDepth, 0.03f),
                Mat("Cream", UIColors.Cream), new Vector3(0, 0, -FrameDepth));
            // Chalk-ledge along the bottom edge.
            Box("Ledge", board, new Vector3(0, -FrameSize.y * 0.5f - 0.004f, -0.03f), new Vector3(FrameSize.x - 0.04f, 0.014f, 0.05f), wood);
            BuildEaselLegs(root, board, wood);
            return root;
        }

        /// <summary>A panel on the board's face. Everything inside is laid out in DesignPanel units (0.62 x 0.42).</summary>
        internal static Transform NewPanel(string name, Transform board)
        {
            var panel = Group(name, board, new Vector3(0, 0, -FrameDepth - PanelDepth));
            panel.localScale = Vector3.one * LayoutScale;
            return panel;
        }

        internal static Vector2 Design => DesignPanel;

        public static MainMenuController Build(Transform table, float tableCenterZ)
        {
            var semi = FontSetup.Load(FontSetup.FredokaSemiBold);
            var bold = FontSetup.Load(FontSetup.FredokaBold);

            var root = BuildEasel(table, tableCenterZ, "MenuBoard", out var board);
            var controller = root.gameObject.AddComponent<MainMenuController>();
            var panel = NewPanel("Panel_Main", board);

            // Layout (panel center = 0,0; design panel is 0.62 x 0.42). Top to bottom: Play, Quick play, then How to play | Settings.
            // Vertical: 3.5 cm top margin, Play 11, gap 3, Quick 9, gap 3, small row 8, 4.5 cm bottom margin.
            // Horizontal: 4 cm side margins; the small buttons have a 4 cm gap between them.
            const float wide = 0.54f, gap = 0.03f;
            const float playH = 0.11f, quickH = 0.09f, smallH = 0.08f;
            float playY = DesignPanel.y * 0.5f - 0.035f - playH * 0.5f;
            float quickY = playY - playH * 0.5f - gap - quickH * 0.5f;
            float smallY = quickY - quickH * 0.5f - gap - smallH * 0.5f;
            const float smallW = 0.25f, smallX = smallW * 0.5f + 0.02f;

            var play = MakeButton("Btn_Play", panel, new Vector2(0, playY), new Vector2(wide, playH), UIColors.YayButton, out var playContent);
            Icon("PlayIcon", playContent, new[] { new Vector2(-0.4f, -0.5f), new Vector2(0.6f, 0f), new Vector2(-0.4f, 0.5f) },
                0.04f, UIColors.Cream, new Vector2(-0.085f, 0.022f));
            Label(playContent, "Play", semi, 0.048f, UIColors.Cream, new Vector2(0.025f, 0.022f), 0.18f);
            ChipRow(playContent, new[] { Fire, Doctor, Librarian, Police, Teacher, Mail }, new Vector2(0, -0.029f), 0.044f);

            var quick = MakeButton("Btn_QuickPlay", panel, new Vector2(0, quickY), new Vector2(wide, quickH), UIColors.Sky, out var quickContent);
            Icon("BoltIcon", quickContent, new[]
                {
                    new Vector2(0.15f, 1f), new Vector2(-0.45f, -0.05f), new Vector2(0f, -0.05f),
                    new Vector2(-0.2f, -1f), new Vector2(0.5f, 0.15f), new Vector2(0.05f, 0.15f),
                }, 0.02f, UIColors.Sun, new Vector2(-0.215f, 0f)); // outline spans -1..1, so 4 cm tall
            Label(quickContent, "Quick play", semi, 0.04f, UIColors.Cream, new Vector2(-0.06f, 0f), 0.26f);
            ChipRow(quickContent, new[] { Fire, Doctor, Police }, new Vector2(0.165f, 0f), 0.044f);

            var howTo = MakeButton("Btn_HowToPlay", panel, new Vector2(-smallX, smallY), new Vector2(smallW, smallH), UIColors.Lilac, out var howContent);
            Label(howContent, "?", bold, 0.056f, UIColors.Coral, new Vector2(-0.085f, 0.002f), 0.05f);
            Label(howContent, "How to play", semi, 0.028f, UIColors.Ink, new Vector2(0.025f, 0f), 0.18f);

            var settings = MakeButton("Btn_Settings", panel, new Vector2(smallX, smallY), new Vector2(smallW, smallH), UIColors.Mist, out var setContent);
            Gear(setContent, new Vector2(-0.08f, 0f), 0.015f, UIColors.Ink);
            Label(setContent, "Settings", semi, 0.03f, UIColors.Ink, new Vector2(0.025f, 0f), 0.18f);

            // Wire buttons -> controller.
            UnityEventTools.AddPersistentListener(play.onPressed, new UnityAction(controller.OnPlay));
            UnityEventTools.AddPersistentListener(quick.onPressed, new UnityAction(controller.OnQuickPlay));
            UnityEventTools.AddPersistentListener(howTo.onPressed, new UnityAction(controller.OnHowToPlay));
            UnityEventTools.AddPersistentListener(settings.onPressed, new UnityAction(controller.OnSettings));

            var cso = new SerializedObject(controller);
            cso.FindProperty("board").objectReferenceValue = root;
            cso.FindProperty("playButton").objectReferenceValue = play;
            cso.FindProperty("quickPlayButton").objectReferenceValue = quick;
            cso.FindProperty("howToPlayButton").objectReferenceValue = howTo;
            cso.FindProperty("settingsButton").objectReferenceValue = settings;
            cso.FindProperty("panelMain").objectReferenceValue = panel;
            cso.ApplyModifiedPropertiesWithoutUndo();

            // Step 4: the other screens on the same board (hidden until opened).
            MenuPanelsBuilder.Build(board, controller);
            return controller;
        }

        // ------------------------------------------------------------------ Button

        /// <summary>
        /// Builds a 3D button: dark base, colored face (moves on press), hover glow, and a Meta ISDK surface
        /// that both the controller ray and poke can press. "content" is where labels and icons go (on the face).
        /// </summary>
        internal static KidButton3D MakeButton(string name, Transform parent, Vector2 center, Vector2 size, Color32 color,
            out Transform content)
        {
            var root = Group(name, parent, new Vector3(center.x, center.y, 0));
            float r = Mathf.Min(size.y * 0.38f, 0.04f);
            string key = $"Btn_{Mathf.RoundToInt(size.x * 1000)}x{Mathf.RoundToInt(size.y * 1000)}";

            Color baseColor = (Color)color * 0.72f;
            baseColor.a = 1f;
            MeshKit.Show("Base", root, MeshKit.RoundedSlab(key + "_Base", size.x, size.y, BaseDepth, r),
                Mat($"{name}_Base", (Color32)baseColor));

            var glow = MeshKit.Show("Glow", root,
                MeshKit.RoundedSlab(key + "_Glow", size.x + 0.016f, size.y + 0.016f, 0.004f, r + 0.008f),
                Unlit("Glow", UIColors.Sun));
            glow.SetActive(false);

            var face = Group("Face", root, new Vector3(0, 0, -BaseDepth));
            var faceMesh = MeshKit.Show("FaceMesh", face,
                MeshKit.RoundedSlab(key + "_Face", size.x - FaceInset * 2, size.y - FaceInset * 2, FaceDepth, r - FaceInset),
                Mat($"{name}_Face", color, 0.35f));
            content = Group("Content", face, new Vector3(0, 0, -FaceDepth - 0.0002f));

            // Meta Interaction SDK: one clipped plane on the face, shared by poke and ray.
            var surfaceGo = new GameObject("Surface");
            surfaceGo.transform.SetParent(root, false);
            surfaceGo.transform.localPosition = new Vector3(0, 0, -BaseDepth - FaceDepth);
            var plane = surfaceGo.AddComponent<PlaneSurface>(); // default facing: toward -Z (the player)
            var clipper = surfaceGo.AddComponent<BoundsClipper>();
            var clipped = surfaceGo.AddComponent<ClippedPlaneSurface>();
            Set(clipper, so => so.FindProperty("_size").vector3Value = new Vector3(size.x, size.y, 0.1f));
            Set(clipped, so =>
            {
                so.FindProperty("_planeSurface").objectReferenceValue = plane;
                var list = so.FindProperty("_clippers");
                list.arraySize = 1;
                list.GetArrayElementAtIndex(0).objectReferenceValue = clipper;
            });

            var poke = root.gameObject.AddComponent<PokeInteractable>();
            Set(poke, so =>
            {
                so.FindProperty("_surfacePatch").objectReferenceValue = clipped;
                so.FindProperty("_enterHoverNormal").floatValue = 0.05f; // "finger near" within 5 cm
                so.FindProperty("_exitHoverNormal").floatValue = 0.07f;
            });
            var ray = root.gameObject.AddComponent<RayInteractable>();
            Set(ray, so => so.FindProperty("_surface").objectReferenceValue = clipped);

            var button = root.gameObject.AddComponent<KidButton3D>();
            Set(button, so =>
            {
                so.FindProperty("face").objectReferenceValue = face;
                so.FindProperty("faceRenderer").objectReferenceValue = faceMesh.GetComponent<MeshRenderer>();
                so.FindProperty("glow").objectReferenceValue = glow;
                so.FindProperty("faceColor").colorValue = color;
                var p = so.FindProperty("pointables");
                p.arraySize = 2;
                p.GetArrayElementAtIndex(0).objectReferenceValue = poke;
                p.GetArrayElementAtIndex(1).objectReferenceValue = ray;
            });
            return button;
        }

        private static void Set(Object target, System.Action<SerializedObject> edit)
        {
            var so = new SerializedObject(target);
            edit(so);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ------------------------------------------------------------------ Content

        internal static TMPro.TextMeshPro Label(Transform content, string text, TMPro.TMP_FontAsset font, float em, Color32 color, Vector2 pos, float width)
        {
            var t = Text3D("Label_" + text, content, text, font, em, color, new Vector3(pos.x, pos.y, -0.0005f), new Vector2(width, em * 1.4f));
            t.textWrappingMode = TMPro.TextWrappingModes.NoWrap;
            return t;
        }

        /// <summary>Extruded flat icon: the polygon is multiplied by scale (meters per unit).</summary>
        internal static void Icon(string name, Transform content, Vector2[] unitPolygon, float height, Color32 color, Vector2 pos)
        {
            var pts = new Vector2[unitPolygon.Length];
            for (int i = 0; i < pts.Length; i++) pts[i] = unitPolygon[i] * height;
            MeshKit.Show(name, content, MeshKit.Extrude("Icon_" + name, pts, 0.004f), Mat("Icon_" + ColorUtility.ToHtmlStringRGB(color), color),
                new Vector3(pos.x, pos.y, 0));
        }

        internal static void Gear(Transform content, Vector2 pos, float radius, Color32 color)
        {
            var g = Group("GearIcon", content, new Vector3(pos.x, pos.y, 0));
            var mat = Mat("Icon_" + ColorUtility.ToHtmlStringRGB(color), color);
            Cyl("Hub", g, new Vector3(0, 0, -0.002f), radius * 0.78f, 0.004f, mat, new Vector3(90, 0, 0));
            for (int i = 0; i < 8; i++)
            {
                float a = i * 45f;
                var tooth = Box("Tooth", g, Vector3.zero, new Vector3(radius * 0.42f, radius * 0.5f, 0.004f), mat, new Vector3(0, 0, a));
                tooth.transform.localPosition = Quaternion.Euler(0, 0, a) * new Vector3(0, radius * 0.85f, -0.002f);
            }
            Cyl("Hole", g, new Vector3(0, 0, -0.0042f), radius * 0.3f, 0.0006f, Mat("Mist", UIColors.Mist), new Vector3(90, 0, 0));
        }

        /// <summary>Row of helper chips centered at pos.</summary>
        internal static void ChipRow(Transform content, Helper[] helpers, Vector2 pos, float spacing)
        {
            var row = Group("HelperChips", content, new Vector3(pos.x, pos.y, 0));
            float x0 = -spacing * (helpers.Length - 1) * 0.5f;
            for (int i = 0; i < helpers.Length; i++)
                Chip(row, helpers[i], new Vector2(x0 + i * spacing, 0));
        }

        /// <summary>
        /// A helper chip: colored disc with a cream rim and a tiny 3D model. Board size is 3.3 cm across;
        /// scale multiplies everything (the title sign uses big ones). Returns the model so callers can animate it.
        /// </summary>
        internal static Transform Chip(Transform row, Helper h, Vector2 pos, float scale = 1f)
        {
            const float r = 0.0165f;
            var chip = Group($"Chip_{h.Model}", row, new Vector3(pos.x, pos.y, 0));
            chip.localScale = Vector3.one * scale;
            Cyl("Rim", chip, new Vector3(0, 0, -0.001f), r + 0.0015f, 0.002f, Mat("Cream", UIColors.Cream), new Vector3(90, 0, 0));
            Cyl("Disc", chip, new Vector3(0, 0, -0.0025f), r, 0.003f, Mat("Chip_" + h.Model, h.Color), new Vector3(90, 0, 0));

            // Tiny 3D model sitting on the chip, tipped toward the player a little.
            var holder = Group("Model", chip, new Vector3(0, 0, -0.004f), new Vector3(-12f, 0, 0));
            var model = KenneyKit.Place(h.Kit, h.Model, holder, Vector3.zero, h.Yaw, out var size);
            float s = 0.025f / Mathf.Max(0.0001f, Mathf.Max(size.x, Mathf.Max(size.y, size.z)));
            model.localScale = Vector3.one * s;
            var b = KenneyKit.LocalBounds(holder);
            model.localPosition -= new Vector3(b.center.x, b.center.y, b.max.z);
            return model;
        }

        // ------------------------------------------------------------------ Easel

        private static void BuildEaselLegs(Transform root, Transform board, Material wood)
        {
            var table = root.parent;
            // "Behind the board" along the table, following the board's turn toward the seat.
            Vector3 back = Vector3.ProjectOnPlane(board.forward, table.up).normalized;
            float legX = FrameSize.x * 0.36f, footX = FrameSize.x * 0.44f;
            for (int side = -1; side <= 1; side += 2)
            {
                // Back leg: from behind the board's upper half down and back to the table.
                Vector3 top = board.TransformPoint(new Vector3(side * legX, 0.06f, 0.005f));
                Vector3 foot = board.TransformPoint(new Vector3(side * (legX + 0.02f), 0.06f, 0.005f)) + back * 0.2f;
                foot.y = table.position.y;
                BoxBetween($"BackLeg_{(side < 0 ? "L" : "R")}", root, top, foot, 0.018f, wood);

                // Front foot: from the bottom edge straight down to the table.
                Vector3 bottom = board.TransformPoint(new Vector3(side * footX, -FrameSize.y * 0.5f, -0.012f));
                Vector3 floor = new Vector3(bottom.x, table.position.y, bottom.z);
                BoxBetween($"FrontFoot_{(side < 0 ? "L" : "R")}", root, bottom, floor, 0.022f, wood);
            }
        }

        private static void BoxBetween(string name, Transform parent, Vector3 aWorld, Vector3 bWorld, float thickness, Material mat)
        {
            var go = Box(name, parent, Vector3.zero, Vector3.one, mat);
            Vector3 d = bWorld - aWorld;
            go.transform.position = (aWorld + bWorld) * 0.5f;
            go.transform.rotation = Quaternion.FromToRotation(Vector3.up, d.normalized);
            go.transform.localScale = new Vector3(thickness, Mathf.Max(0.001f, d.magnitude), thickness);
        }
    }
}
