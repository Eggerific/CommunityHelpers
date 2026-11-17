using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.Events;
using static CommunityHelpers.EditorTools.BuildKit;
using B = CommunityHelpers.EditorTools.MenuBoardBuilder;

namespace CommunityHelpers.EditorTools
{
    /// <summary>
    /// Step 4: the menu screens besides Main, as panels on the same easel board (hidden until opened):
    /// "Dress up Robo!" (hats + Let's go!), "High five!", Settings, and How to play.
    /// Layout is in design-panel units (0.62 x 0.42, center = 0,0), same as Panel_Main.
    /// </summary>
    public static class MenuPanelsBuilder
    {
        internal static readonly Vector2[] BackArrow =
        {
            new(-1f, 0f), new(-0.2f, 0.75f), new(-0.2f, 0.3f), new(0.9f, 0.3f),
            new(0.9f, -0.3f), new(-0.2f, -0.3f), new(-0.2f, -0.75f),
        };
        internal static readonly Vector2[] PlayTriangle = { new(-0.4f, -0.5f), new(0.6f, 0f), new(-0.4f, 0.5f) };

        // Hat button colors, in GameSession.RoboHat order (firefighter, police, mail carrier, doctor).
        private static readonly Color32[] HatColors = { UIColors.Coral, UIColors.Sky, UIColors.Violet, UIColors.Teal };

        public static void Build(Transform board, MainMenuController controller)
        {
            var dressUp = BuildDressUp(board, controller, out var hatButtons, out var letsGo);
            var highFive = BuildHighFive(board, controller);
            var settings = BuildSettings(board, controller, out var dots, out var dotOn, out var dotOff);
            var howTo = BuildHowToPlay(board, controller);

            var so = new SerializedObject(controller);
            so.FindProperty("panelDressUp").objectReferenceValue = dressUp;
            so.FindProperty("panelHighFive").objectReferenceValue = highFive;
            so.FindProperty("panelSettings").objectReferenceValue = settings;
            so.FindProperty("panelHowToPlay").objectReferenceValue = howTo;
            SetArray(so.FindProperty("hatButtons"), hatButtons);
            so.FindProperty("letsGoButton").objectReferenceValue = letsGo;
            SetArray(so.FindProperty("volumeDots"), dots);
            so.FindProperty("dotOn").objectReferenceValue = dotOn;
            so.FindProperty("dotOff").objectReferenceValue = dotOff;
            so.ApplyModifiedPropertiesWithoutUndo();

            dressUp.gameObject.SetActive(false);
            highFive.gameObject.SetActive(false);
            settings.gameObject.SetActive(false);
            howTo.gameObject.SetActive(false);
        }

        /// <summary>
        /// Replaces the "Who are you today?" (or an older "Dress up Robo!") panel and the High five panel in an
        /// existing Main Menu scene, and rewires the controller. Used by "Update Main Menu".
        /// </summary>
        internal static void RebuildDressUp(MainMenuController controller)
        {
            var so = new SerializedObject(controller);
            var dressProp = so.FindProperty("panelDressUp");
            var highProp = so.FindProperty("panelHighFive");
            var old = dressProp.objectReferenceValue as Transform;
            Transform board = old != null ? old.parent : null;
            if (board == null)
            {
                Debug.LogError("[MenuPanelsBuilder] Couldn't find the old animal picker / dress-up panel to replace.");
                return;
            }
            Object.DestroyImmediate(old.gameObject);
            if (highProp.objectReferenceValue is Transform oldHigh && oldHigh != null) Object.DestroyImmediate(oldHigh.gameObject);

            var dressUp = BuildDressUp(board, controller, out var hatButtons, out var letsGo);
            var highFive = BuildHighFive(board, controller);
            dressProp.objectReferenceValue = dressUp;
            highProp.objectReferenceValue = highFive;
            SetArray(so.FindProperty("hatButtons"), hatButtons);
            so.FindProperty("letsGoButton").objectReferenceValue = letsGo;
            so.ApplyModifiedPropertiesWithoutUndo();
            dressUp.gameObject.SetActive(false);
            highFive.gameObject.SetActive(false);
        }

        /// <summary>A blank playing card (white border, colored middle), tilted a little. Stands in for card art.</summary>
        private static void PlainCard(Transform parent, Vector2 pos, float tilt, Color32 color)
        {
            var g = Group("Card", parent, new Vector3(pos.x, pos.y, 0), new Vector3(0, 0, tilt));
            MeshKit.Show("Edge", g, MeshKit.RoundedSlab("HowTo_PlainCard", 0.04f, 0.056f, 0.002f, 0.006f), Mat("Cream", UIColors.Cream));
            MeshKit.Show("Middle", g, MeshKit.RoundedSlab("HowTo_PlainCardIn", 0.03f, 0.046f, 0.001f, 0.004f),
                Mat("Card_" + ColorUtility.ToHtmlStringRGB(color), color), new Vector3(0, 0, -0.002f));
        }

        /// <summary>A "target" icon: a ring with a gap at the top (one simple polygon, so it extrudes cleanly).</summary>
        private static Vector2[] Ring(float outer, float inner)
        {
            var pts = new List<Vector2>();
            const int n = 20;
            const float gap = 0.35f; // radians either side of straight up
            float a0 = Mathf.PI * 0.5f + gap, a1 = Mathf.PI * 2.5f - gap;
            for (int i = 0; i <= n; i++) { float a = Mathf.Lerp(a0, a1, i / (float)n); pts.Add(new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * outer); }
            for (int i = n; i >= 0; i--) { float a = Mathf.Lerp(a0, a1, i / (float)n); pts.Add(new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * inner); }
            return pts.ToArray();
        }

        private static void SetArray<T>(SerializedProperty prop, IList<T> items) where T : Object
        {
            prop.arraySize = items.Count;
            for (int i = 0; i < items.Count; i++) prop.GetArrayElementAtIndex(i).objectReferenceValue = items[i];
        }

        private static void On(KidButton3D button, UnityAction action) =>
            UnityEventTools.AddPersistentListener(button.onPressed, action);

        private static void Title(Transform panel, string text)
        {
            B.Label(panel, text, FontSetup.Load(FontSetup.FredokaSemiBold), 0.04f, UIColors.Ink, new Vector2(0, 0.165f), 0.5f);
        }

        /// <summary>Mist-colored "← Back" button.</summary>
        internal static KidButton3D BackButton(Transform panel, Vector2 pos, Vector2 size, string text = "Back")
        {
            var btn = B.MakeButton("Btn_Back", panel, pos, size, UIColors.Mist, out var c);
            if (string.IsNullOrEmpty(text))
            {
                B.Icon("BackIcon", c, BackArrow, 0.02f, UIColors.Ink, Vector2.zero);
                return btn;
            }

            // Arrow pinned near the left edge; the label starts after it, left-aligned, so the two never overlap.
            const float arrowHalf = 0.02f, edge = 0.022f, gap = 0.012f;
            float arrowX = -size.x * 0.5f + edge + arrowHalf;
            B.Icon("BackIcon", c, BackArrow, 0.02f, UIColors.Ink, new Vector2(arrowX, 0f));
            float textLeft = arrowX + arrowHalf + gap;
            float textWidth = size.x * 0.5f - edge * 0.5f - textLeft;
            var label = B.Label(c, text, FontSetup.Load(FontSetup.FredokaSemiBold), 0.03f, UIColors.Ink,
                new Vector2(textLeft + textWidth * 0.5f, 0f), textWidth);
            label.alignment = TMPro.TextAlignmentOptions.Left;
            label.enableAutoSizing = true;   // long labels shrink to fit instead of running into the arrow
            label.fontSizeMin = 0.18f;
            label.fontSizeMax = 0.3f;
            return btn;
        }

        // ------------------------------------------------------------------ Dress up Robo! / High five

        /// <summary>
        /// "Dress up Robo!": four hat buttons (each shows its hat) and Let's go! (grayed out until a hat is on).
        /// Picking a hat drops it onto Robo's head (RoboDressUp).
        /// </summary>
        private static Transform BuildDressUp(Transform board, MainMenuController controller,
            out List<KidButton3D> hatButtons, out KidButton3D letsGo)
        {
            var semi = FontSetup.Load(FontSetup.FredokaSemiBold);
            var panel = B.NewPanel("Panel_DressUp", board);
            Title(panel, "Dress up Robo!");

            hatButtons = new List<KidButton3D>();
            const float w = 0.125f, h = 0.165f, spacing = 0.135f, y = 0.03f;
            for (int i = 0; i < RoboHatsBuilder.HatNames.Length; i++)
            {
                string name = RoboHatsBuilder.HatNames[i];
                float x = (i - 1.5f) * spacing;
                var btn = B.MakeButton($"Btn_Hat_{name.Replace(" ", "")}", panel, new Vector2(x, y), new Vector2(w, h), HatColors[i], out var c);
                RoboHatsBuilder.ButtonHat(i, c, new Vector2(0, 0.022f), 0.085f);
                var label = B.Label(c, name, semi, 0.024f, UIColors.Cream, new Vector2(0, -0.058f), w - 0.012f);
                label.enableAutoSizing = true; // "Mail carrier" is the longest
                label.fontSizeMin = 0.14f;
                label.fontSizeMax = 0.24f;
                UnityEventTools.AddIntPersistentListener(btn.onPressed, new UnityAction<int>(controller.OnPickHat), i);
                hatButtons.Add(btn);
            }

            letsGo = B.MakeButton("Btn_LetsGo", panel, new Vector2(-0.02f, -0.14f), new Vector2(0.28f, 0.085f), UIColors.YayButton, out var lc);
            B.Icon("PlayIcon", lc, PlayTriangle, 0.032f, UIColors.Cream, new Vector2(-0.085f, 0f));
            B.Label(lc, "Let's go!", semi, 0.038f, UIColors.Cream, new Vector2(0.025f, 0f), 0.2f);
            On(letsGo, controller.OnLetsGo);

            var back = BackButton(panel, new Vector2(0.235f, -0.14f), new Vector2(0.1f, 0.085f), text: null);
            On(back, controller.OnBack);
            return panel;
        }

        /// <summary>
        /// "High five!": shown while Robo stands at the front of the table with his hand up. A big arrow points
        /// right, toward Robo. Back (bottom left, away from Robo) goes back to the hats.
        /// </summary>
        private static Transform BuildHighFive(Transform board, MainMenuController controller)
        {
            var bold = FontSetup.Load(FontSetup.FredokaBold);
            var andika = FontSetup.Load(FontSetup.AndikaRegular);
            var panel = B.NewPanel("Panel_HighFive", board);

            B.Label(panel, "High five!", bold, 0.07f, UIColors.Ink, new Vector2(-0.04f, 0.1f), 0.45f);
            Text3D("Note", panel, "Touch Robo's glowing hand to start!", andika, 0.03f, UIColors.Ink,
                new Vector3(-0.04f, 0.015f, -0.0005f), new Vector2(0.42f, 0.09f));

            // Arrow pointing right, toward Robo (the back arrow, mirrored).
            var arrow = new Vector2[BackArrow.Length];
            for (int i = 0; i < arrow.Length; i++) arrow[i] = new Vector2(-BackArrow[BackArrow.Length - 1 - i].x, BackArrow[BackArrow.Length - 1 - i].y);
            B.Icon("HighFiveArrow", panel, arrow, 0.045f, UIColors.Sun, new Vector2(0.23f, 0.05f));

            var back = BackButton(panel, new Vector2(-0.2f, -0.15f), new Vector2(0.18f, 0.075f));
            On(back, controller.OnBackToDressUp);
            return panel;
        }

        // ------------------------------------------------------------------ Settings

        private static Transform BuildSettings(Transform board, MainMenuController controller,
            out List<Renderer> dots, out Material dotOn, out Material dotOff)
        {
            var semi = FontSetup.Load(FontSetup.FredokaSemiBold);
            var andika = FontSetup.Load(FontSetup.AndikaRegular);
            var panel = B.NewPanel("Panel_Settings", board);
            B.Gear(panel, new Vector2(-0.115f, 0.165f), 0.017f, UIColors.Ink);
            B.Label(panel, "Settings", semi, 0.04f, UIColors.Ink, new Vector2(0.025f, 0.165f), 0.3f);

            // Volume:  Volume   [-]  ● ● ● ● ○  [+]
            const float rowA = 0.075f;
            B.Label(panel, "Volume", semi, 0.032f, UIColors.Ink, new Vector2(-0.17f, rowA), 0.22f);
            var minus = B.MakeButton("Btn_VolumeDown", panel, new Vector2(0.02f, rowA), new Vector2(0.07f, 0.07f), UIColors.Mist, out var mc);
            B.Icon("Minus", mc, new[] { new Vector2(-1f, -0.25f), new Vector2(1f, -0.25f), new Vector2(1f, 0.25f), new Vector2(-1f, 0.25f) },
                0.016f, UIColors.Ink, Vector2.zero);
            On(minus, controller.OnVolumeDown);

            dotOn = Mat("VolumeDotOn", UIColors.Sky, 0.4f);
            dotOff = Mat("VolumeDotOff", new Color32(0xC9, 0xCE, 0xD8, 255), 0.4f);
            dots = new List<Renderer>();
            for (int i = 0; i < GameSettings.MaxVolume; i++)
            {
                var d = Cyl($"VolumeDot{i + 1}", panel, new Vector3(0.08f + i * 0.03f, rowA, -0.003f), 0.011f, 0.004f, dotOn, new Vector3(90, 0, 0));
                dots.Add(d.GetComponent<Renderer>());
            }

            var plus = B.MakeButton("Btn_VolumeUp", panel, new Vector2(0.255f, rowA), new Vector2(0.07f, 0.07f), UIColors.Mist, out var pc);
            B.Icon("Plus", pc, new[]
                {
                    new Vector2(-1f, -0.25f), new Vector2(-0.25f, -0.25f), new Vector2(-0.25f, -1f), new Vector2(0.25f, -1f),
                    new Vector2(0.25f, -0.25f), new Vector2(1f, -0.25f), new Vector2(1f, 0.25f), new Vector2(0.25f, 0.25f),
                    new Vector2(0.25f, 1f), new Vector2(-0.25f, 1f), new Vector2(-0.25f, 0.25f), new Vector2(-1f, 0.25f),
                }, 0.016f, UIColors.Ink, Vector2.zero);
            On(plus, controller.OnVolumeUp);

            // Re-center:  View   [ ⟳ Re-center ]   puts the table and menu back in front of the player.
            const float rowB = -0.02f;
            B.Label(panel, "View", semi, 0.032f, UIColors.Ink, new Vector2(-0.17f, rowB), 0.22f);
            var recenter = B.MakeButton("Btn_Recenter", panel, new Vector2(0.14f, rowB), new Vector2(0.24f, 0.07f), UIColors.Sky, out var rc);
            B.Icon("TargetIcon", rc, Ring(1f, 0.55f), 0.017f, UIColors.Cream, new Vector2(-0.08f, 0f));
            B.Label(rc, "Re-center", semi, 0.028f, UIColors.Cream, new Vector2(0.022f, 0f), 0.16f);
            On(recenter, controller.OnRecenter);

            Text3D("Note", panel, "Grown-ups: please supervise kids under 10 and keep sessions short.", andika, 0.017f,
                UIColors.Muted, new Vector3(0, -0.085f, -0.0005f), new Vector2(0.55f, 0.03f));

            var back = BackButton(panel, new Vector2(-0.12f, -0.15f), new Vector2(0.2f, 0.075f));
            On(back, controller.OnBack);
            var quit = B.MakeButton("Btn_Quit", panel, new Vector2(0.12f, -0.15f), new Vector2(0.2f, 0.075f), UIColors.Peach, out var qc);
            B.Label(qc, "Quit", semi, 0.03f, UIColors.Ink, Vector2.zero, 0.15f);
            On(quit, controller.OnQuit);
            return panel;
        }

        // ------------------------------------------------------------------ How to play

        /// <summary>
        /// Replaces the How to play panel in an existing Main Menu scene (keeps everything else).
        /// Used by MainMenuSceneBuilder's "Update Main Menu".
        /// </summary>
        internal static void RebuildHowToPlay(MainMenuController controller)
        {
            var so = new SerializedObject(controller);
            var prop = so.FindProperty("panelHowToPlay");
            var old = prop.objectReferenceValue as Transform;
            Transform board = old != null ? old.parent : null;
            if (board == null)
            {
                Debug.LogError("[MenuPanelsBuilder] Couldn't find the How to play panel to replace.");
                return;
            }
            Object.DestroyImmediate(old.gameObject);
            var panel = BuildHowToPlay(board, controller);
            prop.objectReferenceValue = panel;
            so.ApplyModifiedPropertiesWithoutUndo();
            panel.gameObject.SetActive(false);
        }

        /// <summary>
        /// Three steps that match the real game (Nov 17): pick up a card from the deck, put it with its helper,
        /// fill all the stars. A tip below says a helper at the window helps with wrong cards.
        /// </summary>
        private static Transform BuildHowToPlay(Transform board, MainMenuController controller)
        {
            var semi = FontSetup.Load(FontSetup.FredokaSemiBold);
            var andika = FontSetup.Load(FontSetup.AndikaRegular);
            var panel = B.NewPanel("Panel_HowToPlay", board);
            Title(panel, "How to play");

            var cardMat = Mat("Lilac", UIColors.Lilac, 0.3f);
            var sun = Mat("Sun", UIColors.Sun, 0.4f);
            var cardMesh = MeshKit.RoundedSlab("HowTo_Card", 0.17f, 0.19f, 0.004f, 0.025f);
            string[] steps = { "Pick up a card from the deck.", "Put it with its helper.", "Fill all the stars!" };
            for (int i = 0; i < 3; i++)
            {
                float x = (i - 1) * 0.19f;
                var card = Group($"Step{i + 1}", panel, new Vector3(x, 0.02f, 0));
                MeshKit.Show("Card", card, cardMesh, cardMat);
                // Number badge.
                Cyl("Badge", card, new Vector3(-0.06f, 0.07f, -0.006f), 0.018f, 0.004f, sun, new Vector3(90, 0, 0));
                B.Label(card, (i + 1).ToString(), semi, 0.026f, UIColors.Ink, new Vector2(-0.06f, 0.07f), 0.03f)
                    .transform.localPosition += new Vector3(0, 0, -0.008f); // in front of the badge

                var pic = Group("Picture", card, new Vector3(0, 0.02f, -0.004f));
                if (i == 0)
                {
                    // The deck (face-down, gold backs like the DRAW pile) with the top card lifted off.
                    PlainCard(pic, new Vector2(-0.012f, -0.008f), 0f, UIColors.CardHelper);
                    PlainCard(pic, new Vector2(-0.009f, -0.005f), 0f, UIColors.CardHelper);
                    var lifted = Group("Lifted", pic, new Vector3(0.022f, 0.012f, -0.004f));
                    PlainCard(lifted, Vector2.zero, -14f, UIColors.CardTool);
                }
                else if (i == 1)
                {
                    // A helper card with its tool card stacked on it, like a lane on the table, and a check.
                    PlainCard(pic, new Vector2(0f, -0.012f), 0f, UIColors.Coral);
                    var onTop = Group("OnTop", pic, new Vector3(0f, 0.01f, -0.003f));
                    PlainCard(onTop, Vector2.zero, 0f, UIColors.CardTool);
                    Cyl("CheckDisc", pic, new Vector3(0.028f, 0.03f, -0.008f), 0.012f, 0.003f, Mat("Yay", UIColors.Yay), new Vector3(90, 0, 0));
                    var mark = Group("Check", pic, new Vector3(0.028f, 0.03f, -0.0095f));
                    B.Icon("HowTo_Check", mark, CheckMark, 0.011f, UIColors.Cream, Vector2.zero);
                }
                else
                {
                    // A row of stars, like the star track on the table.
                    for (int k = 0; k < 3; k++)
                    {
                        var s = Group("Star" + k, pic, new Vector3((k - 1) * 0.04f, k == 1 ? 0.006f : -0.002f, 0));
                        float r = k == 1 ? 0.022f : 0.017f; // meshes are saved by name: one per size
                        MeshKit.Show("Star", s, MeshKit.Extrude($"HowTo_Star_{r * 1000:0}", Star(r), 0.005f), sun);
                    }
                }
                Text3D("Text", card, steps[i], andika, 0.019f, UIColors.Ink, new Vector3(0, -0.05f, -0.0045f), new Vector2(0.15f, 0.05f));
            }

            Text3D("Tip", panel, "Wrong card? A helper at the window will give you a hint!", andika, 0.017f, UIColors.Ink,
                new Vector3(0, -0.095f, -0.0005f), new Vector2(0.56f, 0.028f));

            var back = BackButton(panel, new Vector2(0, -0.15f), new Vector2(0.2f, 0.075f));
            On(back, controller.OnBack);
            return panel;
        }

        private static List<Vector2> Star(float radius)
        {
            var star = new List<Vector2>();
            for (int k = 0; k < 10; k++)
            {
                float a = Mathf.PI * 0.5f + k * Mathf.PI / 5f;
                float r = k % 2 == 0 ? radius : radius * 0.43f;
                star.Add(new Vector2(Mathf.Cos(a) * r, Mathf.Sin(a) * r));
            }
            return star;
        }

        private static readonly Vector2[] CheckMark =
        {
            new(-0.6f, 0.05f), new(-0.15f, -0.5f), new(0.65f, 0.35f), new(0.45f, 0.52f), new(-0.15f, -0.15f), new(-0.42f, 0.22f),
        };
    }
}
