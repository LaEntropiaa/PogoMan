using System.Collections.Generic;
using UnityEngine;

// Islas flotantes del Estigia (Isla_XX_N): las convierte en ROCAS FLOTANDO.
//  - Debajo de cada isla cuelga un pedazo de roca dentado (malla generada, sombreado plano).
//  - Textura de piedra en pixel art (estratos, grietas con brillo de lava) en la isla y su roca.
//  - Piedritas que flotan alrededor y polvo que se desmorona.
// Todo se genera al iniciar; el prefab del mapa no se toca.
public class FloatingRocks : MonoBehaviour
{
    private static Material rockMat, topMat;

    private struct Pebble { public Transform t; public Vector3 basePos; public float phase, speed; public Vector3 spin; }
    private readonly List<Pebble> pebbles = new List<Pebble>();

    public static void Decorate(MonoBehaviour host)
    {
        var islands = new List<Renderer>();
        foreach (Renderer r in host.GetComponentsInChildren<Renderer>(true))
            if (r.name.StartsWith("Isla_") && r is MeshRenderer)
                islands.Add(r);
        if (islands.Count == 0) return;
        EnsureMaterials(islands[0].sharedMaterial);
        int seed = 1;
        foreach (Renderer r in islands)
        {
            var fr = r.gameObject.AddComponent<FloatingRocks>();
            fr.Build(r, seed++);
        }
    }

    // ------------------------------------------------------------------ textura y materiales
    private static void EnsureMaterials(Material template)
    {
        if (rockMat != null) return;
        Texture2D tex = RockTexture(64, false);
        Texture2D emi = RockTexture(64, true);
        // Copia de un material del mapa (URP Lit ya incluido en el juego) con la textura de roca.
        rockMat = template != null ? new Material(template) : new Material(Shader.Find("Universal Render Pipeline/Lit"));
        rockMat.name = "RocaFlotante";
        rockMat.SetTexture("_DetailAlbedoMap", null);
        rockMat.SetTexture("_BaseMap", tex);
        rockMat.SetColor("_BaseColor", Color.white);
        rockMat.SetFloat("_Smoothness", 0f);
        rockMat.EnableKeyword("_EMISSION");
        rockMat.SetTexture("_EmissionMap", emi);
        rockMat.SetColor("_EmissionColor", new Color(1f, 0.3f, 0.05f) * 0.9f);
        rockMat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        topMat = new Material(rockMat) { name = "RocaFlotanteArriba" };
        topMat.SetTextureScale("_BaseMap", new Vector2(2f, 2f));
        topMat.SetColor("_BaseColor", new Color(1.05f, 0.95f, 0.9f));
        topMat.SetColor("_EmissionColor", new Color(1f, 0.3f, 0.05f) * 0.35f);
    }

    // Piedra en pixel art: estratos horizontales, manchas, bordes oscuros y grietas (la máscara de emisión).
    private static Texture2D RockTexture(int n, bool emission)
    {
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Repeat };
        var px = new Color[n * n];
        Color[] palette =
        {
            new Color(0.16f, 0.11f, 0.13f), new Color(0.24f, 0.16f, 0.17f), new Color(0.33f, 0.22f, 0.21f),
            new Color(0.42f, 0.29f, 0.25f), new Color(0.52f, 0.37f, 0.3f),
        };
        var rng = new System.Random(42);
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float u = x / (float)n, v = y / (float)n;
                float strata = Mathf.Sin(v * Mathf.PI * 2f * 5f + Mathf.Sin(u * Mathf.PI * 2f * 2f) * 1.2f) * 0.5f + 0.5f;
                float noise = Tile(u, v, 6f) * 0.6f + Tile(u, v, 14f) * 0.4f;
                float h = strata * 0.45f + noise * 0.55f;
                int idx = Mathf.Clamp(Mathf.FloorToInt(h * palette.Length), 0, palette.Length - 1);
                Color c = palette[idx];
                // Grietas: líneas finas donde el ruido cruza un umbral.
                float crack = Mathf.Abs(Tile(u + 0.31f, v + 0.17f, 5f) - 0.5f);
                bool isCrack = crack < 0.014f;
                if (isCrack) c = new Color(0.08f, 0.04f, 0.05f);
                if (rng.NextDouble() < 0.03) c *= 1.25f;   // brillitos de mineral
                px[y * n + x] = emission ? (isCrack && Tile(u, v, 3f) > 0.58f ? Color.white : Color.black) : c;
            }
        tex.SetPixels(px);
        tex.Apply();
        return tex;
    }

    // Ruido que se repite sin costura (para que la textura haga mosaico).
    private static float Tile(float u, float v, float freq)
    {
        float a = u * Mathf.PI * 2f, b = v * Mathf.PI * 2f;
        float x = Mathf.Cos(a) * freq / 6.28f, y = Mathf.Sin(a) * freq / 6.28f;
        float z = Mathf.Cos(b) * freq / 6.28f, w = Mathf.Sin(b) * freq / 6.28f;
        return (Mathf.PerlinNoise(x + 10f + z * 1.7f, y + 20f + w * 1.3f) + Mathf.PerlinNoise(z + 30f + x * 0.9f, w + 40f + y * 1.1f)) * 0.5f;
    }

    // ------------------------------------------------------------------ roca colgante
    private void Build(Renderer island, int seed)
    {
        Bounds b = island.bounds;
        float radius = Mathf.Max(b.extents.x, b.extents.z);
        if (radius < 0.4f) return;

        // La isla también se ve de piedra.
        var mats = island.sharedMaterials;
        for (int i = 0; i < mats.Length; i++) mats[i] = topMat;
        island.sharedMaterials = mats;

        var rng = new System.Random(seed * 7919);
        float R() => (float)rng.NextDouble();
        float depth = radius * (1.1f + R() * 0.7f);
        Vector3 top = new Vector3(b.center.x, b.min.y + Mathf.Min(0.25f, b.size.y * 0.5f), b.center.z);

        var go = new GameObject("RocaColgante");
        go.transform.position = top;
        Mesh mesh = RockMesh(radius * 1.02f, depth, rng);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = rockMat;
        go.transform.SetParent(island.transform, true);   // si la isla se mueve, la roca va con ella

        // Piedritas flotando alrededor de la roca.
        int count = 3 + rng.Next(0, 4);
        for (int i = 0; i < count; i++)
        {
            var p = new GameObject("Piedrita");
            float a = R() * Mathf.PI * 2f;
            float dist = radius * (1.1f + R() * 0.6f);
            p.transform.position = top + new Vector3(Mathf.Cos(a) * dist, -depth * (0.2f + R() * 0.6f), Mathf.Sin(a) * dist);
            p.AddComponent<MeshFilter>().sharedMesh = RockMesh(0.15f + R() * radius * 0.12f, 0.3f + R() * radius * 0.2f, rng, true);
            p.AddComponent<MeshRenderer>().sharedMaterial = rockMat;
            p.transform.SetParent(go.transform, true);
            pebbles.Add(new Pebble
            {
                t = p.transform, basePos = p.transform.localPosition, phase = R() * 6f, speed = 0.6f + R() * 0.6f,
                spin = new Vector3(R() - 0.5f, R() - 0.5f, R() - 0.5f) * 30f,
            });
        }

        // Polvo/piedrita que se desmorona de la punta.
        var ps = new GameObject("Desmorona").AddComponent<ParticleSystem>();
        ps.transform.SetParent(go.transform, false);
        ps.transform.position = top + Vector3.down * depth * 0.9f;
        ps.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.startLifetime = 2.5f;
        main.startSpeed = 0.3f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.14f);
        main.startColor = new Color(0.45f, 0.32f, 0.28f, 0.9f);
        main.gravityModifier = 0.25f;
        main.maxParticles = 30;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        var em = ps.emission;
        em.rateOverTime = 2.5f;
        var sh = ps.shape;
        sh.shapeType = ParticleSystemShapeType.Circle;
        sh.radius = radius * 0.3f;
        ItemFx.FadeOut(ps);
        var pr = ps.GetComponent<ParticleSystemRenderer>();
        pr.sharedMaterial = rockMat;
        pr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        ps.Play();
    }

    // Cono irregular hacia abajo (o piedra chica si "pebble"): anillos con ruido, sombreado plano.
    private static Mesh RockMesh(float radius, float depth, System.Random rng, bool pebble = false)
    {
        int seg = pebble ? 7 : 14;
        int rings = pebble ? 3 : 6;
        var rim = new Vector3[rings + 1, seg];
        for (int k = 0; k <= rings; k++)
        {
            float t = k / (float)rings;
            // Se angosta hacia la punta (con una panza a la mitad), y la punta se ladea un poco.
            float r = radius * (pebble ? Mathf.Sin(Mathf.Lerp(0.15f, Mathf.PI - 0.15f, t)) : Mathf.Pow(1f - t, 0.8f) * (1f + 0.15f * Mathf.Sin(t * Mathf.PI)));
            float y = pebble ? -t * depth : -Mathf.Pow(t, 0.9f) * depth;
            Vector3 lean = pebble ? Vector3.zero : new Vector3((float)rng.NextDouble() - 0.5f, 0f, (float)rng.NextDouble() - 0.5f) * radius * 0.25f * t;
            for (int i = 0; i < seg; i++)
            {
                float a = (i + (float)rng.NextDouble() * 0.35f) / seg * Mathf.PI * 2f;
                float jag = 1f + ((float)rng.NextDouble() - 0.5f) * (pebble ? 0.5f : 0.45f);
                rim[k, i] = new Vector3(Mathf.Cos(a) * r * jag, y + ((float)rng.NextDouble() - 0.5f) * depth * 0.06f, Mathf.Sin(a) * r * jag) + lean;
            }
        }
        var verts = new List<Vector3>();
        var uvs = new List<Vector2>();
        var tris = new List<int>();
        void Tri(Vector3 a, Vector3 b, Vector3 c, Vector2 ua, Vector2 ub, Vector2 uc)
        {
            int s = verts.Count;
            verts.Add(a); verts.Add(b); verts.Add(c);
            uvs.Add(ua); uvs.Add(ub); uvs.Add(uc);
            tris.Add(s); tris.Add(s + 1); tris.Add(s + 2);
        }
        float uScale = Mathf.Max(1f, Mathf.Round(radius * 2f));
        float vScale = Mathf.Max(1f, depth * 0.5f);
        for (int k = 0; k < rings; k++)
            for (int i = 0; i < seg; i++)
            {
                int j = (i + 1) % seg;
                Vector2 uv(int kk, int ii) => new Vector2(ii / (float)seg * uScale, -kk / (float)rings * vScale);
                Vector3 a = rim[k, i], bb = rim[k, j], c = rim[k + 1, i], d = rim[k + 1, j];
                Tri(a, bb, c, uv(k, i), uv(k, i + 1), uv(k + 1, i));
                Tri(bb, d, c, uv(k, i + 1), uv(k + 1, i + 1), uv(k + 1, i));
            }
        // Punta (y tapa de arriba en las piedritas).
        Vector3 tip = Vector3.zero;
        for (int i = 0; i < seg; i++) tip += rim[rings, i];
        tip = tip / seg + Vector3.down * depth * (pebble ? 0.05f : 0.18f);
        for (int i = 0; i < seg; i++)
            Tri(rim[rings, i], rim[rings, (i + 1) % seg], tip, new Vector2(i / (float)seg, 0f), new Vector2((i + 1f) / seg, 0f), new Vector2(0.5f, -0.3f));
        Vector3 cap = Vector3.zero;
        for (int i = 0; i < seg; i++) cap += rim[0, i];
        cap /= seg;
        if (pebble) cap += Vector3.up * depth * 0.1f;
        for (int i = 0; i < seg; i++)
            Tri(rim[0, (i + 1) % seg], rim[0, i], cap, new Vector2(0f, 0f), new Vector2(0.3f, 0f), new Vector2(0.15f, 0.3f));

        var mesh = new Mesh { name = pebble ? "Piedrita" : "RocaColgante" };
        mesh.SetVertices(verts);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();   // vértices separados = sombreado plano (low poly)
        mesh.RecalculateBounds();
        return mesh;
    }

    private void Update()
    {
        float t = Time.time;
        foreach (Pebble p in pebbles)
        {
            if (p.t == null) continue;
            p.t.localPosition = p.basePos + Vector3.up * Mathf.Sin(t * p.speed + p.phase) * 0.25f;
            p.t.Rotate(p.spin * Time.deltaTime, Space.Self);
        }
    }
}
