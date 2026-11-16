using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using static CommunityHelpers.EditorTools.BuildKit;
using Kind = CommunityHelpers.EditorTools.CardDesignsBuilder.Kind;

namespace CommunityHelpers.EditorTools
{
    /// <summary>
    /// Builds the card layout on the Game scene table (design doc rules D2, D5, D7) and wires CardTable,
    /// which deals the cards when the game starts.
    ///
    /// Table space: origin = table-top surface, -z = toward the player, table is 1.8 x 1.1 m with its near
    /// edge 0.25 m in front of the seat. Everything the player touches stays within ~40 cm of the near edge.
    ///
    ///   far   [ AI ]        one lane per helper, side by side (6, or 3 in Quick play):
    ///         [ WORKPLACE ]   the helper card nearest the player, then the Tool, Workplace and AI slots,
    ///         [ TOOL ]        each 4.5 cm farther and 1.5 mm higher, so every card's name stays visible.
    ///         [ helper ]    Each empty slot shows a colored tag in the strip its card will cover.
    ///
    ///   near  Discard (left)   |   hand mat for drawn cards (middle)   |   draw deck incl. AI cards, face down (right)
    /// </summary>
    public static class CardTableBuilder
    {
        private const string GroupName = "CardTable";
        private const string OldGroupName = "CardDesigns"; // the earlier "all 18 cards in two rows" layout

        private static float L => CardDesignsBuilder.CardLength;
        private static float TableNearZ => -MainMenuSceneBuilder.TableSize.y * 0.5f;

        // ---- Mats ----
        private const float MatThickness = 0.002f; // cards rest on top of the mats

        // ---- Hand: one row along the near edge ----
        private const float HandEdgeMargin = 0.012f;
        private const int HandSpots = 7;             // the mat's width; a hand can still grow past it
        private const float HandGap = 0.012f;
        private static float HandZ => TableNearZ + HandEdgeMargin + 0.008f + L * 0.5f;

        // ---- Helper lanes ----
        private static float LaneNearZ => HandZ + L * 0.5f + 0.008f + 0.012f; // helper card's near edge
        private const float LaneGap = 0.04f;
        private const float Peek = 0.045f;  // each slot sits this much farther than the one before
        private const float SlotLift = 0.001f;
        // Each slot sits only a hair above the one before (not a full card thickness), so a card never
        // floats when the slot under it is still empty. Snapped cards are kinematic, so overlapping is fine.
        private const float StackStep = 0.0015f;

        // ---- Piles beside the hand ----
        private const float PileX = 0.45f;

        private static readonly string[] SlotNames = { "Helper", "Tool", "Workplace", "AI" };

        public static void Build(Transform table)
        {
            var cards = CardDesignsBuilder.BuildAllPrefabs();
            if (cards.Count == 0)
            {
                Debug.LogError("[CardTableBuilder] No card prefabs were built; is Assets/CardS missing?");
                return;
            }

            foreach (var name in new[] { GroupName, OldGroupName })
            {
                var old = table.Find(name);
                if (old != null) Object.DestroyImmediate(old.gameObject);
            }
            EnsureTableCollider(table);

            var font = FontSetup.Load(FontSetup.FredokaSemiBold);
            float w = cards.Max(c => c.prefab.GetComponent<BoxCollider>().size.x);
            float t = cards.Max(c => c.prefab.GetComponent<BoxCollider>().size.y);
            float laneSpacing = w + LaneGap;
            float handSpacing = w + HandGap;

            var root = Group(GroupName, table);
            var cardTable = root.gameObject.AddComponent<CardTable>();
            var loose = Group("Cards", root);

            // ---- Lanes ----
            var lanesGroup = Group("Lanes", root);
            int helperCount = CardDesignsBuilder.Catalog.Length;
            var lanes = new CardTable.Lane[helperCount];
            for (int h = 0; h < helperCount; h++)
            {
                string helper = CardDesignsBuilder.Catalog[h].helper;
                float x = (h - (helperCount - 1) * 0.5f) * laneSpacing;
                var lane = new CardTable.Lane { helper = helper, root = Group("Lane_" + helper, lanesGroup, new Vector3(x, 0, 0)) };
                BuildLane(lane, w, t, font);

                foreach (var c in cards.Where(c => c.helperIndex == h))
                {
                    var go = (GameObject)PrefabUtility.InstantiatePrefab(c.prefab, loose);
                    var card = go.GetComponent<CardScript>();
                    if (c.kind == Kind.Helper) lane.helperCard = card;
                    else if (c.kind == Kind.Tool) lane.toolCard = card;
                    else lane.workplaceCard = card;
                }
                lanes[h] = lane;
            }

            // ---- Hand, draw deck, AI pile, discard ----
            float handWidth = HandSpots * handSpacing;
            var hand = Group("Hand", root, new Vector3(0, MatThickness, HandZ));
            PileMat(hand, handWidth + 0.01f, L + 0.016f, Mat("CardMat_Hand", UIColors.Mist));

            var deck = Group("DrawDeck", root, new Vector3(PileX, MatThickness, HandZ));
            PileMat(deck, w + 0.02f, L + 0.02f, Mat("CardMat_Deck", UIColors.CardHelper));
            Tab("Label", deck, "DRAW", w + 0.02f, L * 0.5f + 0.01f, UIColors.CardHelper, font);

            // AI cards are shuffled into the draw deck (CardTable.aiCards), so they have no pile of their own.
            // Discard pile: AI cards only (D5). The accept/bounce-back logic comes with the AI cards.
            var discard = Group("Discard", root, new Vector3(-PileX, MatThickness, HandZ));
            float dw = w + 0.03f, dl = L + 0.03f;
            PileMat(discard, dw, dl, Mat("CardMat_Discard", UIColors.Mist));
            var rim = Mat("CardMat_DiscardRim", UIColors.CardAI);
            const float rimH = 0.014f, rimT = 0.008f;
            Box("Rim_Near", discard, new Vector3(0, rimH * 0.5f, -dl * 0.5f), new Vector3(dw, rimH, rimT), rim);
            Box("Rim_Far", discard, new Vector3(0, rimH * 0.5f, dl * 0.5f), new Vector3(dw, rimH, rimT), rim);
            Box("Rim_Left", discard, new Vector3(-dw * 0.5f, rimH * 0.5f, 0), new Vector3(rimT, rimH, dl), rim);
            Box("Rim_Right", discard, new Vector3(dw * 0.5f, rimH * 0.5f, 0), new Vector3(rimT, rimH, dl), rim);
            Tab("Label", discard, "DISCARD", dw, dl * 0.5f + 0.01f, UIColors.CardAI, font);

            cardTable.EditorSetup(lanes, laneSpacing, hand, handSpacing, deck, loose);
            cardTable.Deal(helperCount, seed: 1, lockHelpers: false); // show a Full game deal in the editor
            EditorUtility.SetDirty(cardTable);

            float span = helperCount * laneSpacing;
            if (span > 2f * PileX - w - 0.06f)
                Debug.LogWarning("[CardTableBuilder] The helper lanes run into the piles beside the hand.");
            Debug.Log($"[CardTableBuilder] Built {helperCount} helper lanes and placed {cards.Count} cards.");
        }

        /// <summary>Lane mat, slot tags and the four slots (helper, tool, workplace, AI).</summary>
        private static void BuildLane(CardTable.Lane lane, float w, float t, TMP_FontAsset font)
        {
            float laneLength = L + Peek * (SlotNames.Length - 1);
            Mat3D("Mat", lane.root, w + 0.016f, laneLength + 0.016f, Mat("CardMat_Lane", UIColors.Cream),
                new Vector3(0, 0, LaneNearZ + laneLength * 0.5f)); // on the table, top at MatThickness

            Color32[] tagColors = { UIColors.CardHelper, UIColors.CardTool, UIColors.CardWorkplace, UIColors.CardAI };
            string[] tagText = { "", "TOOL", "WORKPLACE", "AI" };

            for (int i = 0; i < SlotNames.Length; i++)
            {
                float z = LaneNearZ + L * 0.5f + i * Peek;
                float y = MatThickness + t * 0.5f + SlotLift + i * StackStep;

                var slotGo = new GameObject("Slot_" + SlotNames[i]);
                slotGo.transform.SetParent(lane.root, false);
                slotGo.transform.localPosition = new Vector3(0, y, z);
                var trigger = slotGo.AddComponent<BoxCollider>();
                trigger.isTrigger = true;
                trigger.size = new Vector3(w * 0.9f, 0.05f, L * 0.9f);
                trigger.center = new Vector3(0, 0.015f, 0);
                var slot = slotGo.AddComponent<CardPlaceSlot>();
                slot.cardCategory = i == 3 ? $"{lane.helper}/AI" : CardDesignsBuilder.Category(lane.helper, (Kind)i);

                if (i == 0) lane.helperSlot = slot;
                else if (i == 1) lane.toolSlot = slot;
                else if (i == 2) lane.workplaceSlot = slot;
                else lane.aiSlot = slot;

                // The tag fills the strip this slot's card adds beyond the card below it.
                if (i == 0) continue;
                float stripZ = z + L * 0.5f - Peek * 0.5f;
                var tag = Group("Tag_" + SlotNames[i], lane.root, new Vector3(0, MatThickness, stripZ));
                Mat3D("Strip", tag, w - 0.004f, Peek - 0.006f, Mat("CardTag_" + SlotNames[i], tagColors[i]), depth: 0.0008f);
                FlatText("Text", tag, tagText[i], font, i == 2 ? 0.012f : 0.016f, w - 0.01f, 0.0009f);
            }
        }

        // ------------------------------------------------------------------ Pieces

        /// <summary>Flat rounded slab: bottom at localPos.y, top at localPos.y + depth.</summary>
        private static void Mat3D(string name, Transform parent, float width, float length, Material mat,
            Vector3 localPos = default, float depth = MatThickness)
        {
            var mesh = MeshKit.RoundedSlab($"CardMat_{width * 1000:0}x{length * 1000:0}x{depth * 10000:0}",
                width, length, depth, Mathf.Min(0.008f, width * 0.2f));
            // Rotating the slab 90 degrees about x lays it flat and turns its depth (-z) upward.
            MeshKit.Show(name, parent, mesh, mat, localPos, new Vector3(90f, 0f, 0f));
        }

        /// <summary>A mat for a pile whose group sits on the mat's top surface (y = MatThickness).</summary>
        private static void PileMat(Transform pile, float width, float length, Material mat) =>
            Mat3D("Mat", pile, width, length, mat, new Vector3(0, -MatThickness, 0));

        /// <summary>Text lying flat on the table, reading upright from the seat.</summary>
        private static void FlatText(string name, Transform parent, string text, TMP_FontAsset font, float em, float width, float lift)
        {
            var tmp = Text3D(name, parent, text, font, em, UIColors.Cream, new Vector3(0, lift, 0),
                new Vector2(width, em * 1.6f), new Vector3(90f, 0f, 0f));
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
        }

        /// <summary>A colored name tab just past the far end of a pile, where the cards won't cover it.</summary>
        private static void Tab(string name, Transform parent, string text, float width, float farOffset, Color32 color, TMP_FontAsset font)
        {
            const float tabLength = 0.026f;
            // On the bare table (the pile group sits on its mat's top surface).
            var tab = Group(name, parent, new Vector3(0, -MatThickness, farOffset + tabLength * 0.5f));
            Mat3D("Strip", tab, width, tabLength, Mat("CardTab_" + text.Replace(' ', '_'), color), depth: 0.0008f);
            FlatText("Text", tab, text, font, 0.014f, width - 0.006f, 0.0009f);
        }

        /// <summary>The generated table top is decor-only (no collider), so physics cards would fall through it.</summary>
        private static void EnsureTableCollider(Transform table)
        {
            var top = table.Find("Top");
            if (top != null && top.GetComponent<Collider>() == null) top.gameObject.AddComponent<BoxCollider>();
        }
    }
}
