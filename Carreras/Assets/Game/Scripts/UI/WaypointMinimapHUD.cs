using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using StreetRacing.Environment;
using StreetRacing.Race;
using StreetRacing.Vehicle;

namespace StreetRacing.UI
{
    /// <summary>
    /// Renders an interactive 2D Minimap in the HUD showing:
    /// - The complete road loop
    /// - All circuit Waypoints (WP 0..N) with numbered markers
    /// - The Player vehicle position
    /// - The 2 FollowWP AI cars and which waypoint each one is currently targeting
    /// </summary>
    public class WaypointMinimapHUD : MonoBehaviour
    {
        [Header("Referencias del Circuito y Vehículos")]
        public TrackPath trackPath;
        public WaypointCircuitManager waypointManager;
        public ArcadeCarController playerCar;
        public FollowWP[] rivalCars;

        [Header("Elementos UI del Mapa")]
        public RectTransform mapArea;
        public RawImage trackBackgroundImage;
        public Text waypointStatusText;
        public Font labelFont;

        [Header("Colores del Mapa")]
        public Color waypointDotColor = new Color(0.2f, 0.9f, 1f, 0.95f);
        public Color waypointStartColor = new Color(0.25f, 1f, 0.45f, 1f);
        public Color playerDotColor = new Color(0.2f, 1f, 0.4f, 1f);
        public Color[] rivalColors =
        {
            new Color(1.0f, 0.45f, 0.15f, 1f), // Rival 1 (Naranja/Rojo)
            new Color(1.0f, 0.90f, 0.15f, 1f)  // Rival 2 (Amarillo)
        };

        readonly List<RectTransform> wpDots = new List<RectTransform>();
        readonly List<Image> wpDotImages = new List<Image>();
        RectTransform playerDot;
        readonly List<RectTransform> rivalDots = new List<RectTransform>();
        Texture2D mapTexture;

        float minX, maxX, minZ, maxZ;
        bool initialized;

        void Start()
        {
            InitializeMap();
        }

        public void InitializeMap()
        {
            if (!trackPath) trackPath = FindFirstObjectByType<TrackPath>();
            if (!waypointManager) waypointManager = FindFirstObjectByType<WaypointCircuitManager>();
            if (!playerCar) playerCar = FindFirstObjectByType<ArcadeCarController>();
            if ((rivalCars == null || rivalCars.Length == 0) && waypointManager)
                rivalCars = waypointManager.aiCars;
            if (rivalCars == null || rivalCars.Length == 0)
                rivalCars = FindObjectsByType<FollowWP>(FindObjectsSortMode.None);

            if (!mapArea || !trackPath) return;
            trackPath.SampleSpline();
            ComputeWorldBounds();
            RenderTrackTexture();
            CreateWaypointAndCarMarkers();
            initialized = true;
        }

        void ComputeWorldBounds()
        {
            minX = float.MaxValue; maxX = float.MinValue;
            minZ = float.MaxValue; maxZ = float.MinValue;

            foreach (Vector3 p in trackPath.sampledPoints)
            {
                if (p.x < minX) minX = p.x;
                if (p.x > maxX) maxX = p.x;
                if (p.z < minZ) minZ = p.z;
                if (p.z > maxZ) maxZ = p.z;
            }
            float padX = Mathf.Max(20f, (maxX - minX) * 0.12f);
            float padZ = Mathf.Max(20f, (maxZ - minZ) * 0.12f);
            minX -= padX; maxX += padX;
            minZ -= padZ; maxZ += padZ;
        }

        Vector2 WorldToNormalizedMap(Vector3 worldPos)
        {
            float nx = Mathf.InverseLerp(minX, maxX, worldPos.x);
            float ny = Mathf.InverseLerp(minZ, maxZ, worldPos.z);
            return new Vector2(nx, ny);
        }

        Vector2 WorldToMapAnchoredPos(Vector3 worldPos)
        {
            Vector2 norm = WorldToNormalizedMap(worldPos);
            Rect r = mapArea.rect;
            float w = r.width > 10f ? r.width : (mapArea.sizeDelta.x > 10f ? mapArea.sizeDelta.x : 260f);
            float h = r.height > 10f ? r.height : (mapArea.sizeDelta.y > 10f ? mapArea.sizeDelta.y : 260f);
            return new Vector2((norm.x - 0.5f) * w, (norm.y - 0.5f) * h);
        }

        void RenderTrackTexture()
        {
            if (!trackBackgroundImage) return;
            int res = 256;
            if (mapTexture)
            {
                if (Application.isPlaying) Destroy(mapTexture);
                else DestroyImmediate(mapTexture);
            }
            mapTexture = new Texture2D(res, res, TextureFormat.RGBA32, false)
            {
                name = "MinimapTrack_Tex",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };

            var pixels = new Color32[res * res];
            var bg = new Color32(10, 14, 22, 220);
            var grid = new Color32(22, 32, 46, 230);
            for (int y = 0; y < res; y++)
            {
                for (int x = 0; x < res; x++)
                {
                    bool isGrid = (x % 32 == 0) || (y % 32 == 0);
                    pixels[y * res + x] = isGrid ? grid : bg;
                }
            }

            // Draw road spline loop onto texture
            int count = trackPath.sampledPoints.Count;
            Color32 roadOuter = new Color32(55, 70, 92, 255);
            Color32 roadInner = new Color32(120, 138, 160, 255);

            for (int i = 0; i < count; i++)
            {
                Vector3 a = trackPath.sampledPoints[i];
                Vector3 b = trackPath.sampledPoints[(i + 1) % count];
                for (int s = 0; s < 10; s++)
                {
                    Vector3 p = Vector3.Lerp(a, b, s / 10f);
                    Vector2 uv = WorldToNormalizedMap(p);
                    int px = Mathf.RoundToInt(uv.x * (res - 1));
                    int py = Mathf.RoundToInt(uv.y * (res - 1));
                    StampDisc(pixels, res, px, py, 5, roadOuter);
                    StampDisc(pixels, res, px, py, 3, roadInner);
                }
            }

            mapTexture.SetPixels32(pixels);
            mapTexture.Apply();
            trackBackgroundImage.texture = mapTexture;
        }

        static void StampDisc(Color32[] pixels, int res, int cx, int cy, int radius, Color32 col)
        {
            int r2 = radius * radius;
            for (int dy = -radius; dy <= radius; dy++)
            {
                int y = cy + dy;
                if (y < 0 || y >= res) continue;
                for (int dx = -radius; dx <= radius; dx++)
                {
                    int x = cx + dx;
                    if (x < 0 || x >= res) continue;
                    if (dx * dx + dy * dy <= r2)
                        pixels[y * res + x] = col;
                }
            }
        }

        void CreateWaypointAndCarMarkers()
        {
            wpDots.Clear();
            wpDotImages.Clear();
            rivalDots.Clear();
            playerDot = null;

            for (int i = mapArea.childCount - 1; i >= 0; i--)
            {
                var child = mapArea.GetChild(i).gameObject;
                if (Application.isPlaying) Destroy(child);
                else DestroyImmediate(child);
            }

            // Create waypoint markers
            if (waypointManager && waypointManager.waypoints != null)
            {
                for (int i = 0; i < waypointManager.waypoints.Length; i++)
                {
                    var wp = waypointManager.waypoints[i];
                    if (!wp) continue;

                    var dotGo = new GameObject($"MapWP_{i:00}", typeof(RectTransform), typeof(Image));
                    dotGo.transform.SetParent(mapArea, false);
                    var rt = dotGo.GetComponent<RectTransform>();
                    rt.sizeDelta = (i == 0) ? new Vector2(11f, 11f) : new Vector2(7.5f, 7.5f);
                    rt.anchoredPosition = WorldToMapAnchoredPos(wp.transform.position);

                    var img = dotGo.GetComponent<Image>();
                    img.color = (i == 0) ? waypointStartColor : waypointDotColor;

                    // Number label on every 2nd waypoint so the map stays clean and readable
                    if (i % 2 == 0 && labelFont)
                    {
                        var lblGo = new GameObject("WPIndex", typeof(RectTransform), typeof(Text));
                        lblGo.transform.SetParent(rt, false);
                        var lrt = lblGo.GetComponent<RectTransform>();
                        lrt.anchoredPosition = new Vector2(9f, 6f);
                        lrt.sizeDelta = new Vector2(40f, 16f);
                        var txt = lblGo.GetComponent<Text>();
                        txt.font = labelFont;
                        txt.fontSize = 10;
                        txt.fontStyle = FontStyle.Bold;
                        txt.color = new Color(0.82f, 0.95f, 1f, 0.92f);
                        txt.alignment = TextAnchor.MiddleLeft;
                        txt.text = $"WP{i}";
                    }

                    wpDots.Add(rt);
                    wpDotImages.Add(img);
                }
            }

            // Create rival markers
            if (rivalCars != null)
            {
                for (int i = 0; i < rivalCars.Length; i++)
                {
                    Color rc = rivalColors != null && rivalColors.Length > 0 ? rivalColors[i % rivalColors.Length] : Color.yellow;
                    var rDot = CreateVehicleDot($"RivalMapMarker_{i + 1}", new Vector2(12f, 12f), rc, $"R{i + 1}");
                    rivalDots.Add(rDot);
                }
            }

            // Create player marker on top
            playerDot = CreateVehicleDot("PlayerMapMarker", new Vector2(14f, 14f), playerDotColor, "TÚ");
        }

        RectTransform CreateVehicleDot(string name, Vector2 size, Color color, string tagText)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(mapArea, false);
            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = size;
            var img = go.GetComponent<Image>();
            img.color = color;

            if (labelFont && !string.IsNullOrEmpty(tagText))
            {
                var lblGo = new GameObject("Tag", typeof(RectTransform), typeof(Text));
                lblGo.transform.SetParent(rt, false);
                var lrt = lblGo.GetComponent<RectTransform>();
                lrt.anchoredPosition = new Vector2(0f, 11f);
                lrt.sizeDelta = new Vector2(44f, 16f);
                var txt = lblGo.GetComponent<Text>();
                txt.font = labelFont;
                txt.fontSize = 10;
                txt.fontStyle = FontStyle.Bold;
                txt.color = color;
                txt.alignment = TextAnchor.MiddleCenter;
                txt.text = tagText;
            }
            return rt;
        }

        void Update()
        {
            if (!initialized) return;

            // Update waypoint positions & highlight the waypoints currently targeted by rival cars
            int targetWp0 = (rivalCars != null && rivalCars.Length > 0 && rivalCars[0]) ? rivalCars[0].currentWP : -1;
            int targetWp1 = (rivalCars != null && rivalCars.Length > 1 && rivalCars[1]) ? rivalCars[1].currentWP : -1;

            if (waypointManager && waypointManager.waypoints != null)
            {
                int count = Mathf.Min(wpDots.Count, waypointManager.waypoints.Length);
                for (int i = 0; i < count; i++)
                {
                    var wp = waypointManager.waypoints[i];
                    if (!wp || !wpDots[i]) continue;
                    wpDots[i].anchoredPosition = WorldToMapAnchoredPos(wp.transform.position);

                    if (i == targetWp0)
                        wpDotImages[i].color = rivalColors[0];
                    else if (i == targetWp1)
                        wpDotImages[i].color = rivalColors[1 % rivalColors.Length];
                    else
                        wpDotImages[i].color = (i == 0) ? waypointStartColor : waypointDotColor;
                }
            }

            if (playerCar && playerDot)
                playerDot.anchoredPosition = WorldToMapAnchoredPos(playerCar.transform.position);

            if (rivalCars != null)
            {
                for (int i = 0; i < rivalCars.Length && i < rivalDots.Count; i++)
                {
                    if (rivalCars[i] && rivalDots[i])
                        rivalDots[i].anchoredPosition = WorldToMapAnchoredPos(rivalCars[i].transform.position);
                }
            }

            if (waypointStatusText && waypointManager && waypointManager.waypoints != null)
            {
                int wpTotal = waypointManager.waypoints.Length;
                string r1 = targetWp0 >= 0 ? $"R1→WP#{targetWp0}" : "";
                string r2 = targetWp1 >= 0 ? $"  |  R2→WP#{targetWp1}" : "";
                waypointStatusText.text = $"MAPA WAYPOINTS ({wpTotal})   {r1}{r2}";
            }
        }

        void OnDestroy()
        {
            if (mapTexture) Destroy(mapTexture);
        }
    }
}
