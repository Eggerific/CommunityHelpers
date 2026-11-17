# Community Helpers VR

A cheerful virtual reality card game that teaches young kids about the people who help their town: firefighters, doctors, librarians, police officers, teachers and mail carriers.

Kids sit at a table in a sunny classroom, draw cards, and match each helper with the **tool** they use and the **place** they work. A friendly robot named Robo cheers them on, and the helpers themselves stroll past the window and wave in with hints.

> UNCW VR Final Project · Group 6

---

## At a glance

| | |
|---|---|
| **Who it's for** | Kids in kindergarten to 2nd grade (ages 5–8), with an adult nearby |
| **Headset** | Meta Quest 2 or Quest 3 |
| **How you play** | Seated, using controllers or your hands |
| **Players** | One |
| **How long** | A few minutes a round: Quick play has 6 cards to place, the Full game 12 |

---

## How to play

1. **Pick a game.** **Play** has all 6 helpers. **Quick play** has 3.
2. **Dress up Robo.** Choose a hat for Robo: a fire helmet, police cap, mail cap or doctor's head mirror.
3. **High five!** Robo holds up his glowing hand. Reach out and touch it to start.
4. **Draw a card.** Pick up any card from the deck and turn it over to see what it is.
5. **Put it with its helper.** Every helper has their own row on the table. Place the tool or workplace card in the right row.
6. **Fill all the stars!** Each right card earns a gold star. Fill every star to win.

**Got one wrong?** No problem. The card floats back to your hand, and the helper it belongs to walks up to the window with a hint.

---

## What's in the game

**The classroom.** A bright, cozy room with a big window looking out on Helper Town. You can see the hospital, fire station, library, police station, school and post office, with cars driving by and a plane overhead.

**Robo.** Your robot buddy on the table. He waves hello, cheers when you get a card right, points at the window when a helper has a hint, and wears whatever hat you picked.

**The helpers.** Six community helpers stroll along the path outside the window, each carrying something of theirs: an extinguisher, a medical case, books, a stop sign, an apple, a parcel. When you place one of their cards, they turn and nod at you. When you need help, they come to the window to talk.

**The cards.**

| Helper | Tool | Workplace |
|---|---|---|
| Firefighter | Hose | Fire station |
| Doctor | Stethoscope | Hospital |
| Librarian | Book | Library |
| Police officer | Badge | Police station |
| Teacher | Whiteboard | School |
| Mail carrier | Mailbag | Post office |

Quick play uses the first three helpers.

**The win.** When every star is filled, confetti flies, everyone celebrates, and you can **Play again** or go **Home**.

---

## Made for little learners

- **No reading needed to play.** Pictures, colors and stars carry the game.
- **No losing.** A wrong card is just a hint, never a penalty, and there's no timer.
- **Big, friendly buttons** that can be poked with a hand or pointed at with a controller.
- **Comfortable VR.** The player stays seated, nothing moves the camera, and screens change with gentle fades.
- **Everything within reach.** The cards sit close to the player's side of the table.
- **An adult nearby.** The game is meant to be played with a grown-up close by to help and talk about the helpers.

---

## Getting it running

**You'll need:** Unity **6000.3.22f1** (Unity 6), a Meta Quest 2 or 3, and the Android build tools for Unity.

1. Clone this repository and open the folder in Unity Hub.
2. Open `Assets/Scenes/MainMenu.unity` and press **Play** to try it in the editor. It uses the Meta XR Simulator or a Quest connected with Link.
3. To put it on a headset, go to **File → Build Settings**, choose **Android**, connect the Quest, and press **Build and Run**.

**Testing without a headset:** the game has keyboard shortcuts in the editor. For example, on the menu, number keys press the buttons and **Space** high-fives Robo. In the game, **P** places the next card and **H** shows a hint. The full list is in the docs below.

---

## Still to come

- **AI Suggestion cards:** a new kind of card where kids decide whether a suggestion is right or wrong.
- **Voice-overs:** spoken hints and read-aloud for every card.
- **Sound effects** and music.

---

## For the team

These docs explain how each part is built and where to change things:

- [Main Menu](Docs/MainMenu.md): the menu, Robo, the hats and the high five
- [Game Table](Docs/GameTable.md): the cards, the rules, the stars and the end of the round
- [Hints](Docs/Hints.md): the helpers outside the window and the hint lines (for review)

---

## Credits

- **Card art:** Group 5
- **3D models:** [Kenney](https://kenney.nl): City Kit (Commercial and Suburban), Car Kit, Furniture Kit, Nature Kit and Mini Characters. All CC0.
- **Fonts:** Fredoka and Andika, from Google Fonts (SIL Open Font License)
- **Built with:** Unity 6 and the Meta XR SDK

