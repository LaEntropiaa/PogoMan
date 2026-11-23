using UnityEngine;

// Orbe de los guardianes: núcleo brillante, cáscara, tres lunitas que giran alrededor y estela,
// del color de cada guardián. Persigue un rato a Pogoman y explota con onda en el piso.
public class GuardianOrb : MonoBehaviour
{
    private Transform target;
    private Vector3 velocity;
    private float damage, life = 5f, homing = 1.6f, age;
    private Color color;
    private Transform shell, moons;
    private Light glow;
    private float damageScale;

    public static Color ColorFor(GuardianKind k) =>
        k == GuardianKind.Fundidor ? new Color(1f, 0.45f, 0.1f)
        : k == GuardianKind.Viuda ? new Color(0.75f, 0.3f, 1f)
        : k == GuardianKind.Recaudador ? new Color(1f, 0.82f, 0.2f)
        : new Color(0.3f, 1f, 0.9f);

    public static void Fire(Vector3 from, Transform target, float damage, Color color)
    {
        var go = new GameObject("OrbeGuardian");
        go.transform.position = from;
        var o = go.AddComponent<GuardianOrb>();
        o.target = target;
        o.damage = damage;
        o.color = color;
        Vector3 dir = target != null ? (target.position + Vector3.up * 0.8f - from).normalized : Vector3.forward;
        o.velocity = (dir + Vector3.up * 0.35f).normalized * 9f;
        o.Build();
    }

    private GameObject Part(PrimitiveType type, Transform parent, Vector3 pos, float size, Color c, float glowAmount)
    {
        GameObject g = GameObject.CreatePrimitive(type);
        Destroy(g.GetComponent<Collider>());
        g.transform.SetParent(parent, false);
        g.transform.localPosition = pos;
        g.transform.localScale = Vector3.one * size;
        ItemFx.Paint(g, c, glowAmount);
        return g;
    }

    private void Build()
    {
        Part(PrimitiveType.Sphere, transform, Vector3.zero, 0.38f, Color.Lerp(color, Color.white, 0.65f), 3f);   // núcleo
        shell = Part(PrimitiveType.Cube, transform, Vector3.zero, 0.62f, color, 1.4f).transform;              // cáscara facetada
        moons = new GameObject("Lunas").transform;
        moons.SetParent(transform, false);
        for (int i = 0; i < 3; i++)
        {
            float a = i / 3f * Mathf.PI * 2f;
            Part(PrimitiveType.Cube, moons, new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 0.62f, 0.14f, Color.Lerp(color, Color.white, 0.3f), 2f);
        }
        Material fx = ItemEffects.Instance != null ? ItemEffects.Instance.FxMaterial : null;
        ItemFx.Trail(gameObject, color, 0.45f, 0.35f, fx);
        glow = gameObject.AddComponent<Light>();
        glow.type = LightType.Point;
        glow.color = color;
        glow.range = 5f;
        glow.intensity = 2.5f;
        glow.shadows = LightShadows.None;
    }

    private void Update()
    {
        if (TimeStop.Active) return;
        float dt = Time.deltaTime;
        age += dt;
        life -= dt;
        homing -= dt;
        if (target != null && homing > 0f)
        {
            Vector3 want = (target.position + Vector3.up * 0.8f - transform.position).normalized * 11f;
            velocity = Vector3.MoveTowards(velocity, want, 14f * dt);
        }
        Vector3 next = transform.position + velocity * dt;
        shell.Rotate(140f * dt, 220f * dt, 0f);
        moons.Rotate(0f, 400f * dt, 0f, Space.Self);
        moons.localRotation *= Quaternion.Euler(90f * dt, 0f, 0f);
        float pulse = 1f + Mathf.Sin(age * 18f) * 0.08f;
        transform.localScale = Vector3.one * pulse;

        if (target != null && (target.position + Vector3.up * 0.8f - next).sqrMagnitude < 0.9f * 0.9f)
        {
            var p = target.GetComponent<PogoPlayer>();
            if (p != null && !PogoBridge.IsDead(p) && GuardianKit.TryHitPlayer())
                PlayerVitals.Damage(p, damage);
            Pop(next);
            return;
        }
        if (age > 0.2f && Physics.Linecast(transform.position, next, out RaycastHit hit, ~0, QueryTriggerInteraction.Ignore) &&
            hit.rigidbody == null && hit.collider.GetComponentInParent<Enemy>() == null)
        {
            Pop(hit.point);
            return;
        }
        transform.position = next;
        if (life <= 0f) Pop(next);
    }

    private void Pop(Vector3 at)
    {
        Material fx = ItemEffects.Instance != null ? ItemEffects.Instance.FxMaterial : null;
        ItemFx.Burst(at, 14, 0.4f, color, fx, 5f, 0.3f, 0.4f);
        Vector3 ground = at;
        if (Physics.Raycast(at + Vector3.up * 0.5f, Vector3.down, out RaycastHit h, 4f, ~0, QueryTriggerInteraction.Ignore)) ground = h.point;
        FxKit.Ring(ground, 2.2f, Color.Lerp(color, Color.white, 0.5f), color);
        EnemyTracker.HitArea(at, 2f, damage * Guardian.DemonDamageFactor, HitSource.Other, true, 2.5f, null, true);
        Destroy(gameObject);
    }
}
