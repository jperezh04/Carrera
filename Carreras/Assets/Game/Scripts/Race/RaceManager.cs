using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace StreetRacing.Race
{
    public enum RaceState
    {
        Countdown,
        Racing,
        Paused,
        Finished
    }

    /// <summary>
    /// Controls the full race lifecycle: countdown, ordered checkpoint validation,
    /// lap timing, best lap tracking, vehicle reset to safe track pose, pause, and restart.
    /// </summary>
    public class RaceManager : MonoBehaviour
    {
        public static RaceManager Instance { get; private set; }

        [Header("Configuración de Carrera")]
        [Min(1)]
        [Tooltip("Número total de vueltas para completar la carrera.")]
        public int totalLaps = 3;

        [Min(1)]
        [Tooltip("Segundos de cuenta regresiva (3, 2, 1, GO!).")]
        public int countdownSteps = 3;

        [Tooltip("Duración de cada paso de la cuenta regresiva en segundos.")]
        public float countdownStepDuration = 1.0f;

        [Header("Referencias de Escena")]
        public Vehicle.ArcadeCarController playerCar;
        public FollowWP[] rivalCars;
        public CameraSystem.RaceCamera raceCamera;
        public Environment.TrackPath trackPath;
        public Volume postProcessVolume;
        public List<RaceCheckpoint> checkpoints = new List<RaceCheckpoint>();

        // ---- Estado público para HUD y pruebas ----
        public RaceState State { get; private set; } = RaceState.Countdown;
        public int CurrentLap { get; private set; } = 1;
        public int NextCheckpointIndex { get; private set; } = 1;
        public int LastPassedCheckpointIndex { get; private set; } = 0;
        public float CurrentLapTime { get; private set; }
        public float LastLapTime { get; private set; }
        public float BestLapTime { get; private set; }
        public float TotalRaceTime { get; private set; }
        public string CountdownText { get; private set; } = "";
        public float GoBannerTimer { get; private set; }
        public readonly List<float> CompletedLapTimes = new List<float>();

        public event System.Action<int, float, bool> OnLapCompleted; // (lapNumber, lapTime, isNewBest)
        public event System.Action OnRaceFinished;
        public event System.Action<bool> OnPauseChanged;

        float countdownTimer;
        RaceState stateBeforePause = RaceState.Racing;

        void Awake()
        {
            Instance = this;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            Time.timeScale = 1f;
            AudioListener.pause = false;
        }

        void Start()
        {
            DiscoverReferencesIfNeeded();
            ApplyPostProcessSettings();
            if (playerCar)
                playerCar.ResetRequested += ResetCarToSafePosition;

            StartNewRace();
        }

        public void DiscoverReferencesIfNeeded()
        {
            if (!trackPath) trackPath = FindFirstObjectByType<Environment.TrackPath>();
            if (!playerCar) playerCar = FindFirstObjectByType<Vehicle.ArcadeCarController>();
            if (rivalCars == null || rivalCars.Length == 0)
                rivalCars = FindObjectsByType<FollowWP>(FindObjectsSortMode.None);
            if (!raceCamera) raceCamera = FindFirstObjectByType<CameraSystem.RaceCamera>();
            if (!postProcessVolume) postProcessVolume = FindFirstObjectByType<Volume>();

            if ((checkpoints == null || checkpoints.Count == 0) && trackPath && trackPath.checkpointsRoot)
            {
                checkpoints = new List<RaceCheckpoint>(
                    trackPath.checkpointsRoot.GetComponentsInChildren<RaceCheckpoint>(false));
                checkpoints.Sort((a, b) => a.index.CompareTo(b.index));
            }
            else if (checkpoints == null || checkpoints.Count == 0)
            {
                checkpoints = new List<RaceCheckpoint>(FindObjectsByType<RaceCheckpoint>(FindObjectsSortMode.None));
                checkpoints.Sort((a, b) => a.index.CompareTo(b.index));
            }
        }

        public void ApplyPostProcessSettings()
        {
            if (!postProcessVolume || !postProcessVolume.profile) return;
            if (postProcessVolume.profile.TryGet<MotionBlur>(out var mb))
                mb.active = Core.GameSettings.MotionBlur;
        }

        /// <summary>Resets timers, lap counters, car position and starts the 3-2-1-GO countdown without reloading or duplicating objects.</summary>
        public void StartNewRace()
        {
            Time.timeScale = 1f;
            AudioListener.pause = false;

            DiscoverReferencesIfNeeded();
            CompletedLapTimes.Clear();
            CurrentLap = 1;
            LastPassedCheckpointIndex = 0;
            NextCheckpointIndex = (checkpoints != null && checkpoints.Count > 1) ? 1 : 0;
            CurrentLapTime = 0f;
            LastLapTime = 0f;
            TotalRaceTime = 0f;

            // Place car at SpawnPoint
            if (playerCar)
            {
                Vector3 spawnPos = Vector3.up * 0.25f;
                Quaternion spawnRot = Quaternion.identity;
                if (trackPath && trackPath.spawnPoint)
                {
                    spawnPos = trackPath.spawnPoint.position;
                    spawnRot = trackPath.spawnPoint.rotation;
                }
                else if (trackPath)
                {
                    trackPath.EvaluateAtNormalized(0.985f, out spawnPos, out Vector3 fwd, out _);
                    spawnPos += Vector3.up * 0.2f;
                    spawnRot = Quaternion.LookRotation(fwd, Vector3.up);
                }
                playerCar.ResetTo(spawnPos, spawnRot);
                var fx = playerCar.GetComponent<Vehicle.CarEffects>();
                if (fx) fx.ResetMarks();
                if (Vehicle.SkidmarkManager.Instance) Vehicle.SkidmarkManager.Instance.Clear();

                if (playerCar.Input) playerCar.Input.Locked = true;
            }

            if (rivalCars != null)
            {
                foreach (var rival in rivalCars)
                {
                    if (rival) rival.ResetToStart();
                }
            }

            if (raceCamera) raceCamera.Snap();

            State = RaceState.Countdown;
            countdownTimer = Mathf.Max(1, countdownSteps) * Mathf.Max(0.25f, countdownStepDuration);
            GoBannerTimer = 0f;
            UpdateCountdownLabel();
            OnPauseChanged?.Invoke(false);
        }

        void Update()
        {
            var kb = Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame && State != RaceState.Finished)
                TogglePause();

            if (State == RaceState.Paused) return;

            float dt = Time.deltaTime;
            if (State == RaceState.Countdown)
            {
                countdownTimer -= dt;
                if (countdownTimer > 0f)
                {
                    UpdateCountdownLabel();
                }
                else
                {
                    BeginRacing();
                }
            }
            else if (State == RaceState.Racing)
            {
                CurrentLapTime += dt;
                TotalRaceTime += dt;
                if (GoBannerTimer > 0f)
                {
                    GoBannerTimer -= dt;
                    if (GoBannerTimer <= 0f) CountdownText = "";
                }
            }
        }

        void UpdateCountdownLabel()
        {
            int step = Mathf.CeilToInt(countdownTimer / Mathf.Max(0.25f, countdownStepDuration));
            CountdownText = Mathf.Max(1, step).ToString();
        }

        public void BeginRacing()
        {
            State = RaceState.Racing;
            CountdownText = "GO!";
            GoBannerTimer = 1.1f;
            if (playerCar && playerCar.Input)
                playerCar.Input.Locked = false;
        }

        public void OnCarEnteredCheckpoint(RaceCheckpoint cp, Vehicle.ArcadeCarController car)
        {
            if (State != RaceState.Racing || car != playerCar) return;
            if (checkpoints == null || checkpoints.Count == 0) return;

            // Prevent wrong-way trigger activation
            if (Vector3.Dot(car.transform.forward, cp.transform.forward) < -0.25f)
                return;

            int count = checkpoints.Count;
            if (cp.index == NextCheckpointIndex)
            {
                LastPassedCheckpointIndex = cp.index;

                // Crossing Start/Finish (index 0) after having passed all intermediate checkpoints completes a lap
                if (cp.index == 0)
                {
                    CompleteCurrentLap();
                    NextCheckpointIndex = count > 1 ? 1 : 0;
                }
                else
                {
                    NextCheckpointIndex = (cp.index + 1) % count;
                }
            }
        }

        void CompleteCurrentLap()
        {
            float lapTime = CurrentLapTime;
            LastLapTime = lapTime;
            CompletedLapTimes.Add(lapTime);

            bool isNewBest = BestLapTime <= 0.001f || lapTime < BestLapTime;
            if (isNewBest) BestLapTime = lapTime;

            int finishedLap = CurrentLap;
            OnLapCompleted?.Invoke(finishedLap, lapTime, isNewBest);

            if (CurrentLap >= totalLaps)
            {
                FinishRace();
            }
            else
            {
                CurrentLap++;
                CurrentLapTime = 0f;
            }
        }

        void FinishRace()
        {
            State = RaceState.Finished;
            CountdownText = "";
            if (playerCar && playerCar.Input)
                playerCar.Input.Locked = true;
            OnRaceFinished?.Invoke();
        }

        public void ResetCarToSafePosition()
        {
            if (!playerCar || State == RaceState.Paused || State == RaceState.Finished) return;

            Vector3 safePos = playerCar.transform.position + Vector3.up * 0.5f;
            Quaternion safeRot = playerCar.transform.rotation;

            if (checkpoints != null && LastPassedCheckpointIndex >= 0 && LastPassedCheckpointIndex < checkpoints.Count && checkpoints[LastPassedCheckpointIndex])
            {
                Transform cpTf = checkpoints[LastPassedCheckpointIndex].transform;
                safePos = cpTf.position;
                safePos.y -= 1.75f; // Box trigger is centered +2m above road
                safeRot = cpTf.rotation;
            }
            else if (trackPath)
            {
                int idx = trackPath.GetClosestSampleIndex(playerCar.transform.position);
                safePos = trackPath.sampledPoints[idx] + Vector3.up * 0.25f;
                safeRot = Quaternion.LookRotation(trackPath.sampledTangents[idx], Vector3.up);
            }

            playerCar.ResetTo(safePos, safeRot);
            var fx = playerCar.GetComponent<Vehicle.CarEffects>();
            if (fx) fx.ResetMarks();
        }

        public void TogglePause()
        {
            if (State == RaceState.Paused) Resume();
            else if (State == RaceState.Countdown || State == RaceState.Racing) Pause();
        }

        public void Pause()
        {
            if (State == RaceState.Paused || State == RaceState.Finished) return;
            stateBeforePause = State;
            State = RaceState.Paused;
            Time.timeScale = 0f;
            AudioListener.pause = true;
            if (playerCar && playerCar.Input) playerCar.Input.Locked = true;
            OnPauseChanged?.Invoke(true);
        }

        public void Resume()
        {
            if (State != RaceState.Paused) return;
            State = stateBeforePause;
            Time.timeScale = 1f;
            AudioListener.pause = false;
            if (playerCar && playerCar.Input)
                playerCar.Input.Locked = (State != RaceState.Racing);
            OnPauseChanged?.Invoke(false);
        }

        public void RestartRace()
        {
            StartNewRace();
        }

        public static string FormatTime(float seconds)
        {
            if (seconds <= 0.001f) return "--:--.---";
            int mins = Mathf.FloorToInt(seconds / 60f);
            float secs = seconds - mins * 60f;
            int wholeSecs = Mathf.FloorToInt(secs);
            int millis = Mathf.FloorToInt((secs - wholeSecs) * 1000f);
            return $"{mins:00}:{wholeSecs:00}.{millis:000}";
        }
    }
}
