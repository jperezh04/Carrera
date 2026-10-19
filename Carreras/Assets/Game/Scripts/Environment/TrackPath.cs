using System.Collections.Generic;
using UnityEngine;

namespace StreetRacing.Environment
{
    /// <summary>
    /// Editable closed spline circuit generator.
    /// Stores control points (with adjustable height Y), road width, barrier settings, and materials.
    /// Can be edited visually in the Scene View via TrackPathEditor and regenerated without duplicating geometry.
    /// </summary>
    [ExecuteAlways]
    public class TrackPath : MonoBehaviour
    {
        [Header("Trazado (Puntos de Control)")]
        [Tooltip("Puntos de control del circuito cerrado en espacio local. Edítalos en la Scene View o en el Inspector.")]
        public List<Vector3> controlPoints = new List<Vector3>()
        {
            new Vector3(   0f, 0.0f,    0f), // 0 - Recta principal (Salida / Meta)
            new Vector3(   0f, 0.0f,  110f), // 1 - Final de recta principal
            new Vector3(  35f, 0.0f,  175f), // 2 - Entrada curva amplia norte
            new Vector3( 110f, 1.5f,  200f), // 3 - Paso elevado industrial
            new Vector3( 195f, 2.0f,  175f), // 4 - Bajada rápida este
            new Vector3( 230f, 0.0f,  100f), // 5 - Frenada hacia chicane técnica
            new Vector3( 175f, 0.0f,   45f), // 6 - Horquilla / sección técnica interior
            new Vector3( 215f, 0.0f,  -25f), // 7 - Salida sección técnica
            new Vector3( 185f, 0.0f, -110f), // 8 - Curva sur hacia almacenes
            new Vector3(  95f, 0.0f, -135f), // 9 - Recta trasera sur
            new Vector3(  20f, 0.0f,  -95f), // 10 - Última curva hacia meta
        };

        [Header("Dimensiones de Carretera")]
        [Range(8f, 28f)]
        [Tooltip("Ancho total de la calzada asfaltada en metros.")]
        public float roadWidth = 14f;

        [Range(1f, 6f)]
        [Tooltip("Ancho de los bordillos / aceras laterales en metros.")]
        public float sidewalkWidth = 2.5f;

        [Range(0.05f, 0.4f)]
        [Tooltip("Altura del bordillo sobre el asfalto.")]
        public float curbHeight = 0.16f;

        [Range(0.6f, 2.5f)]
        [Tooltip("Altura de los muros/barreras de contención del circuito.")]
        public float barrierHeight = 1.25f;

        [Range(6, 32)]
        [Tooltip("Subdivisiones por tramo entre puntos de control.")]
        public int subdivisionsPerSegment = 16;

        [Tooltip("Escala de repetición UV a lo largo de la carretera.")]
        public float uvTilingLength = 8f;

        [Header("Materiales y Físicas")]
        public Material roadMaterial;
        public Material curbMaterial;
        public Material centerLineMaterial;
        public Material barrierMaterial;
        public PhysicsMaterial roadPhysicsMaterial;
        public PhysicsMaterial barrierPhysicsMaterial;

        [Header("Generación de Elementos")]
        public bool generateSidewalks = true;
        public bool generateCenterLine = true;
        public bool generateBarriers = true;
        [Range(4, 24)]
        public int checkpointCount = 10;
        [Range(8, 48)]
        [Tooltip("Número de Waypoints generados alrededor del circuito para los vehículos con FollowWP.")]
        public int waypointCount = 24;
        public Material waypointMarkerMaterial;

        [Header("Referencias Generadas (Editables en Jerarquía)")]
        public Transform generatedGeometryRoot;
        public Transform checkpointsRoot;
        public Transform waypointsRoot;
        public Transform spawnPoint;

        // Cached sampled ring in world space
        [HideInInspector] public List<Vector3> sampledPoints = new List<Vector3>();
        [HideInInspector] public List<Vector3> sampledTangents = new List<Vector3>();
        [HideInInspector] public List<Vector3> sampledRights = new List<Vector3>();
        [HideInInspector] public List<float> sampledDistances = new List<float>();
        [HideInInspector] public float totalLength;

        void OnEnable()
        {
            if (sampledPoints == null || sampledPoints.Count == 0)
                SampleSpline();
        }

        /// <summary>Evaluates Catmull-Rom closed spline and caches world-space points, tangents and cumulative distances.</summary>
        public void SampleSpline()
        {
            sampledPoints.Clear();
            sampledTangents.Clear();
            sampledRights.Clear();
            sampledDistances.Clear();
            totalLength = 0f;

            int n = controlPoints != null ? controlPoints.Count : 0;
            if (n < 3) return;

            int steps = Mathf.Max(4, subdivisionsPerSegment);
            Vector3 prevWorld = Vector3.zero;

            for (int i = 0; i < n; i++)
            {
                Vector3 p0 = controlPoints[(i - 1 + n) % n];
                Vector3 p1 = controlPoints[i];
                Vector3 p2 = controlPoints[(i + 1) % n];
                Vector3 p3 = controlPoints[(i + 2) % n];

                for (int s = 0; s < steps; s++)
                {
                    float t = s / (float)steps;
                    Vector3 localPos = CatmullRom(p0, p1, p2, p3, t);
                    Vector3 localTan = CatmullRomDerivative(p0, p1, p2, p3, t).normalized;
                    if (localTan.sqrMagnitude < 0.0001f)
                        localTan = (p2 - p1).normalized;

                    Vector3 worldPos = transform.TransformPoint(localPos);
                    Vector3 worldTan = transform.TransformDirection(localTan).normalized;
                    Vector3 worldRight = Vector3.Cross(Vector3.up, worldTan).normalized;

                    if (sampledPoints.Count > 0)
                        totalLength += Vector3.Distance(prevWorld, worldPos);

                    sampledPoints.Add(worldPos);
                    sampledTangents.Add(worldTan);
                    sampledRights.Add(worldRight);
                    sampledDistances.Add(totalLength);
                    prevWorld = worldPos;
                }
            }

            if (sampledPoints.Count > 1)
                totalLength += Vector3.Distance(sampledPoints[sampledPoints.Count - 1], sampledPoints[0]);
        }

        public static Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            float t2 = t * t;
            float t3 = t2 * t;
            return 0.5f * (
                (2f * p1) +
                (-p0 + p2) * t +
                (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 +
                (-p0 + 3f * p1 - 3f * p2 + p3) * t3
            );
        }

        public static Vector3 CatmullRomDerivative(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            float t2 = t * t;
            return 0.5f * (
                (-p0 + p2) +
                2f * (2f * p0 - 5f * p1 + 4f * p2 - p3) * t +
                3f * (-p0 + 3f * p1 - 3f * p2 + p3) * t2
            );
        }

        /// <summary>Returns a point, forward direction and right vector at normalized circuit progress [0..1].</summary>
        public void EvaluateAtNormalized(float u, out Vector3 position, out Vector3 forward, out Vector3 right)
        {
            if (sampledPoints == null || sampledPoints.Count < 2) SampleSpline();
            int count = sampledPoints.Count;
            if (count == 0)
            {
                position = transform.position;
                forward = transform.forward;
                right = transform.right;
                return;
            }
            u = Mathf.Repeat(u, 1f);
            float fIndex = u * count;
            int i0 = Mathf.FloorToInt(fIndex) % count;
            int i1 = (i0 + 1) % count;
            float frac = fIndex - Mathf.FloorToInt(fIndex);
            position = Vector3.Lerp(sampledPoints[i0], sampledPoints[i1], frac);
            forward = Vector3.Slerp(sampledTangents[i0], sampledTangents[i1], frac).normalized;
            right = Vector3.Slerp(sampledRights[i0], sampledRights[i1], frac).normalized;
        }

        /// <summary>Finds the closest sampled point on the circuit to a world position (used for safe vehicle reset).</summary>
        public int GetClosestSampleIndex(Vector3 worldPos)
        {
            if (sampledPoints == null || sampledPoints.Count == 0) SampleSpline();
            int best = 0;
            float bestDistSq = float.MaxValue;
            for (int i = 0; i < sampledPoints.Count; i++)
            {
                float d = (sampledPoints[i] - worldPos).sqrMagnitude;
                if (d < bestDistSq) { bestDistSq = d; best = i; }
            }
            return best;
        }

        /// <summary>
        /// Regenerates road surface, sidewalks/curbs, center markings, physics barriers and checkpoints.
        /// Safely replaces previous generated meshes without creating duplicates.
        /// </summary>
        [ContextMenu("Regenerate Track Geometry")]
        public void RebuildTrack(bool rebuildCheckpoints = true)
        {
            SampleSpline();
            if (sampledPoints.Count < 4) return;

            if (!generatedGeometryRoot)
            {
                Transform existing = transform.Find("GeneratedRoad");
                if (existing) generatedGeometryRoot = existing;
                else
                {
                    var go = new GameObject("GeneratedRoad");
                    go.transform.SetParent(transform, false);
                    generatedGeometryRoot = go.transform;
                }
            }

            BuildRoadMesh();
            BuildCenterLineMesh();
            BuildSidewalkAndCurbMesh();
            BuildBarrierMesh();
            UpdateSpawnPoint();
            if (rebuildCheckpoints)
            {
                BuildCheckpoints();
                BuildWaypoints();
            }
        }

        Transform GetOrCreateChild(Transform parent, string childName)
        {
            Transform t = parent.Find(childName);
            if (!t)
            {
                var go = new GameObject(childName);
                go.transform.SetParent(parent, false);
                t = go.transform;
            }
            t.localPosition = Vector3.zero;
            t.localRotation = Quaternion.identity;
            t.localScale = Vector3.one;
            return t;
        }

        void BuildRoadMesh()
        {
            Transform roadTf = GetOrCreateChild(generatedGeometryRoot, "RoadSurface");
            int n = sampledPoints.Count;
            var verts = new Vector3[(n + 1) * 2];
            var uvs = new Vector2[(n + 1) * 2];
            var normals = new Vector3[(n + 1) * 2];
            var tris = new int[n * 6];

            float halfW = roadWidth * 0.5f;
            float dist = 0f;

            for (int i = 0; i <= n; i++)
            {
                int idx = i % n;
                if (i > 0)
                    dist += Vector3.Distance(sampledPoints[(i - 1) % n], sampledPoints[idx]);

                Vector3 p = transform.InverseTransformPoint(sampledPoints[idx]);
                Vector3 r = transform.InverseTransformDirection(sampledRights[idx]);
                verts[i * 2] = p - r * halfW;
                verts[i * 2 + 1] = p + r * halfW;
                float v = dist / Mathf.Max(1f, uvTilingLength);
                uvs[i * 2] = new Vector2(0f, v);
                uvs[i * 2 + 1] = new Vector2(1f, v);
                normals[i * 2] = Vector3.up;
                normals[i * 2 + 1] = Vector3.up;

                if (i < n)
                {
                    int b = i * 2;
                    int t = i * 6;
                    tris[t + 0] = b;
                    tris[t + 1] = b + 2;
                    tris[t + 2] = b + 1;
                    tris[t + 3] = b + 1;
                    tris[t + 4] = b + 2;
                    tris[t + 5] = b + 3;
                }
            }

            var mesh = new Mesh { name = "ProceduralRoad_Mesh" };
            mesh.vertices = verts;
            mesh.uv = uvs;
            mesh.normals = normals;
            mesh.triangles = tris;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            ApplyMesh(roadTf.gameObject, mesh, roadMaterial, true, roadPhysicsMaterial);
        }

        void BuildCenterLineMesh()
        {
            Transform lineTf = GetOrCreateChild(generatedGeometryRoot, "RoadMarkings");
            lineTf.gameObject.SetActive(generateCenterLine);
            if (!generateCenterLine) return;

            int n = sampledPoints.Count;
            var verts = new List<Vector3>(n * 6);
            var uvs = new List<Vector2>(n * 6);
            var tris = new List<int>(n * 18);

            float lineHalfW = 0.14f;
            float edgeInset = roadWidth * 0.5f - 0.35f;
            float yOffset = 0.02f;

            for (int i = 0; i < n; i++)
            {
                int next = (i + 1) % n;
                Vector3 p0 = transform.InverseTransformPoint(sampledPoints[i]) + Vector3.up * yOffset;
                Vector3 p1 = transform.InverseTransformPoint(sampledPoints[next]) + Vector3.up * yOffset;
                Vector3 r0 = transform.InverseTransformDirection(sampledRights[i]);
                Vector3 r1 = transform.InverseTransformDirection(sampledRights[next]);

                // Dashed center line (every other segment)
                if (i % 2 == 0)
                    AddQuadStripSegment(verts, uvs, tris, p0, p1, r0, r1, 0f, lineHalfW);

                // Continuous edge lines (left & right)
                AddQuadStripSegment(verts, uvs, tris, p0, p1, r0, r1, -edgeInset, lineHalfW * 0.8f);
                AddQuadStripSegment(verts, uvs, tris, p0, p1, r0, r1, edgeInset, lineHalfW * 0.8f);
            }

            var mesh = new Mesh { name = "ProceduralMarkings_Mesh" };
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            ApplyMesh(lineTf.gameObject, mesh, centerLineMaterial ? centerLineMaterial : roadMaterial, false, null);
        }

        static void AddQuadStripSegment(List<Vector3> verts, List<Vector2> uvs, List<int> tris,
            Vector3 p0, Vector3 p1, Vector3 r0, Vector3 r1, float centerOffset, float halfWidth)
        {
            int b = verts.Count;
            verts.Add(p0 + r0 * (centerOffset - halfWidth));
            verts.Add(p0 + r0 * (centerOffset + halfWidth));
            verts.Add(p1 + r1 * (centerOffset - halfWidth));
            verts.Add(p1 + r1 * (centerOffset + halfWidth));
            uvs.Add(new Vector2(0f, 0f));
            uvs.Add(new Vector2(1f, 0f));
            uvs.Add(new Vector2(0f, 1f));
            uvs.Add(new Vector2(1f, 1f));
            tris.Add(b); tris.Add(b + 2); tris.Add(b + 1);
            tris.Add(b + 1); tris.Add(b + 2); tris.Add(b + 3);
        }

        void BuildSidewalkAndCurbMesh()
        {
            Transform curbTf = GetOrCreateChild(generatedGeometryRoot, "SidewalksAndCurbs");
            curbTf.gameObject.SetActive(generateSidewalks);
            if (!generateSidewalks) return;

            int n = sampledPoints.Count;
            var verts = new List<Vector3>(n * 16);
            var uvs = new List<Vector2>(n * 16);
            var tris = new List<int>(n * 24);

            float inner = roadWidth * 0.5f;
            float outer = inner + sidewalkWidth;
            float h = curbHeight;

            for (int i = 0; i < n; i++)
            {
                int next = (i + 1) % n;
                Vector3 p0 = transform.InverseTransformPoint(sampledPoints[i]);
                Vector3 p1 = transform.InverseTransformPoint(sampledPoints[next]);
                Vector3 r0 = transform.InverseTransformDirection(sampledRights[i]);
                Vector3 r1 = transform.InverseTransformDirection(sampledRights[next]);
                float v0 = (i % 2 == 0) ? 0f : 0.5f;
                float v1 = v0 + 0.5f;

                // Right curb face + top sidewalk
                AddQuad(verts, uvs, tris,
                    p0 + r0 * inner, p1 + r1 * inner,
                    p0 + r0 * inner + Vector3.up * h, p1 + r1 * inner + Vector3.up * h, v0, v1);
                AddQuad(verts, uvs, tris,
                    p0 + r0 * inner + Vector3.up * h, p1 + r1 * inner + Vector3.up * h,
                    p0 + r0 * outer + Vector3.up * h, p1 + r1 * outer + Vector3.up * h, v0, v1);

                // Left curb face + top sidewalk
                AddQuad(verts, uvs, tris,
                    p1 - r1 * inner, p0 - r0 * inner,
                    p1 - r1 * inner + Vector3.up * h, p0 - r0 * inner + Vector3.up * h, v0, v1);
                AddQuad(verts, uvs, tris,
                    p1 - r1 * inner + Vector3.up * h, p0 - r0 * inner + Vector3.up * h,
                    p1 - r1 * outer + Vector3.up * h, p0 - r0 * outer + Vector3.up * h, v0, v1);
            }

            var mesh = new Mesh { name = "ProceduralSidewalks_Mesh" };
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            ApplyMesh(curbTf.gameObject, mesh, curbMaterial ? curbMaterial : roadMaterial, true, roadPhysicsMaterial);
        }

        void BuildBarrierMesh()
        {
            Transform barrierTf = GetOrCreateChild(generatedGeometryRoot, "TrackBarriers");
            barrierTf.gameObject.SetActive(generateBarriers);
            if (!generateBarriers) return;

            int n = sampledPoints.Count;
            var verts = new List<Vector3>(n * 24);
            var uvs = new List<Vector2>(n * 24);
            var tris = new List<int>(n * 36);

            float wallInner = roadWidth * 0.5f + (generateSidewalks ? sidewalkWidth * 0.45f : 0.25f);
            float wallThickness = 0.45f;
            float wallOuter = wallInner + wallThickness;
            float baseY = generateSidewalks ? curbHeight : 0f;
            float topY = baseY + barrierHeight;

            for (int i = 0; i < n; i++)
            {
                int next = (i + 1) % n;
                Vector3 p0 = transform.InverseTransformPoint(sampledPoints[i]);
                Vector3 p1 = transform.InverseTransformPoint(sampledPoints[next]);
                Vector3 r0 = transform.InverseTransformDirection(sampledRights[i]);
                Vector3 r1 = transform.InverseTransformDirection(sampledRights[next]);
                float v0 = i * 0.25f;
                float v1 = (i + 1) * 0.25f;

                // Right barrier (inner face, top face, outer face)
                AddQuad(verts, uvs, tris,
                    p0 + r0 * wallInner + Vector3.up * baseY, p1 + r1 * wallInner + Vector3.up * baseY,
                    p0 + r0 * wallInner + Vector3.up * topY, p1 + r1 * wallInner + Vector3.up * topY, v0, v1);
                AddQuad(verts, uvs, tris,
                    p0 + r0 * wallInner + Vector3.up * topY, p1 + r1 * wallInner + Vector3.up * topY,
                    p0 + r0 * wallOuter + Vector3.up * topY, p1 + r1 * wallOuter + Vector3.up * topY, v0, v1);
                AddQuad(verts, uvs, tris,
                    p1 + r1 * wallOuter + Vector3.up * baseY, p0 + r0 * wallOuter + Vector3.up * baseY,
                    p1 + r1 * wallOuter + Vector3.up * topY, p0 + r0 * wallOuter + Vector3.up * topY, v0, v1);

                // Left barrier (inner face, top face, outer face)
                AddQuad(verts, uvs, tris,
                    p1 - r1 * wallInner + Vector3.up * baseY, p0 - r0 * wallInner + Vector3.up * baseY,
                    p1 - r1 * wallInner + Vector3.up * topY, p0 - r0 * wallInner + Vector3.up * topY, v0, v1);
                AddQuad(verts, uvs, tris,
                    p1 - r1 * wallInner + Vector3.up * topY, p0 - r0 * wallInner + Vector3.up * topY,
                    p1 - r1 * wallOuter + Vector3.up * topY, p0 - r0 * wallOuter + Vector3.up * topY, v0, v1);
                AddQuad(verts, uvs, tris,
                    p0 - r0 * wallOuter + Vector3.up * baseY, p1 - r1 * wallOuter + Vector3.up * baseY,
                    p0 - r0 * wallOuter + Vector3.up * topY, p1 - r1 * wallOuter + Vector3.up * topY, v0, v1);
            }

            var mesh = new Mesh { name = "ProceduralBarriers_Mesh" };
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            ApplyMesh(barrierTf.gameObject, mesh, barrierMaterial ? barrierMaterial : curbMaterial, true, barrierPhysicsMaterial);
        }

        static void AddQuad(List<Vector3> verts, List<Vector2> uvs, List<int> tris,
            Vector3 bl, Vector3 br, Vector3 tl, Vector3 tr, float u0, float u1)
        {
            int b = verts.Count;
            verts.Add(bl); verts.Add(br); verts.Add(tl); verts.Add(tr);
            uvs.Add(new Vector2(u0, 0f)); uvs.Add(new Vector2(u1, 0f));
            uvs.Add(new Vector2(u0, 1f)); uvs.Add(new Vector2(u1, 1f));
            tris.Add(b); tris.Add(b + 2); tris.Add(b + 1);
            tris.Add(b + 1); tris.Add(b + 2); tris.Add(b + 3);
        }

        static void ApplyMesh(GameObject go, Mesh mesh, Material mat, bool withCollider, PhysicsMaterial physMat)
        {
            var mf = go.GetComponent<MeshFilter>();
            if (!mf) mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = mesh;

            var mr = go.GetComponent<MeshRenderer>();
            if (!mr) mr = go.AddComponent<MeshRenderer>();
            if (mat) mr.sharedMaterial = mat;

            if (withCollider)
            {
                var mc = go.GetComponent<MeshCollider>();
                if (!mc) mc = go.AddComponent<MeshCollider>();
                mc.sharedMesh = null;
                mc.sharedMesh = mesh;
                if (physMat) mc.sharedMaterial = physMat;
            }
        }

        void UpdateSpawnPoint()
        {
            if (!spawnPoint)
            {
                Transform existing = transform.Find("SpawnPoint");
                if (existing) spawnPoint = existing;
                else
                {
                    var go = new GameObject("SpawnPoint");
                    go.transform.SetParent(transform, false);
                    spawnPoint = go.transform;
                }
            }
            // Place spawn slightly behind the start/finish line (u = 0.985) so the player starts right behind the gate
            EvaluateAtNormalized(0.985f, out Vector3 pos, out Vector3 fwd, out _);
            spawnPoint.position = pos + Vector3.up * 0.15f;
            spawnPoint.rotation = Quaternion.LookRotation(fwd, Vector3.up);
        }

        public void BuildCheckpoints()
        {
            if (!checkpointsRoot)
            {
                Transform existing = transform.Find("Checkpoints");
                if (existing) checkpointsRoot = existing;
                else
                {
                    var go = new GameObject("Checkpoints");
                    go.transform.SetParent(transform, false);
                    checkpointsRoot = go.transform;
                }
            }

            // Clear old children safely
            for (int i = checkpointsRoot.childCount - 1; i >= 0; i--)
            {
                var child = checkpointsRoot.GetChild(i).gameObject;
                if (Application.isPlaying) Destroy(child);
                else DestroyImmediate(child);
            }

            int count = Mathf.Max(4, checkpointCount);
            float triggerWidth = roadWidth + sidewalkWidth * 2f + 2f;

            for (int i = 0; i < count; i++)
            {
                float u = i / (float)count;
                EvaluateAtNormalized(u, out Vector3 pos, out Vector3 fwd, out _);
                string cpName = i == 0 ? "Checkpoint_00_StartFinish" : $"Checkpoint_{i:00}";
                var go = new GameObject(cpName);
                go.transform.SetParent(checkpointsRoot, true);
                go.transform.position = pos + Vector3.up * 2f;
                go.transform.rotation = Quaternion.LookRotation(fwd, Vector3.up);

                var box = go.AddComponent<BoxCollider>();
                box.isTrigger = true;
                box.size = new Vector3(triggerWidth, 5f, 2.5f);

                var cp = go.AddComponent<Race.RaceCheckpoint>();
                cp.index = i;
                cp.isStartFinish = (i == 0);
            }
        }

        public void BuildWaypoints()
        {
            if (!waypointsRoot)
            {
                Transform existing = transform.Find("Waypoints_Circuit");
                if (existing) waypointsRoot = existing;
                else
                {
                    var go = new GameObject("Waypoints_Circuit");
                    go.transform.SetParent(transform, false);
                    waypointsRoot = go.transform;
                }
            }

            var wpMgr = waypointsRoot.GetComponent<Race.WaypointCircuitManager>();
            if (!wpMgr) wpMgr = waypointsRoot.gameObject.AddComponent<Race.WaypointCircuitManager>();

            for (int i = waypointsRoot.childCount - 1; i >= 0; i--)
            {
                var child = waypointsRoot.GetChild(i).gameObject;
                if (Application.isPlaying) Destroy(child);
                else DestroyImmediate(child);
            }

            int count = Mathf.Max(8, waypointCount);
            var wpArray = new GameObject[count];
            Material markerMat = waypointMarkerMaterial ? waypointMarkerMaterial : centerLineMaterial;

            for (int i = 0; i < count; i++)
            {
                // Place WP_00 just ahead of the start line so cars immediately head forward
                float u = ((i + 0.5f) / count) % 1f;
                EvaluateAtNormalized(u, out Vector3 pos, out Vector3 fwd, out _);

                var wpGo = new GameObject($"Waypoint_{i:00}");
                wpGo.transform.SetParent(waypointsRoot, true);
                wpGo.transform.position = pos;
                wpGo.transform.rotation = Quaternion.LookRotation(fwd, Vector3.up);

                // Visible non-colliding ground ring/pad on the road surface
                var pad = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                pad.name = $"WP_{i:00}_RoadPad";
                var padCol = pad.GetComponent<Collider>();
                if (padCol) { if (Application.isPlaying) Destroy(padCol); else DestroyImmediate(padCol); }
                pad.transform.SetParent(wpGo.transform, false);
                pad.transform.localPosition = new Vector3(0f, 0.03f, 0f);
                pad.transform.localScale = new Vector3(1.5f, 0.02f, 1.5f);
                if (markerMat) pad.GetComponent<MeshRenderer>().sharedMaterial = markerMat;

                // Floating glowing beacon diamond above the waypoint
                var beacon = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                beacon.name = $"WP_{i:00}_Beacon";
                var bCol = beacon.GetComponent<Collider>();
                if (bCol) { if (Application.isPlaying) Destroy(bCol); else DestroyImmediate(bCol); }
                beacon.transform.SetParent(wpGo.transform, false);
                beacon.transform.localPosition = new Vector3(0f, 3.6f, 0f);
                beacon.transform.localScale = new Vector3(0.65f, 0.95f, 0.65f);
                if (markerMat) beacon.GetComponent<MeshRenderer>().sharedMaterial = markerMat;

                wpArray[i] = wpGo;
            }

            wpMgr.waypoints = wpArray;

            // Update any FollowWP AI cars in the scene so their waypoints[] array stays connected
            var aiCars = FindObjectsByType<FollowWP>(FindObjectsSortMode.None);
            foreach (var ai in aiCars)
            {
                if (ai) ai.waypoints = wpArray;
            }
            wpMgr.aiCars = aiCars;
        }

        void OnDrawGizmos()
        {
            if (controlPoints == null || controlPoints.Count < 3) return;
            if (sampledPoints == null || sampledPoints.Count == 0) SampleSpline();

            Gizmos.color = new Color(1f, 0.55f, 0.05f, 0.85f);
            for (int i = 0; i < sampledPoints.Count; i++)
            {
                Vector3 a = sampledPoints[i];
                Vector3 b = sampledPoints[(i + 1) % sampledPoints.Count];
                Gizmos.DrawLine(a + Vector3.up * 0.25f, b + Vector3.up * 0.25f);
            }

            Gizmos.color = Color.cyan;
            for (int i = 0; i < controlPoints.Count; i++)
            {
                Vector3 wp = transform.TransformPoint(controlPoints[i]);
                Gizmos.DrawSphere(wp + Vector3.up * 0.35f, 0.85f);
            }
        }
    }
}
