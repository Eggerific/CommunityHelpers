using Oculus.Interaction.HandGrab;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CommunityHelpers.EditorTools
{
    /// <summary>
    /// Game-scene table layout: the card table in front of the player with its progress track and end-of-round
    /// pieces (see CardTableBuilder), and Robo out of the way. Used by the Game scene build and by the menu item
    /// (which updates the existing Game scene in place, keeping hand-made changes).
    /// </summary>
    public static class GameTableBuilder
    {
        // Table space: origin = table-top surface, -z = toward the player, table is 1.8 x 1.1 m.
        // Back-right corner: ~1.2 m from the seat, out of reach, so nobody grabs Robo by accident.
        private static readonly Vector3 RoboGamePosition = new(0.6f, 0f, 0.3f);

        /// <summary>
        /// Command line (Unity closed):
        /// Unity.exe -batchmode -projectPath . -executeMethod CommunityHelpers.EditorTools.GameTableBuilder.UpdateGameSceneAndExit
        /// Exits straight away; "-quit" can hang for minutes waiting on background work.
        /// </summary>
        public static void UpdateGameSceneAndExit()
        {
            UpdateGameScene();
            AssetDatabase.SaveAssets();
            EditorApplication.Exit(0);
        }

        [MenuItem("Community Helpers/Update Game Table (Cards, Progress, Robo, People)", priority = 3)]
        public static void UpdateGameScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            string previousScene = EditorSceneManager.GetActiveScene().path;
            var scene = EditorSceneManager.OpenScene(MainMenuSceneBuilder.GameScenePath);
            var table = GameObject.Find("World/Table");
            if (table == null)
            {
                Debug.LogError("[GameTableBuilder] No World/Table in " + MainMenuSceneBuilder.GameScenePath);
                return;
            }

            Apply(table.transform);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            // Put the editor back on the scene you were working in.
            if (!string.IsNullOrEmpty(previousScene) && previousScene != MainMenuSceneBuilder.GameScenePath)
                EditorSceneManager.OpenScene(previousScene);
        }

        public static void Apply(Transform table)
        {
            CardTableBuilder.Build(table); // cards, star track, end sign and buttons, rules
            TownPeopleBuilder.Build(table); // people outside the window who give the hints
            RemoveEasel(table);
            HideTestFixtures(table);
            SetUpRobo(table);
        }

        /// <summary>
        /// The Game scene used to have the menu's easel (first a placeholder, then a progress board). Progress
        /// and the end of the round are on the table now, so nothing stands in front of the player.
        /// </summary>
        private static void RemoveEasel(Transform table)
        {
            var easel = table.Find("GameBoard");
            if (easel != null) Object.DestroyImmediate(easel.gameObject);
        }

        /// <summary>Robo stands at the back-right corner and can't be distance-grabbed (reaching still works).</summary>
        private static void SetUpRobo(Transform table)
        {
            var robo = table.Find("Robo");
            if (robo == null)
            {
                Debug.LogWarning("[GameTableBuilder] No Robo on the table.");
                return;
            }
            robo.localPosition = RoboGamePosition;
            robo.localEulerAngles = new Vector3(0f,
                MenuBoardBuilder.YawTowardSeat(RoboGamePosition, MainMenuSceneBuilder.TableCenterZ), 0f);

            // He wears the hat the player dressed him in on the menu.
            RoboHatsBuilder.AddHats(robo, wearSessionHat: true);

            // Distance grab lets a hand pointed anywhere near Robo pick him up from the seat.
            foreach (var distanceGrab in robo.GetComponentsInChildren<DistanceHandGrabInteractable>(true))
            {
                distanceGrab.enabled = false;
                EditorUtility.SetDirty(distanceGrab);
            }
            EditorUtility.SetDirty(robo);
        }

        /// <summary>
        /// Van's hand-made card-placement test ("Spots" slot, "Deck" and its test "Card") is replaced by the
        /// card table's real slots and draw deck. It's switched off rather than deleted, so it's easy to
        /// bring back for testing. Skipped when it isn't in the scene.
        /// </summary>
        private static void HideTestFixtures(Transform table)
        {
            foreach (Transform child in table)
            {
                bool isTest = child.name == "Spots" || child.name == "Deck" ||
                              (child.name == "Card" && child.GetComponent<CardScript>() != null);
                if (!isTest || !child.gameObject.activeSelf) continue;
                child.gameObject.SetActive(false);
                EditorUtility.SetDirty(child.gameObject);
            }
        }
    }
}
