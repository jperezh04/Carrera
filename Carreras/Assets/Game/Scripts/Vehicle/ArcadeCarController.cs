using UnityEngine;

namespace StreetRacing.Vehicle
{
    /// <summary>
    /// Arcade car physics built on a single Rigidbody with raycast suspension.
    ///  - Suspension: one spring/damper raycast per wheel (gives pitch/roll and absorbs bumps).
    ///  - Grip: lateral velocity is removed up to a grip limit (handbrake lowers it -> drift).
    ///  - Steering: a yaw-rate controller based on a bicycle model, limited by grip and speed,
    ///    which keeps handling predictable at any speed.
    /// All tuning values are exposed in the Inspector.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(CarInput))]
    public class ArcadeCarController : MonoBehaviour
    {
        [System.Serializable]
        public class Wheel
        {
            public string name = "Wheel";
            [Tooltip("Top of the suspension. The ray is cast downwards from here.")]
            public Transform anchor;
            [Tooltip("Visual pivot of the wheel (centre of the wheel mesh). Optional.")]
            public Transform visual;
            public bool front;
            public float radius = 0.34f;

            [System.NonSerialized] public bool grounded;
            [System.NonSerialized] public RaycastHit hit;
            [System.NonSerialized] public float compression;      // 0..1
            [System.NonSerialized] public float lastLength;
            [System.NonSerialized] public float spin;              // degrees
            [System.NonSerialized] public float lateralSlip;       // m/s sideways at contact
            [System.NonSerialized] public float longitudinalSlip;  // 0..1 wheelspin/lock amount
            [System.NonSerialized] public Quaternion baseRotation;
            [System.NonSerialized] public Vector3 contactPoint;
        }

        [Header("Wheels")]
        public Wheel[] wheels = new Wheel[0];
        public LayerMask groundMask = ~0;

        [Header("Engine")]
        [Tooltip("Top speed in km/h.")]
        public float maxSpeedKmh = 230f;
        [Tooltip("Peak forward acceleration in m/s².")]
        public float acceleration = 13f;
        [Tooltip("Acceleration multiplier depending on speed (x = speed / top speed).")]
        public AnimationCurve accelerationCurve = new AnimationCurve(
            new Keyframe(0f, 0.85f), new Keyframe(0.15f, 1f), new Keyframe(0.6f, 0.7f), new Keyframe(0.9f, 0.3f), new Keyframe(1f, 0f));
        public float maxReverseKmh = 45f;
        public float reverseAcceleration = 7f;

        [Header("Brakes & drag")]
        [Tooltip("Braking deceleration in m/s².")]
        public float brakeDeceleration = 26f;
        [Tooltip("Deceleration when coasting (no pedals), m/s².")]
        public float coastDeceleration = 2.2f;
        [Tooltip("Aerodynamic drag coefficient (accel = k * v²).")]
        public float airDrag = 0.0009f;
        [Tooltip("Downforce coefficient (accel = k * v²). Keeps the car planted at speed.")]
        public float downforce = 0.004f;

        [Header("Steering")]
        [Tooltip("Max steering angle at low speed (degrees).")]
        public float maxSteerAngleLowSpeed = 34f;
        [Tooltip("Max steering angle at high speed (degrees).")]
        public float maxSteerAngleHighSpeed = 9f;
        [Tooltip("Speed (km/h) at which the high-speed steer angle is reached.")]
        public float steerFadeSpeedKmh = 180f;
        public float wheelBase = 2.6f;
        [Tooltip("Absolute yaw-rate cap (rad/s).")]
        public float maxYawRate = 2.1f;
        [Tooltip("How aggressively the car reaches the target yaw rate.")]
        public float yawResponse = 9f;
        [Tooltip("Allows a bit of oversteer beyond what grip permits (1 = neutral).")]
        public float oversteerAllowance = 1.25f;

        [Header("Grip")]
        [Tooltip("Lateral grip limit expressed in g.")]
        public float grip = 1.35f;
        [Tooltip("How quickly sideways velocity is removed (1/s) before hitting the grip limit.")]
        public float lateralStiffness = 14f;
        [Tooltip("Grip multiplier while the handbrake is held.")]
        [Range(0.05f, 1f)] public float handbrakeGripMultiplier = 0.28f;
        [Tooltip("Extra yaw authority while the handbrake is held.")]
        public float handbrakeYawBoost = 1.45f;
        [Tooltip("Deceleration caused by the handbrake (m/s²).")]
        public float handbrakeDeceleration = 3.5f;
        [Tooltip("Seconds to recover full grip after releasing the handbrake.")]
        public float gripRecoveryTime = 0.45f;
        [Tooltip("Portion of sideways speed converted back into forward speed while drifting (arcade momentum).")]
        [Range(0f, 1f)] public float driftMomentumKeep = 0.35f;

        [Header("Suspension")]
        public float suspensionLength = 0.32f;
        [Tooltip("Spring strength in m/s² at full compression (per car).")]
        public float springStrength = 24f;
        [Tooltip("Damping in (m/s²)/(m/s).")]
        public float damping = 7f;

        [Header("Stability")]
        public Vector3 centerOfMass = new Vector3(0f, 0.35f, 0.05f);
        [Tooltip("Self-righting torque while airborne.")]
        public float airStabilization = 6f;
        [Tooltip("Seconds upside-down before an automatic reset is requested.")]
        public float flipResetDelay = 2.5f;

        // ---- Runtime state ----
        Rigidbody body;
        CarInput carInput;
        public Rigidbody Body => body ? body : (body = GetComponent<Rigidbody>());
        public CarInput Input => carInput ? carInput : (carInput = GetComponent<CarInput>());
        public float SpeedKmh => (Body && !Body.isKinematic) ? Body.linearVelocity.magnitude * 3.6f : 0f;
        public float ForwardSpeed { get; private set; }
        public float SpeedNormalized => Mathf.Clamp01(Mathf.Abs(ForwardSpeed) * 3.6f / maxSpeedKmh);
        public bool IsGrounded { get; private set; }
        public float DriftAmount { get; private set; }  // 0..1
        public bool IsBraking { get; private set; }
        public bool IsReversing { get; private set; }
        public float SteerAngle { get; private set; }
        public float LateralAcceleration { get; private set; }

        /// <summary>Raised when the car wants to be placed back on track (R key or flipped).</summary>
        public event System.Action ResetRequested;

        float gripRecovery = 1f;
        float flippedTimer;
        Vector3 lastVelocity;

        void Awake()
        {
            body = GetComponent<Rigidbody>();
            carInput = GetComponent<CarInput>();
            body.centerOfMass = centerOfMass;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            Body.maxAngularVelocity = 7f;
            foreach (var w in wheels)
            {
                if (w.visual) w.baseRotation = w.visual.localRotation;
                w.lastLength = suspensionLength;
            }
        }

        void Update()
        {
            if (Input.ResetPressed) ResetRequested?.Invoke();
            UpdateWheelVisuals();
        }

        void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;
            Vector3 up = transform.up;
            Vector3 vel = Body.linearVelocity;
            ForwardSpeed = Vector3.Dot(vel, transform.forward);

            // ---------- Suspension ----------
            int groundedCount = 0;
            Vector3 groundNormal = Vector3.zero;
            float perWheelMass = Body.mass / Mathf.Max(1, wheels.Length);
            foreach (var w in wheels)
            {
                if (!w.anchor) continue;
                Vector3 origin = w.anchor.position;
                float rayLen = suspensionLength + w.radius;
                if (Physics.Raycast(origin, -up, out w.hit, rayLen, groundMask, QueryTriggerInteraction.Ignore))
                {
                    w.grounded = true;
                    groundedCount++;
                    groundNormal += w.hit.normal;
                    float length = w.hit.distance - w.radius;
                    w.compression = 1f - Mathf.Clamp01(length / suspensionLength);
                    float suspVel = (w.lastLength - length) / dt; // positive = compressing
                    w.lastLength = length;
                    float accel = w.compression * springStrength + suspVel * damping;
                    accel = Mathf.Max(0f, accel);
                    Body.AddForceAtPosition(up * accel * perWheelMass, origin, ForceMode.Force);
                    w.contactPoint = w.hit.point;
                }
                else
                {
                    w.grounded = false;
                    w.compression = 0f;
                    w.lastLength = suspensionLength;
                    w.contactPoint = origin - up * rayLen;
                }
            }
            IsGrounded = groundedCount > 0;
            float groundFactor = wheels.Length > 0 ? (float)groundedCount / wheels.Length : 0f;
            groundNormal = groundedCount > 0 ? groundNormal.normalized : Vector3.up;

            // Ground-plane basis
            Vector3 fwd = Vector3.ProjectOnPlane(transform.forward, groundNormal).normalized;
            Vector3 right = Vector3.Cross(groundNormal, fwd).normalized;
            float fwdSpeed = Vector3.Dot(vel, fwd);
            float latSpeed = Vector3.Dot(vel, right);
            float absSpeed = Mathf.Abs(fwdSpeed);

            // ---------- Inputs ----------
            bool locked = Input.Locked;
            float throttle = Input.Throttle;
            float brake = Input.Brake;
            bool handbrake = Input.Handbrake && !locked;

            // ---------- Longitudinal ----------
            float maxSpeed = maxSpeedKmh / 3.6f;
            float maxReverse = maxReverseKmh / 3.6f;
            float longAccel = 0f;
            IsBraking = false;
            IsReversing = false;

            if (locked)
            {
                longAccel = -Mathf.Sign(fwdSpeed) * Mathf.Min(brakeDeceleration, absSpeed / dt);
                IsBraking = true;
            }
            else
            {
                if (throttle > 0.01f)
                {
                    if (fwdSpeed < -0.5f) { longAccel += brakeDeceleration * throttle; IsBraking = true; }
                    else
                    {
                        float k = accelerationCurve.Evaluate(Mathf.Clamp01(fwdSpeed / maxSpeed));
                        longAccel += throttle * acceleration * k;
                    }
                }
                if (brake > 0.01f)
                {
                    if (fwdSpeed > 0.8f) { longAccel -= brakeDeceleration * brake; IsBraking = true; }
                    else if (throttle < 0.01f)
                    {
                        IsReversing = true;
                        if (fwdSpeed > -maxReverse) longAccel -= reverseAcceleration * brake;
                    }
                }
                if (throttle < 0.01f && brake < 0.01f && absSpeed > 0.1f)
                    longAccel -= Mathf.Sign(fwdSpeed) * Mathf.Min(coastDeceleration, absSpeed / dt);
                if (handbrake && absSpeed > 0.1f)
                    longAccel -= Mathf.Sign(fwdSpeed) * Mathf.Min(handbrakeDeceleration, absSpeed / dt);
                if (fwdSpeed > maxSpeed) longAccel -= (fwdSpeed - maxSpeed) * 2f;
            }
            longAccel -= Mathf.Sign(fwdSpeed) * airDrag * fwdSpeed * fwdSpeed;

            // ---------- Lateral grip ----------
            if (handbrake) gripRecovery = handbrakeGripMultiplier;
            else gripRecovery = Mathf.MoveTowards(gripRecovery, 1f, dt / Mathf.Max(0.01f, gripRecoveryTime));
            float gripLimit = grip * 9.81f * gripRecovery;
            float latAccel = Mathf.Clamp(-latSpeed * lateralStiffness, -gripLimit, gripLimit);

            // Arcade drift momentum: give back part of the lost sideways speed as forward speed.
            float slideAmount = Mathf.Abs(latSpeed);
            if (gripRecovery < 0.99f && fwdSpeed > 3f)
                longAccel += Mathf.Abs(latAccel) * driftMomentumKeep;

            if (groundFactor > 0f)
            {
                Vector3 accelVec = (fwd * longAccel + right * latAccel) * Mathf.Max(groundFactor, 0.5f);
                Body.AddForce(accelVec, ForceMode.Acceleration);
                Body.AddForce(-groundNormal * downforce * fwdSpeed * fwdSpeed, ForceMode.Acceleration);
            }
            LateralAcceleration = latAccel;
            DriftAmount = Mathf.Clamp01((slideAmount - 2.5f) / 8f) * (absSpeed > 4f ? 1f : 0f);

            // ---------- Steering (yaw-rate controller) ----------
            float speedT = Mathf.Clamp01(absSpeed * 3.6f / steerFadeSpeedKmh);
            float maxAngle = Mathf.Lerp(maxSteerAngleLowSpeed, maxSteerAngleHighSpeed, speedT * speedT * (3f - 2f * speedT));
            SteerAngle = Input.Steer * maxAngle;
            Vector3 angVel = Body.angularVelocity;
            float currentYaw = Vector3.Dot(angVel, groundNormal);
            if (groundFactor > 0f)
            {
                float targetYaw = fwdSpeed / wheelBase * Mathf.Tan(SteerAngle * Mathf.Deg2Rad);
                float gripYaw = absSpeed > 1f ? (grip * 9.81f / absSpeed) * oversteerAllowance : maxYawRate;
                float cap = Mathf.Min(maxYawRate, gripYaw);
                if (handbrake || gripRecovery < 0.99f) cap = maxYawRate * handbrakeYawBoost;
                targetYaw = Mathf.Clamp(targetYaw, -cap, cap);
                // Low speed: let the car rotate slowly when nearly stopped.
                targetYaw *= Mathf.Clamp01(absSpeed / 2f);
                float yawError = targetYaw - currentYaw;
                Body.AddTorque(groundNormal * yawError * yawResponse * groundFactor, ForceMode.Acceleration);
            }
            else
            {
                // Airborne: gently level the car to avoid landing on the roof.
                Vector3 axis = Vector3.Cross(transform.up, Vector3.up);
                Body.AddTorque(axis * airStabilization - angVel * 0.5f, ForceMode.Acceleration);
            }

            // ---------- Wheel slip info for effects ----------
            foreach (var w in wheels)
            {
                if (!w.grounded) { w.lateralSlip = 0f; w.longitudinalSlip = 0f; continue; }
                Vector3 pv = Body.GetPointVelocity(w.hit.point);
                w.lateralSlip = Vector3.Dot(pv, right);
                float lockSlip = (!w.front && handbrake && absSpeed > 3f) ? 1f : 0f;
                float brakeSlip = (IsBraking && absSpeed > 18f && brake > 0.9f) ? 0.4f : 0f;
                float spinSlip = (!w.front && throttle > 0.9f && fwdSpeed < 6f && fwdSpeed > -0.5f) ? 0.8f : 0f;
                w.longitudinalSlip = Mathf.Max(lockSlip, brakeSlip, spinSlip);
            }

            // ---------- Flip detection ----------
            if (Vector3.Dot(transform.up, Vector3.up) < 0.25f && absSpeed < 3f)
            {
                flippedTimer += dt;
                if (flippedTimer > flipResetDelay) { flippedTimer = 0f; ResetRequested?.Invoke(); }
            }
            else flippedTimer = 0f;

            lastVelocity = vel;
        }

        void UpdateWheelVisuals()
        {
            float dt = Time.deltaTime;
            foreach (var w in wheels)
            {
                if (!w.visual || !w.anchor) continue;
                float length = w.grounded ? Mathf.Clamp(w.hit.distance - w.radius, 0f, suspensionLength) : suspensionLength;
                w.visual.position = w.anchor.position - transform.up * length;
                bool locked = !w.front && Input.Handbrake;
                if (!locked) w.spin += ForwardSpeed / Mathf.Max(0.05f, w.radius) * Mathf.Rad2Deg * dt;
                w.spin %= 360f;
                float steer = w.front ? SteerAngle : 0f;
                w.visual.localRotation = Quaternion.Euler(0f, steer, 0f) * Quaternion.Euler(w.spin, 0f, 0f) * w.baseRotation;
            }
        }

        /// <summary>Teleports the car and clears all motion.</summary>
        public void ResetTo(Vector3 position, Quaternion rotation)
        {
            if (Body)
            {
                if (Application.isPlaying && !Body.isKinematic)
                {
                    Body.linearVelocity = Vector3.zero;
                    Body.angularVelocity = Vector3.zero;
                }
                Body.position = position;
                Body.rotation = rotation;
            }
            transform.SetPositionAndRotation(position, rotation);
            gripRecovery = 1f;
            flippedTimer = 0f;
            foreach (var w in wheels) w.lastLength = suspensionLength;
            Physics.SyncTransforms();
        }

        void OnDrawGizmosSelected()
        {
            if (wheels == null) return;
            foreach (var w in wheels)
            {
                if (!w.anchor) continue;
                Gizmos.color = Color.yellow;
                Vector3 o = w.anchor.position;
                Gizmos.DrawLine(o, o - transform.up * (suspensionLength + w.radius));
                Gizmos.DrawWireSphere(o - transform.up * suspensionLength, w.radius);
            }
            Gizmos.color = Color.red;
            Gizmos.DrawSphere(transform.TransformPoint(centerOfMass), 0.08f);
        }
    }
}
