# Game Table (cards and deck)

How the cards are laid out on the Game scene table, and where to change it. The rules come from the team's design doc (*Community Helpers VR: Final Game Concept and Decisions*, Nov 3 2026).

## Rules this layout follows

| Decision | Pick | What it means on the table |
|---|---|---|
| D2 Turn structure | Face-up helpers (Van's version) | The helpers are laid out face up. All tool and workplace cards start face down in the draw deck. There's no starting hand: cards are drawn from the deck and laid on the hand mat. |
| D7 Helper slots | One slot per helper | Each helper has its own lane (its slot), with spots for **Tool**, **Workplace** and **AI**. The cards overlap a little so each one's name still shows. The helper card is the job card. |
| AI cards | Mixed into the main deck | There's no separate AI pile. AI cards get shuffled into the draw deck with the tool and workplace cards (`CardTable` → **AI Cards**). |
| D5 Discard pile | AI cards only | The discard tray sits left of the hand mat. The accept/bounce-back logic will come with the AI cards. |
| D3 Who's playing | Solo | There's one hand, at the player's seat. |
| Quick play | First 3 helpers | Firefighter, Doctor, Librarian. Full game uses all 6. |

The **Community Helper** card art shows the job too ("Helps sick people"), so it works as the helper card and the job card in one. That's why each lane has no separate Job slot.

## Layout

```
                 (easel, pushed back)                    Robo (back right)

   far     [ AI ]  [ AI ]  [ AI ]  [ AI ]  [ AI ]  [ AI ]
           [WORK]  [WORK]  [WORK]  [WORK]  [WORK]  [WORK]     each slot sits 4.5 cm farther
           [TOOL]  [TOOL]  [TOOL]  [TOOL]  [TOOL]  [TOOL]     than the one before it, 1.5 mm higher
           Fire    Doctor  Library Police  Teacher Mail       helper cards, face up

  DISCARD    [ hand mat: drawn cards go here ]               DRAW (12 cards + AI cards, face down)
   near    ---------------------------- table edge ----------------------------
                                    (player)
```

- Everything the player touches is within about 40 cm of the table's near edge.
- Every empty slot shows a colored tag (teal Tool, purple Workplace, gray AI) in the strip its card will cover.
- A card only snaps into the slot whose `cardCategory` matches its own (`"Doctor/Tool"` and so on). The slots overlap, so a wrong card just doesn't snap.
- A card that snaps into a slot stops being a physics object (it goes kinematic), so the overlapping cards don't jitter or push each other. The slots step up only 1.5 mm each, so a card doesn't float when the slot under it is empty.
- Helper cards are locked in their slots. If a kid picks one up to look at it, it goes back to its slot when they let go.

## How it works

| Piece | File |
|---|---|
| Lanes, slots, mats, tags, piles; wires everything up | `Assets/_Project/Editor/CardTableBuilder.cs` |
| Card prefabs, the helper list and its order, card categories | `Assets/_Project/Editor/CardDesignsBuilder.cs` |
| Sets up at game start: picks 3 or 6 lanes, centers them, shuffles, stacks the deck (a starting hand can be turned on with Full/Quick Hand) | `Assets/_Project/Scripts/Game/CardTable.cs` |
| Slot snapping (Van's scripts) | `Assets/Scripts/CardPlaceSlot.cs`, `Assets/Scripts/CardScript.cs` |

To rebuild: **Community Helpers → Update Game Table (Cards + Robo)**. This replaces the `CardTable` group in `Game.unity` and keeps everything else. In the editor the scene shows a fixed Full game deal. In Play mode, a new shuffle happens every game; set **Shuffle Seed** on `CardTable` to get the same deal every time.

Van's earlier test slot, deck and card are switched off (not deleted) in the scene.

## Not built yet

- AI Suggestion cards (no art yet), and discard pile logic that only accepts AI cards (D5).
- Wrong-match feedback (D6): the card goes back to the hand with a hint.
- Turns, scoring, "Explain why", and end of round (D1, D4, D10).
- Read-aloud (D8).
