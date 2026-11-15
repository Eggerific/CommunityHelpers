using Oculus.Interaction.HandGrab;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CommunityHelpers.EditorTools
{
    /// <summary>
    /// Game-scene table layout: the card designs in front of the player, Robo out of the way, and the
    /// card-placement test slot + deck off to the side. Used by the Game scene build and by the menu item
    /// (which updates the existing Game scene in place, keeping hand-made changes).
    /// </summary>
    public static class GameTableBuilder
    {
        // Table space: origin = table-top surface, -z = toward the player, table is 1.8 x 1.1 m.
        // Back-right corner: ~1.2 m from the seat, out of reach, so nobody grabs Robo by accident.
        private static readonly Vector3 RoboGamePosition = new(0.6f, 0f, 0.3f);

        // Van's card-placement test (slot + deck), moved right of the card rows.
        private static readonly Vector3 TestSlotPosition = new(0.62f, 0f, -0.40f);
        private static readonly Vector3 TestDeckPosition = new(0.80f, 0f, -0.415f);

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
            CardDesignsBuilder.PlaceCards(table);
            MoveTestFixtures(table);
            SetUpRobo(table);
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
        /// The hand-made card-placement test ("Spots" slot group and "Deck" with its test "Card") would sit
        /// under the new card rows, so move it to the right. Skipped when it isn't in the scene.
        /// </summary>
        private static void MoveTestFixtures(Transform table)
        {
            var spots = table.Find("Spots");
            if (spots != null)
            {
                // The slot itself is a child offset in x; place the slot (not the group) at TestSlotPosition.
                var slot = spots.childCount > 0 ? spots.GetChild(0) : spots;
                Vector3 offset = slot == spots ? Vector3.zero : slot.localPosition;
                spots.localPosition = new Vector3(TestSlotPosition.x - offset.x, spots.localPosition.y, TestSlotPosition.z - offset.z);
                EditorUtility.SetDirty(spots);
            }

            var deck = table.Find("Deck");
            if (deck != null)
            {
                Vector3 shift = new Vector3(TestDeckPosition.x, deck.localPosition.y, TestDeckPosition.z) - deck.localPosition;
                deck.localPosition += shift;
                EditorUtility.SetDirty(deck);

                // The test card lying on the deck comes along.
                foreach (Transform child in table)
                {
                    if (child.name != "Card" || child.GetComponent<CardScript>() == null) continue;
                    Vector3 p = child.localPosition;
                    if (Mathf.Abs(p.x - (deck.localPosition.x - shift.x)) < 0.05f && Mathf.Abs(p.z - (deck.localPosition.z - shift.z)) < 0.05f)
                    {
                        child.localPosition = p + new Vector3(shift.x, 0f, shift.z);
                        EditorUtility.SetDirty(child);
                    }
                }
            }
        }
    }
}
