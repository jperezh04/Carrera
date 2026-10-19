using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using StreetRacing.CameraSystem;
using StreetRacing.Core;
using StreetRacing.Environment;
using StreetRacing.Race;
using StreetRacing.UI;
using StreetRacing.Vehicle;

namespace StreetRacing.EditorTools
{
    /// <summary>
    /// Runs static and live PlayMode checks across MainMenu.unity and RaceTrack.unity
    /// and writes detailed verification logs to AgentBridge/.
    /// </summary>
    [InitializeOnLoad]
    public static class StreetRacingVerificationSuite
    {
        const string PlayTestKey = "SR_ActivePlayModeTest";
        static float phaseTimer;
        static int phase;
        static float maxForwardKmh;
        static float maxSteerDeg;
        static float maxLateralSlip;
        static float minReverseSpeed;
        static bool groundedObserved;
        static bool countdownLockedObserved;
        static bool paintRuntimeVerified;
        static bool pauseVerified;
        static bool resumeVerified;
        static bool finishScreenVerified;
        static bool playAgainVerified;
        static Vector3 rival1StartPos;
        static Vector3 rival2StartPos;
        static float rival1MaxDist;
        static float rival2MaxDist;
        static int rival1MaxWP;
        static int rival2MaxWP;

        static StreetRacingVerificationSuite()
        {
            EditorApplication.update += TickPlayModeTest;
        }

        [MenuItem("Tools/Street Racing 2000s/2. Run Automated Verification Suite", false, 2)]
        public static string RunStaticAndSceneChecks()
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== STREET RACING 2000s — STATIC & SCENE VERIFICATION REPORT ===");

            string menuPath = "Assets/Game/Scenes/MainMenu.unity";
            string racePath = "Assets/Game/Scenes/RaceTrack.unity";
            sb.AppendLine($"MainMenu.unity exists: {File.Exists(menuPath)}");
            sb.AppendLine($"RaceTrack.unity exists: {File.Exists(racePath)}");

            EditorSceneManager.OpenScene(menuPath, OpenSceneMode.Single);
            var menuCtrl = Object.FindFirstObjectByType<MainMenuController>();
            sb.AppendLine($"[MainMenu] MainMenuController found: {menuCtrl != null}");
            if (menuCtrl)
            {
                sb.AppendLine($"[MainMenu] Play/Quit/Color buttons wired: play={menuCtrl.playButton != null}, quit={menuCtrl.quitButton != null}, swatches={(menuCtrl.colorSwatchButtons != null ? menuCtrl.colorSwatchButtons.Length : 0)}");
                sb.AppendLine($"[MainMenu] Preview CarPaint slots count: {(menuCtrl.previewCarPaint != null ? menuCtrl.previewCarPaint.slots.Count : 0)}");
                menuCtrl.SelectColor(1); // Blue
                sb.AppendLine($"[MainMenu] Color selection persisted index={GameSettings.PaintColorIndex} ({GameSettings.PaintColors[GameSettings.PaintColorIndex].name})");
                menuCtrl.SelectColor(0); // Reset to Red
            }

            EditorSceneManager.OpenScene(racePath, OpenSceneMode.Single);
            var track = Object.FindFirstObjectByType<TrackPath>();
            var car = Object.FindFirstObjectByType<ArcadeCarController>();
            var cam = Object.FindFirstObjectByType<RaceCamera>();
            var rm = Object.FindFirstObjectByType<RaceManager>();
            var ui = Object.FindFirstObjectByType<RaceUIController>();
            var wpMgr = Object.FindFirstObjectByType<WaypointCircuitManager>();
            var minimap = Object.FindFirstObjectByType<WaypointMinimapHUD>();
            var followCars = Object.FindObjectsByType<FollowWP>(FindObjectsSortMode.None);

            sb.AppendLine($"[RaceTrack] TrackPath found: {track != null}, sampledPoints={(track ? track.sampledPoints.Count : 0)}, totalLength={(track ? track.totalLength : 0f):F1}m");
            if (track && track.generatedGeometryRoot)
            {
                var roadCol = track.generatedGeometryRoot.Find("RoadSurface")?.GetComponent<MeshCollider>();
                var barCol = track.generatedGeometryRoot.Find("TrackBarriers")?.GetComponent<MeshCollider>();
                sb.AppendLine($"[RaceTrack] Road MeshCollider valid: {roadCol != null && roadCol.sharedMesh != null && roadCol.sharedMesh.vertexCount > 100}");
                sb.AppendLine($"[RaceTrack] Barrier MeshCollider valid: {barCol != null && barCol.sharedMesh != null && barCol.sharedMesh.vertexCount > 100}");
            }

            sb.AppendLine($"[RaceTrack] PlayerCar found: {car != null}, wheels={(car ? car.wheels.Length : 0)}");
            sb.AppendLine($"[RaceTrack] WaypointCircuitManager found: {wpMgr != null}, waypointsCount={(wpMgr && wpMgr.waypoints != null ? wpMgr.waypoints.Length : 0)}");
            sb.AppendLine($"[RaceTrack] FollowWP Rival Cars count: {followCars.Length}");
            for (int i = 0; i < followCars.Length; i++)
            {
                var fc = followCars[i];
                sb.AppendLine($"[RaceTrack] FollowWP[{i}] '{fc.name}': waypoints={(fc.waypoints != null ? fc.waypoints.Length : 0)}, speed={fc.speed}, rotSpeed={fc.rotSpeed}, laneOffset={fc.laneOffset}");
            }
            sb.AppendLine($"[RaceTrack] WaypointMinimapHUD found & wired: {minimap != null && minimap.mapArea != null && minimap.trackBackgroundImage != null && minimap.waypointManager == wpMgr}");
            sb.AppendLine($"[RaceTrack] RaceCamera found & targets car: {cam != null && cam.target == car}");
            sb.AppendLine($"[RaceTrack] RaceManager found: {rm != null}, checkpoints={(rm && rm.checkpoints != null ? rm.checkpoints.Count : 0)}, totalLaps={(rm ? rm.totalLaps : 0)}, rivalCars={(rm && rm.rivalCars != null ? rm.rivalCars.Length : 0)}");
            sb.AppendLine($"[RaceTrack] RaceUIController found & wired: {ui != null && ui.speedValueText != null && ui.lapCounterText != null && ui.pausePanel != null && ui.finishPanel != null}");

            if (rm && car && ui)
            {
                rm.StartNewRace();
                bool lockedDuringCountdown = car.Input.Locked && rm.State == RaceState.Countdown;
                sb.AppendLine($"[GameplayLogic] Countdown locks driving: {lockedDuringCountdown}");

                rm.BeginRacing();
                bool unlockedOnGo = !car.Input.Locked && rm.State == RaceState.Racing;
                sb.AppendLine($"[GameplayLogic] GO! unlocks driving: {unlockedOnGo}");

                int lapBeforeCheat = rm.CurrentLap;
                rm.OnCarEnteredCheckpoint(rm.checkpoints[0], car);
                bool antiCheatWorked = (rm.CurrentLap == lapBeforeCheat);
                sb.AppendLine($"[GameplayLogic] Anti-cheat prevents invalid lap on Start/Finish: {antiCheatWorked}");

                for (int lap = 1; lap <= rm.totalLaps; lap++)
                {
                    for (int c = 1; c < rm.checkpoints.Count; c++)
                    {
                        var cp = rm.checkpoints[c];
                        car.transform.SetPositionAndRotation(cp.transform.position, cp.transform.rotation);
                        rm.OnCarEnteredCheckpoint(cp, car);
                    }
                    var finishCp = rm.checkpoints[0];
                    car.transform.SetPositionAndRotation(finishCp.transform.position, finishCp.transform.rotation);
                    rm.OnCarEnteredCheckpoint(finishCp, car);
                }

                bool finishedOk = (rm.State == RaceState.Finished && rm.CompletedLapTimes.Count == rm.totalLaps);
                sb.AppendLine($"[GameplayLogic] Full {rm.totalLaps}-lap race completion & finish state: {finishedOk}");

                rm.RestartRace();
                int carCountAfterRestart = Object.FindObjectsByType<ArcadeCarController>(FindObjectsSortMode.None).Length;
                int camCountAfterRestart = Object.FindObjectsByType<RaceCamera>(FindObjectsSortMode.None).Length;
                int uiCountAfterRestart  = Object.FindObjectsByType<RaceUIController>(FindObjectsSortMode.None).Length;
                sb.AppendLine($"[GameplayLogic] RestartRace resets cleanly without duplicates: state={rm.State}, cars={carCountAfterRestart}, cams={camCountAfterRestart}, uis={uiCountAfterRestart}");

                if (track && track.spawnPoint)
                    car.ResetTo(track.spawnPoint.position, track.spawnPoint.rotation);
                if (cam) cam.Snap();
                EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            }

            string reportDir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "AgentBridge"));
            Directory.CreateDirectory(reportDir);
            File.WriteAllText(Path.Combine(reportDir, "verification_report.txt"), sb.ToString());
            Debug.Log(sb.ToString());
            return sb.ToString();
        }

        [MenuItem("Tools/Street Racing 2000s/3. Run Live PlayMode Physics Test", false, 3)]
        public static string StartPlayModeTest()
        {
            EditorSceneManager.OpenScene("Assets/Game/Scenes/RaceTrack.unity", OpenSceneMode.Single);
            EditorPrefs.SetInt(PlayTestKey, 1);
            phase = 0;
            phaseTimer = 0f;
            maxForwardKmh = 0f;
            maxSteerDeg = 0f;
            maxLateralSlip = 0f;
            minReverseSpeed = 0f;
            groundedObserved = false;
            countdownLockedObserved = false;
            paintRuntimeVerified = false;
            pauseVerified = false;
            resumeVerified = false;
            finishScreenVerified = false;
            playAgainVerified = false;
            rival1MaxDist = 0f;
            rival2MaxDist = 0f;
            rival1MaxWP = 0;
            rival2MaxWP = 0;
            EditorApplication.isPlaying = true;
            return "PlayModeTest_Started";
        }

        static void TickPlayModeTest()
        {
            if (EditorPrefs.GetInt(PlayTestKey, 0) != 1) return;
            if (!EditorApplication.isPlaying) return;

            var rm = RaceManager.Instance;
            var ui = Object.FindFirstObjectByType<RaceUIController>();
            if (!rm || !rm.playerCar || !ui) return;

            var car = rm.playerCar;
            float dt = Time.unscaledDeltaTime;
            if (dt > 0.2f) dt = 0.02f;
            phaseTimer += dt;

            if (car.IsGrounded) groundedObserved = true;
            if (car.SpeedKmh > maxForwardKmh && car.ForwardSpeed > 0f) maxForwardKmh = car.SpeedKmh;
            if (Mathf.Abs(car.SteerAngle) > maxSteerDeg) maxSteerDeg = Mathf.Abs(car.SteerAngle);
            foreach (var w in car.wheels)
                if (Mathf.Abs(w.lateralSlip) > maxLateralSlip) maxLateralSlip = Mathf.Abs(w.lateralSlip);
            if (car.ForwardSpeed < minReverseSpeed) minReverseSpeed = car.ForwardSpeed;

            if (rm.rivalCars != null && rm.rivalCars.Length >= 2)
            {
                if (rm.rivalCars[0])
                {
                    float d1 = Vector3.Distance(rival1StartPos, rm.rivalCars[0].transform.position);
                    if (d1 > rival1MaxDist) rival1MaxDist = d1;
                    if (rm.rivalCars[0].currentWP > rival1MaxWP) rival1MaxWP = rm.rivalCars[0].currentWP;
                }
                if (rm.rivalCars[1])
                {
                    float d2 = Vector3.Distance(rival2StartPos, rm.rivalCars[1].transform.position);
                    if (d2 > rival2MaxDist) rival2MaxDist = d2;
                    if (rm.rivalCars[1].currentWP > rival2MaxWP) rival2MaxWP = rm.rivalCars[1].currentWP;
                }
            }

            switch (phase)
            {
                case 0: // Verify countdown lock & CarPaint runtime material instancing, then start race
                    if (rm.State == RaceState.Countdown && car.Input.Locked)
                        countdownLockedObserved = true;

                    if (rm.rivalCars != null && rm.rivalCars.Length >= 2)
                    {
                        if (rm.rivalCars[0]) rival1StartPos = rm.rivalCars[0].transform.position;
                        if (rm.rivalCars[1]) rival2StartPos = rm.rivalCars[1].transform.position;
                    }

                    var paint = car.GetComponent<CarPaint>();
                    if (paint && paint.slots.Count > 0)
                    {
                        Color testBlue = GameSettings.PaintColors[1].color;
                        paint.SetColor(testBlue);
                        Color applied = paint.slots[0].renderer.sharedMaterials[paint.slots[0].materialIndices[0]].GetColor("_BaseColor");
                        paintRuntimeVerified = Vector4.Distance(applied, testBlue) < 0.05f;
                        paint.SetColor(GameSettings.PaintColor);
                    }

                    if (phaseTimer > 0.4f)
                    {
                        rm.BeginRacing();
                        phase = 1;
                        phaseTimer = 0f;
                    }
                    break;

                case 1: // Full throttle acceleration on straight (W)
                    car.Input.externalOverride = () => new Vector4(1f, 0f, 0f, 0f);
                    if (phaseTimer > 2.0f)
                    {
                        phase = 2;
                        phaseTimer = 0f;
                    }
                    break;

                case 2: // Steering + Handbrake drift (W + D + Space)
                    car.Input.externalOverride = () => new Vector4(1f, 0f, 0.9f, 1f);
                    if (phaseTimer > 1.2f)
                    {
                        phase = 3;
                        phaseTimer = 0f;
                    }
                    break;

                case 3: // Full braking and reverse (S)
                    car.Input.externalOverride = () => new Vector4(0f, 1f, 0f, 0f);
                    if (phaseTimer > 2.4f)
                    {
                        car.Input.externalOverride = null;
                        phase = 4;
                        phaseTimer = 0f;
                    }
                    break;

                case 4: // Test safe reset (R), Pause & Resume
                    rm.ResetCarToSafePosition();
                    rm.Pause();
                    pauseVerified = (rm.State == RaceState.Paused && Time.timeScale == 0f && ui.pausePanel.activeSelf);
                    rm.Resume();
                    resumeVerified = (rm.State == RaceState.Racing && Time.timeScale == 1f && !ui.pausePanel.activeSelf);
                    phase = 5;
                    phaseTimer = 0f;
                    break;

                case 5: // Complete all laps via checkpoints & test Finish + Play Again
                    int totalLapsToRun = rm.totalLaps;
                    for (int lap = 1; lap <= totalLapsToRun; lap++)
                    {
                        for (int c = 1; c < rm.checkpoints.Count; c++)
                        {
                            var cp = rm.checkpoints[c];
                            car.transform.SetPositionAndRotation(cp.transform.position, cp.transform.rotation);
                            rm.OnCarEnteredCheckpoint(cp, car);
                        }
                        var fin = rm.checkpoints[0];
                        car.transform.SetPositionAndRotation(fin.transform.position, fin.transform.rotation);
                        rm.OnCarEnteredCheckpoint(fin, car);
                    }
                    finishScreenVerified = (rm.State == RaceState.Finished && ui.finishPanel.activeSelf);

                    ui.OnRestartClicked();
                    playAgainVerified = (rm.State == RaceState.Countdown && rm.CurrentLap == 1 && !ui.finishPanel.activeSelf);
                    phase = 6;
                    break;

                case 6: // Write report and exit PlayMode
                    EditorPrefs.SetInt(PlayTestKey, 0);
                    var sb = new StringBuilder();
                    sb.AppendLine("=== STREET RACING 2000s — LIVE PLAYMODE PHYSICS REPORT ===");
                    sb.AppendLine($"CountdownLockedObserved: {countdownLockedObserved}");
                    sb.AppendLine($"PaintRuntimeVerified: {paintRuntimeVerified}");
                    sb.AppendLine($"GroundedObserved: {groundedObserved}");
                    sb.AppendLine($"PeakForwardSpeedKmh (2.0s throttle): {maxForwardKmh:F1} km/h");
                    sb.AppendLine($"MaxSteerAngleDeg: {maxSteerDeg:F1} deg");
                    sb.AppendLine($"MaxLateralSlip (Handbrake Drift): {maxLateralSlip:F2} m/s");
                    sb.AppendLine($"ReverseSpeedReached: {minReverseSpeed * 3.6f:F1} km/h (IsReversing={minReverseSpeed < -0.5f})");
                    sb.AppendLine($"Rival1_FollowWP_Progress: dist={rival1MaxDist:F1}m, reachedWP={rival1MaxWP}");
                    sb.AppendLine($"Rival2_FollowWP_Progress: dist={rival2MaxDist:F1}m, reachedWP={rival2MaxWP}");
                    sb.AppendLine($"PauseVerified (timeScale=0 & UI): {pauseVerified}");
                    sb.AppendLine($"ResumeVerified (timeScale=1 & UI): {resumeVerified}");
                    sb.AppendLine($"FinishScreenVerified: {finishScreenVerified}");
                    sb.AppendLine($"PlayAgainRestartVerified: {playAgainVerified}");

                    string reportDir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "AgentBridge"));
                    Directory.CreateDirectory(reportDir);
                    File.WriteAllText(Path.Combine(reportDir, "playmode_report.txt"), sb.ToString());
                    Debug.Log(sb.ToString());
                    EditorApplication.isPlaying = false;
                    break;
            }
        }
    }
}
