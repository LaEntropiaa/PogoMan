using System.Collections.Generic;
using UnityEngine;

// Habilidades de los jefes (guardianes de los pilares y los mini jefes del Diablo):
//  - Orbes morados que te persiguen.
//  - Anillo de fuego: marca el suelo donde estás y explota al segundo.
//  - Invoca unos demonios.
// Va en JefeZombie.prefab. Se pausa con la Estampita (TimeStop apaga los componentes del enemigo).
[RequireComponent(typeof(EnemyTracker))]
public class BossCaster : MonoBehaviour
{
    [SerializeField] private float range = 24f;
    [SerializeField] private Vector2 cooldown = new Vector2(3.2f, 5.2f);
    [SerializeField] private int orbs = 3;
    [SerializeField] private float orbDamage = 9f;
    [SerializeField] private float ringDamage = 18f;
    [SerializeField] private float ringRadius = 3f;
    [SerializeField] private int summonCount = 3;
    [SerializeField] private int maxSummoned = 6;

    // Los pone RunSystems (para no cablear cada prefab).
    public static Material OrbMaterial, FxMaterial;
    public static AudioClip CastClip, ImpactClip, RingClip;

    private EnemyTracker tracker;
    private Transform player;
    private float timer;
    private AudioSource voice;
    private readonly List<Enemy> summoned = new List<Enemy>();

    private void Start()
    {
        tracker = GetComponent<EnemyTracker>();
        player = FindAnyObjectByType<PogoPlayer>()?.transform;
        timer = Random.Range(1.5f, 3f);
        voice = gameObject.AddComponent<AudioSource>();
        voice.playOnAwake = false;
        voice.spatialBlend = 1f;
        voice.minDistance = 4f;
        voice.maxDistance = 40f;
        voice.rolloffMode = AudioRolloffMode.Linear;
    }

    private void Update()
    {
        if (player == null || tracker == null || tracker.IsDead)
            return;

        // Más rápido con el tiempo de partida.
        float minutes = PlayerStats.Instance != null ? PlayerStats.Instance.RunTime / 60f : 0f;
        timer -= Time.deltaTime * (1f + minutes * 0.03f);
        if (timer > 0f)
            return;

        if ((player.position - tracker.Center).sqrMagnitude > range * range)
        {
            timer = 0.5f;
            return;
        }
        timer = Random.Range(cooldown.x, cooldown.y);

        summoned.RemoveAll(e => e == null || PogoBridge.IsDead(e));
        int roll = Random.Range(0, summoned.Count < maxSummoned ? 3 : 2);
        if (roll == 0) Volley(minutes);
        else if (roll == 1) Ring();
        else Summon();
    }

    private Vector3 Hand => tracker.Top + Vector3.down * 0.4f + transform.forward * 0.6f;

    private void Volley(float minutes)
    {
        int count = orbs + Mathf.FloorToInt(minutes / 5f);
        Vector3 from = Hand;
        for (int i = 0; i < count; i++)
        {
            float spread = (i - (count - 1) * 0.5f) * 25f;
            Vector3 dir = Quaternion.Euler(-25f, spread, 0f) * (player.position - from).normalized;
            MagicOrb.Launch(from, dir * 7f, player, orbDamage, OrbMaterial, FxMaterial, ImpactClip);
        }
        ItemFx.Burst(from, 25, 0.4f, new Color(0.7f, 0.2f, 1f), FxMaterial, 5f, 0.35f, 0.5f);
        Play(CastClip);
    }

    private void Ring()
    {
        Vector3 at = player.position;
        if (Physics.Raycast(at + Vector3.up, Vector3.down, out RaycastHit hit, 10f, ~0, QueryTriggerInteraction.Ignore) && hit.rigidbody == null)
            at = hit.point;
        FireRing.Spawn(at, ringRadius, 1.1f, ringDamage, FxMaterial, RingClip);
        ItemFx.Burst(Hand, 20, 0.4f, new Color(1f, 0.45f, 0.1f), FxMaterial, 4f, 0.35f, 0.5f);
    }

    private void Summon()
    {
        EnemyDirector d = EnemyDirector.Instance;
        if (d == null)
            return;
        for (int i = 0; i < summonCount; i++)
        {
            Vector2 c = Random.insideUnitCircle.normalized * Random.Range(2.5f, 5f);
            Vector3 p = transform.position + new Vector3(c.x, 3f, c.y);
            if (!Physics.Raycast(p, Vector3.down, out RaycastHit hit, 10f, ~0, QueryTriggerInteraction.Ignore) || hit.rigidbody != null)
                continue;
            Enemy e = d.SpawnEnemy(d.RandomDemonPrefab(), hit.point);
            if (e != null)
                summoned.Add(e);
        }
        ItemFx.Burst(tracker.Center, 40, 1.2f, new Color(0.55f, 0.1f, 0.9f), FxMaterial, 6f, 0.5f, 0.8f);
        Play(CastClip, 0.7f);
    }

    private void Play(AudioClip clip, float pitch = 1f)
    {
        if (clip == null || voice == null)
            return;
        voice.pitch = pitch * Random.Range(0.95f, 1.05f);
        voice.PlayOneShot(clip, GameSettings.SfxVolume);
    }
}

// Orbe morado que persigue a Pogoman un rato y luego sigue derecho.
public class MagicOrb : MonoBehaviour
{
    private Vector3 velocity;
    private Transform target;
    private float damage;
    private float life = 5f;
    private float homing = 2.2f;
    private float age;
    private Material fx;
    private AudioClip impact;

    // scale: tamaño y alcance del golpe (el Diablo los lanza al doble). victim: a quién le pega aunque no lo persiga.
    private float scale = 1f;
    private Transform victim;

    public static void Launch(Vector3 from, Vector3 velocity, Transform target, float damage, Material orbMat, Material fxMat, AudioClip impact,
        float scale = 1f, Transform victim = null)
    {
        var go = new GameObject("OrbeMorado");
        go.transform.position = from;
        var o = go.AddComponent<MagicOrb>();
        o.scale = scale;
        o.victim = victim != null ? victim : target;
        o.velocity = velocity;
        o.target = target;
        o.damage = damage;
        o.fx = fxMat;
        o.impact = impact;

        var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Destroy(sphere.GetComponent<Collider>());
        sphere.transform.SetParent(go.transform, false);
        sphere.transform.localScale = Vector3.one * 0.45f * scale;
        var r = sphere.GetComponent<Renderer>();
        if (orbMat != null) r.sharedMaterial = orbMat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        ItemFx.Trail(go, new Color(0.75f, 0.3f, 1f), 0.35f, 0.4f, fxMat);
        var l = go.AddComponent<Light>();
        l.type = LightType.Point;
        l.color = new Color(0.7f, 0.25f, 1f);
        l.range = 4f;
        l.intensity = 2f;
        l.shadows = LightShadows.None;
    }

    private void Update()
    {
        if (TimeStop.Active)
            return;
        float dt = Time.deltaTime;
        life -= dt;
        homing -= dt;
        age += dt;
        if (target != null && homing > 0f)
        {
            Vector3 want = (target.position + Vector3.up * 0.8f - transform.position).normalized * 9f;
            velocity = Vector3.MoveTowards(velocity, want, 9f * dt);
        }
        Vector3 next = transform.position + velocity * dt;

        // Golpea a Pogoman
        float reach = 0.8f * scale;
        if (victim != null && (victim.position + Vector3.up * 0.8f - next).sqrMagnitude < reach * reach)
        {
            var p = victim.GetComponent<PogoPlayer>();
            if (p != null && !PogoBridge.IsDead(p))
                PlayerVitals.Damage(p, damage);
            Explode(next);
            return;
        }
        // Choca con el escenario
        // (ignora demonios y al jefe que lo lanzó; los primeros instantes no choca para que salga)
        if (age > 0.15f && Physics.Linecast(transform.position, next, out RaycastHit hit, ~0, QueryTriggerInteraction.Ignore) &&
            hit.rigidbody == null && hit.collider.GetComponentInParent<Enemy>() == null)
        {
            Explode(hit.point);
            return;
        }
        transform.position = next;
        if (life <= 0f)
            Explode(next);
    }

    private void Explode(Vector3 at)
    {
        ItemFx.Burst(at, Mathf.RoundToInt(22 * scale), 0.3f * scale, new Color(0.75f, 0.3f, 1f), fx, 5f * scale, 0.3f * scale, 0.45f);
        if (impact != null)
            AudioSource.PlayClipAtPoint(impact, at, 0.7f * GameSettings.SfxVolume);
        Destroy(gameObject);
    }
}

// Anillo de fuego: aviso en el suelo y luego erupción de flamas.
public class FireRing : MonoBehaviour
{
    private float radius, delay, damage, age;
    private bool erupted;
    private Material fx;
    private ParticleSystem warn;

    public static void Spawn(Vector3 at, float radius, float delay, float damage, Material fx, AudioClip clip)
    {
        var go = new GameObject("AnilloDeFuego");
        go.transform.position = at + Vector3.up * 0.08f;
        var f = go.AddComponent<FireRing>();
        f.radius = radius;
        f.delay = delay;
        f.damage = damage;
        f.fx = fx;
        if (clip != null)
            AudioSource.PlayClipAtPoint(clip, at, 0.85f * GameSettings.SfxVolume);

        f.warn = go.AddComponent<ParticleSystem>();
        f.warn.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var m = f.warn.main;
        m.startLifetime = 0.3f;
        m.startSpeed = 0.3f;
        m.startSize = 0.3f;
        m.startColor = new Color(1f, 0.35f, 0.05f, 0.9f);
        m.maxParticles = 300;
        var e = f.warn.emission;
        e.rateOverTime = 200f;
        var s = f.warn.shape;
        s.shapeType = ParticleSystemShapeType.Circle;
        s.radius = radius;
        s.radiusThickness = 0f;
        go.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);
        ItemFx.Apply(f.warn, fx);
        f.warn.Play();
    }

    private void Update()
    {
        if (TimeStop.Active)
            return;
        age += Time.deltaTime;
        if (!erupted && age >= delay)
        {
            erupted = true;
            warn.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            for (int i = 0; i < 10; i++)
            {
                float a = i / 10f * Mathf.PI * 2f;
                Vector3 p = transform.position + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * radius * Random.Range(0.2f, 1f);
                ItemFx.Burst(p + Vector3.up * 0.5f, 14, 0.4f, new Color(1f, 0.5f, 0.1f), fx, 7f, 0.55f, 0.6f);
            }
            PogoPlayer player = FindAnyObjectByType<PogoPlayer>();
            if (player != null)
            {
                Vector3 d = player.transform.position - transform.position;
                if (new Vector2(d.x, d.z).magnitude <= radius && d.y < 3f && d.y > -1.5f)
                    PlayerVitals.Damage(player, damage);
            }
        }
        if (age > delay + 1f)
            Destroy(gameObject);
    }
}
