using System.Collections.Generic;
using UnityEngine;

public class EnemySpawner : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform player;
    [SerializeField] private Enemy enemyPrefab;

    [Header("Activation")]
    [SerializeField] private float activationDistance = 20f;

    [Header("Spawning")]
    [SerializeField] private int maxEnemies = 10;
    [SerializeField] private float spawnInterval = 3f;
    [SerializeField] private float spawnRadius = 8f;
    [Tooltip("Altura del centro del enemigo sobre el suelo.")]
    [SerializeField] private float spawnHeightOffset = 1f;

    private readonly List<Enemy> spawnedEnemies = new();

    private float spawnTimer;
    private bool warnedMissingReferences;

    private void Update()
    {
        if (player == null || enemyPrefab == null)
        {
            // Avisamos una sola vez para no llenar la consola.
            if (!warnedMissingReferences)
            {
                Debug.LogWarning($"{name}: falta Player o Enemy Prefab.");
                warnedMissingReferences = true;
            }
            return;
        }

        CleanupDestroyedEnemies();

        float distance =
            Vector3.Distance(
                transform.position,
                player.position
            );

        if (distance > activationDistance)
        {
            spawnTimer = 0f;
            return;
        }

        if (spawnedEnemies.Count >= maxEnemies)
            return;

        spawnTimer -= Time.deltaTime;

        if (spawnTimer <= 0f)
        {
            SpawnEnemy();
            spawnTimer = spawnInterval;
        }
    }

    private void SpawnEnemy()
    {
        if (spawnedEnemies.Count >= maxEnemies)
            return;

        Vector3 spawnPosition =
            GetRandomSpawnPosition();

        Enemy enemy =
            Instantiate(
                enemyPrefab,
                spawnPosition,
                Quaternion.identity
            );

        enemy.SetTarget(player);

        spawnedEnemies.Add(enemy);

        Debug.Log(
            $"EnemySpawner: enemigo creado. " +
            $"Total: {spawnedEnemies.Count}/{maxEnemies}"
        );
    }

    private Vector3 GetRandomSpawnPosition()
    {
        // Probamos algunos puntos al azar y pegamos el enemigo al suelo,
        // porque el mapa tiene escalones y si no salen enterrados o flotando.
        for (int attempt = 0; attempt < 6; attempt++)
        {
            Vector2 randomCircle = Random.insideUnitCircle * spawnRadius;

            Vector3 candidate = transform.position +
                new Vector3(randomCircle.x, 0f, randomCircle.y);

            if (TryGetGround(candidate, out Vector3 ground))
                return ground + Vector3.up * spawnHeightOffset;
        }

        return transform.position + Vector3.up * spawnHeightOffset;
    }

    private static bool TryGetGround(Vector3 point, out Vector3 ground)
    {
        // Rayo desde un poco arriba hacia abajo (no desde muy alto, para
        // no caer encima de las islas flotantes).
        Vector3 origin = point + Vector3.up * 4f;

        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 30f,
                ~0, QueryTriggerInteraction.Ignore) &&
            hit.collider.GetComponentInParent<Enemy>() == null &&
            hit.collider.GetComponentInParent<PogoPlayer>() == null)
        {
            ground = hit.point;
            return true;
        }

        ground = point;
        return false;
    }

    private void CleanupDestroyedEnemies()
    {
        spawnedEnemies.RemoveAll(
            enemy => enemy == null
        );
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.DrawWireSphere(
            transform.position,
            activationDistance
        );

        Gizmos.DrawWireSphere(
            transform.position,
            spawnRadius
        );
    }
}
