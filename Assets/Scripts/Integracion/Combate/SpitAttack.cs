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

    private void Awake()
    {
        player = GetComponent<PogoPlayer>();
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

    private static EnemyTracker FindClosestEnemy(Vector3 from, float range)
    {
        EnemyTracker best = null;
        float bestDistance = range * range;

        foreach (EnemyTracker e in EnemyTracker.All)
        {
            if (e == null || e.IsDead)
                continue;

            float d = (e.Center - from).sqrMagnitude;
            if (d < bestDistance)
            {
                bestDistance = d;
                best = e;
            }
        }

        return best;
    }

    private void Fire(Vector3 mouth, EnemyTracker target, PlayerStats stats, float range)
    {
        float damage = stats != null ? stats.Get(PogoStat.SpitDamage) : 15f;
        float critChance = stats != null ? stats.Get(PogoStat.CritChance) : 0.1f;
        float critMultiplier = stats != null ? stats.Get(PogoStat.CritMultiplier) : 2f;
        float speed = stats != null ? stats.Get(PogoStat.ProjectileSpeed) : 22f;

        bool crit = Random.value < critChance;
        if (crit)
            damage *= critMultiplier;

        SpitProjectile projectile = Instantiate(projectilePrefab, mouth, Quaternion.LookRotation(target.Center - mouth));
        projectile.Launch(target, damage, crit, speed, range * 1.6f / speed + 0.5f, transform, hitClip, critClip);

        if (stats != null)
            stats.Shots++;

        if (spitClip != null)
        {
            audioSource.pitch = Random.Range(0.9f, 1.15f);
            audioSource.PlayOneShot(spitClip, spitVolume * GameSettings.SfxVolume);
        }
    }
}
