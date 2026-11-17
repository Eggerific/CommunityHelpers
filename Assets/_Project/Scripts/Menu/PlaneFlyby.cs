using UnityEngine;

namespace CommunityHelpers
{
    /// <summary>
    /// Flies a little propeller plane back and forth across the sky forever, on a racetrack loop:
    /// left to right on the near pass, a banked U-turn, right to left on a slightly farther pass, and a turn back.
    /// The straight passes cross the whole window; the U-turns happen past the ends, out of view and clear of buildings.
    /// Local space: x = left/right across the window, z = farther away. The plane's nose is its local +X.
    /// </summary>
    public class PlaneFlyby : MonoBehaviour
    {
        [SerializeField] private Transform plane;
        [SerializeField] private Transform propeller;
        [Tooltip("Straight passes run from -halfWidth to +halfWidth (meters). The U-turns happen past the ends, out of view.")]
        [SerializeField] private float halfWidth = 30f;
        [Tooltip("Radius of the U-turns; the far pass is 2x this farther away.")]
        [SerializeField] private float turnRadius = 3f;
        [SerializeField] private float speed = 4.5f;              // m/s
        [SerializeField] private float bankDegrees = 25f;         // lean into the turns
        [SerializeField] private float bobHeight = 0.4f;
        [SerializeField] private float propellerSpeed = 1500f;    // degrees per second
        [SerializeField, Range(0f, 1f)] private float startAt = 0.1f; // where on the loop to begin

        private float _s, _baseY;

        private float Straight => 2f * halfWidth;
        private float Arc => Mathf.PI * turnRadius;
        private float LoopLength => 2f * Straight + 2f * Arc;

        private void Start()
        {
            _baseY = plane.localPosition.y;
            _s = startAt * LoopLength;
        }

        private void Update()
        {
            float t = Time.time;
            _s = (_s + speed * Time.deltaTime) % LoopLength;

            float W = halfWidth, R = turnRadius, s = _s;
            Vector3 pos;
            float yaw, bank = 0f;

            if (s < Straight)                               // near pass, left -> right
            {
                pos = new Vector3(-W + s, 0f, 0f);
                yaw = 0f;
            }
            else if (s < Straight + Arc)                    // turn at the right end
            {
                float a = (s - Straight) / R;
                pos = new Vector3(W + R * Mathf.Sin(a), 0f, R - R * Mathf.Cos(a));
                yaw = -a * Mathf.Rad2Deg;
                bank = Mathf.Sin(a) * bankDegrees;
            }
            else if (s < 2f * Straight + Arc)               // far pass, right -> left
            {
                pos = new Vector3(W - (s - Straight - Arc), 0f, 2f * R);
                yaw = 180f;
            }
            else                                            // turn at the left end
            {
                float a = (s - 2f * Straight - Arc) / R;
                pos = new Vector3(-W - R * Mathf.Sin(a), 0f, R + R * Mathf.Cos(a));
                yaw = 180f - a * Mathf.Rad2Deg;
                bank = Mathf.Sin(a) * bankDegrees;
            }

            pos.y = _baseY + Mathf.Sin(t * 0.8f) * bobHeight;
            plane.localPosition = pos;
            // Yaw to face the direction of travel, then roll (around the nose) into the turn, plus a gentle pitch bob.
            plane.localRotation = Quaternion.Euler(0f, yaw, 0f) * Quaternion.Euler(bank, 0f, Mathf.Cos(t * 0.8f) * 3f);

            if (propeller != null) propeller.Rotate(propellerSpeed * Time.deltaTime, 0f, 0f, Space.Self);
        }
    }
}
