using UnityEngine;
using UnityEngine.UI;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace KVH.Game.UI
{
    // Marker on the GameUI canvas (Screen Space Overlay). HUD/menus live under this root.
    // Pixel world is presented by LowResOutput + PresentFeature — not under this canvas.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Canvas))]
    [RequireComponent(typeof(CanvasScaler))]
    [ExecuteAlways]
    public sealed class UiRoot : MonoBehaviour
    {
        // Author against 1080p; scaler adapts other displays from this.
        public static readonly Vector2 ReferenceResolution = new(1920f, 1080f);

        // Don't touch RectTransforms in Awake — Unity blocks SendMessage (OnRectTransformDimensionsChange).
        void OnEnable() => ApplyBestPracticeLayout();

        void Start() => ApplyBestPracticeLayout();

#if UNITY_EDITOR
        bool _validateQueued;

        void OnValidate()
        {
            // Rect sizeDelta during OnValidate → same SendMessage spam; defer one editor tick.
            if (_validateQueued)
                return;
            _validateQueued = true;
            EditorApplication.delayCall += ApplyAfterValidate;
        }

        void ApplyAfterValidate()
        {
            _validateQueued = false;
            if (this == null)
                return;
            ApplyBestPracticeLayout();
        }
#endif

        // Overlay canvas fills the display; CanvasScaler owns resolution math (not a fixed 1920×1080 root).
        public void ApplyBestPracticeLayout()
        {
            var rt = transform as RectTransform;
            if (rt != null)
                StretchFill(rt);

            var canvas = GetComponent<Canvas>();
            if (canvas != null)
            {
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                if (canvas.sortingOrder < 100)
                    canvas.sortingOrder = 100;
            }

            var scaler = GetComponent<CanvasScaler>();
            if (scaler != null)
            {
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = ReferenceResolution;
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                // 0.5 balances width vs height across 16:9 / laptop / ultrawide — not height-only.
                scaler.matchWidthOrHeight = 0.5f;
                scaler.referencePixelsPerUnit = 100f;
            }

            StretchNamedChild("Hud");
            StretchNamedChild("Menus");
        }

        void StretchNamedChild(string childName)
        {
            var child = transform.Find(childName) as RectTransform;
            if (child != null)
                StretchFill(child);
        }

        // Full-parent stretch: anchors 0–1, zero offsets. Default sizeDelta (100,100) breaks this.
        public static void StretchFill(RectTransform rt)
        {
            if (rt == null)
                return;

            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = Vector2.zero;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.localScale = Vector3.one;
            rt.localRotation = Quaternion.identity;
        }

        // Only rewrite when stretch is wrong — avoids RectTransform churn every frame.
        public static void EnsureStretchFill(RectTransform rt)
        {
            if (rt == null)
                return;

            if (rt.anchorMin == Vector2.zero
                && rt.anchorMax == Vector2.one
                && rt.sizeDelta == Vector2.zero
                && rt.offsetMin == Vector2.zero
                && rt.offsetMax == Vector2.zero)
                return;

            StretchFill(rt);
        }
    }
}
