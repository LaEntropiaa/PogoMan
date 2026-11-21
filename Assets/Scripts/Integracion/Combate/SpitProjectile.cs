using UnityEngine;

// Proyectil del escupitajo: persigue un poco al objetivo, hace daño al primer
// enemigo que toca y salpica.
public class SpitProjectile : MonoBehaviour
{
    [SerializeField] private float radius = 0.12f;
    [Tooltip("Qué tanto corrige su rumbo hacia el objetivo (radianes por segundo).")]
    [SerializeField] private float homing = 5f;
    [SerializeField] private float gravityFactor = 0.15f;
    [SerializeField] private Material splatMaterial;
    [SerializeField] private Color splatColor = new Color(0.55f, 1f, 0.2f);

    private EnemyTracker target;
    private float damage;
    private bool crit;
    private Vector3 velocity;
    private float lifeLeft;
    private Transform owner;
    private AudioClip hitClip;
    private AudioClip critClip;

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
                tracker.MarkIncomingSpit(crit);

            // Cualquier enemigo con un método TakeDamage(float) recibe el daño.
            enemy.SendMessage("TakeDamage", damage, SendMessageOptions.DontRequireReceiver);

            AudioClip clip = crit ? critClip : hitClip;
            if (clip != null)
                AudioSource.PlayClipAtPoint(clip, point, 0.9f * GameSettings.SfxVolume);
        }
        else if (hitClip != null)
        {
            AudioSource.PlayClipAtPoint(hitClip, point, 0.35f * GameSettings.SfxVolume);
        }

        SpawnSplat(point, enemy != null ? 10 : 6);
        Destroy(gameObject);
    }

    private void SpawnSplat(Vector3 point, int count)
    {
        var go = new GameObject("Salpicadura");
        go.transform.position = point;
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.duration = 0.3f;
        main.loop = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.5f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 3.5f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.08f);
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
