using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CommunityHelpers
{
    /// <summary>
    /// Fades the view to a soft cream color and back, using a quad just in front of the headset camera.
    /// Fades in when a scene starts; <see cref="LoadScene"/> fades out, then loads the next scene
    /// (which fades itself in). Comfortable: no camera motion, only a color fade.
    /// </summary>
    public class ScreenFader : MonoBehaviour
    {
        public static ScreenFader Instance { get; private set; }

        [SerializeField] private Renderer quad;
        [SerializeField] private float fadeInOnStart = 0.6f;
        [SerializeField] private float fadeOut = 0.5f;

        private Material _mat;
        private float _alpha = 1f;

        private void Awake()
        {
            Instance = this;
            if (quad == null) quad = GetComponent<Renderer>();
            _mat = quad.material; // per-instance copy so the saved material isn't changed
            SetAlpha(1f);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private IEnumerator Start()
        {
            yield return Fade(1f, 0f, fadeInOnStart);
        }

        /// <summary>Fade out, then load a scene. Blocks menu buttons while it happens.</summary>
        public static void LoadScene(string sceneName)
        {
            KidButton3D.GlobalBlockUntil = float.MaxValue;
            if (Instance != null) Instance.StartCoroutine(Instance.FadeAndLoad(sceneName));
            else SceneManager.LoadScene(sceneName);
        }

        private IEnumerator FadeAndLoad(string sceneName)
        {
            yield return Fade(_alpha, 1f, fadeOut);
            var op = SceneManager.LoadSceneAsync(sceneName);
            if (op == null)
            {
                Debug.LogError($"[ScreenFader] Scene '{sceneName}' isn't in Build Settings. Run Community Helpers → Build All Scenes.");
                KidButton3D.GlobalBlockUntil = 0f;
                yield return Fade(1f, 0f, fadeInOnStart);
                yield break;
            }
            while (!op.isDone) yield return null;
        }

        private IEnumerator Fade(float from, float to, float duration)
        {
            for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
            {
                SetAlpha(Mathf.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t / duration)));
                yield return null;
            }
            SetAlpha(to);
        }

        private void SetAlpha(float a)
        {
            _alpha = a;
            var c = _mat.color;
            c.a = a;
            _mat.color = c;
            quad.enabled = a > 0.001f;
        }
    }
}
