using UnityEngine;
using UnityEngine.UI;
using StreetRacing.Core;
using StreetRacing.Race;

namespace StreetRacing.UI
{
    /// <summary>
    /// Updates the in-race HUD (speed in km/h, scale/bar, lap counter, lap timer, best lap, countdown)
    /// and manages the Pause and Race Finished screens.
    /// </summary>
    public class RaceUIController : MonoBehaviour
    {
        public RaceManager raceManager;

        [Header("HUD Elements")]
        public GameObject hudRoot;
        public Text speedValueText;
        public Text speedMaxLabelText;
        public Text gearText;
        public Image speedBarFill;
        public Image rpmBarFill;
        public Text lapCounterText;
        public Text checkpointCounterText;
        public Text currentLapTimeText;
        public Text bestLapTimeText;
        public Text countdownCenterText;
        public Text lapBannerText;

        [Header("Pause Menu")]
        public GameObject pausePanel;
        public Button resumeButton;
        public Button pauseRestartButton;
        public Button pauseMainMenuButton;

        [Header("Finish Screen")]
        public GameObject finishPanel;
        public Text finishTitleText;
        public Text finishTotalTimeText;
        public Text finishBestLapText;
        public Button playAgainButton;
        public Button finishMainMenuButton;

        float bannerTimer;

        void Start()
        {
            if (!raceManager) raceManager = RaceManager.Instance ? RaceManager.Instance : FindFirstObjectByType<RaceManager>();

            if (resumeButton)
            {
                resumeButton.onClick.RemoveAllListeners();
                resumeButton.onClick.AddListener(OnResumeClicked);
            }
            if (pauseRestartButton)
            {
                pauseRestartButton.onClick.RemoveAllListeners();
                pauseRestartButton.onClick.AddListener(OnRestartClicked);
            }
            if (pauseMainMenuButton)
            {
                pauseMainMenuButton.onClick.RemoveAllListeners();
                pauseMainMenuButton.onClick.AddListener(OnMainMenuClicked);
            }
            if (playAgainButton)
            {
                playAgainButton.onClick.RemoveAllListeners();
                playAgainButton.onClick.AddListener(OnRestartClicked);
            }
            if (finishMainMenuButton)
            {
                finishMainMenuButton.onClick.RemoveAllListeners();
                finishMainMenuButton.onClick.AddListener(OnMainMenuClicked);
            }

            if (raceManager)
            {
                raceManager.OnPauseChanged += HandlePauseChanged;
                raceManager.OnRaceFinished += HandleRaceFinished;
                raceManager.OnLapCompleted += HandleLapCompleted;
            }

            if (pausePanel) pausePanel.SetActive(false);
            if (finishPanel) finishPanel.SetActive(false);
            if (lapBannerText) lapBannerText.text = "";
        }

        void OnDestroy()
        {
            if (raceManager)
            {
                raceManager.OnPauseChanged -= HandlePauseChanged;
                raceManager.OnRaceFinished -= HandleRaceFinished;
                raceManager.OnLapCompleted -= HandleLapCompleted;
            }
        }

        void Update()
        {
            if (!raceManager) return;

            var car = raceManager.playerCar;
            if (car)
            {
                int kmh = Mathf.RoundToInt(car.SpeedKmh);
                if (speedValueText) speedValueText.text = kmh.ToString("000");
                if (speedMaxLabelText) speedMaxLabelText.text = $"KM/H   |   ESCALA MAX {Mathf.RoundToInt(car.maxSpeedKmh)} KM/H";
                if (speedBarFill) speedBarFill.fillAmount = Mathf.Clamp01(car.SpeedKmh / Mathf.Max(1f, car.maxSpeedKmh));

                var audio = car.GetComponent<Vehicle.CarAudio>();
                if (gearText)
                {
                    string g = car.IsReversing ? "R" : (audio ? audio.Gear.ToString() : "D");
                    gearText.text = car.DriftAmount > 0.35f ? $"MARCHA {g}  [DERRAPE]" : $"MARCHA {g}";
                }
                if (rpmBarFill && audio)
                    rpmBarFill.fillAmount = Mathf.Clamp01(audio.Rpm01);
            }

            if (lapCounterText)
                lapCounterText.text = $"VUELTA  {raceManager.CurrentLap} / {raceManager.totalLaps}";

            if (checkpointCounterText && raceManager.checkpoints != null && raceManager.checkpoints.Count > 0)
            {
                int totalCp = raceManager.checkpoints.Count;
                int passed = raceManager.NextCheckpointIndex == 0 ? totalCp : raceManager.NextCheckpointIndex - 1;
                checkpointCounterText.text = $"SECTOR  {passed} / {totalCp}";
            }

            if (currentLapTimeText)
                currentLapTimeText.text = $"TIEMPO VUELTA:  {RaceManager.FormatTime(raceManager.CurrentLapTime)}";

            if (bestLapTimeText)
                bestLapTimeText.text = $"MEJOR VUELTA:   {RaceManager.FormatTime(raceManager.BestLapTime)}";

            if (countdownCenterText)
            {
                string cd = raceManager.CountdownText;
                countdownCenterText.gameObject.SetActive(!string.IsNullOrEmpty(cd));
                countdownCenterText.text = cd;
                countdownCenterText.color = cd == "GO!"
                    ? new Color(0.25f, 1f, 0.45f)
                    : new Color(1f, 0.76f, 0.15f);
            }

            if (bannerTimer > 0f)
            {
                bannerTimer -= Time.deltaTime;
                if (bannerTimer <= 0f && lapBannerText)
                    lapBannerText.text = "";
            }
        }

        void HandleLapCompleted(int lapNumber, float lapTime, bool isNewBest)
        {
            if (!lapBannerText) return;
            string suffix = isNewBest ? "  ★ ¡NUEVA MEJOR VUELTA!" : "";
            lapBannerText.text = $"VUELTA {lapNumber} COMPLETADA — {RaceManager.FormatTime(lapTime)}{suffix}";
            bannerTimer = 3.2f;
        }

        void HandlePauseChanged(bool paused)
        {
            if (pausePanel) pausePanel.SetActive(paused);
            if (!paused && finishPanel && raceManager && raceManager.State != RaceState.Finished)
                finishPanel.SetActive(false);
        }

        void HandleRaceFinished()
        {
            if (pausePanel) pausePanel.SetActive(false);
            if (finishPanel) finishPanel.SetActive(true);
            if (finishTitleText) finishTitleText.text = "¡CARRERA COMPLETADA!";
            if (finishTotalTimeText) finishTotalTimeText.text = $"TIEMPO TOTAL:   {RaceManager.FormatTime(raceManager.TotalRaceTime)}";
            if (finishBestLapText) finishBestLapText.text = $"MEJOR VUELTA:   {RaceManager.FormatTime(raceManager.BestLapTime)}";
        }

        public void OnResumeClicked()
        {
            if (raceManager) raceManager.Resume();
        }

        public void OnRestartClicked()
        {
            if (pausePanel) pausePanel.SetActive(false);
            if (finishPanel) finishPanel.SetActive(false);
            if (lapBannerText) lapBannerText.text = "";
            if (raceManager) raceManager.RestartRace();
        }

        public void OnMainMenuClicked()
        {
            SceneLoader.LoadMainMenu();
        }
    }
}
