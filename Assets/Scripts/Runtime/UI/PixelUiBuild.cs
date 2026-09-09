using UnityEngine;
using UnityEngine.UI;

namespace KVH.Game.UI
{
    // Code-built UI chrome for live HUD / pause / dialogue.
    public static class PixelUiBuild
    {
        public static RectTransform Rect(
            string name, Transform parent,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPos)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;
            rt.anchoredPosition = anchoredPos;

            var stretchX = !Mathf.Approximately(anchorMin.x, anchorMax.x);
            var stretchY = !Mathf.Approximately(anchorMin.y, anchorMax.y);
            if (stretchX || stretchY)
            {
                rt.sizeDelta = Vector2.zero;
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
            }

            return rt;
        }

        public static Image Panel(RectTransform rt, bool light = false)
        {
            var face = light
                ? (PixelUiArt.Theme != null ? PixelUiArt.Theme.panelInner : new Color(0.15f, 0.13f, 0.19f, 1f))
                : PixelUiArt.PanelFace;
            var img = Flat(rt, face);

            var outline = rt.GetComponent<Outline>();
            if (outline == null)
                outline = rt.gameObject.AddComponent<Outline>();
            outline.effectColor = PixelUiArt.Theme != null
                ? PixelUiArt.Theme.panelBorder
                : new Color(0.42f, 0.38f, 0.50f, 1f);
            outline.effectDistance = new Vector2(1f, -1f);
            outline.useGraphicAlpha = true;
            return img;
        }

        public static Image Flat(RectTransform rt, Color color)
        {
            var img = rt.GetComponent<Image>();
            if (img == null)
                img = rt.gameObject.AddComponent<Image>();
            img.sprite = PixelUiArt.WhiteSprite;
            img.type = Image.Type.Simple;
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        // HideAndDontSave sprites die on domain reload — rebind before show.
        public static void RefreshChrome(RectTransform root)
        {
            if (root == null)
                return;

            foreach (var img in root.GetComponentsInChildren<Image>(true))
            {
                if (img == null)
                    continue;

                var n = img.gameObject.name;
                switch (n)
                {
                    case "Dim":
                        Flat(img.rectTransform, new Color(0.04f, 0.03f, 0.06f, 0.60f));
                        break;
                    case "Fill":
                    case "Portrait":
                        img.sprite = PixelUiArt.WhiteSprite;
                        img.type = Image.Type.Simple;
                        img.raycastTarget = false;
                        break;
                    case "Hp":
                    case "Mp":
                    case "HpBar":
                    case "MpBar":
                        Flat(img.rectTransform, PixelUiArt.Ink);
                        break;
                    case "PortraitFrame":
                        img.sprite = PixelUiShapes.RoundFrame;
                        img.type = Image.Type.Simple;
                        img.color = Color.white;
                        img.preserveAspect = true;
                        img.raycastTarget = false;
                        break;
                    case "SpeakerChip":
                        Flat(img.rectTransform, PixelUiArt.Theme != null
                            ? PixelUiArt.Theme.panelInner
                            : new Color(0.15f, 0.13f, 0.19f, 1f));
                        break;
                    default:
                        if (n.StartsWith("Cmd_"))
                        {
                            var selected = n == "Cmd_0";
                            if (selected)
                                Flat(img.rectTransform, new Color(1f, 1f, 1f, 0.10f));
                            else if (img.GetComponent<Outline>() == null)
                                Flat(img.rectTransform, new Color(0f, 0f, 0f, 0f));
                        }
                        else
                            Panel(img.rectTransform);
                        break;
                }
            }
        }

        public static Text Label(
            RectTransform parent, string name, string value,
            int size, FontStyle style, Color color,
            Vector2 anchoredPos, Vector2 sizeDelta,
            TextAnchor align = TextAnchor.MiddleLeft)
        {
            var rt = Rect(name, parent,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), anchoredPos);
            rt.sizeDelta = sizeDelta;

            var t = rt.gameObject.AddComponent<Text>();
            t.text = value;
            t.font = PixelUiArt.Font;
            t.fontSize = size;
            t.fontStyle = style;
            t.color = color;
            t.alignment = align;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            return t;
        }

        public static Image Bar(
            RectTransform parent, string name, Color fillColor,
            Vector2 anchoredPos, Vector2 size, out Text label)
        {
            var row = Rect(name, parent,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), anchoredPos);
            row.sizeDelta = size;

            var track = row.gameObject.AddComponent<Image>();
            track.sprite = PixelUiArt.WhiteSprite;
            track.color = PixelUiArt.Ink;
            track.raycastTarget = false;

            var fillGo = Rect("Fill", row, Vector2.zero, Vector2.one, new Vector2(0f, 0.5f), Vector2.zero);
            fillGo.offsetMin = new Vector2(2f, 2f);
            fillGo.offsetMax = new Vector2(-2f, -2f);
            fillGo.anchorMin = Vector2.zero;
            fillGo.anchorMax = new Vector2(1f, 1f);
            fillGo.pivot = new Vector2(0f, 0.5f);

            var fill = fillGo.gameObject.AddComponent<Image>();
            fill.sprite = PixelUiArt.WhiteSprite;
            fill.color = fillColor;
            fill.raycastTarget = false;

            label = Label(row, "Label", "", Mathf.Max(12, PixelUiArt.SizeHint - 2), FontStyle.Bold,
                PixelUiArt.Cream, new Vector2(6f, 0f), size, TextAnchor.MiddleLeft);
            var labelRt = label.rectTransform;
            labelRt.anchorMin = Vector2.zero;
            labelRt.anchorMax = Vector2.one;
            labelRt.offsetMin = new Vector2(6f, 0f);
            labelRt.offsetMax = new Vector2(-6f, 0f);
            labelRt.anchoredPosition = Vector2.zero;

            return fill;
        }

        public static Image Shape(RectTransform rt, Sprite sprite)
        {
            var img = rt.GetComponent<Image>();
            if (img == null)
                img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.type = Image.Type.Simple;
            img.color = Color.white;
            img.raycastTarget = false;
            img.preserveAspect = true;
            return img;
        }

        public static void ApplyFont(Text t, int size, FontStyle style, Color color)
        {
            if (t == null)
                return;
            t.font = PixelUiArt.Font;
            t.fontSize = size;
            t.fontStyle = style;
            t.color = color;
        }
    }
}
