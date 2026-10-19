using UnityEngine;

namespace StreetRacing.CameraSystem
{
    /// <summary>
    /// Third-person chase camera with smoothing, speed-based pull-back, FOV widening,
    /// look-ahead, drift swing and obstacle avoidance. No external dependencies (no Cinemachine).
    /// </summary>
    public class RaceCamera : MonoBehaviour
    {
        public Vehicle.ArcadeCarController target;

        [Header("Placement")]
        public float distance = 6.2f;
        public float height = 2.0f;
        [Tooltip("Extra distance at top speed.")]
        public float distanceAtSpeed = 1.6f;
        [Tooltip("Height lowered at top speed (more dramatic).")]
        public float heightDropAtSpeed = 0.35f;
        [Tooltip("Point on the car the camera looks at (local space).")]
        public Vector3 lookOffset = new Vector3(0f, 1.0f, 0f);
        [Tooltip("How far ahead of the car the camera aims at top speed.")]
        public float lookAhead = 4f;

        [Header("Smoothing")]
        public float positionDamping = 9f;
        public float rotationDamping = 5f;
        [Tooltip("How much of the drift angle the camera keeps (0 = follow body, 1 = follow velocity).")]
        [Range(0f, 1f)] public float velocityAlign = 0.45f;

        [Header("Field of view")]
        public float baseFov = 60f;
        public float maxFov = 76f;

        [Header("Collision")]
        public LayerMask obstacleMask = ~0;
        public float collisionRadius = 0.3f;

        Camera cam;
        float yaw;
        Vector3 smoothedPos;
        bool snapped;

        void Awake() { cam = GetComponent<Camera>(); }

        public void Snap()
        {
            if (!target) return;
            yaw = target.transform.eulerAngles.y;
            smoothedPos = DesiredPosition(0f);
            transform.position = smoothedPos;
            transform.LookAt(target.transform.TransformPoint(lookOffset));
            snapped = true;
        }

        Vector3 DesiredPosition(float speed01)
        {
            Quaternion rot = Quaternion.Euler(0f, yaw, 0f);
            float d = distance + distanceAtSpeed * speed01;
            float h = height - heightDropAtSpeed * speed01;
            return target.transform.position + rot * new Vector3(0f, h, -d);
        }

        void LateUpdate()
        {
            if (!target) return;
            if (!snapped) Snap();
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            float speed01 = target.SpeedNormalized;

            // Heading: body forward blended with velocity direction (keeps drifts readable).
            Vector3 fwd = target.transform.forward; fwd.y = 0f;
            Vector3 vel = target.Body.linearVelocity; vel.y = 0f;
            Vector3 heading = fwd;
            if (vel.magnitude > 5f && target.ForwardSpeed > 0f)
                heading = Vector3.Slerp(fwd.normalized, vel.normalized, velocityAlign);
            if (target.ForwardSpeed < -2f) heading = fwd; // reversing: keep behind body
            float targetYaw = Mathf.Atan2(heading.x, heading.z) * Mathf.Rad2Deg;
            yaw = Mathf.LerpAngle(yaw, targetYaw, 1f - Mathf.Exp(-rotationDamping * dt));

            Vector3 desired = DesiredPosition(speed01);
            smoothedPos = Vector3.Lerp(smoothedPos, desired, 1f - Mathf.Exp(-positionDamping * dt));
            // Never fall below the car's own height band (stops ground clipping on bumps).
            float minY = target.transform.position.y + 0.6f;
            if (smoothedPos.y < minY) smoothedPos.y = minY;

            Vector3 lookPoint = target.transform.TransformPoint(lookOffset) + heading.normalized * lookAhead * speed01;

            // Obstacle avoidance
            Vector3 pos = smoothedPos;
            Vector3 dir = pos - lookPoint;
            if (Physics.SphereCast(lookPoint, collisionRadius, dir.normalized, out var hit, dir.magnitude, obstacleMask, QueryTriggerInteraction.Ignore)
                && !hit.transform.IsChildOf(target.transform))
                pos = lookPoint + dir.normalized * Mathf.Max(1.5f, hit.distance - 0.1f);

            transform.position = pos;
            transform.rotation = Quaternion.LookRotation(lookPoint - pos, Vector3.up);
            if (cam) cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, Mathf.Lerp(baseFov, maxFov, speed01 * speed01), 1f - Mathf.Exp(-3f * dt));
        }
    }
}
