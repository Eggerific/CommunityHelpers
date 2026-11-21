using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace CommunityHelpers
{
    /// <summary>
    /// The card game's rules (design doc decisions, and the team's Nov 16 calls):
    /// - Draw freely: no turns. Cards are picked up off the deck as the player likes (Solo, D3).
    /// - +1 point for every card placed in its helper's slot, with sparkles, a "+1" and a Robo cheer (no sound or
    ///   vibration yet; sounds can be assigned below once they exist).
    /// - A card dropped on the wrong slot flies back to the hand mat (D6). <see cref="WrongCard"/> is where
    ///   Robo's spoken hint will plug in.
    /// - The round ends when every card is placed (D10; no timer for now). GameSceneController then shows the
    ///   round's end: a "You did it!" sign and Play again / Home buttons on the table.
    /// AI Suggestion cards (D1) count once they exist: an AI card that some lane's AI slot takes is a card to
    /// place, like a tool. Cards meant for the discard pile aren't handled yet.
    /// Lives on the CardTable group; built and wired by CardTableBuilder / GameTableBuilder.
    /// </summary>
    public class CardGame : MonoBehaviour
    {
        public enum SlotKind { Tool = 0, Workplace = 1, AI = 2 }

        [SerializeField] private CardTable table;
        [SerializeField] private GameSceneController board;
        [SerializeField] private RoboController robo;

        [Header("Correct-card effects")]
        [SerializeField] private ParticleSystem sparkles;
        [SerializeField] private int sparkleCount = 28;
        [Tooltip("Inactive template for the floating \"+1\".")]
        [SerializeField] private TextMeshPro plusOneTemplate;
        [SerializeField] private float plusOneRise = 0.09f;
        [SerializeField] private float plusOneTime = 1.1f;

        [Header("Sounds (a teammate is making them; nothing plays until a clip is assigned)")]
        [SerializeField] private AudioClip correctSound;
        [SerializeField] private AudioClip laneDoneSound;
        [SerializeField] private AudioClip fanfareSound;
        [SerializeField] private AudioClip wrongSound;

        [Header("End of round")]
        [Tooltip("Pause after the last card before the end screen, so its own celebration plays first.")]
        [SerializeField] private float endDelay = 1.4f;

        /// <summary>Points so far and the points needed to finish.</summary>
        public event Action<int, int> ScoreChanged;
        /// <summary>A card landed on a slot that doesn't take it (it's already on its way back to the hand).
        /// Hook Robo's hint here.</summary>
        public event Action<CardScript, CardPlaceSlot> WrongCard;
        public event Action Finished;

        public int Score { get; private set; }
        public int Total => _goals.Count;
        public bool IsFinished { get; private set; }

        private class Goal
        {
            public PlayCard card;
            public int lane;
            public SlotKind kind;
            public CardPlaceSlot slot;
            public bool done;
        }

        private readonly List<Goal> _goals = new();
        private readonly Dictionary<CardScript, Goal> _byCard = new();
        private AudioSource _audio;
        private Camera _cam;

        private void Awake()
        {
            _audio = gameObject.AddComponent<AudioSource>();
            _audio.playOnAwake = false;
            _audio.spatialBlend = 0f;
            if (plusOneTemplate != null) plusOneTemplate.gameObject.SetActive(false);
        }

        private void OnEnable() => CardScript.Placed += OnPlaced;
        private void OnDisable() => CardScript.Placed -= OnPlaced;

        private void Start()
        {
            _cam = Camera.main;
            if (table == null) table = GetComponent<CardTable>();
            if (table == null)
            {
                Debug.LogError("[CardGame] No CardTable to play on.", this);
                enabled = false;
                return;
            }

            // CardTable has already dealt (in its Awake): the active lanes and the deck are set.
            int lanes = table.ActiveLaneCount;
            var laneHasAi = new bool[lanes];
            for (int i = 0; i < lanes; i++)
            {
                var lane = table.Lanes[i];
                AddGoal(lane.toolCard, i, SlotKind.Tool, lane.toolSlot);
                AddGoal(lane.workplaceCard, i, SlotKind.Workplace, lane.workplaceSlot);
            }
            foreach (var ai in table.AiCards)
            {
                if (ai == null) continue;
                for (int i = 0; i < lanes; i++)
                {
                    var slot = table.Lanes[i].aiSlot;
                    if (slot == null || !slot.Accepts(ai)) continue;
                    AddGoal(ai, i, SlotKind.AI, slot);
                    laneHasAi[i] = true;
                    break;
                }
            }

            if (board != null) board.SetUpProgress(lanes, laneHasAi, Total);
            ScoreChanged?.Invoke(Score, Total);
            Debug.Log($"[CardGame] {GameSession.Mode}: {lanes} helpers, {Total} cards to place.");
        }

        private void AddGoal(CardScript card, int lane, SlotKind kind, CardPlaceSlot slot)
        {
            if (card == null || slot == null) return;
            var play = card.GetComponent<PlayCard>();
            if (play == null) play = card.gameObject.AddComponent<PlayCard>();
            play.DroppedOnWrongSlot += OnWrongSlot;
            var goal = new Goal { card = play, lane = lane, kind = kind, slot = slot };
            _goals.Add(goal);
            _byCard[card] = goal;
        }

        // ------------------------------------------------------------------ Right card

        private void OnPlaced(CardScript card, CardPlaceSlot slot)
        {
            if (IsFinished || !_byCard.TryGetValue(card, out var goal) || goal.done) return;
            goal.done = true;
            Score++;

            bool laneDone = _goals.TrueForAll(g => g.lane != goal.lane || g.done);
            bool allDone = Score >= Total;

            Celebrate(card, slot);
            Play(laneDone ? laneDoneSound : correctSound);
            if (robo != null && !allDone) robo.DoCheer(laneDone ? RoboController.Cheer.Yes : RoboController.Cheer.ThumbsUp);

            if (board != null)
            {
                board.MarkPlaced(goal.lane, goal.kind, Score, Total, slot.transform.position);
            }
            ScoreChanged?.Invoke(Score, Total);

            if (allDone) StartCoroutine(Finish());
        }

        private void Celebrate(CardScript card, CardPlaceSlot slot)
        {
            Vector3 at = slot.transform.position;
            if (sparkles != null)
            {
                sparkles.transform.position = at + Vector3.up * 0.01f;
                sparkles.Emit(sparkleCount);
            }
            if (plusOneTemplate != null) StartCoroutine(FloatPlusOne(at + Vector3.up * 0.03f));
            var visual = card.transform.Find("Visual");
            if (visual != null) StartCoroutine(Pop(visual, 0.22f, 0.4f));
        }

        private IEnumerator FloatPlusOne(Vector3 start)
        {
            var label = Instantiate(plusOneTemplate, transform);
            label.gameObject.SetActive(true);
            var t = label.transform;
            Vector3 baseScale = t.localScale;
            for (float time = 0f; time < plusOneTime; time += Time.deltaTime)
            {
                float k = time / plusOneTime;
                t.position = start + Vector3.up * (plusOneRise * (1f - (1f - k) * (1f - k)));
                if (_cam != null) t.rotation = Quaternion.LookRotation(t.position - _cam.transform.position, Vector3.up);
                float pop = k < 0.15f ? Mathf.SmoothStep(0.4f, 1.15f, k / 0.15f) : Mathf.Lerp(1.15f, 1f, (k - 0.15f) / 0.2f);
                t.localScale = baseScale * pop;
                label.alpha = k < 0.6f ? 1f : 1f - (k - 0.6f) / 0.4f;
                yield return null;
            }
            Destroy(label.gameObject);
        }

        /// <summary>Grows <paramref name="t"/> by <paramref name="amount"/> and back, over <paramref name="time"/> seconds.</summary>
        public static IEnumerator Pop(Transform t, float amount, float time)
        {
            Vector3 s0 = t.localScale;
            for (float x = 0f; x < time; x += Time.deltaTime)
            {
                if (t == null) yield break;
                t.localScale = s0 * (1f + amount * Mathf.Sin(x / time * Mathf.PI));
                yield return null;
            }
            if (t != null) t.localScale = s0;
        }

        // ------------------------------------------------------------------ Wrong card

        private void OnWrongSlot(PlayCard card, CardPlaceSlot slot)
        {
            if (IsFinished) return;
            Play(wrongSound);
            var spot = table.HandSpot(FreeHandSpot(card.Card), card.Card);
            // Keep it the way up it was: a card the player hadn't turned over stays face down (no auto-flip).
            if (card.IsFaceDown) spot.rotation *= Quaternion.AngleAxis(180f, Vector3.forward);
            card.SendTo(spot);
            WrongCard?.Invoke(card.Card, slot);
        }

        /// <summary>The first hand-mat spot (middle first) with no other card on it.</summary>
        private int FreeHandSpot(CardScript card)
        {
            var box = card.GetComponent<BoxCollider>();
            Vector3 half = box != null
                ? Vector3.Scale(box.size, card.transform.lossyScale) * 0.4f
                : new Vector3(0.03f, 0.005f, 0.05f);
            half.y = 0.01f;
            for (int i = 0; i < table.HandSpotCount; i++)
            {
                var pose = table.HandSpot(i, card);
                bool taken = false;
                foreach (var hit in Physics.OverlapBox(pose.position, half, pose.rotation, ~0, QueryTriggerInteraction.Ignore))
                {
                    var other = hit.GetComponent<CardScript>();
                    if (other != null && other != card) { taken = true; break; }
                }
                if (!taken) return i;
            }
            return 0;
        }

        // ------------------------------------------------------------------ End of round

        private IEnumerator Finish()
        {
            IsFinished = true;
            yield return new WaitForSeconds(endDelay);

            Play(fanfareSound);
            if (robo != null) robo.DoCheer(RoboController.Cheer.Jump);
            var sign = FindFirstObjectByType<TitleSignAnimator>();
            if (sign != null) sign.Burst();
            if (sparkles != null)
            {
                foreach (var g in _goals)
                {
                    sparkles.transform.position = g.slot.transform.position + Vector3.up * 0.01f;
                    sparkles.Emit(sparkleCount / 2);
                }
            }
            if (board != null) board.ShowEnd(Score, Total);
            Debug.Log($"[CardGame] All {Total} cards placed. {Score} points.");
            Finished?.Invoke();
        }

        private void Play(AudioClip clip)
        {
            if (clip != null && _audio != null) _audio.PlayOneShot(clip);
        }

#if UNITY_EDITOR
        /// <summary>Called by the editor table builder.</summary>
        public void EditorSetup(CardTable table, GameSceneController board, RoboController robo,
            ParticleSystem sparkles, TextMeshPro plusOneTemplate)
        {
            this.table = table;
            this.board = board;
            this.robo = robo;
            this.sparkles = sparkles;
            this.plusOneTemplate = plusOneTemplate;
        }

        private void Update()
        {
            // Editor shortcuts for testing without the headset:
            // P = put the next card in its slot, O = drop a card on a wrong slot (it should fly back).
            // (Not W: the Meta XR Simulator moves with WASD.)
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb == null || IsFinished) return;
            if (kb.pKey.wasPressedThisFrame) DebugPlaceNext();
            if (kb.oKey.wasPressedThisFrame) DebugDropWrong();
        }

        private Goal NextOpen() => _goals.Find(g => !g.done && !g.card.Card.IsLocked && !g.card.IsFlying);

        private void DebugPlaceNext()
        {
            var g = NextOpen();
            if (g == null) return;
            g.card.Card.PlaceInSlot(g.slot); // the game's own placement doesn't raise Placed, so score it here
            OnPlaced(g.card.Card, g.slot);
        }

        private void DebugDropWrong()
        {
            var g = NextOpen();
            if (g == null) return;
            // Another lane's slot of the same kind (or this lane's other slot when there's only one lane).
            var lanes = table.Lanes;
            int other = table.ActiveLaneCount > 1 ? (g.lane + 1) % table.ActiveLaneCount : g.lane;
            var lane = lanes[other];
            var wrong = other != g.lane
                ? (g.kind == SlotKind.Workplace ? lane.workplaceSlot : lane.toolSlot)
                : lane.helperSlot;
            var t = g.card.transform;
            t.SetPositionAndRotation(wrong.transform.position + Vector3.up * 0.02f, wrong.transform.rotation);
            g.card.DebugDrop();
        }
#endif
    }
}
