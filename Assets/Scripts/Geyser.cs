using UnityEngine;

public class Geyser : MonoBehaviour
{
    [Header("Launch")]
    [SerializeField] private float launchForce = 15f;
    [SerializeField] private float cooldown = 1f;

    [Header("Detection")]
    [SerializeField] private float detectionRadius = 2f;

    private float lastLaunchTime = -Mathf.Infinity;

    private void Update()
    {
        Collider[] colliders = Physics.OverlapSphere(
            transform.position,
            detectionRadius
        );

        foreach (Collider collider in colliders)
        {
            PogoPlayer player =
                collider.GetComponent<PogoPlayer>();

            if (player == null)
                continue;

            LaunchPlayer(player);
        }
    }

    private void LaunchPlayer(PogoPlayer player)
    {
        if (Time.time < lastLaunchTime + cooldown)
            return;

        Rigidbody rb =
            player.GetComponent<Rigidbody>();

        if (rb == null)
            return;

        Vector3 velocity = rb.linearVelocity;

        velocity.y = 0f;

        rb.linearVelocity = velocity;

        rb.AddForce(
            Vector3.up * launchForce,
            ForceMode.VelocityChange
        );

        lastLaunchTime = Time.time;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;

        Gizmos.DrawWireSphere(
            transform.position,
            detectionRadius
        );
    }
}
