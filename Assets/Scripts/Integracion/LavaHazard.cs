using UnityEngine;

// La lava hace daño: si Pogoman la toca, pierde vida y sale disparado hacia
// arriba (para que pueda escapar). Los enemigos que caen también se queman.
// Va en el objeto de la lava (necesita su collider).
public class LavaHazard : MonoBehaviour
{
    [Header("Jugador")]
    [SerializeField] private float playerDamage = 15f;
    [SerializeField] private float playerCooldown = 0.6f;
    [Tooltip("Velocidad hacia arriba con la que la lava lanza a Pogoman.")]
    [SerializeField] private float launchSpeed = 11f;

    [Header("Enemigos")]
    [SerializeField] private float enemyDamage = 25f;
    [SerializeField] private float enemyCooldown = 0.5f;

    [Header("Efectos")]
    [SerializeField] private AudioClip sizzleClip;
    [SerializeField] private Material sparkMaterial;

    private float playerTimer;

    public void Configure(AudioClip sizzle, Material sparks)
    {
        sizzleClip = sizzle;
        sparkMaterial = sparks;
    }
    private float enemyTimer;

    private void Update()
    {
        playerTimer -= Time.deltaTime;
        enemyTimer -= Time.deltaTime;
    }

    private void OnCollisionEnter(Collision collision) => Burn(collision);
    private void OnCollisionStay(Collision collision) => Burn(collision);

    private void Burn(Collision collision)
    {
        Rigidbody body = collision.rigidbody;
        if (body == null)
            return;

        Vector3 point = collision.contactCount > 0 ? collision.GetContact(0).point : body.position;

        PogoPlayer player = body.GetComponent<PogoPlayer>();
        if (player != null)
        {
            if (playerTimer > 0f || PogoBridge.IsDead(player))
                return;

            playerTimer = playerCooldown;
            PlayerVitals.Damage(player, playerDamage);

            Vector3 v = body.linearVelocity;
            v.y = launchSpeed;
            body.linearVelocity = v;

            Effects(point);
            return;
        }

        Enemy enemy = body.GetComponent<Enemy>();
        if (enemy != null && enemyTimer <= 0f && !PogoBridge.IsDead(enemy))
        {
            enemyTimer = enemyCooldown;
            EnemyTracker tracker = enemy.GetComponent<EnemyTracker>();
            if (tracker != null)
                tracker.Hit(enemyDamage, HitSource.Other);
            else
                enemy.SendMessage("TakeDamage", enemyDamage, SendMessageOptions.DontRequireReceiver);
            Effects(point);
        }
    }

    private void Effects(Vector3 point)
    {
        if (sizzleClip != null)
            AudioSource.PlayClipAtPoint(sizzleClip, point, 0.9f * GameSettings.SfxVolume);

        var go = new GameObject("ChispasLava");
        go.transform.position = point;
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.duration = 0.4f;
        main.loop = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.9f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(3f, 7f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.2f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.85f, 0.2f), new Color(1f, 0.3f, 0.05f));
        main.gravityModifier = 1.2f;
        main.maxParticles = 30;
        main.stopAction = ParticleSystemStopAction.Destroy;

        var emission = ps.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 25) });

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 35f;
        go.transform.rotation = Quaternion.LookRotation(Vector3.up);

        if (sparkMaterial != null)
            go.GetComponent<ParticleSystemRenderer>().sharedMaterial = sparkMaterial;

        ps.Play();
    }
}
