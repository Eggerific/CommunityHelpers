using TMPro;
using UnityEngine;

namespace CommunityHelpers
{
    /// <summary>
    /// PLACEHOLDER for the real game, which will be played on the table. The easel only names the mode
    /// ("Play" or "Quick play") and offers "Back to menu". GameSession still carries the mode and the
    /// chosen animal for the real game to use.
    /// </summary>
    public class GameSceneController : MonoBehaviour
    {
        [SerializeField] private TMP_Text titleLabel;
        [SerializeField] private RoboController robo;
        [SerializeField] private string menuScene = "MainMenu";

        private void Start()
        {
            KidButton3D.GlobalBlockUntil = Time.unscaledTime + 0.8f; // let the fade-in finish first
            if (titleLabel) titleLabel.text = GameSession.Mode == GameSession.GameMode.Full ? "Play" : "Quick play";
            Invoke(nameof(Celebrate), 2.6f); // after Robo's hello wave
            Debug.Log($"[Game] Placeholder game: {GameSession.Mode}, {GameSession.HelperCount} helpers, playing as {GameSession.Animal}.");
        }

        private void Celebrate()
        {
            if (robo) robo.DoCheer(RoboController.Cheer.Jump);
        }

        public void OnBackToMenu() => ScreenFader.LoadScene(menuScene);

#if UNITY_EDITOR
        private void Update()
        {
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null && kb.backspaceKey.wasPressedThisFrame) OnBackToMenu();
        }
#endif
    }
}
