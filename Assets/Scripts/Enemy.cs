using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public abstract class Enemy : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] protected Transform target;

    [Header("Movement")]
    [SerializeField] protected float moveSpeed = 3.0f;

    protected Rigidbody rb;

    protected virtual void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    protected virtual void FixedUpdate()
    {
        if (target == null)
            return;

        FollowTarget();
    }

    protected void FollowTarget()
    {
        Vector3 direction = target.position - rb.position;

        // El enemigo solamente se mueve horizontalmente.
        direction.y = 0.0f;

        if (direction.sqrMagnitude < 0.001f)
            return;

        direction.Normalize();

        Vector3 movement =
            direction * moveSpeed * Time.fixedDeltaTime;

        rb.MovePosition(rb.position + movement);
    }

    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
    }
}
