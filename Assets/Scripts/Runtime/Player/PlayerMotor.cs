using KVH.Game.Input;
using KVH.KylePixelator;
using UnityEngine;

namespace KVH.Game.Player
{
    // camera-relative move + gravity on CharacterController.
    // Dialogue/UI/battle freeze walking via Busy — they do not call Move() themselves.
    [RequireComponent(typeof(CharacterController))]
    public sealed class PlayerMotor : MonoBehaviour
    {
        [SerializeField] InputReader input;
        [Tooltip("Camera used for movement relative axes. Assign Main Camera or leave empty to use Camera.main.")]
        [SerializeField] Transform cameraTransform;
        [SerializeField] float moveSpeed = 5f;
        [SerializeField] float gravity = -20f;
        [SerializeField] float rotateSpeed = 720f;
        [SerializeField] PlayerBusy busy;

        public PlayerBusy Busy
        {
            get => busy;
            set => busy = value;
        }

        CharacterController _controller;
        float _verticalVelocity;
        Transform _cameraTransform;

        void Awake()
        {
            _controller = GetComponent<CharacterController>();
            if (input == null)
                input = FindAnyObjectByType<InputReader>();

            CacheCamera();
        }

        void CacheCamera()
        {
            if (cameraTransform != null)
            {
                _cameraTransform = cameraTransform;
                return;
            }

            if (UnityEngine.Camera.main != null)
                _cameraTransform = UnityEngine.Camera.main.transform;
        }

        void Reset()
        {
            // use the pivot (GameCamera) for move axes if we have one
            var rig = FindAnyObjectByType<CameraRig>();
            if (rig != null)
                cameraTransform = rig.transform;
        }

        void Update()
        {
            if (input == null)
                return;

            if (_cameraTransform == null)
                CacheCamera();

            var move = busy == PlayerBusy.Free ? input.Move : Vector2.zero;
            var planar = Vector3.zero;

            if (_cameraTransform != null && move.sqrMagnitude > 0.0001f)
            {
                var forward = _cameraTransform.forward;
                var right = _cameraTransform.right;
                forward.y = 0f;
                right.y = 0f;
                forward.Normalize();
                right.Normalize();
                planar = right * move.x + forward * move.y;
                if (planar.sqrMagnitude > 1f)
                    planar.Normalize();
                planar *= moveSpeed;

                var face = planar.normalized;
                if (face.sqrMagnitude > 0.0001f)
                {
                    var targetRot = Quaternion.LookRotation(face, Vector3.up);
                    transform.rotation = Quaternion.RotateTowards(
                        transform.rotation,
                        targetRot,
                        rotateSpeed * Time.deltaTime);
                }
            }

            if (_controller.isGrounded && _verticalVelocity < 0f)
                _verticalVelocity = -1f;
            else
                _verticalVelocity += gravity * Time.deltaTime;

            _controller.Move((planar + Vector3.up * _verticalVelocity) * Time.deltaTime);
        }
    }
}
