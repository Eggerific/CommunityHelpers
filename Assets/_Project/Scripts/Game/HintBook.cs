using System;
using System.Collections.Generic;
using UnityEngine;

namespace CommunityHelpers
{
    /// <summary>
    /// The hint lines for every card, edited in the Inspector (Assets/_Project/Data/HintBook.asset).
    /// Each card has a clue (first wrong try) and a helping hand (every wrong try after that), each with an
    /// optional voice-over clip. The first drafts come from Docs/Hints.md.
    /// </summary>
    [CreateAssetMenu(menuName = "Community Helpers/Hint Book", fileName = "HintBook")]
    public class HintBook : ScriptableObject
    {
        [Serializable]
        public class Entry
        {
            [Tooltip("The card's category, e.g. \"Doctor/Tool\" (CardScript.cardCategory).")]
            public string card;
            [TextArea(1, 3)] public string clue;
            [TextArea(1, 3)] public string helpingHand;
            [Tooltip("Spoken clue (a teammate is recording these). Empty = no sound.")]
            public AudioClip clueVoice;
            [Tooltip("Spoken helping hand. Empty = no sound.")]
            public AudioClip helpingHandVoice;
        }

        [Tooltip("Off: the bubble just shows \"...\" (the helper is talking; the voice-over will say the hint). " +
                 "On: the bubble shows the hint text.")]
        public bool showText;

        [Tooltip("What the bubble says when Show Text is off, or when a card has no line.")]
        public string placeholder = "...";

        public List<Entry> entries = new();

        public Entry Find(string card) => entries.Find(e => e.card == card);

        /// <summary>The line for this card and try (1 = clue, 2+ = helping hand).</summary>
        public void Get(string card, int attempt, out string text, out AudioClip voice)
        {
            var e = Find(card);
            bool clue = attempt <= 1;
            voice = e == null ? null : clue ? e.clueVoice : e.helpingHandVoice;
            string line = e == null ? null : clue ? e.clue : e.helpingHand;
            text = showText && !string.IsNullOrWhiteSpace(line) ? line : placeholder;
        }
    }
}
