using KVH.Game.Interaction;
using KVH.KylePixelator;
using UnityEngine;
using UnityEngine.UI;

namespace KVH.Game.UI
{
    // Floating prompt above the focused interactable. Screen-space overlay, mapped through
    // LowResOutput's letterbox. Props → gold "F"; NPCs → sky "Talk" (no body outline).
    [DisallowMultipleComponent]
    public sealed class InteractPrompt : MonoBehaviour
    {
        static readonly Color PropChip = new Color(0.08f, 0.07f, 0.12f, 0.82f);
        static readonly Color PropText = new Color(0.97f, 0.90f, 0.42f, 1f);
        static readonly Color NpcChip = new Color(0.10f, 0.14f, 0.22f, 0.88f);
        static readonly Color NpcText = new Color(0.78f, 0.90f, 1f, 1f);

        [SerializeField] InteractionScanner scanner;
        [SerializeField] LowResOutput lowRes;
        [SerializeField] RectTransform promptRoot;
        [SerializeField] Image chip;
        [SerializeField] Text label;
        [SerializeField] float bobPixels = 5f;
        [SerializeField] float bobHz = 1.35f;

        Canvas _canvas;
        RectTransform _canvasRect;
        float _bobPhase;
        bool _built;
        InteractableKind _styledKind = (InteractableKind)(-1);

        void Awake() => EnsureUi();

        void OnEnable()
        {
            EnsureUi();
            ResolveScanner();
            if (lowRes == null)
            {
                var rig = FindAnyObjectByType<CameraRig>();
                if (rig != null && rig.PixelCamera != null)
                    lowRes = rig.PixelCamera.GetComponent<LowResOutput>();
                if (lowRes == null)
                    lowRes = FindAnyObjectByType<LowResOutput>();
            }
        }

        void LateUpdate()
        {
            EnsureUi();
            // Prefab may still point at a deactivated capsule Player scanner.
            if (scanner == null || !scanner.isActiveAndEnabled)
                ResolveScanner();

            var focus = scanner != null ? scanner.Current : null;
            if (focus == null || promptRoot == null || _canvasRect == null)
            {
                if (promptRoot != null)
                    promptRoot.gameObject.SetActive(false);
                return;
            }

            if (lowRes == null || !lowRes.TryWorldToOverlayScreen(focus.PromptWorldPosition, out var screen))
            {
                promptRoot.gameObject.SetActive(false);
                return;
            }

            _bobPhase += Time.deltaTime * bobHz * Mathf.PI * 2f;
            screen.y += Mathf.Sin(_bobPhase) * bobPixels;

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _canvasRect, screen, null, out var local))
            {
                promptRoot.gameObject.SetActive(false);
                return;
            }

            ApplyStyle(focus);
            promptRoot.gameObject.SetActive(true);
            promptRoot.anchoredPosition = local;
            if (label != null && label.text != focus.PromptLabel)
                label.text = focus.PromptLabel;
        }

        void ResolveScanner()
        {
            if (scanner != null && scanner.isActiveAndEnabled)
                return;
            scanner = FindAnyObjectByType<InteractionScanner>();
        }

        void ApplyStyle(Interactable focus)
        {
            if (focus.Kind == _styledKind)
                return;

            _styledKind = focus.Kind;
            var npc = focus.Kind == InteractableKind.Npc;
            if (chip != null)
                chip.color = npc ? NpcChip : PropChip;
            if (label != null)
            {
                label.color = npc ? NpcText : PropText;
                label.fontSize = npc ? 18 : 28;
                promptRoot.sizeDelta = npc ? new Vector2(72f, 36f) : new Vector2(44f, 44f);
            }
        }

        void EnsureUi()
        {
            if (_built && promptRoot != null && label != null)
                return;

            _canvas = GetComponentInParent<Canvas>();
            if (_canvas == null)
                _canvas = FindAnyObjectByType<Canvas>();
            if (_canvas == null)
                return;

            _canvasRect = _canvas.transform as RectTransform;

            var hudTf = transform.Find("Hud") ?? _canvas.transform.Find("Hud");
            RectTransform parentRt = hudTf as RectTransform;
            if (parentRt == null)
                parentRt = _canvasRect;

            if (promptRoot == null)
            {
                var existing = parentRt.Find("InteractPrompt");
                if (existing != null)
                    promptRoot = existing as RectTransform;
            }

            if (promptRoot == null)
            {
                var go = new GameObject("InteractPrompt", typeof(RectTransform));
                go.transform.SetParent(parentRt, false);
                promptRoot = go.GetComponent<RectTransform>();
                promptRoot.sizeDelta = new Vector2(44f, 44f);

                chip = go.AddComponent<Image>();
                chip.color = PropChip;
                chip.raycastTarget = false;

                var textGo = new GameObject("Label", typeof(RectTransform));
                textGo.transform.SetParent(go.transform, false);
                var textRt = textGo.GetComponent<RectTransform>();
                textRt.anchorMin = Vector2.zero;
                textRt.anchorMax = Vector2.one;
                textRt.offsetMin = Vector2.zero;
                textRt.offsetMax = Vector2.zero;

                label = textGo.AddComponent<Text>();
                label.text = "F";
                label.alignment = TextAnchor.MiddleCenter;
                label.color = PropText;
                label.fontSize = PixelUiArt.SizeChip + 8;
                label.fontStyle = FontStyle.Bold;
                label.raycastTarget = false;
                label.font = PixelUiArt.Font;
            }

            if (chip == null && promptRoot != null)
                chip = promptRoot.GetComponent<Image>();
            if (label == null && promptRoot != null)
                label = promptRoot.GetComponentInChildren<Text>(true);

            if (label != null)
            {
                label.font = PixelUiArt.Font;
                if (label.fontSize < 18)
                    label.fontSize = PixelUiArt.SizeChip + 8;
            }

            if (promptRoot != null)
                promptRoot.gameObject.SetActive(false);

            _built = promptRoot != null && label != null;
        }
    }
}
