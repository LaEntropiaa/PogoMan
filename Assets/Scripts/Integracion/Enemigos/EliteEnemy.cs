using UnityEngine;

// Demonio ÉLITE: cada 100 apariciones sale uno (de cualquier tipo). Un poco más grande, con corona,
// ×3 de vida y ×2 de daño (los pone EnemyDirector). Suelta más experiencia y monedas, y con 30 %
// deja un cofre morado que se abre solo al acercarte.
public class EliteEnemy : MonoBehaviour
{
    public const float HealthFactor = 3f;
    public const float DamageFactor = 2f;
    public const float SizeFactor = 1.3f;
    public static float ChestChance = 0.3f;   // (no const: las pruebas lo suben a 1)

    public static readonly Color CrownGold = new Color(1f, 0.78f, 0.2f);
    public static readonly Color ElitePurple = new Color(0.75f, 0.25f, 1f);

    private EnemyTracker tracker;
    private Transform crown;
    private float phase;
    private ParticleSystem aura;
    private static bool subscribed;

    public static int Spawned { get; private set; }
    public static int ChestsDropped { get; private set; }

    public static EliteEnemy Make(Enemy enemy)
    {
        if (!subscribed)
        {
            subscribed = true;
            EnemyTracker.EliteKilled += OnEliteKilled;
        }
        Spawned++;
        enemy.transform.localScale *= SizeFactor;
        var elite = enemy.gameObject.AddComponent<EliteEnemy>();
        elite.tracker = enemy.GetComponent<EnemyTracker>();
        if (elite.tracker != null) elite.tracker.IsElite = true;
        elite.BuildCrown();
        return elite;
    }

    private void BuildCrown()
    {
        crown = new GameObject("CoronaElite").transform;   // no es hija: no hereda la escala ni los giros del demonio
        GameObject Part(PrimitiveType type, Vector3 pos, Vector3 scale, Quaternion rot, Color c, float glow)
        {
            GameObject g = GameObject.CreatePrimitive(type);
            Destroy(g.GetComponent<Collider>());
            g.transform.SetParent(crown, false);
            g.transform.localPosition = pos;
            g.transform.localScale = scale;
            g.transform.localRotation = rot;
            ItemFx.Paint(g, c, glow);
            return g;
        }
        // Aro.
        Part(PrimitiveType.Cylinder, Vector3.zero, new Vector3(0.62f, 0.07f, 0.62f), Quaternion.identity, CrownGold, 0.6f);
        // Picos alrededor, con una gema en cada punta.
        for (int i = 0; i < 5; i++)
        {
            float a = i / 5f * Mathf.PI * 2f;
            Vector3 p = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 0.27f;
            Part(PrimitiveType.Cube, p + Vector3.up * 0.13f, new Vector3(0.09f, 0.24f, 0.09f), Quaternion.Euler(0f, -a * Mathf.Rad2Deg, 45f), CrownGold, 0.6f);
            Part(PrimitiveType.Sphere, p + Vector3.up * 0.28f, Vector3.one * 0.08f, Quaternion.identity, ElitePurple, 1.5f);
        }

        // Chispitas moradas alrededor del demonio.
        var go = new GameObject("AuraElite");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = Vector3.up * 0.8f;
        aura = go.AddComponent<ParticleSystem>();
        aura.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = aura.main;
        main.startLifetime = 0.9f;
        main.startSpeed = 0.6f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.18f);
        main.startColor = new Color(ElitePurple.r, ElitePurple.g, ElitePurple.b, 0.8f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = -0.2f;
        main.maxParticles = 40;
        var em = aura.emission;
        em.rateOverTime = EndlessMode.Active ? 0f : 5f;   // en el infinito hay muchos élites: sin aura
        var shape = aura.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.6f;
        ItemFx.FadeOut(aura);
        ItemFx.Apply(aura, ItemEffects.Instance != null ? ItemEffects.Instance.FxMaterial : null);
        aura.Play();
    }

    private void LateUpdate()
    {
        if (crown == null)
            return;
        if (tracker == null || tracker.IsDead)
        {
            Destroy(crown.gameObject);
            if (aura != null) aura.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            return;
        }
        phase += Time.deltaTime;
        crown.position = tracker.Top + Vector3.up * (0.35f + Mathf.Sin(phase * 2.4f) * 0.06f);
        crown.rotation = Quaternion.Euler(Mathf.Sin(phase * 1.7f) * 6f, phase * 50f, Mathf.Cos(phase * 1.3f) * 6f);
    }

    private void OnDestroy()
    {
        if (crown != null) Destroy(crown.gameObject);
    }

    private static void OnEliteKilled(EnemyTracker e)
    {
        Material fx = ItemEffects.Instance != null ? ItemEffects.Instance.FxMaterial : null;
        ItemFx.Burst(e.Center, 50, 0.6f, ElitePurple, fx, 7f, 0.3f, 0.6f);
        ItemFx.Burst(e.Top, 25, 0.3f, CrownGold, fx, 5f, 0.2f, 0.5f);
        if (Random.value >= (EndlessMode.Active ? EndlessMode.EliteChestChance : ChestChance))
            return;
        Chest chest = ChestSpawner.Spawn(e.transform.position, 0, true);
        if (chest == null)
            return;
        chest.MakeElite();
        ChestsDropped++;
    }
}
