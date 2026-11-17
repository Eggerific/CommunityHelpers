using System.Collections;
using UnityEngine;

namespace CommunityHelpers
{
    /// <summary>
    /// Short controller buzzes. Picks the controller that is pointing at (or closest to aiming at) the target,
    /// which works for both ray and poke presses without needing to know which interactor fired.
    /// </summary>
    public static class ControllerHaptics
    {
        private static OVRCameraRig _rig;

        public static void Pulse(MonoBehaviour runner, Vector3 target, float amplitude = 0.3f, float duration = 0.08f)
        {
            if (runner == null || !runner.isActiveAndEnabled) return;
            var controller = PickController(target);
            if (controller == OVRInput.Controller.None) return;
            runner.StartCoroutine(Buzz(controller, amplitude, duration));
        }

        private static IEnumerator Buzz(OVRInput.Controller c, float amplitude, float duration)
        {
            OVRInput.SetControllerVibration(1f, amplitude, c);
            yield return new WaitForSecondsRealtime(duration);
            OVRInput.SetControllerVibration(0f, 0f, c);
        }

        private static OVRInput.Controller PickController(Vector3 target)
        {
            if (_rig == null) _rig = Object.FindFirstObjectByType<OVRCameraRig>();
            if (_rig == null) return OVRInput.Controller.None;

            float Score(Transform anchor)
            {
                if (anchor == null) return float.MaxValue;
                Vector3 to = target - anchor.position;
                // Small angle = pointing at it; small distance = poking it. Either should win.
                float angle = Vector3.Angle(anchor.forward, to);
                return Mathf.Min(angle, to.magnitude * 300f);
            }

            bool leftOk = OVRInput.IsControllerConnected(OVRInput.Controller.LTouch);
            bool rightOk = OVRInput.IsControllerConnected(OVRInput.Controller.RTouch);
            if (!leftOk && !rightOk) return OVRInput.Controller.None;
            if (!leftOk) return OVRInput.Controller.RTouch;
            if (!rightOk) return OVRInput.Controller.LTouch;
            return Score(_rig.leftControllerAnchor) < Score(_rig.rightControllerAnchor)
                ? OVRInput.Controller.LTouch
                : OVRInput.Controller.RTouch;
        }
    }
}
