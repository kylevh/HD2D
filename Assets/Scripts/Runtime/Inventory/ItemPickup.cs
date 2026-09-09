using KVH.Game.UI;
using UnityEngine;

namespace KVH.Game.Inventory
{
    // Sibling to Interactable. Grants an item once, toast, then disables the interactable.
    [DisallowMultipleComponent]
    public sealed class ItemPickup : MonoBehaviour
    {
        [SerializeField] ItemDef item;
        [SerializeField] int amount = 1;
        [SerializeField] bool disableAfterPickup = true;
        [SerializeField] ItemToast toast;

        public bool TryPickup(PlayerInventory bag)
        {
            if (item == null || bag == null || amount <= 0)
                return false;

            bag.Add(item, amount);
            if (toast == null)
                toast = FindAnyObjectByType<ItemToast>();
            toast?.Show(item, amount);

            if (disableAfterPickup)
            {
                var marker = GetComponent<Interaction.Interactable>();
                if (marker != null)
                    marker.enabled = false;
                enabled = false;
            }

            return true;
        }
    }
}
