using System.Collections;
using TMPro;
using UnityEngine;

namespace CommunityHelpers
{
    /// <summary>
    /// One person on the garden path outside the window (a Kenney Mini Character).
    /// - Strolls: picks a spot along the path, walks there, stands a while (sometimes turning to look in
    ///   the window), and picks another.
    /// - Talks: <see cref="ComeTalk"/> walks them to a spot by the window, turns them to the player, and
    ///   shows their speech bubble until <see cref="StopTalking"/>.
    /// Moves in its parent's space (the TownPeople group: x right, z away from the seat, y up). The model
    /// faces this object's +Z. Built by TownPeopleBuilder.
    /// </summary>
    public class TownWalker : MonoBehaviour
    {
        [SerializeField] private Animator animator;
        [Tooltip("Speech bubble (hidden until they talk). Turns to face the player.")]
        [SerializeField] private Transform bubble;
        [SerializeField] private TMP_Text bubbleText;
        [Tooltip("About head height: Robo points here.")]
        [SerializeField] private Transform head;

        [Header("Strolling")]
        [SerializeField] private Vector2 pathX = new(-7.5f, 7.5f);
        [SerializeField] private float laneZ = 5.2f;
        [SerializeField] private Vector2 walkSpeed = new(0.55f, 0.8f);
        [SerializeField] private Vector2 pause = new(1.5f, 5f);
        [Tooltip("Chance, at each stop, that they turn and look in the window.")]
        [SerializeField, Range(0, 1)] private float lookInChance = 0.35f;
        [SerializeField] private float turnSpeed = 300f; // degrees per second

        [Header("Talking")]
        [SerializeField] private float hurrySpeed = 1.6f; // helpers may come from off to the side of the window
        [Tooltip("About how far left/right (from the window's center line) they can be and still be seen from the seat.")]
        [SerializeField] private float visibleX = 3.4f;
        [Tooltip("Seconds between nods while the bubble is up.")]
        [SerializeField] private float nodEvery = 2.2f;

        // Animator state names (TownPeopleBuilder makes these states).
        public const string Idle = "Idle", Walk = "Walk", Gesture = "Gesture", Nod = "Nod";

        public Transform Head => head != null ? head : transform;
        public bool IsTalking { get; private set; }
        /// <summary>At the window with the bubble up.</summary>
        public bool HasArrived { get; private set; }

        private Coroutine _routine;
        private Vector3 _bubbleScale = Vector3.one;
        private string _state;
        private Camera _cam;
        private AudioSource _voice;

        private void Awake()
        {
            if (bubble != null)
            {
                _bubbleScale = bubble.localScale;
                bubble.gameObject.SetActive(false);
            }
            if (animator != null) animator.applyRootMotion = false;
        }

        private void OnEnable()
        {
            _cam = Camera.main;
            _routine = StartCoroutine(Stroll());
        }

        private void OnDisable()
        {
            _routine = null;
            IsTalking = HasArrived = false;
        }

        private void LateUpdate()
        {
            // The bubble always faces the player (only turning about the vertical).
            if (bubble == null || !bubble.gameObject.activeSelf || _cam == null) return;
            Vector3 away = bubble.position - _cam.transform.position;
            away.y = 0f;
            if (away.sqrMagnitude > 0.0001f) bubble.rotation = Quaternion.LookRotation(away, Vector3.up);
        }

        // ------------------------------------------------------------------ Strolling

        private IEnumerator Stroll()
        {
            // Spread out at the start: some stand a moment first.
            yield return new WaitForSeconds(Random.Range(0f, 2f));
            while (true)
            {
                float x = transform.localPosition.x;
                float target;
                do target = Random.Range(pathX.x, pathX.y); while (Mathf.Abs(target - x) < 1.2f);
                yield return WalkTo(new Vector3(target, 0f, laneZ), Random.Range(walkSpeed.x, walkSpeed.y));

                Play(Idle);
                if (Random.value < lookInChance) yield return TurnTo(180f); // face the window (toward the seat)
                yield return new WaitForSeconds(Random.Range(pause.x, pause.y));
            }
        }

        private IEnumerator WalkTo(Vector3 to, float speed)
        {
            Vector3 from = transform.localPosition;
            to.y = from.y;
            Vector3 d = to - from;
            if (d.sqrMagnitude < 0.0004f) yield break;
            yield return TurnTo(Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg);
            Play(Walk);
            if (animator != null) animator.speed = Mathf.Clamp(speed / 0.7f, 0.7f, 1.8f); // feet roughly match the ground
            while (true)
            {
                Vector3 p = transform.localPosition;
                Vector3 step = Vector3.MoveTowards(p, to, speed * Time.deltaTime);
                transform.localPosition = step;
                if ((step - to).sqrMagnitude < 0.0001f) break;
                yield return null;
            }
            if (animator != null) animator.speed = 1f;
        }

        private IEnumerator TurnTo(float yaw)
        {
            var target = Quaternion.Euler(0f, yaw, 0f);
            while (Quaternion.Angle(transform.localRotation, target) > 1f)
            {
                transform.localRotation = Quaternion.RotateTowards(transform.localRotation, target, turnSpeed * Time.deltaTime);
                yield return null;
            }
            transform.localRotation = target;
        }

        // ------------------------------------------------------------------ Talking

        /// <summary>
        /// Walk to <paramref name="spot"/> (parent space), face the player, gesture, and show the bubble with
        /// <paramref name="text"/>. Plays <paramref name="voice"/> on arrival if there is one.
        /// </summary>
        public void ComeTalk(Vector3 spot, string text, AudioClip voice)
        {
            if (bubbleText != null) bubbleText.text = text;
            if (IsTalking && HasArrived)
            {
                // Already at the window: just update what they're saying.
                StartCoroutine(CardGame.Pop(bubble, 0.15f, 0.25f));
                PlayVoice(voice);
                return;
            }
            Restart(Talk(spot, voice));
        }

        /// <param name="happy">The card they were helping with just went in: nod before walking on.</param>
        public void StopTalking(bool happy = false)
        {
            if (!IsTalking) return;
            HasArrived = false; // a new hint while the bubble is closing starts a fresh Talk, not an update
            Restart(Leave(happy));
        }

        private void Restart(IEnumerator next)
        {
            if (_routine != null) StopCoroutine(_routine);
            if (animator != null) animator.speed = 1f;
            _routine = StartCoroutine(next);
        }

        private IEnumerator Talk(Vector3 spot, AudioClip voice)
        {
            IsTalking = true;
            HasArrived = false;
            yield return WalkTo(spot, hurrySpeed);
            yield return TurnTo(180f); // face the player
            HasArrived = true;

            Play(Gesture);
            yield return ShowBubble(true);
            PlayVoice(voice);
            while (true)
            {
                yield return new WaitForSeconds(nodEvery);
                Play(Nod, restart: true);
            }
        }

        private IEnumerator Leave(bool happy)
        {
            yield return ShowBubble(false);
            IsTalking = HasArrived = false;
            if (happy)
            {
                yield return ReactRoutine(); // already facing the window: gesture, nod, then stroll on
                yield break;
            }
            Play(Idle);
            yield return new WaitForSeconds(0.4f);
            yield return Stroll();
        }

        private IEnumerator ShowBubble(bool show)
        {
            if (bubble == null) yield break;
            const float time = 0.25f;
            if (show) bubble.gameObject.SetActive(true);
            for (float t = 0f; t < time; t += Time.deltaTime)
            {
                float k = t / time;
                float s = show ? Mathf.SmoothStep(0f, 1f, k) * (1f + 0.2f * Mathf.Sin(k * Mathf.PI)) : 1f - Mathf.SmoothStep(0f, 1f, k);
                bubble.localScale = _bubbleScale * s;
                yield return null;
            }
            bubble.localScale = _bubbleScale;
            if (!show) bubble.gameObject.SetActive(false);
        }

        private void PlayVoice(AudioClip clip)
        {
            if (clip == null) return;
            if (_voice == null)
            {
                _voice = gameObject.AddComponent<AudioSource>();
                _voice.playOnAwake = false;
                _voice.spatialBlend = 0f; // heard clearly from the seat, wherever they stand
            }
            _voice.Stop();
            _voice.clip = clip;
            _voice.Play();
        }

        /// <summary>
        /// One of their cards just went in its right slot: they stop, turn to the window, gesture and nod.
        /// If they're out of view, they first hurry back to the window's edge so the player sees it.
        /// </summary>
        public void React()
        {
            if (IsTalking) return; // the hint giver handles the one at the window
            Restart(ReactRoutine());
        }

        private IEnumerator ReactRoutine()
        {
            Vector3 p = transform.localPosition;
            if (Mathf.Abs(p.x) > visibleX)
                yield return WalkTo(new Vector3(Mathf.Sign(p.x) * (visibleX - 0.4f), p.y, p.z), hurrySpeed);
            yield return TurnTo(180f);
            Play(Gesture);
            yield return new WaitForSeconds(1.0f);
            Play(Nod, restart: true);
            yield return new WaitForSeconds(1.3f);
            yield return Stroll();
        }

        /// <summary>Everyone at the end of the round: stop, face the player and nod.</summary>
        public void Cheer()
        {
            if (IsTalking) StopTalking();
            Restart(CheerRoutine());
        }

        private IEnumerator CheerRoutine()
        {
            yield return TurnTo(180f);
            Play(Gesture);
            yield return new WaitForSeconds(1.2f);
            Play(Nod, restart: true);
            yield return new WaitForSeconds(2f);
            yield return Stroll();
        }

        private void Play(string state, bool restart = false)
        {
            if (animator == null || (!restart && _state == state)) return;
            _state = state;
            animator.CrossFadeInFixedTime(state, 0.2f, 0, 0f);
        }

#if UNITY_EDITOR
        /// <summary>Called by TownPeopleBuilder.</summary>
        public void EditorSetup(Animator animator, Transform bubble, TMP_Text bubbleText, Transform head,
            Vector2 pathX, float laneZ)
        {
            this.animator = animator;
            this.bubble = bubble;
            this.bubbleText = bubbleText;
            this.head = head;
            this.pathX = pathX;
            this.laneZ = laneZ;
        }
#endif
    }
}
