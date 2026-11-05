using UnityEngine;

namespace CommunityHelpers
{
    /// <summary>Design tokens. Use these instead of hard-coded colors.</summary>
    public static class UIColors
    {
        // UI
        public static readonly Color32 Ink       = new(0x1F, 0x2A, 0x44, 255); // text, outlines
        public static readonly Color32 Cream     = new(0xFF, 0xF8, 0xEC, 255); // panels (never pure white)
        public static readonly Color32 WoodDark  = new(0x9C, 0x6A, 0x3E, 255); // board frame, window frame
        public static readonly Color32 YayButton = new(0x3E, 0x9B, 0x58, 255); // Play / Let's go
        public static readonly Color32 Yay       = new(0x4C, 0xAF, 0x6A, 255); // right-answer feedback
        public static readonly Color32 Sky       = new(0x3A, 0x86, 0xC8, 255); // Quick play
        public static readonly Color32 Lilac     = new(0xED, 0xE6, 0xF7, 255); // How to play
        public static readonly Color32 Mist      = new(0xE5, 0xE8, 0xEE, 255); // Settings, Back
        public static readonly Color32 Peach     = new(0xF6, 0xDC, 0xD0, 255); // Quit
        public static readonly Color32 Sun       = new(0xF4, 0xB9, 0x42, 255); // selection, hover glow, title sign
        public static readonly Color32 Coral     = new(0xEF, 0x6F, 0x5E, 255); // Fox
        public static readonly Color32 Bear      = new(0xB0, 0x7A, 0x4F, 255); // Bear
        public static readonly Color32 Violet    = new(0x7C, 0x6B, 0xC4, 255); // Owl
        public static readonly Color32 Muted     = new(0x8A, 0x93, 0xA6, 255); // small notes
        public static readonly Color32 TryAgain  = new(0xF2, 0x9E, 0x4C, 255); // gentle "not yet" (never red)

        // In-game card types (not used by the menu yet)
        public static readonly Color32 CardHelper    = new(0xF4, 0xB9, 0x42, 255);
        public static readonly Color32 CardJob       = new(0xEF, 0x6F, 0x5E, 255);
        public static readonly Color32 CardTool      = new(0x2B, 0xA5, 0x9B, 255);
        public static readonly Color32 CardWorkplace = new(0x7C, 0x6B, 0xC4, 255);
        public static readonly Color32 CardAI        = new(0x5B, 0x6B, 0x82, 255);

        // Room / environment
        public static readonly Color32 WallUpper  = new(0xF6, 0xEB, 0xDA, 255);
        public static readonly Color32 WallLower  = new(0x9F, 0xC6, 0xB6, 255);
        public static readonly Color32 FloorWood  = new(0xD9, 0xB0, 0x82, 255);
        public static readonly Color32 TableWood  = new(0xC8, 0x8B, 0x55, 255);
        public static readonly Color32 SkyBlue    = new(0xBF, 0xE0, 0xF5, 255);
        public static readonly Color32 Grass      = new(0x8F, 0xC2, 0x84, 255);
        public static readonly Color32 Roof       = new(0x4A, 0x55, 0x78, 255);
        public static readonly Color32 Teal       = new(0x2B, 0xA5, 0x9B, 255);
        public static readonly Color32 Leaf       = new(0x5E, 0xA8, 0x5C, 255);
    }
}
