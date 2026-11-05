namespace CommunityHelpers
{
    /// <summary>
    /// What the player chose on the main menu, carried into the Game scene.
    /// Static so it survives scene loads (single player, one session at a time).
    /// </summary>
    public static class GameSession
    {
        public enum GameMode { Full, Quick }
        public enum Buddy { Fox, Bear, Owl, Bunny }

        public static GameMode Mode = GameMode.Full;
        public static Buddy Animal = Buddy.Fox;

        /// <summary>Full game uses all 6 helpers; Quick play uses 3.</summary>
        public static int HelperCount => Mode == GameMode.Full ? 6 : 3;
    }
}
