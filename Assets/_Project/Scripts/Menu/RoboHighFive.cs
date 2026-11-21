using System;
using System.Collections;
using UnityEngine;

namespace CommunityHelpers
{
    /// <summary>
    /// "Give Robo a high five!" before the game starts. Robo hops to the front of the table (within reach of
    /// the seat), turns to the player and holds up his hand, which glows. When either of the player's hands or
    /// controllers touches it, Robo jumps for joy and <see cref="Begin"/>'s callback runs (the menu starts the
    /// game). Doubles as a warm-up for reaching out and touching things, which is how cards are picked up.
    /// Editor: Space counts as a high five.
    /// </summary>
    public class RoboHighFive : MonoBehaviour
    {
        [SerializeField] private RoboController robo;
        [Tooltip("Robo's right hand (the one he holds up).")]
        [SerializeField] private Transform hand;
        [Tooltip("Glowing ball around the hand while he waits (hidden otherwise).")]
        [SerializeField] private Transform glow;

        [Header("Where he waits (parent space, e.g. the table)")]
        [SerializeField] private Vector3 spot;
        [SerializeField] private float spotYaw;
        [SerializeField] private float hopTime = 0.6f;
        [SerializeField] private float hopHeight = 0.08f;

        [Tooltip("How close (m) a hand or controller has to get to Robo's hand. Generous for small kids.")]
        [SerializeField] private float touchRadius = 0.14f;

        public bool IsWaiting { get; private set; }

        private Vector3 _homePos;
        private Quaternion _homeRot;
        private Vector3 _glowScale = Vector3.one;
        private Action _onHighFive;
        private Coroutine _routine;
        private OVRCameraRig _rig;

        private void Awake()
        {
            _homePos = transform.localPosition;
            _homeRot = transform.localRotation;
            if (glow != null)
            {
                _glowScale = glow.localScale;
                glow.gameObject.SetActive(false);
            }
        }

        /// <summary>Hop forward, hold up a hand, and call <paramref name="onHighFive"/> once the player touches it.</summary>
        public void Begin(Action onHighFive)
        {
            _onHighFive = onHighFive;
            Restart(Wait());
        }

        /// <summary>Hand down and hop back home (e.g. the player pressed Back).</summary>
        public void Cancel()
        {
            if (!IsWaiting) return;
            Restart(GoHome());
        }

        private void Restart(IEnumerator next)
        {
            if (_routine != null) StopCoroutine(_routine);
            _routine = StartCoroutine(next);
        }

        private IEnumerator Wait()
        {
            IsWaiting = true;
            yield return Hop(spot, Quaternion.Euler(0f, spotYaw, 0f));
            if (robo != null) robo.HoldUpHand(true);
            yield return new WaitForSeconds(0.25f); // hand on its way up
            if (glow != null) glow.gameObject.SetActive(true);

            while (!Touched())
            {
                if (glow != null) glow.localScale = _glowScale * (1f + 0.15f * Mathf.Sin(Time.time * 6f));
                yield return null;
            }

            // High five!
            IsWaiting = false;
            if (glow != null) glow.gameObject.SetActive(false);
            if (robo != null)
            {
                robo.HoldUpHand(false);
                robo.DoCheer(RoboController.Cheer.Jump);
            }
            yield return new WaitForSeconds(0.5f);
            _onHighFive?.Invoke();
        }

        private IEnumerator GoHome()
        {
            IsWaiting = false;
            if (glow != null) glow.gameObject.SetActive(false);
            if (robo != null) robo.HoldUpHand(false);
            yield return new WaitForSeconds(0.2f);
            yield return Hop(_homePos, _homeRot);
        }

        private IEnumerator Hop(Vector3 to, Quaternion toRot)
        {
            Vector3 from = transform.localPosition;
            Quaternion fromRot = transform.localRotation;
            for (float t = 0f; t < hopTime; t += Time.deltaTime)
            {
                float k = Mathf.SmoothStep(0f, 1f, t / hopTime);
                transform.localPosition = Vector3.Lerp(from, to, k) + Vector3.up * (hopHeight * 4f * k * (1f - k));
                transform.localRotation = Quaternion.Slerp(fromRot, toRot, k);
                yield return null;
            }
            transform.localPosition = to;
            transform.localRotation = toRot;
        }

        private bool Touched()
        {
#if UNITY_EDITOR
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null && kb.spaceKey.wasPressedThisFrame) return true;
#endif
            if (hand == null) return false;
            if (_rig == null) _rig = FindFirstObjectByType<OVRCameraRig>();
            if (_rig == null) return false;
            Vector3 h = hand.position;
            float r2 = touchRadius * touchRadius;
            // Hands (hand tracking) and controllers: whichever the player is using.
            foreach (var anchor in new[] { _rig.leftHandAnchor, _rig.rightHandAnchor, _rig.leftControllerAnchor, _rig.rightControllerAnchor })
                if (anchor != null && anchor.gameObject.activeInHierarchy && (anchor.position - h).sqrMagnitude < r2) return true;
            return false;
        }

#if UNITY_EDITOR
        /// <summary>Called by RoboHatsBuilder.</summary>
        public void EditorSetup(RoboController robo, Transform hand, Transform glow, Vector3 spot, float spotYaw)
        {
            this.robo = robo;
            this.hand = hand;
            this.glow = glow;
            this.spot = spot;
            this.spotYaw = spotYaw;
        }
#endif
    }
}
