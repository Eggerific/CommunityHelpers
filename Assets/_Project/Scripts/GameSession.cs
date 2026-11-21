namespace CommunityHelpers
{
    /// <summary>
    /// What the player chose on the main menu, carried into the Game scene.
    /// Static so it survives scene loads (single player, one session at a time).
    /// </summary>
    public static class GameSession
    {
        public enum GameMode { Full, Quick }

        /// <summary>The helper hats Robo can wear (team call, Nov 17: "Dress up Robo" replaces the animal picker).</summary>
        public enum RoboHat { None = -1, Firefighter, Police, MailCarrier, Doctor }

        public static GameMode Mode = GameMode.Full;
        public static RoboHat Hat = RoboHat.None;

        /// <summary>Full game uses all 6 helpers; Quick play uses 3.</summary>
        public static int HelperCount => Mode == GameMode.Full ? 6 : 3;
    }
}
