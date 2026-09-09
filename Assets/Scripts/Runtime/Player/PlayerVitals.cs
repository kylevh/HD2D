using UnityEngine;

namespace KVH.Game.Player
{
    // Placeholder overworld vitals until Party/Combat own real stats.
    // Stick on the player (or a future Party root). HUD only reads this.
    public sealed class PlayerVitals : MonoBehaviour
    {
        [SerializeField] string displayName = "HERO";
        [SerializeField] int maxHp = 100;
        [SerializeField] int hp = 100;
        [SerializeField] int maxMp = 40;
        [SerializeField] int mp = 40;

        public string DisplayName => displayName;
        public int MaxHp => Mathf.Max(1, maxHp);
        public int Hp => Mathf.Clamp(hp, 0, MaxHp);
        public int MaxMp => Mathf.Max(1, maxMp);
        public int Mp => Mathf.Clamp(mp, 0, MaxMp);
        public float HpNormalized => Hp / (float)MaxHp;
        public float MpNormalized => Mp / (float)MaxMp;

        public void SetHp(int value) => hp = Mathf.Clamp(value, 0, MaxHp);
        public void SetMp(int value) => mp = Mathf.Clamp(mp, 0, MaxMp);

#if UNITY_EDITOR
        void OnValidate()
        {
            maxHp = Mathf.Max(1, maxHp);
            maxMp = Mathf.Max(1, maxMp);
            hp = Mathf.Clamp(hp, 0, maxHp);
            mp = Mathf.Clamp(mp, 0, maxMp);
        }
#endif
    }
}
