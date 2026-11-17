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
                      [ You did it! ]  (pops up at the end)          Robo (back right)
   far     [* *]   [* *]   [* *]   [* *]   [* *]   [* *]      star track: a star spot per card
           [ AI ]  [ AI ]  [ AI ]  [ AI ]  [ AI ]  [ AI ]
           [WORK]  [WORK]  [WORK]  [WORK]  [WORK]  [WORK]     each slot sits 4.5 cm farther
           [TOOL]  [TOOL]  [TOOL]  [TOOL]  [TOOL]  [TOOL]     than the one before it, 1.5 mm higher
           Fire    Doctor  Library Police  Teacher Mail       helper cards, face up

  (home)  DISCARD    [ hand mat: drawn cards go here ]       DRAW (12 cards + AI cards, face down)
                       [Play again]  [Home]   (rise out of the table at the end)
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
| The rules: scoring, wrong cards, end of round | `Assets/_Project/Scripts/Game/CardGame.cs` |
| Per card: turns face up when drawn, notices a wrong slot, flies back to the hand | `Assets/_Project/Scripts/Game/PlayCard.cs` |
| Progress on the table (star track), end sign, Home / Play again buttons | `Assets/_Project/Scripts/Menu/GameSceneController.cs`, built by `Assets/_Project/Editor/ProgressTrackBuilder.cs` |

To rebuild: **Community Helpers → Update Game Table (Cards, Progress, Robo)**. This replaces the `CardTable` group in `Game.unity` (and removes the old easel, if it's still there) and keeps everything else. In the editor the scene shows a fixed Full game deal. In Play mode, a new shuffle happens every game; set **Shuffle Seed** on `CardTable` to get the same deal every time.

With Unity closed, the same rebuild runs from the command line (it exits by itself when done):
`Unity.exe -batchmode -projectPath . -executeMethod CommunityHelpers.EditorTools.GameTableBuilder.UpdateGameSceneAndExit`

Van's earlier test slot, deck and card are switched off (not deleted) in the scene.

## How a round plays

Team decisions from Nov 16, 2026, on top of the design doc:

| | Rule |
|---|---|
| Drawing | Draw freely: there are no turns. The player picks up any card from the deck. Cards stay face down until the player turns them over (no auto-flip); a card goes into its slot face up either way. |
| Right card | Dropping a card on its helper's slot snaps it in and scores **+1**. The slot puffs sparkles, a "+1" floats up, the card pops and Robo gives a thumbs up. When a helper has all its cards, Robo nods. |
| Wrong card | If a card lands on a slot that doesn't take it (another helper's slot or a helper card), the card flies back to a free spot on the hand mat (D6), the same way up it landed. There's no penalty. Dropping it anywhere on its own helper's lane counts as right. |
| Progress | All on the table, so nothing stands in front of the player. Past the far end of each lane is a dark strip with a star spot for each card that helper needs. When a card goes in, a gold star spins out of its slot and lands in the next spot. When all of a helper's cards are in, its row of stars is full; nothing else pops up. A small **Home** button sits in the table's near-left corner. |
| End | When every card is placed: confetti from the title sign, sparkles on every slot, and Robo jumps. A "You did it!" sign with the star count pops up behind the lanes, the small Home button sinks away, and two big buttons rise out of the table over the hand mat: **Play again** (same mode) and **Home**. |
| Not used | No timer (D10, on hold), no "Explain why" (D4, dropped), and no sound effects or controller vibration for now (a teammate is making the sounds). |

A full game has 12 cards to place (6 helpers × Tool + Workplace); Quick play has 6.

### Testing in the editor

In Play mode: **P** places the next card in its slot, **O** drops a card on a wrong slot (it should fly back, and its helper comes to the window), **H** shows a hint for a random card, **R** restarts and **Backspace** goes to the main menu. W isn't used because the Meta XR Simulator moves with WASD.

## Hooks for what's next

- **Hints** are built: the helper the card belongs to walks up to the window outside and talks in a speech bubble while Robo points at them. See [Hints.md](Hints.md). They start from `CardGame.WrongCard`, which fires with the card and the slot it was dropped on.
- **AI Suggestion cards** (a teammate is designing them). Add the AI card objects to `CardTable` → **AI Cards** and they're shuffled into the deck. An AI card that a lane's AI slot accepts (category `"<Helper>/AI"`, e.g. `"Doctor/AI"`) becomes a card to place: it counts toward the total, scores +1, and adds a third star spot to that helper's part of the track. Still to do: AI cards that should be **discarded** (D1, judging a false card), and the discard tray accepting AI cards only (D5). Right now the tray has no logic, so a card dropped in it just sits there.
- **Sounds** (a teammate is making them). `CardGame` has four empty slots: Correct, Lane Done, Fanfare and Wrong. Nothing plays until a clip is assigned.

## Not built yet

- AI Suggestion cards and the discard pile rules (D1, D5): see the hooks above.
- Voice-overs for the hints and read-aloud (D8): a teammate is recording them. The hint bubbles show "..." until then.
- Timer (D10, on hold).
