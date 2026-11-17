using TMPro;
using UnityEngine;

namespace CommunityHelpers
{
    /// <summary>
    /// Brings the "Community Helpers" sign to life: letters bounce in a wave, marquee bulbs chase around the
    /// border, helper models spin, stars twinkle, the sign sways gently, and confetti bursts on cue.
    /// </summary>
    public class TitleSignAnimator : MonoBehaviour
    {
        [Header("Bouncing letters")]
        [Tooltip("Front text first, then the darker layers behind it that make the letters look thick.")]
        [SerializeField] private TMP_Text[] letterLayers;
        [SerializeField] private float waveHeight = 0.025f;   // meters
        [SerializeField] private float waveSpeed = 3f;
        [SerializeField] private float wavePhase = 0.5f;      // per letter

        [Header("Marquee bulbs")]
        [SerializeField] private Renderer[] bulbs;
        [SerializeField] private Material bulbOn;
        [SerializeField] private Material bulbOff;
        [SerializeField] private float chaseStep = 0.12f;
        [SerializeField] private int chaseSpacing = 3;        // every Nth bulb is lit

        [Header("Spinning helper models and twinkling stars")]
        [SerializeField] private Transform[] spinners;
        [SerializeField] private float spinSpeed = 45f;
        [SerializeField] private Transform[] stars;

        [Header("Gentle sway (the sign hangs from its top)")]
        [SerializeField] private Transform swing;
        [SerializeField] private float swingDegrees = 1.2f;
        [SerializeField] private float swingSpeed = 0.7f;

        [Header("Confetti")]
        [SerializeField] private ParticleSystem confetti;
        [SerializeField] private float confettiAtStart = 1.2f;

        private Vector3[][][] _baseVerts; // [layer][meshIndex][vertex]
        private int _chase;
        private float _chaseTimer;
        private Vector3[] _starScales;
        private Quaternion _swingRest;

        private void Start()
        {
            CacheLetters();
            if (swing != null) _swingRest = swing.localRotation;
            if (stars != null)
            {
                _starScales = new Vector3[stars.Length];
                for (int i = 0; i < stars.Length; i++) _starScales[i] = stars[i].localScale;
            }
            if (confetti != null && confettiAtStart >= 0f) Invoke(nameof(Burst), confettiAtStart);
        }

        /// <summary>Fire a confetti burst (also called when Play is pressed).</summary>
        public void Burst()
        {
            if (confetti != null) confetti.Play(true);
        }

        private void CacheLetters()
        {
            if (letterLayers == null) return;
            _baseVerts = new Vector3[letterLayers.Length][][];
            for (int l = 0; l < letterLayers.Length; l++)
            {
                var t = letterLayers[l];
                if (t == null) continue;
                t.ForceMeshUpdate();
                var info = t.textInfo;
                _baseVerts[l] = new Vector3[info.meshInfo.Length][];
                for (int m = 0; m < info.meshInfo.Length; m++)
                    _baseVerts[l][m] = (Vector3[])info.meshInfo[m].vertices.Clone();
            }
        }

        private void Update()
        {
            float t = Time.time;
            AnimateLetters(t);
            AnimateBulbs();

            if (spinners != null)
                foreach (var s in spinners)
                    if (s != null) s.Rotate(0f, spinSpeed * Time.deltaTime, 0f, Space.Self);

            if (stars != null)
                for (int i = 0; i < stars.Length; i++)
                {
                    if (stars[i] == null) continue;
                    stars[i].Rotate(0f, 0f, (i % 2 == 0 ? 30f : -30f) * Time.deltaTime, Space.Self);
                    float pulse = 1f + 0.12f * Mathf.Sin(t * 3f + i * 1.7f);
                    stars[i].localScale = _starScales[i] * pulse;
                }

            if (swing != null)
                swing.localRotation = _swingRest * Quaternion.Euler(0f, 0f, swingDegrees * Mathf.Sin(t * swingSpeed));
        }

        private void AnimateLetters(float t)
        {
            if (_baseVerts == null) return;
            for (int l = 0; l < letterLayers.Length; l++)
            {
                var text = letterLayers[l];
                if (text == null || _baseVerts[l] == null) continue;
                var info = text.textInfo;
                if (info.meshInfo.Length != _baseVerts[l].Length) { CacheLetters(); return; }

                // TMP 3D text units: the text's local space. Convert the wave height from meters.
                float scale = Mathf.Max(0.0001f, text.transform.lossyScale.y);
                float amp = waveHeight / scale;

                for (int c = 0; c < info.characterCount; c++)
                {
                    var ch = info.characterInfo[c];
                    if (!ch.isVisible) continue;
                    int m = ch.materialReferenceIndex, v = ch.vertexIndex;
                    var src = _baseVerts[l][m];
                    var dst = info.meshInfo[m].vertices;
                    if (src == null || v + 3 >= src.Length) continue;
                    float y = Mathf.Sin(t * waveSpeed - c * wavePhase) * amp;
                    for (int k = 0; k < 4; k++) dst[v + k] = src[v + k] + new Vector3(0f, y, 0f);
                }
                text.UpdateVertexData(TMP_VertexDataUpdateFlags.Vertices);
            }
        }

        private void AnimateBulbs()
        {
            if (bulbs == null || bulbs.Length == 0 || bulbOn == null || bulbOff == null) return;
            _chaseTimer += Time.deltaTime;
            if (_chaseTimer < chaseStep) return;
            _chaseTimer = 0f;
            _chase = (_chase + 1) % Mathf.Max(1, chaseSpacing);
            for (int i = 0; i < bulbs.Length; i++)
                if (bulbs[i] != null)
                    bulbs[i].sharedMaterial = (i % chaseSpacing == _chase) ? bulbOn : bulbOff;
        }
    }
}
