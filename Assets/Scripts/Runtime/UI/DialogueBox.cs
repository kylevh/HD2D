using UnityEngine;
using UnityEngine.UI;

namespace KVH.Game.UI
{
    // Bottom dialogue plate + left portrait viewport. Terminal placeholder chrome.
    [DisallowMultipleComponent]
    [ExecuteAlways]
    public sealed class DialogueBox : MonoBehaviour
    {
        const float EdgeMargin = 48f;
        const float BoxHeight = 170f;
        const float PortraitSize = 120f;
        static readonly Color PortraitFallback = new Color(0.18f, 0.16f, 0.22f, 1f);

        RectTransform _root;
        Image _portrait;
        Text _speaker;
        Text _body;
        Text _hint;
        bool _built;

        void OnEnable() => EnsureBuilt();
        void Start() => EnsureBuilt();

        public void Show(string speaker, string body, Sprite portrait = null)
        {
            EnsureBuilt();
            if (_root == null)
                return;

            PixelUiBuild.RefreshChrome(_root);

            if (_speaker != null)
            {
                _speaker.text = speaker ?? "";
                var chip = _speaker.transform.parent;
                if (chip != null && chip.name == "SpeakerChip")
                    chip.gameObject.SetActive(!string.IsNullOrEmpty(speaker));
                else
                    _speaker.gameObject.SetActive(!string.IsNullOrEmpty(speaker));
            }

            if (_body != null)
                _body.text = body ?? "";

            if (_portrait != null)
            {
                if (portrait != null)
                {
                    _portrait.sprite = portrait;
                    _portrait.color = Color.white;
                    _portrait.preserveAspect = true;
                }
                else
                {
                    _portrait.sprite = PixelUiArt.WhiteSprite;
                    _portrait.color = PortraitFallback;
                    _portrait.preserveAspect = false;
                }
            }

            _root.gameObject.SetActive(true);
        }

        public void Hide()
        {
            if (_root != null)
                _root.gameObject.SetActive(false);
        }

        void EnsureBuilt()
        {
            var canvas = GetComponentInParent<Canvas>();
            if (canvas == null)
                canvas = FindAnyObjectByType<Canvas>();
            if (canvas == null)
                return;

            RectTransform parent = null;
            var menus = canvas.transform.Find("Menus");
            if (menus is RectTransform menusRt)
                parent = menusRt;
            if (parent == null)
                parent = canvas.transform as RectTransform;

            var existing = parent.Find("DialogueBox");
            // rebuild each play session so placeholder chrome picks up theme changes
            if (existing != null && Application.isPlaying && !_built)
            {
                DestroyImmediate(existing.gameObject);
                existing = null;
            }

            if (existing != null)
            {
                // wipe pre-portrait / pre-chip plates so we always match the locked look
                if (existing.Find("PortraitFrame") == null || existing.Find("SpeakerChip") == null)
                {
                    if (Application.isPlaying)
                        DestroyImmediate(existing.gameObject);
                    else
                        DestroyImmediate(existing.gameObject);
                    existing = null;
                    _built = false;
                    _root = null;
                }
            }

            if (_built && _root != null && _portrait != null)
            {
                PixelUiBuild.RefreshChrome(_root);
                RefreshFonts();
                return;
            }

            if (existing != null)
            {
                _root = existing as RectTransform;
                CacheRefs(_root);
                PixelUiBuild.RefreshChrome(_root);
                RefreshFonts();
                _built = _root != null && _portrait != null;
                if (_root != null)
                    _root.gameObject.SetActive(false);
                return;
            }

            _root = PixelUiBuild.Rect("DialogueBox", parent,
                new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, EdgeMargin));
            _root.offsetMin = new Vector2(EdgeMargin, EdgeMargin);
            _root.offsetMax = new Vector2(-EdgeMargin, EdgeMargin + BoxHeight);
            PixelUiBuild.Panel(_root);

            var frame = PixelUiBuild.Rect("PortraitFrame", _root,
                new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(24f, 4f));
            frame.sizeDelta = new Vector2(PortraitSize, PortraitSize);
            PixelUiBuild.Shape(frame, PixelUiShapes.RoundFrame);

            var face = PixelUiBuild.Rect("Portrait", frame,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero);
            face.sizeDelta = new Vector2(PortraitSize - 18f, PortraitSize - 18f);
            _portrait = face.gameObject.AddComponent<Image>();
            _portrait.sprite = PixelUiArt.WhiteSprite;
            _portrait.color = PortraitFallback;
            _portrait.raycastTarget = false;

            var textLeft = PortraitSize + 44f;

            var chip = PixelUiBuild.Rect("SpeakerChip", _root,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(textLeft, 10f));
            chip.sizeDelta = new Vector2(180f, 32f);
            PixelUiBuild.Flat(chip, PixelUiArt.Theme != null
                ? PixelUiArt.Theme.panelInner
                : new Color(0.15f, 0.13f, 0.19f, 1f));
            _speaker = PixelUiBuild.Label(chip, "Speaker", "", PixelUiArt.SizeHint, FontStyle.Bold,
                PixelUiArt.Accent, new Vector2(12f, -5f), new Vector2(156f, 22f));

            _body = PixelUiBuild.Label(_root, "Body", "", PixelUiArt.SizeBody, FontStyle.Normal,
                PixelUiArt.Cream, new Vector2(textLeft, -40f), new Vector2(100f, 70f));
            var bodyRt = _body.rectTransform;
            bodyRt.anchorMin = new Vector2(0f, 0f);
            bodyRt.anchorMax = new Vector2(1f, 1f);
            bodyRt.offsetMin = new Vector2(textLeft, 36f);
            bodyRt.offsetMax = new Vector2(-28f, -44f);
            _body.alignment = TextAnchor.UpperLeft;
            _body.horizontalOverflow = HorizontalWrapMode.Wrap;
            _body.verticalOverflow = VerticalWrapMode.Truncate;

            _hint = PixelUiBuild.Label(_root, "Hint", "[F] continue", PixelUiArt.SizeHint, FontStyle.Normal,
                PixelUiArt.Sky, new Vector2(-24f, 14f), new Vector2(160f, 24f));
            var hintRt = _hint.rectTransform;
            hintRt.anchorMin = new Vector2(1f, 0f);
            hintRt.anchorMax = new Vector2(1f, 0f);
            hintRt.pivot = new Vector2(1f, 0f);
            hintRt.anchoredPosition = new Vector2(-24f, 14f);
            _hint.alignment = TextAnchor.MiddleRight;

            _root.gameObject.SetActive(false);
            _built = true;
        }

        void CacheRefs(RectTransform root)
        {
            var portrait = root.Find("PortraitFrame/Portrait");
            if (portrait != null)
                _portrait = portrait.GetComponent<Image>();
            var sp = root.Find("SpeakerChip/Speaker") ?? root.Find("Speaker");
            if (sp != null)
                _speaker = sp.GetComponent<Text>();
            var bd = root.Find("Body");
            if (bd != null)
                _body = bd.GetComponent<Text>();
            var hn = root.Find("Hint");
            if (hn != null)
            {
                _hint = hn.GetComponent<Text>();
                if (_hint != null)
                    _hint.text = "[F] continue";
            }
        }

        void RefreshFonts()
        {
            PixelUiBuild.ApplyFont(_speaker, PixelUiArt.SizeHint, FontStyle.Bold, PixelUiArt.Accent);
            PixelUiBuild.ApplyFont(_body, PixelUiArt.SizeBody, FontStyle.Normal, PixelUiArt.Cream);
            PixelUiBuild.ApplyFont(_hint, PixelUiArt.SizeHint, FontStyle.Normal, PixelUiArt.Sky);
        }
    }
}
