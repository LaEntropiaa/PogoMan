using UnityEngine;

// Ataque automático de Pogoman: escupe al enemigo más cercano dentro del alcance.
// La cadencia, el daño y el crítico salen de PlayerStats.
public class SpitAttack : MonoBehaviour
{
    [SerializeField] private SpitProjectile projectilePrefab;
    [Tooltip("Desde dónde sale el escupitajo, relativo a Pogoman.")]
    [SerializeField] private Vector3 mouthOffset = new Vector3(0f, 0.75f, 0.25f);

    [Header("Sonidos")]
    [SerializeField] private AudioClip spitClip;
    [SerializeField] private AudioClip hitClip;
    [SerializeField] private AudioClip critClip;
    [SerializeField, Range(0f, 1f)] private float spitVolume = 0.6f;

    private PogoPlayer player;
    private AudioSource audioSource;
    private float cooldown;

    public static SpitAttack Instance { get; private set; }
    // Cada salva disparada, con su proyectil central.
    public static event System.Action<SpitProjectile> VolleyFired;
    // Matraca del Cobrador: mientras está activo se guardan las salvas en vez de dispararlas.
    public bool Holding { get; set; }
    public int StoredVolleys { get; set; }

    private void Awake()
    {
        player = GetComponent<PogoPlayer>();
        Instance = this;
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;
    }

    private void Update()
    {
        if (projectilePrefab == null || (player != null && PogoBridge.IsDead(player)))
            return;

        cooldown -= Time.deltaTime;
        if (cooldown > 0f)
            return;

        PlayerStats stats = PlayerStats.Instance;
        float range = stats != null ? stats.Get(PogoStat.Range) : 14f;
        Vector3 mouth = MouthPosition();
        EnemyTracker target = FindClosestEnemy(mouth, range);

        if (target == null)
        {
            cooldown = 0.1f;   // volvemos a buscar pronto
            return;
        }

        float rate = stats != null ? Mathf.Max(0.05f, stats.Get(PogoStat.FireRate)) : 1f;
        cooldown = 1f / rate;
        if (Holding)
        {
            StoredVolleys = Mathf.Min(4, StoredVolleys + 1);   // solo salvas que habrías disparado
            return;
        }
        Fire(mouth, target, stats, range);
    }

    private Vector3 MouthPosition()
    {
        Vector3 forward = Vector3.ProjectOnPlane(GetComponent<Rigidbody>().linearVelocity, Vector3.up);
        if (forward.sqrMagnitude < 0.1f)
            forward = transform.forward;
        Quaternion facing = Quaternion.LookRotation(forward.normalized, Vector3.up);
        return transform.position + facing * mouthOffset;
    }

    private readonly System.Collections.Generic.List<EnemyTracker> targets = new System.Collections.Generic.List<EnemyTracker>();
    private readonly System.Collections.Generic.List<float> targetDistances = new System.Collections.Generic.List<float>();

    // Los "count" enemigos más cercanos dentro del alcance (para varios proyectiles).
    private void FindClosestEnemies(Vector3 from, float range, int count)
    {
        targets.Clear();
        targetDistances.Clear();
        float max = range * range;

        foreach (EnemyTracker e in EnemyTracker.All)
        {
            if (e == null || e.IsDead || !e.Targetable)
                continue;

            float d = (e.Center - from).sqrMagnitude;
            if (d >= max)
                continue;

            int i = targets.Count;
            while (i > 0 && targetDistances[i - 1] > d)
                i--;
            if (i >= count)
                continue;
            targets.Insert(i, e);
            targetDistances.Insert(i, d);
            if (targets.Count > count)
            {
                targets.RemoveAt(count);
                targetDistances.RemoveAt(count);
            }
        }
    }

    private EnemyTracker FindClosestEnemy(Vector3 from, float range)
    {
        FindClosestEnemies(from, range, 1);
        return targets.Count > 0 ? targets[0] : null;
    }

    // Suelta varias salvas juntas (Matraca): hasta 12 proyectiles hacia los enemigos de enfrente.
    public void Burst(int volleys, Vector3 aimDirection)
    {
        PlayerStats stats = PlayerStats.Instance;
        float range = stats != null ? stats.Get(PogoStat.Range) : 14f;
        Vector3 mouth = MouthPosition();
        EnemyTracker best = null;
        float bestScore = float.MaxValue;
        foreach (EnemyTracker e in EnemyTracker.All)
        {
            if (e == null || e.IsDead || !e.Targetable) continue;
            Vector3 d = e.Center - mouth;
            if (d.magnitude > range * 1.3f) continue;
            float score = d.magnitude * (2f - Vector3.Dot(d.normalized, aimDirection.normalized));
            if (score < bestScore) { bestScore = score; best = e; }
        }
        if (best == null)
            return;
        int perVolley = stats != null ? Mathf.RoundToInt(stats.Get(PogoStat.Projectiles)) : 1;
        burstCount = Mathf.Clamp(perVolley * volleys, 1, 60);
        Fire(mouth, best, stats, range * 1.3f);
        burstCount = 0;
    }

    private int burstCount;

    private void Fire(Vector3 mouth, EnemyTracker target, PlayerStats stats, float range)
    {
        float baseDamage = stats != null ? stats.Get(PogoStat.SpitDamage) : 15f;
        float critChance = stats != null ? stats.Get(PogoStat.CritChance) : 0.1f;
        float critMultiplier = stats != null ? stats.Get(PogoStat.CritMultiplier) : 2f;
        float speed = stats != null ? stats.Get(PogoStat.ProjectileSpeed) : 22f;
        float area = stats != null ? stats.Get(PogoStat.SpitArea) : 0f;
        float size = stats != null ? Mathf.Max(0.3f, stats.Get(PogoStat.SpitSize)) : 1f;
        int bounces = stats != null ? Mathf.RoundToInt(stats.Get(PogoStat.SpitBounces)) : 0;
        int count = burstCount > 0 ? burstCount : stats != null ? Mathf.RoundToInt(stats.Get(PogoStat.Projectiles)) : 1;
        SpitProjectile central = null;

        // Cada proyectil va a un enemigo distinto; si hay menos enemigos, se reparten en abanico.
        FindClosestEnemies(mouth, range, count);
        if (targets.Count == 0)
            targets.Add(target);

        for (int i = 0; i < count; i++)
        {
            if (SpitProjectile.AliveCount > 220) break;   // tope de escupitajos en el aire
            EnemyTracker t = targets[i % targets.Count];
            int round = i / targets.Count;
            float spread = round == 0 ? 0f : (round % 2 == 1 ? 1f : -1f) * (8f + 6f * ((round - 1) / 2));

            float damage = baseDamage;
            bool crit = Random.value < critChance;
            if (crit)
                damage *= critMultiplier;

            Vector3 aim = t.Center - mouth;
            Quaternion rotation = Quaternion.AngleAxis(spread, Vector3.up) * Quaternion.LookRotation(aim);
            SpitProjectile projectile = Instantiate(projectilePrefab, mouth, rotation);
            projectile.Launch(t, damage, crit, speed, range * 1.6f / speed + 0.5f, transform, hitClip, critClip);
            projectile.SetUpgrades(area, size, rotation * Vector3.forward);
            projectile.Bounces = bounces;
            if (i == 0)
            {
                central = projectile;
                projectile.Central = true;
            }
        }
        if (central != null)
            VolleyFired?.Invoke(central);

        if (stats != null)
            stats.Shots++;

        if (spitClip != null)
        {
            audioSource.pitch = Random.Range(0.9f, 1.15f) - Mathf.Min(size - 1f, 1f) * 0.15f;
            audioSource.PlayOneShot(spitClip, spitVolume * GameSettings.SfxVolume);
        }
    }
}
