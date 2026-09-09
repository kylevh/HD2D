using UnityEngine;

namespace KVH.Game.UI
{
    // Portrait viewport frame for dialogue.
    public static class PixelUiShapes
    {
        static Sprite s_RoundFrame;

        public static Sprite RoundFrame => s_RoundFrame ??= BuildPortraitFrame();

        static Sprite BuildPortraitFrame()
        {
            const int n = 24;
            var t = new Texture2D(n, n, TextureFormat.RGBA32, false);
            t.filterMode = FilterMode.Point;
            t.wrapMode = TextureWrapMode.Clamp;
            var clear = new Color(0, 0, 0, 0);
            var px = new Color[n * n];
            for (var i = 0; i < px.Length; i++)
                px[i] = clear;
            t.SetPixels(px);

            var rim = PixelUiArt.Accent;
            var ink = PixelUiArt.Ink;
            var face = PixelUiArt.PanelFace;
            const int inset = 2;
            for (var y = 0; y < n; y++)
            for (var x = 0; x < n; x++)
            {
                var outer = x == 0 || y == 0 || x == n - 1 || y == n - 1;
                var inner = x == inset || y == inset || x == n - 1 - inset || y == n - 1 - inset;
                var inHole = x > inset && y > inset && x < n - 1 - inset && y < n - 1 - inset;
                if (outer) t.SetPixel(x, y, rim);
                else if (inner) t.SetPixel(x, y, ink);
                else if (inHole) t.SetPixel(x, y, face);
            }

            t.SetPixel(1, 1, rim);
            t.SetPixel(n - 2, 1, rim);
            t.SetPixel(1, n - 2, rim);
            t.SetPixel(n - 2, n - 2, rim);

            t.Apply();
            t.hideFlags = HideFlags.HideAndDontSave;
            var sprite = Sprite.Create(t, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 1f);
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }
    }
}
