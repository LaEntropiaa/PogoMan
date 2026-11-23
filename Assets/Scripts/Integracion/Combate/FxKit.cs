using System.Collections;
using UnityEngine;

// Efectos "sólidos" para los ataques de jefes (no solo luces): mallas que crecen y se deshacen,
// rocas que saltan del suelo y ondas en el piso. Pocas partículas: casi todo son mallas simples.
public class FxKit : MonoBehaviour
{
    private static FxKit runner;
    private static FxKit Runner
    {
        get
        {
            if (runner == null) runner = new GameObject("FxKit").AddComponent<FxKit>();
            return runner;
        }
    }

    private static GameObject Prim(PrimitiveType type, Vector3 pos, Vector3 scale, Quaternion rot, Color c, float glow)
    {
        GameObject g = GameObject.CreatePrimitive(type);
        Destroy(g.GetComponent<Collider>());
        g.transform.SetPositionAndRotation(pos, rot);
        g.transform.localScale = scale;
        ItemFx.Paint(g, c, glow);
        return g;
    }

    // Translúcido (sin luz): el fuego deja ver lo que hay detrás y no tapa la pantalla.
    private static readonly System.Collections.Generic.Dictionary<Color, Material> glass = new System.Collections.Generic.Dictionary<Color, Material>();
    private static GameObject Glow(Vector3 pos, Color c)
    {
        GameObject g = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Destroy(g.GetComponent<Collider>());
        g.transform.position = pos;
        g.transform.localScale = Vector3.zero;
        var r = g.GetComponent<Renderer>();
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        if (Telegraph.SharedMaterial != null)
        {
            if (!glass.TryGetValue(c, out Material m) || m == null)
            {
                m = new Material(Telegraph.SharedMaterial);
                m.SetColor("_BaseColor", c);
                glass[c] = m;
            }
            r.sharedMaterial = m;
        }
        else ItemFx.Paint(g, c, 2f);
        return g;
    }

    // Columna de fuego: tres capas (núcleo claro, medio naranja, exterior rojo) que brotan, ondulan y se encogen.
    public static void FlameColumn(Vector3 ground, float radius, float height, Color outer, Color mid, Color core)
    {
        Runner.StartCoroutine(FlameRoutine(ground, radius, height, outer, mid, core));
    }

    private static IEnumerator FlameRoutine(Vector3 ground, float radius, float height, Color outer, Color mid, Color core)
    {
        var layers = new[]
        {
            Glow(ground, new Color(outer.r, outer.g, outer.b, 0.35f)),
            Glow(ground, new Color(mid.r, mid.g, mid.b, 0.45f)),
            Glow(ground, new Color(core.r, core.g, core.b, 0.6f)),
        };
        float[] widths = { 0.8f, 0.55f, 0.3f };
        float[] heights = { 1f, 1.12f, 1.22f };
        Crack(ground, radius, mid);
        const float life = 0.75f;
        for (float t = 0f; t < life; t += Time.deltaTime)
        {
            float k = t / life;
            float grow = k < 0.18f ? k / 0.18f : 1f - Mathf.Pow((k - 0.18f) / 0.82f, 2f);
            for (int i = 0; i < layers.Length; i++)
            {
                if (layers[i] == null) continue;
                float wob = 1f + Mathf.Sin(t * 30f + i * 2f) * 0.08f;
                float w = radius * 2f * widths[i] * Mathf.Lerp(1f, 0.4f, k) * wob;
                float h = height * heights[i] * grow;
                layers[i].transform.localScale = new Vector3(w, h * 0.5f, w);
                layers[i].transform.position = ground + Vector3.up * h * 0.5f;
                layers[i].transform.rotation = Quaternion.Euler(0f, t * 200f * (i % 2 == 0 ? 1f : -1f), 0f);
            }
            yield return null;
        }
        foreach (GameObject g in layers) if (g != null) Destroy(g);
    }

    // Grietas en el piso y rocas que saltan.
    public static void Crack(Vector3 ground, float radius, Color glow)
    {
        Runner.StartCoroutine(CrackRoutine(ground, radius, glow));
    }

    private static IEnumerator CrackRoutine(Vector3 ground, float radius, Color glow)
    {
        int n = 6;
        var cracks = new GameObject[n];
        for (int i = 0; i < n; i++)
        {
            float a = (i + Random.value * 0.5f) / n * 360f;
            float len = radius * Random.Range(0.7f, 1.2f);
            Quaternion rot = Quaternion.Euler(0f, a, 0f);
            cracks[i] = Prim(PrimitiveType.Cube, ground + rot * Vector3.forward * len * 0.5f + Vector3.up * 0.04f,
                new Vector3(0.18f, 0.06f, len), rot, glow, 2f);
        }
        var rocks = new GameObject[5];
        var vel = new Vector3[5];
        for (int i = 0; i < rocks.Length; i++)
        {
            float s = Random.Range(0.2f, 0.45f) * Mathf.Clamp(radius / 2f, 0.6f, 2f);
            rocks[i] = Prim(PrimitiveType.Cube, ground + Random.insideUnitSphere * radius * 0.5f + Vector3.up * 0.2f,
                Vector3.one * s, Random.rotation, new Color(0.22f, 0.14f, 0.13f), 0.05f);
            vel[i] = new Vector3(Random.Range(-1f, 1f), Random.Range(1.5f, 2.6f), Random.Range(-1f, 1f)) * 3.5f;
        }
        for (float t = 0f; t < 0.9f; t += Time.deltaTime)
        {
            float k = t / 0.9f;
            for (int i = 0; i < rocks.Length; i++)
            {
                if (rocks[i] == null) continue;
                vel[i] += Physics.gravity * Time.deltaTime;
                rocks[i].transform.position += vel[i] * Time.deltaTime;
                rocks[i].transform.Rotate(300f * Time.deltaTime, 200f * Time.deltaTime, 0f);
            }
            foreach (GameObject c in cracks)
                if (c != null) c.transform.localScale = new Vector3(0.18f * (1f - k), 0.06f, c.transform.localScale.z);
            yield return null;
        }
        foreach (GameObject g in cracks) if (g != null) Destroy(g);
        foreach (GameObject g in rocks) if (g != null) Destroy(g);
    }

    // Onda en el piso (solo visual).
    public static void Ring(Vector3 ground, float radius, Color inner, Color outer) =>
        ShockRing.SpawnVisual(ground + Vector3.up * 0.1f, radius, 0.4f, inner, outer);
}
