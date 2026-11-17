using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;

namespace CommunityHelpers
{
    /// <summary>
    /// Main menu flow. All screens are panels on the same easel board; only one shows at a time.
    ///
    ///   Main ──Play / Quick play──▶ Dress up Robo! ──Let's go!──▶ High five! ──(touch Robo's hand)──▶ Game scene
    ///    │ ▲                          │                      │
    ///    │ └─────────── Back ─────────┘ ◀──────── Back ───────┘
    ///    ├──How to play──▶ How to play ──Back──▶ Main
    ///    └──Settings────▶ Settings ─────Back──▶ Main   (Quit closes the app)
    ///
    /// Panels swap with a quick shrink-out / pop-in while the board itself stays put.
    /// </summary>
    public class MainMenuController : MonoBehaviour
    {
        private enum MenuScreen { Main, DressUp, HighFive, Settings, HowToPlay }

        [Tooltip("The menu board that pops in when the scene starts.")]
        [SerializeField] private Transform board;
        [SerializeField] private float popDelay = 0.6f;
        [SerializeField] private float popDuration = 0.3f;

        [Header("Panels (one shows at a time)")]
        [SerializeField] private Transform panelMain;
        [FormerlySerializedAs("panelPickAnimal")]
        [SerializeField] private Transform panelDressUp;
        [SerializeField] private Transform panelHighFive;
        [SerializeField] private Transform panelSettings;
        [SerializeField] private Transform panelHowToPlay;
        [SerializeField] private float swapOut = 0.15f;
        [SerializeField] private float swapIn = 0.2f;

        [Header("Main panel buttons")]
        [SerializeField] private KidButton3D playButton;
        [SerializeField] private KidButton3D quickPlayButton;
        [SerializeField] private KidButton3D howToPlayButton;
        [SerializeField] private KidButton3D settingsButton;

        [Header("Dress up Robo!")]
        [Tooltip("Firefighter, Police, Mail carrier, Doctor (same order as GameSession.RoboHat).")]
        [FormerlySerializedAs("animalButtons")]
        [SerializeField] private KidButton3D[] hatButtons;
        [SerializeField] private KidButton3D letsGoButton;

        [Header("Settings")]
        [SerializeField] private Renderer[] volumeDots;
        [SerializeField] private Material dotOn;
        [SerializeField] private Material dotOff;

        [Header("Reactions")]
        [SerializeField] private RoboController robo;
        [SerializeField] private TitleSignAnimator titleSign;
        [SerializeField] private RoboDressUp dressUp;
        [SerializeField] private RoboHighFive highFive;

        [Header("Scenes")]
        [SerializeField] private string gameScene = "Game";

        private MenuScreen _screen = MenuScreen.Main;
        private int _pickedHat = -1;
        private bool _swapping;

        private IEnumerator Start()
        {
            ShowOnly(panelMain);
            RefreshSettings();
            ResetHatPick();

            if (board == null) yield break;
            Vector3 full = board.localScale;
            board.localScale = full * 0.9f;
            SetBoardVisible(false);
            KidButton3D.GlobalBlockUntil = Time.unscaledTime + popDelay + popDuration;

            yield return new WaitForSecondsRealtime(popDelay);
            SetBoardVisible(true);
            for (float t = 0; t < popDuration; t += Time.unscaledDeltaTime)
            {
                float e = 1f - Mathf.Pow(1f - t / popDuration, 3f); // ease out
                board.localScale = full * Mathf.Lerp(0.9f, 1f, e);
                yield return null;
            }
            board.localScale = full;
        }

        private void SetBoardVisible(bool visible)
        {
            foreach (var r in board.GetComponentsInChildren<Renderer>(true)) r.enabled = visible;
        }

        // ---------------------------------------------------------------- Main panel

        public void OnPlay() => StartPick(GameSession.GameMode.Full);
        public void OnQuickPlay() => StartPick(GameSession.GameMode.Quick);

        private void StartPick(GameSession.GameMode mode)
        {
            GameSession.Mode = mode;
            ResetHatPick();
            if (dressUp) dressUp.Wear(-1, animate: false);
            if (robo) robo.DoCheer(RoboController.Cheer.Jump);
            Go(MenuScreen.DressUp);
        }

        public void OnHowToPlay()
        {
            if (robo) robo.DoCheer(RoboController.Cheer.Yes);
            Go(MenuScreen.HowToPlay);
        }

        public void OnSettings()
        {
            if (robo) robo.DoCheer(RoboController.Cheer.ThumbsUp);
            Go(MenuScreen.Settings);
        }

        /// <summary>Every sub-panel's Back button returns to Main.</summary>
        public void OnBack() => Go(MenuScreen.Main);

        // ---------------------------------------------------------------- Dress up Robo! and the high five

        /// <summary>Puts hat <paramref name="index"/> on Robo (he nods) and lights up Let's go!</summary>
        public void OnPickHat(int index)
        {
            _pickedHat = index;
            for (int i = 0; i < hatButtons.Length; i++)
                if (hatButtons[i]) hatButtons[i].SetSelected(i == index);
            if (letsGoButton) letsGoButton.Interactable = true;
            if (dressUp) dressUp.Wear(index);
            else if (robo) robo.DoCheer(RoboController.Cheer.Yes);
        }

        /// <summary>Let's go! Robo hops over and holds up his hand; the game starts when the player high-fives him.</summary>
        public void OnLetsGo()
        {
            if (_pickedHat < 0) return;
            GameSession.Hat = (GameSession.RoboHat)_pickedHat;
            Debug.Log($"[MainMenu] {GameSession.Mode} game ({GameSession.HelperCount} helpers), Robo wears the {GameSession.Hat} hat.");
            if (highFive == null)
            {
                StartGame();
                return;
            }
            Go(MenuScreen.HighFive);
            highFive.Begin(StartGame);
        }

        /// <summary>Back from the high five: Robo lowers his hand and hops home; the hats are still there to change.</summary>
        public void OnBackToDressUp()
        {
            if (highFive) highFive.Cancel();
            Go(MenuScreen.DressUp);
        }

        private void StartGame() => ScreenFader.LoadScene(gameScene);

        private void ResetHatPick()
        {
            _pickedHat = -1;
            if (hatButtons != null)
                foreach (var b in hatButtons) if (b) b.SetSelected(false);
            if (letsGoButton) letsGoButton.Interactable = false; // grayed out until Robo has a hat
        }

        // ---------------------------------------------------------------- Settings

        public void OnVolumeDown() { GameSettings.Volume--; RefreshSettings(); }
        public void OnVolumeUp() { GameSettings.Volume++; RefreshSettings(); }

        /// <summary>Puts the room (table, menu, Robo) back in front of the player, e.g. after shifting in the seat.</summary>
        public void OnRecenter()
        {
            var anchor = FindFirstObjectByType<SeatAnchor>();
            if (anchor != null) anchor.Recenter();
            if (robo) robo.Wave();
        }

        public void OnQuit()
        {
            Debug.Log("[MainMenu] Quit");
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void RefreshSettings()
        {
            int v = GameSettings.Volume;
            if (volumeDots != null)
                for (int i = 0; i < volumeDots.Length; i++)
                    if (volumeDots[i]) volumeDots[i].sharedMaterial = i < v ? dotOn : dotOff;
        }

        // ---------------------------------------------------------------- Panel swapping

        private Transform PanelFor(MenuScreen s) => s switch
        {
            MenuScreen.DressUp => panelDressUp,
            MenuScreen.HighFive => panelHighFive,
            MenuScreen.Settings => panelSettings,
            MenuScreen.HowToPlay => panelHowToPlay,
            _ => panelMain,
        };

        private void Go(MenuScreen next)
        {
            if (_swapping || next == _screen) return;
            StartCoroutine(Swap(PanelFor(_screen), PanelFor(next)));
            _screen = next;
        }

        private IEnumerator Swap(Transform from, Transform to)
        {
            _swapping = true;
            KidButton3D.GlobalBlockUntil = Time.unscaledTime + swapOut + swapIn + 0.05f;

            if (from != null)
            {
                Vector3 s0 = from.localScale;
                for (float t = 0; t < swapOut; t += Time.unscaledDeltaTime)
                {
                    from.localScale = s0 * Mathf.Lerp(1f, 0.85f, t / swapOut);
                    yield return null;
                }
                from.gameObject.SetActive(false);
                from.localScale = s0;
            }
            if (to != null)
            {
                Vector3 s1 = to.localScale;
                to.gameObject.SetActive(true);
                for (float t = 0; t < swapIn; t += Time.unscaledDeltaTime)
                {
                    float e = 1f - Mathf.Pow(1f - t / swapIn, 3f);
                    to.localScale = s1 * Mathf.Lerp(0.85f, 1f, e);
                    yield return null;
                }
                to.localScale = s1;
            }
            _swapping = false;
        }

        private void ShowOnly(Transform panel)
        {
            foreach (var p in new[] { panelMain, panelDressUp, panelHighFive, panelSettings, panelHowToPlay })
                if (p != null) p.gameObject.SetActive(p == panel);
            _screen = MenuScreen.Main;
        }

#if UNITY_EDITOR
        private void Update()
        {
            // Editor shortcuts for testing without the headset:
            // 1-4 = Play / Quick play / How to play / Settings. Dress up: F/P/M/D = fire helmet / police cap /
            // mail cap / doctor's mirror, Enter = Let's go!. High five: Space (RoboHighFive). Backspace = Back.
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb == null) return;
            if (_screen == MenuScreen.Main)
            {
                if (kb.digit1Key.wasPressedThisFrame && playButton) playButton.PressFromCode();
                if (kb.digit2Key.wasPressedThisFrame && quickPlayButton) quickPlayButton.PressFromCode();
                if (kb.digit3Key.wasPressedThisFrame && howToPlayButton) howToPlayButton.PressFromCode();
                if (kb.digit4Key.wasPressedThisFrame && settingsButton) settingsButton.PressFromCode();
            }
            else if (kb.backspaceKey.wasPressedThisFrame)
            {
                if (_screen == MenuScreen.HighFive) OnBackToDressUp();
                else OnBack();
            }

            if (_screen == MenuScreen.DressUp)
            {
                if (kb.fKey.wasPressedThisFrame) OnPickHat(0);
                if (kb.pKey.wasPressedThisFrame) OnPickHat(1);
                if (kb.mKey.wasPressedThisFrame) OnPickHat(2);
                if (kb.dKey.wasPressedThisFrame) OnPickHat(3);
                if (kb.enterKey.wasPressedThisFrame && letsGoButton) letsGoButton.PressFromCode();
            }
        }
#endif
    }
}
