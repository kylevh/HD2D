using UnityEngine;
using UnityEngine.InputSystem;

namespace KVH.Game.Input
{
    // Player map: move, attack, interact, camera left/right, menu.
    // Full UI map (navigate menus) lands later — don't put menu logic in this reader.
    public sealed class InputReader : MonoBehaviour
    {
        [SerializeField] InputActionAsset actions;

        InputActionMap _player;
        InputAction _move;
        InputAction _attack;
        InputAction _interact;
        InputAction _cameraLeft;
        InputAction _cameraRight;
        InputAction _menu;

        public Vector2 Move => _move != null ? _move.ReadValue<Vector2>() : Vector2.zero;
        public bool AttackPressed => _attack != null && _attack.WasPressedThisFrame();
        public bool InteractPressed => _interact != null && _interact.WasPressedThisFrame();
        public bool CameraLeftPressed => _cameraLeft != null && _cameraLeft.WasPressedThisFrame();
        public bool CameraRightPressed => _cameraRight != null && _cameraRight.WasPressedThisFrame();
        public bool CameraLeftHeld => _cameraLeft != null && _cameraLeft.IsPressed();
        public bool CameraRightHeld => _cameraRight != null && _cameraRight.IsPressed();
        public bool MenuPressed => _menu != null && _menu.WasPressedThisFrame();

        void OnEnable()
        {
            if (actions == null)
                return;

            _player = actions.FindActionMap("Player", throwIfNotFound: true);
            _move = _player.FindAction("Move", throwIfNotFound: true);
            _attack = _player.FindAction("Attack", throwIfNotFound: true);
            _interact = _player.FindAction("Interact", throwIfNotFound: true);
            _cameraLeft = _player.FindAction("CameraLeft", throwIfNotFound: true);
            _cameraRight = _player.FindAction("CameraRight", throwIfNotFound: true);
            _menu = _player.FindAction("Menu", throwIfNotFound: true);
            _player.Enable();
        }

        void OnDisable()
        {
            _player?.Disable();
        }
    }
}
