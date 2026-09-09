using KVH.Game.Inventory;
using UnityEngine;
using UnityEngine.UI;

namespace KVH.Game.UI
{
    // Brief "Got X ×N" under Hud. Auto-hides. Built via PixelUiBuild.
    [DisallowMultipleComponent]
    [ExecuteAlways]
    public sealed class ItemToast : MonoBehaviour
    {
        const float EdgeMargin = 32f;
        const float ShowSeconds = 2.4f;

        RectTransform _root;
        Text _label;
        bool _built;
        float _hideAt = -1f;

        void OnEnable() => EnsureBuilt();
        void Start() => EnsureBuilt();

        void Update()
        {
            if (_hideAt < 0f || _root == null)
                return;
            if (Time.unscaledTime < _hideAt)
                return;
            _root.gameObject.SetActive(false);
            _hideAt = -1f;
        }

        public void Show(ItemDef item, int amount)
        {
            EnsureBuilt();
            if (_root == null || item == null)
                return;

            PixelUiBuild.RefreshChrome(_root);
            PixelUiBuild.ApplyFont(_label, PixelUiArt.SizeBody, FontStyle.Bold, PixelUiArt.Cream);

            var name = item.DisplayName;
            if (_label != null)
                _label.text = amount > 1 ? $"Got  {name}  ×{amount}" : $"Got  {name}";

            _root.gameObject.SetActive(true);
            _hideAt = Time.unscaledTime + ShowSeconds;
        }

        void EnsureBuilt()
        {
            if (_built && _root != null)
            {
                PixelUiBuild.RefreshChrome(_root);
                PixelUiBuild.ApplyFont(_label, PixelUiArt.SizeBody, FontStyle.Bold, PixelUiArt.Cream);
                return;
            }

            var canvas = GetComponentInParent<Canvas>();
            if (canvas == null)
                canvas = FindAnyObjectByType<Canvas>();
            if (canvas == null)
                return;

            RectTransform parent = null;
            var hud = canvas.transform.Find("Hud");
            if (hud is RectTransform hudRt)
                parent = hudRt;
            if (parent == null)
                parent = canvas.transform as RectTransform;

            var existing = parent.Find("ItemToast");
            if (existing != null)
            {
                _root = existing as RectTransform;
                var lab = _root.Find("Label");
                if (lab != null)
                    _label = lab.GetComponent<Text>();
                PixelUiBuild.RefreshChrome(_root);
                PixelUiBuild.ApplyFont(_label, PixelUiArt.SizeBody, FontStyle.Bold, PixelUiArt.Cream);
                _built = _root != null;
                if (_root != null)
                    _root.gameObject.SetActive(false);
                return;
            }

            _root = PixelUiBuild.Rect("ItemToast", parent,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -EdgeMargin - 8f));
            _root.sizeDelta = new Vector2(360f, 48f);
            PixelUiBuild.Panel(_root);

            _label = PixelUiBuild.Label(_root, "Label", "", PixelUiArt.SizeBody, FontStyle.Bold,
                PixelUiArt.Cream, Vector2.zero, new Vector2(360f, 48f), TextAnchor.MiddleCenter);
            var textRt = _label.rectTransform;
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = new Vector2(16f, 4f);
            textRt.offsetMax = new Vector2(-16f, -4f);
            textRt.pivot = new Vector2(0.5f, 0.5f);
            textRt.anchoredPosition = Vector2.zero;

            _root.gameObject.SetActive(false);
            _built = true;
        }
    }
}
