using UnityEngine;

namespace StreetRacing.Race
{
    /// <summary>
    /// Holds the ordered array of circuit Waypoints and renders clear visual gizmos
    /// and in-game 3D waypoint markers along the track.
    /// </summary>
    public class WaypointCircuitManager : MonoBehaviour
    {
        public static WaypointCircuitManager Instance { get; private set; }

        [Tooltip("Lista ordenada de los GameObjects Waypoint que forman el circuito.")]
        public GameObject[] waypoints;

        [Tooltip("Rival cars controlled by FollowWP.")]
        public FollowWP[] aiCars;

        [Tooltip("Mostrar u ocultar los marcadores 3D de los waypoints sobre la pista.")]
        public bool showInWorldMarkers = true;

        void Awake()
        {
            Instance = this;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void SetMarkersVisible(bool visible)
        {
            showInWorldMarkers = visible;
            if (waypoints == null) return;
            foreach (var wp in waypoints)
            {
                if (!wp) continue;
                foreach (var r in wp.GetComponentsInChildren<Renderer>(true))
                    r.enabled = visible;
            }
        }

        void OnDrawGizmos()
        {
            if (waypoints == null || waypoints.Length == 0) return;

            for (int i = 0; i < waypoints.Length; i++)
            {
                var wp = waypoints[i];
                if (!wp) continue;
                Vector3 p = wp.transform.position + Vector3.up * 0.6f;

                Gizmos.color = (i == 0) ? Color.green : new Color(0.15f, 0.9f, 1f, 0.9f);
                Gizmos.DrawSphere(p, 1.15f);

                var nextWp = waypoints[(i + 1) % waypoints.Length];
                if (nextWp)
                {
                    Vector3 np = nextWp.transform.position + Vector3.up * 0.6f;
                    Gizmos.color = new Color(1f, 0.78f, 0.1f, 0.9f);
                    Gizmos.DrawLine(p, np);
                }
            }
        }
    }
}
