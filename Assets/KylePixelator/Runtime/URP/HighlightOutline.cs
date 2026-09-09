using System.Collections.Generic;
using UnityEngine;

namespace KVH.KylePixelator
{
    // Game writes this (InteractionScanner); feature reads it on the pixel cam.
    // In-world select cue — not menu/HUD chrome (that is uGUI UiRoot).
    public static class HighlightOutline
    {
        // Project only defines 8 rendering layers (bits 0–7). Bit 31 never matches a RendererList filter.
        public const uint RenderingLayerBit = 1u << 7;

        static readonly List<Renderer> s_Renderers = new List<Renderer>(8);
        static readonly List<uint> s_SavedMasks = new List<uint>(8);

        public static Color Color = new Color(0.97f, 0.90f, 0.42f, 1f);
        public static float Fill;

        public static void Set(IList<Renderer> renderers, Color color, float fill = 0f)
        {
            RestoreLayerBits();
            s_Renderers.Clear();
            s_SavedMasks.Clear();
            Color = color;
            Fill = fill;
            if (renderers == null)
                return;

            for (var i = 0; i < renderers.Count; i++)
            {
                var r = renderers[i];
                if (r == null || !r.enabled || !r.gameObject.activeInHierarchy)
                    continue;

                s_Renderers.Add(r);
                s_SavedMasks.Add(r.renderingLayerMask);
                r.renderingLayerMask |= RenderingLayerBit;
            }
        }

        public static void Clear()
        {
            RestoreLayerBits();
            s_Renderers.Clear();
            s_SavedMasks.Clear();
            Fill = 0f;
        }

        public static bool HasAny
        {
            get
            {
                for (var i = 0; i < s_Renderers.Count; i++)
                {
                    var r = s_Renderers[i];
                    if (r != null && r.enabled && r.gameObject.activeInHierarchy)
                        return true;
                }

                return false;
            }
        }

        // X-ray mask draw — feature iterates live renderers (no RendererList / depth test).
        public static void ForEachActive(System.Action<Renderer> action)
        {
            if (action == null)
                return;

            for (var i = 0; i < s_Renderers.Count; i++)
            {
                var r = s_Renderers[i];
                if (r != null && r.enabled && r.gameObject.activeInHierarchy)
                    action(r);
            }
        }

        static void RestoreLayerBits()
        {
            for (var i = 0; i < s_Renderers.Count; i++)
            {
                var r = s_Renderers[i];
                if (r != null)
                    r.renderingLayerMask = s_SavedMasks[i];
            }
        }
    }
}
