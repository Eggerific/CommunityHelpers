using System.Collections.Generic;
using Oculus.Interaction;
using UnityEngine;
using UnityEngine.Events;

namespace CommunityHelpers
{
    /// <summary>
    /// A chunky 3D button pressed with a controller ray (trigger) or by poking it with the controller.
    /// States: Normal, Hover (glow + slight grow), Pressed (face pushes in, darkens, buzz), Disabled (grayed).
    /// Fires <see cref="onPressed"/> on release.
    /// </summary>
    public class KidButton3D : MonoBehaviour
    {
        [Header("Parts")]
        [Tooltip("The colored top of the button. It moves in when pressed.")]
        [SerializeField] private Transform face;
        [SerializeField] private Renderer faceRenderer;
        [Tooltip("Sun-colored ring shown on hover.")]
        [SerializeField] private GameObject glow;
        [Tooltip("Meta Interaction SDK interactables (PokeInteractable, RayInteractable) on this button.")]
        [SerializeField] private MonoBehaviour[] pointables;

        [Header("Look")]
        [SerializeField] private Color faceColor = Color.white;
        [SerializeField] private float pressDepth = 0.007f;   // meters the face travels in
        [SerializeField] private float hoverScale = 1.03f;
        [SerializeField, Range(0, 1)] private float pressDarken = 0.12f;
        [SerializeField] private float animSpeed = 22f;

        [Header("Behavior")]
        [SerializeField] private bool interactable = true;
        [Tooltip("Ignore presses this soon after the last one, so a double tap can't fire twice.")]
        [SerializeField] private float debounce = 0.4f;
        [SerializeField] private float hapticAmplitude = 0.3f;
        [SerializeField] private float hapticDuration = 0.08f;

        public UnityEvent onPressed = new();

        /// <summary>Blocks every button until this time (used while panels fade).</summary>
        public static float GlobalBlockUntil;

        private readonly HashSet<int> _hovering = new();
        private readonly HashSet<int> _selecting = new();
        private readonly List<IPointable> _subscribed = new();
        private MaterialPropertyBlock _mpb;
        private Vector3 _faceRestPos, _faceRestScale;
        private float _pressAmount, _hoverAmount, _lastFire = -10f;
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        public bool Interactable
        {
            get => interactable;
            set
            {
                interactable = value;
                foreach (var p in pointables)
                    if (p != null) p.enabled = value;
                if (!value) { _hovering.Clear(); _selecting.Clear(); }
            }
        }

        public bool IsHovered => _hovering.Count > 0;
        public bool IsPressed => _selecting.Count > 0;

        private void Awake()
        {
            _mpb = new MaterialPropertyBlock();
            if (face != null)
            {
                _faceRestPos = face.localPosition;
                _faceRestScale = face.localScale;
            }
            if (glow != null) glow.SetActive(false);
        }

        private void OnEnable()
        {
            foreach (var mb in pointables)
            {
                if (mb is IPointable p)
                {
                    p.WhenPointerEventRaised += HandlePointerEvent;
                    _subscribed.Add(p);
                }
            }
            Interactable = interactable;
        }

        private void OnDisable()
        {
            foreach (var p in _subscribed) p.WhenPointerEventRaised -= HandlePointerEvent;
            _subscribed.Clear();
            _hovering.Clear();
            _selecting.Clear();
        }

        private void HandlePointerEvent(PointerEvent evt)
        {
            if (!interactable) return;
            switch (evt.Type)
            {
                case PointerEventType.Hover:
                    _hovering.Add(evt.Identifier);
                    break;
                case PointerEventType.Unhover:
                    _hovering.Remove(evt.Identifier);
                    _selecting.Remove(evt.Identifier);
                    break;
                case PointerEventType.Select:
                    bool first = _selecting.Count == 0;
                    _selecting.Add(evt.Identifier);
                    if (first) ControllerHaptics.Pulse(this, transform.position, hapticAmplitude, hapticDuration);
                    break;
                case PointerEventType.Unselect:
                    if (_selecting.Remove(evt.Identifier) && _selecting.Count == 0) Fire();
                    break;
                case PointerEventType.Cancel:
                    _selecting.Remove(evt.Identifier);
                    _hovering.Remove(evt.Identifier);
                    break;
            }
        }

        private void Fire()
        {
            float now = Time.unscaledTime;
            if (now < GlobalBlockUntil || now - _lastFire < debounce) return;
            _lastFire = now;
            onPressed.Invoke();
        }

        /// <summary>Press from code (editor testing, keyboard shortcuts).</summary>
        [ContextMenu("Press")]
        public void PressFromCode()
        {
            if (interactable) Fire();
        }

        private void Update()
        {
            float k = 1f - Mathf.Exp(-animSpeed * Time.unscaledDeltaTime);
            _pressAmount = Mathf.Lerp(_pressAmount, IsPressed ? 1f : 0f, k);
            _hoverAmount = Mathf.Lerp(_hoverAmount, IsHovered && interactable ? 1f : 0f, k);

            if (face != null)
            {
                face.localPosition = _faceRestPos + Vector3.forward * (pressDepth * _pressAmount);
                face.localScale = _faceRestScale * Mathf.Lerp(1f, hoverScale, _hoverAmount);
            }
            if (glow != null)
            {
                bool show = _hoverAmount > 0.05f;
                if (glow.activeSelf != show) glow.SetActive(show);
            }
            if (faceRenderer != null)
            {
                Color c = faceColor;
                if (!interactable) c = Color.Lerp(c, new Color(0.82f, 0.84f, 0.88f), 0.7f);
                else c = Color.Lerp(c, c * (1f - pressDarken), _pressAmount);
                c.a = 1f;
                faceRenderer.GetPropertyBlock(_mpb);
                _mpb.SetColor(ColorId, c);
                faceRenderer.SetPropertyBlock(_mpb);
            }
        }
    }
}
