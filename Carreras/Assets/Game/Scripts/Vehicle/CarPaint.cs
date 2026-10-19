using System.Collections.Generic;
using UnityEngine;

namespace StreetRacing.Vehicle
{
    /// <summary>
    /// Applies the body colour to an explicit, Inspector-editable list of renderer material slots.
    /// Only the listed slots are touched, so tyres, glass, lights and trim keep their look.
    /// One material instance is created per distinct source material, once (never per frame),
    /// so the original assets are never modified.
    /// </summary>
    public class CarPaint : MonoBehaviour
    {
        [System.Serializable]
        public class PaintSlot
        {
            public Renderer renderer;
            [Tooltip("Indices into renderer.sharedMaterials that are body paint.")]
            public int[] materialIndices = { 0 };
        }

        [Tooltip("Renderers/material slots that make up the car body paint.")]
        public List<PaintSlot> slots = new List<PaintSlot>();
        [Tooltip("Shader colour property (URP Lit uses _BaseColor).")]
        public string colorProperty = "_BaseColor";
        [Tooltip("Apply the colour chosen in the menu on Start.")]
        public bool applySavedColorOnStart = true;

        readonly Dictionary<Material, Material> instances = new Dictionary<Material, Material>();
        bool initialised;

        void Start()
        {
            if (applySavedColorOnStart) SetColor(Core.GameSettings.PaintColor);
        }

        void Init()
        {
            if (initialised) return;
            initialised = true;
            foreach (var slot in slots)
            {
                if (!slot.renderer) continue;
                var mats = slot.renderer.sharedMaterials;
                foreach (int i in slot.materialIndices)
                {
                    if (i < 0 || i >= mats.Length || !mats[i]) continue;
                    if (!instances.TryGetValue(mats[i], out var inst))
                    {
                        inst = new Material(mats[i]) { name = mats[i].name + " (Paint)" };
                        instances[mats[i]] = inst;
                        instances[inst] = inst; // already-instanced lookups map to themselves
                    }
                    mats[i] = inst;
                }
                slot.renderer.sharedMaterials = mats;
            }
        }

        public void SetColor(Color c)
        {
            if (!Application.isPlaying) return;
            Init();
            var done = new HashSet<Material>();
            foreach (var m in instances.Values)
            {
                if (!done.Add(m)) continue;
                if (m.HasProperty(colorProperty)) m.SetColor(colorProperty, c);
                else m.color = c;
            }
        }

        void OnDestroy()
        {
            var done = new HashSet<Material>();
            foreach (var m in instances.Values)
            {
                if (m && done.Add(m))
                {
                    if (Application.isPlaying) Destroy(m);
                    else DestroyImmediate(m);
                }
            }
        }
    }
}
