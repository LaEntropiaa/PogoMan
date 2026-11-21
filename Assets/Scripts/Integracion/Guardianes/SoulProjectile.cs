using UnityEngine;

// Alma del Barquero: espera en la proa y, al lanzarla, persigue un poquito (máx. 30°/s
// durante 0.6 s) y luego sigue derecho. Desaparece al pegar, tocar una pared o a los 2.5 s.
public class SoulProjectile : MonoBehaviour
{
    private const float Speed = 3.8f;
    private const float TurnRate = 30f;
    private const float TurnTime = 0.6f;
    private const float Life = 2.5f;

    public bool Launched { get; private set; }
    private Transform target;
    private float damage;
    private float age;
    private Vector3 dir;
    private AudioClip hitClip;
    private Material fx;
    private float phase = Random.value * 10f;

    public static GameObject Spawn(Vector3 at, Material soulMat, Material fxMat)
    {
        var go = new GameObject("AlmaDelBarquero");
        go.transform.position = at;
        var s = go.AddComponent<SoulProjectile>();
        s.fx = fxMat;
        var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Destroy(sphere.GetComponent<Collider>());
        sphere.transform.SetParent(go.transform, false);
        sphere.transform.localScale = new Vector3(0.45f, 0.55f, 0.45f);
        var r = sphere.GetComponent<Renderer>();
        if (soulMat != null) r.sharedMaterial = soulMat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        ItemFx.Trail(go, new Color(0.4f, 1f, 0.9f), 0.3f, 0.5f, fxMat);
        var l = go.AddComponent<Light>();
        l.type = LightType.Point; l.color = new Color(0.4f, 1f, 0.9f); l.range = 3.5f; l.intensity = 1.6f; l.shadows = LightShadows.None;
        ItemFx.Burst(at, 12, 0.3f, new Color(0.4f, 1f, 0.9f), fxMat, 2f, 0.25f, 0.5f);
        return go;
    }

    public void Launch(Transform target, float damage, AudioClip hitClip)
    {
        this.target = target;
        this.damage = damage;
        this.hitClip = hitClip;
        dir = (target.position + Vector3.up * 0.6f - transform.position).normalized;
        Launched = true;
    }

    private void Update()
    {
        // Flota en su lugar hasta que la lanzan.
        transform.position += Vector3.up * Mathf.Sin(Time.time * 6f + phase) * 0.002f;
        if (!Launched || TimeStop.Active)
            return;

        float dt = Time.deltaTime;
        age += dt;
        if (target != null && age <= TurnTime)
        {
            Vector3 want = (target.position + Vector3.up * 0.6f - transform.position).normalized;
            dir = Vector3.RotateTowards(dir, want, TurnRate * Mathf.Deg2Rad * dt, 0f);
        }
        Vector3 next = transform.position + dir * Speed * dt;

        if (target != null && (target.position + Vector3.up * 0.5f - next).sqrMagnitude < 0.75f * 0.75f)
        {
            var p = target.GetComponent<PogoPlayer>();
            if (p != null && !PogoBridge.IsDead(p) && GuardianKit.TryHitPlayer())
                PlayerVitals.Damage(p, damage);   // respeta la invulnerabilidad global
            Pop(next);
            return;
        }
        if (Physics.Linecast(transform.position, next, out RaycastHit hit, ~0, QueryTriggerInteraction.Ignore) &&
            hit.rigidbody == null && hit.collider.GetComponentInParent<Enemy>() == null)
        {
            Pop(hit.point);
            return;
        }
        transform.position = next;
        if (age >= Life)
            Pop(next);
    }

    private void Pop(Vector3 at)
    {
        ItemFx.Burst(at, 18, 0.3f, new Color(0.4f, 1f, 0.9f), fx, 3.5f, 0.3f, 0.5f);
        if (hitClip != null)
            AudioSource.PlayClipAtPoint(hitClip, at, 0.7f * GameSettings.SfxVolume);
        Destroy(gameObject);
    }
}
