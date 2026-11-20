using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class Enemy : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Transform target;

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 3.0f;

    private Rigidbody rb;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    private void FixedUpdate()
    {
        if (target == null)
            return;

        FollowTarget();
    }

    private void FollowTarget()
    {
        // Dirección hacia el jugador.
        Vector3 direction = target.position - rb.position;

        // El enemigo solamente persigue sobre el suelo.
        direction.y = 0.0f;

        if (direction.sqrMagnitude < 0.001f)
            return;

        direction.Normalize();

        // Movimiento a velocidad constante.
        Vector3 movement = direction * moveSpeed * Time.fixedDeltaTime;

        rb.MovePosition(rb.position + movement);
    }
}
