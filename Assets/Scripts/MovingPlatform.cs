using UnityEngine;

public class MovingPlatform : MonoBehaviour
{
    [Header("Points")]
    [SerializeField] private Transform pointA;
    [SerializeField] private Transform pointB;

    [Header("Movement")]
    [SerializeField] private float duration = 5f;
    [SerializeField] private AnimationCurve movementCurve =
        AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    private float timer;
    private bool movingToB = true;

    private void Start()
    {
        if (pointA == null || pointB == null)
        {
            Debug.LogError(
                "MovingPlatform: Point A or Point B is not assigned."
            );

            enabled = false;
            return;
        }

        transform.position = pointA.position;
    }

    private void Update()
    {
        timer += Time.deltaTime;

        float t = Mathf.Clamp01(timer / duration);

        // Aplicamos la curva al progreso.
        float curvedT = movementCurve.Evaluate(t);

        if (movingToB)
        {
            transform.position = Vector3.Lerp(
                pointA.position,
                pointB.position,
                curvedT
            );
        }
        else
        {
            transform.position = Vector3.Lerp(
                pointB.position,
                pointA.position,
                curvedT
            );
        }

        if (timer >= duration)
        {
            timer = 0f;
            movingToB = !movingToB;
        }
    }

    private void OnDrawGizmos()
    {
        if (pointA == null || pointB == null)
            return;

        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(pointA.position, 0.2f);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(pointB.position, 0.2f);

        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(
            pointA.position,
            pointB.position
        );
    }
}
