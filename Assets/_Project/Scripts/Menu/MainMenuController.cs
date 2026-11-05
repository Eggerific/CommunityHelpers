using System.Collections;
using UnityEngine;

namespace CommunityHelpers
{
    /// <summary>
    /// Main menu. Step 2: board pop-in and button hooks (they only log for now).
    /// Step 4 adds the panels (Who are you today?, Settings), fades and scene loading.
    /// </summary>
    public class MainMenuController : MonoBehaviour
    {
        [Tooltip("The menu board that pops in when the scene starts.")]
        [SerializeField] private Transform board;
        [SerializeField] private float popDelay = 0.6f;
        [SerializeField] private float popDuration = 0.3f;

        [Header("Main panel buttons")]
        [SerializeField] private KidButton3D playButton;
        [SerializeField] private KidButton3D quickPlayButton;
        [SerializeField] private KidButton3D howToPlayButton;
        [SerializeField] private KidButton3D settingsButton;

        [Header("Reactions (step 3)")]
        [SerializeField] private RoboController robo;
        [SerializeField] private TitleSignAnimator titleSign;

        private IEnumerator Start()
        {
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

        // Hooked up to the buttons' onPressed by the scene builder.
        public void OnPlay()
        {
            Debug.Log("[MainMenu] Play (6 helpers). Who are you today? comes in step 4.");
            if (robo) robo.DoCheer(RoboController.Cheer.Jump);
        }

        public void OnQuickPlay()
        {
            Debug.Log("[MainMenu] Quick play (3 helpers). Who are you today? comes in step 4.");
            if (robo) robo.DoCheer(RoboController.Cheer.Jump);
        }

        public void OnHowToPlay()
        {
            Debug.Log("[MainMenu] How to play. Tutorial scene comes in step 4.");
            if (robo) robo.DoCheer(RoboController.Cheer.Yes);
        }

        public void OnSettings()
        {
            Debug.Log("[MainMenu] Settings. Settings panel comes in step 4.");
            if (robo) robo.DoCheer(RoboController.Cheer.ThumbsUp);
        }

#if UNITY_EDITOR
        private void Update()
        {
            // Editor shortcuts for testing without the headset: 1-4 press the buttons.
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb == null) return;
            if (kb.digit1Key.wasPressedThisFrame && playButton) playButton.PressFromCode();
            if (kb.digit2Key.wasPressedThisFrame && quickPlayButton) quickPlayButton.PressFromCode();
            if (kb.digit3Key.wasPressedThisFrame && howToPlayButton) howToPlayButton.PressFromCode();
            if (kb.digit4Key.wasPressedThisFrame && settingsButton) settingsButton.PressFromCode();
        }
#endif
    }
}
