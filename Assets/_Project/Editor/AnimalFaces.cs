using UnityEngine;
using static CommunityHelpers.EditorTools.BuildKit;

namespace CommunityHelpers.EditorTools
{
    /// <summary>
    /// Simple, friendly 3D animal faces (Fox, Bear, Owl, Bunny) built from discs, triangles and dots,
    /// sitting on a cream disc. Placeholders until the team's animal art is ready.
    /// Faces point toward -Z (the player). r = head radius in meters (before any parent scale).
    /// </summary>
    public static class AnimalFaces
    {
        private static readonly Color32 FoxOrange = new(0xF0, 0x8A, 0x3C, 255);
        private static readonly Color32 BearBrown = new(0xA8, 0x72, 0x48, 255);
        private static readonly Color32 BearMuzzle = new(0xE3, 0xC3, 0x9D, 255);
        private static readonly Color32 OwlPurple = new(0x8E, 0x7B, 0xD1, 255);
        private static readonly Color32 BunnyGray = new(0xDD, 0xDA, 0xE6, 255);
        private static readonly Color32 Pink = new(0xF4, 0xA7, 0xB9, 255);

        public static Transform Build(Transform parent, GameSession.Buddy kind, Vector2 pos, float r)
        {
            var g = Group($"Face_{kind}", parent, new Vector3(pos.x, pos.y, 0));
            var ink = Mat("Ink", UIColors.Ink);
            var cream = Mat("Cream", UIColors.Cream);

            Disc("Rim", g, Vector2.zero, r * 1.18f, -0.0015f, 0.002f, cream);

            switch (kind)
            {
                case GameSession.Buddy.Fox:
                {
                    var fur = Mat("Animal_Fox", FoxOrange);
                    Ear(g, "EarL", new Vector2(-0.58f, 0.52f) * r, r, 20f, fur, cream);
                    Ear(g, "EarR", new Vector2(0.58f, 0.52f) * r, r, -20f, fur, cream);
                    Disc("Head", g, Vector2.zero, r, -0.004f, 0.003f, fur);
                    Disc("Muzzle", g, new Vector2(0, -0.32f) * r, r * 0.5f, -0.0065f, 0.002f, cream);
                    Eyes(g, r, 0.32f, 0.12f, ink);
                    Dot("Nose", g, new Vector2(0, -0.2f) * r, r * 0.2f, ink);
                    break;
                }
                case GameSession.Buddy.Bear:
                {
                    var fur = Mat("Animal_Bear", BearBrown);
                    var light = Mat("Animal_BearMuzzle", BearMuzzle);
                    for (int s = -1; s <= 1; s += 2)
                    {
                        Disc(s < 0 ? "EarL" : "EarR", g, new Vector2(s * 0.7f, 0.68f) * r, r * 0.36f, -0.0025f, 0.002f, fur);
                        Disc(s < 0 ? "EarInL" : "EarInR", g, new Vector2(s * 0.7f, 0.68f) * r, r * 0.2f, -0.0037f, 0.001f, light);
                    }
                    Disc("Head", g, Vector2.zero, r, -0.004f, 0.003f, fur);
                    Disc("Muzzle", g, new Vector2(0, -0.3f) * r, r * 0.45f, -0.0065f, 0.002f, light);
                    Eyes(g, r, 0.36f, 0.15f, ink);
                    Dot("Nose", g, new Vector2(0, -0.2f) * r, r * 0.22f, ink);
                    break;
                }
                case GameSession.Buddy.Owl:
                {
                    var feathers = Mat("Animal_Owl", OwlPurple);
                    Tuft(g, "TuftL", new Vector2(-0.55f, 0.72f) * r, r * 0.55f, 15f, feathers);
                    Tuft(g, "TuftR", new Vector2(0.55f, 0.72f) * r, r * 0.55f, -15f, feathers);
                    Disc("Head", g, Vector2.zero, r, -0.004f, 0.003f, feathers);
                    for (int s = -1; s <= 1; s += 2)
                    {
                        Disc(s < 0 ? "EyeRingL" : "EyeRingR", g, new Vector2(s * 0.38f, 0.1f) * r, r * 0.36f, -0.0065f, 0.002f, cream);
                        Dot(s < 0 ? "EyeL" : "EyeR", g, new Vector2(s * 0.38f, 0.1f) * r, r * 0.2f, ink);
                    }
                    var beak = MeshKit.Extrude($"Animal_Beak_{Mm(r)}",
                        new[] { new Vector2(-0.14f, 0f) * r, new Vector2(0.14f, 0f) * r, new Vector2(0f, -0.24f) * r }, 0.002f);
                    MeshKit.Show("Beak", g, beak, Mat("Sun", UIColors.Sun, 0.4f), new Vector3(0, -0.15f * r, -0.0065f));
                    break;
                }
                case GameSession.Buddy.Bunny:
                {
                    var fur = Mat("Animal_Bunny", BunnyGray);
                    var pink = Mat("Animal_Pink", Pink);
                    for (int s = -1; s <= 1; s += 2)
                    {
                        // Long ears: rounded slabs standing up behind the head.
                        var ear = MeshKit.RoundedSlab($"Animal_BunnyEar_{Mm(r)}", r * 0.42f, r * 1.25f, 0.002f, r * 0.2f);
                        MeshKit.Show(s < 0 ? "EarL" : "EarR", g, ear, fur, new Vector3(s * 0.36f * r, 1.0f * r, -0.001f), new Vector3(0, 0, -s * 10f));
                        var inner = MeshKit.RoundedSlab($"Animal_BunnyEarIn_{Mm(r)}", r * 0.2f, r * 0.9f, 0.001f, r * 0.1f);
                        MeshKit.Show(s < 0 ? "EarInL" : "EarInR", g, inner, pink, new Vector3(s * 0.36f * r, 1.0f * r, -0.0025f), new Vector3(0, 0, -s * 10f));
                    }
                    Disc("Head", g, Vector2.zero, r, -0.004f, 0.003f, fur);
                    Eyes(g, r, 0.34f, 0.12f, ink);
                    Dot("Nose", g, new Vector2(0, -0.15f) * r, r * 0.17f, pink);
                    for (int s = -1; s <= 1; s += 2)
                        Disc(s < 0 ? "CheekL" : "CheekR", g, new Vector2(s * 0.5f, -0.25f) * r, r * 0.15f, -0.006f, 0.001f, pink);
                    break;
                }
            }
            return g;
        }

        private static int Mm(float meters) => Mathf.RoundToInt(meters * 1000f);

        /// <summary>Flat disc facing the player; z is its center depth (negative = toward the player).</summary>
        private static void Disc(string name, Transform parent, Vector2 pos, float radius, float z, float thickness, Material mat)
            => Cyl(name, parent, new Vector3(pos.x, pos.y, z), radius, thickness, mat, new Vector3(90, 0, 0));

        /// <summary>Little flattened sphere on the front (eyes, noses).</summary>
        private static void Dot(string name, Transform parent, Vector2 pos, float diameter, Material mat)
        {
            Sphere(name, parent, new Vector3(pos.x, pos.y, -0.0075f), new Vector3(diameter, diameter, diameter * 0.5f), mat);
        }

        private static void Eyes(Transform g, float r, float x, float y, Material ink)
        {
            Dot("EyeL", g, new Vector2(-x, y) * r, r * 0.18f, ink);
            Dot("EyeR", g, new Vector2(x, y) * r, r * 0.18f, ink);
        }

        /// <summary>Pointed ear (triangle) behind the head, with a cream inner triangle.</summary>
        private static void Ear(Transform g, string name, Vector2 pos, float r, float tilt, Material fur, Material inner)
        {
            var outer = MeshKit.Extrude($"Animal_Ear_{Mm(r)}",
                new[] { new Vector2(-0.42f, 0f) * r, new Vector2(0.42f, 0f) * r, new Vector2(0f, 0.75f) * r }, 0.002f);
            MeshKit.Show(name, g, outer, fur, new Vector3(pos.x, pos.y, -0.001f), new Vector3(0, 0, tilt));
            var inside = MeshKit.Extrude($"Animal_EarIn_{Mm(r)}",
                new[] { new Vector2(-0.22f, 0.08f) * r, new Vector2(0.22f, 0.08f) * r, new Vector2(0f, 0.55f) * r }, 0.001f);
            MeshKit.Show(name + "In", g, inside, inner, new Vector3(pos.x, pos.y, -0.0025f), new Vector3(0, 0, tilt));
        }

        private static void Tuft(Transform g, string name, Vector2 pos, float size, float tilt, Material mat)
        {
            var tri = MeshKit.Extrude($"Animal_Tuft_{Mm(size)}",
                new[] { new Vector2(-0.4f, 0f) * size, new Vector2(0.4f, 0f) * size, new Vector2(0f, 0.8f) * size }, 0.002f);
            MeshKit.Show(name, g, tri, mat, new Vector3(pos.x, pos.y, -0.001f), new Vector3(0, 0, tilt));
        }
    }
}
