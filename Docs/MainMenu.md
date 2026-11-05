# Main Menu Scene

The main menu is the first thing a player sees: a cozy classroom corner with a big window onto Helper Town, a wooden menu board on the table, and Robo the robot. Players are kids in K–2 (ages 5–8), seated, on Meta Quest 2/3 with **controllers**.

This doc covers how the scene is built, the five build steps and where each one stands, and where to change things.

---

## Quick start

1. Open the project in Unity 6 (6000.3.22f1).
2. Run **Community Helpers → Build Main Menu Scene**.
   - The first run may ask to import **TMP Essential Resources**. Accept, then run it again.
   - It creates `Assets/Scenes/MainMenu.unity` and makes it scene 0 in Build Settings.
3. Press Play over Quest Link, or build to the headset, and sit down.

> **Don't hand-edit `MainMenu.unity`.** The builder recreates the scene from code each time, so manual changes are lost on the next build. Change the builder scripts in `Assets/_Project/Editor/` instead (see [Where to change things](#where-to-change-things)).

---

## Decisions (what we're building, and why)

| Decision | Choice | Why |
|---|---|---|
| XR toolkit | **Meta XR SDK + Meta Interaction SDK** (`OVRCameraRig` + `OVRComprehensiveInteractionRig`) | Already installed; no XRI. |
| Input | **Controllers**, pressing with **ray (trigger) or poke** | The board sits ~0.6 m away so Robo and the board are both in view; that's past a kid's reach, so ray is the main input. Poke still works if they lean in. |
| Movement | **None** (seated). Teleport, turning and sliding are switched off. | Comfort. |
| Look | **Everything 3D**, stylized low-poly (Kenney CC0 kits) | Toy-like, friendly, cheap on Quest. |
| Fonts | **Fredoka** (titles, buttons) and **Andika** (body text) | Round and friendly; Andika writes "a" and "g" the way kids learn them. |
| Voice / sound | **Not yet** | Coming later. |
| Card and icon art | **Placeholders** (colored chips with tiny 3D models) | Teammates are making the real art. |
| Grown-up menu | Called **Settings**, opens with a normal press (no 2-second hold) | Simpler. |

---

## Build steps

Each step ends with a **headset check-in** before the next one starts.

### Step 1: Room, rig and Helper Town ✅ merged (PR #1)

- One-click scene builder.
- Meta camera rig + interaction rig, with locomotion off and floor-level tracking.
- `SeatAnchor` places the whole room around the player's head at start, again when the headset is put on, and on recenter. Table height follows eye height. If the headset reports an unbelievable floor height (common over Link), it assumes a 1.15 m seated eye height. Press **R** in the editor to place the room again.
- Classroom: walls with a wide wood-framed window, a square-cornered table (now 1.8 × 1.1 m), and Kenney props (bookcases, books, plants, rug, teacher's desk, kid chairs).
- Helper Town: a street with six tinted buildings, each with a rooftop sign and its vehicle (Fire Station + fire truck, Hospital + ambulance, Library, Police + police car, School, Post Office + delivery truck), plus trees, a back row of houses and a skyline.
- Fonts and color tokens (`UIColors.cs`).

**Check-in:** Is the table at a good height? Is the town visible through the window? Do the controller rays show? Is teleport/turning gone?

### Step 2: Menu board and 3D buttons 🔨 in progress (branch `Main-Menu-Step-2`)

- A wooden easel board on the table (0.80 × 0.56 m, tilted back 25°): a rounded frame, a cream panel, a chalk ledge, and legs down to the table. It pops in (scale 0.9 → 1.0) 0.6 s after the scene starts.
- **Layout on the table:** the board sits 20 cm left of center. Robo stands to the right and closer to the player (about 0.65 m away). Both are turned to face the seat, so the middle of the window stays open for the view outside. The table is 1.8 × 1.1 m.
- The button layout is designed on a 0.62 × 0.42 m panel and scaled to fill 90% of the board's panel (`ContentFill`), so everything grows together and leaves a clear cream border inside the wood frame.
- **Main panel** with four chunky 3D buttons:

  | Button | Color | Shows |
  |---|---|---|
  | **Play** | green | ▶ icon + six helper chips |
  | **Quick play** | blue | ⚡ icon + three helper chips |
  | **How to play** | lilac | ? |
  | **Settings** | gray | gear icon |

- `KidButton3D` states:

  | State | Look | Feel |
  |---|---|---|
  | Normal | as built | – |
  | Hover (ray on it, or controller within 5 cm) | yellow glow ring, face grows 3% | – |
  | Pressed | face pushes in 7 mm and darkens 12% | 0.08 s controller buzz |
  | Released | springs back, fires the button's action | – |
  | Disabled | grayed out, ignores input | – |

  Double taps within 0.4 s are ignored.
- The buttons only **log** for now. The real flow is step 4.
- Editor testing without a headset: press **1–4** in Play mode to press Play / Quick play / How to play / Settings.

**Check-in:** Can you reach every button with the ray? Can you poke them by leaning in? Is the board the right size and distance? Are the hover glow, press and buzz noticeable? Are the helper chips readable?

### Step 3: Robo and the title sign ⏳ next

- Robo built from simple 3D shapes, standing on the table to the right of the board. Idle bob, wave on start, point at Play when idle.
- A big "Community Helpers" title sign over the window.

### Step 4: Menu flow ⏳

- **Who are you today?** panel after Play / Quick play: pick Fox, Bear, Owl or Bunny, then **Let's go!** (disabled until an animal is picked).
- **Settings** panel: volume, left-handed, tutorial first, Back, Quit.
- Panel swaps (0.25 s fade out, 0.25 s fade in, input blocked during the fade), saved settings (PlayerPrefs), and loading the Game or Tutorial scene with the chosen mode and animal.

### Step 5: Polish ⏳

- Idle hint: after 20 s, the Play button pulses and Robo points.
- Fade from black on start.
- Performance pass (72 FPS, draw calls).
- Trim the Kenney kits to the models we actually use.

---

## How the scene is put together

```
MainMenu
├─ Sun Light
├─ OVRCameraRig                       Meta camera rig (OVRManager: floor-level tracking)
│  └─ OVRComprehensiveInteractionRig  controller ray + poke (locomotion children disabled)
└─ World                              SeatAnchor moves this to the player at start
   ├─ Room                            walls, floor, window frame
   ├─ HelperTown                      street, buildings, signs, vehicles, trees, skyline
   ├─ ClassroomProps                  Kenney furniture
   └─ Table                           SeatAnchor sets its height
      ├─ MenuBoard                    MainMenuController (pop-in, button actions)
      │  ├─ Board                     frame, panel, ledge (tilted 25°)
      │  │  └─ Panel_Main             Btn_Play, Btn_QuickPlay, Btn_HowToPlay, Btn_Settings
      │  └─ BackLeg / FrontFoot ×2
      └─ Placeholder_Robo             replaced in step 3
```

Each button:

```
Btn_<Name>       KidButton3D, PokeInteractable, RayInteractable
├─ Base          dark rounded slab
├─ Glow          yellow ring, shown on hover
├─ Face          colored slab (moves on press)
│  └─ Content    label (TextMeshPro) and icons
└─ Surface       PlaneSurface + BoundsClipper + ClippedPlaneSurface (shared by poke and ray)
```

---

## Where to change things

| I want to change… | File | What to edit |
|---|---|---|
| Room size, window, table size/position | `Editor/MainMenuSceneBuilder.cs` | Constants at the top (`TableSize`, `TableCenterZ`, `WindowX`, …) |
| Table height vs. eye height, floor fallback | `SeatAnchor` component on **World** (Inspector) | `tableTopBelowEye`, `assumedEyeHeight`, `useRealFloor` |
| Town buildings, colors, vehicles, signs | `Editor/MainMenuSceneBuilder.cs` → `BuildHelperTown` | The `places` list |
| Classroom props | `Editor/MainMenuSceneBuilder.cs` → `BuildClassroomProps` | |
| A Kenney kit shows up backwards | `Editor/KenneyKit.cs` | `FrontYaw` (0 or 180) |
| Board position, tilt, size | `Editor/MenuBoardBuilder.cs` | `BoardCenter` (x = offset from center; negative is left), `BoardTilt`, `PanelSize` (buttons scale with it), `ContentFill` (how much of the panel the buttons use) |
| Robo's spot | `Editor/MainMenuSceneBuilder.cs` → `BuildPlaceholders` | `roboPos` |
| Button layout, labels, icons, helper chips | `Editor/MenuBoardBuilder.cs` → `Build` | |
| Button feel (press depth, glow, buzz, debounce) | `KidButton3D` (Inspector, or defaults in `Scripts/Menu/KidButton3D.cs`) | |
| Colors | `Scripts/UIColors.cs` | |

After changing a builder script, run **Community Helpers → Build Main Menu Scene** again.

---

## Folders and assets

| Path | What's there |
|---|---|
| `Assets/_Project/Scripts/` | Runtime code (`SeatAnchor`, `UIColors`, `Menu/KidButton3D`, `Menu/MainMenuController`, `Menu/ControllerHaptics`) |
| `Assets/_Project/Editor/` | Scene builders and helpers (editor-only; not in builds) |
| `Assets/_Project/Fonts/` | Fredoka and Andika `.ttf` files + OFL licenses; `SDF/` holds the generated TextMeshPro font assets |
| `Assets/_Project/Materials/` | Generated materials (safe to regenerate by rebuilding) |
| `Assets/_Project/Meshes/Generated/` | Generated rounded slabs and icon meshes |
| `Assets/_Project/ThirdParty/Kenney/` | Kenney kits (FBX + textures + `License.txt`, CC0) |
| `_Downloads/` (project root, git-ignored) | The original Kenney zips. Not committed. Get them from kenney.nl if you need to re-extract. |

**Licenses:** Kenney kits are CC0 (no attribution required, but appreciated). Fredoka and Andika are under the SIL Open Font License (license files included next to the fonts).

---

## Known issues / to-do

- The Kenney kits include several hundred models we don't use. Trim them in step 5.
- The helper icons and button icons are placeholders until the team's card art is ready.
