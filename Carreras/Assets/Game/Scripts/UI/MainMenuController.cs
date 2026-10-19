using UnityEngine;
using UnityEngine.UI;
using StreetRacing.Core;
using StreetRacing.Vehicle;

namespace StreetRacing.UI
{
    /// <summary>
    /// Controls the Main Menu scene: live 3D car preview turntable, body paint colour selection,
    /// Play Race button and Quit button.
    /// </summary>
    public class MainMenuController : MonoBehaviour
    {
        [Header("Vista Previa 3D")]
        public Transform turntablePlatform;
        public CarPaint previewCarPaint;
        public float turntableSpeed = 18f;

        [Header("Botones Principales")]
        public Button playButton;
        public Button quitButton;
        public Button prevColorButton;
        public Button nextColorButton;

        [Header("Selección Directa de Color")]
        public Button[] colorSwatchButtons;
        public Text selectedColorLabel;
        public Image selectedColorSwatchPreview;

        void Start()
        {
            Time.timeScale = 1f;
            AudioListener.pause = false;

            if (!previewCarPaint)
                previewCarPaint = FindFirstObjectByType<CarPaint>();

            if (playButton)
            {
                playButton.onClick.RemoveAllListeners();
                playButton.onClick.AddListener(StartRace);
            }
            if (quitButton)
            {
                quitButton.onClick.RemoveAllListeners();
                quitButton.onClick.AddListener(SceneLoader.Quit);
            }
            if (prevColorButton)
            {
                prevColorButton.onClick.RemoveAllListeners();
                prevColorButton.onClick.AddListener(SelectPreviousColor);
            }
            if (nextColorButton)
            {
                nextColorButton.onClick.RemoveAllListeners();
                nextColorButton.onClick.AddListener(SelectNextColor);
            }

            if (colorSwatchButtons != null)
            {
                for (int i = 0; i < colorSwatchButtons.Length; i++)
                {
                    int capturedIndex = i;
                    Button btn = colorSwatchButtons[i];
                    if (!btn) continue;
                    btn.onClick.RemoveAllListeners();
                    btn.onClick.AddListener(() => SelectColor(capturedIndex));
                }
            }

            ApplyCurrentColor();
        }

        void Update()
        {
            if (turntablePlatform)
                turntablePlatform.Rotate(0f, turntableSpeed * Time.deltaTime, 0f, Space.Self);
        }

        public void SelectColor(int index)
        {
            int count = GameSettings.PaintColors.Length;
            GameSettings.PaintColorIndex = ((index % count) + count) % count;
            ApplyCurrentColor();
        }

        public void SelectNextColor()
        {
            SelectColor(GameSettings.PaintColorIndex + 1);
        }

        public void SelectPreviousColor()
        {
            SelectColor(GameSettings.PaintColorIndex - 1);
        }

        void ApplyCurrentColor()
        {
            var entry = GameSettings.PaintColors[GameSettings.PaintColorIndex];
            if (previewCarPaint)
                previewCarPaint.SetColor(entry.color);
            if (selectedColorLabel)
                selectedColorLabel.text = $"COLOR DE CARROCERÍA:  {entry.name}";
            if (selectedColorSwatchPreview)
                selectedColorSwatchPreview.color = entry.color;
        }

        public void StartRace()
        {
            SceneLoader.LoadRace();
        }
    }
}
