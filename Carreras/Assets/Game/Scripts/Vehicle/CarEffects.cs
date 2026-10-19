using UnityEngine;

namespace StreetRacing.Vehicle
{
    /// <summary>Tyre smoke particles and skid marks driven by wheel slip.</summary>
    public class CarEffects : MonoBehaviour
    {
        public ArcadeCarController car;
        [Tooltip("One smoke particle system per wheel (same order as the controller's wheels). Entries may be null.")]
        public ParticleSystem[] wheelSmoke;
        public float smokeRate = 70f;
        [Tooltip("Lateral slip (m/s) where marks/smoke start.")]
        public float slipThreshold = 3f;
        public float minSegmentLength = 0.3f;

        Vector3[] lastPos, lastL, lastR;
        bool[] wasMarking;

        void Start()
        {
            if (!car) car = GetComponentInParent<ArcadeCarController>();
            int n = car.wheels.Length;
            lastPos = new Vector3[n]; lastL = new Vector3[n]; lastR = new Vector3[n];
            wasMarking = new bool[n];
        }

        void LateUpdate()
        {
            if (!car) return;
            var skid = SkidmarkManager.Instance;
            for (int i = 0; i < car.wheels.Length; i++)
            {
                var w = car.wheels[i];
                float lat = Mathf.Clamp01((Mathf.Abs(w.lateralSlip) - slipThreshold) / 5f);
                float slip = w.grounded && car.SpeedKmh > 6f ? Mathf.Max(lat, w.longitudinalSlip) : 0f;
                if (w.grounded && car.SpeedKmh <= 6f && w.longitudinalSlip > 0.5f) slip = w.longitudinalSlip; // burnout

                if (wheelSmoke != null && i < wheelSmoke.Length && wheelSmoke[i])
                {
                    var em = wheelSmoke[i].emission;
                    em.rateOverTimeMultiplier = slip > 0.2f ? smokeRate * slip : 0f;
                    wheelSmoke[i].transform.position = w.contactPoint + Vector3.up * 0.15f;
                }

                if (!skid) continue;
                bool marking = slip > 0.25f;
                if (marking)
                {
                    Vector3 p = w.contactPoint;
                    if (!wasMarking[i]) { lastPos[i] = p; wasMarking[i] = true; lastL[i] = lastR[i] = Vector3.zero; continue; }
                    if ((p - lastPos[i]).sqrMagnitude >= minSegmentLength * minSegmentLength)
                    {
                        bool cont = lastL[i] != Vector3.zero;
                        skid.AddSegment(lastPos[i], p, w.hit.normal, slip, ref lastL[i], ref lastR[i], cont);
                        lastPos[i] = p;
                    }
                }
                else wasMarking[i] = false;
            }
        }

        /// <summary>Breaks mark continuity (call after teleporting the car).</summary>
        public void ResetMarks()
        {
            if (wasMarking == null) return;
            for (int i = 0; i < wasMarking.Length; i++) wasMarking[i] = false;
        }
    }
}
