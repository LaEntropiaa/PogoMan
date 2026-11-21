using UnityEngine;

// Daño provisional por contacto: si el enemigo está pegado a Pogoman, le quita vida.
// (El ataque del Zombie todavía no le quita vida al jugador; cuando lo haga,
// se puede quitar este componente del prefab.)
[RequireComponent(typeof(Enemy))]
public class EnemyContactDamage : MonoBehaviour
{
    [SerializeField] private float damage = 10f;
    [SerializeField] private float range = 1.6f;
    [SerializeField] private float cooldown = 1f;

    // Tiempo de invulnerabilidad compartido para que varios enemigos no maten de golpe.
    private const float PlayerInvulnerability = 0.5f;
    private static float lastPlayerHit = -100f;
    private static PogoPlayer cachedPlayer;

    private Enemy enemy;
    private Collider body;
    private float timer;

    private void Awake()
    {
        enemy = GetComponent<Enemy>();
        body = GetComponent<Collider>();
    }

    private void Update()
    {
        timer -= Time.deltaTime;
        if (timer > 0f || PogoBridge.IsDead(enemy))
            return;

        if (cachedPlayer == null)
            cachedPlayer = FindAnyObjectByType<PogoPlayer>();
        if (cachedPlayer == null || PogoBridge.IsDead(cachedPlayer))
            return;

        Vector3 from = body != null ? body.ClosestPoint(cachedPlayer.transform.position) : transform.position;
        if (Vector3.Distance(from, cachedPlayer.transform.position) > range)
            return;

        if (Time.time < lastPlayerHit + PlayerInvulnerability)
            return;

        lastPlayerHit = Time.time;
        timer = cooldown;
        cachedPlayer.TakeDamage(damage);
    }
}
