using UnityEngine;

namespace KVH.Game.Player
{
    // Temporary driver: push CharacterController planar speed into the Hero animator.
    // Replace when the real animation/state system lands.
    [DisallowMultipleComponent]
    public sealed class HeroAnimDriver : MonoBehaviour
    {
        [SerializeField] Animator animator;
        [SerializeField] CharacterController controller;
        [SerializeField] float runThreshold = 0.15f;

        static readonly int SpeedHash = Animator.StringToHash("Speed");
        static readonly int MovingHash = Animator.StringToHash("Moving");

        void Awake()
        {
            if (animator == null)
                animator = GetComponentInChildren<Animator>(true);
            if (controller == null)
                controller = GetComponent<CharacterController>();
        }

        void Update()
        {
            if (animator == null)
                return;

            var v = controller != null ? controller.velocity : Vector3.zero;
            v.y = 0f;
            var speed = v.magnitude;
            animator.SetFloat(SpeedHash, speed);
            animator.SetBool(MovingHash, speed > runThreshold);
        }
    }
}
