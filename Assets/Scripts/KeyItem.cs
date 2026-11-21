using UnityEngine;

public class KeyItem : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private KeyManager keyManager;
    [SerializeField] private Transform player;

    [Header("Collection")]
    [SerializeField] private float collectionRadius = 1.5f;

    private bool collected;

    private void Start()
    {
        if (player == null)
        {
            PogoPlayer pogo =
                FindFirstObjectByType<PogoPlayer>();

            if (pogo != null)
                player = pogo.transform;
        }

        if (keyManager == null)
        {
            keyManager =
                FindFirstObjectByType<KeyManager>();
        }
    }

    private void Update()
    {
        if (collected || player == null || keyManager == null)
            return;

        float distance = Vector3.Distance(
            transform.position,
            player.position
        );

        if (distance <= collectionRadius)
        {
            collected = true;

            keyManager.CollectKey();

            Destroy(gameObject);
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;

        Gizmos.DrawWireSphere(
            transform.position,
            collectionRadius
        );
    }
}
