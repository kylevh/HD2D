using UnityEngine;

namespace KVH.Game.Inventory
{
    // Data-only item. Chests write; bag stores counts; UI displays.
    [CreateAssetMenu(menuName = "KVH/Inventory/Item Def", fileName = "Item")]
    public sealed class ItemDef : ScriptableObject
    {
        [SerializeField] string id = "item";
        [SerializeField] string displayName = "Item";
        [SerializeField] Sprite icon;

        public string Id => id;
        public string DisplayName => string.IsNullOrEmpty(displayName) ? name : displayName;
        public Sprite Icon => icon;
    }
}
