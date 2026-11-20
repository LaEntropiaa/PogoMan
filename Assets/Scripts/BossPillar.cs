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

        Transform selectedPosition = possiblePositions[
            Random.Range(0, possiblePositions.Length)
        ];

        if (selectedPosition == null)
            return;

        transform.position = selectedPosition.position;
        transform.rotation = selectedPosition.rotation;

        Debug.Log(
            $"{name}: pilar colocado en {selectedPosition.name}."
        );
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

        Vector2 randomOffset =
            Random.insideUnitCircle * activationRadius;

        Vector3 spawnPosition = transform.position +
            new Vector3(
                randomOffset.x,
                spawnHeightOffset,
                randomOffset.y
            );

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

