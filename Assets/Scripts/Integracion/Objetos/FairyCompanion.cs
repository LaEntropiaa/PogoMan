using UnityEngine;

// "Comensal de más" del Cuadro de la Última Cena: una esferita tipo hada que vuela junto a
// Pogoman y cada 2 s le dispara al demonio más cercano (25 % del daño del pogo).
public class FairyCompanion : MonoBehaviour
{
    private static readonly Color FairyColor = new Color(0.75f, 1f, 0.85f);

    private ItemEffects owner;
    private Material fxMaterial;
    private int slot;
    private int total = 1;
    private float timer;
    private float phase;
    private Vector3 velocity;
    private Transform body;
    private Light glow;

    public static FairyCompanion Spawn(ItemEffects owner, int slot, Material bodyMaterial, Material fxMaterial)
    {
        var go = new GameObject("Hada_" + slot);
        var fairy = go.AddComponent<FairyCompanion>();
        fairy.owner = owner;
        fairy.fxMaterial = fxMaterial;
        fairy.slot = slot;
        fairy.timer = 0.6f + slot * 0.4f;   // que no disparen todas al mismo tiempo
        fairy.Build(bodyMaterial);
        if (owner.Player != null)
            go.transform.position = owner.Player.position + Vector3.up * 2f;
        return fairy;
    }

    public void SetSlot(int index, int count)
    {
        slot = index;
        total = Mathf.Max(1, count);
    }

    private void Build(Material bodyMaterial)
    {
        var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Destroy(sphere.GetComponent<Collider>());
        sphere.name = "Cuerpo";
        body = sphere.transform;
        body.SetParent(transform, false);
        body.localScale = Vector3.one * 0.26f;
        var r = sphere.GetComponent<Renderer>();
        if (bodyMaterial != null)
            r.sharedMaterial = bodyMaterial;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        // Alitas: dos esferas aplastadas que aletean.
        for (int side = -1; side <= 1; side += 2)
        {
            var wing = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(wing.GetComponent<Collider>());
            wing.name = side < 0 ? "AlaIzq" : "AlaDer";
            wing.transform.SetParent(body, false);
            wing.transform.localPosition = new Vector3(side * 0.6f, 0.25f, -0.1f);
            wing.transform.localScale = new Vector3(0.9f, 0.12f, 0.55f);
            var wr = wing.GetComponent<Renderer>();
            if (bodyMaterial != null)
                wr.sharedMaterial = bodyMaterial;
            wr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }


        glow = gameObject.AddComponent<Light>();
        glow.type = LightType.Point;
        glow.color = FairyColor;
        glow.range = 3.5f;
        glow.intensity = 1.2f;
        glow.shadows = LightShadows.None;
    }

    private void Update()
    {
        if (owner == null || owner.Player == null)
            return;

        float dt = Time.deltaTime;
        phase += dt;

        // Vuela en círculo alrededor de la cabeza de Pogoman, repartidas si hay varias.
        float angle = phase * 1.4f + slot * Mathf.PI * 2f / total;
        float radius = 1.1f + 0.15f * (total - 1);
        Vector3 target = owner.Player.position + new Vector3(Mathf.Cos(angle) * radius, 1.7f + Mathf.Sin(phase * 3f + slot) * 0.2f, Mathf.Sin(angle) * radius);
        transform.position = Vector3.SmoothDamp(transform.position, target, ref velocity, 0.18f);

        // Aleteo
        float flap = Mathf.Sin(phase * 40f) * 35f;
        for (int i = 0; i < body.childCount; i++)
        {
            Transform w = body.GetChild(i);
            float side = w.localPosition.x < 0f ? -1f : 1f;
            w.localRotation = Quaternion.Euler(0f, 0f, side * flap);
        }
        glow.intensity = 1.1f + Mathf.Sin(phase * 6f) * 0.25f;

        timer -= dt;
        if (timer > 0f)
            return;

        EnemyTracker enemy = ItemEffects.Nearest(transform.position, owner.FairyRange);
        if (enemy == null)
        {
            timer = 0.25f;   // vuelve a buscar pronto
            return;
        }

        timer = owner.FairyInterval;
        FairyBolt.Fire(transform.position, enemy, owner.FairyDamage, fxMaterial);
        owner.Play(owner.FairyShotClip, Random.Range(0.95f, 1.15f), 0.5f);
    }
}

// Disparo del hada: chispa que persigue al enemigo.
public class FairyBolt : MonoBehaviour
{
    private EnemyTracker target;
    private float damage;
    private float life = 2f;
    private Vector3 velocity;
    private Material fxMaterial;

    public static void Fire(Vector3 from, EnemyTracker target, float damage, Material fxMaterial)
    {
        var go = new GameObject("ChispaHada");
        go.transform.position = from;
        var bolt = go.AddComponent<FairyBolt>();
        bolt.target = target;
        bolt.damage = damage;
        bolt.fxMaterial = fxMaterial;
        bolt.velocity = (Vector3.up + Random.insideUnitSphere * 0.8f).normalized * 6f;
        // Sin partículas: una bolita brillante.
        var ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Destroy(ball.GetComponent<Collider>());
        ball.transform.SetParent(go.transform, false);
        ball.transform.localScale = Vector3.one * 0.22f;
        ItemFx.Paint(ball, new Color(0.7f, 1f, 0.85f), 2f);
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        life -= dt;
        if (life <= 0f || target == null || target.IsDead)
        {
            Destroy(gameObject);
            return;
        }

        Vector3 to = target.Center - transform.position;
        if (to.sqrMagnitude < 0.6f * 0.6f)
        {
            target.Hit(damage);
            Destroy(gameObject);
            return;
        }

        // Curva bonita al principio y luego directo al objetivo.
        velocity = Vector3.MoveTowards(velocity, to.normalized * 26f, 70f * dt);
        transform.position += velocity * dt;
    }
}
