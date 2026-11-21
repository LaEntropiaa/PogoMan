using UnityEngine;

// Golpe del enemigo con hitbox ajustada (sin golpes fantasma):
//  - Mide la distancia REAL entre el collider del enemigo y el de Pogoman.
//  - Si están casi tocándose, el enemigo prepara el golpe (levanta los brazos).
//  - Al terminar la preparación, solo pega si Pogoman sigue al alcance.
// Así siempre se ve venir el golpe y se puede esquivar.
// (Provisional mientras el Zombie no le quite vida al jugador por su cuenta.)
[RequireComponent(typeof(Enemy))]
public class EnemyContactDamage : MonoBehaviour
{
    [SerializeField] private float damage = 10f;
    [Tooltip("Separación máxima (m) entre los colliders para empezar a atacar.")]
    [SerializeField] private float reach = 0.35f;
    [Tooltip("Separación máxima (m) en el momento del golpe para que cuente.")]
    [SerializeField] private float hitReach = 0.5f;
    [Tooltip("Tiempo que tarda en preparar el golpe (se ve venir).")]
    [SerializeField] private float windup = 0.45f;
    [SerializeField] private float cooldown = 0.9f;

    [Header("Sonido")]
    [SerializeField] private AudioClip swingClip;

    // Invulnerabilidad compartida para que varios enemigos no maten de golpe.
    private const float PlayerInvulnerability = 0.6f;
    private static float lastPlayerHit = -100f;

    private static PogoPlayer player;
    private static Collider playerCollider;

    private Enemy enemy;
    private Collider body;
    private float windupTimer = -1f;
    private float cooldownTimer;
    private float strikeFlash;

    // 0 = nada, 0..1 = preparando, 1 = golpe (lo usa la animación).
    public float AttackProgress { get; private set; }

    private void Awake()
    {
        enemy = GetComponent<Enemy>();
        body = GetComponent<Collider>();
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        cooldownTimer -= dt;

        if (player == null)
        {
            player = FindAnyObjectByType<PogoPlayer>();
            playerCollider = player != null ? player.GetComponent<Collider>() : null;
        }

        if (PogoBridge.IsDead(enemy) || player == null || playerCollider == null || PogoBridge.IsDead(player))
        {
            windupTimer = -1f;
            AttackProgress = Mathf.MoveTowards(AttackProgress, 0f, dt * 4f);
            return;
        }

        float gap = Gap();

        if (windupTimer >= 0f)
        {
            windupTimer += dt;
            AttackProgress = Mathf.Clamp01(windupTimer / windup) * 0.85f;

            if (windupTimer >= windup)
            {
                windupTimer = -1f;
                cooldownTimer = cooldown;
                strikeFlash = 0.25f;

                if (gap <= hitReach && Time.time >= lastPlayerHit + PlayerInvulnerability)
                {
                    lastPlayerHit = Time.time;
                    player.TakeDamage(damage);
                }
            }
            return;
        }

        // Golpe: brazos abajo un momento.
        if (strikeFlash > 0f)
        {
            strikeFlash -= dt;
            AttackProgress = 1f;
            return;
        }

        AttackProgress = Mathf.MoveTowards(AttackProgress, 0f, dt * 3f);

        if (cooldownTimer <= 0f && gap <= reach)
        {
            windupTimer = 0f;
            if (swingClip != null)
                AudioSource.PlayClipAtPoint(swingClip, transform.position, 0.7f * GameSettings.SfxVolume);
        }
    }

    // Distancia entre las superficies de los dos colliders (0 si se tocan).
    private float Gap()
    {
        if (body == null)
            return Vector3.Distance(transform.position, player.transform.position);

        Vector3 onEnemy = body.ClosestPoint(playerCollider.bounds.center);
        Vector3 onPlayer = playerCollider.ClosestPoint(onEnemy);
        return Vector3.Distance(onEnemy, onPlayer);
    }
}
