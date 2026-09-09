using UnityEngine;

namespace KVH.Game.UI
{
    // Dark indie/pixel UI placeholder — deep panels, white type, pastel bars.
    [CreateAssetMenu(menuName = "KVH/UI/Pixel UI Theme", fileName = "PixelUiTheme")]
    public sealed class PixelUiTheme : ScriptableObject
    {
        [Header("Type")]
        public Font font;
        public int sizeTitle = 20;
        public int sizeBody = 16;
        public int sizeHint = 14;

        [Header("Colors")]
        public Color ink = new Color(0.06f, 0.05f, 0.08f, 1f);              // bar tracks
        public Color cream = new Color(0.96f, 0.95f, 0.97f, 1f);             // body (near-white)
        public Color gold = new Color(1f, 1f, 1f, 1f);                       // titles / hero
        public Color sky = new Color(0.72f, 0.70f, 0.78f, 1f);               // hints
        public Color hp = new Color(0.90f, 0.48f, 0.55f, 1f);                // pastel red
        public Color mp = new Color(0.50f, 0.65f, 0.90f, 1f);                // pastel blue
        public Color panelFace = new Color(0.10f, 0.09f, 0.13f, 0.88f);     // #1A171F
        public Color panelBorder = new Color(0.42f, 0.38f, 0.50f, 1f);       // soft lilac rim
        public Color panelInner = new Color(0.15f, 0.13f, 0.19f, 1f);        // elevated chip
    }
}
