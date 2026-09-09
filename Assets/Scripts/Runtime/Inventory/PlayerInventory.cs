using System.Collections.Generic;
using UnityEngine;

namespace KVH.Game.Inventory
{
    // Tiny bag of item id → count. No menu yet — toast shows grants.
    [DisallowMultipleComponent]
    public sealed class PlayerInventory : MonoBehaviour
    {
        readonly Dictionary<string, int> _counts = new();

        public int CountOf(ItemDef def)
        {
            if (def == null || string.IsNullOrEmpty(def.Id))
                return 0;
            return _counts.TryGetValue(def.Id, out var n) ? n : 0;
        }

        public void Add(ItemDef def, int amount = 1)
        {
            if (def == null || amount <= 0 || string.IsNullOrEmpty(def.Id))
                return;
            _counts.TryGetValue(def.Id, out var n);
            _counts[def.Id] = n + amount;
        }
    }
}
