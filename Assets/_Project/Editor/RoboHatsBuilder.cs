using UnityEditor;
using UnityEngine;
using static CommunityHelpers.EditorTools.BuildKit;

namespace CommunityHelpers.EditorTools
{
    /// <summary>
    /// Robo's dress-up hats (RoboDressUp) and the pre-game high five (RoboHighFive).
    /// Hats are built in Robo's head space (y up, front toward -Z, the top of the head at y = 0.155), in
    /// GameSession.RoboHat order: fire helmet, police cap, mail cap, doctor's head mirror. The same builder
    /// makes the little hats on the "Dress up Robo!" buttons.
    /// </summary>
    public static class RoboHatsBuilder
    {
        public static readonly string[] HatNames = { "Firefighter", "Police", "Mail carrier", "Doctor" };
        private const float HeadTop = 0.155f; // RoboBuilder: head height

        // Where Robo waits for the high five on the menu table (table space): in front of the easel's right
        // edge, about 0.4 m from the seat, so a seated child can reach his raised hand.
        private static readonly Vector3 HighFiveSpot = new(0.3f, 0f, -0.42f);

        /// <summary>Adds (or rebuilds) the hats on Robo's head and the RoboDressUp that switches them.</summary>
        /// <param name="wearSessionHat">Game scene: put on the hat picked in the menu when the scene starts.</param>
        public static RoboDressUp AddHats(Transform robo, bool wearSessionHat)
        {
            var head = Find(robo, "Head");
            if (head == null)
            {
                Debug.LogError("[RoboHatsBuilder] Robo has no Head.");
                return null;
            }
            var old = head.Find("Hats");
            if (old != null) Object.DestroyImmediate(old.gameObject);
            var group = Group("Hats", head);

            var hats = new Transform[HatNames.Length];
            for (int i = 0; i < hats.Length; i++)
            {
                hats[i] = BuildHat(i, group);
                hats[i].gameObject.SetActive(false);
            }

            var dress = robo.GetComponent<RoboDressUp>();
            if (dress == null) dress = robo.gameObject.AddComponent<RoboDressUp>();
            dress.EditorSetup(robo.GetComponent<RoboController>(), hats, wearSessionHat);
            EditorUtility.SetDirty(dress);
            return dress;
        }

        /// <summary>Adds (or rebuilds) the high-five glow on Robo's right hand and the RoboHighFive that runs it.</summary>
        public static RoboHighFive AddHighFive(Transform robo)
        {
            var arm = Find(robo, "Arm_R");
            var hand = arm != null ? arm.Find("Hand") : null;
            if (hand == null)
            {
                Debug.LogError("[RoboHatsBuilder] Robo has no right hand (Arm_R/Hand).");
                return null;
            }
            var old = arm.Find("HighFiveGlow");
            if (old != null) Object.DestroyImmediate(old.gameObject);
            // On the arm (not the hand, which is a scaled sphere), around the hand: "touch here!"
            var glow = Sphere("HighFiveGlow", arm, hand.localPosition, Vector3.one * 0.075f, Unlit("HighFiveGlow", UIColors.Sun)).transform;
            glow.gameObject.SetActive(false);

            var hf = robo.GetComponent<RoboHighFive>();
            if (hf == null) hf = robo.gameObject.AddComponent<RoboHighFive>();
            float yaw = MenuBoardBuilder.YawTowardSeat(HighFiveSpot, MainMenuSceneBuilder.TableCenterZ);
            hf.EditorSetup(robo.GetComponent<RoboController>(), hand, glow, HighFiveSpot, yaw);
            EditorUtility.SetDirty(hf);
            return hf;
        }

        // ------------------------------------------------------------------ Hats

        /// <summary>Builds hat <paramref name="kind"/> (GameSession.RoboHat order) under <paramref name="parent"/>, in head space.</summary>
        public static Transform BuildHat(int kind, Transform parent)
        {
            var hat = Group("Hat_" + HatNames[kind].Replace(" ", ""), parent);
            var ink = Mat("Ink", UIColors.Ink);
            var sun = Mat("Sun", UIColors.Sun, 0.4f);
            const float y = HeadTop;
            switch (kind)
            {
                case 0: // Fire helmet: red dome, wide brim (longer at the back), gold shield on the front
                {
                    var red = Mat("Prop_Red", UIColors.FireRed, 0.4f);
                    Sphere("Dome", hat, new Vector3(0, y + 0.01f, 0.005f), new Vector3(0.2f, 0.12f, 0.175f), red);
                    var brim = Cyl("Brim", hat, new Vector3(0, y + 0.004f, 0.012f), 0.118f, 0.012f, red);
                    brim.transform.localScale = Vector3.Scale(brim.transform.localScale, new Vector3(1f, 1f, 1.15f));
                    Box("Shield", hat, new Vector3(0, y + 0.05f, -0.088f), new Vector3(0.055f, 0.05f, 0.008f), sun, new Vector3(15f, 0, 0));
                    break;
                }
                case 1: // Police cap: navy crown with a flat top, black band and visor, gold badge
                {
                    var navy = Mat("PoliceNavy", UIColors.Roof, 0.3f);
                    Cyl("Band", hat, new Vector3(0, y + 0.009f, 0), 0.101f, 0.018f, ink);
                    Cyl("Crown", hat, new Vector3(0, y + 0.032f, 0), 0.1f, 0.03f, navy);
                    Cyl("Top", hat, new Vector3(0, y + 0.052f, 0), 0.112f, 0.014f, navy);
                    Box("Visor", hat, new Vector3(0, y + 0.004f, -0.105f), new Vector3(0.13f, 0.008f, 0.06f), ink, new Vector3(-15f, 0, 0));
                    Box("Badge", hat, new Vector3(0, y + 0.034f, -0.101f), new Vector3(0.028f, 0.032f, 0.006f), sun);
                    break;
                }
                case 2: // Mail carrier cap: blue ball cap with a little cream envelope on the front
                {
                    var blue = Mat("MailBlue", UIColors.Sky, 0.3f);
                    Sphere("Dome", hat, new Vector3(0, y + 0.004f, 0), new Vector3(0.19f, 0.1f, 0.165f), blue);
                    Box("Bill", hat, new Vector3(0, y + 0.006f, -0.11f), new Vector3(0.12f, 0.008f, 0.075f), blue, new Vector3(-8f, 0, 0));
                    Box("Envelope", hat, new Vector3(0, y + 0.03f, -0.08f), new Vector3(0.04f, 0.026f, 0.006f), Mat("Cream", UIColors.Cream), new Vector3(20f, 0, 0));
                    break;
                }
                default: // Doctor's head mirror: white band around the head (above the face screen), round mirror on the front
                {
                    var white = Mat("Cream", UIColors.Cream);
                    const float by = 0.142f, bh = 0.018f, t = 0.008f;
                    Box("Band_Front", hat, new Vector3(0, by, -0.078f), new Vector3(0.206f, bh, t), white);
                    Box("Band_Back", hat, new Vector3(0, by, 0.078f), new Vector3(0.206f, bh, t), white);
                    Box("Band_Left", hat, new Vector3(-0.103f, by, 0), new Vector3(t, bh, 0.158f), white);
                    Box("Band_Right", hat, new Vector3(0.103f, by, 0), new Vector3(t, bh, 0.158f), white);
                    var mirror = Group("Mirror", hat, new Vector3(0.03f, by + 0.032f, -0.088f), new Vector3(-70f, 0, 0));
                    Cyl("Disc", mirror, Vector3.zero, 0.032f, 0.006f, Mat("Prop_Pole", UIColors.Mist));
                    Cyl("Hole", mirror, new Vector3(0, -0.0035f, 0), 0.01f, 0.002f, Mat("Sky", UIColors.Sky, 0.4f));
                    Box("Mount", hat, new Vector3(0.03f, by + 0.014f, -0.084f), new Vector3(0.012f, 0.022f, 0.008f), white);
                    break;
                }
            }
            return hat;
        }

        /// <summary>
        /// A small copy of hat <paramref name="kind"/> for a button face (front toward the player, turned a
        /// little so it reads as 3D). <paramref name="size"/> is about its width in meters.
        /// </summary>
        public static void ButtonHat(int kind, Transform content, Vector2 pos, float size)
        {
            float s = size / 0.24f; // hats are ~0.24 m wide on Robo
            var holder = Group("HatIcon", content, new Vector3(pos.x, pos.y, -0.02f), new Vector3(-20f, 25f, 0));
            holder.localScale = Vector3.one * s;
            var hat = BuildHat(kind, holder);
            hat.localPosition = new Vector3(0, -HeadTop - 0.03f, 0); // center the hat on the holder
        }

        private static Transform Find(Transform root, string name)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == name) return t;
            return null;
        }
    }
}
