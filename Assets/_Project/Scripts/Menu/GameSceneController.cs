using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CommunityHelpers
{
    /// <summary>
    /// Progress and the end of the round, all on the table (no easel, so nothing sits in front of the player):
    /// - Star track: past the far end of each helper's lane is a strip with a star spot for each card that
    ///   helper needs. When a card goes in, a gold star flies from its slot into the next spot.
    /// - Nothing extra pops up when a helper is done (team call, Nov 17); its full row of stars says it.
    /// - End: a "You did it!" sign pops up in the middle of the table, and Play again / Home buttons rise
    ///   out of the table within reach. A small Home button in the table's left corner is there while playing.
    /// CardGame drives it. Built by ProgressTrackBuilder (Community Helpers > Update Game Table).
    /// </summary>
    public class GameSceneController : MonoBehaviour
    {
        [Serializable]
        public class LaneTrack
        {
            [Tooltip("Star spots: Tool, Workplace, AI.")] public Transform[] sockets = new Transform[3];
            [Tooltip("The gold star in each spot (shown once its card is placed).")] public Transform[] stars = new Transform[3];
        }

        [SerializeField] private RoboController robo;
        [SerializeField] private string menuScene = "MainMenu";

        [Header("Star track")]
        [SerializeField] private LaneTrack[] lanes = Array.Empty<LaneTrack>();
        [SerializeField] private float socketSpacing = 0.036f;
        [SerializeField] private float starFlyTime = 0.55f;
        [SerializeField] private float starFlyArc = 0.12f;

        [Header("Buttons and end of round")]
        [Tooltip("Home button shown while playing (hidden at the end, where the big one takes over).")]
        [SerializeField] private Transform homeButton;
        [Tooltip("\"You did it!\" sign.")]
        [SerializeField] private Transform endSign;
        [SerializeField] private TMP_Text endPointsLabel;
        [SerializeField] private Transform[] endStars = Array.Empty<Transform>();
        [Tooltip("Play again and Home; they rise out of the table.")]
        [SerializeField] private Transform endButtons;
        [SerializeField] private float riseDepth = 0.12f;
        [SerializeField] private float riseTime = 0.6f;

        private void Start()
        {
            KidButton3D.GlobalBlockUntil = Time.unscaledTime + 0.8f; // let the fade-in finish first
            if (endSign != null) endSign.gameObject.SetActive(false);
            if (endButtons != null) endButtons.gameObject.SetActive(false);
            Invoke(nameof(Celebrate), 2.6f); // after Robo's hello wave
        }

        private void Celebrate()
        {
            if (robo) robo.DoCheer(RoboController.Cheer.Jump);
        }

        // ------------------------------------------------------------------ Progress (called by CardGame)

        /// <summary>Shows a star spot for each card each helper needs (2, or 3 when there's an AI card for it).</summary>
        public void SetUpProgress(int laneCount, bool[] laneHasAi, int total)
        {
            for (int i = 0; i < lanes.Length && i < laneCount; i++)
            {
                var lane = lanes[i];
                bool ai = laneHasAi != null && i < laneHasAi.Length && laneHasAi[i];
                int count = ai ? 3 : 2;
                for (int k = 0; k < lane.sockets.Length; k++)
                {
                    var socket = lane.sockets[k];
                    if (socket == null) continue;
                    socket.gameObject.SetActive(k < count);
                    var p = socket.localPosition;
                    socket.localPosition = new Vector3((k - (count - 1) * 0.5f) * socketSpacing, p.y, p.z);
                    if (k < lane.stars.Length && lane.stars[k] != null) lane.stars[k].gameObject.SetActive(false);
                }
            }
        }

        /// <param name="from">Where the card went in; the star flies from there to its spot on the track.</param>
        public void MarkPlaced(int lane, CardGame.SlotKind kind, int score, int total, Vector3 from)
        {
            if (lane < 0 || lane >= lanes.Length) return;
            int k = (int)kind;
            var star = k < lanes[lane].stars.Length ? lanes[lane].stars[k] : null;
            if (star != null) StartCoroutine(FlyStar(star, from));
        }

        private IEnumerator FlyStar(Transform star, Vector3 from)
        {
            Vector3 restLocal = star.localPosition;
            Quaternion restRot = star.localRotation;
            Vector3 restScale = star.localScale;
            star.gameObject.SetActive(true);
            Vector3 to = star.position;
            for (float t = 0f; t < starFlyTime; t += Time.deltaTime)
            {
                float e = Mathf.SmoothStep(0f, 1f, t / starFlyTime);
                star.position = Vector3.Lerp(from, to, e) + Vector3.up * (starFlyArc * Mathf.Sin(e * Mathf.PI));
                star.localRotation = restRot * Quaternion.AngleAxis(360f * e, Vector3.forward); // a spin on the way
                star.localScale = restScale * Mathf.Lerp(0.6f, 1f, e);
                yield return null;
            }
            star.localPosition = restLocal;
            star.localRotation = restRot;
            star.localScale = restScale;
            yield return CardGame.Pop(star, 0.5f, 0.3f);
        }

        /// <summary>Shows <paramref name="t"/> after <paramref name="delay"/>, growing from nothing with a little overshoot.</summary>
        private static IEnumerator PopUp(Transform t, float delay)
        {
            if (delay > 0f) yield return new WaitForSeconds(delay);
            Vector3 s = t.localScale;
            t.localScale = Vector3.zero;
            t.gameObject.SetActive(true);
            const float time = 0.35f;
            for (float x = 0f; x < time; x += Time.deltaTime)
            {
                float k = x / time;
                float over = 1f + 0.25f * Mathf.Sin(k * Mathf.PI); // grows past full size, then settles
                t.localScale = s * (Mathf.SmoothStep(0f, 1f, k) * over);
                yield return null;
            }
            t.localScale = s;
        }

        // ------------------------------------------------------------------ End of round

        public void ShowEnd(int score, int total)
        {
            if (endPointsLabel != null) endPointsLabel.text = score == 1 ? "You got 1 star!" : $"You got {score} stars!";
            StartCoroutine(EndSequence());
        }

        private IEnumerator EndSequence()
        {
            KidButton3D.GlobalBlockUntil = Time.unscaledTime + 1.6f; // no stray press while things move
            if (homeButton != null) StartCoroutine(Sink(homeButton));
            if (endSign != null) yield return PopUp(endSign, 0f);
            for (int i = 0; i < endStars.Length; i++)
            {
                if (endStars[i] == null) continue;
                StartCoroutine(CardGame.Pop(endStars[i], 0.4f, 0.4f));
                yield return new WaitForSeconds(0.15f);
            }
            if (endButtons != null) yield return Rise(endButtons);
        }

        /// <summary>Slides <paramref name="t"/> up out of the table to where it was built.</summary>
        private IEnumerator Rise(Transform t)
        {
            Vector3 rest = t.localPosition;
            t.localPosition = rest - Vector3.up * riseDepth;
            t.gameObject.SetActive(true);
            for (float x = 0f; x < riseTime; x += Time.deltaTime)
            {
                float e = 1f - Mathf.Pow(1f - x / riseTime, 3f);
                t.localPosition = rest - Vector3.up * (riseDepth * (1f - e));
                yield return null;
            }
            t.localPosition = rest;
        }

        private IEnumerator Sink(Transform t)
        {
            Vector3 rest = t.localPosition;
            for (float x = 0f; x < riseTime; x += Time.deltaTime)
            {
                t.localPosition = rest - Vector3.up * (riseDepth * Mathf.SmoothStep(0f, 1f, x / riseTime));
                yield return null;
            }
            t.gameObject.SetActive(false);
            t.localPosition = rest;
        }

        // ------------------------------------------------------------------ Buttons

        public void OnPlayAgain() => ScreenFader.LoadScene(SceneManager.GetActiveScene().name);

        public void OnBackToMenu() => ScreenFader.LoadScene(menuScene);

#if UNITY_EDITOR
        private void Update()
        {
            // Editor shortcuts: Backspace = Main menu, R = Play again.
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null && kb.backspaceKey.wasPressedThisFrame) OnBackToMenu();
            if (kb != null && kb.rKey.wasPressedThisFrame) OnPlayAgain();
        }

        /// <summary>Called by ProgressTrackBuilder.</summary>
        public void EditorSetup(RoboController robo, LaneTrack[] lanes, float socketSpacing, Transform homeButton,
            Transform endSign, TMP_Text endPointsLabel, Transform[] endStars, Transform endButtons)
        {
            this.robo = robo;
            this.lanes = lanes;
            this.socketSpacing = socketSpacing;
            this.homeButton = homeButton;
            this.endSign = endSign;
            this.endPointsLabel = endPointsLabel;
            this.endStars = endStars;
            this.endButtons = endButtons;
        }
#endif
    }
}
