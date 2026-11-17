# Hints (draft for team review)

When a card lands in the wrong slot, it flies back to the hand. Then **the helper the card belongs to** walks up to the window, faces the player and gestures, and a speech bubble pops up over their head. Robo turns part way toward the window and points at them with the arm on that side. They go back to strolling when that card is placed correctly, or after 10 seconds.

**For now the bubble just shows "..."** (team call, Nov 17). The voice-over will say the hint once a teammate has recorded it.

## How it works in Unity

| What | Where |
|---|---|
| The hint lines and voice clips | `Assets/_Project/Data/HintBook.asset`. Select it and edit it in the Inspector. Each card has a **Clue**, a **Helping Hand**, and a voice clip slot for each. |
| Showing the text instead of "..." | Tick **Show Text** on the HintBook. Lines that are still empty keep showing "...". |
| People outside the window | `World/TownPeople` in the Game scene: a grassy terrace with a path, about 4.3–5.8 m from the seat. Only this game's helpers are out there (3 in Quick play, 6 in Full); there are no extra townsfolk, so it doesn't feel crowded. They stroll well past both edges of the window, so only some are in view at a time, and they stop now and then and sometimes turn to look in. When one of a helper's cards goes in its right slot, that helper stops, turns to the window, gestures and nods (if they're out of view, they first hurry to the edge of the window). At the end of the round they all face the window and nod. |
| Code | `HintGiver.cs` (picks the helper and the line), `TownWalker.cs` (walking, talking, the bubble), `HintBook.cs` (the data), `RoboController.PointAt` |
| Rebuilding | **Community Helpers → Update Game Table (Cards, Progress, Robo, People)**. The builder (`TownPeopleBuilder.cs`) makes the HintBook only if it doesn't exist yet, so edits to it are never overwritten. |
| Testing in the editor | In Play mode, **H** gives a hint for a random card. **O** drops a card on a wrong slot, which also triggers the hint. |

The characters are [Kenney Mini Characters](https://kenney.nl/assets/mini-characters-1) (CC0), in `Assets/_Project/ThirdParty/Kenney/mini-characters`. Who plays whom:

| Helper | Character |
|---|---|
| Firefighter | `worker` (the yellow hard hat is recolored red), carries a fire extinguisher |
| Doctor | `female-e` (white coat), carries a red medical case |
| Librarian | `male-e` (glasses), carries a stack of books |
| Police officer | `male-c` (blue cap and uniform), carries a crossing-guard STOP sign |
| Teacher | `female-d` (blazer), carries an apple |
| Mail carrier | `female-c` (blue shirt, red bag strap), carries a parcel |

## The lines

Each card has two hints:

- **Hint 1 (clue):** the first time the card goes in the wrong place. It says what the helper does with the card, but not where it goes.
- **Hint 2 (helping hand):** if the same card goes wrong again. It says which helper the card belongs to.

The hints are written for ages 5–8, to be read aloud: short, simple words, no more than about 10 words. The helper is the one talking, so they say "I" and "my".

**To review:** these are the first drafts. They were copied into the HintBook asset when it was made, so make the real edits there. Updating this doc as well keeps both in step.

### Firefighter

| Card | Hint 1 (clue) | Hint 2 (helping hand) |
|---|---|---|
| Hose (tool) | I spray water on fires with this! | That's my hose! Put it with me, the firefighter. |
| Fire station (workplace) | My big red fire truck waits here! | That's my fire station! Put it with the firefighter. |

### Doctor

| Card | Hint 1 (clue) | Hint 2 (helping hand) |
|---|---|---|
| Stethoscope (tool) | I listen to your heartbeat with this! | That's my stethoscope! Put it with me, the doctor. |
| Hospital (workplace) | I help sick people feel better here! | That's my hospital! Put it with the doctor. |

### Librarian

| Card | Hint 1 (clue) | Hint 2 (helping hand) |
|---|---|---|
| Book (tool) | I help kids find stories like this! | That's my book! Put it with me, the librarian. |
| Library (workplace) | I work where the shelves are full of books! | That's my library! Put it with the librarian. |

### Police officer

| Card | Hint 1 (clue) | Hint 2 (helping hand) |
|---|---|---|
| Badge (tool) | I wear this to show I keep you safe! | That's my badge! Put it with me, the police officer. |
| Police station (workplace) | I go here to start my day keeping people safe! | That's my police station! Put it with the police officer. |

### Teacher

| Card | Hint 1 (clue) | Hint 2 (helping hand) |
|---|---|---|
| Whiteboard (tool) | I write lessons on this for my class! | That's my whiteboard! Put it with me, the teacher. |
| School (workplace) | Kids come here to learn with me! | That's my school! Put it with the teacher. |

### Mail carrier

| Card | Hint 1 (clue) | Hint 2 (helping hand) |
|---|---|---|
| Mailbag (tool) | I carry letters to your house in this! | That's my mailbag! Put it with me, the mail carrier. |
| Post office (workplace) | All the letters get sorted here! | That's my post office! Put it with the mail carrier. |

## Not written yet

- **AI Suggestion cards:** a teammate is designing them. Their hints get added here once the cards exist.
- **Helper cards:** these are already locked in their lanes, so they never need a hint.

## Main Menu

Three helpers (the firefighter, the doctor and the police officer) also stroll past the Main Menu's window, carrying their things. There are no hints there. To update an existing menu scene, use **Community Helpers → Update Main Menu (Robo, How to Play, People)**.
