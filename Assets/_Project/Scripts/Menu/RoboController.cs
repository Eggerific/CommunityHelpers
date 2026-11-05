using UnityEngine;

namespace CommunityHelpers
{
    /// <summary>
    /// Robo, the friendly robot on the table. Built from separate parts (body, head, arms, antenna, eyes),
    /// all animated in code:
    /// - Idle: gentle bob and sway, arms swing, antenna wobbles, eyes blink, head and pupils follow the player.
    /// - Actions: Wave, Jump, Yes (nod), ThumbsUp, plus a quick "surprised" eye pop when cheering.
    /// Robo faces -Z in its own space (toward the player).
    /// </summary>
    public class RoboController : MonoBehaviour
    {
        public enum Cheer { ThumbsUp, Jump, Yes }
        private enum Action { None, Wave, Jump, Yes, ThumbsUp }

        [Header("Parts (filled in by the scene builder)")]
        [SerializeField] private Transform rig;        // everything; moves for jumps
        [SerializeField] private Transform body;
        [SerializeField] private Transform head;
        [SerializeField] private Transform armLeft;    // shoulder pivot on Robo's -X side
        [SerializeField] private Transform armRight;   // shoulder pivot on Robo's +X side (the waving arm)
        [SerializeField] private Transform antenna;
        [SerializeField] private Transform eyeLeft;
        [SerializeField] private Transform eyeRight;
        [SerializeField] private Transform pupilLeft;
        [SerializeField] private Transform pupilRight;
        [SerializeField] private Renderer heartLight;
        [SerializeField] private GameObject mouthSmile;
        [SerializeField] private GameObject mouthOh;

        [Header("Pick up (Meta Interaction SDK Grabbable on this object)")]
        [Tooltip("The Grabbable (an IPointable). Robo reacts when it is grabbed and flies home when released.")]
        [SerializeField] private MonoBehaviour grabbable;
        [SerializeField] private float returnTime = 0.9f;
        [SerializeField] private float returnHop = 0.12f;

        [Header("Behavior")]
        [Tooltip("Wave hello this long after the scene starts (after the board pops in).")]
        [SerializeField] private float greetDelay = 1.3f;
        [SerializeField] private Vector2 fidgetEvery = new(10f, 18f);
        [SerializeField] private Vector2 blinkEvery = new(2.5f, 5.5f);
        [SerializeField] private float headFollow = 0.6f;    // 0 = never look at the player, 1 = fully
        [SerializeField] private float pupilTravel = 0.009f; // meters (in Robo space)

        private Transform _target;
        private Vector3 _rigRest, _bodyRest, _pupilLRest, _pupilRRest;
        private Quaternion _headRest, _armLRest, _armRRest, _antennaRest, _bodyRotRest;
        private Vector3 _eyeScale;
        private Action _action;
        private float _actionT, _actionLen;
        private float _nextBlink, _blinkT = -1f, _nextFidget, _surprised;
        private Vector2 _look; // smoothed head yaw/pitch (degrees)
        private MaterialPropertyBlock _mpb;
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        // Pick up
        private int _grabs;
        private bool Held => _grabs > 0;
        private float _heldAmount;                   // 0..1, eases the "Wheee!" pose in and out
        private Vector3 _homePos;
        private Quaternion _homeRot;
        private bool _returning;
        private float _returnT;
        private Vector3 _returnFromPos;
        private Quaternion _returnFromRot;

        private void Awake()
        {
            _rigRest = rig.localPosition;
            _bodyRest = body.localPosition;
            _bodyRotRest = body.localRotation;
            _headRest = head.localRotation;
            _armLRest = armLeft.localRotation;
            _armRRest = armRight.localRotation;
            _antennaRest = antenna.localRotation;
            _eyeScale = eyeLeft.localScale;
            _pupilLRest = pupilLeft.localPosition;
            _pupilRRest = pupilRight.localPosition;
            _mpb = new MaterialPropertyBlock();
        }

        private void OnEnable()
        {
            if (grabbable is Oculus.Interaction.IPointable p) p.WhenPointerEventRaised += OnGrabEvent;
        }

        private void OnDisable()
        {
            if (grabbable is Oculus.Interaction.IPointable p) p.WhenPointerEventRaised -= OnGrabEvent;
        }

        private void OnGrabEvent(Oculus.Interaction.PointerEvent evt)
        {
            switch (evt.Type)
            {
                case Oculus.Interaction.PointerEventType.Select:
                    _grabs++;
                    _returning = false;
                    _action = Action.None;
                    _surprised = 1f;
                    break;
                case Oculus.Interaction.PointerEventType.Unselect:
                case Oculus.Interaction.PointerEventType.Cancel:
                    if (_grabs > 0 && --_grabs == 0) StartReturn();
                    break;
            }
        }

        private void StartReturn()
        {
            _returning = true;
            _returnT = 0f;
            _returnFromPos = transform.localPosition;
            _returnFromRot = transform.localRotation;
        }

        private void Start()
        {
            _homePos = transform.localPosition;
            _homeRot = transform.localRotation;
            var cam = Camera.main;
            if (cam != null) _target = cam.transform;
            _nextBlink = Time.time + Random.Range(blinkEvery.x, blinkEvery.y);
            _nextFidget = Time.time + greetDelay + Random.Range(fidgetEvery.x, fidgetEvery.y);
            Invoke(nameof(Wave), greetDelay);
        }

        // ---------------------------------------------------------------- Public API

        /// <summary>React to a button press.</summary>
        public void DoCheer(Cheer cheer)
        {
            Begin(cheer switch { Cheer.Jump => Action.Jump, Cheer.Yes => Action.Yes, _ => Action.ThumbsUp });
            _surprised = 1f;
        }

        public void Wave() => Begin(Action.Wave);

        private void Begin(Action a)
        {
            _action = a;
            _actionT = 0f;
            _actionLen = a switch { Action.Wave => 2.0f, Action.Jump => 0.9f, Action.Yes => 1.2f, Action.ThumbsUp => 1.4f, _ => 0f };
            _nextFidget = Time.time + Random.Range(fidgetEvery.x, fidgetEvery.y);
        }

        // ---------------------------------------------------------------- Animation

        private void Update()
        {
            float t = Time.time, dt = Time.deltaTime;

            // ---- Picked up / flying home
            _heldAmount = Mathf.MoveTowards(_heldAmount, Held ? 1f : 0f, dt * 5f);
            if (Held) _nextFidget = t + Random.Range(fidgetEvery.x, fidgetEvery.y);
            if (_returning)
            {
                _returnT += dt;
                float r = Mathf.Clamp01(_returnT / returnTime);
                float e = Ease(r);
                transform.localPosition = Vector3.Lerp(_returnFromPos, _homePos, e) + Vector3.up * (returnHop * 4f * r * (1f - r));
                transform.localRotation = Quaternion.Slerp(_returnFromRot, _homeRot, e);
                if (r >= 1f)
                {
                    _returning = false;
                    Begin(Action.Yes); // happy "phew, home again" nod
                }
            }

            if (!Held && !_returning && _action == Action.None && t >= _nextFidget)
                Begin(Random.value < 0.5f ? Action.Wave : Action.Yes);

            float p = 0f;
            if (_action != Action.None)
            {
                _actionT += dt;
                p = Mathf.Clamp01(_actionT / _actionLen);
                if (_actionT >= _actionLen) _action = Action.None;
            }

            // ---- Idle layer
            float bob = Mathf.Sin(t * 2.2f) * 0.004f;
            float sway = Mathf.Sin(t * 1.1f) * 2f;
            float armSwing = Mathf.Sin(t * 1.6f) * 4f;
            Vector3 rigOffset = Vector3.zero;
            float squash = 1f;
            Quaternion armL = _armLRest * Quaternion.Euler(armSwing * 0.5f, 0, -6f);
            Quaternion armR = _armRRest * Quaternion.Euler(-armSwing * 0.5f, 0, 6f);
            float nod = 0f, tilt = 0f;

            // ---- Action layer
            switch (_action)
            {
                case Action.Wave:
                {
                    float up = Ease(Mathf.InverseLerp(0f, 0.15f, p)) * (1f - Ease(Mathf.InverseLerp(0.85f, 1f, p)));
                    float wiggle = Mathf.Sin(_actionT * 12f) * 22f * up;
                    armR = _armRRest * Quaternion.Euler(0, 0, 145f * up + wiggle);
                    tilt = 8f * up;
                    break;
                }
                case Action.Jump:
                {
                    float hop = 4f * p * (1f - p);
                    rigOffset = Vector3.up * (0.07f * hop);
                    squash = 1f - 0.08f * Mathf.Sin(p * Mathf.PI * 2f) * (1f - hop); // squash at takeoff and landing
                    float arms = Mathf.Sin(p * Mathf.PI);
                    armL = _armLRest * Quaternion.Euler(0, 0, -150f * arms);
                    armR = _armRRest * Quaternion.Euler(0, 0, 150f * arms);
                    break;
                }
                case Action.Yes:
                    nod = -12f * Mathf.Sin(p * Mathf.PI * 6f) * Mathf.Sin(p * Mathf.PI);
                    break;
                case Action.ThumbsUp:
                {
                    float up = Ease(Mathf.InverseLerp(0f, 0.2f, p)) * (1f - Ease(Mathf.InverseLerp(0.8f, 1f, p)));
                    armR = _armRRest * Quaternion.Euler(80f * up, 0, 20f * up);
                    tilt = -10f * up;
                    break;
                }
            }

            // ---- "Wheee!" pose while held: arms up and waving, chin up, head wiggle
            float held = Ease(_heldAmount);
            if (held > 0f)
            {
                float flail = Mathf.Sin(t * 9f) * 25f;
                armL = Quaternion.Slerp(armL, _armLRest * Quaternion.Euler(0, 0, -140f + flail), held);
                armR = Quaternion.Slerp(armR, _armRRest * Quaternion.Euler(0, 0, 140f - flail), held);
                nod += 10f * held;
                tilt += Mathf.Sin(t * 4f) * 6f * held;
            }
            if (mouthSmile != null && mouthSmile.activeSelf == held > 0.5f) mouthSmile.SetActive(held <= 0.5f);
            if (mouthOh != null && mouthOh.activeSelf != held > 0.5f) mouthOh.SetActive(held > 0.5f);

            rig.localPosition = _rigRest + rigOffset;
            body.localPosition = _bodyRest + Vector3.up * bob;
            body.localRotation = _bodyRotRest * Quaternion.Euler(0, 0, sway * 0.5f);
            body.localScale = new Vector3(2f - squash, squash, 2f - squash);
            armLeft.localRotation = Quaternion.Slerp(armLeft.localRotation, armL, 1f - Mathf.Exp(-18f * dt));
            armRight.localRotation = Quaternion.Slerp(armRight.localRotation, armR, 1f - Mathf.Exp(-18f * dt));

            // ---- Head and eyes follow the player
            Vector3 dirLocal = Vector3.back;
            if (_target != null)
            {
                Vector3 d = head.parent.InverseTransformDirection((_target.position - head.position).normalized);
                if (d.sqrMagnitude > 0.0001f) dirLocal = d.normalized;
            }
            float yaw = Mathf.Atan2(-dirLocal.x, -dirLocal.z) * Mathf.Rad2Deg;
            float pitch = Mathf.Asin(Mathf.Clamp(dirLocal.y, -1f, 1f)) * Mathf.Rad2Deg;
            var lookTarget = new Vector2(Mathf.Clamp(yaw, -30f, 30f), Mathf.Clamp(pitch, -20f, 25f)) * headFollow;
            _look = Vector2.Lerp(_look, lookTarget, 1f - Mathf.Exp(-4f * dt));
            head.localRotation = _headRest * Quaternion.Euler(_look.y + nod, _look.x, tilt + sway);

            Vector3 eyeDir = head.InverseTransformDirection(_target != null ? (_target.position - head.position).normalized : -head.forward);
            Vector3 pupilOffset = new Vector3(Mathf.Clamp(eyeDir.x, -1f, 1f), Mathf.Clamp(eyeDir.y, -1f, 1f), 0f) * pupilTravel;
            pupilLeft.localPosition = _pupilLRest + pupilOffset;
            pupilRight.localPosition = _pupilRRest + pupilOffset;

            // ---- Blink and surprise
            if (_blinkT < 0f && t >= _nextBlink) _blinkT = 0f;
            float lid = 1f;
            if (_blinkT >= 0f)
            {
                _blinkT += dt;
                lid = Mathf.Lerp(1f, 0.1f, Mathf.Sin(Mathf.Clamp01(_blinkT / 0.14f) * Mathf.PI));
                if (_blinkT >= 0.14f)
                {
                    _blinkT = -1f;
                    _nextBlink = t + Random.Range(blinkEvery.x, blinkEvery.y);
                }
            }
            _surprised = Mathf.MoveTowards(_surprised, 0f, dt * 1.8f);
            float pop = 1f + Mathf.Max(0.25f * Mathf.SmoothStep(0f, 1f, _surprised), 0.3f * held); // wide "Wheee!" eyes when held
            if (held > 0.5f) lid = 1f; // no blinking while flying
            var eyeScale = new Vector3(_eyeScale.x * pop, _eyeScale.y * pop * lid, _eyeScale.z);
            eyeLeft.localScale = eyeScale;
            eyeRight.localScale = eyeScale;

            // ---- Antenna wobble (extra bounce during actions)
            float kick = (_action != Action.None ? 10f : 0f) + 16f * held;
            float wiggleSpeed = Mathf.Lerp(3.3f, 11f, held);
            antenna.localRotation = _antennaRest * Quaternion.Euler(Mathf.Sin(t * 2.7f) * 4f, 0, Mathf.Sin(t * wiggleSpeed) * (6f + kick));

            // ---- Heart light pulse
            if (heartLight != null)
            {
                float glow = 0.75f + 0.25f * Mathf.Sin(t * 3f);
                Color c = Color.Lerp((Color)UIColors.Coral * 0.7f, new Color(1f, 0.75f, 0.7f), glow);
                c.a = 1f;
                heartLight.GetPropertyBlock(_mpb);
                _mpb.SetColor(ColorId, c);
                heartLight.SetPropertyBlock(_mpb);
            }
        }

        private static float Ease(float x) => x * x * (3f - 2f * x);
    }
}
