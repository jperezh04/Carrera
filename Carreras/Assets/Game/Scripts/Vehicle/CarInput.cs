using UnityEngine;
using UnityEngine.InputSystem;

namespace StreetRacing.Vehicle
{
    /// <summary>
    /// Reads keyboard (and optional gamepad) input through the Input System package and exposes
    /// smoothed driving values. Other systems can override the input (e.g. automated tests) or
    /// lock it (countdown, pause, finish).
    /// </summary>
    public class CarInput : MonoBehaviour
    {
        [Tooltip("How fast the steering value moves towards the target (units/sec). Gives keyboard steering a progressive feel.")]
        public float steerResponse = 4.5f;
        [Tooltip("How fast steering re-centres when no key is pressed.")]
        public float steerReturn = 6f;
        [Tooltip("How fast throttle/brake ramp up (units/sec).")]
        public float pedalResponse = 6f;

        public float Throttle { get; private set; }
        public float Brake { get; private set; }
        public float Steer { get; private set; }
        public bool Handbrake { get; private set; }
        public bool ResetPressed { get; private set; }

        /// <summary>When true all driving values are zero (handbrake engaged).</summary>
        public bool Locked { get; set; }

        /// <summary>Optional external driver (used by the automated test bot).</summary>
        public System.Func<Vector4> externalOverride; // x=throttle, y=brake, z=steer, w=handbrake

        void Update()
        {
            float tTarget = 0f, bTarget = 0f, sTarget = 0f; bool hb = false; bool reset = false;

            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.wKey.isPressed || kb.upArrowKey.isPressed) tTarget = 1f;
                if (kb.sKey.isPressed || kb.downArrowKey.isPressed) bTarget = 1f;
                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) sTarget -= 1f;
                if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) sTarget += 1f;
                hb = kb.spaceKey.isPressed;
                reset = kb.rKey.wasPressedThisFrame;
            }
            var gp = Gamepad.current;
            if (gp != null)
            {
                tTarget = Mathf.Max(tTarget, gp.rightTrigger.ReadValue());
                bTarget = Mathf.Max(bTarget, gp.leftTrigger.ReadValue());
                float stick = gp.leftStick.ReadValue().x;
                if (Mathf.Abs(stick) > 0.1f) sTarget = stick;
                hb |= gp.buttonSouth.isPressed;
                reset |= gp.buttonNorth.wasPressedThisFrame;
            }

            if (externalOverride != null)
            {
                Vector4 o = externalOverride();
                tTarget = o.x; bTarget = o.y; sTarget = o.z; hb = o.w > 0.5f;
            }

            if (Locked)
            {
                Throttle = 0f; Brake = 0f; Steer = Mathf.MoveTowards(Steer, 0f, steerReturn * Time.unscaledDeltaTime);
                Handbrake = true; ResetPressed = false;
                return;
            }

            float dt = Time.deltaTime;
            Throttle = Mathf.MoveTowards(Throttle, tTarget, pedalResponse * dt);
            Brake = Mathf.MoveTowards(Brake, bTarget, pedalResponse * 1.5f * dt);
            float rate = (Mathf.Abs(sTarget) < 0.01f || Mathf.Sign(sTarget) != Mathf.Sign(Steer)) ? steerReturn : steerResponse;
            Steer = externalOverride != null ? sTarget : Mathf.MoveTowards(Steer, sTarget, rate * dt);
            Handbrake = hb;
            ResetPressed = reset;
        }
    }
}
