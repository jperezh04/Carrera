using UnityEngine;

namespace StreetRacing.Vehicle
{
    /// <summary>
    /// Single dynamic mesh that stores tyre marks in a ring buffer. No allocations while driving.
    /// One instance per scene (created automatically by CarEffects if missing).
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class SkidmarkManager : MonoBehaviour
    {
        public int maxSegments = 1500;
        public float markWidth = 0.24f;
        public float groundOffset = 0.025f;

        Vector3[] verts; Vector2[] uvs; Color32[] cols; int[] tris;
        Mesh mesh;
        int next;
        bool dirty;

        public static SkidmarkManager Instance { get; private set; }

        void Awake()
        {
            Instance = this;
            verts = new Vector3[maxSegments * 4];
            uvs = new Vector2[maxSegments * 4];
            cols = new Color32[maxSegments * 4];
            tris = new int[maxSegments * 6];
            for (int i = 0; i < maxSegments; i++)
            {
                int v = i * 4, t = i * 6;
                tris[t] = v; tris[t + 1] = v + 2; tris[t + 2] = v + 1;
                tris[t + 3] = v + 2; tris[t + 4] = v + 3; tris[t + 5] = v + 1;
                uvs[v] = new Vector2(0, 0); uvs[v + 1] = new Vector2(1, 0); uvs[v + 2] = new Vector2(0, 1); uvs[v + 3] = new Vector2(1, 1);
            }
            mesh = new Mesh { name = "Skidmarks" };
            mesh.MarkDynamic();
            mesh.vertices = verts; mesh.uv = uvs; mesh.colors32 = cols; mesh.triangles = tris;
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 10000f);
            GetComponent<MeshFilter>().sharedMesh = mesh;
            transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            transform.localScale = Vector3.one;
        }

        void OnDestroy() { if (Instance == this) Instance = null; if (mesh) Destroy(mesh); }

        /// <summary>Adds a segment between two contact points. Returns the new segment end data.</summary>
        public void AddSegment(Vector3 from, Vector3 to, Vector3 normal, float intensity, ref Vector3 lastLeft, ref Vector3 lastRight, bool continuous)
        {
            Vector3 dir = to - from;
            if (dir.sqrMagnitude < 0.0001f) return;
            Vector3 side = Vector3.Cross(dir.normalized, normal) * markWidth * 0.5f;
            Vector3 off = normal * groundOffset;
            Vector3 l0 = continuous ? lastLeft : from + side + off;
            Vector3 r0 = continuous ? lastRight : from - side + off;
            Vector3 l1 = to + side + off, r1 = to - side + off;
            int v = next * 4;
            verts[v] = l0; verts[v + 1] = r0; verts[v + 2] = l1; verts[v + 3] = r1;
            byte a = (byte)(Mathf.Clamp01(intensity) * 200);
            var c = new Color32(20, 20, 20, a);
            cols[v] = c; cols[v + 1] = c; cols[v + 2] = c; cols[v + 3] = c;
            lastLeft = l1; lastRight = r1;
            next = (next + 1) % maxSegments;
            dirty = true;
        }

        void LateUpdate()
        {
            if (!dirty) return;
            dirty = false;
            mesh.vertices = verts;
            mesh.colors32 = cols;
        }

        public void Clear()
        {
            System.Array.Clear(cols, 0, cols.Length);
            System.Array.Clear(verts, 0, verts.Length);
            dirty = true;
        }
    }
}
