using UnityEngine;
using UnityEngine.SceneManagement;

namespace StreetRacing.Core
{
    /// <summary>Central place for scene names and transitions.</summary>
    public static class SceneLoader
    {
        public const string MainMenuScene = "MainMenu";
        public const string RaceScene = "RaceTrack";

        public static void LoadMainMenu() => Load(MainMenuScene);
        public static void LoadRace() => Load(RaceScene);
        public static void ReloadCurrent() => Load(SceneManager.GetActiveScene().name);

        static void Load(string scene)
        {
            Time.timeScale = 1f;
            AudioListener.pause = false;
            SceneManager.LoadScene(scene);
        }

        public static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
