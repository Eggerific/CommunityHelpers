using UnityEditor;
using UnityEngine;

namespace CommunityHelpers.EditorTools
{
    /// <summary>Import settings for the Kenney model kits: no animation/cameras/lights, matte materials.</summary>
    public class KenneyImportSettings : AssetPostprocessor
    {
        public const string Root = "Assets/_Project/ThirdParty/Kenney";

        private bool IsKenney => assetPath.StartsWith(Root);

        private void OnPreprocessModel()
        {
            if (!IsKenney) return;
            var mi = (ModelImporter)assetImporter;
            mi.importAnimation = false;
            mi.animationType = ModelImporterAnimationType.None;
            mi.importCameras = false;
            mi.importLights = false;
            mi.importBlendShapes = false;
            // Buildings stay readable so the scene builder can find the roof edge for the signs.
            mi.isReadable = assetPath.Contains("/city-kit-commercial/");
            mi.addCollider = false;
            mi.meshCompression = ModelImporterMeshCompression.Medium;
            mi.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
            mi.userData = "kenney-v2"; // marker so the scene builder knows these settings were applied
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
