using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CommunityHelpers.EditorTools
{
    /// <summary>
    /// Keeps exactly one OVRComprehensiveInteractionRig per scene.
    ///
    /// Meta Building Blocks (HandGrab, etc.) install their own "[BuildingBlock] OVRComprehensiveInteractionRig"
    /// when they don't recognise ours. That extra rig skips our "seated, no locomotion" setup, so its gravity is on
    /// and the player falls through the floor; it also doubles the hands and interactors.
    /// This removes the extra rig whenever a scene is saved and right before Play, keeping the scene builder's rig.
    /// </summary>
    [InitializeOnLoad]
    public static class SingleRigGuard
    {
        private const string RigPrefabGuid = "0a7d2469f24041c4284c66706f84c45e"; // OVRComprehensiveInteractionRig

        static SingleRigGuard()
        {
            EditorSceneManager.sceneSaving += (scene, _) => RemoveExtraRigs(scene);
            EditorApplication.playModeStateChanged += state =>
            {
                if (state != PlayModeStateChange.ExitingEditMode) return;
                for (int i = 0; i < SceneManager.sceneCount; i++)
                {
                    var scene = SceneManager.GetSceneAt(i);
                    if (scene.isLoaded && RemoveExtraRigs(scene) > 0) EditorSceneManager.SaveScene(scene);
                }
            };
        }

        [MenuItem("Community Helpers/Setup/Remove Extra Interaction Rigs", priority = 30)]
        private static void RemoveFromOpenScenes()
        {
            int removed = 0;
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (scene.isLoaded) removed += RemoveExtraRigs(scene);
            }
            if (removed == 0) Debug.Log("[SingleRigGuard] Only one interaction rig per scene. Nothing to remove.");
        }

        /// <summary>Removes all but one rig from <paramref name="scene"/>. Returns how many were removed.</summary>
        public static int RemoveExtraRigs(Scene scene)
        {
            var rigs = FindRigs(scene);
            if (rigs.Count <= 1) return 0;

            // Keep the scene builder's rig (plain name); Building Blocks prefix theirs with "[BuildingBlock]".
            var keep = rigs.FirstOrDefault(r => !r.name.StartsWith("[BuildingBlock]")) ?? rigs[0];
            int removed = 0;
            foreach (var rig in rigs)
            {
                if (rig == keep) continue;
                Debug.LogWarning($"[SingleRigGuard] Removed extra interaction rig \"{rig.name}\" from {scene.name}. " +
                                 "It was probably added by a Meta Building Block; the scene already has one, and a " +
                                 "second rig makes the player fall through the floor.", keep);
                Undo.DestroyObjectImmediate(rig);
                removed++;
            }
            EditorSceneManager.MarkSceneDirty(scene);
            return removed;
        }

        private static List<GameObject> FindRigs(Scene scene)
        {
            var rigs = new List<GameObject>();
            foreach (var root in scene.GetRootGameObjects())
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                var go = t.gameObject;
                if (!PrefabUtility.IsAnyPrefabInstanceRoot(go)) continue;
                var source = PrefabUtility.GetCorrespondingObjectFromOriginalSource(go);
                if (source == null) continue;
                if (AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(source)) == RigPrefabGuid) rigs.Add(go);
            }
            return rigs;
        }
    }
}
