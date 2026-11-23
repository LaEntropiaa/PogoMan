using System.Collections;
using UnityEngine;

// Efectos de los ataques especiales (guardianes y Diablo), hechos con partículas de verdad:
// fuego que sube y cambia de color, humo, chispas que caen, polvo, magia y agua, todos con una
// textura suave (no cuadros). Cada tipo es UN sistema compartido (se emite, no se crean objetos),
// así se ven ricos sin tumbar el FPS.
public static class AttackFx
{
    private enum Kind { Fire, Smoke, Sparks, Dust, Magic, Scorch }

    private static readonly ParticleSystem[] systems = new ParticleSystem[6];
    private static Texture2D soft;
    private static Material additive, alpha;

    // Textura redonda y suave (con escalones: sigue viéndose pixel).
    private static Texture2D Soft()
    {
        if (soft != null) return soft;
        const int n = 32;
        soft = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        var px = new Color[n * n];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float dx = (x + 0.5f) / n * 2f - 1f, dy = (y + 0.5f) / n * 2f - 1f;
                float a = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                a = Mathf.Floor(a * a * 6f) / 5f;
                px[y * n + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(a));
            }
        soft.SetPixels(px);
        soft.Apply();
        return soft;
    }

    private static Material Mat(bool add)
    {
        Material baseMat = ItemEffects.Instance != null ? ItemEffects.Instance.FxMaterial : null;
        if (baseMat == null) return null;
        if (add && additive != null) return additive;
        if (!add && alpha != null) return alpha;
        var m = new Material(baseMat) { name = add ? "AtaqueAditivo" : "AtaqueAlfa" };
        m.SetTexture("_BaseMap", Soft());
        m.SetColor("_BaseColor", Color.white);
        if (!add)
        {
            // Mezcla normal (el humo y el polvo tapan; no brillan).
            m.SetFloat("_Blend", 0f);
            m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.renderQueue = 3000;
        }
        if (add) additive = m; else alpha = m;
        return m;
    }

    // Material aditivo con la textura suave (para estelas de bolas de fuego, etc.).
    public static Material SoftAdditive => Mat(true);

    private static ParticleSystem Sys(Kind k)
    {
        ParticleSystem ps = systems[(int)k];
        if (ps != null) return ps;
        var go = new GameObject("AtaqueFx_" + k);
        ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.loop = true;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = k == Kind.Fire ? 2500 : 1500;
        main.startSpeed = 0f;
        var em = ps.emission; em.rateOverTime = 0f;
        var shape = ps.shape; shape.enabled = false;
        var r = ps.GetComponent<ParticleSystemRenderer>();
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        bool add = k == Kind.Fire || k == Kind.Sparks || k == Kind.Magic;
        r.sharedMaterial = Mat(add);

        var col = ps.colorOverLifetime;
        col.enabled = true;
        var size = ps.sizeOverLifetime;
        size.enabled = true;
        switch (k)
        {
            case Kind.Fire:
                // Blanco → amarillo → naranja → rojo oscuro → nada. Crece y luego se encoge.
                col.color = new Gradient
                {
                    colorKeys = new[] { new GradientColorKey(new Color(1f, 1f, 0.85f), 0f), new GradientColorKey(new Color(1f, 0.8f, 0.2f), 0.2f),
                                        new GradientColorKey(new Color(1f, 0.38f, 0.05f), 0.5f), new GradientColorKey(new Color(0.45f, 0.04f, 0.02f), 1f) },
                    alphaKeys = new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.9f, 0.55f), new GradientAlphaKey(0f, 1f) },
                };
                size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.5f), new Keyframe(0.25f, 1f), new Keyframe(1f, 0.2f)));
                main.gravityModifier = -0.25f;
                var noise = ps.noise; noise.enabled = true; noise.strength = 0.8f; noise.frequency = 1.2f;
                break;
            case Kind.Smoke:
                col.color = new Gradient
                {
                    colorKeys = new[] { new GradientColorKey(new Color(0.25f, 0.2f, 0.2f), 0f), new GradientColorKey(new Color(0.12f, 0.1f, 0.1f), 1f) },
                    alphaKeys = new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.55f, 0.2f), new GradientAlphaKey(0f, 1f) },
                };
                size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.5f), new Keyframe(1f, 1.8f)));
                main.gravityModifier = -0.08f;
                break;
            case Kind.Sparks:
                col.color = new Gradient
                {
                    colorKeys = new[] { new GradientColorKey(new Color(1f, 0.9f, 0.55f), 0f), new GradientColorKey(new Color(1f, 0.6f, 0.15f), 0.4f), new GradientColorKey(new Color(1f, 0.25f, 0.05f), 1f) },
                    alphaKeys = new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) },
                };
                size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0.3f)));
                main.gravityModifier = 1.1f;
                r.renderMode = ParticleSystemRenderMode.Stretch;
                r.velocityScale = 0.03f;
                r.lengthScale = 1.2f;
                break;
            case Kind.Dust:
                col.color = new Gradient
                {
                    colorKeys = new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                    alphaKeys = new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.7f, 0.15f), new GradientAlphaKey(0f, 1f) },
                };
                size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.6f), new Keyframe(1f, 1.6f)));
                var lv = ps.limitVelocityOverLifetime; lv.enabled = true; lv.dampen = 0.15f; lv.limit = 0.5f;
                break;
            case Kind.Magic:
                col.color = new Gradient
                {
                    colorKeys = new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                    alphaKeys = new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.8f, 0.5f), new GradientAlphaKey(0f, 1f) },
                };
                size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0.1f)));
                main.gravityModifier = -0.15f;
                var n2 = ps.noise; n2.enabled = true; n2.strength = 1.2f; n2.frequency = 1.5f;
                break;
            case Kind.Scorch:
                // Marca quemada en el piso (se pone acostada y se desvanece).
                col.color = new Gradient
                {
                    colorKeys = new[] { new GradientColorKey(new Color(1f, 0.45f, 0.1f), 0f), new GradientColorKey(new Color(0.05f, 0.03f, 0.03f), 0.15f), new GradientColorKey(new Color(0.05f, 0.03f, 0.03f), 1f) },
                    alphaKeys = new[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0.75f, 0.7f), new GradientAlphaKey(0f, 1f) },
                };
                size.enabled = false;
                r.renderMode = ParticleSystemRenderMode.HorizontalBillboard;
                r.sharedMaterial = Mat(false);
                break;
        }
        ps.Play();
        systems[(int)k] = ps;
        return ps;
    }

    private static ParticleSystem.EmitParams ep;

    private static void Emit(Kind k, Vector3 pos, Vector3 vel, float size, float life, Color color)
    {
        ParticleSystem ps = Sys(k);
        ep.position = pos;
        ep.velocity = vel;
        ep.startSize = size;
        ep.startLifetime = life;
        ep.startColor = color;
        ep.applyShapeToPosition = false;
        ps.Emit(ep, 1);
    }

    private static float Budget => EndlessMode.Active ? 0.5f : 1f;
    private static int N(float n) => Mathf.Max(1, Mathf.RoundToInt(n * Budget));

    // ---------------------------------------------------------------- efectos
    // Erupción de fuego: columna que sube, chispas que caen, humo arriba y marca quemada.
    public static void Eruption(Vector3 ground, float radius, float height)
    {
        // Columna: llamas que suben rápido (más finas arriba), de amarillo a rojo.
        float upSpeed = height * 1.3f;
        Color tone = new Color(1f, 0.72f, 0.42f, 0.8f);
        for (int i = 0, n = N(28 + radius * 9); i < n; i++)
        {
            Vector2 c = Random.insideUnitCircle * radius * 0.55f;
            float k = Random.value;
            Emit(Kind.Fire, ground + new Vector3(c.x, Random.Range(0f, 0.4f), c.y),
                new Vector3(c.x * 0.35f, Mathf.Lerp(0.45f, 1.05f, k) * upSpeed, c.y * 0.35f),
                Mathf.Lerp(1.1f, 0.45f, k) * Mathf.Clamp(radius, 0.7f, 1.8f), Mathf.Lerp(0.55f, 0.95f, k), tone);
        }
        for (int i = 0, n = N(22 + radius * 6); i < n; i++)
            Emit(Kind.Sparks, ground + Vector3.up * Random.Range(0.5f, height * 0.6f),
                Random.onUnitSphere * Random.Range(4f, 10f) + Vector3.up * 6f, Random.Range(0.08f, 0.16f), Random.Range(0.6f, 1.1f), Color.white);
        for (int i = 0, n = N(8 + radius * 3); i < n; i++)
        {
            Vector2 c = Random.insideUnitCircle * radius;
            Emit(Kind.Smoke, ground + new Vector3(c.x, height * Random.Range(0.4f, 0.9f), c.y),
                Vector3.up * Random.Range(1f, 2.5f) + new Vector3(c.x, 0f, c.y) * 0.3f, Random.Range(1.2f, 2.2f) * Mathf.Clamp(radius, 0.8f, 2f), Random.Range(1.4f, 2.4f), Color.white);
        }
        Emit(Kind.Scorch, ground + Vector3.up * 0.05f, Vector3.zero, radius * 2.2f, 2.5f, Color.white);
        Flash(ground + Vector3.up * 2f, new Color(1f, 0.5f, 0.15f), 6f + radius * 2f, 0.35f);
    }

    // Golpe al piso: anillo de polvo que sale de lado, piedritas, chispas y marca.
    public static void GroundSlam(Vector3 ground, float radius, Color dustColor, Color sparkColor)
    {
        for (int i = 0, n = N(28 + radius * 10); i < n; i++)
        {
            float a = Random.value * Mathf.PI * 2f;
            Vector3 dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            Emit(Kind.Dust, ground + dir * radius * 0.3f + Vector3.up * 0.3f,
                dir * Random.Range(3f, 6f) * Mathf.Clamp(radius, 1f, 3f) + Vector3.up * Random.Range(0.3f, 1.5f),
                Random.Range(0.8f, 1.6f), Random.Range(0.6f, 1.1f), dustColor);
        }
        for (int i = 0, n = N(18); i < n; i++)
            Emit(Kind.Sparks, ground + Vector3.up * 0.3f, Random.onUnitSphere * 7f + Vector3.up * 5f, 0.1f, 0.7f, sparkColor);
        Emit(Kind.Scorch, ground + Vector3.up * 0.05f, Vector3.zero, radius * 1.8f, 1.8f, Color.white);
    }

    // Estallido mágico (almas, telarañas, monedas, agua): destello, aro de partículas y chispas del color dado.
    public static void Magic(Vector3 at, float radius, Color color)
    {
        Color bright = Color.Lerp(color, Color.white, 0.5f);
        for (int i = 0, n = N(30 + radius * 10); i < n; i++)
        {
            Vector3 dir = Random.onUnitSphere;
            Emit(Kind.Magic, at + dir * radius * 0.2f, dir * Random.Range(2f, 5f) * Mathf.Clamp(radius, 0.8f, 2.5f),
                Random.Range(0.3f, 0.7f), Random.Range(0.5f, 0.9f), i % 3 == 0 ? bright : color);
        }
        for (int i = 0, n = N(14); i < n; i++)
            Emit(Kind.Sparks, at, Random.onUnitSphere * 8f + Vector3.up * 3f, 0.08f, 0.6f, bright);
        Flash(at, color, 5f + radius * 2f, 0.25f);
    }

    // Llamitas que brotan (para efectos continuos: líneas, rayos, onda de fuego).
    public static void Flicker(Vector3 at, float spread, Color tint)
    {
        Vector2 c = Random.insideUnitCircle * spread;
        Emit(Kind.Fire, at + new Vector3(c.x, 0.1f, c.y), Vector3.up * Random.Range(3f, 6f), Random.Range(0.6f, 1.2f), Random.Range(0.35f, 0.6f), tint);
        if (Random.value < 0.3f) Emit(Kind.Sparks, at, Random.onUnitSphere * 4f + Vector3.up * 4f, 0.08f, 0.5f, tint);
    }

    // Partícula mágica suelta (para estelas y líneas de color).
    public static void Wisp(Vector3 at, Color color, float size = 0.5f)
    {
        Emit(Kind.Magic, at + Random.insideUnitSphere * 0.3f, Random.insideUnitSphere * 1.5f + Vector3.up * 1.5f, size, 0.6f, color);
    }

    // Luz que destella un instante.
    private static void Flash(Vector3 at, Color color, float range, float seconds)
    {
        var go = new GameObject("DestelloAtaque");
        go.transform.position = at;
        var l = go.AddComponent<Light>();
        l.type = LightType.Point;
        l.color = color;
        l.range = range;
        l.intensity = 6f;
        l.shadows = LightShadows.None;
        go.AddComponent<FlashFade>().life = seconds;
    }
}

public class FlashFade : MonoBehaviour
{
    public float life = 0.3f;
    private float age;
    private Light l;
    private void Awake() => l = GetComponent<Light>();
    private void Update()
    {
        age += Time.deltaTime;
        if (l != null) l.intensity = 6f * (1f - age / life);
        if (age >= life) Destroy(gameObject);
    }
}
