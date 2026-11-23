using UnityEngine;

// Bola de fuego del Diablo: vuela en arco hacia donde estaba (o estará) Pogoman.
// Marca en el suelo dónde va a caer y explota con daño en área.
public class Fireball : MonoBehaviour
{
    private const float Gravity = 14f;

    private Vector3 velocity;
    private Vector3 target;
    private float damage;
    private float radius;
    private float life;
    private Material fxMaterial;
    private AudioClip explodeClip;
    private ParticleSystem warning;
    private Transform warningRoot;

    public static void Launch(Vector3 from, Vector3 target, float flightTime, float damage, float radius,
        Material ballMaterial, Material fxMaterial, AudioClip explodeClip)
    {
        var go = new GameObject("BolaDeFuego");
        go.transform.position = from;
        var f = go.AddComponent<Fireball>();
        f.target = target;
        f.damage = damage;
        f.radius = radius;
        f.fxMaterial = fxMaterial;
        f.explodeClip = explodeClip;
        f.life = flightTime + 1.5f;

        // Tiro parabólico que llega al objetivo en flightTime.
        Vector3 d = target - from;
        f.velocity = new Vector3(d.x / flightTime, (d.y + 0.5f * Gravity * flightTime * flightTime) / flightTime, d.z / flightTime);
        f.Build(ballMaterial);
    }

    private void Build(Material ballMaterial)
    {
        var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Destroy(sphere.GetComponent<Collider>());
        sphere.transform.SetParent(transform, false);
        sphere.transform.localScale = Vector3.one * 0.9f * Mathf.Clamp(radius / 2.4f, 0.5f, 2.5f);   // más área = bola más grande
        var r = sphere.GetComponent<Renderer>();
        if (ballMaterial != null)
            r.sharedMaterial = ballMaterial;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        var light = gameObject.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = new Color(1f, 0.45f, 0.1f);
        light.range = 7f;
        light.intensity = 3f;
        light.shadows = LightShadows.None;

        // Estela de flamas
        var ps = gameObject.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.5f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 1f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.35f, 0.8f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.85f, 0.3f), new Color(1f, 0.25f, 0.05f));
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 150;
        var em = ps.emission;
        em.rateOverTime = 80f;
        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.35f;
        ItemFx.FadeOut(ps);
        ItemFx.Apply(ps, fxMaterial);
        ps.Play();

        // Aviso en el suelo: un anillo de chispas rojas donde va a caer.
        warningRoot = new GameObject("AvisoBola").transform;
        warningRoot.position = target + Vector3.up * 0.1f;
        warningRoot.rotation = Quaternion.Euler(-90f, 0f, 0f);
        warning = warningRoot.gameObject.AddComponent<ParticleSystem>();
        warning.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var wm = warning.main;
        wm.startLifetime = 0.35f;
        wm.startSpeed = 0.4f;
        wm.startSize = 0.28f;
        wm.startColor = new Color(1f, 0.15f, 0.05f, 0.9f);
        wm.maxParticles = 200;
        var we = warning.emission;
        we.rateOverTime = 120f;
        var wsh = warning.shape;
        wsh.shapeType = ParticleSystemShapeType.Circle;
        wsh.radius = radius;
        wsh.radiusThickness = 0f;
        ItemFx.Apply(warning, fxMaterial);
        warning.Play();
    }

    private void Update()
    {
        if (TimeStop.Active)
            return;   // la bola se queda congelada en el aire

        float dt = Time.deltaTime;
        life -= dt;
        Vector3 prev = transform.position;
        velocity += Vector3.down * Gravity * dt;
        Vector3 next = prev + velocity * dt;

        // Choca con el suelo/muros (no con demonios ni con Pogoman: esos los daña la explosión).
        Vector3 step = next - prev;
        if (Physics.Raycast(prev, step.normalized, out RaycastHit hit, step.magnitude + 0.3f, ~0, QueryTriggerInteraction.Ignore) &&
            hit.rigidbody == null)
        {
            Explode(hit.point);
            return;
        }

        transform.position = next;
        if (life <= 0f || (velocity.y < 0f && next.y < target.y - 0.2f))
            Explode(next);
    }

    private void Explode(Vector3 at)
    {
        ItemFx.Burst(at + Vector3.up * 0.4f, Mathf.RoundToInt(25f * radius / 2.4f + 25f), radius * 0.5f, new Color(1f, 0.5f, 0.1f), fxMaterial, 9f * Mathf.Max(1f, radius / 2.4f), 0.7f, 0.6f);
        ItemFx.Burst(at + Vector3.up * 0.2f, 25, radius * 0.3f, new Color(0.3f, 0.2f, 0.2f), fxMaterial, 4f, 0.9f, 0.9f);
        if (explodeClip != null)
            AudioSource.PlayClipAtPoint(explodeClip, at, 0.9f * GameSettings.SfxVolume);

        PogoPlayer player = ArenaFight.Player;
        if (damage > 0f && player != null && !PogoBridge.IsDead(player))   // los meteoros de adorno no pegan
        {
            Vector3 d = player.transform.position - at;
            if (d.magnitude <= radius + 0.6f)
            {
                PlayerVitals.Damage(player, damage);
                // Empujoncito hacia arriba y afuera.
                var rb = player.GetComponent<Rigidbody>();
                Vector3 push = new Vector3(d.x, 0f, d.z).normalized * 5f + Vector3.up * 6f;
                rb.linearVelocity = new Vector3(rb.linearVelocity.x + push.x, Mathf.Max(rb.linearVelocity.y, push.y), rb.linearVelocity.z + push.z);
            }
        }

        if (warningRoot != null)
            Destroy(warningRoot.gameObject);
        Destroy(gameObject);
    }

    private void OnDestroy()
    {
        if (warningRoot != null)
            Destroy(warningRoot.gameObject);
    }
}
