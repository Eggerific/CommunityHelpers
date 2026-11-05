using System.Collections;
using UnityEngine;

namespace CommunityHelpers
{
    /// <summary>
    /// Moves the whole room so the player's seat is under their head and the table is in front of them.
    /// Runs when tracking first starts, when the headset is put on, and whenever the Quest recenters.
    ///
    /// Floor height: uses the real floor when the headset reports a believable eye height.
    /// If it doesn't (eye-level tracking, a bad Link floor, or the headset not on yet), the room floor
    /// is placed <see cref="assumedEyeHeight"/> below the eyes instead, so you never end up inside the floor.
    /// </summary>
    public class SeatAnchor : MonoBehaviour
    {
        [Tooltip("CenterEyeAnchor of the OVR camera rig.")]
        [SerializeField] private Transform head;
        [Tooltip("TrackingSpace of the OVR camera rig (its Y is the real floor when tracking origin is Floor Level).")]
        [SerializeField] private Transform trackingSpace;

        [Header("Floor")]
        [Tooltip("Off: always use assumedEyeHeight (good for testing). On: use the real floor when it looks right.")]
        [SerializeField] private bool useRealFloor = true;
        [Tooltip("Eye heights outside this range are treated as a bad floor reading.")]
        [SerializeField] private Vector2 believableEyeHeight = new(0.7f, 2.0f);
        [Tooltip("Seated eye height used when the real floor can't be trusted.")]
        [SerializeField] private float assumedEyeHeight = 1.15f;

        [Header("Table height follows the player's eye height")]
        [SerializeField] private Transform table;
        [Tooltip("How far below the eyes the table top sits.")]
        [SerializeField] private float tableTopBelowEye = 0.45f;
        [SerializeField] private float minTableTop = 0.45f;
        [SerializeField] private float maxTableTop = 0.80f;

        [Header("Debug")]
        [SerializeField] private bool logPlacement = true;

        private bool _subscribed;

        public float EyeHeight { get; private set; }

        private IEnumerator Start()
        {
            // Wait (up to 10 s) for the headset to report a real pose before the first placement.
            float t = 0f;
            while (t < 10f && !HasTrackedPose())
            {
                t += Time.deltaTime;
                yield return null;
            }
            yield return null; // one more frame so the pose is fully applied
            Recenter();

            OVRManager.TrackingAcquired += Recenter;
            OVRManager.HMDMounted += Recenter;
            if (OVRManager.display != null) OVRManager.display.RecenteredPose += Recenter;
            _subscribed = true;
        }

        private void OnDestroy()
        {
            if (!_subscribed) return;
            OVRManager.TrackingAcquired -= Recenter;
            OVRManager.HMDMounted -= Recenter;
            if (OVRManager.display != null) OVRManager.display.RecenteredPose -= Recenter;
        }

        private bool HasTrackedPose() =>
            head != null && OVRManager.isHmdPresent && head.localPosition.sqrMagnitude > 0.0001f;

        /// <summary>Re-run the placement from the current head pose.</summary>
        [ContextMenu("Recenter")]
        public void Recenter()
        {
            if (head == null) return;

            float realFloorY = trackingSpace != null ? trackingSpace.position.y : 0f;
            float realEye = head.position.y - realFloorY;
            bool believable = realEye >= believableEyeHeight.x && realEye <= believableEyeHeight.y;

            float floorY;
            if (useRealFloor && believable)
            {
                floorY = realFloorY;
                EyeHeight = realEye;
            }
            else
            {
                floorY = head.position.y - assumedEyeHeight;
                EyeHeight = assumedEyeHeight;
            }

            Vector3 fwd = head.forward;
            fwd.y = 0f;
            if (fwd.sqrMagnitude < 0.0001f) fwd = Vector3.forward;

            transform.SetPositionAndRotation(
                new Vector3(head.position.x, floorY, head.position.z),
                Quaternion.LookRotation(fwd.normalized, Vector3.up));

            if (table != null)
            {
                float top = Mathf.Clamp(EyeHeight - tableTopBelowEye, minTableTop, maxTableTop);
                Vector3 p = table.localPosition;
                table.localPosition = new Vector3(p.x, top, p.z);
            }

            if (logPlacement)
                Debug.Log($"[SeatAnchor] Placed room. Reported eye height {realEye:F2} m " +
                          $"({(useRealFloor && believable ? "using real floor" : $"not believable or disabled, assuming {assumedEyeHeight:F2} m")}). " +
                          $"HMD present: {OVRManager.isHmdPresent}.");
        }

#if UNITY_EDITOR
        private void Update()
        {
            // Editor shortcut: press R to re-place the room while testing over Link.
            if (UnityEngine.InputSystem.Keyboard.current != null &&
                UnityEngine.InputSystem.Keyboard.current.rKey.wasPressedThisFrame)
                Recenter();
        }
#endif
    }
}
