using KVH.Game.Dialogue;
using KVH.Game.Input;
using KVH.Game.Inventory;
using KVH.Game.Player;
using KVH.KylePixelator;
using UnityEngine;

namespace KVH.Game.Interaction
{
    // Walk-up focus: nearest Interactable gets X-ray silhouette + soft pulse / enter flash.
    // Interact routes to DialogueSpeaker → ItemPickup → InspectText (or advances open dialogue).
    public sealed class InteractionScanner : MonoBehaviour
    {
        [SerializeField] InputReader input;
        [SerializeField] PlayerMotor motor;
        [SerializeField] DialogueRunner dialogue;
        [SerializeField] PlayerInventory inventory;
        [Tooltip("Sample point above the feet so range feels like 'standing next to', not stepping on.")]
        [SerializeField] float originHeight = 0.9f;

        [Header("Highlight candy")]
        [Tooltip("Outline glimmer rate (cycles per second). Runs the whole time you're in range.")]
        [SerializeField] float pulseHz = 1.6f;
        [SerializeField] [Range(0.55f, 1f)] float pulseDim = 0.72f;
        [SerializeField] [Range(1f, 1.5f)] float pulseBright = 1.28f;
        [Tooltip("At pulse peaks, mix toward white for a cheap metallic glimmer.")]
        [SerializeField] [Range(0f, 0.6f)] float glimmerTowardWhite = 0.35f;
        [Tooltip("Soft fill that breathes with the pulse (always on while focused).")]
        [SerializeField] [Range(0f, 0.35f)] float pulseFill = 0.12f;
        [Tooltip("Extra fill when focus is gained; decays on top of the pulse.")]
        [SerializeField] [Range(0f, 0.6f)] float enterFlashFill = 0.32f;
        [SerializeField] float enterFlashSeconds = 0.28f;

        Interactable _current;
        float _flash; // 1 → 0 after focus gain
        float _pulsePhase;

        public Interactable Current => _current;

        void Awake()
        {
            if (input == null)
                input = FindAnyObjectByType<InputReader>();
            if (motor == null)
                motor = GetComponent<PlayerMotor>();
            if (inventory == null)
                inventory = GetComponent<PlayerInventory>();
            if (dialogue == null)
                dialogue = FindAnyObjectByType<DialogueRunner>();
        }

        void OnDisable()
        {
            _current = null;
            _flash = 0f;
            HighlightOutline.Clear();
        }

        void Update()
        {
            if (input != null && input.InteractPressed)
                TryInteract();

            if (_flash > 0f)
            {
                var dur = Mathf.Max(0.05f, enterFlashSeconds);
                _flash = Mathf.Max(0f, _flash - Time.deltaTime / dur);
            }

            if (_current != null)
                _pulsePhase += Time.deltaTime * pulseHz * Mathf.PI * 2f;
        }

        void TryInteract()
        {
            if (dialogue != null && dialogue.IsActive)
            {
                dialogue.Advance();
                return;
            }

            if (motor != null && motor.Busy != PlayerBusy.Free)
                return;

            if (_current == null)
                return;

            if (dialogue == null)
                dialogue = FindAnyObjectByType<DialogueRunner>();

            var speaker = _current.GetComponent<DialogueSpeaker>();
            if (speaker != null && speaker.TryStart(dialogue))
                return;

            var pickup = _current.GetComponent<ItemPickup>();
            if (pickup != null)
            {
                if (inventory == null)
                    inventory = GetComponent<PlayerInventory>() ?? FindAnyObjectByType<PlayerInventory>();
                if (pickup.TryPickup(inventory))
                    return;
            }

            var inspect = _current.GetComponent<InspectText>();
            if (inspect != null && inspect.TryStart(dialogue))
                return;
        }

        void LateUpdate()
        {
            var next = FindFocus();
            if (next != _current)
            {
                _current = next;
                _flash = _current != null ? 1f : 0f;
                if (_current == null)
                {
                    HighlightOutline.Clear();
                    return;
                }
            }

            if (_current == null)
                return;

            ApplyHighlight(_current);
        }

        void ApplyHighlight(Interactable target)
        {
            if (!target.UseOutline)
            {
                HighlightOutline.Clear();
                return;
            }

            // Constant glimmer while focused (not only the enter flash).
            var wave = 0.5f + 0.5f * Mathf.Sin(_pulsePhase);
            var mul = Mathf.Lerp(pulseDim, pulseBright, wave);
            var color = target.OutlineColor * mul;
            // NPCs: gentler white mix so sky rim doesn't turn metallic gold
            var glimmer = target.Kind == InteractableKind.Npc ? glimmerTowardWhite * 0.45f : glimmerTowardWhite;
            color = Color.Lerp(color, Color.white, wave * glimmer);
            color.a = target.OutlineColor.a;

            // Props keep fill pulse; NPCs stay outline-only (Talk chip owns the read).
            var fillPulse = target.Kind == InteractableKind.Npc ? 0f : pulseFill;
            var flash = target.Kind == InteractableKind.Npc ? enterFlashFill * 0.35f : enterFlashFill;
            var fill = Mathf.Max(target.Fill, fillPulse * wave + flash * _flash);
            HighlightOutline.Set(target.Renderers, color, fill);
        }

        Interactable FindFocus()
        {
            // Don't retarget while talking — keeps the conversation stable.
            if (dialogue != null && dialogue.IsActive)
                return _current;

            var origin = transform.position + Vector3.up * originHeight;
            var all = FindObjectsByType<Interactable>(FindObjectsInactive.Exclude);
            Interactable best = null;
            var bestDist = float.MaxValue;
            for (var i = 0; i < all.Length; i++)
            {
                var it = all[i];
                if (it == null || !it.isActiveAndEnabled)
                    continue;

                var d = it.DistanceTo(origin);
                if (d > it.Range || d >= bestDist)
                    continue;

                bestDist = d;
                best = it;
            }

            return best;
        }
    }
}
