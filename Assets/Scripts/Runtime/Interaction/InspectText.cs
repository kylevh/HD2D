using UnityEngine;

namespace KVH.Game.Interaction
{
    // Flavor text on a prop. Reuses DialogueRunner — no second text UI.
    [DisallowMultipleComponent]
    public sealed class InspectText : MonoBehaviour
    {
        [SerializeField] string title = "";
        [SerializeField] [TextArea(1, 4)] string[] lines =
        {
            "An ordinary crate.",
        };

        public bool TryStart(Dialogue.DialogueRunner runner)
        {
            if (runner == null || lines == null || lines.Length == 0)
                return false;
            runner.StartLines(title, lines);
            return true;
        }
    }
}
