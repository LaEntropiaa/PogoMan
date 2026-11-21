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

        RaycastHit[] hits = Physics.SphereCastAll(transform.position, radius, step / distance, distance,
            ~0, QueryTriggerInteraction.Ignore);

        float best = float.MaxValue;
        bool found = false;

        foreach (RaycastHit h in hits)
        {
            if (owner != null && h.collider.transform.IsChildOf(owner))
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
                tracker.Hit(damage, HitSource.Spit, crit);
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
        SpawnSplat(point, enemy != null ? 10 : 6);
        Destroy(gameObject);
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

    private void SpawnSplat(Vector3 point, int count)
    {
        count = Mathf.RoundToInt(count * (1f + area * 0.8f));
        var go = new GameObject("Salpicadura");
        go.transform.position = point;
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.duration = 0.3f;
        main.loop = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.5f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 3.5f + area * 2.5f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.03f * sizeMultiplier, 0.08f * sizeMultiplier);
        main.startColor = crit ? new Color(1f, 0.9f, 0.3f) : splatColor;
        main.gravityModifier = 1.5f;
        main.maxParticles = count;
        main.stopAction = ParticleSystemStopAction.Destroy;

        var emission = ps.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.1f;

        if (splatMaterial != null)
            go.GetComponent<ParticleSystemRenderer>().sharedMaterial = splatMaterial;

        ps.Play();
    }
}
