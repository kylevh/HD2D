using UnityEngine;

namespace KVH.Game.Player
{
    // Imported Hero clips still fire FootL/FootR (pack footstep hooks).
    // No-op for now — swap in SFX later without touching the FBXs.
    [DisallowMultipleComponent]
    public sealed class HeroAnimEvents : MonoBehaviour
    {
        public void FootL() { }

        public void FootR() { }
    }
}
