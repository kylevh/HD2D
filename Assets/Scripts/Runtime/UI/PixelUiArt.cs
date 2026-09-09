using UnityEngine;

namespace KVH.Game.UI
{
    // Dark indie/pixel placeholder chrome. Theme colors + white 1x1 sprite.
    public static class PixelUiArt
    {
        const string DefaultThemeResource = "UI/DefaultPixelUiTheme";

        static PixelUiTheme s_Theme;
        static Sprite s_White;
        static Font s_Builtin;

        public static PixelUiTheme Theme
        {
            get
            {
                if (s_Theme == null)
                    s_Theme = Resources.Load<PixelUiTheme>(DefaultThemeResource);
                return s_Theme;
            }
            set => s_Theme = value;
        }

        public static Color Ink => Theme != null ? Theme.ink : new Color(0.06f, 0.05f, 0.08f, 1f);
        public static Color Cream => Theme != null ? Theme.cream : new Color(0.96f, 0.95f, 0.97f, 1f);
        public static Color Gold => Theme != null ? Theme.gold : Color.white;
        public static Color Sky => Theme != null ? Theme.sky : new Color(0.72f, 0.70f, 0.78f, 1f);
        public static Color Hp => Theme != null ? Theme.hp : new Color(0.90f, 0.48f, 0.55f, 1f);
        public static Color Mp => Theme != null ? Theme.mp : new Color(0.50f, 0.65f, 0.90f, 1f);
        public static Color PanelFace => Theme != null ? Theme.panelFace : new Color(0.10f, 0.09f, 0.13f, 0.88f);
        public static Color Accent => Gold;

        public static Font Font
        {
            get
            {
                if (Theme != null && Theme.font != null)
                    return Theme.font;
                if (s_Builtin == null)
                {
                    s_Builtin = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                    if (s_Builtin == null)
                        s_Builtin = Resources.GetBuiltinResource<Font>("Arial.ttf");
                }
                return s_Builtin;
            }
        }

        public static int SizeTitle => Theme != null ? Theme.sizeTitle : 20;
        public static int SizeBody => Theme != null ? Theme.sizeBody : 16;
        public static int SizeHint => Theme != null ? Theme.sizeHint : 14;
        public static int SizeChip => SizeHint; // interact prompt

        public static Sprite WhiteSprite
        {
            get
            {
                if (s_White != null)
                    return s_White;

                var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
                tex.filterMode = FilterMode.Point;
                tex.SetPixel(0, 0, Color.white);
                tex.Apply();
                tex.hideFlags = HideFlags.HideAndDontSave;
                s_White = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
                s_White.hideFlags = HideFlags.HideAndDontSave;
                return s_White;
            }
        }
    }
}
