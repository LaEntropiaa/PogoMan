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

    private readonly List<Enemy> spawnedEnemies = new();

    private float spawnTimer;

    private void Update()
    {
        if (player == null || enemyPrefab == null)
        {
            Debug.LogWarning("Spawner: falta Player o Enemy Prefab.");
            return;
        }

        CleanupDestroyedEnemies();

        float distance =
            Vector3.Distance(
                transform.position,
                player.position
            );

        Debug.Log(
            $"Spawner | Distancia: {distance:F1} | " +
            $"Enemigos: {spawnedEnemies.Count}/{maxEnemies} | " +
            $"Timer: {spawnTimer:F1}"
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
        Vector2 randomCircle =
            Random.insideUnitCircle *
            spawnRadius;

        return transform.position +
            new Vector3(
                randomCircle.x,
                0f,
                randomCircle.y
            );
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
