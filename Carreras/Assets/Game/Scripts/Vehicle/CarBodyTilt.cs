using UnityEngine;

namespace StreetRacing.Vehicle
{
    /// <summary>
    /// Purely visual body lean (roll under cornering, pitch under acceleration/braking) applied to the
    /// body model only, so the car never looks rigid. Physics are unaffected.
    /// </summary>
    public class CarBodyTilt : MonoBehaviour
    {
        public ArcadeCarController car;
        [Tooltip("Transform holding the body meshes (NOT the wheels).")]
        public Transform body;
        public float rollPerG = 2.6f;
        public float pitchPerG = 1.8f;
        public float maxAngle = 4.5f;
        public float smoothing = 6f;

        Quaternion baseRot;
        float lastFwd, roll, pitch;

        void Start()
        {
            if (!car) car = GetComponentInParent<ArcadeCarController>();
            if (body) baseRot = body.localRotation;
        }

        void FixedUpdate()
        {
            if (!car || !body) return;
            float fwd = car.ForwardSpeed;
            float longG = (fwd - lastFwd) / Time.fixedDeltaTime / 9.81f;
            lastFwd = fwd;
            float latG = car.LateralAcceleration / 9.81f;
            float targetRoll = Mathf.Clamp(latG * rollPerG, -maxAngle, maxAngle);
            float targetPitch = Mathf.Clamp(-longG * pitchPerG, -maxAngle, maxAngle);
            float k = 1f - Mathf.Exp(-smoothing * Time.fixedDeltaTime);
            roll = Mathf.Lerp(roll, targetRoll, k);
            pitch = Mathf.Lerp(pitch, targetPitch, k);
        }

        void LateUpdate()
        {
            if (!body) return;
            body.localRotation = Quaternion.Euler(pitch, 0f, roll) * baseRot;
        }
    }
}
