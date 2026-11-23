using UnityEngine;

// Proyectil del escupitajo: persigue un poco al objetivo, hace daño al primer
// enemigo que toca y salpica. La salpicadura hace daño en área a los de alrededor.
public class SpitProjectile : MonoBehaviour
{
    [SerializeField] private float radius = 0.12f;
    [Tooltip("Qué tanto corrige su rumbo hacia el objetivo (radianes por segundo).")]
    [SerializeField] private float homing = 5f;
    [SerializeField] private float gravityFactor = 0.15f;
    [SerializeField] private Material splatMaterial;
    [SerializeField] private Color splatColor = new Color(0.3f, 0.7f, 1f);
    [Tooltip("Fracción del daño que reciben los enemigos dentro del área.")]
    [SerializeField] private float splashDamageFraction = 0.5f;

    private static readonly RaycastHit[] castHits = new RaycastHit[24];
    // Escupitajos en el aire (para no pasar de un tope y que no se caiga el FPS).
    public static int AliveCount { get; private set; }
    private void OnEnable() => AliveCount++;
    private void OnDisable() => AliveCount--;
    private EnemyTracker target;
    private float damage;
    private bool crit;
    private Vector3 velocity;
    private float lifeLeft;
    private Transform owner;
    private AudioClip hitClip;
    private AudioClip critClip;
    private float area;
    private float sizeMultiplier = 1f;

    // Proyectil central de su salva (lo usan Salsa de la Casa y Muela de Rebote).
    public bool Central { get; set; }
    public float ExtraSplashRadius { get; set; }
    public float ExtraSplashFraction { get; set; }
    public float Damage => damage;
    // Rebotes que le quedan (mejora "Gargajo Saltarín"): al pegarle a un demonio salta al siguiente.
    public int Bounces { get; set; }
    private Enemy lastEnemy;
    private System.Collections.Generic.List<EnemyTracker> visited;
    public bool Crit => crit;
    // Impacto directo en un enemigo (proyectil, enemigo, punto).
    public static event System.Action<SpitProjectile, EnemyTracker, Vector3> DirectHit;

    public void Launch(EnemyTracker target, float damage, bool crit, float speed, float lifetime,
        Transform owner, AudioClip hitClip, AudioClip critClip)
    {
        this.target = target;
        this.damage = damage;
        this.crit = crit;
        this.owner = owner;
        this.hitClip = hitClip;
        this.critClip = critClip;
        lifeLeft = lifetime;
        velocity = (target.Center - transform.position).normalized * speed;

        if (crit)
            transform.localScale *= 1.3f;
    }

    // Mejoras: radio del daño en área, tamaño y dirección inicial (para el abanico).
    public void SetUpgrades(float areaRadius, float size, Vector3 direction)
    {
        area = areaRadius;
        sizeMultiplier = size;
        transform.localScale *= size;
        radius *= size;
        velocity = direction.normalized * velocity.magnitude;
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        lifeLeft -= dt;

        if (lifeLeft <= 0f)
        {
            Destroy(gameObject);
            return;
        }

        if (target != null && !target.IsDead)
        {
            Vector3 desired = (target.Center - transform.position).normalized * velocity.magnitude;
            velocity = Vector3.RotateTowards(velocity, desired, homing * dt, 0f);
        }

        velocity += Physics.gravity * gravityFactor * dt;

        Vector3 step = velocity * dt;
        if (TryHit(step, out RaycastHit hit))
        {
            OnHit(hit);
            return;
        }

        transform.position += step;
        if (velocity.sqrMagnitude > 0.01f)
            transform.rotation = Quaternion.LookRotation(velocity);
    }

    private bool TryHit(Vector3 step, out RaycastHit result)
    {
        result = default;
        float distance = step.magnitude;
        if (distance < 0.0001f)
            return false;

        // Con triggers: el Ojo Alado solo tiene un trigger. Los demás triggers (monedas, cofres...) se ignoran abajo.
        int count = Physics.SphereCastNonAlloc(transform.position, radius, step / distance, castHits, distance,
            ~0, QueryTriggerInteraction.Collide);

        float best = float.MaxValue;
        bool found = false;

        for (int i = 0; i < count; i++)
        {
            RaycastHit h = castHits[i];
            if (owner != null && h.collider.transform.IsChildOf(owner))
                continue;
            if (lastEnemy != null && h.collider.GetComponentInParent<Enemy>() == lastEnemy)
                continue;   // el que acaba de golpear (al rebotar sale de adentro de él)
            if (h.collider.isTrigger && h.collider.GetComponentInParent<OjoEnemy>() == null)
                continue;

            if (h.distance < best)
            {
                best = h.distance;
                result = h;
                found = true;
            }
        }

        return found;
    }

    private void OnHit(RaycastHit hit)
    {
        Enemy enemy = hit.collider.GetComponentInParent<Enemy>();
        Vector3 point = hit.point == Vector3.zero ? transform.position : hit.point;

        if (enemy != null)
        {
            EnemyTracker tracker = enemy.GetComponent<EnemyTracker>();
            if (tracker != null)
            {
                tracker.Hit(damage, HitSource.Spit, crit);
                DirectHit?.Invoke(this, tracker, point);
            }
            else   // Cualquier enemigo con un método TakeDamage(float) recibe el daño.
                enemy.SendMessage("TakeDamage", damage, SendMessageOptions.DontRequireReceiver);

            AudioClip clip = crit ? critClip : hitClip;
            if (clip != null)
                AudioSource.PlayClipAtPoint(clip, point, 0.9f * GameSettings.SfxVolume);
        }
        else if (hitClip != null)
        {
            AudioSource.PlayClipAtPoint(hitClip, point, 0.35f * GameSettings.SfxVolume);
        }

        SplashDamage(point, enemy);
        if (ExtraSplashRadius > 0f)
        {
            float r2 = ExtraSplashRadius * ExtraSplashRadius;
            nearby.Clear();
            foreach (EnemyTracker e in EnemyTracker.All)
                if (e != null && e.Enemy != enemy && !e.IsDead && (e.Center - point).sqrMagnitude < r2)
                    nearby.Add(e);
            foreach (EnemyTracker e in nearby)
                e.Hit(damage * ExtraSplashFraction, HitSource.Spit);
            ItemFx.Burst(point, 30, ExtraSplashRadius * 0.5f, new Color(1f, 0.35f, 0.1f), ItemEffects.Instance != null ? ItemEffects.Instance.FxMaterial : null, 6f, 0.35f, 0.5f);
        }
        SpawnSplat(point, enemy != null ? 10 : 6);
        if (enemy != null && Bounces > 0 && TryBounce(enemy, point))
            return;
        Destroy(gameObject);
    }

    private bool TryBounce(Enemy from, Vector3 point)
    {
        if (visited == null) visited = new System.Collections.Generic.List<EnemyTracker>();
        EnemyTracker fromTracker = from.GetComponent<EnemyTracker>();
        if (fromTracker != null) visited.Add(fromTracker);

        EnemyTracker best = null;
        float bestD = 9f * 9f;
        foreach (EnemyTracker e in EnemyTracker.All)
        {
            if (e == null || e.IsDead || !e.Targetable || visited.Contains(e)) continue;
            float d = (e.Center - point).sqrMagnitude;
            if (d < bestD) { bestD = d; best = e; }
        }
        if (best == null)
            return false;

        Bounces--;
        damage *= 0.85f;
        lastEnemy = from;
        target = best;
        transform.position = point;
        velocity = (best.Center - point).normalized * Mathf.Max(velocity.magnitude, 14f);
        lifeLeft = Mathf.Max(lifeLeft, 1.2f);
        return true;
    }

    private static readonly System.Collections.Generic.List<EnemyTracker> nearby = new System.Collections.Generic.List<EnemyTracker>();

    private void SplashDamage(Vector3 point, Enemy direct)
    {
        if (area <= 0.05f)
            return;

        nearby.Clear();
        float r2 = area * area;
        foreach (EnemyTracker e in EnemyTracker.All)
            if (e != null && e.Enemy != direct && !e.IsDead && (e.Center - point).sqrMagnitude < r2)
                nearby.Add(e);

        float splash = damage * splashDamageFraction;
        foreach (EnemyTracker e in nearby)
        {
            e.Hit(splash, HitSource.Spit);
        }
    }

    // Salpicadura (en el sistema de partículas compartido: antes era un objeto nuevo por cada golpe).
    private void SpawnSplat(Vector3 point, int count)
    {
        count = Mathf.RoundToInt(count * (1f + area * 0.4f));
        ItemFx.Burst(point, count, 0.1f, crit ? new Color(1f, 0.9f, 0.3f) : splatColor, splatMaterial,
            3f + area * 2f, 0.08f * sizeMultiplier, 0.4f);
    }
}
