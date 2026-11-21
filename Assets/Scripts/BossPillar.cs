using UnityEngine;

public class BossPillar : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform player;
    [SerializeField] private Enemy bossPrefab;
    [SerializeField] private GameObject rewardPrefab;

    [Header("Pillar Positions")]
    [SerializeField] private Transform[] possiblePositions;

    [Header("Activation")]
    [SerializeField] private float activationRadius = 15f;

    [Header("Boss Spawn")]
    [SerializeField] private float bossSpawnRadius = 5f;
    [SerializeField] private float spawnHeightOffset = 0f;

    private Enemy spawnedBoss;

    private bool bossSpawned;
    private bool rewardSpawned;
    private bool placed;

    private void Start()
    {
        SelectRandomPosition();
    }

    private void Update()
    {
        if (player == null || bossPrefab == null)
            return;

        if (!bossSpawned)
        {
            CheckPlayerDistance();
            return;
        }

        if (!rewardSpawned && spawnedBoss == null)
        {
            SpawnReward();
        }
    }

    private void SelectRandomPosition()
    {
        if (possiblePositions == null ||
            possiblePositions.Length == 0)
        {
            Debug.LogError(
                $"{name}: No hay posiciones disponibles."
            );
            return;
        }

        // Elegimos una posición que no esté usando otro pilar.
        var freePositions = new System.Collections.Generic.List<Transform>();

        foreach (Transform candidate in possiblePositions)
        {
            if (candidate != null && !IsTakenByOtherPillar(candidate.position))
                freePositions.Add(candidate);
        }

        if (freePositions.Count == 0)
        {
            Debug.LogWarning($"{name}: todas las posiciones están ocupadas.");
            return;
        }

        Transform selectedPosition = freePositions[
            Random.Range(0, freePositions.Count)
        ];

        if (selectedPosition == null)
            return;

        transform.position = selectedPosition.position;
        transform.rotation = selectedPosition.rotation;
        placed = true;

        Debug.Log(
            $"{name}: pilar colocado en {selectedPosition.name}."
        );
    }

    private bool IsTakenByOtherPillar(Vector3 position)
    {
        foreach (BossPillar other in FindObjectsByType<BossPillar>())
        {
            if (other != this && other.placed &&
                Vector3.Distance(other.transform.position, position) < 1f)
                return true;
        }

        return false;
    }

    private void CheckPlayerDistance()
    {
        float distance = Vector3.Distance(
            transform.position,
            player.position
        );

        if (distance > activationRadius)
            return;

        SpawnBoss();
    }

    

    private void SpawnBoss()
    {
        if (bossSpawned)
            return;

        // Antes usaba activationRadius (15 m); el radio correcto es bossSpawnRadius.
        Vector2 randomOffset =
            Random.insideUnitCircle * bossSpawnRadius;

        Vector3 spawnPosition = transform.position +
            new Vector3(
                randomOffset.x,
                0f,
                randomOffset.y
            );

        // Pegamos al jefe al suelo (el mapa tiene escalones).
        if (Physics.Raycast(spawnPosition + Vector3.up * 4f, Vector3.down,
                out RaycastHit hit, 30f, ~0, QueryTriggerInteraction.Ignore) &&
            !hit.collider.transform.IsChildOf(transform))
            spawnPosition = hit.point;

        spawnPosition += Vector3.up * spawnHeightOffset;

        spawnedBoss = Instantiate(
            bossPrefab,
            spawnPosition,
            Quaternion.identity
        );

        spawnedBoss.SetTarget(player);

        bossSpawned = true;

        Debug.Log(
            $"{name}: jefe spawneado aleatoriamente " +
            "dentro del radio de activación."
        );
    }


    private void SpawnReward()
    {
        if (rewardSpawned)
            return;

        if (rewardPrefab == null)
        {
            Debug.LogWarning(
                $"{name}: No hay Reward Prefab asignado."
            );
            return;
        }

        Instantiate(
            rewardPrefab,
            transform.position,
            transform.rotation
        );

        rewardSpawned = true;

        Debug.Log(
            $"{name}: recompensa spawneada en el pilar."
        );

        Destroy(gameObject);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;

        Gizmos.DrawWireSphere(
            transform.position,
            activationRadius
        );
    }
}

