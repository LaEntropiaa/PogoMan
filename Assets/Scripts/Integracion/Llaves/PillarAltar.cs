using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Los pilares ya no despiertan solos: te acercas y con E despiertas al guardián.
// Usa el BossPillar de Alonso tal cual: solo le apaga la activación por distancia
// y llama a su SpawnBoss cuando el jugador interactúa.
public class PillarAltar : MonoBehaviour
{
    [SerializeField] private float interactDistance = 5f;
    [Tooltip("Radio de la onda que limpia demonios al despertar al guardián.")]
    [SerializeField] private float clearRadius = 10f;
    [SerializeField] private Font font;
    [SerializeField] private Material fxMaterial;
    [SerializeField] private AudioClip awakenClip;

    private static readonly MethodInfo SpawnBoss =
        typeof(BossPillar).GetMethod("SpawnBoss", BindingFlags.Instance | BindingFlags.NonPublic);

    private readonly List<BossPillar> pillars = new List<BossPillar>();
    private readonly Dictionary<BossPillar, ParticleSystem> glows = new Dictionary<BossPillar, ParticleSystem>();
    private Transform player;
    private Text prompt;
    private AudioSource audioSource;

    private void Start()
    {
        player = FindAnyObjectByType<PogoPlayer>()?.transform;
        audioSource = UiKit.UiAudio(gameObject);
        foreach (BossPillar p in FindObjectsByType<BossPillar>())
        {
            PogoBridge.SetFloat(p, "activationRadius", -1f);   // nunca por distancia
            pillars.Add(p);
            glows[p] = MakeGlow(p.transform);
        }
        AssignGuardians();

        Canvas canvas = UiKit.MakeCanvas("HUDPilar", 6);
        canvas.transform.SetParent(transform, false);
        prompt = UiKit.Text(UiKit.Rect("Prompt", canvas.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
            new Vector2(0, 190), new Vector2(1200, 60)), font, 40, new Color(0.85f, 0.55f, 1f), TextAnchor.MiddleCenter);
        prompt.gameObject.AddComponent<Outline>().effectDistance = new Vector2(3, -3);
    }

    // Humito morado que sube del pilar mientras su guardián duerme.
    private ParticleSystem MakeGlow(Transform pillar)
    {
        var go = new GameObject("AuraPilar");
        go.transform.SetParent(pillar, false);
        go.transform.localPosition = Vector3.up * 0.5f;
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.startLifetime = 2.2f;
        main.startSpeed = 1.2f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.2f, 0.45f);
        main.startColor = new Color(0.65f, 0.2f, 1f, 0.7f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 120;
        var em = ps.emission;
        em.rateOverTime = 22f;
        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 1.6f;
        go.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);
        ItemFx.FadeOut(ps);
        ItemFx.Apply(ps, fxMaterial);
        ps.Play();
        return ps;
    }

    private static bool Awakened(BossPillar p) => PogoBridge.GetBool(p, "bossSpawned");

    // Cada pilar activo recibe un guardián distinto, al azar en cada partida.
    private void AssignGuardians()
    {
        GuardianKit kit = GuardianKit.Instance;
        if (kit == null || kit.GuardianPrefabs == null)
            return;
        var bag = new List<Enemy>();
        foreach (Enemy e in kit.GuardianPrefabs)
            if (e != null) bag.Add(e);
        if (bag.Count == 0)
            return;
        for (int i = bag.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (bag[i], bag[j]) = (bag[j], bag[i]);
        }
        for (int i = 0; i < pillars.Count; i++)
            PogoBridge.Set(pillars[i], "bossPrefab", bag[i % bag.Count]);
    }

    private void Update()
    {
        if (player == null)
            return;

        BossPillar near = null;
        float best = interactDistance;
        foreach (BossPillar p in pillars)
        {
            if (p == null || !p.isActiveAndEnabled || Awakened(p))
                continue;
            Vector3 d = p.transform.position - player.position;
            d.y = 0f;
            if (d.magnitude < best)
            {
                best = d.magnitude;
                near = p;
            }
        }

        // Solo un guardián a la vez: hasta vencerlo no se despierta otro pilar.
        bool busy = false;
        foreach (Guardian g in Guardian.Alive)
            if (g != null && !g.IsDead) busy = true;

        prompt.text = near == null || Time.timeScale <= 0f || ChestUI.IsOpen ? ""
            : busy ? "DERROTA PRIMERO AL GUARDIÁN QUE DESPERTASTE"
            : "E  DESPERTAR AL GUARDIÁN DEL PILAR";
        if (near == null || busy || Time.timeScale <= 0f || ChestUI.IsOpen)
            return;

        bool pressed = (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame) ||
                       (Gamepad.current != null && Gamepad.current.buttonNorth.wasPressedThisFrame);
        if (!pressed)
            return;

        // Onda expansiva al despertar al guardián: limpia a los demonios en 10 m (sin experiencia ni monedas).
        ShockRing.SpawnClear(near.transform.position + Vector3.up * 0.2f, clearRadius, 999999f, 0.55f,
            new Color(0.9f, 0.6f, 1f), new Color(0.55f, 0.1f, 1f));
        EnemyTracker.HitArea(near.transform.position, clearRadius, 999999f, HitSource.Other, true, 8f, null, true);
        Explode(near);
        SpawnBoss?.Invoke(near, null);
        if (glows.TryGetValue(near, out ParticleSystem glow) && glow != null)
            glow.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        ItemFx.Burst(near.transform.position + Vector3.up * 2f, 70, 1.5f, new Color(0.7f, 0.2f, 1f), fxMaterial, 10f, 0.5f, 0.9f);
        if (awakenClip != null)
            audioSource.PlayOneShot(awakenClip, GameSettings.SfxVolume);
    }

    // ---- El pilar explota al despertar al guardián ----
    // Revienta en pedazos (con fuego y humo) y desaparece: así el guardián ya no se queda atorado en él.
    private static AudioClip boom;

    private void Explode(BossPillar pillar)
    {
        Bounds b = new Bounds(pillar.transform.position + Vector3.up * 2f, Vector3.one);
        bool first = true;
        var stoneColors = new List<Color>();
        foreach (Renderer r in pillar.GetComponentsInChildren<Renderer>())
        {
            if (r is ParticleSystemRenderer) continue;
            if (first) { b = r.bounds; first = false; } else b.Encapsulate(r.bounds);
            Material m = r.sharedMaterial;
            if (m != null && m.HasProperty("_BaseColor")) stoneColors.Add(m.GetColor("_BaseColor"));
            r.enabled = false;
        }
        foreach (Collider c in pillar.GetComponentsInChildren<Collider>())
            if (!c.isTrigger) c.enabled = false;
        if (stoneColors.Count == 0) stoneColors.Add(new Color(0.35f, 0.3f, 0.32f));

        Collider playerCol = player != null ? player.GetComponent<Collider>() : null;
        int chunks = 26;
        for (int i = 0; i < chunks; i++)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "PedazoPilar";
            float size = Random.Range(0.25f, 0.75f) * Mathf.Clamp(b.size.x, 0.8f, 2f);
            go.transform.position = new Vector3(
                Random.Range(b.min.x, b.max.x), Random.Range(b.min.y + 0.3f, b.max.y), Random.Range(b.min.z, b.max.z));
            go.transform.rotation = Random.rotation;
            go.transform.localScale = new Vector3(size, size * Random.Range(0.6f, 1.4f), size * Random.Range(0.7f, 1.2f));
            Color c = stoneColors[Random.Range(0, stoneColors.Count)] * Random.Range(0.75f, 1.1f);
            ItemFx.Paint(go, i % 5 == 0 ? new Color(1f, 0.45f, 0.1f) : new Color(c.r, c.g, c.b), i % 5 == 0 ? 2f : 0.1f);
            if (playerCol != null) Physics.IgnoreCollision(go.GetComponent<Collider>(), playerCol);
            var body = go.AddComponent<Rigidbody>();
            body.mass = 0.5f;
            Vector3 dir = (go.transform.position - new Vector3(b.center.x, b.min.y, b.center.z)).normalized;
            body.linearVelocity = (dir + Vector3.up * 0.8f).normalized * Random.Range(7f, 15f);
            body.angularVelocity = Random.insideUnitSphere * 10f;
            go.AddComponent<Debris>().life = Random.Range(2.5f, 4f);
        }

        // Fuego, humo y polvo en toda la altura del pilar.
        for (int i = 0; i < 6; i++)
        {
            Vector3 at = Vector3.Lerp(new Vector3(b.center.x, b.min.y, b.center.z), new Vector3(b.center.x, b.max.y, b.center.z), i / 5f);
            ItemFx.Burst(at, 40, 0.8f, i % 2 == 0 ? new Color(1f, 0.5f, 0.1f) : new Color(1f, 0.85f, 0.3f), fxMaterial, 9f, 0.7f, 0.7f);
        }
        ItemFx.Burst(new Vector3(b.center.x, b.min.y + 0.5f, b.center.z), 60, 1.5f, new Color(0.25f, 0.2f, 0.22f, 0.8f), fxMaterial, 4f, 1.4f, 1.6f);
        ItemFx.Burst(b.center, 90, 1f, new Color(0.7f, 0.25f, 1f), fxMaterial, 16f, 0.4f, 0.8f);

        var lightGo = new GameObject("DestelloPilar");
        lightGo.transform.position = b.center;
        var light = lightGo.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = new Color(1f, 0.55f, 0.2f);
        light.range = 25f;
        light.intensity = 8f;
        lightGo.AddComponent<Debris>().fadeLight = true;

        if (boom == null) boom = MakeBoom();
        audioSource.PlayOneShot(boom, GameSettings.SfxVolume);
    }

    private static AudioClip MakeBoom()
    {
        const int rate = 44100;
        int n = (int)(rate * 1.6f);
        var d = new float[n];
        var rng = new System.Random(5);
        float lp = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)rate;
            float w = (float)rng.NextDouble() * 2f - 1f;
            lp += (w - lp) * Mathf.Lerp(0.5f, 0.04f, Mathf.Clamp01(t / 0.4f));
            float body = Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(80f, 30f, Mathf.Clamp01(t / 0.6f)) * t) * Mathf.Exp(-t * 4f);
            // Crujidos de piedra al principio.
            float crack = t < 0.25f && rng.NextDouble() < 0.02 ? (float)rng.NextDouble() : 0f;
            d[i] = Mathf.Clamp((lp * 2.2f * Mathf.Exp(-t * 2.5f) + body * 0.9f + crack * 0.6f) * 0.9f, -1f, 1f);
        }
        AudioClip c = AudioClip.Create("PilarExplota", n, 1, rate, false);
        c.SetData(d, 0);
        return c;
    }

    public IReadOnlyList<BossPillar> Pillars => pillars;
    public static bool IsAwake(BossPillar p) => Awakened(p);
}

// Pedazo del pilar (o destello): se encoge y desaparece.
public class Debris : MonoBehaviour
{
    public float life = 3f;
    public bool fadeLight;
    private float age;
    private Vector3 scale;
    private Light lightComp;

    private void Start()
    {
        scale = transform.localScale;
        lightComp = GetComponent<Light>();
        if (fadeLight) life = 0.6f;
    }

    private void Update()
    {
        age += Time.deltaTime;
        float k = age / life;
        if (lightComp != null) lightComp.intensity = 8f * (1f - k);
        else if (k > 0.7f) transform.localScale = scale * Mathf.Clamp01((1f - k) / 0.3f);
        if (k >= 1f) Destroy(gameObject);
    }
}
