using KVH.Game.Flow;
using KVH.Game.Input;
using KVH.Game.Player;
using KVH.KylePixelator;
using UnityEngine;

namespace KVH.Game.Camera
{
    // follow + shake on the pivot, plus 8-way yaw (Q/E, bumpers). CameraRig owns
    // the pixel cam's local XY and locks pitch/roll — don't write those here.
    [DefaultExecutionOrder(-100)]
    public sealed class GameCamera : MonoBehaviour
    {
        const float YawSnapEpsilon = 0.05f;

        [Header("Follow")]
        [SerializeField] Transform target;
        [SerializeField] float height = 1f;
        [SerializeField] float damp;
        [SerializeField] bool snapFollow;

        [Header("Shake")]
        [SerializeField] float shakeDamping = 8f;

        [Header("Orbit")]
        [SerializeField] InputReader input;
        [SerializeField] CameraRig rig;
        [SerializeField] GameFlow flow;
        [SerializeField] PlayerMotor motor;
        [Tooltip("Compass step. 45 = 8 views (4 iso diagonals + 4 axis-aligned).")]
        [SerializeField] float yawStep = 45f;
        [Tooltip("Higher = snappier. 0 = instant.")]
        [SerializeField] float yawDamp = 14f;
        [SerializeField] float holdRepeatDelay = 0.28f;
        [SerializeField] float holdRepeatRate = 0.16f;

        Vector3 _shakeOffset;
        Vector3 _shakeVelocity;
        CameraOcclusionFade _occlusionFade;
        float _currentYaw;
        float _targetYaw;
        int _yawIndex;
        int _holdDir;
        float _holdTimer;
        bool _yawReady;

        public float Height => height;

        public void SetTarget(Transform followTarget) => target = followTarget;

        public void AddImpulse(Vector3 worldImpulse) => _shakeVelocity += worldImpulse;

        void Awake()
        {
            _occlusionFade = GetComponent<CameraOcclusionFade>();
            if (rig == null)
                rig = GetComponent<CameraRig>();
            if (input == null)
                input = FindAnyObjectByType<InputReader>();
            if (flow == null)
                flow = FindAnyObjectByType<GameFlow>();
            if (motor == null)
                motor = FindAnyObjectByType<PlayerMotor>();
        }

        void OnEnable() => InitYawFromSettings();

        void Update()
        {
            if (!Application.isPlaying)
                return;
            TickOrbitInput();
        }

        void LateUpdate()
        {
            var pos = transform.position;

            if (target != null)
            {
                if (_occlusionFade != null)
                    _occlusionFade.SetTarget(target);

                var desired = FollowPoint(target);
                if (snapFollow || damp <= 0f)
                    pos = desired;
                else
                {
                    var t = 1f - Mathf.Exp(-damp * Time.deltaTime);
                    pos = Vector3.Lerp(pos, desired, t);
                }
            }

            var dt = Time.deltaTime;
            if (dt > 0f)
            {
                _shakeOffset += _shakeVelocity * dt;
                _shakeVelocity = Vector3.Lerp(_shakeVelocity, Vector3.zero, 1f - Mathf.Exp(-shakeDamping * dt));
                _shakeOffset = Vector3.Lerp(_shakeOffset, Vector3.zero, 1f - Mathf.Exp(-shakeDamping * dt));
                if (_shakeOffset.sqrMagnitude < 1e-8f && _shakeVelocity.sqrMagnitude < 1e-8f)
                {
                    _shakeOffset = Vector3.zero;
                    _shakeVelocity = Vector3.zero;
                }
            }

            TickOrbitYaw(dt);
            transform.position = pos + _shakeOffset;
        }

        // world Y = player Y + offset. offset is rounded to whole texels along camera.up
        // so feet and pivot hop on the same screen-Y frame (iso pitch couples world Y into screen-Y).
        Vector3 FollowPoint(Transform t)
        {
            var h = height;
            if (rig != null)
            {
                var upp = rig.UnitsPerPixel;
                var along = Vector3.Dot(Vector3.up, rig.transform.up);
                if (upp > 0f && Mathf.Abs(along) > 0.01f)
                {
                    var n = Mathf.Max(1, Mathf.RoundToInt(height * along / upp));
                    h = n * upp / along;
                }
            }

            return new Vector3(t.position.x, t.position.y + h, t.position.z);
        }

        void InitYawFromSettings()
        {
            if (rig == null)
                rig = GetComponent<CameraRig>();

            var start = 45f;
            if (rig != null && rig.Settings != null)
                start = rig.Settings.fixedEuler.y;

            var step = Mathf.Max(1f, yawStep);
            _yawIndex = WrapIndex(Mathf.RoundToInt(start / step), DirCount);
            _targetYaw = _currentYaw = _yawIndex * step;
            _yawReady = true;
            _holdDir = 0;
            rig?.SetYaw(_currentYaw);
        }

        void TickOrbitInput()
        {
            if (input == null || !CanOrbit())
            {
                _holdDir = 0;
                return;
            }

            var pressed = 0;
            if (input.CameraLeftPressed)
                pressed = -1;
            else if (input.CameraRightPressed)
                pressed = 1;

            if (pressed != 0)
            {
                StepYaw(pressed);
                _holdDir = pressed;
                _holdTimer = holdRepeatDelay;
                return;
            }

            var held = 0;
            if (input.CameraLeftHeld)
                held = -1;
            else if (input.CameraRightHeld)
                held = 1;

            if (held == 0 || held != _holdDir)
            {
                _holdDir = 0;
                return;
            }

            _holdTimer -= Time.deltaTime;
            if (_holdTimer > 0f)
                return;

            StepYaw(held);
            _holdTimer = holdRepeatRate;
        }

        void TickOrbitYaw(float dt)
        {
            if (!_yawReady)
                InitYawFromSettings();

            if (yawDamp <= 0f || dt <= 0f)
                _currentYaw = _targetYaw;
            else
            {
                var t = 1f - Mathf.Exp(-yawDamp * dt);
                _currentYaw = Mathf.LerpAngle(_currentYaw, _targetYaw, t);
                if (Mathf.Abs(Mathf.DeltaAngle(_currentYaw, _targetYaw)) < YawSnapEpsilon)
                    _currentYaw = _targetYaw;
            }

            rig?.SetYaw(_currentYaw);
        }

        void StepYaw(int dir)
        {
            var count = DirCount;
            _yawIndex = WrapIndex(_yawIndex + dir, count);
            _targetYaw = _yawIndex * Mathf.Max(1f, yawStep);
        }

        bool CanOrbit()
        {
            if (flow != null && flow.Mode != GameMode.Exploration)
                return false;
            if (motor != null && motor.Busy != PlayerBusy.Free)
                return false;
            return true;
        }

        int DirCount => Mathf.Max(1, Mathf.RoundToInt(360f / Mathf.Max(1f, yawStep)));

        static int WrapIndex(int i, int count)
        {
            var m = i % count;
            return m < 0 ? m + count : m;
        }
    }
}
