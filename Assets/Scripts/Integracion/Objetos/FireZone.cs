using UnityEngine;

// Fuego en el suelo de la Veladora de la Santa Muerte: quema a los demonios que lo pisan.
public class FireZone : MonoBehaviour
{
    public static int ActiveCount { get; private set; }

    private const float TickInterval = 0.5f;

    private float radius;
    private float duration;
    private float tickDamage;
    private float age;
    private float tickTimer;
    private ParticleSystem flames;
    private Light glow;

    public static FireZone Spawn(Vector3 ground, float radius, float duration, float tickDamage, Material material)
    {
        var go = new GameObject("FuegoVeladora");
        go.transform.position = ground + Vector3.up * 0.05f;
        var fire = go.AddComponent<FireZone>();
        fire.radius = radius;
        fire.duration = duration;
        fire.tickDamage = tickDamage;
        fire.Build(material);
        return fire;
    }

    private void OnEnable() => ActiveCount++;
    private void OnDisable() => ActiveCount--;

    private void Build(Material material)
    {
        flames = gameObject.AddComponent<ParticleSystem>();
        flames.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = flames.main;
        main.duration = duration;
        main.loop = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.7f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.3f, 1.2f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.25f, 0.55f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.85f, 0.25f), new Color(1f, 0.3f, 0.05f));
        main.gravityModifier = -0.35f;   // sube como flama
        main.maxParticles = 120;
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        var emission = flames.emission;
        emission.rateOverTime = 55f * radius;

        var shape = flames.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = radius * 0.9f;
        transform.rotation = Quaternion.Euler(-90f, 0f, 0f);   // el círculo queda acostado en el piso

        var color = flames.colorOverLifetime;
        color.enabled = true;
        color.color = new Gradient
        {
            colorKeys = new[] { new GradientColorKey(new Color(1f, 0.9f, 0.4f), 0f), new GradientColorKey(new Color(0.9f, 0.15f, 0.02f), 1f) },
            alphaKeys = new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.8f, 0.5f), new GradientAlphaKey(0f, 1f) },
        };
        ItemFx.FadeOut(flames);
        ItemFx.Apply(flames, material);
        flames.Play();

        // Poquitas a la vez (máximo 24), así que una luz por fuego está bien.
        var lightGo = new GameObject("Luz");
        lightGo.transform.SetParent(transform, false);
        lightGo.transform.position = transform.position + Vector3.up * 0.6f;
        glow = lightGo.AddComponent<Light>();
        glow.type = LightType.Point;
        glow.color = new Color(1f, 0.5f, 0.15f);
        glow.range = radius * 3f;
        glow.intensity = 0f;
        glow.shadows = LightShadows.None;
    }

    private void Update()
    {
        if (TimeStop.Active)
            return;   // el fuego también se congela

        age += Time.deltaTime;
        float fade = Mathf.Clamp01(age / 0.2f) * Mathf.Clamp01((duration - age) / 0.5f);
        if (glow != null)
            glow.intensity = (2.2f + Mathf.Sin(age * 23f) * 0.4f + Mathf.Sin(age * 37f) * 0.3f) * fade;

        tickTimer -= Time.deltaTime;
        if (tickTimer <= 0f && age < duration)
        {
            tickTimer = TickInterval;
            Burn();
        }

        if (age >= duration)
        {
            var emission = flames.emission;
            emission.enabled = false;
            if (age >= duration + 0.8f)
                Destroy(gameObject);
        }
    }

    private void Burn()
    {
        Vector3 c = transform.position;
        float r2 = radius * radius;
        for (int i = EnemyTracker.All.Count - 1; i >= 0; i--)
        {
            EnemyTracker e = EnemyTracker.All[i];
            if (e == null || e.IsDead)
                continue;
            Vector3 p = e.Center;
            Vector3 d = p - c;
            if (d.y < -1f || d.y > 2.5f)
                continue;
            d.y = 0f;
            if (d.sqrMagnitude <= r2)
                e.Hit(tickDamage);
        }
    }
}
