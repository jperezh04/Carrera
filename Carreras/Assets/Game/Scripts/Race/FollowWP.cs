using UnityEngine;
using StreetRacing.Race;

/// <summary>
/// Implementación del script FollowWP de la guía de laboratorio (Sesión 6: Waypoints),
/// adaptado para los vehículos rivales en el circuito de carreras 3D.
/// Navega secuencialmente por el arreglo de waypoints usando Quaternion.LookRotation,
/// Quaternion.Slerp y transform.Translate(0, 0, speed * Time.deltaTime).
/// </summary>
public class FollowWP : MonoBehaviour
{
    [Header("Waypoints del Circuito (Guía Sesión 6)")]
    public GameObject[] waypoints;
    public int currentWP = 0;

    [Header("Parámetros de Movimiento")]
    public float speed = 28.0f;
    public float rotSpeed = 6.0f;
    [Tooltip("Distancia a la que se considera alcanzado el waypoint actual (en la guía: 3-4m).")]
    public float accuracy = 4.0f;
    [Tooltip("Desplazamiento lateral opcional respecto al waypoint para que dos carros no se solapen.")]
    public float laneOffset = 0.0f;

    [Header("Ajuste a Calzada y Ruedas Visuales")]
    public LayerMask groundMask = ~0;
    public Transform[] wheelVisuals;
    public float wheelRadius = 0.35f;

    Vector3 startPosition;
    Quaternion startRotation;
    bool startPoseSaved;
    float wheelSpin;

    void Awake()
    {
        SaveStartPose();
    }

    void Start()
    {
        if (!startPoseSaved) SaveStartPose();

        var rb = GetComponent<Rigidbody>();
        if (rb)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
        }
    }

    public void SaveStartPose()
    {
        startPosition = transform.position;
        startRotation = transform.rotation;
        startPoseSaved = true;
    }

    public void ResetToStart()
    {
        currentWP = 0;
        if (startPoseSaved)
            transform.SetPositionAndRotation(startPosition, startRotation);
    }

    void Update()
    {
        if (waypoints == null || waypoints.Length == 0) return;

        // Esperar en la parrilla de salida durante la cuenta regresiva (3, 2, 1) o si la carrera terminó
        if (RaceManager.Instance != null && RaceManager.Instance.State != RaceState.Racing)
            return;

        if (currentWP >= waypoints.Length)
            currentWP = 0;

        GameObject wpObj = waypoints[currentWP];
        if (!wpObj) return;

        Vector3 targetPos = GetTargetWorldPosition(currentWP);

        // Distancia horizontal para comprobar llegada al waypoint (lógica de la guía FollowWP)
        Vector3 flatSelf = new Vector3(this.transform.position.x, targetPos.y, this.transform.position.z);
        if (Vector3.Distance(flatSelf, targetPos) < accuracy)
            currentWP++;

        if (currentWP >= waypoints.Length)
            currentWP = 0;

        targetPos = GetTargetWorldPosition(currentWP);
        Vector3 dir = targetPos - this.transform.position;
        if (dir.sqrMagnitude > 0.01f)
        {
            Quaternion lookatWP = Quaternion.LookRotation(dir);
            this.transform.rotation = Quaternion.Slerp(this.transform.rotation, lookatWP, rotSpeed * Time.deltaTime);
        }

        this.transform.Translate(0, 0, speed * Time.deltaTime);

        // Mantener las ruedas apoyadas sobre la altura real del asfalto
        SnapToRoadHeight();
        AnimateWheels();
    }

    public Vector3 GetTargetWorldPosition(int wpIndex)
    {
        if (waypoints == null || waypoints.Length == 0) return transform.position;
        int idx = ((wpIndex % waypoints.Length) + waypoints.Length) % waypoints.Length;
        Transform wp = waypoints[idx] ? waypoints[idx].transform : null;
        if (!wp) return transform.position;
        return wp.position + wp.right * laneOffset;
    }

    readonly RaycastHit[] rayHits = new RaycastHit[8];

    void SnapToRoadHeight()
    {
        Vector3 rayOrigin = transform.position + Vector3.up * 2.5f;
        int hitCount = Physics.RaycastNonAlloc(rayOrigin, Vector3.down, rayHits, 6.0f, groundMask, QueryTriggerInteraction.Ignore);
        float bestDist = float.MaxValue;
        float bestY = transform.position.y;
        bool found = false;

        for (int i = 0; i < hitCount; i++)
        {
            var h = rayHits[i];
            if (!h.collider || h.collider.transform.IsChildOf(transform)) continue;
            if (h.distance < bestDist)
            {
                bestDist = h.distance;
                bestY = h.point.y;
                found = true;
            }
        }

        if (found)
        {
            Vector3 p = transform.position;
            p.y = Mathf.Lerp(p.y, bestY + 0.04f, Time.deltaTime * 18f);
            transform.position = p;
        }
    }

    void AnimateWheels()
    {
        if (wheelVisuals == null || wheelVisuals.Length == 0) return;
        wheelSpin = (wheelSpin + (speed / Mathf.Max(0.1f, wheelRadius)) * Mathf.Rad2Deg * Time.deltaTime) % 360f;
        for (int i = 0; i < wheelVisuals.Length; i++)
        {
            if (wheelVisuals[i])
                wheelVisuals[i].localRotation = Quaternion.Euler(wheelSpin, 0f, 0f);
        }
    }

    void OnDrawGizmos()
    {
        if (waypoints == null || waypoints.Length == 0) return;
        int idx = Mathf.Clamp(currentWP, 0, waypoints.Length - 1);
        if (!waypoints[idx]) return;

        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(transform.position + Vector3.up * 0.8f, GetTargetWorldPosition(idx) + Vector3.up * 0.8f);
        Gizmos.DrawWireSphere(GetTargetWorldPosition(idx) + Vector3.up * 0.8f, 1.1f);
    }
}
