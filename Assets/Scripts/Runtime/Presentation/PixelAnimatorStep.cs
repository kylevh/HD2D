using UnityEngine;

namespace KVH.Game.Presentation
{
    // Steps Mecanim at a fixed FPS so clips hold poses between ticks (pixel-art cadence),
    // while gameplay/physics keep running at full framerate.
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(50)]
    public sealed class PixelAnimatorStep : MonoBehaviour
    {
        static readonly int MovingHash = Animator.StringToHash("Moving");

        [SerializeField] Animator animator;
        [SerializeField, Range(4, 30)] int framesPerSecond = 8;
        [Tooltip("Idle poses. 3 = short pixel loop.")]
        [SerializeField, Range(1, 4)] int idleFrames = 3;
        [Tooltip("How often idle swaps pose. 2 = one bounce per second.")]
        [SerializeField, Range(1, 6)] int idleFramesPerSecond = 2;
        [Tooltip("If the game hitchs, don't dump many anim frames in one go.")]
        [SerializeField] int maxStepsPerFrame = 2;

        float _carry;
        int _idleIndex;
        bool _wasIdle;

        public int FramesPerSecond
        {
            get => framesPerSecond;
            set => framesPerSecond = Mathf.Clamp(value, 1, 60);
        }

        void Awake()
        {
            if (animator == null)
                animator = GetComponentInChildren<Animator>(true);
        }

        void OnEnable()
        {
            if (animator == null)
                animator = GetComponentInChildren<Animator>(true);
            if (animator == null)
                return;

            animator.enabled = false;
            _carry = 0f;
            _wasIdle = false;
        }

        void OnDisable()
        {
            if (animator != null)
                animator.enabled = true;
        }

        void LateUpdate()
        {
            if (animator == null || framesPerSecond <= 0)
                return;

            if (animator.enabled)
                animator.enabled = false;

            var idle = IsIdle();
            if (idle != _wasIdle)
            {
                _carry = 0f;
                _idleIndex = 0;
                _wasIdle = idle;
                if (idle)
                    SampleIdlePose();
            }

            if (idle)
                StepIdle();
            else
                StepClips();
        }

        bool IsIdle()
        {
            if (animator.IsInTransition(0))
                return false;
            if (animator.GetBool(MovingHash))
                return false;
            return animator.GetCurrentAnimatorStateInfo(0).IsName("Idle");
        }

        void StepIdle()
        {
            var fps = Mathf.Max(1, idleFramesPerSecond);
            var hold = 1f / fps;
            _carry += Time.deltaTime;

            var swapped = false;
            while (_carry >= hold)
            {
                _carry -= hold;
                _idleIndex++;
                swapped = true;
            }

            if (swapped)
                SampleIdlePose();
        }

        void SampleIdlePose()
        {
            var n = Mathf.Max(1, idleFrames);
            var i = ((_idleIndex % n) + n) % n;
            var t = (i + 0.5f) / n;
            animator.Play("Idle", 0, t);
            animator.Update(0f);
        }

        void StepClips()
        {
            var step = 1f / framesPerSecond;
            _carry += Time.deltaTime;

            var steps = 0;
            while (_carry >= step && steps < maxStepsPerFrame)
            {
                animator.Update(step);
                _carry -= step;
                steps++;
            }

            if (_carry > step)
                _carry = step;
        }
    }
}
