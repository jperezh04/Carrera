using UnityEngine;

namespace StreetRacing.Vehicle
{
    /// <summary>
    /// Engine sound built from three looping samples (low / mid / high) cross-faded by a simulated
    /// RPM with automatic gear shifts, plus tyre skid, wind and impact sounds.
    /// </summary>
    public class CarAudio : MonoBehaviour
    {
        public ArcadeCarController car;

        [Header("Clips")]
        public AudioClip engineIdle;
        public AudioClip engineLow;
        public AudioClip engineMid;
        public AudioClip engineHigh;
        public AudioClip skid;
        public AudioClip wind;
        public AudioClip[] impacts;

        [Header("Engine")]
        public int gearCount = 6;
        public float minPitch = 0.55f;
        public float maxPitch = 1.45f;
        [Range(0f, 1f)] public float engineVolume = 0.55f;
        [Range(0f, 1f)] public float skidVolume = 0.5f;
        [Range(0f, 1f)] public float windVolume = 0.25f;
        [Range(0f, 1f)] public float impactVolume = 0.8f;

        public float Rpm01 { get; private set; }
        public int Gear { get; private set; } = 1;

        AudioSource idleSrc, lowSrc, midSrc, highSrc, skidSrc, windSrc, oneShotSrc;
        float rpmSmooth;
        float lastImpactTime;

        void Awake()
        {
            if (!car) car = GetComponentInParent<ArcadeCarController>();
            idleSrc = MakeLoop(engineIdle, "EngineIdle");
            lowSrc = MakeLoop(engineLow, "EngineLow");
            midSrc = MakeLoop(engineMid, "EngineMid");
            highSrc = MakeLoop(engineHigh, "EngineHigh");
            skidSrc = MakeLoop(skid, "Skid");
            windSrc = MakeLoop(wind, "Wind");
            oneShotSrc = gameObject.AddComponent<AudioSource>();
            oneShotSrc.spatialBlend = 0.6f;
            oneShotSrc.playOnAwake = false;
        }

        AudioSource MakeLoop(AudioClip clip, string label)
        {
            if (!clip) return null;
            var s = gameObject.AddComponent<AudioSource>();
            s.clip = clip;
            s.loop = true;
            s.volume = 0f;
            s.spatialBlend = 0.5f;
            s.dopplerLevel = 0f;
            s.playOnAwake = false;
            s.Play();
            return s;
        }

        void Update()
        {
            if (!car) return;
            float speed01 = car.SpeedNormalized;
            float throttle = car.Input ? car.Input.Throttle : 0f;

            // Simulated gearbox: each gear covers an equal slice of the speed range with overlap.
            float gearSpan = 1f / gearCount;
            Gear = Mathf.Clamp(Mathf.FloorToInt(speed01 / gearSpan) + 1, 1, gearCount);
            float inGear = (speed01 - (Gear - 1) * gearSpan) / gearSpan;   // 0..1 within gear
            float rpmTarget = Gear == 1 ? inGear : Mathf.Lerp(0.35f, 1f, inGear);
            if (car.DriftAmount > 0.3f || (throttle > 0.5f && speed01 < 0.03f)) rpmTarget = Mathf.Max(rpmTarget, 0.75f * throttle);
            if (car.IsReversing) rpmTarget = Mathf.Clamp01(Mathf.Abs(car.ForwardSpeed) / 12f);
            rpmSmooth = Mathf.Lerp(rpmSmooth, rpmTarget, Time.deltaTime * 8f);
            Rpm01 = rpmSmooth;

            float pitch = Mathf.Lerp(minPitch, maxPitch, rpmSmooth);
            float load = Mathf.Lerp(0.55f, 1f, throttle);
            float idleW = Mathf.Clamp01(1f - rpmSmooth * 4f);
            float lowW = Mathf.Clamp01(1f - Mathf.Abs(rpmSmooth - 0.25f) * 3f);
            float midW = Mathf.Clamp01(1f - Mathf.Abs(rpmSmooth - 0.55f) * 3f);
            float highW = Mathf.Clamp01((rpmSmooth - 0.6f) * 3f);
            Set(idleSrc, idleW * engineVolume, 1f);
            Set(lowSrc, lowW * engineVolume * load, pitch);
            Set(midSrc, midW * engineVolume * load, pitch);
            Set(highSrc, highW * engineVolume * load, pitch * 0.95f);

            float slip = 0f;
            foreach (var w in car.wheels)
                if (w.grounded) slip = Mathf.Max(slip, Mathf.Clamp01((Mathf.Abs(w.lateralSlip) - 2.5f) / 6f), w.longitudinalSlip);
            if (car.SpeedKmh < 5f) slip = 0f;
            if (skidSrc) { skidSrc.volume = Mathf.Lerp(skidSrc.volume, slip * skidVolume, Time.deltaTime * 10f); skidSrc.pitch = 0.9f + slip * 0.2f; }
            if (windSrc) { windSrc.volume = speed01 * speed01 * windVolume; windSrc.pitch = 0.8f + speed01 * 0.5f; }
        }

        static void Set(AudioSource s, float vol, float pitch)
        {
            if (!s) return;
            s.volume = vol;
            s.pitch = pitch;
        }

        void OnCollisionEnter(Collision c)
        {
            if (impacts == null || impacts.Length == 0 || Time.time - lastImpactTime < 0.15f) return;
            float v = c.relativeVelocity.magnitude;
            if (v < 2.5f) return;
            lastImpactTime = Time.time;
            var clip = impacts[Random.Range(0, impacts.Length)];
            if (clip) oneShotSrc.PlayOneShot(clip, Mathf.Clamp01(v / 20f) * impactVolume);
        }
    }
}
