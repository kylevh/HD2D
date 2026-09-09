using KVH.Game.UI;
using UnityEditor;
using UnityEngine;

namespace KVH.Game.Editor
{
    // Keeps DefaultPixelUiTheme in Settings + Resources in sync for PixelUiArt.
    public static class PixelUiThemeMenus
    {
        const string FontPath = "Assets/Art/UI/Fonts/ShareTechMono-Regular.ttf";
        const string DarkThemePath = "Assets/Settings/UI/DefaultPixelUiTheme.asset";
        const string DarkThemeResourcePath = "Assets/Resources/UI/DefaultPixelUiTheme.asset";

        [MenuItem("KVH/UI/Ensure Pixel UI Themes")]
        public static void EnsureThemesMenu()
        {
            EnsureThemes();
            AssetDatabase.SaveAssets();
        }

        public static void EnsureThemes()
        {
            EnsureFolder("Assets/Art/UI");
            EnsureFolder("Assets/Art/UI/Fonts");
            EnsureFolder("Assets/Settings");
            EnsureFolder("Assets/Settings/UI");
            EnsureFolder("Assets/Resources");
            EnsureFolder("Assets/Resources/UI");

            var font = AssetDatabase.LoadAssetAtPath<Font>(FontPath);
            if (font == null)
                Debug.LogWarning($"Pixel UI: missing font at {FontPath}");

            var dark = LoadOrCreateTheme(DarkThemePath, font);
            CopyThemeAsset(dark, DarkThemeResourcePath);
        }

        static PixelUiTheme LoadOrCreateTheme(string path, Font font)
        {
            var theme = AssetDatabase.LoadAssetAtPath<PixelUiTheme>(path);
            if (theme == null)
            {
                theme = ScriptableObject.CreateInstance<PixelUiTheme>();
                AssetDatabase.CreateAsset(theme, path);
            }

            var so = new SerializedObject(theme);
            so.FindProperty("font").objectReferenceValue = font;
            // dark indie/pixel placeholder — white type, pastel bars
            so.FindProperty("ink").colorValue = new Color(0.06f, 0.05f, 0.08f, 1f);
            so.FindProperty("cream").colorValue = new Color(0.96f, 0.95f, 0.97f, 1f);
            so.FindProperty("gold").colorValue = new Color(1f, 1f, 1f, 1f);
            so.FindProperty("sky").colorValue = new Color(0.72f, 0.70f, 0.78f, 1f);
            so.FindProperty("hp").colorValue = new Color(0.90f, 0.48f, 0.55f, 1f);
            so.FindProperty("mp").colorValue = new Color(0.50f, 0.65f, 0.90f, 1f);
            so.FindProperty("panelFace").colorValue = new Color(0.10f, 0.09f, 0.13f, 0.88f);
            so.FindProperty("panelBorder").colorValue = new Color(0.42f, 0.38f, 0.50f, 1f);
            so.FindProperty("panelInner").colorValue = new Color(0.15f, 0.13f, 0.19f, 1f);
            so.FindProperty("sizeTitle").intValue = 20;
            so.FindProperty("sizeBody").intValue = 16;
            so.FindProperty("sizeHint").intValue = 14;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(theme);
            return theme;
        }

        static void CopyThemeAsset(PixelUiTheme source, string destPath)
        {
            var existing = AssetDatabase.LoadAssetAtPath<PixelUiTheme>(destPath);
            if (existing == null)
            {
                AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(source), destPath);
                return;
            }

            EditorUtility.CopySerialized(source, existing);
            EditorUtility.SetDirty(existing);
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;
            var parts = path.Split('/');
            var cur = parts[0];
            for (var i = 1; i < parts.Length; i++)
            {
                var next = cur + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(cur, parts[i]);
                cur = next;
            }
        }
    }
}
