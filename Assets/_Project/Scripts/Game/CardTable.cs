using System;
using System.Collections.Generic;
using UnityEngine;

namespace CommunityHelpers
{
    /// <summary>
    /// Lays out the game table when the Game scene starts (rules D2 and D7 in the design doc):
    /// the helpers for this mode (6 in Full game, 3 in Quick play) go face up in their lanes, the tool and
    /// workplace cards (and any AI cards) are shuffled and stacked face down on the draw deck (no starting hand is dealt;
    /// drawn cards go on the hand mat in front of the player).
    ///
    /// Each lane is one helper's column: the helper card nearest the player, then the Tool, Workplace and
    /// AI slots, each one a little farther away and a little higher, so every card's name stays visible.
    /// Built by GameTableBuilder (Community Helpers > Update Game Table).
    /// </summary>
    public class CardTable : MonoBehaviour
    {
        [Serializable]
        public class Lane
        {
            public string helper;
            public Transform root;
            public CardPlaceSlot helperSlot, toolSlot, workplaceSlot, aiSlot;
            public CardScript helperCard, toolCard, workplaceCard;
        }

        [Tooltip("In the design doc's order. Quick play uses the first three.")]
        [SerializeField] private Lane[] lanes = Array.Empty<Lane>();
        [SerializeField] private float laneSpacing = 0.12f;

        [Header("Hand (face up, in front of the player)")]
        [Tooltip("Where drawn cards go. Nothing is dealt there at the start (team call, Nov 16): every tool and " +
                 "workplace card starts in the deck. Raise the counts below to deal a starting hand.")]
        [SerializeField] private Transform handArea;
        [SerializeField] private float handSpacing = 0.092f;
        [Tooltip("How many card spots fit across the hand mat (a wrong card is sent back to a free one).")]
        [SerializeField] private int handSpots = 7;
        [SerializeField] private int fullGameHand = 0;
        [SerializeField] private int quickPlayHand = 0;

        [Header("Draw deck (face down)")]
        [SerializeField] private Transform deckSpot;

        [Tooltip("AI Suggestion cards, shuffled into the draw deck with the tool and workplace cards. " +
                 "Empty until the AI card art exists.")]
        [SerializeField] private List<CardScript> aiCards = new();

        [Tooltip("Parent for cards that aren't in a slot.")]
        [SerializeField] private Transform looseCards;

        [Tooltip("0 = a new shuffle every game. Any other number always deals the same way (handy for testing).")]
        [SerializeField] private int shuffleSeed;

#if UNITY_EDITOR
        /// <summary>Called by the editor table builder.</summary>
        public void EditorSetup(Lane[] lanes, float laneSpacing, Transform handArea, float handSpacing,
            Transform deckSpot, Transform looseCards)
        {
            this.lanes = lanes;
            this.laneSpacing = laneSpacing;
            this.handArea = handArea;
            this.handSpacing = handSpacing;
            this.deckSpot = deckSpot;
            this.looseCards = looseCards;
        }
#endif

        public IReadOnlyList<Lane> Lanes => lanes;
        public int ActiveLaneCount { get; private set; }
        public IReadOnlyList<CardScript> AiCards => aiCards;
        public int HandSpotCount => Mathf.Max(1, handSpots);

        /// <summary>
        /// Spot <paramref name="i"/> on the hand mat (world space) for a face-up card. Spot 0 is the middle;
        /// the rest alternate right and left of it, working outward.
        /// </summary>
        public Pose HandSpot(int i, CardScript card)
        {
            i = Mathf.Clamp(i, 0, HandSpotCount - 1);
            int step = (i + 1) / 2;
            float x = (i % 2 == 1 ? step : -step) * handSpacing;
            float y = (card != null ? HalfThickness(card) : 0.002f) + 0.001f;
            return new Pose(handArea.TransformPoint(new Vector3(x, y, 0f)), handArea.rotation);
        }

        private void Awake()
        {
            int seed = shuffleSeed != 0 ? shuffleSeed : Environment.TickCount;
            Deal(GameSession.HelperCount, seed, lockHelpers: true);
        }

        /// <summary>
        /// Lays out the table for <paramref name="helperCount"/> helpers. The editor calls this too (with
        /// lockHelpers off) to show a Full game layout in the scene.
        /// </summary>
        public void Deal(int helperCount, int seed, bool lockHelpers)
        {
            int n = Mathf.Clamp(helperCount, 1, lanes.Length);
            ActiveLaneCount = n;
            var pool = new List<CardScript>();

            for (int i = 0; i < lanes.Length; i++)
            {
                var lane = lanes[i];
                bool active = i < n;
                lane.root.gameObject.SetActive(active);
                foreach (var card in new[] { lane.helperCard, lane.toolCard, lane.workplaceCard })
                    if (card != null) card.gameObject.SetActive(active);
                if (!active) continue;

                // Active lanes sit side by side, centered on the seat.
                var p = lane.root.localPosition;
                lane.root.localPosition = new Vector3((i - (n - 1) * 0.5f) * laneSpacing, p.y, p.z);

                if (lane.helperCard != null)
                {
                    if (lockHelpers) lane.helperCard.PlaceInSlot(lane.helperSlot);
                    else PutAt(lane.helperCard, lane.helperSlot.transform, Vector3.zero, faceDown: false, parent: lane.helperSlot.transform);
                }
                if (lane.toolCard != null) pool.Add(lane.toolCard);
                if (lane.workplaceCard != null) pool.Add(lane.workplaceCard);
            }

            foreach (var card in aiCards)
            {
                if (card == null) continue;
                card.gameObject.SetActive(true);
                pool.Add(card);
            }

            Shuffle(pool, new System.Random(seed));

            int handSize = Mathf.Min(n == lanes.Length ? fullGameHand : quickPlayHand, pool.Count);
            for (int i = 0; i < handSize; i++)
            {
                float x = (i - (handSize - 1) * 0.5f) * handSpacing;
                PutAt(pool[i], handArea, new Vector3(x, HalfThickness(pool[i]), 0f), faceDown: false, parent: looseCards);
            }

            float y = 0f;
            for (int i = handSize; i < pool.Count; i++)
            {
                float half = HalfThickness(pool[i]);
                PutAt(pool[i], deckSpot, new Vector3(0f, y + half, 0f), faceDown: true, parent: looseCards);
                y += half * 2f + 0.0005f; // a hair of space so the stacked bodies don't start overlapping
            }
        }

        private static void PutAt(CardScript card, Transform spot, Vector3 localOffset, bool faceDown, Transform parent)
        {
            var t = card.transform;
            t.SetParent(parent, true);
            t.position = spot.TransformPoint(localOffset + Vector3.up * 0.001f);
            t.rotation = spot.rotation * (faceDown ? Quaternion.Euler(0f, 0f, 180f) : Quaternion.identity);
        }

        private static float HalfThickness(CardScript card)
        {
            var col = card.GetComponent<BoxCollider>();
            return col != null ? col.size.y * 0.5f * card.transform.lossyScale.y : 0.002f;
        }

        private static void Shuffle<T>(IList<T> list, System.Random rng)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }
}
