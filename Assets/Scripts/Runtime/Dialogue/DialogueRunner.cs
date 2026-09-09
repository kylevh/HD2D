using KVH.Game.Player;
using KVH.Game.UI;
using UnityEngine;

namespace KVH.Game.Dialogue
{
    // Linear talk / inspect lines. Lives on GameFlow. Sets PlayerBusy.Talking; UI only displays.
    [DisallowMultipleComponent]
    public sealed class DialogueRunner : MonoBehaviour
    {
        [SerializeField] DialogueBox box;
        [SerializeField] PlayerMotor motor;

        string _speaker;
        string[] _lines;
        Sprite _portrait;
        int _index = -1;

        public bool IsActive => _index >= 0 && _lines != null;

        void Awake()
        {
            if (box == null)
                box = FindAnyObjectByType<DialogueBox>();
            if (motor == null)
            {
                var player = GameObject.FindGameObjectWithTag("Player");
                if (player != null)
                    motor = player.GetComponent<PlayerMotor>();
            }
        }

        public void StartLines(string speaker, string[] lines, Sprite portrait = null)
        {
            if (lines == null || lines.Length == 0)
                return;
            if (IsActive)
                return;

            _speaker = string.IsNullOrEmpty(speaker) ? "" : speaker;
            _lines = lines;
            _portrait = portrait;
            _index = 0;
            if (motor != null)
                motor.Busy = PlayerBusy.Talking;
            ShowCurrent();
        }

        // Interact while talking: next line, or close on the last.
        public void Advance()
        {
            if (!IsActive)
                return;

            _index++;
            if (_index >= _lines.Length)
            {
                Close();
                return;
            }

            ShowCurrent();
        }

        public void Close()
        {
            _lines = null;
            _index = -1;
            _speaker = null;
            _portrait = null;
            box?.Hide();
            if (motor != null && motor.Busy == PlayerBusy.Talking)
                motor.Busy = PlayerBusy.Free;
        }

        void ShowCurrent()
        {
            if (box == null)
                box = FindAnyObjectByType<DialogueBox>();
            box?.Show(_speaker, _lines[_index], _portrait);
        }
    }
}
