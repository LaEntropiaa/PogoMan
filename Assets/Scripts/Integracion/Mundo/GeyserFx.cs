using UnityEngine;

// Efectos de los géiseres de almas del Estigia (el lanzamiento lo hace EstigiaGeyser, esto es lo visual):
//  - Siempre: vapor que sube, burbujeo con gotitas, luz que late y un borboteo 3D.
//  - Cada pocos segundos: un chorrito de aviso.
//  - Al lanzar a Pogoman: ERUPCIÓN, columna alta de agua de almas, salpicadura, nube y onda en el suelo.
// Reemplaza al "Surtidor" del mapa (una malla que solo se estiraba).
public class GeyserFx : MonoBehaviour
{
    private static readonly Color Soul = new Color(0.35f, 0.95f, 1f);
    private static readonly Color SoulDeep = new Color(0.1f, 0.55f, 0.9f);
    private static AudioClip bubbleLoop, eruptClip, puffClip;

    private Vector3 mouth;
    private ParticleSystem steam, bubbles, column, spray, mist, ring;
    private Light glow;
    private AudioSource loop, shots;
    private float nextPuff, flare;
    private Material fx;

    public void Setup(Material fxMaterial, Renderer crater, Renderer oldJet)
    {
        fx = fxMaterial;
        Bounds b = crater != null ? crater.bounds : new Bounds(transform.position, Vector3.one);
        mouth = new Vector3(b.center.x, b.max.y - 0.15f, b.center.z);
        if (oldJet != null) oldJet.enabled = false;

        steam = Make("Vapor", 2.4f, new Vector2(0.9f, 2f), new Vector2(0.25f, 0.6f), new Color(0.75f, 0.95f, 1f, 0.13f), 0f, 70, -0.05f);
        SetRate(steam, 9f);
        SetShape(steam, 0.6f, 12f);

        bubbles = Make("Burbujeo", 0.9f, new Vector2(2.5f, 5f), new Vector2(0.05f, 0.12f), Soul, 1f, 80, 1f);
        SetRate(bubbles, 16f);
        SetShape(bubbles, 0.45f, 18f);

        column = Make("Chorro", 1.3f, new Vector2(16f, 22f), new Vector2(0.1f, 0.22f), Soul, 0f, 600, 1.15f);
        SetRate(column, 0f);
        SetShape(column, 0.35f, 5f);
        var cr = column.GetComponent<ParticleSystemRenderer>();
        cr.renderMode = ParticleSystemRenderMode.Stretch;
        cr.velocityScale = 0.035f;
        cr.lengthScale = 1.5f;
        var col = column.colorOverLifetime;
        col.enabled = true;
        col.color = new Gradient
        {
            colorKeys = new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Soul, 0.3f), new GradientColorKey(SoulDeep, 1f) },
            alphaKeys = new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.9f, 0.6f), new GradientAlphaKey(0f, 1f) },
        };

        spray = Make("Salpicadura", 1.4f, new Vector2(4f, 9f), new Vector2(0.06f, 0.15f), Soul, 0f, 250, 1.6f);
        SetRate(spray, 0f);
        SetShape(spray, 0.5f, 55f);

        mist = Make("Nube", 1.6f, new Vector2(1.5f, 4f), new Vector2(0.4f, 0.9f), new Color(0.7f, 0.95f, 1f, 0.16f), 0f, 60, -0.1f);
        SetRate(mist, 0f);
        SetShape(mist, 1f, 80f);

        ring = Make("Onda", 0.5f, new Vector2(7f, 9f), new Vector2(0.12f, 0.22f), Soul, 0f, 120, 0f);
        SetRate(ring, 0f);
        var rs = ring.shape;
        rs.shapeType = ParticleSystemShapeType.Circle;
        rs.radius = 0.4f;
        rs.radiusThickness = 0f;
        ring.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);   // sale de lado, por el piso

        var lg = new GameObject("LuzGeiser");
        lg.transform.SetParent(transform, false);
        lg.transform.position = mouth + Vector3.up * 1.2f;
        glow = lg.AddComponent<Light>();
        glow.type = LightType.Point;
        glow.color = Soul;
        glow.range = 7f;
        glow.shadows = LightShadows.None;

        if (bubbleLoop == null) MakeSounds();
        loop = gameObject.AddComponent<AudioSource>();
        loop.clip = bubbleLoop;
        loop.loop = true;
        loop.spatialBlend = 1f;
        loop.rolloffMode = AudioRolloffMode.Linear;
        loop.minDistance = 2f;
        loop.maxDistance = 18f;
        loop.volume = 0.35f * GameSettings.SfxVolume;
        loop.time = Random.value * bubbleLoop.length;
        loop.Play();
        shots = gameObject.AddComponent<AudioSource>();
        shots.spatialBlend = 1f;
        shots.rolloffMode = AudioRolloffMode.Linear;
        shots.minDistance = 4f;
        shots.maxDistance = 40f;

        nextPuff = Random.Range(2f, 6f);
    }

    private ParticleSystem Make(string name, float life, Vector2 speed, Vector2 size, Color color, float stretch, int max, float gravity)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        go.transform.position = mouth;
        go.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);   // hacia arriba
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(life * 0.6f, life);
        main.startSpeed = new ParticleSystem.MinMaxCurve(speed.x, speed.y);
        main.startSize = new ParticleSystem.MinMaxCurve(size.x, size.y);
        main.startColor = color;
        main.gravityModifier = gravity;
        main.maxParticles = max;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        ItemFx.FadeOut(ps);
        ItemFx.Apply(ps, fx);
        ps.Play();
        return ps;
    }

    private static void SetRate(ParticleSystem ps, float rate) { var e = ps.emission; e.rateOverTime = rate; }
    private static void SetShape(ParticleSystem ps, float radius, float angle)
    {
        var s = ps.shape;
        s.shapeType = ParticleSystemShapeType.Cone;
        s.radius = radius;
        s.angle = angle;
    }

    // Lanzamiento de Pogoman: erupción completa.
    public void Erupt()
    {
        column.Emit(320);
        spray.Emit(140);
        mist.Emit(18);
        ring.Emit(90);
        flare = 1f;
        shots.pitch = Random.Range(0.92f, 1.06f);
        shots.PlayOneShot(eruptClip, GameSettings.SfxVolume);
    }

    // Chorrito de aviso (solo).
    private void Puff()
    {
        column.Emit(45);
        spray.Emit(25);
        mist.Emit(4);
        flare = Mathf.Max(flare, 0.35f);
        shots.pitch = Random.Range(0.9f, 1.15f);
        shots.PlayOneShot(puffClip, 0.5f * GameSettings.SfxVolume);
    }

    private void Update()
    {
        if (glow == null) return;
        nextPuff -= Time.deltaTime;
        if (nextPuff <= 0f)
        {
            nextPuff = Random.Range(3.5f, 7f);
            Puff();
        }
        flare = Mathf.MoveTowards(flare, 0f, Time.deltaTime * 1.2f);
        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 2.3f + mouth.x);
        glow.intensity = 1.2f + pulse * 0.8f + flare * 7f;
        glow.range = 7f + flare * 8f;
        loop.volume = 0.35f * GameSettings.SfxVolume;
    }

    // ---- Sonidos sintetizados ----
    private static void MakeSounds()
    {
        const int rate = 22050;
        var rng = new System.Random(9);

        // Borboteo en bucle (4 s): ruido grave + burbujas que suben de tono.
        int n = rate * 4;
        var d = new float[n];
        float lp = 0f;
        for (int i = 0; i < n; i++)
        {
            float w = (float)rng.NextDouble() * 2f - 1f;
            lp += (w - lp) * 0.03f;
            d[i] = lp * 1.6f;
        }
        for (int k = 0; k < 40; k++)
        {
            int start = rng.Next(0, n - rate / 5);
            float f0 = 180f + (float)rng.NextDouble() * 320f;
            int len = rate / 12 + rng.Next(0, rate / 12);
            for (int i = 0; i < len; i++)
            {
                float t = i / (float)rate;
                d[start + i] += Mathf.Sin(2f * Mathf.PI * f0 * (1f + t * 6f) * t) * Mathf.Exp(-t * 30f) * 0.35f;
            }
        }
        // Que el bucle no truene: entra y sale suave en los extremos.
        for (int i = 0; i < rate / 10; i++) { float k = i / (rate / 10f); d[i] *= k; d[n - 1 - i] *= k; }
        bubbleLoop = AudioClip.Create("GeiserBorbotea", n, 1, rate, false);
        bubbleLoop.SetData(d, 0);

        eruptClip = Whoosh("GeiserErupcion", 1.6f, rate, rng, 1f);
        puffClip = Whoosh("GeiserChorrito", 0.6f, rate, rng, 0.6f);
    }

    // Chorro de agua a presión: ruido que sube de brillo, golpe grave y gotas que caen.
    private static AudioClip Whoosh(string name, float seconds, int rate, System.Random rng, float strength)
    {
        int n = (int)(rate * seconds);
        var d = new float[n];
        float lp = 0f, lp2 = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)rate;
            float w = (float)rng.NextDouble() * 2f - 1f;
            float cut = Mathf.Lerp(0.05f, 0.5f, Mathf.Clamp01(t / 0.15f)) * Mathf.Lerp(1f, 0.2f, Mathf.Clamp01(t / seconds));
            lp += (w - lp) * cut;
            lp2 += (lp - lp2) * 0.5f;
            float env = Mathf.Clamp01(t / 0.03f) * Mathf.Exp(-t * 2.8f / seconds);
            float thump = Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(90f, 40f, Mathf.Clamp01(t / 0.3f)) * t) * Mathf.Exp(-t * 10f) * strength;
            float drip = t > seconds * 0.4f && rng.NextDouble() < 0.0015 ? 0.5f : 0f;
            d[i] = Mathf.Clamp((lp2 * 1.5f * env + thump * 0.8f + drip) * 0.8f, -1f, 1f);
        }
        AudioClip c = AudioClip.Create(name, n, 1, rate, false);
        c.SetData(d, 0);
        return c;
    }
}
