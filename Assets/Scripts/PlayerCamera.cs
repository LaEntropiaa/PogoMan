using UnityEngine;

[RequireComponent(typeof(Camera))]
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

    [Header("FOV")]
    [SerializeField] private float normalFov = 60f;
    [SerializeField] private float slideFov = 75f;
    [SerializeField] private float fovSpeed = 8f;

    [Header("Player")]
    [SerializeField] private PogoPlayer player;

    private float yaw;
    private float pitch;

    private Vector3 currentPosition;

    private Camera cam;

    private void Start()
    {
        if (target == null)
        {
            Debug.LogError(
                "PogoCamera: No target assigned."
            );

            enabled = false;
            return;
        }

        cam = GetComponent<Camera>();

        yaw = target.eulerAngles.y;
        pitch = 15f;

        currentPosition =
            transform.position;

        cam.fieldOfView =
            normalFov;

        Cursor.lockState =
            CursorLockMode.Locked;

        Cursor.visible = false;
    }

    private void LateUpdate()
    {
        HandleMouse();
        FollowTarget();
        HandleFov();
    }

    private void HandleMouse()
    {
        float mouseX =
            Input.GetAxis("Mouse X") *
            mouseSensitivity;

        float mouseY =
            Input.GetAxis("Mouse Y") *
            mouseSensitivity;

        yaw += mouseX;

        pitch -= mouseY;

        pitch =
            Mathf.Clamp(
                pitch,
                minPitch,
                maxPitch
            );
    }

    private void HandleFov()
    {
        float targetFov =
            player != null &&
            player.IsSliding
                ? slideFov
                : normalFov;

        cam.fieldOfView =
            Mathf.Lerp(
                cam.fieldOfView,
                targetFov,
                fovSpeed *
                Time.deltaTime
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
            rotation *
            Vector3.forward *
            distance;

        Vector3 direction =
            desiredPosition -
            target.position;

        float desiredDistance =
            direction.magnitude;

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
                float collisionDistance =
                    Mathf.Max(
                        hit.distance -
                        collisionRadius,
                        minDistance
                    );

                desiredPosition =
                    target.position +
                    direction *
                    collisionDistance;
            }
        }

        currentPosition =
            Vector3.Lerp(
                currentPosition,
                desiredPosition,
                followSpeed *
                Time.deltaTime
            );

        transform.position =
            currentPosition;

        Vector3 lookDirection =
            target.position -
            transform.position;

        if (lookDirection.sqrMagnitude > 0.001f)
        {
            Quaternion targetRotation =
                Quaternion.LookRotation(
                    lookDirection,
                    Vector3.up
                );

            transform.rotation =
                Quaternion.Slerp(
                    transform.rotation,
                    targetRotation,
                    rotationSpeed *
                    Time.deltaTime
                );
        }
    }
}
