using UnityEditor;
using UnityEngine;

namespace CommunityHelpers.EditorTools
{
    /// <summary>
    /// Import settings for the Kenney model kits: no animation/cameras/lights, matte materials.
    /// Exception: the Mini Characters keep their animations (Generic rig; idle/walk loop).
    /// </summary>
    public class KenneyImportSettings : AssetPostprocessor
    {
        public const string Root = "Assets/_Project/ThirdParty/Kenney";

        private bool IsKenney => assetPath.StartsWith(Root);

        /// <summary>The Mini Characters (people outside the window) are the one kit that keeps its animations.</summary>
        public const string CharactersFolder = "/mini-characters/";
        private bool IsCharacter => IsKenney && assetPath.Contains(CharactersFolder);

        // Clips that repeat on their own; the rest (emote-yes, interact-right ...) play once.
        private static readonly string[] LoopingClips = { "idle", "walk", "sprint", "static" };

        private void OnPreprocessModel()
        {
            if (!IsKenney) return;
            var mi = (ModelImporter)assetImporter;
            mi.importAnimation = IsCharacter;
            mi.animationType = IsCharacter ? ModelImporterAnimationType.Generic : ModelImporterAnimationType.None;
            if (IsCharacter) mi.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            mi.importCameras = false;
            mi.importLights = false;
            mi.importBlendShapes = false;
            // Buildings: the scene builder finds the roof edge for the signs. Characters: the people builder
            // finds where each hand is (to give them something to carry).
            mi.isReadable = assetPath.Contains("/city-kit-commercial/") || IsCharacter;
            mi.addCollider = false;
            mi.meshCompression = ModelImporterMeshCompression.Medium;
            mi.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
            mi.userData = "kenney-v2"; // marker so the scene builder knows these settings were applied
        }

        private void OnPreprocessAnimation()
        {
            if (!IsCharacter) return;
            var mi = (ModelImporter)assetImporter;
            var clips = mi.defaultClipAnimations;
            foreach (var clip in clips)
            {
                string n = ClipName(clip.name);
                bool loop = System.Array.IndexOf(LoopingClips, n) >= 0;
                clip.loopTime = loop;
                clip.loopPose = loop;
                clip.name = n;
            }
            mi.clipAnimations = clips;
        }

        /// <summary>"Armature|walk" or "walk" → "walk".</summary>
        public static string ClipName(string take)
        {
            int bar = take.LastIndexOf('|');
            return bar >= 0 ? take.Substring(bar + 1) : take;
        }

        private void OnPostprocessMaterial(Material material)
        {
            if (!IsKenney) return;
            // Kenney materials come in shiny; the classroom should look soft and matte.
            if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", 0.1f);
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", 0f);
            material.enableInstancing = true;
        }

        [MenuItem("Community Helpers/Setup/Reimport Kenney Models")]
        public static void ReimportAll()
        {
            AssetDatabase.ImportAsset(Root, ImportAssetOptions.ImportRecursive | ImportAssetOptions.ForceUpdate);
        }

        /// <summary>Reimports the kits once if they were imported before these settings existed.</summary>
        public static void EnsureApplied()
        {
            var probe = AssetImporter.GetAtPath($"{Root}/city-kit-commercial/building-a.fbx") as ModelImporter;
            if (probe != null && probe.userData != "kenney-v2") ReimportAll();
        }
    }
}
