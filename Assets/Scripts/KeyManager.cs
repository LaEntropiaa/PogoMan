using UnityEngine;

public class KeyManager : MonoBehaviour
{
    [Header("Keys")]
    [SerializeField] private int requiredKeys = 3;

    private int collectedKeys;

    public int CollectedKeys => collectedKeys;
    public bool AllKeysCollected =>
        collectedKeys >= requiredKeys;

    public void CollectKey()
    {
        if (AllKeysCollected)
            return;

        collectedKeys++;

        Debug.Log(
            $"Llaves recolectadas: {collectedKeys}/{requiredKeys}"
        );
    }
}

