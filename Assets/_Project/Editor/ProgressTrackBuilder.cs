using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.Events;
using static CommunityHelpers.EditorTools.BuildKit;
using B = CommunityHelpers.EditorTools.MenuBoardBuilder;

namespace CommunityHelpers.EditorTools
{
    /// <summary>
    /// Progress and end-of-round pieces on the Game table (GameSceneController). Called by CardTableBuilder.
    /// Table space: origin = table-top surface, -z = toward the player.
    ///
    ///          [ You did it! ]          end sign: pops up at the end, behind the track
    ///     [ * * ]   [ * * ]    ...      star track: one segment past the far end of each lane
    ///     | lane |  | lane |   ...
    ///  (home)  discard   hand   deck    small Home button in the left corner while playing;
    ///          [Play again] [Home]      big buttons rise out of the table at the end
    /// </summary>
    public static class ProgressTrackBuilder
    {
        public const float SegmentLength = 0.06f;
        private const float SegmentDepth = 0.004f;
        private const float SocketSpacing = 0.036f, StarSize = 0.016f;
        private const float TableHalfWidth = 0.9f;

        /// <param name="matTop">Height of the lane mats' top surface.</param>
        /// <param name="trackZ">Center line of the star track.</param>
        /// <param name="handZ">Center line of the hand mat (the end buttons rise there).</param>
        public static GameSceneController Build(Transform root, CardTable.Lane[] lanes, float laneSpacing,
            float matTop, float trackZ, float handZ, RoboController robo)
        {
            var controller = root.gameObject.AddComponent<GameSceneController>();

            var tracks = new GameSceneController.LaneTrack[lanes.Length];
            for (int i = 0; i < lanes.Length; i++)
                tracks[i] = BuildSegment(lanes[i].root, laneSpacing, matTop, trackZ);

            var home = BuildHomeButton(root, controller, matTop, handZ);
            var endSign = BuildEndSign(root, matTop, trackZ, out var points, out var stars);
            var endButtons = BuildEndButtons(root, controller, matTop, handZ);

            controller.EditorSetup(robo, tracks, SocketSpacing, home, endSign, points, stars, endButtons);
            EditorUtility.SetDirty(controller);
            return controller;
        }

        // ------------------------------------------------------------------ Star track

        private static GameSceneController.LaneTrack BuildSegment(Transform laneRoot, float laneSpacing,
            float matTop, float trackZ)
        {
            var view = new GameSceneController.LaneTrack();
            var seg = Group("Track", laneRoot, new Vector3(0, matTop, trackZ));
            float width = laneSpacing - 0.008f;
            FlatSlab("Strip", seg, width, SegmentLength, SegmentDepth, Mat("TrackStrip", UIColors.Roof));

            string[] kinds = { "Tool", "Workplace", "AI" };
            var empty = UIColors.Muted;
            for (int k = 0; k < 3; k++)
            {
                var socket = Group("Socket_" + kinds[k], seg, new Vector3((k - 1) * SocketSpacing, SegmentDepth, 0));
                FlatStar("Empty", socket, StarSize, empty, 0f);
                // The gold star that flies in from the slot. Sits on top of the empty one.
                view.sockets[k] = socket;
                view.stars[k] = FlatStar("Star", socket, StarSize * 1.15f, UIColors.Sun, 0.0045f);
                view.stars[k].gameObject.SetActive(false);
            }
            // Nothing pops up when a helper's stars are all in (team call, Nov 17): the full row says it.
            return view;
        }

        /// <summary>A star lying flat (points away from the player), extruded upward. Returns its holder.</summary>
        private static Transform FlatStar(string name, Transform parent, float size, Color32 color, float lift)
        {
            // Rotating 90 degrees about x lays the icon's plane on the table and turns its extrusion (-z) upward.
            var holder = Group(name, parent, new Vector3(0, lift, 0), new Vector3(90f, 0f, 0f));
            B.Icon($"Star_{size * 1000:0}mm", holder, StarShape(), size, color, Vector2.zero);
            return holder;
        }

        // ------------------------------------------------------------------ Buttons

        /// <summary>Small Home button in the near-left corner of the table, within reach but away from the cards.</summary>
        private static Transform BuildHomeButton(Transform root, GameSceneController controller, float matTop, float handZ)
        {
            var pos = new Vector3(-TableHalfWidth + 0.2f, matTop + 0.03f, handZ);
            var holder = TableButtonHolder("HomeButton", root, pos);
            var btn = B.MakeButton("Btn_Home", holder, Vector2.zero, new Vector2(0.1f, 0.08f), UIColors.Mist, out var c);
            B.Icon("HouseIcon_Ink", c, House, 0.03f, UIColors.Ink, Vector2.zero);
            UnityEventTools.AddPersistentListener(btn.onPressed, new UnityAction(controller.OnBackToMenu));
            return holder;
        }

        /// <summary>Play again and Home, side by side over the hand mat. Hidden until the end; then they rise up.</summary>
        private static Transform BuildEndButtons(Transform root, GameSceneController controller, float matTop, float handZ)
        {
            var semi = FontSetup.Load(FontSetup.FredokaSemiBold);
            var group = Group("EndButtons", root, new Vector3(0, matTop, handZ));
            var size = new Vector2(0.22f, 0.1f);
            const float x = 0.13f, y = 0.035f;

            var againHolder = TableButtonHolder("PlayAgain", group, new Vector3(-x, y, 0), root.InverseTransformPoint(group.position));
            var again = B.MakeButton("Btn_PlayAgain", againHolder, Vector2.zero, size, UIColors.YayButton, out var a);
            B.Icon("PlayAgainIcon", a, MenuPanelsBuilder.PlayTriangle, 0.03f, UIColors.Cream, new Vector2(-0.075f, 0f));
            B.Label(a, "Play again", semi, 0.034f, UIColors.Cream, new Vector2(0.02f, 0f), 0.15f);
            UnityEventTools.AddPersistentListener(again.onPressed, new UnityAction(controller.OnPlayAgain));

            var homeHolder = TableButtonHolder("Home", group, new Vector3(x, y, 0), root.InverseTransformPoint(group.position));
            var home = B.MakeButton("Btn_HomeBig", homeHolder, Vector2.zero, size, UIColors.Sky, out var h);
            B.Icon("HouseIcon_Cream", h, House, 0.032f, UIColors.Cream, new Vector2(-0.06f, 0.002f));
            B.Label(h, "Home", semi, 0.034f, UIColors.Cream, new Vector2(0.025f, 0f), 0.12f);
            UnityEventTools.AddPersistentListener(home.onPressed, new UnityAction(controller.OnBackToMenu));

            group.gameObject.SetActive(false);
            return group;
        }

        /// <summary>
        /// A holder for a 3D button on the table: tipped back 60 degrees (its face points up and toward the
        /// player, easy to poke from the seat) and turned toward the seat.
        /// </summary>
        /// <param name="parentOffset">Where the parent sits in table space (for working out the turn).</param>
        private static Transform TableButtonHolder(string name, Transform parent, Vector3 localPos, Vector3 parentOffset = default)
        {
            float yaw = B.YawTowardSeat(parentOffset + localPos, MainMenuSceneBuilder.TableCenterZ);
            return Group(name, parent, localPos, new Vector3(60f, yaw, 0f));
        }

        // ------------------------------------------------------------------ End sign

        /// <summary>"You did it!" on a little wooden sign on two posts, just behind the star track. Pops up at the end.</summary>
        private static Transform BuildEndSign(Transform root, float matTop, float trackZ, out TMPro.TMP_Text points, out Transform[] stars)
        {
            var bold = FontSetup.Load(FontSetup.FredokaBold);
            var semi = FontSetup.Load(FontSetup.FredokaSemiBold);
            var wood = Mat("WoodDark", UIColors.WoodDark);

            var sign = Group("EndSign", root, new Vector3(0, matTop, trackZ + 0.1f));
            var size = new Vector2(0.46f, 0.19f);
            const float bottom = 0.05f; // a little off the table, so the stars on the track stay in view
            const float tilt = 12f;     // leaned back a little, toward the player's eyes
            var board = Group("Board", sign, new Vector3(0, bottom + size.y * 0.5f, 0), new Vector3(tilt, 0, 0));
            MeshKit.Show("Frame", board, MeshKit.RoundedSlab("EndSign_Frame", size.x + 0.03f, size.y + 0.03f, 0.015f, 0.03f), wood);
            MeshKit.Show("Panel", board, MeshKit.RoundedSlab("EndSign_Panel", size.x, size.y, 0.003f, 0.02f),
                Mat("Cream", UIColors.Cream), new Vector3(0, 0, -0.015f));
            var face = Group("Face", board, new Vector3(0, 0, -0.018f));

            stars = new Transform[3];
            (float x, float y, float s)[] spots = { (-0.08f, 0.1f, 0.028f), (0f, 0.115f, 0.04f), (0.08f, 0.1f, 0.028f) };
            for (int i = 0; i < 3; i++)
            {
                var st = Group("Star_" + i, face, new Vector3(spots[i].x, spots[i].y, -0.002f));
                B.Icon($"Star_{spots[i].s * 1000:0}mm", st, StarShape(), spots[i].s, UIColors.Sun, Vector2.zero);
                stars[i] = st;
            }
            B.Label(face, "You did it!", bold, 0.06f, UIColors.Ink, new Vector2(0, 0.015f), size.x - 0.04f);
            points = B.Label(face, "You got 12 stars!", semi, 0.032f, UIColors.YayButton, new Vector2(0, -0.05f), size.x - 0.04f);

            // Two posts from the table up to the board.
            for (int side = -1; side <= 1; side += 2)
                Box("Post", sign, new Vector3(side * size.x * 0.35f, bottom * 0.5f + 0.01f, 0.012f), new Vector3(0.012f, bottom + 0.02f, 0.012f), wood);

            sign.gameObject.SetActive(false);
            return sign;
        }

        // ------------------------------------------------------------------ Shapes and pieces

        /// <summary>Flat rounded slab lying on the table: bottom at y = 0 of <paramref name="parent"/>, top at depth.</summary>
        private static void FlatSlab(string name, Transform parent, float width, float length, float depth, Material mat)
        {
            var mesh = MeshKit.RoundedSlab($"Track_{width * 1000:0}x{length * 1000:0}x{depth * 10000:0}", width, length, depth, 0.012f);
            MeshKit.Show(name, parent, mesh, mat, Vector3.zero, new Vector3(90f, 0f, 0f));
        }

        // Icon meshes are saved by name (Icon_<name>.asset), so every size and shape needs its own name, or
        // building one overwrites another (e.g. the main menu's PlayIcon).

        /// <summary>Five-pointed star, outer radius 1.</summary>
        private static Vector2[] StarShape()
        {
            var pts = new Vector2[10];
            for (int i = 0; i < 10; i++)
            {
                float a = Mathf.PI * 0.5f + i * Mathf.PI / 5f;
                float r = i % 2 == 0 ? 1f : 0.45f;
                pts[i] = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
            }
            return pts;
        }

        /// <summary>House: square body with a pointed roof, about 1.3 x 1.15 units.</summary>
        private static readonly Vector2[] House =
        {
            new(-0.5f, -0.5f), new(0.5f, -0.5f), new(0.5f, 0.1f), new(0.65f, 0.1f), new(0f, 0.65f), new(-0.65f, 0.1f), new(-0.5f, 0.1f),
        };
    }
}
