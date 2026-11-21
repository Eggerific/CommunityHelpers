using System;
using System.Collections;
using Oculus.Interaction;
using UnityEngine;

namespace CommunityHelpers
{
    /// <summary>
    /// Added at game start (by CardGame) to every card the player has to place. It does two things
    /// (cards drawn from the deck stay face down until the player turns them over; no auto-flip, team call Nov 17):
    /// - Wrong card: when a card lands on a slot that doesn't take it, it raises <see cref="DroppedOnWrongSlot"/>.
    /// - Send back: <see cref="SendTo"/> flies the card to a spot (CardGame sends wrong cards back to the hand, rule D6).
    /// Snapping into the right slot is still CardScript's job.
    /// </summary>
    [RequireComponent(typeof(CardScript))]
    public class PlayCard : MonoBehaviour
    {
        private const float SettleTimeout = 1.5f; // stop waiting for a dropped card to settle after this
        private const float SettleSpeed = 0.05f;  // m/s
        private const float FlyTime = 0.6f;
        private const float FlyArc = 0.08f;       // m above the straight line, at the middle of the flight

        /// <summary>The card came to rest on a slot that doesn't accept it (and on no slot that does).</summary>
        public event Action<PlayCard, CardPlaceSlot> DroppedOnWrongSlot;

        public CardScript Card { get; private set; }
        public bool IsHeld => _grabbable != null && _grabbable.SelectingPointsCount > 0;
        public bool IsFlying => _fly != null;

        private Grabbable _grabbable;
        private Rigidbody _body;
        private BoxCollider _box;
        private Coroutine _watch, _fly;

        private void Awake()
        {
            Card = GetComponent<CardScript>();
            _grabbable = GetComponent<Grabbable>();
            _body = GetComponent<Rigidbody>();
            _box = GetComponent<BoxCollider>();
        }

        private void OnEnable()
        {
            if (_grabbable != null) _grabbable.WhenPointerEventRaised += OnPointer;
        }

        private void OnDisable()
        {
            if (_grabbable != null) _grabbable.WhenPointerEventRaised -= OnPointer;
        }

        private void OnPointer(PointerEvent evt)
        {
            switch (evt.Type)
            {
                case PointerEventType.Select:
                    OnGrab();
                    break;
                case PointerEventType.Unselect:
                case PointerEventType.Cancel:
                    // The Grabbable has already updated its count (and the body's kinematic state) by now.
                    if (!IsHeld) OnRelease();
                    break;
            }
        }

        /// <summary>True when the card's face points down (as it's dealt on the deck).</summary>
        public bool IsFaceDown => Vector3.Dot(transform.up, Vector3.up) < 0f;

        private void OnGrab()
        {
            if (_watch != null) { StopCoroutine(_watch); _watch = null; }
            if (_fly != null) EndFlight();
        }

        private void OnRelease()
        {
            if (Card.IsLocked) return;

            // The Grabbable puts the body back the way it was when grabbed. If it was grabbed mid-flight
            // (kinematic), that would leave it hanging in the air.
            if (_body != null) _body.isKinematic = false;

            _watch = StartCoroutine(WatchLanding());
        }

        // ------------------------------------------------------------------ Wrong slot

        private IEnumerator WatchLanding()
        {
            // Wait for the card to land (it may have been dropped from above the slot).
            for (float t = 0f; t < SettleTimeout; t += Time.deltaTime)
            {
                if (Card.IsLocked || IsHeld) { _watch = null; yield break; }
                bool still = _body == null || _body.isKinematic ||
                             _body.linearVelocity.sqrMagnitude < SettleSpeed * SettleSpeed;
                if (t > 0.15f && still) break;
                yield return null;
            }
            yield return null; // give CardScript a frame to snap the card in if a slot took it
            _watch = null;
            if (Card.IsLocked || IsHeld || Card.slotPull != null) yield break;

            var wrong = FindWrongSlot();
            if (wrong != null) DroppedOnWrongSlot?.Invoke(this, wrong);
        }

        /// <summary>The slot under the card that doesn't take it, or null if there's none (or one that does).</summary>
        private CardPlaceSlot FindWrongSlot()
        {
            if (_box == null) return null;
            Vector3 center = transform.TransformPoint(_box.center);
            Vector3 half = Vector3.Scale(_box.size, transform.lossyScale) * 0.5f;
            var hits = Physics.OverlapBox(center, half, transform.rotation, ~0, QueryTriggerInteraction.Collide);

            CardPlaceSlot wrong = null;
            float best = float.MaxValue;
            foreach (var hit in hits)
            {
                var slot = hit.GetComponent<CardPlaceSlot>();
                if (slot == null) continue;
                if (slot.Accepts(Card)) return null; // close enough to its own slot; CardScript will take it
                float d = (slot.transform.position - center).sqrMagnitude;
                if (d < best) { best = d; wrong = slot; }
            }
            return wrong;
        }

        // ------------------------------------------------------------------ Fly to a spot

        /// <summary>Flies the card to <paramref name="target"/> in a little arc, then lets physics have it again.</summary>
        public void SendTo(Pose target)
        {
            if (Card.IsLocked) return;
            if (_watch != null) { StopCoroutine(_watch); _watch = null; }
            if (_fly != null) StopCoroutine(_fly);
            _fly = StartCoroutine(Fly(target));
        }

        private IEnumerator Fly(Pose target)
        {
            // No collider while flying: the card can't be grabbed mid-air, and it can't snap into a slot it
            // passes over on the way.
            if (_box != null) _box.enabled = false;
            if (_body != null)
            {
                if (!_body.isKinematic)
                {
                    _body.linearVelocity = Vector3.zero;
                    _body.angularVelocity = Vector3.zero;
                }
                _body.isKinematic = true;
            }

            Vector3 p0 = transform.position;
            Quaternion r0 = transform.rotation;
            for (float t = 0f; t < FlyTime; t += Time.deltaTime)
            {
                float e = Mathf.SmoothStep(0f, 1f, t / FlyTime);
                transform.position = Vector3.Lerp(p0, target.position, e) + Vector3.up * (FlyArc * Mathf.Sin(e * Mathf.PI));
                transform.rotation = Quaternion.Slerp(r0, target.rotation, e);
                yield return null;
            }
            transform.SetPositionAndRotation(target.position, target.rotation);
            EndFlight();
        }

        private void EndFlight()
        {
            if (_fly != null) { StopCoroutine(_fly); _fly = null; }
            if (_box != null) _box.enabled = true;
            if (_body != null) _body.isKinematic = false;
            Card.ResetPull();
        }

#if UNITY_EDITOR
        /// <summary>Editor testing: act as if the player just let go of the card where it is.</summary>
        public void DebugDrop()
        {
            if (_body != null) _body.isKinematic = false;
            if (_watch != null) StopCoroutine(_watch);
            _watch = StartCoroutine(WatchLanding());
        }
#endif
    }
}
