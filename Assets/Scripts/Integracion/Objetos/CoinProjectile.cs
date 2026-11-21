using System.Collections.Generic;
using UnityEngine;

// Monedas del Limosnero: salen disparadas del demonio que mataste con el pogo y buscan a otros.
// Hacen daño al primero que tocan y no se pueden recoger.
public class CoinProjectile : MonoBehaviour
{
    private const float Speed = 20f;
    private const float SearchRange = 14f;
    private const float HitRadius = 0.75f;

    private EnemyTracker target;
    private EnemyTracker ignore;
    private float damage;
    private float life = 1.8f;
    private float launchTime = 0.18f;
    private Vector3 velocity;
    private Transform visual;
    private Material fxMaterial;

    public static void Launch(Vector3 from, EnemyTracker killed, int count, float damage, Material coinMaterial, Material fxMaterial)
    {
        // Cada moneda va por un demonio distinto (si hay suficientes cerca).
        var taken = new List<EnemyTracker> { killed };
        for (int i = 0; i < count; i++)
        {
            EnemyTracker target = ItemEffects.Nearest(from, SearchRange, taken);
            if (target != null)
                taken.Add(target);

            float a = (i + Random.value * 0.5f) / count * Mathf.PI * 2f;
            Vector3 dir = new Vector3(Mathf.Cos(a), 0.9f, Mathf.Sin(a)).normalized;

            var go = new GameObject("MonedaLimosnero");
            go.transform.position = from;
            var coin = go.AddComponent<CoinProjectile>();
            coin.target = target;
            coin.ignore = killed;
            coin.damage = damage;
            coin.fxMaterial = fxMaterial;
            coin.velocity = dir * 9f;
            coin.Build(coinMaterial);
        }
    }

    private void Build(Material coinMaterial)
    {
        var cylinder = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Destroy(cylinder.GetComponent<Collider>());
        visual = cylinder.transform;
        visual.SetParent(transform, false);
        visual.localScale = new Vector3(0.34f, 0.03f, 0.34f);
        var r = cylinder.GetComponent<Renderer>();
        if (coinMaterial != null)
            r.sharedMaterial = coinMaterial;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        ItemFx.Trail(gameObject, new Color(1f, 0.85f, 0.3f), 0.14f, 0.2f, fxMaterial);
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        life -= dt;
        launchTime -= dt;
        if (life <= 0f)
        {
            Destroy(gameObject);
            return;
        }

        visual.Rotate(720f * dt, 0f, 300f * dt, Space.Self);

        if (launchTime <= 0f)
        {
            if (target == null || target.IsDead)
                target = ItemEffects.Nearest(transform.position, SearchRange, new[] { ignore });

            if (target != null)
                velocity = Vector3.MoveTowards(velocity, (target.Center - transform.position).normalized * Speed, 90f * dt);
            else
                velocity += Vector3.down * 9.8f * dt;
        }
        else
        {
            velocity += Vector3.down * 6f * dt;   // primero brincan hacia arriba como piñata
        }

        transform.position += velocity * dt;
        if (launchTime > 0f)
            return;   // primero se ven salir volando

        // Golpea al primer demonio que toque (no solo al que perseguía).
        foreach (EnemyTracker e in EnemyTracker.All)
        {
            if (e == null || e == ignore || e.IsDead)
                continue;
            if ((e.Center - transform.position).sqrMagnitude < HitRadius * HitRadius * 2.2f)
            {
                e.Hit(damage);
                ItemFx.Burst(transform.position, 8, 0.15f, new Color(1f, 0.85f, 0.3f), fxMaterial, 3f, 0.14f, 0.3f);
                Destroy(gameObject);
                return;
            }
        }
    }
}
