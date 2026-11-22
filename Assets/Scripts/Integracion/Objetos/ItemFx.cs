using UnityEngine;

// Partículas sencillas para los efectos de los objetos (se arman desde código).
public static class ItemFx
{
    // Material URP de un color (las primitivas traen un material que en URP sale magenta).
    private static readonly System.Collections.Generic.Dictionary<Color, Material> colored = new System.Collections.Generic.Dictionary<Color, Material>();
    public static Material Colored(Color c, float glow = 0.3f)
    {
        if (colored.TryGetValue(c, out Material m) && m != null) return m;
        Material template = ItemEffects.Instance != null ? ItemEffects.Instance.FairyMaterial : null;
        if (template == null) return null;
        m = new Material(template);
        m.SetColor("_BaseColor", c);
        m.SetColor("_EmissionColor", c * glow);
        colored[c] = m;
        return m;
    }

    public static void Paint(GameObject go, Color c, float glow = 0.3f)
    {
        Material m = Colored(c, glow);
        foreach (Renderer r in go.GetComponentsInChildren<Renderer>())
        {
            if (m != null) r.sharedMaterial = m;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
    }

    // Ráfaga corta que se destruye sola.
    public static ParticleSystem Burst(Vector3 point, int count, float radius, Color color, Material material,
        float speed = 4f, float size = 0.22f, float lifetime = 0.45f)
    {
        var go = new GameObject("FxObjeto");
        go.transform.position = point;
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.duration = 0.2f;
        main.loop = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime * 0.6f, lifetime);
        main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.5f, speed);
        main.startSize = new ParticleSystem.MinMaxCurve(size * 0.5f, size);
        main.startColor = color;
        main.maxParticles = count;
        main.stopAction = ParticleSystemStopAction.Destroy;

        var emission = ps.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = radius;

        FadeOut(ps);
        Apply(ps, material);
        ps.Play();
        return ps;
    }

    public static void FadeOut(ParticleSystem ps)
    {
        var size = ps.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0f)));
    }

    public static void Apply(ParticleSystem ps, Material material)
    {
        var r = ps.GetComponent<ParticleSystemRenderer>();
        if (material != null)
            r.sharedMaterial = material;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
    }

    // Estela que sigue a un objeto (hada, monedas, proyectiles).
    public static TrailRenderer Trail(GameObject go, Color color, float width, float time, Material material)
    {
        var trail = go.AddComponent<TrailRenderer>();
        trail.time = time;
        trail.minVertexDistance = 0.05f;
        trail.widthCurve = new AnimationCurve(new Keyframe(0f, width), new Keyframe(1f, 0f));
        trail.colorGradient = new Gradient
        {
            colorKeys = new[] { new GradientColorKey(color, 0f), new GradientColorKey(color, 1f) },
            alphaKeys = new[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0f, 1f) },
        };
        trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        if (material != null)
            trail.sharedMaterial = material;
        return trail;
    }
}
