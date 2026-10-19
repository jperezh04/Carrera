using UnityEngine;

namespace StreetRacing.Race
{
    /// <summary>
    /// Trigger volume placed along the circuit. Index 0 is the Start/Finish line;
    /// indices 1..N-1 are intermediate checkpoints that must be crossed in order.
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public class RaceCheckpoint : MonoBehaviour
    {
        public int index;
        public bool isStartFinish;

        void OnTriggerEnter(Collider other)
        {
            var car = other.GetComponentInParent<Vehicle.ArcadeCarController>();
            if (!car) return;
            if (RaceManager.Instance)
                RaceManager.Instance.OnCarEnteredCheckpoint(this, car);
        }

        void OnDrawGizmos()
        {
            var box = GetComponent<BoxCollider>();
            if (!box) return;
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = isStartFinish
                ? new Color(0.1f, 1f, 0.25f, 0.28f)
                : new Color(0.1f, 0.75f, 1f, 0.18f);
            Gizmos.DrawCube(box.center, box.size);
            Gizmos.color = isStartFinish ? Color.green : Color.cyan;
            Gizmos.DrawWireCube(box.center, box.size);
        }
    }
}
