using System;
using UnityEngine;

namespace PogoMan.Estigia
{
    public class EstigiaEffects : MonoBehaviour
    {
        [Serializable]
        public class Motion
        {
            public Transform target;
            public bool soul;
            public float period, phase, amplitude;
            [NonSerialized] public Vector3 rest, scale, up;
            [NonSerialized] public int heightAxis;
        }

        public Motion[] movements = Array.Empty<Motion>();

        private void Awake()
        {
            foreach (var m in movements)
            {
                m.rest = m.target.localPosition;
                m.scale = m.target.localScale;
                m.up = m.target.parent.InverseTransformVector(transform.up);
                Vector3 axis = m.target.InverseTransformDirection(transform.up);
                m.heightAxis = Mathf.Abs(axis.x) > Mathf.Abs(axis.y) ? 0 : 1;
                if (Mathf.Abs(axis.z) > Mathf.Abs(axis[m.heightAxis])) m.heightAxis = 2;
            }
        }

        private void Update()
        {
            float seconds = Time.time;
            foreach (var m in movements)
            {
                float t = Mathf.Repeat(seconds / m.period + m.phase, 1f);
                if (m.soul)
                {
                    float envelope = Mathf.SmoothStep(0.02f, 1f, t / .17f)
                        * Mathf.SmoothStep(.02f, 1f, (1f - t) / .33f);
                    m.target.localPosition = m.rest + m.up * (m.amplitude * t);
                    m.target.localScale = m.scale * envelope;
                }
                else
                {
                    float crest = .5f - .5f * Mathf.Cos(t * Mathf.PI * 2f);
                    Vector3 scale = m.scale;
                    scale[m.heightAxis] *= .25f + .75f * crest * crest;
                    m.target.localScale = scale;
                }
            }
        }
    }
}
