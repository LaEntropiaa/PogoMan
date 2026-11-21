using UnityEngine;

// Señales en el suelo de los ataques de los guardianes: abanico, anillo, franja y disco.
// Se dibujan pegadas al terreno (cada vértice busca el suelo) con un color translúcido
// que se va llenando conforme se acerca el golpe. El borde es más brillante.
public class Telegraph : MonoBehaviour
{
    public static Material SharedMaterial;   // URP Unlit transparente (lo pone GuardianKit)

    private enum Shape { Fan, Ring, Strip, Disc }

    private Shape shape;
    private Vector3 center;
    private Vector3 forward = Vector3.forward;
    private float angle = 360f, rMin, rMax, length, width;
    private float fill = 1f;            // 0..1: qué parte se dibuja (preparación)
    private float sweep = 1f;           // para abanicos: qué fracción del arco se muestra
    private bool sweepFromLeft = true;
    private Color color = new Color(1f, 0.55f, 0.1f, 0.4f);
    private Mesh mesh;
    private MeshRenderer mr;
    private MaterialPropertyBlock block;
    private bool dirty = true;
    private int segments = 32;

    private static Telegraph Make(string name)
    {
        var go = new GameObject(name);
        var t = go.AddComponent<Telegraph>();
        t.mesh = new Mesh { name = name };
        t.mesh.MarkDynamic();
        go.AddComponent<MeshFilter>().sharedMesh = t.mesh;
        t.mr = go.AddComponent<MeshRenderer>();
        t.mr.sharedMaterial = SharedMaterial;
        t.mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        t.mr.receiveShadows = false;
        t.block = new MaterialPropertyBlock();
        return t;
    }

    public static Telegraph Fan(Vector3 center, Vector3 forward, float angleDeg, float rMin, float rMax)
    {
        var t = Make("AvisoAbanico");
        t.shape = Shape.Fan; t.center = center; t.forward = Flat(forward); t.angle = angleDeg; t.rMin = rMin; t.rMax = rMax;
        t.segments = Mathf.Max(8, Mathf.CeilToInt(angleDeg / 6f));
        return t;
    }

    public static Telegraph Ring(Vector3 center, float rMin, float rMax)
    {
        var t = Make("AvisoAnillo");
        t.shape = Shape.Ring; t.center = center; t.rMin = rMin; t.rMax = rMax; t.segments = 48;
        return t;
    }

    public static Telegraph Strip(Vector3 origin, Vector3 dir, float length, float width)
    {
        var t = Make("AvisoFranja");
        t.shape = Shape.Strip; t.center = origin; t.forward = Flat(dir); t.length = length; t.width = width; t.segments = 14;
        return t;
    }

    public static Telegraph Disc(Vector3 center, float radius)
    {
        var t = Make("AvisoDisco");
        t.shape = Shape.Disc; t.center = center; t.rMin = 0f; t.rMax = radius; t.segments = 28;
        return t;
    }

    private static Vector3 Flat(Vector3 v) { v.y = 0f; return v.sqrMagnitude > 0.0001f ? v.normalized : Vector3.forward; }

    public Telegraph SetColor(Color c) { color = c; return this; }
    public void SetFill(float f) { fill = Mathf.Clamp01(f); }
    public void SetCenter(Vector3 c) { if ((c - center).sqrMagnitude > 0.0004f) { center = c; dirty = true; } }
    public void SetForward(Vector3 f) { f = Flat(f); if (Vector3.Angle(f, forward) > 0.2f) { forward = f; dirty = true; } }
    public void SetRadii(float a, float b) { if (!Mathf.Approximately(a, rMin) || !Mathf.Approximately(b, rMax)) { rMin = a; rMax = b; dirty = true; } }
    public void SetSweep(float s, bool fromLeft) { s = Mathf.Clamp01(s); if (!Mathf.Approximately(s, sweep) || fromLeft != sweepFromLeft) { sweep = s; sweepFromLeft = fromLeft; dirty = true; } }

    // Altura del suelo (ignora personajes) para que la señal se pegue al terreno.
    public static float GroundY(Vector3 p, float fallback)
    {
        var hits = Physics.RaycastAll(p + Vector3.up * 4f, Vector3.down, 12f, ~0, QueryTriggerInteraction.Ignore);
        float best = float.MaxValue, y = fallback;
        foreach (RaycastHit h in hits)
        {
            if (h.rigidbody != null) continue;
            if (h.distance < best) { best = h.distance; y = h.point.y; }
        }
        return y;
    }

    private Vector3 OnGround(Vector3 p) => new Vector3(p.x, GroundY(p, center.y) + 0.08f, p.z);

    private void Rebuild()
    {
        dirty = false;
        var verts = new System.Collections.Generic.List<Vector3>();
        var cols = new System.Collections.Generic.List<Color>();
        var tris = new System.Collections.Generic.List<int>();
        Color inner = new Color(1f, 1f, 1f, 0.55f), edge = new Color(1f, 1f, 1f, 1f);

        if (shape == Shape.Strip)
        {
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            for (int i = 0; i <= segments; i++)
            {
                float d = length * i / segments;
                Vector3 c = center + forward * d;
                verts.Add(OnGround(c - right * width * 0.5f) - transform.position); cols.Add(edge);
                verts.Add(OnGround(c + right * width * 0.5f) - transform.position); cols.Add(edge);
                if (i > 0) { int b = verts.Count - 4; tris.AddRange(new[] { b, b + 2, b + 1, b + 1, b + 2, b + 3 }); }
            }
        }
        else
        {
            float total = shape == Shape.Fan ? angle * sweep : 360f;
            float start = shape == Shape.Fan ? (sweepFromLeft ? -angle / 2f : angle / 2f - total) : 0f;
            int segs = Mathf.Max(3, Mathf.CeilToInt(segments * total / Mathf.Max(1f, shape == Shape.Fan ? angle : 360f)));
            float r0 = shape == Shape.Disc ? 0f : rMin;
            for (int i = 0; i <= segs; i++)
            {
                float a = (start + total * i / segs) * Mathf.Deg2Rad;
                Vector3 dir = shape == Shape.Fan
                    ? Quaternion.AngleAxis(a * Mathf.Rad2Deg, Vector3.up) * forward
                    : new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                verts.Add(OnGround(center + dir * Mathf.Max(0.01f, r0)) - transform.position); cols.Add(shape == Shape.Disc ? inner : edge);
                verts.Add(OnGround(center + dir * Mathf.Max(0.02f, rMax - 0.12f)) - transform.position); cols.Add(inner);
                verts.Add(OnGround(center + dir * rMax) - transform.position); cols.Add(edge);
                if (i > 0)
                {
                    int b = verts.Count - 6;
                    tris.AddRange(new[] { b, b + 3, b + 1, b + 1, b + 3, b + 4, b + 1, b + 4, b + 2, b + 2, b + 4, b + 5 });
                }
            }
        }
        mesh.Clear();
        mesh.SetVertices(verts);
        mesh.SetColors(cols);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateBounds();
    }

    private void LateUpdate()
    {
        if (dirty)
        {
            transform.position = center;
            Rebuild();
        }
        // Pulsa más rápido y se vuelve más opaco al acercarse el golpe.
        float pulse = 0.85f + 0.15f * Mathf.Sin(Time.time * (8f + fill * 18f));
        Color c = color;
        c.a = Mathf.Clamp01(color.a * (0.6f + 0.6f * fill) * pulse);
        block.SetColor("_BaseColor", c);
        mr.SetPropertyBlock(block);
    }

    private void OnDestroy()
    {
        if (mesh != null)
            Destroy(mesh);
    }
}
