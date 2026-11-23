using UnityEngine;

// Cofre del mapa que se compra con monedas. ChestUI se encarga de la animación.
public class Chest : MonoBehaviour
{
    // El cofre cerrado más cercano que está al alcance de Pogoman.
    public static Chest Nearest { get; private set; }

    [SerializeField] private int price = 50;
    [SerializeField] private float interactDistance = 2.6f;

    // Credencial de Mayorista: los cofres de pago cuestan más (los gratis siguen gratis).
    public static float PriceMultiplier = 1f;
    // Precio que sube: el primer cofre de pago cuesta 10 y cada uno que abres encarece el siguiente 30 %.
    public const int FirstPrice = 10;
    public const float PriceGrowth = 1.3f;
    public static int PaidOpened;
    public int Price => price <= 0 ? 0 : Mathf.RoundToInt(FirstPrice * Mathf.Pow(PriceGrowth, PaidOpened) * PriceMultiplier);
    public bool Free => price <= 0;
    // Se abrió un cofre (lo usa el Vale de Farmacia).
    public static event System.Action<Chest> AnyOpened;
    public static void RaiseOpened(Chest c) => AnyOpened?.Invoke(c);
    public bool Opened { get; private set; }

    // Material para las chispitas (lo pone RunSystems).
    public static Material SparkleMaterial;

    private Color GlowColor = new Color(1f, 0.75f, 0.3f);
    // Cofre de élite: morado brillante y se abre solo al acercarte.
    public bool AutoOpen { get; private set; }
    public bool Elite => AutoOpen;

    public void MakeElite()
    {
        AutoOpen = true;
        price = 0;
        interactDistance = 3.2f;
        GlowColor = new Color(0.8f, 0.25f, 1f);
        var block = new MaterialPropertyBlock();
        foreach (Renderer r in renderers)
        {
            if (r == null) continue;
            r.GetPropertyBlock(block);
            block.SetColor("_BaseColor", new Color(0.85f, 0.45f, 1f));
            r.SetPropertyBlock(block);
        }
    }

    private static Transform player;
    private Renderer[] renderers;
    private Light glowLight;
    private ParticleSystem sparkles;
    private Material[] glowMaterials;
    private float glowOffset;

    public void Configure(int newPrice) => price = newPrice;

    private void Awake() => renderers = GetComponentsInChildren<Renderer>();

    // Los cofres cerrados brillan: luz dorada que late, emisión y chispitas que suben.
    private void Start()
    {
        glowOffset = Random.value * 10f;

        var lightGO = new GameObject("BrilloCofre");
        lightGO.transform.SetParent(transform, false);
        lightGO.transform.position = Center + Vector3.up * 0.8f;
        glowLight = lightGO.AddComponent<Light>();
        glowLight.type = LightType.Point;
        glowLight.color = GlowColor;
        glowLight.range = AutoOpen ? 7f : 4.5f;
        glowLight.shadows = LightShadows.None;

        glowMaterials = new Material[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
        {
            Material m = renderers[i].material;   // copia propia para poder hacerla brillar
            if (m.HasProperty("_EmissionColor"))
            {
                m.EnableKeyword("_EMISSION");
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
                glowMaterials[i] = m;
            }
        }

        sparkles = MakeSparkles();
    }

    private ParticleSystem MakeSparkles()
    {
        var go = new GameObject("ChispasCofre");
        go.transform.SetParent(transform, false);
        go.transform.position = Center;
        go.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.loop = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(1f, 1.8f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.3f, 0.9f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.12f);
        main.startColor = AutoOpen ? new Color(0.85f, 0.4f, 1f, 0.95f) : new Color(1f, 0.85f, 0.4f, 0.9f);
        main.maxParticles = 30;
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        var emission = ps.emission;
        emission.rateOverTime = 9f;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 0.5f;

        var size = ps.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0f)));

        var r = go.GetComponent<ParticleSystemRenderer>();
        if (SparkleMaterial != null)
            r.sharedMaterial = SparkleMaterial;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        ps.Play();
        return ps;
    }

    private void LateUpdate()
    {
        if (Opened || glowLight == null)
            return;

        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 2.6f + glowOffset);
        glowLight.intensity = (1.6f + pulse * 1.8f) * (AutoOpen ? 1.8f : 1f);
        Color emission = GlowColor * (0.25f + pulse * 0.55f);
        foreach (Material m in glowMaterials)
            if (m != null)
                m.SetColor("_EmissionColor", emission);
    }

    private Vector3 Center => renderers.Length > 0 ? renderers[0].bounds.center : transform.position;

    private void Update()
    {
        if (Opened)
            return;

        if (player == null)
        {
            PogoPlayer p = FindAnyObjectByType<PogoPlayer>();
            if (p == null)
                return;
            player = p.transform;
        }

        float distance = Vector3.Distance(Center, player.position);
        bool inRange = distance < interactDistance;

        if (inRange && (Nearest == null || Nearest.Opened ||
                        Vector3.Distance(Nearest.Center, player.position) > distance))
            Nearest = this;
        else if (!inRange && Nearest == this)
            Nearest = null;
    }

    public void MarkOpened()
    {
        Opened = true;
        if (Nearest == this)
            Nearest = null;

        // Se apaga el brillo.
        if (glowLight != null)
            Destroy(glowLight.gameObject);
        if (sparkles != null)
            sparkles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        if (glowMaterials != null)
            foreach (Material m in glowMaterials)
                if (m != null)
                    m.SetColor("_EmissionColor", Color.black);

        // Un cofre abierto desaparece con un "puf" dorado (ya no se queda gris).
        ItemFx.Burst(Center, 24, 0.5f, AutoOpen ? new Color(0.85f, 0.4f, 1f) : new Color(1f, 0.85f, 0.4f), SparkleMaterial, 4f, 0.2f, 0.5f);
        foreach (Renderer r in renderers)
            if (r != null) r.enabled = false;
        foreach (Collider c in GetComponentsInChildren<Collider>())
            if (!c.isTrigger) c.enabled = false;
    }

    private void OnDisable()
    {
        if (Nearest == this)
            Nearest = null;
    }
}
