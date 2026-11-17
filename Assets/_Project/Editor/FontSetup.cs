using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace CommunityHelpers.EditorTools
{
    /// <summary>Creates TextMeshPro SDF font assets for Fredoka (titles, buttons) and Andika (body text).</summary>
    public static class FontSetup
    {
        public const string FontDir = "Assets/_Project/Fonts";
        public const string SdfDir = FontDir + "/SDF";

        public const string FredokaSemiBold = "Fredoka-SemiBold";
        public const string FredokaBold = "Fredoka-Bold";
        public const string AndikaRegular = "Andika-Regular";
        public const string AndikaBold = "Andika-Bold";

        private static readonly string[] All = { FredokaSemiBold, FredokaBold, AndikaRegular, AndikaBold };

        public static bool TmpEssentialsPresent =>
            AssetDatabase.FindAssets("t:TMP_Settings").Length > 0;

        [MenuItem("Community Helpers/Setup/Create Font Assets")]
        public static void CreateAllMenu()
        {
            if (!EnsureTmpEssentials()) return;
            CreateAll(force: true);
            EditorUtility.DisplayDialog("Fonts", "Fredoka and Andika SDF font assets are ready in " + SdfDir, "OK");
        }

        /// <summary>Returns false (and starts the import) if TMP Essential Resources are missing.</summary>
        public static bool EnsureTmpEssentials()
        {
            if (TmpEssentialsPresent) return true;
            EditorApplication.ExecuteMenuItem("Window/TextMeshPro/Import TMP Essential Resources");
            EditorUtility.DisplayDialog("TextMeshPro",
                "Importing TMP Essential Resources first. When the import finishes, run the menu item again.", "OK");
            return false;
        }

        public static void CreateAll(bool force)
        {
            if (!AssetDatabase.IsValidFolder(SdfDir))
                AssetDatabase.CreateFolder(FontDir, "SDF");

            foreach (var name in All)
            {
                string assetPath = $"{SdfDir}/{name} SDF.asset";
                if (!force && File.Exists(assetPath)) continue;
                Create(name, assetPath);
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        public static TMP_FontAsset Load(string name)
        {
            var fa = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>($"{SdfDir}/{name} SDF.asset");
            if (fa == null)
            {
                CreateAll(force: false);
                fa = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>($"{SdfDir}/{name} SDF.asset");
            }
            return fa;
        }

        private static void Create(string name, string assetPath)
        {
            var font = AssetDatabase.LoadAssetAtPath<Font>($"{FontDir}/{name}.ttf");
            if (font == null)
            {
                Debug.LogError($"[FontSetup] Missing {FontDir}/{name}.ttf");
                return;
            }

            if (File.Exists(assetPath)) AssetDatabase.DeleteAsset(assetPath);

            // Sampling 90, padding 9, dynamic so any missing glyph is added at runtime.
            // 2048 atlas so all of ASCII fits on one page at this sampling size.
            var fa = TMP_FontAsset.CreateFontAsset(font, 90, 9, GlyphRenderMode.SDFAA, 2048, 2048,
                AtlasPopulationMode.Dynamic, enableMultiAtlasSupport: true);
            if (fa == null)
            {
                Debug.LogError($"[FontSetup] Could not create font asset for {name}");
                return;
            }

            fa.name = $"{name} SDF";
            AssetDatabase.CreateAsset(fa, assetPath);

            // Pre-bake printable ASCII so common text never hitches.
            var ascii = new System.Text.StringBuilder();
            for (char c = (char)32; c < 127; c++) ascii.Append(c);
            fa.TryAddCharacters(ascii.ToString(), out _);

            for (int i = 0; i < fa.atlasTextures.Length; i++)
            {
                var atlas = fa.atlasTextures[i];
                if (atlas == null) continue;
                atlas.name = i == 0 ? $"{name} Atlas" : $"{name} Atlas {i}";
                AssetDatabase.AddObjectToAsset(atlas, fa);
            }

            fa.material.name = $"{name} Material";
            AssetDatabase.AddObjectToAsset(fa.material, fa);

            EditorUtility.SetDirty(fa);
            Debug.Log($"[FontSetup] Created {assetPath}");
        }
    }
}
