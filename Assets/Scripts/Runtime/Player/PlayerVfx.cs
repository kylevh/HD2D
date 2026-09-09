using KVH.Game.Input;
using UnityEngine;

namespace KVH.Game.Player
{
    // overworld juice: dust on steps, slash on Attack. not a combat system.
    [DisallowMultipleComponent]
    public sealed class PlayerVfx : MonoBehaviour
    {
        [SerializeField] InputReader input;
        [SerializeField] CharacterController controller;
        [SerializeField] ParticleSystem footDust;
        [SerializeField] ParticleSystem slash;
        [SerializeField] float stepMeters = 1.15f;
        [SerializeField] float minSpeed = 0.55f;

        float _carry;

        void Awake()
        {
            if (controller == null)
                controller = GetComponent<CharacterController>();
            if (input == null)
                input = FindAnyObjectByType<InputReader>();
        }

        void Update()
        {
            if (controller != null)
            {
                var v = controller.velocity;
                v.y = 0f;
                var speed = v.magnitude;
                if (speed >= minSpeed)
                {
                    _carry += speed * Time.deltaTime;
                    if (_carry >= stepMeters)
                    {
                        _carry = 0f;
                        Burst(footDust);
                    }
                }
                else
                {
                    _carry = 0f;
                }
            }

            if (input != null && input.AttackPressed)
                Burst(slash);
        }

        static void Burst(ParticleSystem ps)
        {
            if (ps == null)
                return;
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ps.Play();
        }
    }
}
