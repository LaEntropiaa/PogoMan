using UnityEngine;

public class Door : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private KeyManager keyManager;
    [SerializeField] private Transform doorModel;

    [Header("Opening")]
    [SerializeField] private Vector3 openRotation =
        new Vector3(0f, 90f, 0f);

    [SerializeField] private float openSpeed = 90f;

    private Quaternion targetRotation;
    private bool isOpen;

    private void Start()
    {
        if (keyManager == null)
            keyManager = FindFirstObjectByType<KeyManager>();

        if (doorModel == null)
            doorModel = transform;

        targetRotation =
            doorModel.localRotation *
            Quaternion.Euler(openRotation);
    }

    private void Update()
    {
        if (!isOpen)
        {
            if (keyManager != null &&
                keyManager.AllKeysCollected)
            {
                isOpen = true;
                Debug.Log("¡Todas las llaves recolectadas! Puerta abierta.");
            }

            return;
        }

        doorModel.localRotation =
            Quaternion.RotateTowards(
                doorModel.localRotation,
                targetRotation,
                openSpeed * Time.deltaTime
            );
    }
}
