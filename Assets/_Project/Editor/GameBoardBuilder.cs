using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.Events;
using static CommunityHelpers.EditorTools.BuildKit;
using B = CommunityHelpers.EditorTools.MenuBoardBuilder;

namespace CommunityHelpers.EditorTools
{
    /// <summary>
    /// PLACEHOLDER Game scene board. The real game will be played on the table; for now the easel only says
    /// which mode was chosen and that the game goes here, plus "Back to menu".
    /// </summary>
    public static class GameBoardBuilder
    {
        public static void Build(Transform table, float tableCenterZ, RoboController robo)
        {
            var semi = FontSetup.Load(FontSetup.FredokaSemiBold);
            var bold = FontSetup.Load(FontSetup.FredokaBold);
            var andika = FontSetup.Load(FontSetup.AndikaRegular);

            var root = B.BuildEasel(table, tableCenterZ, "GameBoard", out var board);
            var controller = root.gameObject.AddComponent<GameSceneController>();
            var panel = B.NewPanel("Panel_Game", board);

            var title = B.Label(panel, "Play", bold, 0.07f, UIColors.Ink, new Vector2(0, 0.08f), 0.55f);
            Text3D("Note", panel, "This is where the game will be played, on the table.", andika, 0.03f,
                UIColors.Muted, new Vector3(0, -0.02f, -0.0005f), new Vector2(0.5f, 0.08f));

            var back = MenuPanelsBuilder.BackButton(panel, new Vector2(0, -0.14f), new Vector2(0.3f, 0.08f), "Back to menu");
            UnityEventTools.AddPersistentListener(back.onPressed, new UnityAction(controller.OnBackToMenu));

            var so = new SerializedObject(controller);
            so.FindProperty("titleLabel").objectReferenceValue = title;
            so.FindProperty("robo").objectReferenceValue = robo;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
