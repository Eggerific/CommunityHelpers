using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using static CommunityHelpers.EditorTools.BuildKit;

namespace CommunityHelpers.EditorTools
{
    /// <summary>
    /// Builds Robo from rounded parts: a light gray shell, a dark face screen with big white eyes,
    /// antenna, ear bolts, a glowing heart on the chest, and arms on shoulder pivots.
    /// Every moving part has its own transform so RoboController can animate it.
    /// Robo space: x right, y up, front faces -Z (toward the player). Sizes in meters before RigScale.
    /// </summary>
    public static class RoboBuilder
    {
        private const float RigScale = 0.85f;   // overall size (about 36 cm tall)

        public static RoboController Build(Transform table, Vector3 tablePos, float yawTowardSeat)
        {
            var shell = Mat("RoboShell", UIColors.RoboShell, 0.45f);
            var joint = Mat("RoboJoint", UIColors.RoboJoint, 0.3f);
            var screen = Mat("RoboScreen", UIColors.Ink, 0.7f);
            var eyeWhite = Unlit("RoboEye", UIColors.RoboEye);
            var pupilMat = Mat("Ink", UIColors.Ink);
            var sky = Mat("Sky", UIColors.Sky, 0.4f);
            var sun = Mat("Sun", UIColors.Sun, 0.4f);
            var coral = Mat("Coral", UIColors.Coral);
            var heart = Unlit("RoboHeart", UIColors.Coral);

            var root = Group("Robo", table, tablePos, new Vector3(0, yawTowardSeat, 0));
            var rig = Group("Rig", root);
            rig.localScale = Vector3.one * RigScale;

            // ---- Feet and legs (don't move)
            for (int s = -1; s <= 1; s += 2)
            {
                Sphere($"Foot_{Side(s)}", rig, new Vector3(s * 0.038f, 0.015f, -0.008f), new Vector3(0.055f, 0.03f, 0.075f), joint);
                Cyl($"Leg_{Side(s)}", rig, new Vector3(s * 0.035f, 0.042f, 0f), 0.016f, 0.04f, joint);
            }

            // ---- Body (bottom of torso at y = 0.06)
            var body = Group("Body", rig, new Vector3(0, 0.06f, 0));
            Slab("Torso", body, 0.14f, 0.12f, 0.11f, 0.04f, shell, new Vector3(0, 0.06f, 0));
            Slab("ChestScreen", body, 0.075f, 0.05f, 0.006f, 0.014f, screen, new Vector3(0, 0.065f, -0.055f), centered: false);
            var heartGo = MeshKit.Show("Heart", body, HeartMesh(), heart, new Vector3(0, 0.066f, -0.0615f));
            Cyl("Neck", body, new Vector3(0, 0.128f, 0), 0.02f, 0.02f, joint);

            // ---- Arms (shoulder pivots; arms hang down -Y)
            Transform Arm(int s)
            {
                var pivot = Group($"Arm_{Side(s)}", body, new Vector3(s * 0.082f, 0.095f, 0));
                Sphere("Shoulder", pivot, Vector3.zero, Vector3.one * 0.036f, joint);
                Cyl("UpperArm", pivot, new Vector3(0, -0.037f, 0), 0.015f, 0.065f, shell);
                Sphere("Hand", pivot, new Vector3(0, -0.078f, 0), Vector3.one * 0.046f, joint);
                return pivot;
            }
            var armL = Arm(-1);
            var armR = Arm(1);

            // ---- Head (pivot at the top of the neck)
            var head = Group("Head", body, new Vector3(0, 0.135f, 0));
            const float hw = 0.2f, hh = 0.155f, hd = 0.15f;
            Slab("Shell", head, hw, hh, hd, 0.055f, shell, new Vector3(0, hh * 0.5f, 0));
            float faceZ = -hd * 0.5f;
            Slab("Face", head, 0.165f, 0.112f, 0.006f, 0.04f, screen, new Vector3(0, 0.076f, faceZ), centered: false);
            float eyeZ = faceZ - 0.006f;

            // Big eyes: white disc (scales to blink) with a pupil and a sparkle (the pupil moves to follow the player).
            Transform Eye(int s, out Transform pupil)
            {
                var eye = Group($"Eye_{Side(s)}", head, new Vector3(s * 0.042f, 0.086f, eyeZ));
                Cyl("White", eye, new Vector3(0, 0, -0.002f), 0.032f, 0.004f, eyeWhite, new Vector3(90, 0, 0));
                pupil = Group("Pupil", eye, new Vector3(0, -0.002f, -0.0045f));
                Cyl("Iris", pupil, Vector3.zero, 0.018f, 0.002f, pupilMat, new Vector3(90, 0, 0));
                Sphere("Sparkle", pupil, new Vector3(0.007f, 0.008f, -0.002f), Vector3.one * 0.009f, eyeWhite);
                return eye;
            }
            var eyeL = Eye(-1, out var pupilL);
            var eyeR = Eye(1, out var pupilR);

            // Smile and rosy cheeks.
            var smile = MeshKit.Show("Smile", head, SmileMesh(), Unlit("RoboSmile", UIColors.Sky), new Vector3(0, 0.034f, eyeZ));
            // Open "O" mouth for the "Wheee!" face when Robo is picked up (hidden until then).
            var mouthOh = MeshKit.Show("MouthOh", head, OhMesh(), Unlit("RoboSmile", UIColors.Sky), new Vector3(0, 0.032f, eyeZ));
            mouthOh.SetActive(false);
            for (int s = -1; s <= 1; s += 2)
                Cyl($"Cheek_{Side(s)}", head, new Vector3(s * 0.068f, 0.036f, eyeZ - 0.001f), 0.011f, 0.002f, coral, new Vector3(90, 0, 0));

            // Ear bolts.
            for (int s = -1; s <= 1; s += 2)
                Cyl($"Ear_{Side(s)}", head, new Vector3(s * (hw * 0.5f + 0.008f), 0.08f, 0), 0.024f, 0.02f, sky, new Vector3(0, 0, 90));

            // Antenna (pivot on top of the head).
            var antenna = Group("Antenna", head, new Vector3(0, hh, 0));
            Cyl("Base", antenna, new Vector3(0, 0.006f, 0), 0.014f, 0.012f, joint);
            Cyl("Stalk", antenna, new Vector3(0, 0.035f, 0), 0.0045f, 0.05f, joint);
            Sphere("Ball", antenna, new Vector3(0, 0.064f, 0), Vector3.one * 0.03f, sun);

            // ---- Pick up: kinematic body + collider + Meta ISDK grab (direct grab, and distance grab from the seat).
            var rb = root.gameObject.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;
            var box = root.gameObject.AddComponent<BoxCollider>();
            box.center = new Vector3(0, 0.185f, 0);
            box.size = new Vector3(0.24f, 0.38f, 0.17f);
            // The OVRComprehensiveInteractionRig grabs with HandGrabInteractor / DistanceHandGrabInteractor for both
            // hands AND controllers, so Robo needs the HandGrab interactables (plain GrabInteractable is never found).
            var grabbable = root.gameObject.AddComponent<Oculus.Interaction.Grabbable>();
            var grab = root.gameObject.AddComponent<Oculus.Interaction.HandGrab.HandGrabInteractable>();
            var distanceGrab = root.gameObject.AddComponent<Oculus.Interaction.HandGrab.DistanceHandGrabInteractable>();
            foreach (Object interactable in new Object[] { grab, distanceGrab })
            {
                var gso = new SerializedObject(interactable);
                gso.FindProperty("_rigidbody").objectReferenceValue = rb;
                gso.FindProperty("_pointableElement").objectReferenceValue = grabbable;
                // Pinch (trigger / finger pinch) and palm (grip / whole-hand grab) both pick Robo up.
                gso.FindProperty("_supportedGrabTypes").intValue = (int)Oculus.Interaction.Grab.GrabTypeFlags.All;
                gso.ApplyModifiedPropertiesWithoutUndo();
            }

            // ---- Controller
            var ctrl = root.gameObject.AddComponent<RoboController>();
            var so = new SerializedObject(ctrl);
            so.FindProperty("rig").objectReferenceValue = rig;
            so.FindProperty("body").objectReferenceValue = body;
            so.FindProperty("head").objectReferenceValue = head;
            so.FindProperty("armLeft").objectReferenceValue = armL;
            so.FindProperty("armRight").objectReferenceValue = armR;
            so.FindProperty("antenna").objectReferenceValue = antenna;
            so.FindProperty("eyeLeft").objectReferenceValue = eyeL;
            so.FindProperty("eyeRight").objectReferenceValue = eyeR;
            so.FindProperty("pupilLeft").objectReferenceValue = pupilL;
            so.FindProperty("pupilRight").objectReferenceValue = pupilR;
            so.FindProperty("heartLight").objectReferenceValue = heartGo.GetComponent<Renderer>();
            so.FindProperty("mouthSmile").objectReferenceValue = smile;
            so.FindProperty("mouthOh").objectReferenceValue = mouthOh;
            so.FindProperty("grabbable").objectReferenceValue = grabbable;
            so.ApplyModifiedPropertiesWithoutUndo();
            return ctrl;
        }

        private static string Side(int s) => s < 0 ? "L" : "R";

        /// <summary>Rounded slab. centered: the slab's middle is at pos; otherwise its back face sits at pos.z.</summary>
        private static void Slab(string name, Transform parent, float w, float h, float d, float r, Material mat,
            Vector3 pos, bool centered = true)
        {
            var mesh = MeshKit.RoundedSlab($"Robo_{name}", w, h, d, r);
            MeshKit.Show(name, parent, mesh, mat, centered ? pos + new Vector3(0, 0, d * 0.5f) : pos);
        }

        private static Mesh HeartMesh()
        {
            var pts = new List<Vector2>();
            for (int i = 0; i < 24; i++)
            {
                float t = i / 24f * Mathf.PI * 2f;
                // Classic heart curve, scaled to about 2.4 cm.
                float x = 16f * Mathf.Pow(Mathf.Sin(t), 3f);
                float y = 13f * Mathf.Cos(t) - 5f * Mathf.Cos(2 * t) - 2f * Mathf.Cos(3 * t) - Mathf.Cos(4 * t);
                pts.Add(new Vector2(x, y) * 0.00075f);
            }
            return MeshKit.Extrude("Robo_Heart", pts, 0.004f);
        }

        /// <summary>Oval "O" mouth, slightly taller than wide.</summary>
        private static Mesh OhMesh()
        {
            var pts = new List<Vector2>();
            for (int i = 0; i < 20; i++)
            {
                float a = i / 20f * Mathf.PI * 2f;
                pts.Add(new Vector2(Mathf.Cos(a) * 0.012f, Mathf.Sin(a) * 0.016f));
            }
            return MeshKit.Extrude("Robo_MouthOh", pts, 0.002f);
        }

        private static Mesh SmileMesh()
        {
            var pts = new List<Vector2>();
            const int n = 10;
            const float outer = 0.022f, inner = 0.015f;
            for (int i = 0; i <= n; i++)
            {
                float a = Mathf.Lerp(200f, 340f, i / (float)n) * Mathf.Deg2Rad;
                pts.Add(new Vector2(Mathf.Cos(a), Mathf.Sin(a) + 0.7f) * outer);
            }
            for (int i = n; i >= 0; i--)
            {
                float a = Mathf.Lerp(200f, 340f, i / (float)n) * Mathf.Deg2Rad;
                pts.Add(new Vector2(Mathf.Cos(a) * inner, (Mathf.Sin(a) + 0.7f * outer / inner) * inner));
            }
            return MeshKit.Extrude("Robo_Smile", pts, 0.002f);
        }
    }
}
