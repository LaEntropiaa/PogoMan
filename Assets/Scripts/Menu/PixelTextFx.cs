using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Estilo para la fuente pixel: degradado vertical (tipo fuego) + sombra "extruida" en escalones
// de pixel (letras en 3D estilo arcade) + un brillo que pasa de vez en cuando.
// No cambia la fuente: solo los vértices, así se conserva el pixel.
[RequireComponent(typeof(Graphic))]
public class PixelTextFx : BaseMeshEffect
{
    public Color top = new Color(1f, 1f, 0.85f);
    public Color bottom = new Color(1f, 0.5f, 0.2f);
    [Tooltip("Capas de la sombra extruida (cada una a 'step' pixeles).")]
    public int depth = 3;
    public float step = 2f;
    public Color extrudeFront = new Color(0.45f, 0.06f, 0.02f, 1f);
    public Color extrudeBack = new Color(0.08f, 0.01f, 0.01f, 1f);
    [Tooltip("Brillo que cruza el texto cada tantos segundos (0 = nunca).")]
    public float shineEvery = 0f;
    public float shineOffset;

    private static readonly List<UIVertex> src = new List<UIVertex>();
    private static readonly List<UIVertex> dst = new List<UIVertex>();

    private void Update()
    {
        if (shineEvery > 0f && graphic != null)
            graphic.SetVerticesDirty();   // para animar el brillo
    }

    public override void ModifyMesh(VertexHelper vh)
    {
        if (!IsActive() || vh.currentVertCount == 0)
            return;
        src.Clear();
        vh.GetUIVertexStream(src);

        float minY = float.MaxValue, maxY = float.MinValue, minX = float.MaxValue, maxX = float.MinValue;
        foreach (UIVertex v in src)
        {
            minY = Mathf.Min(minY, v.position.y); maxY = Mathf.Max(maxY, v.position.y);
            minX = Mathf.Min(minX, v.position.x); maxX = Mathf.Max(maxX, v.position.x);
        }
        float h = Mathf.Max(1f, maxY - minY), w = Mathf.Max(1f, maxX - minX);

        // Brillo: una franja diagonal que recorre el texto.
        float band = -10f;
        if (shineEvery > 0f)
        {
            float cyc = Mathf.Repeat(Time.unscaledTime + shineOffset, shineEvery);
            band = cyc / 0.6f;   // 0..1 durante 0.6 s
        }

        dst.Clear();
        // Sombra extruida: de la capa más lejana a la más cercana (las de atrás se dibujan primero).
        for (int k = depth; k >= 1; k--)
        {
            Color c = Color.Lerp(extrudeFront, extrudeBack, depth > 1 ? (k - 1f) / (depth - 1f) : 0f);
            foreach (UIVertex v in src)
            {
                UIVertex o = v;
                o.position += new Vector3(k * step * 0.5f, -k * step, 0f);
                Color32 baseC = v.color;
                o.color = new Color(c.r, c.g, c.b, c.a * baseC.a / 255f);
                dst.Add(o);
            }
        }
        foreach (UIVertex v in src)
        {
            UIVertex o = v;
            float t = (v.position.y - minY) / h;
            // Degradado en escalones (4 bandas): se ve pixel, no liso.
            t = Mathf.Floor(t * 4f) / 3f;
            Color g = Color.Lerp(bottom, top, Mathf.Clamp01(t));
            Color baseC = v.color;
            Color c = baseC * g;
            if (band >= 0f && band <= 1.3f)
            {
                float x = (v.position.x - minX) / w + (v.position.y - minY) / h * 0.25f;
                float d = Mathf.Abs(x - (band * 1.5f - 0.25f));
                c = Color.Lerp(c, Color.white, Mathf.Clamp01(1f - d / 0.12f) * 0.85f);
            }
            c.a = baseC.a;
            o.color = c;
            dst.Add(o);
        }
        vh.Clear();
        vh.AddUIVertexTriangleStream(dst);
    }
}
