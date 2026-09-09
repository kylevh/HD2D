using UnityEngine;

namespace KVH.Game.Flow
{
    // Scene bootstrap for mode only — not a god GameManager. Wire in the inspector;
    // one-shot Find in Awake is OK. Combat/UI set Mode; they do not own the pawn.
    public sealed class GameFlow : MonoBehaviour
    {
        [SerializeField] GameMode mode = GameMode.Exploration;

        public GameMode Mode => mode;

        public void SetMode(GameMode next) => mode = next;
    }
}
