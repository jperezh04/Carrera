using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using StreetRacing.CameraSystem;
using StreetRacing.Core;
using StreetRacing.Environment;
using StreetRacing.Race;
using StreetRacing.UI;
using StreetRacing.Vehicle;

namespace StreetRacing.EditorTools
{
    /// <summary>
    /// Builds and wires all materials, physics materials, volume profiles, prefabs,
    /// MainMenu.unity, RaceTrack.unity, and Build Settings for the Street Racing 2000s prototype.
    /// </summary>
    public static class StreetRacingProjectBuilder
    {
        const string GameRoot = "Assets/Game";
        const string MatVehicleDir = "Assets/Game/Materials/Vehicle";
        const string MatRoadDir = "Assets/Game/Materials/Road";
        const string MatEnvDir = "Assets/Game/Materials/Environment";
        const string MatFxDir = "Assets/Game/Materials/Effects";
        const string PrefabVehicleDir = "Assets/Game/Prefabs/Vehicle";
        const string PrefabTrackDir = "Assets/Game/Prefabs/Track";
        const string PrefabEnvDir = "Assets/Game/Prefabs/Environment";
        const string PrefabUIDir = "Assets/Game/Prefabs/UI";
        const string ScenesDir = "Assets/Game/Scenes";
        const string SettingsDir = "Assets/Game/Settings";
        const string DocsDir = "Assets/Game/Documentation";

        class BuiltAssets
        {
            public Material carPaintMat;
            public Material carCarbonMat;
            public Material carChromeMat;
            public Material carDarkMetalMat;
            public Material carRimMat;
            public Material carRubberMat;
            public Material carInteriorMat;
            public Material carGlassMat;
            public Material carHeadlightMat;
            public Material carTaillightMat;
            public Material skylineBodyMat;
            public Material skylineWheelMat;
            public Material rivalPaintOrangeMat;
            public Material rivalPaintYellowMat;

            public Material roadAsphaltMat;
            public Material roadCurbMat;
            public Material roadLinesMat;
            public Material barrierWallMat;
            public Material startCheckersMat;
            public Material warmNeonMat;
            public Material cyanNeonMat;
            public Material cityGroundMat;
            public Material kenneyCommercialMat;
            public Material kenneyRoadsMat;
            public Material skidmarkMat;
            public Material smokeParticleMat;

            public PhysicsMaterial asphaltPhysMat;
            public PhysicsMaterial barrierZeroFrictionPhysMat;
            public VolumeProfile nightVolumeProfile;
            public Font mainFont;

            public GameObject coupeCarPrefab;
            public GameObject skylineCarPrefab;
        }

        [MenuItem("Tools/Street Racing 2000s/1. Build & Wire Complete Game (Scenes + Prefabs)", false, 1)]
        public static string BuildAll()
        {
            EnsureDirectories();
            ConfigureUrpAssetForNightRacing();

            var assets = CreateMaterialsAndSettings();
            UpgradeKenneyEnvPrefabs(assets);
            assets.coupeCarPrefab = BuildCoupeVehiclePrefab(assets);
            assets.skylineCarPrefab = BuildSkylineVehiclePrefab(assets);

            string menuScenePath = $"{ScenesDir}/{SceneLoader.MainMenuScene}.unity";
            string raceScenePath = $"{ScenesDir}/{SceneLoader.RaceScene}.unity";

            BuildMainMenuScene(menuScenePath, assets);
            BuildRaceTrackScene(raceScenePath, assets);
            UpdateEditorBuildSettings(menuScenePath, raceScenePath);

            // Leave RaceTrack open in the Editor so the user can immediately inspect/edit or press Play
            EditorSceneManager.OpenScene(raceScenePath, OpenSceneMode.Single);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[StreetRacingBuilder] BuildAll completed successfully. MainMenu.unity and RaceTrack.unity are ready.");
            return "BuildAll_OK";
        }

        static void EnsureDirectories()
        {
            string[] dirs =
            {
                GameRoot,
                MatVehicleDir, MatRoadDir, MatEnvDir, MatFxDir,
                PrefabVehicleDir, PrefabTrackDir, PrefabEnvDir, PrefabUIDir,
                ScenesDir, SettingsDir, DocsDir,
                "Assets/Game/Textures", "Assets/Game/Audio"
            };
            foreach (string d in dirs)
            {
                if (!Directory.Exists(d))
                    Directory.CreateDirectory(d);
            }
            AssetDatabase.Refresh();
        }

        static void ConfigureUrpAssetForNightRacing()
        {
            var rp = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/Settings/PC_RPAsset.asset");
            if (!rp) return;
            var so = new SerializedObject(rp);
            var propLimit = so.FindProperty("m_AdditionalLightsPerObjectLimit");
            if (propLimit != null) propLimit.intValue = 8;
            var propShadowDist = so.FindProperty("m_ShadowDistance");
            if (propShadowDist != null) propShadowDist.floatValue = 90f;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(rp);
        }

        static BuiltAssets CreateMaterialsAndSettings()
        {
            var b = new BuiltAssets();
            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
            Shader urpParticles = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (!urpParticles) urpParticles = urpLit;

            // Load useful textures already in the project
            var carbonAlbedo = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/HDRPLab/SharedAssets/Shaders/HDRPMML/Textures/CarbonFiber_BC.png");
            var carbonNormal = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/HDRPLab/SharedAssets/Shaders/HDRPMML/Textures/CarbonFiber_NM.png");
            var concreteGround = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/HDRPLab/Scenes/HDRPLabController/Textures/HDRPLab/Concrete_Ground.png");
            var concreteNormal = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/HDRPLab/Scenes/HDRPLabController/Textures/HDRPLab/Normal_1024_01_NM.png");
            var checkersTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/kenney_racing-kit/Models/FBX format/Textures/checkers.png");
            var commercialMap = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/kenney_city-kit-commercial_2.1/Models/FBX format/Textures/colormap.png");
            var roadsMap = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/kenney_city-kit-roads/Models/FBX format/Textures/colormap.png");
            var smokeTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Realistic Car Controller Pro/Textures/Particles/Smoke.png");
            var skidTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Realistic Car Controller Pro/Textures/Decals/Skidmarks.png");

            // Vehicle materials
            b.carPaintMat = CreateOrUpdateLitMaterial($"{MatVehicleDir}/Mat_CarBodyPaint.mat", urpLit,
                GameSettings.PaintColors[0].color, 0.62f, 0.88f, null, null, Vector2.one, false, Color.black);
            if (b.carPaintMat.HasProperty("_ClearCoatMask"))
            {
                b.carPaintMat.SetFloat("_ClearCoatMask", 1f);
                b.carPaintMat.SetFloat("_ClearCoatSmoothness", 0.9f);
            }

            b.carCarbonMat = CreateOrUpdateLitMaterial($"{MatVehicleDir}/Mat_CarCarbon.mat", urpLit,
                new Color(0.22f, 0.22f, 0.24f), 0.35f, 0.72f, carbonAlbedo, carbonNormal, new Vector2(12f, 12f), false, Color.black);
            b.carChromeMat = CreateOrUpdateLitMaterial($"{MatVehicleDir}/Mat_CarChrome.mat", urpLit,
                new Color(0.86f, 0.88f, 0.92f), 0.95f, 0.92f, null, null, Vector2.one, false, Color.black);
            b.carDarkMetalMat = CreateOrUpdateLitMaterial($"{MatVehicleDir}/Mat_CarDarkMetal.mat", urpLit,
                new Color(0.16f, 0.17f, 0.19f), 0.75f, 0.58f, null, null, Vector2.one, false, Color.black);
            b.carRimMat = CreateOrUpdateLitMaterial($"{MatVehicleDir}/Mat_CarRim.mat", urpLit,
                new Color(0.78f, 0.80f, 0.84f), 0.88f, 0.82f, null, null, Vector2.one, false, Color.black);
            b.carRubberMat = CreateOrUpdateLitMaterial($"{MatVehicleDir}/Mat_CarRubber.mat", urpLit,
                new Color(0.10f, 0.10f, 0.11f), 0.05f, 0.28f, null, null, Vector2.one, false, Color.black);
            b.carInteriorMat = CreateOrUpdateLitMaterial($"{MatVehicleDir}/Mat_CarInterior.mat", urpLit,
                new Color(0.13f, 0.13f, 0.15f), 0.08f, 0.35f, null, null, Vector2.one, false, Color.black);

            b.carGlassMat = CreateOrUpdateLitMaterial($"{MatVehicleDir}/Mat_CarGlass.mat", urpLit,
                new Color(0.05f, 0.07f, 0.10f, 0.72f), 0.2f, 0.95f, null, null, Vector2.one, false, Color.black);
            SetMaterialTransparent(b.carGlassMat);

            b.carHeadlightMat = CreateOrUpdateLitMaterial($"{MatVehicleDir}/Mat_CarHeadlightGlow.mat", urpLit,
                Color.white, 0.1f, 0.9f, null, null, Vector2.one, true, new Color(4.5f, 4.2f, 3.4f));
            b.carTaillightMat = CreateOrUpdateLitMaterial($"{MatVehicleDir}/Mat_CarTaillightGlow.mat", urpLit,
                new Color(0.9f, 0.05f, 0.02f), 0.1f, 0.88f, null, null, Vector2.one, true, new Color(2.5f, 0.06f, 0.02f));

            // Skyline fallback vehicle URP materials
            var skyDetail = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Realistic Car Controller Pro/Models/Prototype Vehicle/Skyline (Prototype)/Tex/skylineDetail2.png");
            var skyNormal = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Realistic Car Controller Pro/Models/Prototype Vehicle/Skyline (Prototype)/Tex/skylineColor_N.png");
            b.skylineBodyMat = CreateOrUpdateLitMaterial($"{MatVehicleDir}/Mat_SkylineBody.mat", urpLit,
                new Color(1.0f, 0.78f, 0.10f), 0.55f, 0.85f, skyDetail, skyNormal, Vector2.one, false, Color.black);
            b.skylineWheelMat = CreateOrUpdateLitMaterial($"{MatVehicleDir}/Mat_SkylineWheel.mat", urpLit,
                new Color(0.75f, 0.77f, 0.80f), 0.8f, 0.75f, null, null, Vector2.one, false, Color.black);

            // Rival FollowWP car body materials
            b.rivalPaintOrangeMat = CreateOrUpdateLitMaterial($"{MatVehicleDir}/Mat_RivalPaint1_Orange.mat", urpLit,
                new Color(1.0f, 0.36f, 0.04f), 0.65f, 0.88f, null, null, Vector2.one, false, Color.black);
            b.rivalPaintYellowMat = CreateOrUpdateLitMaterial($"{MatVehicleDir}/Mat_RivalPaint2_Yellow.mat", urpLit,
                new Color(1.0f, 0.82f, 0.08f), 0.65f, 0.88f, null, null, Vector2.one, false, Color.black);

            // Road & Environment materials (wet dark 2005 street racing asphalt with reflections)
            b.roadAsphaltMat = CreateOrUpdateLitMaterial($"{MatRoadDir}/Mat_AsphaltNight.mat", urpLit,
                new Color(0.13f, 0.14f, 0.16f), 0.12f, 0.68f, concreteGround, concreteNormal, new Vector2(2f, 2f), false, Color.black);
            b.roadCurbMat = CreateOrUpdateLitMaterial($"{MatRoadDir}/Mat_RoadCurb.mat", urpLit,
                new Color(0.32f, 0.30f, 0.27f), 0.05f, 0.38f, concreteGround, null, new Vector2(2f, 2f), false, Color.black);
            b.roadLinesMat = CreateOrUpdateLitMaterial($"{MatRoadDir}/Mat_RoadMarkings.mat", urpLit,
                new Color(0.95f, 0.72f, 0.18f), 0.05f, 0.5f, null, null, Vector2.one, true, new Color(0.45f, 0.32f, 0.06f));
            b.barrierWallMat = CreateOrUpdateLitMaterial($"{MatRoadDir}/Mat_TrackBarrier.mat", urpLit,
                new Color(0.24f, 0.26f, 0.30f), 0.35f, 0.55f, concreteGround, concreteNormal, new Vector2(1f, 1f), false, Color.black);
            b.startCheckersMat = CreateOrUpdateLitMaterial($"{MatRoadDir}/Mat_StartCheckers.mat", urpLit,
                Color.white, 0.05f, 0.5f, checkersTex, null, new Vector2(4f, 1f), true, new Color(0.35f, 0.35f, 0.35f));

            b.warmNeonMat = CreateOrUpdateLitMaterial($"{MatEnvDir}/Mat_NeonWarmAmber.mat", urpLit,
                new Color(1f, 0.72f, 0.25f), 0f, 0.8f, null, null, Vector2.one, true, new Color(4.2f, 2.5f, 0.6f));
            b.cyanNeonMat = CreateOrUpdateLitMaterial($"{MatEnvDir}/Mat_NeonCoolCyan.mat", urpLit,
                new Color(0.2f, 0.85f, 1f), 0f, 0.8f, null, null, Vector2.one, true, new Color(0.5f, 2.8f, 4.2f));
            b.cityGroundMat = CreateOrUpdateLitMaterial($"{MatEnvDir}/Mat_CityGroundDark.mat", urpLit,
                new Color(0.07f, 0.08f, 0.10f), 0.05f, 0.35f, concreteGround, concreteNormal, new Vector2(80f, 80f), false, Color.black);
            b.kenneyCommercialMat = CreateOrUpdateLitMaterial($"{MatEnvDir}/Mat_KenneyBuildingsURP.mat", urpLit,
                new Color(0.75f, 0.78f, 0.85f), 0.15f, 0.55f, commercialMap, null, Vector2.one, true, new Color(0.18f, 0.14f, 0.08f));
            b.kenneyRoadsMat = CreateOrUpdateLitMaterial($"{MatEnvDir}/Mat_KenneyRoadPropsURP.mat", urpLit,
                new Color(0.85f, 0.85f, 0.88f), 0.25f, 0.55f, roadsMap, null, Vector2.one, false, Color.black);

            // Effects materials
            b.skidmarkMat = CreateOrUpdateParticleMaterial($"{MatFxDir}/Mat_SkidmarksURP.mat", urpParticles,
                new Color(0.05f, 0.05f, 0.05f, 0.85f), skidTex);
            b.smokeParticleMat = CreateOrUpdateParticleMaterial($"{MatFxDir}/Mat_TireSmokeURP.mat", urpParticles,
                new Color(0.75f, 0.78f, 0.82f, 0.38f), smokeTex);

            // Physics materials
            b.asphaltPhysMat = CreateOrUpdatePhysicsMaterial($"{SettingsDir}/PhysMat_Asphalt.physicMaterial", 0.8f, 0.8f, 0f, PhysicsMaterialCombine.Average);
            b.barrierZeroFrictionPhysMat = CreateOrUpdatePhysicsMaterial($"{SettingsDir}/PhysMat_BarrierSlide.physicMaterial", 0f, 0f, 0.05f, PhysicsMaterialCombine.Minimum);

            // Night Volume Profile (NFS Most Wanted / Carbon inspired amber-cyan contrast, moderate bloom, optional motion blur)
            b.nightVolumeProfile = CreateOrUpdateNightVolumeProfile($"{SettingsDir}/StreetRacing_NightVolumeProfile.asset");

            // Load racing font
            b.mainFont = AssetDatabase.LoadAssetAtPath<Font>("Assets/HDRPLab/SharedAssets/Textures/Font/Roboto-Bold.ttf");
            if (!b.mainFont)
                b.mainFont = AssetDatabase.LoadAssetAtPath<Font>("Assets/Realistic Car Controller Pro/Font/good times rg.ttf");
            if (!b.mainFont)
                b.mainFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            return b;
        }

        static Material CreateOrUpdateLitMaterial(string path, Shader shader, Color baseColor, float metallic, float smoothness,
            Texture2D baseMap, Texture2D normalMap, Vector2 tiling, bool emissive, Color emissionColor)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!mat)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            else
            {
                mat.shader = shader;
            }
            mat.SetColor("_BaseColor", baseColor);
            mat.SetFloat("_Metallic", metallic);
            mat.SetFloat("_Smoothness", smoothness);
            if (baseMap)
            {
                mat.SetTexture("_BaseMap", baseMap);
                mat.SetTextureScale("_BaseMap", tiling);
            }
            if (normalMap)
            {
                mat.EnableKeyword("_NORMALMAP");
                mat.SetTexture("_BumpMap", normalMap);
                mat.SetTextureScale("_BumpMap", tiling);
            }
            if (emissive)
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", emissionColor);
            }
            else
            {
                mat.DisableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", Color.black);
            }
            EditorUtility.SetDirty(mat);
            return mat;
        }

        static void SetMaterialTransparent(Material mat)
        {
            mat.SetFloat("_Surface", 1f); // Transparent
            mat.SetFloat("_Blend", 0f);   // Alpha
            mat.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.DisableKeyword("_ALPHATEST_ON");
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = (int)RenderQueue.Transparent;
            EditorUtility.SetDirty(mat);
        }

        static Material CreateOrUpdateParticleMaterial(string path, Shader shader, Color tint, Texture2D tex)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!mat)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            else mat.shader = shader;

            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", tint);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", tint);
            if (tex)
            {
                if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
                if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", tex);
            }
            SetMaterialTransparent(mat);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        static PhysicsMaterial CreateOrUpdatePhysicsMaterial(string path, float dynFric, float statFric, float bounciness, PhysicsMaterialCombine frictionCombine)
        {
            var pm = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(path);
            if (!pm)
            {
                pm = new PhysicsMaterial();
                AssetDatabase.CreateAsset(pm, path);
            }
            pm.dynamicFriction = dynFric;
            pm.staticFriction = statFric;
            pm.bounciness = bounciness;
            pm.frictionCombine = frictionCombine;
            pm.bounceCombine = PhysicsMaterialCombine.Minimum;
            EditorUtility.SetDirty(pm);
            return pm;
        }

        static VolumeProfile CreateOrUpdateNightVolumeProfile(string path)
        {
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if (!profile)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, path);
            }

            if (!profile.TryGet<Bloom>(out var bloom))
                bloom = profile.Add<Bloom>(true);
            bloom.threshold.Override(0.95f);
            bloom.intensity.Override(1.15f);
            bloom.scatter.Override(0.65f);

            if (!profile.TryGet<Tonemapping>(out var tone))
                tone = profile.Add<Tonemapping>(true);
            tone.mode.Override(TonemappingMode.ACES);

            if (!profile.TryGet<ColorAdjustments>(out var col))
                col = profile.Add<ColorAdjustments>(true);
            col.postExposure.Override(0.35f);
            col.contrast.Override(16f);
            col.saturation.Override(12f);
            col.colorFilter.Override(new Color(1.0f, 0.96f, 0.90f));

            if (!profile.TryGet<Vignette>(out var vig))
                vig = profile.Add<Vignette>(true);
            vig.intensity.Override(0.24f);
            vig.smoothness.Override(0.4f);

            if (!profile.TryGet<MotionBlur>(out var mb))
                mb = profile.Add<MotionBlur>(true);
            mb.quality.Override(MotionBlurQuality.Medium);
            mb.intensity.Override(0.18f);

            EditorUtility.SetDirty(profile);
            return profile;
        }

        static void UpgradeKenneyEnvPrefabs(BuiltAssets assets)
        {
            // Create reusable URP environment prefabs in Assets/Game/Prefabs/Environment/
            string[] buildingFbx =
            {
                "Assets/kenney_city-kit-commercial_2.1/Models/FBX format/building-a.fbx",
                "Assets/kenney_city-kit-commercial_2.1/Models/FBX format/building-b.fbx",
                "Assets/kenney_city-kit-commercial_2.1/Models/FBX format/building-d.fbx",
                "Assets/kenney_city-kit-commercial_2.1/Models/FBX format/building-g.fbx",
                "Assets/kenney_city-kit-commercial_2.1/Models/FBX format/building-j.fbx",
                "Assets/kenney_city-kit-commercial_2.1/Models/FBX format/building-m.fbx",
                "Assets/kenney_city-kit-commercial_2.1/Models/FBX format/building-skyscraper-a.fbx",
                "Assets/kenney_city-kit-commercial_2.1/Models/FBX format/building-skyscraper-c.fbx",
            };
            foreach (string p in buildingFbx)
                CreateUrpPropPrefab(p, $"{PrefabEnvDir}/Urban_{Path.GetFileNameWithoutExtension(p)}.prefab", assets.kenneyCommercialMat, true);

            CreateUrpPropPrefab("Assets/kenney_city-kit-roads/Models/FBX format/light-curved.fbx",
                $"{PrefabEnvDir}/Prop_StreetLight.prefab", assets.kenneyRoadsMat, false);
        }

        static GameObject CreateUrpPropPrefab(string fbxPath, string targetPrefabPath, Material overrideMat, bool addBoxCollider)
        {
            var src = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
            if (!src) return null;
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(src);
            inst.name = Path.GetFileNameWithoutExtension(targetPrefabPath);
            if (overrideMat)
            {
                foreach (var r in inst.GetComponentsInChildren<Renderer>(true))
                {
                    var mats = r.sharedMaterials;
                    for (int i = 0; i < mats.Length; i++) mats[i] = overrideMat;
                    r.sharedMaterials = mats;
                }
            }
            if (addBoxCollider && !inst.GetComponent<Collider>())
            {
                var rends = inst.GetComponentsInChildren<Renderer>(true);
                if (rends.Length > 0)
                {
                    Bounds b = rends[0].bounds;
                    for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
                    var bc = inst.AddComponent<BoxCollider>();
                    bc.center = inst.transform.InverseTransformPoint(b.center);
                    bc.size = b.size;
                }
            }
            var saved = PrefabUtility.SaveAsPrefabAsset(inst, targetPrefabPath);
            Object.DestroyImmediate(inst);
            return saved;
        }

        static GameObject BuildCoupeVehiclePrefab(BuiltAssets assets)
        {
            var coupeFbx = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/HDRPLab/Scenes/HDRPLabController/Models/LabCoupe_MD.fbx");
            if (!coupeFbx)
            {
                Debug.LogWarning("LabCoupe_MD.fbx not found, falling back to Skyline.");
                return BuildSkylineVehiclePrefab(assets);
            }

            var root = new GameObject("StreetRacer_Coupe");
            var rb = root.AddComponent<Rigidbody>();
            rb.mass = 1380f;
            rb.linearDamping = 0.02f;
            rb.angularDamping = 2.5f;

            var input = root.AddComponent<CarInput>();
            var controller = root.AddComponent<ArcadeCarController>();
            var paint = root.AddComponent<CarPaint>();
            var lights = root.AddComponent<CarLights>();
            lights.tailLightMaterial = assets.carTaillightMat;
            var audio = root.AddComponent<CarAudio>();
            var fx = root.AddComponent<CarEffects>();
            var tilt = root.AddComponent<CarBodyTilt>();

            // Body visuals container
            var bodyVisuals = new GameObject("BodyVisuals");
            bodyVisuals.transform.SetParent(root.transform, false);
            tilt.body = bodyVisuals.transform;

            var modelInst = (GameObject)PrefabUtility.InstantiatePrefab(coupeFbx, bodyVisuals.transform);
            PrefabUtility.UnpackPrefabInstance(modelInst, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            modelInst.name = "LabCoupe_Model";
            modelInst.transform.localPosition = Vector3.zero;
            modelInst.transform.localRotation = Quaternion.identity;
            modelInst.transform.localScale = Vector3.one;

            // Convert all renderers on the car to clean URP materials and register body paint & tail lights
            var allRenderers = modelInst.GetComponentsInChildren<Renderer>(true);
            foreach (var r in allRenderers)
            {
                if (r.name.Contains("Shadow"))
                {
                    r.gameObject.SetActive(false);
                    continue;
                }
                var mats = r.sharedMaterials;
                var paintIndices = new List<int>();
                bool hasTaillight = false;

                for (int i = 0; i < mats.Length; i++)
                {
                    string mName = mats[i] ? mats[i].name : "";
                    string rName = r.name;

                    if (mName.Contains("Pearl") || (rName.Contains("Body") && !rName.Contains("Under") && !rName.Contains("Window") && !rName.Contains("Chrome") && !rName.Contains("Glass") && !rName.Contains("Metal") && !rName.Contains("Grill") && !rName.Contains("Plate") && i == 0))
                    {
                        mats[i] = assets.carPaintMat;
                        paintIndices.Add(i);
                    }
                    else if (mName.Contains("Window") || mName.Contains("Glass") || rName.Contains("Glass") || rName.Contains("Window_Top") || rName.Contains("Door_Window"))
                    {
                        mats[i] = assets.carGlassMat;
                    }
                    else if (mName.Contains("Rubber") || rName.Contains("Tire") || rName.Contains("Rubber"))
                    {
                        mats[i] = assets.carRubberMat;
                    }
                    else if (mName.Contains("Carbon"))
                    {
                        mats[i] = assets.carCarbonMat;
                    }
                    else if (mName.Contains("HeadLight_F_Glow") || mName.Contains("HeadLights_F_Pixel"))
                    {
                        mats[i] = assets.carHeadlightMat;
                    }
                    else if (mName.Contains("R_Light") || mName.Contains("HeadLight_R_Glow") || mName.Contains("HeadLights_R_Pixel") || rName.Contains("Rear_Lights_04") || rName.Contains("Rear_Lights_Trunk_04"))
                    {
                        mats[i] = assets.carTaillightMat;
                        hasTaillight = true;
                    }
                    else if (mName.Contains("Aluminum") || rName.Contains("Wheels_"))
                    {
                        mats[i] = assets.carRimMat;
                    }
                    else if (mName.Contains("Chrome") || mName.Contains("Mirror"))
                    {
                        mats[i] = assets.carChromeMat;
                    }
                    else if (mName.Contains("Leather") || mName.Contains("Suede") || mName.Contains("Fabric") || rName.Contains("Lab_Int_"))
                    {
                        mats[i] = assets.carInteriorMat;
                    }
                    else
                    {
                        mats[i] = assets.carDarkMetalMat;
                    }
                }
                r.sharedMaterials = mats;

                if (paintIndices.Count > 0)
                {
                    paint.slots.Add(new CarPaint.PaintSlot
                    {
                        renderer = r,
                        materialIndices = paintIndices.ToArray()
                    });
                }
                if (hasTaillight)
                {
                    lights.tailLightRenderers.Add(r);
                }
            }

            // Setup 4 wheels (FL, FR, RL, RR) with exact bounds-derived centers and suspension anchors
            string[] corners = { "FL", "FR", "RL", "RR" };
            bool[] isFront = { true, true, false, false };
            var wheelsContainer = new GameObject("Wheels");
            wheelsContainer.transform.SetParent(root.transform, false);

            var wheelList = new List<ArcadeCarController.Wheel>();
            var smokeSystems = new List<ParticleSystem>();

            for (int c = 0; c < 4; c++)
            {
                string tag = corners[c];
                Transform tireTf = FindDeepChild(modelInst.transform, $"Lab_Tire_{tag}");
                Transform rim02 = FindDeepChild(modelInst.transform, $"Lab_A_Wheels_{tag}_02");
                Transform rim04 = FindDeepChild(modelInst.transform, $"Lab_A_Wheels_{tag}_04");
                Transform decal = FindDeepChild(modelInst.transform, $"Lab_A_Decal_{tag}");

                Vector3 wheelCenter = new Vector3(c % 2 == 0 ? -0.82f : 0.82f, 0.35f, c < 2 ? 1.38f : -1.36f);
                float wheelRadius = 0.35f;

                if (tireTf && tireTf.TryGetComponent<Renderer>(out var tireRen))
                {
                    Bounds b = tireRen.bounds;
                    wheelCenter = b.center;
                    wheelRadius = Mathf.Max(0.28f, b.extents.y);
                }

                var visualPivot = new GameObject($"WheelVisual_{tag}").transform;
                visualPivot.SetParent(wheelsContainer.transform, false);
                visualPivot.position = wheelCenter;
                visualPivot.rotation = Quaternion.identity;

                if (tireTf) tireTf.SetParent(visualPivot, true);
                if (rim02) rim02.SetParent(visualPivot, true);
                if (rim04) rim04.SetParent(visualPivot, true);
                if (decal) decal.SetParent(visualPivot, true);

                var anchor = new GameObject($"WheelAnchor_{tag}").transform;
                anchor.SetParent(wheelsContainer.transform, false);
                // Place suspension anchor above wheel center so at ~30% compression the wheel sits at its authored height
                anchor.position = wheelCenter + Vector3.up * (controller.suspensionLength * 0.72f);

                wheelList.Add(new ArcadeCarController.Wheel
                {
                    name = $"Wheel_{tag}",
                    anchor = anchor,
                    visual = visualPivot,
                    front = isFront[c],
                    radius = wheelRadius
                });

                var smokePs = CreateWheelSmokeParticleSystem(wheelsContainer.transform, $"WheelSmoke_{tag}", wheelCenter, assets.smokeParticleMat);
                smokeSystems.Add(smokePs);
            }

            controller.wheels = wheelList.ToArray();
            fx.wheelSmoke = smokeSystems.ToArray();

            // Chassis colliders (raised off the ground so wheels handle all ground contact and curbs don't snag)
            var collidersRoot = new GameObject("ChassisColliders");
            collidersRoot.transform.SetParent(root.transform, false);

            var mainBodyBox = collidersRoot.AddComponent<BoxCollider>();
            mainBodyBox.center = new Vector3(0f, 0.78f, 0.02f);
            mainBodyBox.size = new Vector3(1.82f, 0.82f, 4.45f);
            mainBodyBox.material = assets.barrierZeroFrictionPhysMat;

            // Headlights, Brake glow & Reverse light
            var lightsRoot = new GameObject("LightsRig");
            lightsRoot.transform.SetParent(root.transform, false);

            Light hlL = CreateSpotLight(lightsRoot.transform, "Headlight_L", new Vector3(-0.65f, 0.68f, 2.15f), new Vector3(5f, 0f, 0f), new Color(0.95f, 0.96f, 1f), 42f, 62f, 8f);
            Light hlR = CreateSpotLight(lightsRoot.transform, "Headlight_R", new Vector3( 0.65f, 0.68f, 2.15f), new Vector3(5f, 0f, 0f), new Color(0.95f, 0.96f, 1f), 42f, 62f, 8f);
            lights.headlights = new[] { hlL, hlR };

            var brakeLightGo = new GameObject("BrakeGlowLight");
            brakeLightGo.transform.SetParent(lightsRoot.transform, false);
            brakeLightGo.transform.localPosition = new Vector3(0f, 0.72f, -2.35f);
            var brakeLight = brakeLightGo.AddComponent<Light>();
            brakeLight.type = LightType.Point;
            brakeLight.color = new Color(1f, 0.08f, 0.02f);
            brakeLight.range = 6.5f;
            brakeLight.intensity = 1.2f;
            brakeLight.shadows = LightShadows.None;
            lights.brakeGlow = brakeLight;

            var revLightGo = new GameObject("ReverseLight");
            revLightGo.transform.SetParent(lightsRoot.transform, false);
            revLightGo.transform.localPosition = new Vector3(0f, 0.65f, -2.35f);
            var revLight = revLightGo.AddComponent<Light>();
            revLight.type = LightType.Point;
            revLight.color = new Color(1f, 0.98f, 0.9f);
            revLight.range = 5.5f;
            revLight.intensity = 2.0f;
            revLight.enabled = false;
            lights.reverseLight = revLight;

            // Wire audio clips from project assets
            WireCarAudioClips(audio);

            string prefabPath = $"{PrefabVehicleDir}/StreetRacer_Coupe.prefab";
            var saved = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            Object.DestroyImmediate(root);
            return saved;
        }

        static GameObject BuildSkylineVehiclePrefab(BuiltAssets assets)
        {
            var skyFbx = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Realistic Car Controller Pro/Models/Prototype Vehicle/Skyline (Prototype)/Model_Skyline by BUMSTRUM(3DMaesen) (Prototype).fbx");
            if (!skyFbx) return null;

            var root = new GameObject("StreetRacer_Skyline");
            var rb = root.AddComponent<Rigidbody>();
            rb.mass = 1380f;
            rb.linearDamping = 0.02f;
            rb.angularDamping = 2.5f;

            root.AddComponent<CarInput>();
            var controller = root.AddComponent<ArcadeCarController>();
            var paint = root.AddComponent<CarPaint>();
            var lights = root.AddComponent<CarLights>();
            var audio = root.AddComponent<CarAudio>();
            var fx = root.AddComponent<CarEffects>();
            var tilt = root.AddComponent<CarBodyTilt>();

            var bodyVisuals = new GameObject("BodyVisuals");
            bodyVisuals.transform.SetParent(root.transform, false);
            tilt.body = bodyVisuals.transform;

            var modelInst = (GameObject)PrefabUtility.InstantiatePrefab(skyFbx, bodyVisuals.transform);
            PrefabUtility.UnpackPrefabInstance(modelInst, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            modelInst.name = "Skyline_Model";
            modelInst.transform.localPosition = new Vector3(0f, 0.34f, 0f);

            foreach (var r in modelInst.GetComponentsInChildren<Renderer>(true))
            {
                if (r.name == "Body")
                {
                    r.sharedMaterial = assets.skylineBodyMat;
                    paint.slots.Add(new CarPaint.PaintSlot { renderer = r, materialIndices = new[] { 0 } });
                }
                else
                {
                    r.sharedMaterial = assets.skylineWheelMat;
                }
            }

            string[] wheelNames = { "Wheel_FL", "Wheel_FR", "Wheel_RL", "Wheel_RR" };
            bool[] isFront = { true, true, false, false };
            var wheelsContainer = new GameObject("Wheels");
            wheelsContainer.transform.SetParent(root.transform, false);

            var wheelList = new List<ArcadeCarController.Wheel>();
            var smokeSystems = new List<ParticleSystem>();

            for (int i = 0; i < 4; i++)
            {
                Transform wt = FindDeepChild(modelInst.transform, wheelNames[i]);
                Vector3 wPos = wt ? wt.position : new Vector3(i % 2 == 0 ? -0.72f : 0.72f, 0.34f, i < 2 ? 1.38f : -0.93f);
                var pivot = new GameObject($"WheelVisual_{wheelNames[i]}").transform;
                pivot.SetParent(wheelsContainer.transform, false);
                pivot.position = wPos;
                if (wt) wt.SetParent(pivot, true);

                var anchor = new GameObject($"WheelAnchor_{wheelNames[i]}").transform;
                anchor.SetParent(wheelsContainer.transform, false);
                anchor.position = wPos + Vector3.up * (controller.suspensionLength * 0.72f);

                wheelList.Add(new ArcadeCarController.Wheel
                {
                    name = wheelNames[i],
                    anchor = anchor,
                    visual = pivot,
                    front = isFront[i],
                    radius = 0.34f
                });

                smokeSystems.Add(CreateWheelSmokeParticleSystem(wheelsContainer.transform, $"Smoke_{wheelNames[i]}", wPos, assets.smokeParticleMat));
            }

            controller.wheels = wheelList.ToArray();
            fx.wheelSmoke = smokeSystems.ToArray();

            var box = root.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, 0.75f, 0.1f);
            box.size = new Vector3(1.65f, 0.78f, 4.1f);
            box.material = assets.barrierZeroFrictionPhysMat;

            var lightsRoot = new GameObject("LightsRig");
            lightsRoot.transform.SetParent(root.transform, false);
            Light hlL = CreateSpotLight(lightsRoot.transform, "Headlight_L", new Vector3(-0.6f, 0.65f, 2.0f), new Vector3(5f, 0f, 0f), Color.white, 42f, 60f, 8f);
            Light hlR = CreateSpotLight(lightsRoot.transform, "Headlight_R", new Vector3( 0.6f, 0.65f, 2.0f), new Vector3(5f, 0f, 0f), Color.white, 42f, 60f, 8f);
            lights.headlights = new[] { hlL, hlR };

            WireCarAudioClips(audio);

            string prefabPath = $"{PrefabVehicleDir}/StreetRacer_Skyline.prefab";
            var saved = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            Object.DestroyImmediate(root);
            return saved;
        }

        static ParticleSystem CreateWheelSmokeParticleSystem(Transform parent, string name, Vector3 pos, Material smokeMat)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.loop = true;
            main.playOnAwake = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.45f, 0.85f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.6f, 1.8f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.45f, 1.1f);
            main.startColor = new Color(0.78f, 0.80f, 0.85f, 0.32f);
            main.maxParticles = 250;

            var em = ps.emission;
            em.enabled = true;
            em.rateOverTimeMultiplier = 0f;

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.18f;

            var sizeOverLife = ps.sizeOverLifetime;
            sizeOverLife.enabled = true;
            sizeOverLife.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.5f, 1f, 2.1f));

            var psr = go.GetComponent<ParticleSystemRenderer>();
            if (smokeMat) psr.sharedMaterial = smokeMat;
            return ps;
        }

        static Light CreateSpotLight(Transform parent, string name, Vector3 localPos, Vector3 localEuler, Color color, float range, float spotAngle, float intensity)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.Euler(localEuler);
            var l = go.AddComponent<Light>();
            l.type = LightType.Spot;
            l.color = color;
            l.range = range;
            l.spotAngle = spotAngle;
            l.innerSpotAngle = spotAngle * 0.55f;
            l.intensity = intensity;
            l.shadows = LightShadows.None;
            return l;
        }

        static void WireCarAudioClips(CarAudio audio)
        {
            audio.engineIdle = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Realistic Car Controller Pro/Audio/Engine/Engine_Generic_Idle.wav");
            audio.engineLow  = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Realistic Car Controller Pro/Audio/Engine/Engine_Generic_Low.wav");
            audio.engineMid  = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Realistic Car Controller Pro/Audio/Engine/Engine_Generic_Med.wav");
            audio.engineHigh = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Realistic Car Controller Pro/Audio/Engine/Engine_Generic_High.wav");
            audio.skid       = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Realistic Car Controller Pro/Audio/Skid/Skid_Asphalt.wav");
            audio.wind       = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Realistic Car Controller Pro/Audio/Vehicle/Vehicle_Wind.wav");
            audio.impacts = new[]
            {
                AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Realistic Car Controller Pro/Audio/Impact/Impact2.wav"),
                AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Realistic Car Controller Pro/Audio/Impact/Impact3.wav"),
                AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Realistic Car Controller Pro/Audio/Impact/Impact4.wav"),
            };
        }

        static Transform FindDeepChild(Transform parent, string exactName)
        {
            foreach (Transform t in parent.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == exactName) return t;
            }
            return null;
        }

        // =====================================================================
        // MAIN MENU SCENE BUILDER
        // =====================================================================
        static void BuildMainMenuScene(string scenePath, BuiltAssets assets)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            ConfigureNightAtmosphere(assets.nightVolumeProfile);

            // Garage / Underpass night backdrop
            var envRoot = new GameObject("MenuBackdrop_Environment");
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "AsphaltFloor";
            ground.transform.SetParent(envRoot.transform, false);
            ground.transform.localScale = new Vector3(12f, 1f, 12f);
            ground.GetComponent<MeshRenderer>().sharedMaterial = assets.roadAsphaltMat;

            // Turntable platform
            var turntable = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            turntable.name = "CarTurntable";
            turntable.transform.position = new Vector3(1.85f, 0.06f, 0f);
            turntable.transform.localScale = new Vector3(5.6f, 0.06f, 5.6f);
            turntable.GetComponent<MeshRenderer>().sharedMaterial = assets.carDarkMetalMat;

            // Instantiate preview car on turntable (physics/input disabled for showcase)
            var previewCar = (GameObject)PrefabUtility.InstantiatePrefab(assets.coupeCarPrefab, turntable.transform);
            previewCar.name = "PreviewCar_Coupe";
            previewCar.transform.position = new Vector3(1.85f, 0.14f, 0f);
            previewCar.transform.rotation = Quaternion.Euler(0f, -145f, 0f);
            if (previewCar.TryGetComponent<Rigidbody>(out var rb)) rb.isKinematic = true;
            if (previewCar.TryGetComponent<ArcadeCarController>(out var cc)) cc.enabled = false;
            if (previewCar.TryGetComponent<CarInput>(out var ci)) ci.enabled = false;
            if (previewCar.TryGetComponent<CarAudio>(out var ca)) ca.enabled = false;

            // Studio / Street lights around the turntable
            CreatePointLight(envRoot.transform, "KeyLight_Warm", new Vector3(4.5f, 3.6f, -3.5f), new Color(1f, 0.72f, 0.35f), 18f, 8f);
            CreatePointLight(envRoot.transform, "RimLight_Cyan", new Vector3(-2.5f, 2.8f, 3.8f), new Color(0.2f, 0.82f, 1f), 18f, 7f);
            CreatePointLight(envRoot.transform, "TopFill_White", new Vector3(1.85f, 4.5f, 0f), new Color(0.85f, 0.9f, 1f), 14f, 4.5f);

            // Neon arch bars in background
            for (int i = 0; i < 4; i++)
            {
                var bar = GameObject.CreatePrimitive(PrimitiveType.Cube);
                bar.name = $"NeonArch_{i}";
                bar.transform.SetParent(envRoot.transform, false);
                bar.transform.position = new Vector3(2f + i * 2.5f, 2.6f, 6.5f + i * 2f);
                bar.transform.localScale = new Vector3(8f, 0.18f, 0.18f);
                bar.GetComponent<MeshRenderer>().sharedMaterial = (i % 2 == 0) ? assets.warmNeonMat : assets.cyanNeonMat;
            }

            // Camera
            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            camGo.transform.position = new Vector3(-0.35f, 1.55f, -5.6f);
            camGo.transform.rotation = Quaternion.Euler(9.5f, 14f, 0f);
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.02f, 0.03f, 0.05f);
            cam.fieldOfView = 44f;
            camGo.AddComponent<AudioListener>();
            var camData = camGo.AddComponent<UniversalAdditionalCameraData>();
            camData.renderPostProcessing = true;

            // EventSystem
            CreateEventSystem();

            // UI Canvas
            var canvasGo = CreateScreenCanvas("MainMenu_Canvas");
            var menuCtrl = canvasGo.AddComponent<MainMenuController>();
            menuCtrl.turntablePlatform = turntable.transform;
            menuCtrl.previewCarPaint = previewCar.GetComponent<CarPaint>();

            // Left panel container (NFS Most Wanted / Carbon style dark angled bar)
            var leftPanel = CreatePanel(canvasGo.transform, "LeftMenuPanel",
                new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f),
                new Vector2(40f, 0f), new Vector2(560f, -60f), new Color(0.04f, 0.05f, 0.07f, 0.88f));

            var accentBar = CreatePanel(leftPanel.transform, "AmberAccentStripe",
                new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f),
                Vector2.zero, new Vector2(8f, 0f), new Color(1f, 0.65f, 0.08f, 1f));

            CreateLabel(leftPanel.transform, "SubHeader", "UNDERGROUND STREET SERIES // 2005-2006 EDITION",
                assets.mainFont, 15, FontStyle.Bold, new Color(0.25f, 0.85f, 1f),
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(30f, -42f), new Vector2(-40f, 28f), TextAnchor.MiddleLeft);

            CreateLabel(leftPanel.transform, "GameTitle", "MOST CARBON\nNIGHTS '05",
                assets.mainFont, 44, FontStyle.BoldAndItalic, new Color(1f, 0.72f, 0.14f),
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(30f, -120f), new Vector2(-40f, 115f), TextAnchor.MiddleLeft);

            // Color Customization Section
            var colorLabel = CreateLabel(leftPanel.transform, "SelectedColorLabel", "COLOR DE CARROCERÍA: ROJO",
                assets.mainFont, 18, FontStyle.Bold, Color.white,
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(30f, -225f), new Vector2(-40f, 32f), TextAnchor.MiddleLeft);
            menuCtrl.selectedColorLabel = colorLabel;

            var previewSwatch = CreatePanel(leftPanel.transform, "CurrentColorIndicator",
                new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 0.5f),
                new Vector2(-30f, -225f), new Vector2(72f, 26f), GameSettings.PaintColor);
            menuCtrl.selectedColorSwatchPreview = previewSwatch.GetComponent<Image>();

            // Swatch grid (8 colors)
            var swatches = new Button[GameSettings.PaintColors.Length];
            for (int i = 0; i < GameSettings.PaintColors.Length; i++)
            {
                int row = i / 4;
                int col = i % 4;
                float x = 30f + col * 124f;
                float y = -280f - row * 56f;
                var cEntry = GameSettings.PaintColors[i];

                var btn = CreateStyledButton(leftPanel.transform, $"ColorBtn_{cEntry.name}", cEntry.name,
                    assets.mainFont, 13, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 0.5f),
                    new Vector2(x, y), new Vector2(114f, 44f),
                    Color.Lerp(cEntry.color, Color.black, 0.25f), Color.white);
                swatches[i] = btn;
            }
            menuCtrl.colorSwatchButtons = swatches;

            // Prev / Next color cycle buttons
            menuCtrl.prevColorButton = CreateStyledButton(leftPanel.transform, "PrevColorBtn", "< COLOR ANTERIOR",
                assets.mainFont, 14, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 0.5f),
                new Vector2(30f, -400f), new Vector2(240f, 40f),
                new Color(0.14f, 0.17f, 0.22f), new Color(0.75f, 0.88f, 1f));

            menuCtrl.nextColorButton = CreateStyledButton(leftPanel.transform, "NextColorBtn", "SIGUIENTE COLOR >",
                assets.mainFont, 14, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 0.5f),
                new Vector2(282f, -400f), new Vector2(240f, 40f),
                new Color(0.14f, 0.17f, 0.22f), new Color(0.75f, 0.88f, 1f));

            // Play & Quit buttons
            menuCtrl.playButton = CreateStyledButton(leftPanel.transform, "PlayRaceButton", "▶   INICIAR CARRERA CALLEJERA",
                assets.mainFont, 21, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 0.5f),
                new Vector2(30f, -490f), new Vector2(492f, 64f),
                new Color(0.92f, 0.52f, 0.04f), Color.white);

            menuCtrl.quitButton = CreateStyledButton(leftPanel.transform, "QuitGameButton", "SALIR DEL JUEGO",
                assets.mainFont, 16, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 0.5f),
                new Vector2(30f, -568f), new Vector2(492f, 46f),
                new Color(0.18f, 0.10f, 0.10f), new Color(0.9f, 0.75f, 0.75f));

            // Controls summary footer
            CreateLabel(leftPanel.transform, "ControlsSummary",
                "CONTROLES EN CARRERA:\n" +
                "• [W / ↑] Acelerar     • [S / ↓] Frenar / Marcha atrás\n" +
                "• [A / D o ← / →] Dirección progresiva\n" +
                "• [ESPACIO] Freno de mano (Derrape Arcade)\n" +
                "• [R] Recuperar coche al circuito     • [ESC] Pausa",
                assets.mainFont, 13, FontStyle.Normal, new Color(0.75f, 0.80f, 0.86f),
                new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(30f, 75f), new Vector2(-40f, 120f), TextAnchor.LowerLeft);

            EditorSceneManager.SaveScene(scene, scenePath);
        }

        // =====================================================================
        // RACE TRACK SCENE BUILDER
        // =====================================================================
        static void BuildRaceTrackScene(string scenePath, BuiltAssets assets)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Volume postVol = ConfigureNightAtmosphere(assets.nightVolumeProfile);

            // 1. Environment & City Floor
            var worldRoot = new GameObject("Environment_UrbanNight");
            var cityGround = GameObject.CreatePrimitive(PrimitiveType.Plane);
            cityGround.name = "CityGroundPlane";
            cityGround.transform.SetParent(worldRoot.transform, false);
            cityGround.transform.position = new Vector3(100f, -0.08f, 40f);
            cityGround.transform.localScale = new Vector3(95f, 1f, 95f);
            cityGround.GetComponent<MeshRenderer>().sharedMaterial = assets.cityGroundMat;
            cityGround.GetComponent<Collider>().sharedMaterial = assets.asphaltPhysMat;

            // 2. Editable Track System (TrackPath + TrackEnvironmentDecorator)
            var trackGo = new GameObject("RaceTrack_EditableSpline");
            var trackPath = trackGo.AddComponent<TrackPath>();
            trackPath.roadMaterial = assets.roadAsphaltMat;
            trackPath.curbMaterial = assets.roadCurbMat;
            trackPath.centerLineMaterial = assets.roadLinesMat;
            trackPath.barrierMaterial = assets.barrierWallMat;
            trackPath.roadPhysicsMaterial = assets.asphaltPhysMat;
            trackPath.barrierPhysicsMaterial = assets.barrierZeroFrictionPhysMat;
            trackPath.roadWidth = 14f;
            trackPath.sidewalkWidth = 2.5f;
            trackPath.curbHeight = 0.16f;
            trackPath.barrierHeight = 1.25f;
            trackPath.checkpointCount = 10;
            trackPath.waypointCount = 24;
            trackPath.waypointMarkerMaterial = assets.cyanNeonMat;

            var decorator = trackGo.AddComponent<TrackEnvironmentDecorator>();
            decorator.track = trackPath;
            decorator.streetLightModel = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabEnvDir}/Prop_StreetLight.prefab");
            decorator.overheadGantryModel = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/kenney_racing-kit/Models/FBX format/overheadLights.fbx");
            decorator.bannerGreenModel = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/kenney_racing-kit/Models/FBX format/bannerTowerGreen.fbx");
            decorator.bannerRedModel = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/kenney_racing-kit/Models/FBX format/bannerTowerRed.fbx");
            decorator.barrierRedModel = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/kenney_racing-kit/Models/FBX format/barrierRed.fbx");
            decorator.barrierWhiteModel = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/kenney_racing-kit/Models/FBX format/barrierWhite.fbx");
            decorator.billboardModel = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/kenney_racing-kit/Models/FBX format/billboard.fbx");
            decorator.warmNeonMaterial = assets.warmNeonMat;
            decorator.cyanNeonMaterial = assets.cyanNeonMat;
            decorator.startCheckersMaterial = assets.startCheckersMat;

            var buildingPrefabs = new List<GameObject>();
            foreach (string guid in AssetDatabase.FindAssets("Urban_building t:Prefab", new[] { PrefabEnvDir }))
            {
                var p = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
                if (p) buildingPrefabs.Add(p);
            }
            decorator.buildingModels = buildingPrefabs.ToArray();

            // Build geometry, checkpoints, waypoints, roadside streetlights & surrounding city buildings
            trackPath.RebuildTrack(true);
            decorator.RebuildRoadsideElements();
            decorator.RebuildCityBuildings();

            var wpMgr = trackPath.waypointsRoot ? trackPath.waypointsRoot.GetComponent<WaypointCircuitManager>() : null;

            // 3. Skidmark Manager
            var skidGo = new GameObject("SkidmarkManager");
            var skidMf = skidGo.AddComponent<MeshFilter>();
            var skidMr = skidGo.AddComponent<MeshRenderer>();
            skidMr.sharedMaterial = assets.skidmarkMat;
            skidMr.shadowCastingMode = ShadowCastingMode.Off;
            skidGo.AddComponent<SkidmarkManager>();

            // 4. Player Vehicle placed at SpawnPoint
            var carInst = (GameObject)PrefabUtility.InstantiatePrefab(assets.coupeCarPrefab);
            carInst.name = "PlayerCar_StreetRacer";
            if (trackPath.spawnPoint)
            {
                carInst.transform.SetPositionAndRotation(trackPath.spawnPoint.position, trackPath.spawnPoint.rotation);
            }
            var carController = carInst.GetComponent<ArcadeCarController>();

            // 4B. Two Rival Vehicles using FollowWP (Sesión 6: Waypoints guide logic)
            var rival1 = CreateRivalFollowWPCar(
                assets.coupeCarPrefab,
                "RivalCar_01_FollowWP",
                trackPath,
                wpMgr ? wpMgr.waypoints : null,
                startNormalizedU: 0.992f,
                laneOffset: -2.8f,
                speed: 33f,
                rotSpeed: 6.2f,
                accuracy: 4.5f,
                bodyPaintOverride: assets.rivalPaintOrangeMat);

            var rival2 = CreateRivalFollowWPCar(
                assets.skylineCarPrefab ? assets.skylineCarPrefab : assets.coupeCarPrefab,
                "RivalCar_02_FollowWP",
                trackPath,
                wpMgr ? wpMgr.waypoints : null,
                startNormalizedU: 0.996f,
                laneOffset: 2.8f,
                speed: 29f,
                rotSpeed: 5.5f,
                accuracy: 4.5f,
                bodyPaintOverride: assets.skylineCarPrefab ? assets.skylineBodyMat : assets.rivalPaintYellowMat);

            var rivalList = new List<FollowWP>();
            if (rival1) rivalList.Add(rival1);
            if (rival2) rivalList.Add(rival2);
            if (wpMgr) wpMgr.aiCars = rivalList.ToArray();

            // 5. Third-Person Race Camera
            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.02f, 0.03f, 0.06f);
            cam.nearClipPlane = 0.2f;
            cam.farClipPlane = 650f;
            camGo.AddComponent<AudioListener>();
            var camData = camGo.AddComponent<UniversalAdditionalCameraData>();
            camData.renderPostProcessing = true;

            var raceCam = camGo.AddComponent<RaceCamera>();
            raceCam.target = carController;
            raceCam.Snap();

            // 6. Race Manager
            var rmGo = new GameObject("RaceManager");
            var raceManager = rmGo.AddComponent<RaceManager>();
            raceManager.totalLaps = 3;
            raceManager.countdownSteps = 3;
            raceManager.countdownStepDuration = 1.0f;
            raceManager.playerCar = carController;
            raceManager.rivalCars = rivalList.ToArray();
            raceManager.raceCamera = raceCam;
            raceManager.trackPath = trackPath;
            raceManager.postProcessVolume = postVol;
            raceManager.checkpoints = new List<RaceCheckpoint>(
                trackPath.checkpointsRoot.GetComponentsInChildren<RaceCheckpoint>(false));
            raceManager.checkpoints.Sort((a, b) => a.index.CompareTo(b.index));

            // 7. EventSystem & Race UI (HUD + Waypoint Minimap + Pause + Finish)
            CreateEventSystem();
            BuildRaceUICanvas(raceManager, wpMgr, assets);

            EditorSceneManager.SaveScene(scene, scenePath);
        }

        static FollowWP CreateRivalFollowWPCar(
            GameObject basePrefab,
            string instanceName,
            TrackPath trackPath,
            GameObject[] waypoints,
            float startNormalizedU,
            float laneOffset,
            float speed,
            float rotSpeed,
            float accuracy,
            Material bodyPaintOverride)
        {
            if (!basePrefab) return null;
            var go = (GameObject)PrefabUtility.InstantiatePrefab(basePrefab);
            PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            go.name = instanceName;

            // Apply custom rival paint material to body slots before stripping player components
            if (go.TryGetComponent<CarPaint>(out var paint) && bodyPaintOverride)
            {
                foreach (var slot in paint.slots)
                {
                    if (!slot.renderer) continue;
                    var mats = slot.renderer.sharedMaterials;
                    foreach (int idx in slot.materialIndices)
                    {
                        if (idx >= 0 && idx < mats.Length)
                            mats[idx] = bodyPaintOverride;
                    }
                    slot.renderer.sharedMaterials = mats;
                }
            }

            // Collect visual wheel pivots for wheel rotation animation in FollowWP
            var wheelPivots = new List<Transform>();
            if (go.TryGetComponent<ArcadeCarController>(out var cc) && cc.wheels != null)
            {
                foreach (var w in cc.wheels)
                {
                    if (w != null && w.visual)
                        wheelPivots.Add(w.visual);
                }
            }

            // Remove player-only components so the rival uses strictly FollowWP movement
            if (go.TryGetComponent<CarInput>(out var ci)) Object.DestroyImmediate(ci);
            if (go.TryGetComponent<CarEffects>(out var fx)) Object.DestroyImmediate(fx);
            if (go.TryGetComponent<CarBodyTilt>(out var tilt)) Object.DestroyImmediate(tilt);
            if (go.TryGetComponent<CarAudio>(out var audio)) Object.DestroyImmediate(audio);
            if (go.TryGetComponent<CarLights>(out var lights)) Object.DestroyImmediate(lights);
            if (paint) Object.DestroyImmediate(paint);
            if (cc) Object.DestroyImmediate(cc);

            if (go.TryGetComponent<Rigidbody>(out var rb))
            {
                rb.isKinematic = true;
                rb.useGravity = false;
            }

            // Disable solid colliders on the kinematic rival car so transform.Translate doesn't launch the player
            foreach (var col in go.GetComponentsInChildren<Collider>(true))
            {
                col.enabled = false;
            }

            // Position on starting grid with lateral lane offset
            trackPath.EvaluateAtNormalized(startNormalizedU, out Vector3 roadPos, out Vector3 roadFwd, out Vector3 roadRight);
            Vector3 spawnPos = roadPos + roadRight * laneOffset + Vector3.up * 0.05f;
            Quaternion spawnRot = Quaternion.LookRotation(roadFwd, Vector3.up);
            go.transform.SetPositionAndRotation(spawnPos, spawnRot);

            var follow = go.AddComponent<FollowWP>();
            follow.waypoints = waypoints;
            follow.currentWP = 0;
            follow.speed = speed;
            follow.rotSpeed = rotSpeed;
            follow.accuracy = accuracy;
            follow.laneOffset = laneOffset;
            follow.wheelVisuals = wheelPivots.ToArray();
            follow.SaveStartPose();

            return follow;
        }

        static void BuildRaceUICanvas(RaceManager raceManager, WaypointCircuitManager wpMgr, BuiltAssets assets)
        {
            var canvasGo = CreateScreenCanvas("RaceUI_Canvas");
            var ui = canvasGo.AddComponent<RaceUIController>();
            ui.raceManager = raceManager;

            // ---- HUD ROOT ----
            var hudRoot = new GameObject("HUD_Root", typeof(RectTransform));
            hudRoot.transform.SetParent(canvasGo.transform, false);
            var hudRt = hudRoot.GetComponent<RectTransform>();
            hudRt.anchorMin = Vector2.zero;
            hudRt.anchorMax = Vector2.one;
            hudRt.offsetMin = Vector2.zero;
            hudRt.offsetMax = Vector2.zero;
            ui.hudRoot = hudRoot;

            // Top-Left Panel: Lap Counter & Timers
            var topLeft = CreatePanel(hudRoot.transform, "TopLeft_TimingPanel",
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(28f, -24f), new Vector2(430f, 148f), new Color(0.03f, 0.04f, 0.06f, 0.84f));

            CreatePanel(topLeft.transform, "AccentTop",
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
                Vector2.zero, new Vector2(0f, 5f), new Color(1f, 0.65f, 0.1f, 1f));

            ui.lapCounterText = CreateLabel(topLeft.transform, "LapCounterText", "VUELTA  1 / 3",
                assets.mainFont, 26, FontStyle.BoldAndItalic, new Color(1f, 0.74f, 0.16f),
                new Vector2(0f, 1f), new Vector2(0.6f, 1f), new Vector2(18f, -26f), new Vector2(0f, 36f), TextAnchor.MiddleLeft);

            ui.checkpointCounterText = CreateLabel(topLeft.transform, "SectorText", "SECTOR  0 / 10",
                assets.mainFont, 15, FontStyle.Bold, new Color(0.25f, 0.85f, 1f),
                new Vector2(0.55f, 1f), new Vector2(1f, 1f), new Vector2(0f, -26f), new Vector2(-18f, 36f), TextAnchor.MiddleRight);

            ui.currentLapTimeText = CreateLabel(topLeft.transform, "CurrentLapTimeText", "TIEMPO VUELTA:  00:00.000",
                assets.mainFont, 19, FontStyle.Bold, Color.white,
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(18f, -72f), new Vector2(-36f, 30f), TextAnchor.MiddleLeft);

            ui.bestLapTimeText = CreateLabel(topLeft.transform, "BestLapTimeText", "MEJOR VUELTA:   --:--.---",
                assets.mainFont, 17, FontStyle.Bold, new Color(0.45f, 0.95f, 0.58f),
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(18f, -112f), new Vector2(-36f, 28f), TextAnchor.MiddleLeft);

            // Top-Right Panel: 2D Waypoint Circuit Minimap (shows WP0..WP23 + Player + 2 FollowWP Rivals)
            var topRightMap = CreatePanel(hudRoot.transform, "TopRight_WaypointMapPanel",
                new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-28f, -24f), new Vector2(316f, 356f), new Color(0.03f, 0.04f, 0.06f, 0.86f));

            CreatePanel(topRightMap.transform, "CyanStripe",
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
                Vector2.zero, new Vector2(0f, 5f), new Color(0.2f, 0.85f, 1f, 1f));

            var wpStatusLabel = CreateLabel(topRightMap.transform, "WaypointStatusText", "MAPA WAYPOINTS (24)   R1→WP#0  |  R2→WP#0",
                assets.mainFont, 12, FontStyle.Bold, new Color(0.25f, 0.90f, 1f),
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -20f), new Vector2(-20f, 24f), TextAnchor.MiddleCenter);

            var mapAreaGo = new GameObject("MapArea", typeof(RectTransform), typeof(RawImage));
            mapAreaGo.transform.SetParent(topRightMap.transform, false);
            var mapAreaRt = mapAreaGo.GetComponent<RectTransform>();
            mapAreaRt.anchorMin = new Vector2(0.5f, 0.5f);
            mapAreaRt.anchorMax = new Vector2(0.5f, 0.5f);
            mapAreaRt.pivot = new Vector2(0.5f, 0.5f);
            mapAreaRt.anchoredPosition = new Vector2(0f, -4f);
            mapAreaRt.sizeDelta = new Vector2(280f, 280f);
            var mapRawImg = mapAreaGo.GetComponent<RawImage>();
            mapRawImg.color = Color.white;

            CreateLabel(topRightMap.transform, "MapLegendText",
                "● TÚ (Verde)    ● WP 0..23 (Cian)    ● R1/R2 FollowWP (Naranja/Amarillo)",
                assets.mainFont, 10, FontStyle.Bold, new Color(0.80f, 0.86f, 0.94f),
                new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 14f), new Vector2(-16f, 20f), TextAnchor.MiddleCenter);

            var minimap = topRightMap.AddComponent<WaypointMinimapHUD>();
            minimap.trackPath = raceManager.trackPath;
            minimap.waypointManager = wpMgr;
            minimap.playerCar = raceManager.playerCar;
            minimap.rivalCars = raceManager.rivalCars;
            minimap.mapArea = mapAreaRt;
            minimap.trackBackgroundImage = mapRawImg;
            minimap.waypointStatusText = wpStatusLabel;
            minimap.labelFont = assets.mainFont;
            minimap.InitializeMap();

            // Bottom-Right Panel: Speedometer & Tachometer
            var bottomRight = CreatePanel(hudRoot.transform, "BottomRight_SpeedometerPanel",
                new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f),
                new Vector2(-28f, 24f), new Vector2(390f, 165f), new Color(0.03f, 0.04f, 0.06f, 0.86f));

            CreatePanel(bottomRight.transform, "CyanStripe",
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
                Vector2.zero, new Vector2(0f, 5f), new Color(0.2f, 0.85f, 1f, 1f));

            ui.speedValueText = CreateLabel(bottomRight.transform, "SpeedValueText", "000",
                assets.mainFont, 62, FontStyle.BoldAndItalic, Color.white,
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(-18f, -46f), new Vector2(-30f, 72f), TextAnchor.MiddleRight);

            ui.gearText = CreateLabel(bottomRight.transform, "GearText", "MARCHA 1",
                assets.mainFont, 18, FontStyle.Bold, new Color(1f, 0.72f, 0.15f),
                new Vector2(0f, 1f), new Vector2(0.6f, 1f), new Vector2(18f, -35f), new Vector2(0f, 34f), TextAnchor.MiddleLeft);

            ui.speedMaxLabelText = CreateLabel(bottomRight.transform, "SpeedScaleText", "KM/H   |   ESCALA MAX 230 KM/H",
                assets.mainFont, 13, FontStyle.Bold, new Color(0.65f, 0.78f, 0.90f),
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(18f, -92f), new Vector2(-36f, 22f), TextAnchor.MiddleRight);

            // Speed Bar
            var speedBarBg = CreatePanel(bottomRight.transform, "SpeedBar_BG",
                new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 38f), new Vector2(-36f, 18f), new Color(0.14f, 0.16f, 0.20f, 1f));
            ui.speedBarFill = CreateFillBar(speedBarBg.transform, "SpeedBar_Fill", new Color(0.2f, 0.85f, 1f, 1f));

            // RPM Bar
            var rpmBarBg = CreatePanel(bottomRight.transform, "RpmBar_BG",
                new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 16f), new Vector2(-36f, 12f), new Color(0.14f, 0.16f, 0.20f, 1f));
            ui.rpmBarFill = CreateFillBar(rpmBarBg.transform, "RpmBar_Fill", new Color(1f, 0.62f, 0.10f, 1f));

            // Bottom-Left Controls Hint
            CreateLabel(hudRoot.transform, "ControlsHint",
                "[W/S] Acelerar/Frenar   [A/D] Dirección   [ESPACIO] Derrape   [R] Recuperar   [ESC] Pausa",
                assets.mainFont, 13, FontStyle.Bold, new Color(0.78f, 0.82f, 0.88f, 0.85f),
                new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(28f, 24f), new Vector2(720f, 28f), TextAnchor.MiddleLeft);

            // Center Countdown & Lap Banner
            ui.countdownCenterText = CreateLabel(hudRoot.transform, "CountdownCenterText", "3",
                assets.mainFont, 96, FontStyle.BoldAndItalic, new Color(1f, 0.76f, 0.15f),
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 40f), new Vector2(600f, 140f), TextAnchor.MiddleCenter);

            ui.lapBannerText = CreateLabel(hudRoot.transform, "LapCompletedBanner", "",
                assets.mainFont, 24, FontStyle.Bold, new Color(0.3f, 1f, 0.55f),
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -52f), new Vector2(760f, 44f), TextAnchor.MiddleCenter);

            // ---- PAUSE PANEL ----
            var pauseOverlay = CreatePanel(canvasGo.transform, "PauseMenu_Panel",
                Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
                Vector2.zero, Vector2.zero, new Color(0.01f, 0.02f, 0.04f, 0.82f));
            ui.pausePanel = pauseOverlay;

            var pauseBox = CreatePanel(pauseOverlay.transform, "PauseCard",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(480f, 360f), new Color(0.05f, 0.07f, 0.10f, 0.96f));

            CreateLabel(pauseBox.transform, "PauseTitle", "CARRERA EN PAUSA",
                assets.mainFont, 32, FontStyle.BoldAndItalic, new Color(1f, 0.72f, 0.15f),
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -48f), new Vector2(-40f, 50f), TextAnchor.MiddleCenter);

            ui.resumeButton = CreateStyledButton(pauseBox.transform, "ResumeButton", "REANUDAR CARRERA",
                assets.mainFont, 18, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, 36f), new Vector2(360f, 52f), new Color(0.90f, 0.52f, 0.05f), Color.white);

            ui.pauseRestartButton = CreateStyledButton(pauseBox.transform, "RestartButton", "REINICIAR CARRERA",
                assets.mainFont, 18, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, -32f), new Vector2(360f, 52f), new Color(0.15f, 0.22f, 0.30f), Color.white);

            ui.pauseMainMenuButton = CreateStyledButton(pauseBox.transform, "MainMenuButton", "VOLVER AL MENÚ PRINCIPAL",
                assets.mainFont, 18, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, -100f), new Vector2(360f, 52f), new Color(0.22f, 0.12f, 0.12f), Color.white);

            pauseOverlay.SetActive(false);

            // ---- FINISH PANEL ----
            var finishOverlay = CreatePanel(canvasGo.transform, "FinishScreen_Panel",
                Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
                Vector2.zero, Vector2.zero, new Color(0.01f, 0.02f, 0.04f, 0.86f));
            ui.finishPanel = finishOverlay;

            var finishBox = CreatePanel(finishOverlay.transform, "FinishCard",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(540f, 410f), new Color(0.05f, 0.07f, 0.10f, 0.96f));

            ui.finishTitleText = CreateLabel(finishBox.transform, "FinishTitleText", "¡CARRERA COMPLETADA!",
                assets.mainFont, 34, FontStyle.BoldAndItalic, new Color(0.25f, 1f, 0.5f),
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -52f), new Vector2(-40f, 52f), TextAnchor.MiddleCenter);

            ui.finishTotalTimeText = CreateLabel(finishBox.transform, "FinishTotalTimeText", "TIEMPO TOTAL:   00:00.000",
                assets.mainFont, 22, FontStyle.Bold, Color.white,
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -125f), new Vector2(-60f, 38f), TextAnchor.MiddleCenter);

            ui.finishBestLapText = CreateLabel(finishBox.transform, "FinishBestLapText", "MEJOR VUELTA:   00:00.000",
                assets.mainFont, 22, FontStyle.Bold, new Color(1f, 0.76f, 0.18f),
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -170f), new Vector2(-60f, 38f), TextAnchor.MiddleCenter);

            ui.playAgainButton = CreateStyledButton(finishBox.transform, "PlayAgainButton", "↻   VOLVER A JUGAR",
                assets.mainFont, 20, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 108f), new Vector2(380f, 56f), new Color(0.92f, 0.52f, 0.04f), Color.white);

            ui.finishMainMenuButton = CreateStyledButton(finishBox.transform, "FinishMenuButton", "MENÚ PRINCIPAL",
                assets.mainFont, 18, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 40f), new Vector2(380f, 48f), new Color(0.15f, 0.20f, 0.28f), Color.white);

            finishOverlay.SetActive(false);
        }

        // =====================================================================
        // LIGHTING & UI HELPERS
        // =====================================================================
        static Volume ConfigureNightAtmosphere(VolumeProfile profile)
        {
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.08f, 0.11f, 0.20f);
            RenderSettings.ambientEquatorColor = new Color(0.14f, 0.11f, 0.15f);
            RenderSettings.ambientGroundColor = new Color(0.04f, 0.04f, 0.06f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(0.03f, 0.04f, 0.07f);
            RenderSettings.fogDensity = 0.0028f;

            var moonGo = new GameObject("DirectionalLight_MoonAndCityGlow");
            moonGo.transform.rotation = Quaternion.Euler(42f, -35f, 0f);
            var moon = moonGo.AddComponent<Light>();
            moon.type = LightType.Directional;
            moon.color = new Color(0.55f, 0.68f, 0.92f);
            moon.intensity = 0.55f;
            moon.shadows = LightShadows.Soft;

            var volGo = new GameObject("GlobalVolume_NightPostProcess");
            var vol = volGo.AddComponent<Volume>();
            vol.isGlobal = true;
            vol.sharedProfile = profile;
            return vol;
        }

        static Light CreatePointLight(Transform parent, string name, Vector3 pos, Color color, float range, float intensity)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            var l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = color;
            l.range = range;
            l.intensity = intensity;
            l.shadows = LightShadows.None;
            return l;
        }

        static void CreateEventSystem()
        {
            var esGo = new GameObject("EventSystem");
            esGo.AddComponent<EventSystem>();
            esGo.AddComponent<InputSystemUIInputModule>();
        }

        static GameObject CreateScreenCanvas(string name)
        {
            var go = new GameObject(name);
            var c = go.AddComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();
            return go;
        }

        static GameObject CreatePanel(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
            Vector2 anchoredPos, Vector2 sizeDelta, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = sizeDelta;
            var img = go.GetComponent<Image>();
            img.color = color;
            return go;
        }

        static Image CreateFillBar(Transform parent, string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            // Create a 1x1 white sprite so Image.Type.Filled works reliably
            var img = go.GetComponent<Image>();
            img.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            img.type = Image.Type.Filled;
            img.fillMethod = Image.FillMethod.Horizontal;
            img.fillOrigin = (int)Image.OriginHorizontal.Left;
            img.fillAmount = 0.5f;
            img.color = color;
            return img;
        }

        static Text CreateLabel(Transform parent, string name, string content, Font font, int fontSize, FontStyle style, Color color,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPos, Vector2 sizeDelta, TextAnchor alignment)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = sizeDelta;

            var txt = go.GetComponent<Text>();
            txt.font = font;
            txt.text = content;
            txt.fontSize = fontSize;
            txt.fontStyle = style;
            txt.color = color;
            txt.alignment = alignment;
            txt.horizontalOverflow = HorizontalWrapMode.Overflow;
            txt.verticalOverflow = VerticalWrapMode.Overflow;
            return txt;
        }

        static Button CreateStyledButton(Transform parent, string name, string label, Font font, int fontSize,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPos, Vector2 sizeDelta, Color bgColor, Color textColor)
        {
            var go = CreatePanel(parent, name, anchorMin, anchorMax, pivot, anchoredPos, sizeDelta, bgColor);
            var btn = go.AddComponent<Button>();
            var colors = btn.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.15f, 1.15f, 1.15f, 1f);
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
            btn.colors = colors;

            CreateLabel(go.transform, "Label", label, font, fontSize, FontStyle.Bold, textColor,
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, TextAnchor.MiddleCenter);
            return btn;
        }

        static void UpdateEditorBuildSettings(string menuScenePath, string raceScenePath)
        {
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(menuScenePath, true),
                new EditorBuildSettingsScene(raceScenePath, true),
            };
        }
    }
}
