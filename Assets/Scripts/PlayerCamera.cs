using UnityEngine;

public class PogoCamera : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform target;

    [Header("Mouse")]
    [SerializeField] private float mouseSensitivity = 3f;
    [SerializeField] private float minPitch = -35f;
    [SerializeField] private float maxPitch = 70f;

    [Header("Distance")]
    [SerializeField] private float distance = 6f;
    [SerializeField] private float minDistance = 2f;
    [SerializeField] private float maxDistance = 10f;

    [Header("Follow")]
    [SerializeField] private float followSpeed = 12f;
    [SerializeField] private float rotationSpeed = 12f;

    [Header("Collision")]
    [SerializeField] private LayerMask collisionLayer = ~0;
    [SerializeField] private float collisionRadius = 0.2f;

    private float yaw;
    private float pitch;

    private Vector3 currentPosition;

    private void Start()
    {
        if (target == null)
        {
            Debug.LogError("PogoCamera: No target assigned.");
            enabled = false;
            return;
        }

        yaw = target.eulerAngles.y;
        pitch = 15f;

        currentPosition = transform.position;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void LateUpdate()
    {
        HandleMouse();

        FollowTarget();
    }

    private void HandleMouse()
    {
        float mouseX =
            Input.GetAxis("Mouse X") * mouseSensitivity;

        float mouseY =
            Input.GetAxis("Mouse Y") * mouseSensitivity;

        yaw += mouseX;

        pitch -= mouseY;

        pitch = Mathf.Clamp(
            pitch,
            minPitch,
            maxPitch
        );
    }

    private void FollowTarget()
    {
        Quaternion rotation =
            Quaternion.Euler(
                pitch,
                yaw,
                0f
            );

        Vector3 desiredPosition =
            target.position -
            rotation * Vector3.forward * distance;

        /*
         * Evita que la cámara atraviese paredes.
         */
        Vector3 direction =
            desiredPosition - target.position;

        float desiredDistance = direction.magnitude;

        if (desiredDistance > 0.01f)
        {
            direction.Normalize();

            if (Physics.SphereCast(
                target.position,
                collisionRadius,
                direction,
                out RaycastHit hit,
                desiredDistance,
                collisionLayer,
                QueryTriggerInteraction.Ignore))
            {
                desiredPosition =
                    target.position +
                    direction * Mathf.Max(
                        hit.distance - collisionRadius,
                        minDistance
                    );
            }
        }

        /*
         * Movimiento suave de la cámara.
         */
        currentPosition = Vector3.Lerp(
            currentPosition,
            desiredPosition,
            followSpeed * Time.deltaTime
        );

        transform.position = currentPosition;

        /*
         * La cámara mira hacia el jugador.
         */
        Quaternion targetRotation =
            Quaternion.LookRotation(
                target.position - transform.position
            );

        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            targetRotation,
            rotationSpeed * Time.deltaTime
        );
    }
}
