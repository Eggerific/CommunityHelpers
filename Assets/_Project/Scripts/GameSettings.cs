using UnityEngine;

namespace CommunityHelpers
{
    /// <summary>Player settings saved between sessions (PlayerPrefs).</summary>
    public static class GameSettings
    {
        public const int MaxVolume = 5;
        private const string VolumeKey = "ch.volume";

        /// <summary>0..5, default 4. Applied to the whole game through AudioListener.volume.</summary>
        public static int Volume
        {
            get => PlayerPrefs.GetInt(VolumeKey, 4);
            set
            {
                PlayerPrefs.SetInt(VolumeKey, Mathf.Clamp(value, 0, MaxVolume));
                PlayerPrefs.Save();
                Apply();
            }
        }

        public static void Apply() => AudioListener.volume = Volume / (float)MaxVolume;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void ApplyOnStartup() => Apply();
    }
}
