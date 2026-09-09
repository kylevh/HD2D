using UnityEngine;

namespace KVH.Game.Dialogue
{
    // Sibling to Interactable on an NPC. Scanner starts DialogueRunner with these lines.
    [DisallowMultipleComponent]
    public sealed class DialogueSpeaker : MonoBehaviour
    {
        [SerializeField] string speakerName = "???";
        [SerializeField] Sprite portrait;
        [SerializeField] [TextArea(1, 4)] string[] lines =
        {
            "Hello, traveler.",
            "The road ahead looks quiet… for now.",
        };

        public string SpeakerName => speakerName;
        public Sprite Portrait => portrait;
        public string[] Lines => lines;

        public bool TryStart(DialogueRunner runner)
        {
            if (runner == null || lines == null || lines.Length == 0)
                return false;
            runner.StartLines(speakerName, lines, portrait);
            return true;
        }
    }
}
