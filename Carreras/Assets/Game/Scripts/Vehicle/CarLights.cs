using System.Collections.Generic;
using UnityEngine;

namespace StreetRacing.Vehicle
{
    /// <summary>Headlights, tail lights (brighter when braking) and reverse light.</summary>
    public class CarLights : MonoBehaviour
    {
        public ArcadeCarController car;
        [Tooltip("Real-time spot lights for the headlights.")]
        public Light[] headlights;
        [Tooltip("Small red point light behind the car that flares when braking.")]
        public Light brakeGlow;
        public Light reverseLight;
        [Tooltip("Renderers that contain the tail-light material.")]
        public List<Renderer> tailLightRenderers = new List<Renderer>();
        [Tooltip("The emissive tail-light material. Only slots using it are modified.")]
        public Material tailLightMaterial;
        public Color tailColor = new Color(1f, 0.05f, 0.02f);
        public float tailIdleIntensity = 1.5f;
        public float tailBrakeIntensity = 7f;
        public bool headlightsOn = true;

        Material tailInstance;
        float brakeBlend;
        static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");

        void Awake()
        {
            if (!car) car = GetComponentInParent<ArcadeCarController>();
            if (tailLightMaterial)
            {
                tailInstance = new Material(tailLightMaterial) { name = tailLightMaterial.name + " (Runtime)" };
                tailInstance.EnableKeyword("_EMISSION");
                foreach (var r in tailLightRenderers)
                {
                    if (!r) continue;
                    var mats = r.sharedMaterials;
                    for (int i = 0; i < mats.Length; i++)
                        if (mats[i] == tailLightMaterial) mats[i] = tailInstance;
                    r.sharedMaterials = mats;
                }
            }
            SetHeadlights(headlightsOn);
        }

        public void SetHeadlights(bool on)
        {
            headlightsOn = on;
            if (headlights != null) foreach (var l in headlights) if (l) l.enabled = on;
        }

        void Update()
        {
            if (!car) return;
            bool braking = car.IsBraking || (car.Input && car.Input.Handbrake && car.SpeedKmh > 5f);
            brakeBlend = Mathf.MoveTowards(brakeBlend, braking ? 1f : 0f, Time.deltaTime * 12f);
            float intensity = Mathf.Lerp(tailIdleIntensity, tailBrakeIntensity, brakeBlend);
            Color c = tailColor * intensity;
            if (tailInstance) tailInstance.SetColor(EmissionColor, c);
            if (brakeGlow) brakeGlow.intensity = Mathf.Lerp(0.4f, 3f, brakeBlend);
            if (reverseLight) reverseLight.enabled = car.IsReversing && car.ForwardSpeed < -0.3f;
        }

        void OnDestroy()
        {
            if (tailInstance) Destroy(tailInstance);
        }
    }
}
