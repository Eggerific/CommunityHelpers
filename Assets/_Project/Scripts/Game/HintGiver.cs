using System;
using System.Collections.Generic;
using UnityEngine;

namespace CommunityHelpers
{
    /// <summary>
    /// Hints for wrong cards (rule D6), given by the people outside the window:
    /// when a card lands on the wrong slot, the helper it belongs to walks up to the window, faces the
    /// player, and talks in a speech bubble while Robo points at them. The first wrong try for a card gets
    /// its clue; every try after that gets its helping hand (HintBook). They walk off when that card goes in
    /// its right slot, or after <see cref="hintSeconds"/>.
    /// Only the helpers in this game (3 in Quick play, 6 in Full) are outside. At the end of the round
    /// everyone turns to the window and cheers.
    /// Built by TownPeopleBuilder.
    /// </summary>
    public class HintGiver : MonoBehaviour
    {
        [Serializable]
        public class HelperWalker
        {
            [Tooltip("Matches the helper part of a card category, e.g. \"Doctor\" in \"Doctor/Tool\".")]
            public string helper;
            public TownWalker walker;
        }

        [SerializeField] private CardGame game;
        [SerializeField] private CardTable table;
        [SerializeField] private RoboController robo;
        [SerializeField] private HintBook hints;
        [SerializeField] private HelperWalker[] helpers = Array.Empty<HelperWalker>();
        [Tooltip("Everyday people who only stroll (and cheer at the end).")]
        [SerializeField] private TownWalker[] townsfolk = Array.Empty<TownWalker>();

        [Header("Where they stand to talk (this object's space)")]
        [SerializeField] private float talkZ = 4.55f;
        [Tooltip("They stop at their own x, kept inside this range so they're well inside the window.")]
        [SerializeField] private Vector2 talkX = new(-1.2f, 1.2f);
        [Tooltip("How long a hint stays up (after the helper reaches the window) if the card isn't placed.")]
        [SerializeField] private float hintSeconds = 10f;

        private readonly Dictionary<CardScript, int> _wrongTries = new();
        private TownWalker _talking;
        private CardScript _hintCard;
        private float _hintEnds = float.MaxValue;

        private void OnEnable()
        {
            if (game != null)
            {
                game.WrongCard += OnWrongCard;
                game.Finished += OnFinished;
            }
            CardScript.Placed += OnPlaced;
        }

        private void OnDisable()
        {
            if (game != null)
            {
                game.WrongCard -= OnWrongCard;
                game.Finished -= OnFinished;
            }
            CardScript.Placed -= OnPlaced;
        }

        private void Start()
        {
            // Only this game's helpers walk outside (CardTable has dealt by now).
            var inGame = new HashSet<string>();
            if (table != null)
                for (int i = 0; i < table.ActiveLaneCount; i++) inGame.Add(table.Lanes[i].helper);
            foreach (var h in helpers)
                if (h.walker != null) h.walker.gameObject.SetActive(table == null || inGame.Contains(h.helper));
        }

        private void Update()
        {
            if (_talking == null) return;
            if (!_talking.HasArrived) { _hintEnds = float.MaxValue; return; }
            if (_hintEnds == float.MaxValue) _hintEnds = Time.time + hintSeconds; // the clock starts once they're there
            if (Time.time >= _hintEnds) EndHint();
        }

        private void OnWrongCard(CardScript card, CardPlaceSlot slot)
        {
            var walker = WalkerFor(card);
            if (walker == null) return;

            _wrongTries.TryGetValue(card, out int tries);
            _wrongTries[card] = ++tries;
            string text = "...";
            AudioClip voice = null;
            if (hints != null) hints.Get(card.cardCategory, tries, out text, out voice);

            // Someone else is talking about a different card: they step back.
            if (_talking != null && _talking != walker) _talking.StopTalking();

            var p = walker.transform.localPosition;
            float x = Mathf.Clamp(p.x, talkX.x, talkX.y);
            // The window's center post lines up with x = 0 from the seat: stand beside it, not behind it.
            if (Mathf.Abs(x) < 0.5f) x = x < 0f ? -0.5f : 0.5f;
            var spot = new Vector3(x, p.y, talkZ);
            walker.ComeTalk(spot, text, voice);
            _talking = walker;
            _hintCard = card;
            _hintEnds = float.MaxValue;
            if (robo != null) robo.PointAt(walker.Head);
        }

        /// <summary>A card went in its right slot: its helper outside reacts (after any hint about it ends).</summary>
        private void OnPlaced(CardScript card, CardPlaceSlot slot)
        {
            if (card == _hintCard) EndHint(happy: true); // the helper at the window nods, then walks on
            var walker = WalkerFor(card);
            if (walker != null) walker.React();          // skipped if they're the one at the window
        }

        private void EndHint(bool happy = false)
        {
            if (_talking != null) _talking.StopTalking(happy);
            if (robo != null) robo.StopPointing();
            _talking = null;
            _hintCard = null;
            _hintEnds = float.MaxValue;
        }

        private void OnFinished()
        {
            EndHint();
            foreach (var h in helpers)
                if (h.walker != null && h.walker.isActiveAndEnabled) h.walker.Cheer();
            foreach (var w in townsfolk)
                if (w != null && w.isActiveAndEnabled) w.Cheer();
        }

        private TownWalker WalkerFor(CardScript card)
        {
            if (card == null || string.IsNullOrEmpty(card.cardCategory)) return null;
            int slash = card.cardCategory.IndexOf('/');
            string helper = slash >= 0 ? card.cardCategory.Substring(0, slash) : card.cardCategory;
            foreach (var h in helpers)
                if (h.helper == helper && h.walker != null && h.walker.isActiveAndEnabled) return h.walker;
            return null;
        }

#if UNITY_EDITOR
        /// <summary>Called by TownPeopleBuilder.</summary>
        public void EditorSetup(CardGame game, CardTable table, RoboController robo, HintBook hints,
            HelperWalker[] helpers, TownWalker[] townsfolk, float talkZ)
        {
            this.game = game;
            this.table = table;
            this.robo = robo;
            this.hints = hints;
            this.helpers = helpers;
            this.townsfolk = townsfolk;
            this.talkZ = talkZ;
        }

        private void LateUpdate()
        {
            // Editor shortcut: H = a hint for a random card in the game (as if it was dropped on a wrong slot).
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb == null || !kb.hKey.wasPressedThisFrame || table == null) return;
            var lane = table.Lanes[UnityEngine.Random.Range(0, Mathf.Max(1, table.ActiveLaneCount))];
            var card = UnityEngine.Random.value < 0.5f ? lane.toolCard : lane.workplaceCard;
            if (card != null && !card.IsLocked) OnWrongCard(card, null);
        }
#endif
    }
}
