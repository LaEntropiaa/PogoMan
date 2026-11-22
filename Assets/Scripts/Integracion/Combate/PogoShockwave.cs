using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Tercer ataque de Pogoman: la ONDA del pogo. Es AUTOMÁTICA: cada vez que se cumple su recarga
// (2 s de base) sale un aro por el suelo debajo de Pogoman, esté saltando, en el aire o deslizándose,
// y daña a los demonios que alcanza.
// Lo mejoran:
//   - Daño de la onda, tamaño, cadencia y ondas por caída (mejoras de nivel).
//   - También el escupitajo: cadencia (recarga), proyectiles (ondas extra) y tamaño (radio).
[DefaultExecutionOrder(50)]   // después de PogoPlayer: el aterrizaje ya se resolvió en este paso
public class PogoShockwave : MonoBehaviour
{
    public const float BaseRadius = 3.2f;
    private const float ExpandTime = 0.32f;
    private const float WaveSpacing = 0.17f;

    public static PogoShockwave Instance { get; private set; }

    private PogoPlayer player;
    private Collider body;
    private float cooldownLeft = 1f;
    private AudioSource audioSource;
    private static AudioClip thump;

    public float CooldownLeft => cooldownLeft;
    public event System.Action<Vector3> Fired;

    private static PlayerStats S => PlayerStats.Instance;

    // ---- Valores actuales (también los muestra la pausa) ----
    public static float Radius
    {
        get
        {
            if (S == null) return BaseRadius;
            float spitSize = 1f + (S.Get(PogoStat.SpitSize) - 1f) * 0.5f;
            return BaseRadius * S.Get(PogoStat.WaveSize) * spitSize;
        }
    }

    public static float Cooldown
    {
        get
        {
            if (S == null) return 2f;
            float rate = S.Get(PogoStat.WaveRate) * Mathf.Sqrt(Mathf.Max(0.2f, S.Get(PogoStat.FireRate) / S.GetBase(PogoStat.FireRate)));
            return Mathf.Max(0.25f, S.WaveCooldownBase / Mathf.Max(0.1f, rate));
        }
    }

    public static int Count => S == null ? 1
        : Mathf.Clamp(Mathf.RoundToInt(S.Get(PogoStat.WaveCount)) + Mathf.FloorToInt((S.Get(PogoStat.Projectiles) - 1f) / 3f), 1, 40);

    public static float Damage => S != null ? S.Get(PogoStat.WaveDamage) : 18f;

    private void Awake()
    {
        Instance = this;
        player = GetComponent<PogoPlayer>();
        body = GetComponent<Collider>();
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;
        if (thump == null) thump = MakeThump();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void FixedUpdate()
    {
        if (player == null || PogoBridge.IsDead(player))
            return;

        if (PlayerStats.Instance != null && PlayerStats.Instance.RunEnded)
            return;
        cooldownLeft -= Time.fixedDeltaTime;
        if (cooldownLeft <= 0f)
            Fire(GroundBelow());
    }

    private Vector3 Feet => body != null
        ? new Vector3(transform.position.x, body.bounds.min.y + 0.08f, transform.position.z)
        : transform.position;

    private readonly RaycastHit[] hits = new RaycastHit[8];

    // El suelo debajo de Pogoman (aunque vaya en el aire); si no hay nada cerca, sus pies.
    private Vector3 GroundBelow()
    {
        Vector3 feet = Feet;
        int n = Physics.RaycastNonAlloc(feet + Vector3.up * 0.3f, Vector3.down, hits, 25f, ~0, QueryTriggerInteraction.Ignore);
        float best = float.MaxValue;
        Vector3 at = feet;
        for (int i = 0; i < n; i++)
        {
            if (hits[i].rigidbody != null) continue;   // demonios, Pogoman
            if (hits[i].distance < best) { best = hits[i].distance; at = hits[i].point + Vector3.up * 0.08f; }
        }
        return at;
    }

    public void Fire(Vector3 at)
    {
        cooldownLeft = Cooldown;
        StartCoroutine(Waves(at, Count, Radius, Damage));
        Fired?.Invoke(at);
    }

    private IEnumerator Waves(Vector3 at, int count, float radius, float damage)
    {
        for (int i = 0; i < count; i++)
        {
            // Cada réplica sale un poco más grande, desde donde va Pogoman en ese momento.
            Vector3 from = i == 0 ? at : GroundBelow();
            ShockRing.Spawn(from, radius * (1f + 0.12f * i), damage, ExpandTime);
            if (thump != null)
            {
                audioSource.pitch = Random.Range(0.92f, 1.05f) + i * 0.04f;
                audioSource.PlayOneShot(thump, (i == 0 ? 0.8f : 0.45f) * GameSettings.SfxVolume);
            }
            if (i < count - 1)
                yield return new WaitForSeconds(WaveSpacing);
        }
    }

    // Golpe grave sintetizado (no necesita archivo de audio).
    private static AudioClip MakeThump()
    {
        const int rate = 44100;
        int n = (int)(rate * 0.45f);
        var data = new float[n];
        var rng = new System.Random(7);
        float noise = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)rate;
            float env = Mathf.Exp(-t * 9f);
            float freq = Mathf.Lerp(95f, 38f, Mathf.Clamp01(t / 0.3f));
            noise = noise * 0.92f + ((float)rng.NextDouble() * 2f - 1f) * 0.08f;   // ruido grave (tierra)
            data[i] = (Mathf.Sin(2f * Mathf.PI * freq * t) * 0.85f + noise * 2.2f * Mathf.Exp(-t * 14f)) * env;
        }
        AudioClip clip = AudioClip.Create("OndaPogo", n, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }
}

// Un aro de la onda: se expande por el suelo, daña a cada demonio una vez al alcanzarlo y se desvanece.
public class ShockRing : MonoBehaviour
{
    private const int Segments = 72;
    private Color Inner = new Color(1f, 0.85f, 0.45f);
    private Color Outer = new Color(1f, 0.45f, 0.15f);
    private bool noReward;

    private float radius, damage, expand, age;
    private LineRenderer ring, glow;
    private Transform disc;
    private Material discMat;
    private readonly HashSet<EnemyTracker> hit = new HashSet<EnemyTracker>();
    private static readonly List<EnemyTracker> buffer = new List<EnemyTracker>();

    // Onda de otro color que mata sin dar recompensa (la del pilar).
    public static void SpawnClear(Vector3 at, float radius, float damage, float expandTime, Color inner, Color outer)
    {
        ShockRing r = Spawn(at, radius, damage, expandTime);
        r.Inner = inner;
        r.Outer = outer;
        r.noReward = true;
        r.ring.widthMultiplier = 0.6f;
    }

    public static ShockRing Spawn(Vector3 at, float radius, float damage, float expandTime)
    {
        var go = new GameObject("OndaPogo");
        go.transform.position = at;
        var r = go.AddComponent<ShockRing>();
        r.radius = radius;
        r.damage = damage;
        r.expand = expandTime;
        Material fx = ItemEffects.Instance != null ? ItemEffects.Instance.FxMaterial : null;
        r.ring = r.MakeLine("Aro", 0.32f, fx);
        r.glow = r.MakeLine("Brillo", 0.9f, fx);

        // Polvo que sale del centro.
        ItemFx.Burst(at + Vector3.up * 0.15f, 18 + Mathf.RoundToInt(radius * 3f), 0.4f, new Color(0.75f, 0.55f, 0.4f, 0.8f), fx, radius * 2.2f, 0.35f, 0.45f);
        return r;
    }

    private LineRenderer MakeLine(string name, float width, Material fx)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        var lr = go.AddComponent<LineRenderer>();
        lr.loop = true;
        lr.useWorldSpace = true;
        lr.positionCount = Segments;
        lr.widthMultiplier = width;
        lr.alignment = LineAlignment.View;
        lr.numCapVertices = 0;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows = false;
        if (fx != null) lr.sharedMaterial = fx;
        return lr;
    }

    // Cada punto del aro se pega al terreno (el piso del mapa no es plano).
    private readonly float[] groundY = new float[Segments];
    private float lastGroundR = -1f;

    private void SetRadius(LineRenderer lr, float r, float lift)
    {
        Vector3 c = transform.position;
        bool resample = Mathf.Abs(r - lastGroundR) > 0.4f;
        for (int i = 0; i < Segments; i++)
        {
            float a = i / (float)Segments * Mathf.PI * 2f;
            Vector3 p = c + new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
            if (resample)
                groundY[i] = Physics.Raycast(p + Vector3.up * 2.5f, Vector3.down, out RaycastHit h, 5f, ~0, QueryTriggerInteraction.Ignore) && h.rigidbody == null
                    ? h.point.y : c.y;
            p.y = groundY[i] + lift;
            lr.SetPosition(i, p);
        }
        if (resample) lastGroundR = r;
    }

    private void Update()
    {
        age += Time.deltaTime;
        float k = Mathf.Clamp01(age / expand);
        float e = 1f - (1f - k) * (1f - k);   // sale rápido y frena
        float r = Mathf.Max(0.2f, radius * e);
        float fade = age <= expand ? 1f : 1f - Mathf.Clamp01((age - expand) / 0.25f);

        SetRadius(ring, r, 0.3f);
        SetRadius(glow, r * 0.96f, 0.15f);
        ring.widthMultiplier = Mathf.Lerp(0.45f, 0.22f, k);
        glow.widthMultiplier = Mathf.Lerp(0.3f, 1.2f, k);
        ring.startColor = ring.endColor = new Color(Inner.r, Inner.g, Inner.b, 0.95f * fade);
        glow.startColor = glow.endColor = new Color(Outer.r, Outer.g, Outer.b, 0.45f * fade);

        if (age <= expand + 0.02f)
            DamageInside(r);

        if (fade <= 0f)
            Destroy(gameObject);
    }

    private void DamageInside(float r)
    {
        buffer.Clear();
        Vector3 c = transform.position;
        float r2 = (r + 0.4f) * (r + 0.4f);
        foreach (EnemyTracker t in EnemyTracker.All)
        {
            if (t == null || t.IsDead || hit.Contains(t)) continue;
            Vector3 d = t.Center - c;
            if (d.y < -2f || d.y > 3.2f) continue;   // los voladores altos y los de otro piso no
            d.y = 0f;
            if (d.sqrMagnitude <= r2) buffer.Add(t);
        }
        foreach (EnemyTracker t in buffer)
        {
            hit.Add(t);
            if (noReward) { if (!t.IsBoss) t.HitNoReward(damage, HitSource.Other); }
            else t.Hit(damage, HitSource.Wave);
            // Empujoncito hacia afuera (los demonios normales tienen Rigidbody).
            Rigidbody body = t.GetComponent<Rigidbody>();
            if (body != null && !body.isKinematic && !t.IsBoss)
            {
                Vector3 away = t.transform.position - c; away.y = 0f;
                if (away.sqrMagnitude > 0.01f)
                    body.AddForce(away.normalized * 4f + Vector3.up * 2f, ForceMode.VelocityChange);
            }
        }
    }
}
