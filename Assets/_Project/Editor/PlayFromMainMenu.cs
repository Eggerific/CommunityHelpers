using UnityEditor;
using UnityEditor.SceneManagement;

namespace CommunityHelpers.EditorTools
{
    /// <summary>
    /// The game always begins at the Main Menu, so pressing Play starts there no matter which scene is open
    /// for editing (the open scene comes back when Play stops). Toggle it off from the menu to test a scene directly.
    /// </summary>
    [InitializeOnLoad]
    public static class PlayFromMainMenu
    {
        private const string MenuPath = "Community Helpers/Play Starts From Main Menu";
        private const string PrefKey = "CommunityHelpers.PlayFromMainMenu";

        static PlayFromMainMenu() => EditorApplication.delayCall += Apply;

        private static bool Enabled
        {
            get => EditorPrefs.GetBool(PrefKey, true);
            set => EditorPrefs.SetBool(PrefKey, value);
        }

        [MenuItem(MenuPath, priority = 40)]
        private static void Toggle()
        {
            Enabled = !Enabled;
            Apply();
        }

        [MenuItem(MenuPath, true)]
        private static bool ToggleValidate()
        {
            Menu.SetChecked(MenuPath, Enabled);
            return true;
        }

        private static void Apply()
        {
            EditorSceneManager.playModeStartScene = Enabled
                ? AssetDatabase.LoadAssetAtPath<SceneAsset>(MainMenuSceneBuilder.ScenePath)
                : null;
        }
    }
}
