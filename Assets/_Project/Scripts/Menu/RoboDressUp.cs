using System.Collections;
using UnityEngine;

namespace CommunityHelpers
{
    /// <summary>
    /// Robo's hats (fire helmet, police cap, mail cap, doctor's head mirror), all on his head and hidden.
    /// On the menu's "Dress up Robo!" panel, <see cref="Wear"/> drops the picked hat onto his head with a spin.
    /// In the Game scene, he puts on the hat from <see cref="GameSession.Hat"/> when the scene starts.
    /// Hats are built by RoboHatsBuilder, in GameSession.RoboHat order.
    /// </summary>
    public class RoboDressUp : MonoBehaviour
    {
        [SerializeField] private RoboController robo;
        [Tooltip("Firefighter, Police, MailCarrier, Doctor (GameSession.RoboHat order).")]
        [SerializeField] private Transform[] hats = new Transform[4];
        [Tooltip("Wear the hat from GameSession when the scene starts (Game scene).")]
        [SerializeField] private bool wearSessionHat;

        private Vector3[] _rest;
        private Coroutine _drop;
        private int _wearing = -1;

        private void Awake()
        {
            _rest = new Vector3[hats.Length];
            for (int i = 0; i < hats.Length; i++)
            {
                if (hats[i] == null) continue;
                _rest[i] = hats[i].localPosition;
                hats[i].gameObject.SetActive(false);
            }
        }

        private void Start()
        {
            if (wearSessionHat && GameSession.Hat != GameSession.RoboHat.None) Wear((int)GameSession.Hat, animate: false);
        }

        /// <summary>Puts on hat <paramref name="index"/> (-1 = none), taking off any other.</summary>
        public void Wear(int index, bool animate = true)
        {
            if (index == _wearing) return;
            if (_drop != null) StopCoroutine(_drop);
            for (int i = 0; i < hats.Length; i++)
            {
                if (hats[i] == null) continue;
                hats[i].gameObject.SetActive(i == index);
                hats[i].localPosition = _rest[i];
                hats[i].localRotation = Quaternion.identity;
            }
            _wearing = index;
            if (index < 0 || index >= hats.Length || hats[index] == null) return;
            if (animate) _drop = StartCoroutine(DropOn(index));
        }

        /// <summary>The hat falls onto his head from above, spinning, with a little bounce. Robo nods.</summary>
        private IEnumerator DropOn(int i)
        {
            var hat = hats[i];
            const float time = 0.45f, height = 0.25f;
            for (float t = 0f; t < time; t += Time.deltaTime)
            {
                float k = t / time;
                float fall = 1f - k * k;                                   // speeds up as it falls
                hat.localPosition = _rest[i] + Vector3.up * (height * fall);
                hat.localRotation = Quaternion.Euler(0f, 360f * (1f - k), 0f);
                yield return null;
            }
            hat.localRotation = Quaternion.identity;
            // Little bounce as it lands.
            for (float t = 0f; t < 0.2f; t += Time.deltaTime)
            {
                hat.localPosition = _rest[i] + Vector3.up * (0.02f * Mathf.Sin(t / 0.2f * Mathf.PI));
                yield return null;
            }
            hat.localPosition = _rest[i];
            if (robo != null) robo.DoCheer(RoboController.Cheer.Yes);
            _drop = null;
        }

#if UNITY_EDITOR
        /// <summary>Called by RoboHatsBuilder.</summary>
        public void EditorSetup(RoboController robo, Transform[] hats, bool wearSessionHat)
        {
            this.robo = robo;
            this.hats = hats;
            this.wearSessionHat = wearSessionHat;
        }
#endif
    }
}
