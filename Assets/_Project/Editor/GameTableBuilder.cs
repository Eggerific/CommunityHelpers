using Oculus.Interaction.HandGrab;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CommunityHelpers.EditorTools
{
    /// <summary>
    /// Game-scene table layout: the card table in front of the player (see CardTableBuilder), the placeholder
    /// easel pushed back behind it, and Robo out of the way. Used by the Game scene build and by the menu item
    /// (which updates the existing Game scene in place, keeping hand-made changes).
    /// </summary>
    public static class GameTableBuilder
    {
        // Table space: origin = table-top surface, -z = toward the player, table is 1.8 x 1.1 m.
        // Back-right corner: ~1.2 m from the seat, out of reach, so nobody grabs Robo by accident.
        private static readonly Vector3 RoboGamePosition = new(0.6f, 0f, 0.3f);

        // The easel stands where the menu put it (over the middle of the table); in the game it would cover
        // the far end of the helper lanes, so it moves back. Its "Back to menu" button is pressed with the ray.
        private static readonly Vector3 EaselGameOffset = new(0f, 0f, 0.30f);

        [MenuItem("Community Helpers/Update Game Table (Cards + Robo)", priority = 3)]
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
            CardTableBuilder.Build(table);
            HideTestFixtures(table);
            MoveEasel(table);
            SetUpRobo(table);
        }

        private static void MoveEasel(Transform table)
        {
            var easel = table.Find("GameBoard");
            if (easel == null) return;
            easel.localPosition = EaselGameOffset;
            EditorUtility.SetDirty(easel);
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
