using System.Collections.Generic;
using UnityEngine;

namespace StreetRacing.Environment
{
    /// <summary>
    /// Populates and manages roadside streetlights, start/finish gantry, corner chevron barriers,
    /// and urban buildings around the TrackPath. All generated objects are placed in clean,
    /// editable Hierarchy containers so they can be moved, rotated, duplicated or deleted in Scene View.
    /// </summary>
    public class TrackEnvironmentDecorator : MonoBehaviour
    {
        public TrackPath track;

        [Header("Contenedores Editables en Escena")]
        public Transform startFinishContainer;
        public Transform streetLightsContainer;
        public Transform cornerPropsContainer;
        public Transform buildingsContainer;

        [Header("Prefabs / Modelos Reutilizados")]
        public GameObject streetLightModel;
        public GameObject overheadGantryModel;
        public GameObject bannerGreenModel;
        public GameObject bannerRedModel;
        public GameObject barrierRedModel;
        public GameObject barrierWhiteModel;
        public GameObject billboardModel;
        public GameObject[] buildingModels;

        [Header("Materiales de Iluminación Nocturna")]
        public Material warmNeonMaterial;
        public Material cyanNeonMaterial;
        public Material startCheckersMaterial;

        [Header("Parámetros de Distribución")]
        [Tooltip("Separación aproximada entre farolas a lo largo del circuito (metros).")]
        public float streetLightSpacing = 38f;
        [Tooltip("Activar luces puntuales reales cada N farolas para optimizar rendimiento en URP.")]
        public int realLightEveryN = 2;
        public Color warmStreetColor = new Color(1.0f, 0.68f, 0.28f);
        public Color coolAccentColor = new Color(0.25f, 0.78f, 1.0f);

        /// <summary>Rebuilds spline-attached elements (Start/Finish gate, streetlights, corner barriers).</summary>
        [ContextMenu("Rebuild Roadside Elements")]
        public void RebuildRoadsideElements()
        {
            if (!track) track = GetComponent<TrackPath>();
            if (!track) return;
            track.SampleSpline();
            if (track.sampledPoints.Count < 4) return;

            BuildStartFinishGate();
            BuildStreetLights();
            BuildCornerBarriers();
        }

        /// <summary>Rebuilds the surrounding city buildings outside the road corridor.</summary>
        [ContextMenu("Rebuild City Buildings")]
        public void RebuildCityBuildings()
        {
            if (!track) track = GetComponent<TrackPath>();
            if (!track) return;
            track.SampleSpline();
            if (track.sampledPoints.Count < 4) return;

            if (!buildingsContainer)
                buildingsContainer = GetOrCreateGroup("CityBuildings_Group");
            ClearChildren(buildingsContainer);

            if (buildingModels == null || buildingModels.Length == 0) return;

            float minRoadClearance = track.roadWidth * 0.5f + track.sidewalkWidth + 8.5f;
            int totalSamples = track.sampledPoints.Count;
            int step = Mathf.Max(3, totalSamples / 36);
            var placedPositions = new List<Vector3>();

            int bIndex = 0;
            for (int i = 0; i < totalSamples; i += step)
            {
                Vector3 p = track.sampledPoints[i];
                Vector3 fwd = track.sampledTangents[i];
                Vector3 right = track.sampledRights[i];

                for (int side = -1; side <= 1; side += 2)
                {
                    float lateralOffset = minRoadClearance + 6f + ((bIndex * 7) % 10);
                    Vector3 candidate = p + right * (side * lateralOffset);
                    candidate.y = 0f; // ground level

                    // Ensure building does not overlap any part of the road spline or another building
                    if (IsTooCloseToTrack(candidate, minRoadClearance + 2.5f)) continue;
                    if (IsTooCloseToList(candidate, placedPositions, 16f)) continue;

                    placedPositions.Add(candidate);
                    GameObject model = buildingModels[bIndex % buildingModels.Length];
                    if (model)
                    {
                        var inst = InstantiateChild(model, buildingsContainer, $"Building_{bIndex:00}_{model.name}");
                        inst.transform.position = candidate;
                        Vector3 lookDir = -right * side;
                        lookDir.y = 0f;
                        if (lookDir.sqrMagnitude > 0.001f)
                            inst.transform.rotation = Quaternion.LookRotation(lookDir.normalized, Vector3.up);
                        float scale = 12f + (bIndex % 4) * 2.5f;
                        inst.transform.localScale = new Vector3(scale, scale + (bIndex % 3) * 3f, scale);
                    }
                    bIndex++;
                }
            }
        }

        bool IsTooCloseToTrack(Vector3 worldPos, float minDistance)
        {
            float minSq = minDistance * minDistance;
            for (int i = 0; i < track.sampledPoints.Count; i++)
            {
                Vector3 sp = track.sampledPoints[i];
                float dx = worldPos.x - sp.x;
                float dz = worldPos.z - sp.z;
                if (dx * dx + dz * dz < minSq) return true;
            }
            return false;
        }

        static bool IsTooCloseToList(Vector3 pos, List<Vector3> list, float minDist)
        {
            float minSq = minDist * minDist;
            for (int i = 0; i < list.Count; i++)
            {
                if ((pos - list[i]).sqrMagnitude < minSq) return true;
            }
            return false;
        }

        void BuildStartFinishGate()
        {
            if (!startFinishContainer)
                startFinishContainer = GetOrCreateGroup("StartFinish_Area");
            ClearChildren(startFinishContainer);

            track.EvaluateAtNormalized(0f, out Vector3 pos, out Vector3 fwd, out Vector3 right);
            Quaternion rot = Quaternion.LookRotation(fwd, Vector3.up);

            // Checkered line decal quad across the road
            var lineGo = GameObject.CreatePrimitive(PrimitiveType.Quad);
            lineGo.name = "StartFinish_CheckeredLine";
            DestroyCollider(lineGo);
            lineGo.transform.SetParent(startFinishContainer, false);
            lineGo.transform.position = pos + Vector3.up * 0.035f;
            lineGo.transform.rotation = rot * Quaternion.Euler(90f, 0f, 0f);
            lineGo.transform.localScale = new Vector3(track.roadWidth, 2.2f, 1f);
            if (startCheckersMaterial)
                lineGo.GetComponent<MeshRenderer>().sharedMaterial = startCheckersMaterial;

            float halfSpan = track.roadWidth * 0.5f + 0.8f;

            // Left & right gantry pillars + overhead beam
            CreatePillarAndBanner(pos - right * halfSpan, rot, "Pillar_Left", bannerGreenModel);
            CreatePillarAndBanner(pos + right * halfSpan, rot, "Pillar_Right", bannerRedModel);

            var beam = GameObject.CreatePrimitive(PrimitiveType.Cube);
            beam.name = "StartFinish_OverheadBeam";
            DestroyCollider(beam);
            beam.transform.SetParent(startFinishContainer, false);
            beam.transform.position = pos + Vector3.up * 5.6f;
            beam.transform.rotation = rot;
            beam.transform.localScale = new Vector3(halfSpan * 2f + 1.2f, 0.75f, 0.85f);
            if (warmNeonMaterial) beam.GetComponent<MeshRenderer>().sharedMaterial = warmNeonMaterial;

            if (overheadGantryModel)
            {
                var oh = InstantiateChild(overheadGantryModel, startFinishContainer, "StartFinish_OverheadModel");
                oh.transform.position = pos + Vector3.up * 5.2f;
                oh.transform.rotation = rot;
                oh.transform.localScale = Vector3.one * 3.5f;
            }

            // Bright warm spotlight illuminating the start grid
            var gateLightGo = new GameObject("StartFinish_Spotlight");
            gateLightGo.transform.SetParent(startFinishContainer, false);
            gateLightGo.transform.position = pos + Vector3.up * 7.5f - fwd * 3f;
            gateLightGo.transform.rotation = Quaternion.LookRotation((pos - gateLightGo.transform.position).normalized, Vector3.up);
            var gateLight = gateLightGo.AddComponent<Light>();
            gateLight.type = LightType.Point;
            gateLight.color = new Color(1f, 0.78f, 0.45f);
            gateLight.range = 28f;
            gateLight.intensity = 6f;
            gateLight.shadows = LightShadows.None;
        }

        void CreatePillarAndBanner(Vector3 basePos, Quaternion rot, string name, GameObject bannerPrefab)
        {
            var pillar = GameObject.CreatePrimitive(PrimitiveType.Cube);
            pillar.name = name;
            DestroyCollider(pillar);
            pillar.transform.SetParent(startFinishContainer, false);
            pillar.transform.position = basePos + Vector3.up * 2.8f;
            pillar.transform.rotation = rot;
            pillar.transform.localScale = new Vector3(0.55f, 5.6f, 0.55f);
            if (cyanNeonMaterial) pillar.GetComponent<MeshRenderer>().sharedMaterial = cyanNeonMaterial;

            if (bannerPrefab)
            {
                var b = InstantiateChild(bannerPrefab, startFinishContainer, name + "_Banner");
                b.transform.position = basePos;
                b.transform.rotation = rot;
                b.transform.localScale = Vector3.one * 2.4f;
            }
        }

        void BuildStreetLights()
        {
            if (!streetLightsContainer)
                streetLightsContainer = GetOrCreateGroup("StreetLights_Group");
            ClearChildren(streetLightsContainer);

            float totalLen = Mathf.Max(50f, track.totalLength);
            int count = Mathf.Clamp(Mathf.RoundToInt(totalLen / Mathf.Max(15f, streetLightSpacing)), 8, 60);
            float lateralOffset = track.roadWidth * 0.5f + (track.generateSidewalks ? track.sidewalkWidth * 0.7f : 0.9f);

            for (int i = 0; i < count; i++)
            {
                float u = (i + 0.5f) / count;
                track.EvaluateAtNormalized(u, out Vector3 pos, out Vector3 fwd, out Vector3 right);
                int side = (i % 2 == 0) ? 1 : -1;
                Vector3 polePos = pos + right * (side * lateralOffset);
                Quaternion lookInward = Quaternion.LookRotation(-right * side, Vector3.up);

                var poleRoot = new GameObject($"StreetLight_{i:00}");
                poleRoot.transform.SetParent(streetLightsContainer, false);
                poleRoot.transform.position = polePos;
                poleRoot.transform.rotation = lookInward;

                if (streetLightModel)
                {
                    var m = InstantiateChild(streetLightModel, poleRoot.transform, "PoleMesh");
                    m.transform.localPosition = Vector3.zero;
                    m.transform.localRotation = Quaternion.identity;
                    m.transform.localScale = Vector3.one * 9f;
                }
                else
                {
                    var cyl = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    DestroyCollider(cyl);
                    cyl.transform.SetParent(poleRoot.transform, false);
                    cyl.transform.localPosition = new Vector3(0f, 3.5f, 0f);
                    cyl.transform.localScale = new Vector3(0.22f, 3.5f, 0.22f);
                }

                // Glowing bulb head (always visible for Bloom at night)
                bool isCool = (i % 5 == 0);
                var bulb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                bulb.name = "LampGlow";
                DestroyCollider(bulb);
                bulb.transform.SetParent(poleRoot.transform, false);
                bulb.transform.localPosition = new Vector3(0f, 6.6f, 1.4f);
                bulb.transform.localScale = new Vector3(0.65f, 0.28f, 0.95f);
                var mr = bulb.GetComponent<MeshRenderer>();
                if (isCool && cyanNeonMaterial) mr.sharedMaterial = cyanNeonMaterial;
                else if (warmNeonMaterial) mr.sharedMaterial = warmNeonMaterial;

                // Real point light on alternating poles for smooth URP performance
                if (realLightEveryN > 0 && (i % realLightEveryN == 0))
                {
                    var lgo = new GameObject("PointLight");
                    lgo.transform.SetParent(poleRoot.transform, false);
                    lgo.transform.localPosition = new Vector3(0f, 6.2f, 1.8f);
                    var light = lgo.AddComponent<Light>();
                    light.type = LightType.Point;
                    light.color = isCool ? coolAccentColor : warmStreetColor;
                    light.range = 24f;
                    light.intensity = isCool ? 3.8f : 4.5f;
                    light.shadows = LightShadows.None;
                }
            }
        }

        void BuildCornerBarriers()
        {
            if (!cornerPropsContainer)
                cornerPropsContainer = GetOrCreateGroup("CornerBarriers_Group");
            ClearChildren(cornerPropsContainer);

            int n = track.sampledPoints.Count;
            float outerOffset = track.roadWidth * 0.5f + (track.generateSidewalks ? track.sidewalkWidth * 0.35f : 0.25f);
            int propIdx = 0;

            for (int i = 0; i < n; i += 2)
            {
                Vector3 t0 = track.sampledTangents[i];
                Vector3 t1 = track.sampledTangents[(i + 3) % n];
                float signedAngle = Vector3.SignedAngle(t0, t1, Vector3.up);

                // Place high-visibility red/white racing barriers on sharp corners
                if (Mathf.Abs(signedAngle) > 11f)
                {
                    float outsideSign = signedAngle > 0f ? -1f : 1f;
                    Vector3 p = track.sampledPoints[i] + track.sampledRights[i] * (outsideSign * outerOffset);
                    Quaternion rot = Quaternion.LookRotation(t0, Vector3.up);
                    GameObject prefab = (propIdx % 2 == 0) ? barrierRedModel : barrierWhiteModel;
                    if (prefab)
                    {
                        var b = InstantiateChild(prefab, cornerPropsContainer, $"CornerBarrier_{propIdx:00}");
                        b.transform.position = p + Vector3.up * track.curbHeight;
                        b.transform.rotation = rot;
                        b.transform.localScale = Vector3.one * 2.2f;
                        DestroyAllColliders(b); // Main TrackBarriers mesh handles smooth physics collisions
                    }
                    propIdx++;
                }
            }

            // Place a few roadside billboards on long straights / wide curves
            if (billboardModel)
            {
                float[] billboardPositions = { 0.14f, 0.38f, 0.64f, 0.86f };
                for (int i = 0; i < billboardPositions.Length; i++)
                {
                    track.EvaluateAtNormalized(billboardPositions[i], out Vector3 pos, out Vector3 fwd, out Vector3 right);
                    int side = (i % 2 == 0) ? 1 : -1;
                    var bb = InstantiateChild(billboardModel, cornerPropsContainer, $"Billboard_{i:00}");
                    bb.transform.position = pos + right * (side * (track.roadWidth * 0.5f + track.sidewalkWidth + 2.5f));
                    bb.transform.rotation = Quaternion.LookRotation(-fwd, Vector3.up);
                    bb.transform.localScale = Vector3.one * 3.5f;
                }
            }
        }

        Transform GetOrCreateGroup(string groupName)
        {
            Transform t = transform.Find(groupName);
            if (!t)
            {
                var go = new GameObject(groupName);
                go.transform.SetParent(transform, false);
                t = go.transform;
            }
            return t;
        }

        static GameObject InstantiateChild(GameObject source, Transform parent, string name)
        {
            GameObject inst;
#if UNITY_EDITOR
            if (!Application.isPlaying && UnityEditor.PrefabUtility.IsPartOfAnyPrefab(source))
                inst = (GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(source, parent);
            else
                inst = Instantiate(source, parent);
#else
            inst = Instantiate(source, parent);
#endif
            inst.name = name;
            return inst;
        }

        static void ClearChildren(Transform parent)
        {
            if (!parent) return;
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                var c = parent.GetChild(i).gameObject;
                if (Application.isPlaying) Destroy(c);
                else DestroyImmediate(c);
            }
        }

        static void DestroyCollider(GameObject go)
        {
            var col = go.GetComponent<Collider>();
            if (!col) return;
            if (Application.isPlaying) Destroy(col);
            else DestroyImmediate(col);
        }

        static void DestroyAllColliders(GameObject root)
        {
            foreach (var col in root.GetComponentsInChildren<Collider>(true))
            {
                if (Application.isPlaying) Destroy(col);
                else DestroyImmediate(col);
            }
        }
    }
}
