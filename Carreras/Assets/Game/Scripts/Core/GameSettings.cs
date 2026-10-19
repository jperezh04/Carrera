using UnityEngine;

namespace StreetRacing.Core
{
    /// <summary>
    /// Settings that persist between the main menu and the race (selected paint colour, lap count).
    /// Stored in PlayerPrefs so they survive restarts of the game.
    /// </summary>
    public static class GameSettings
    {
        const string ColorKey = "SR_PaintColorIndex";
        const string MotionBlurKey = "SR_MotionBlur";

        public static readonly NamedColor[] PaintColors =
        {
            new NamedColor("ROJO",     new Color(0.70f, 0.03f, 0.03f)),
            new NamedColor("AZUL",     new Color(0.04f, 0.16f, 0.62f)),
            new NamedColor("NEGRO",    new Color(0.02f, 0.02f, 0.025f)),
            new NamedColor("BLANCO",   new Color(0.86f, 0.86f, 0.86f)),
            new NamedColor("AMARILLO", new Color(0.92f, 0.62f, 0.02f)),
            new NamedColor("VERDE",    new Color(0.08f, 0.50f, 0.10f)),
            new NamedColor("NARANJA",  new Color(0.95f, 0.30f, 0.02f)),
            new NamedColor("PLATA",    new Color(0.55f, 0.57f, 0.60f)),
        };

        public static int PaintColorIndex
        {
            get => Mathf.Clamp(PlayerPrefs.GetInt(ColorKey, 0), 0, PaintColors.Length - 1);
            set { PlayerPrefs.SetInt(ColorKey, Mathf.Clamp(value, 0, PaintColors.Length - 1)); PlayerPrefs.Save(); }
        }

        public static Color PaintColor => PaintColors[PaintColorIndex].color;

        public static bool MotionBlur
        {
            get => PlayerPrefs.GetInt(MotionBlurKey, 1) == 1;
            set { PlayerPrefs.SetInt(MotionBlurKey, value ? 1 : 0); PlayerPrefs.Save(); }
        }
    }

    [System.Serializable]
    public struct NamedColor
    {
        public string name;
        public Color color;
        public NamedColor(string n, Color c) { name = n; color = c; }
    }
}
