using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;
using static CommunityHelpers.EditorTools.BuildKit;
using Room = CommunityHelpers.EditorTools.MainMenuSceneBuilder;

namespace CommunityHelpers.EditorTools
{
    /// <summary>
    /// Step 3: the big "Community Helpers" marquee sign hanging in front of the top of the window,
    /// plus party bunting along the walls and a confetti cannon.
    /// Sign space: x right, y up, front faces -Z (toward the player).
    /// </summary>
    public static class TitleSignBuilder
    {
        private const string Title = "Community Helpers";

        // Hanging point (room space) and size.
        private const float HangY = 2.9f;
        private static float HangZ => Room.BackWallZ - 0.34f;
        private const float TiltTowardPlayer = -12f;    // bottom swings out so the face looks down at the seat
        // Title only (no helper row), so the sign is a wide banner.
        private static readonly Vector2 BorderSize = new(2.46f, 0.58f);   // wide coral ring so the bulbs have room
        private static readonly Vector2 BoardSize = new(2.22f, 0.34f);
        private const float BorderDepth = 0.05f, BoardDepth = 0.03f;
        private const float TitleEm = 0.2f;            // letter size (m); "Community Helpers" is ~1.9 m wide

        // Title letters: yellow (Sun) with an ink outline and ink depth layers.
        private static readonly Color32 TitleColor = UIColors.Sun;

        // Confetti and bunting cycle through the helper colors.
        private static readonly Color32[] Rainbow =
        {
            UIColors.Coral, UIColors.Sun, UIColors.Teal, UIColors.Sky, UIColors.Violet, UIColors.TryAgain, UIColors.YayButton,
        };

        public static TitleSignAnimator Build(Transform world)
        {
            var root = Group("TitleSign", world, new Vector3(0, HangY, HangZ));
            var swing = Group("Swing", root);                       // sways around the hanging point
            var sign = Group("Sign", swing, new Vector3(0, -BorderSize.y * 0.5f, 0), new Vector3(TiltTowardPlayer, 0, 0));
            var anim = root.gameObject.AddComponent<TitleSignAnimator>();

            var ink = Mat("Ink", UIColors.Ink);

            // Two ropes up to the ceiling.
            float ropeLen = Room.WallHeight - HangY + 0.02f;
            for (int side = -1; side <= 1; side += 2)
                Cyl("Rope", root, new Vector3(side * BorderSize.x * 0.38f, ropeLen * 0.5f, 0), 0.007f, ropeLen, ink);

            // Coral border with marquee bulbs, cream board on top.
            MeshKit.Show("Border", sign, MeshKit.RoundedSlab("Title_Border", BorderSize.x, BorderSize.y, BorderDepth, 0.13f),
                Mat("Coral", UIColors.Coral));
            MeshKit.Show("Board", sign, MeshKit.RoundedSlab("Title_Board", BoardSize.x, BoardSize.y, BoardDepth, 0.09f),
                Mat("Cream", UIColors.Cream), new Vector3(0, 0, -BorderDepth));
            float front = -BorderDepth - BoardDepth;

            var bulbs = BuildBulbs(sign, out var bulbOn, out var bulbOff);
            var letters = BuildTitleText(sign, front);
            var stars = BuildStars(sign);
            var confetti = BuildConfetti(root);
            BuildBunting(world);

            var so = new SerializedObject(anim);
            SetArray(so.FindProperty("letterLayers"), letters);
            SetArray(so.FindProperty("bulbs"), bulbs);
            so.FindProperty("bulbOn").objectReferenceValue = bulbOn;
            so.FindProperty("bulbOff").objectReferenceValue = bulbOff;
            SetArray(so.FindProperty("spinners"), new List<Transform>());
            SetArray(so.FindProperty("stars"), stars);
            so.FindProperty("swing").objectReferenceValue = swing;
            so.FindProperty("confetti").objectReferenceValue = confetti;
            so.ApplyModifiedPropertiesWithoutUndo();
            return anim;
        }

        private static void SetArray<T>(SerializedProperty prop, IList<T> items) where T : Object
        {
            prop.arraySize = items.Count;
            for (int i = 0; i < items.Count; i++) prop.GetArrayElementAtIndex(i).objectReferenceValue = items[i];
        }

        // ------------------------------------------------------------------ Title text

        /// <summary>Front layer (yellow letters, ink outline) plus darker layers behind it for chunky 3D letters.</summary>
        private static List<TMP_Text> BuildTitleText(Transform sign, float front)
        {
            var font = FontSetup.Load(FontSetup.FredokaBold);
            var layers = new List<TMP_Text>();
            const float y = 0f, lift = 0.035f;          // centered; letters float this far off the board
            const int depthLayers = 5;
            const float step = 0.005f;

            // Front layer: sunny yellow letters with a thick ink outline (saved material preset).
            var frontText = Text3D("Title_Front", sign, Title, font, TitleEm, TitleColor,
                new Vector3(0, y, front - lift), new Vector2(BoardSize.x - 0.1f, TitleEm * 1.3f));
            frontText.textWrappingMode = TextWrappingModes.NoWrap;
            frontText.fontSharedMaterial = OutlinedMaterial(font);
            layers.Add(frontText);

            // Depth layers (ink), stepping back toward the board and slightly down.
            for (int k = 1; k <= depthLayers; k++)
            {
                var t = Text3D($"Title_Depth{k}", sign, Title, font, TitleEm, UIColors.Ink,
                    new Vector3(0, y - k * 0.0025f, front - lift + k * step), new Vector2(BoardSize.x - 0.1f, TitleEm * 1.3f));
                t.textWrappingMode = TextWrappingModes.NoWrap;
                layers.Add(t);
            }
            return layers;
        }

        private static Material OutlinedMaterial(TMP_FontAsset font)
        {
            EnsureFolder(MaterialDir);
            string path = $"{MaterialDir}/M_TitleFront.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(font.material);
                AssetDatabase.CreateAsset(mat, path);
            }
            else mat.CopyPropertiesFromMaterial(font.material);
            mat.shader = font.material.shader;
            ShaderUtilities.GetShaderPropertyIDs();
            mat.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.22f);
            mat.SetColor(ShaderUtilities.ID_OutlineColor, UIColors.Ink);
            mat.SetFloat(ShaderUtilities.ID_FaceDilate, 0.1f);
            mat.EnableKeyword(ShaderUtilities.Keyword_Outline);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        // ------------------------------------------------------------------ Bulbs

        private static List<Renderer> BuildBulbs(Transform sign, out Material on, out Material off)
        {
            on = Unlit("BulbOn", new Color32(0xFF, 0xF6, 0xCF, 255));
            off = Mat("BulbOff", new Color32(0xD9, 0x9A, 0x3A, 255), 0.6f);
            var offMat = off; // local copy: out parameters can't be used inside the local function below
            var group = Group("Bulbs", sign);
            var list = new List<Renderer>();
            float bx = (BorderSize.x + BoardSize.x) * 0.25f;   // middle of the border ring
            float by = (BorderSize.y + BoardSize.y) * 0.25f;
            float z = -BorderDepth - 0.012f;                    // bulbs sit proud of the border face

            void Add(float x, float y)
            {
                var b = Sphere("Bulb", group, new Vector3(x, y, z), Vector3.one * 0.04f, offMat);
                list.Add(b.GetComponent<Renderer>());
            }

            // Clockwise from top-left so the chase runs around the sign.
            const float step = 0.1f;
            int nx = Mathf.FloorToInt((bx * 2f - 0.1f) / step);
            int ny = Mathf.FloorToInt((by * 2f - 0.1f) / step);
            for (int i = 0; i <= nx; i++) Add(-bx + 0.05f + i * (bx * 2f - 0.1f) / nx, by);
            for (int i = 1; i < ny; i++) Add(bx, by - i * (by * 2f) / ny);
            for (int i = 0; i <= nx; i++) Add(bx - 0.05f - i * (bx * 2f - 0.1f) / nx, -by);
            for (int i = 1; i < ny; i++) Add(-bx, -by + i * (by * 2f) / ny);
            return list;
        }

        // ------------------------------------------------------------------ Stars

        private static List<Transform> BuildStars(Transform sign)
        {
            var pts = new List<Vector2>();
            for (int i = 0; i < 10; i++)
            {
                float a = Mathf.PI * 0.5f + i * Mathf.PI / 5f;
                float r = i % 2 == 0 ? 0.14f : 0.06f;
                pts.Add(new Vector2(Mathf.Cos(a) * r, Mathf.Sin(a) * r));
            }
            var mesh = MeshKit.Extrude("Title_Star", pts, 0.035f);
            var sun = Mat("Sun", UIColors.Sun, 0.4f);
            var list = new List<Transform>();
            for (int side = -1; side <= 1; side += 2)
            {
                var s = MeshKit.Show("Star", sign, mesh, sun, new Vector3(side * (BorderSize.x * 0.5f + 0.17f), 0.02f, -BorderDepth - 0.01f));
                list.Add(s.transform);
                // A smaller star tucked above each big one.
                var s2 = MeshKit.Show("StarSmall", sign, mesh, Mat("Teal", UIColors.Teal, 0.4f),
                    new Vector3(side * (BorderSize.x * 0.5f - 0.12f), BorderSize.y * 0.5f + 0.1f, -BorderDepth - 0.015f));
                s2.transform.localScale = Vector3.one * 0.55f;
                list.Add(s2.transform);
            }
            return list;
        }

        // ------------------------------------------------------------------ Confetti

        private static ParticleSystem BuildConfetti(Transform root)
        {
            var go = new GameObject("Confetti");
            go.transform.SetParent(root, false);
            go.transform.localPosition = new Vector3(0, -BorderSize.y - 0.05f, -0.25f);
            go.transform.rotation = Quaternion.LookRotation(new Vector3(0, -0.35f, -1f)); // down and toward the player

            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.playOnAwake = false;
            main.loop = false;
            main.duration = 1f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(2.5f, 3.5f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.6f, 1.6f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.02f, 0.04f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.gravityModifier = 0.22f;
            main.maxParticles = 300;

            var palette = new Gradient { mode = GradientMode.Fixed };
            var keys = new GradientColorKey[Rainbow.Length];
            for (int i = 0; i < Rainbow.Length; i++) keys[i] = new GradientColorKey(Rainbow[i], (i + 1f) / Rainbow.Length);
            palette.SetKeys(keys, new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
            main.startColor = new ParticleSystem.MinMaxGradient(palette) { mode = ParticleSystemGradientMode.RandomColor };

            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 160) });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(2.0f, 0.1f, 0.2f);

            var rot = ps.rotationOverLifetime;
            rot.enabled = true;
            rot.z = new ParticleSystem.MinMaxCurve(-6f, 6f);

            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.35f;
            noise.frequency = 0.8f;

            var fade = ps.colorOverLifetime;
            fade.enabled = true;
            var alpha = new Gradient();
            alpha.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.8f), new GradientAlphaKey(0f, 1f) });
            fade.color = alpha;

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            EnsureFolder(MaterialDir);
            string path = $"{MaterialDir}/M_Confetti.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Legacy Shaders/Particles/Alpha Blended"));
                AssetDatabase.CreateAsset(mat, path);
            }
            renderer.sharedMaterial = mat;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return ps;
        }

        // ------------------------------------------------------------------ Bunting

        /// <summary>Strings of triangle flags along the top of the front wall (beside the sign) and both side walls.</summary>
        private static void BuildBunting(Transform world)
        {
            var group = Group("Bunting", world);
            var flag = MeshKit.Extrude("Bunting_Flag", new[] { new Vector2(-0.075f, 0f), new Vector2(0.075f, 0f), new Vector2(0f, -0.18f) }, 0.004f);
            var mats = new Material[Rainbow.Length];
            for (int i = 0; i < Rainbow.Length; i++) mats[i] = Mat("Flag_" + i, Rainbow[i]);
            var cord = Mat("Ink", UIColors.Ink);

            float wx = Room.SideWallX - 0.06f, wz = Room.BackWallZ - 0.08f;
            const float y = 2.92f;
            int colorIndex = 0;

            // Front wall: from each corner in to just past the sign. Flags face the player (-Z).
            Strand(group, new Vector3(-wx, y, wz), new Vector3(-1.75f, y - 0.04f, wz), 0.12f, 0f, flag, mats, cord, ref colorIndex);
            Strand(group, new Vector3(1.75f, y - 0.04f, wz), new Vector3(wx, y, wz), 0.12f, 0f, flag, mats, cord, ref colorIndex);
            // Side walls: from the front corner back toward the player. Flags face into the room.
            Strand(group, new Vector3(-wx, y, wz), new Vector3(-wx, y, -0.2f), 0.2f, -90f, flag, mats, cord, ref colorIndex);
            Strand(group, new Vector3(wx, y, wz), new Vector3(wx, y, -0.2f), 0.2f, 90f, flag, mats, cord, ref colorIndex);
        }

        private static void Strand(Transform parent, Vector3 a, Vector3 b, float sag, float flagYaw,
            Mesh flag, Material[] mats, Material cord, ref int colorIndex)
        {
            const float spacing = 0.24f;
            int n = Mathf.Max(2, Mathf.CeilToInt(Vector3.Distance(a, b) / spacing));
            Vector3 P(float t) => Vector3.Lerp(a, b, t) + Vector3.down * (sag * 4f * t * (1f - t));

            var strand = Group("Strand", parent);
            Vector3 prev = P(0f);
            for (int i = 1; i <= n; i++)
            {
                Vector3 p = P((float)i / n);
                // Cord segment.
                var seg = Cyl("Cord", strand, Vector3.zero, 0.003f, 1f, cord);
                Vector3 d = p - prev;
                seg.transform.localPosition = (p + prev) * 0.5f;
                seg.transform.localRotation = Quaternion.FromToRotation(Vector3.up, d.normalized);
                seg.transform.localScale = new Vector3(0.006f, d.magnitude * 0.5f, 0.006f);
                // Flag hanging from the middle of the segment.
                MeshKit.Show("Flag", strand, flag, mats[colorIndex++ % mats.Length], (p + prev) * 0.5f, new Vector3(0, flagYaw, 0));
                prev = p;
            }
        }
    }
}
